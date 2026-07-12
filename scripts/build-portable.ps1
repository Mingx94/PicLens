[CmdletBinding()]
param(
    [string]$BuildDirectory = "",
    [string]$OutputDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$qtRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = $qtRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\qt-portable"))
if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    $BuildDirectory = Join-Path $qtRoot "build\release"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactRoot "PicLens-win-x64"
}

$buildDirectoryPath = [IO.Path]::GetFullPath($BuildDirectory)
$outputDirectoryPath = [IO.Path]::GetFullPath($OutputDirectory)
$artifactPrefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $outputDirectoryPath.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Portable output must stay below $artifactRoot"
}

$sourceExecutable = Join-Path $buildDirectoryPath "bin\PicLens.exe"
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw "Release executable was not found: $sourceExecutable"
}

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class PicLensWindowsResourceAudit
{
    private const uint LoadLibraryAsDataFile = 0x00000002;
    private static readonly IntPtr AppIconId = new IntPtr(101);
    private static readonly IntPtr GroupIconType = new IntPtr(14);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string fileName, IntPtr file, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindResourceW(IntPtr module, IntPtr name, IntPtr type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);

    public static bool HasApplicationIcon(string executablePath)
    {
        IntPtr module = LoadLibraryExW(executablePath, IntPtr.Zero, LoadLibraryAsDataFile);
        if (module == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return FindResourceW(module, AppIconId, GroupIconType) != IntPtr.Zero;
        }
        finally
        {
            FreeLibrary(module);
        }
    }
}
"@
if (-not [PicLensWindowsResourceAudit]::HasApplicationIcon($sourceExecutable)) {
    throw "Release executable does not contain the expected native Windows app icon: $sourceExecutable"
}
$deployTool = (Get-Command windeployqt -ErrorAction Stop).Source

if (Test-Path -LiteralPath $outputDirectoryPath) {
    Remove-Item -LiteralPath $outputDirectoryPath -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDirectoryPath -Force | Out-Null
$deployedExecutable = Join-Path $outputDirectoryPath "PicLens.exe"
Copy-Item -LiteralPath $sourceExecutable -Destination $deployedExecutable
Copy-Item -LiteralPath (Join-Path $qtRoot "qt.conf") -Destination $outputDirectoryPath
$assetDirectory = Join-Path $outputDirectoryPath "Assets"
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
Copy-Item `
    -LiteralPath (Join-Path $repoRoot "assets\AppIcon.ico") `
    -Destination (Join-Path $assetDirectory "AppIcon.ico")

Write-Host "==> Deploying Qt runtime and QML imports"
$qtBin = Split-Path $deployTool -Parent
$qtPrefix = [IO.Path]::GetFullPath((Join-Path $qtBin ".."))
$qtHelperBin = @(
    $qtBin,
    (Join-Path $qtPrefix "libexec"),
    (Join-Path $qtPrefix "share\qt6\bin")
) | Where-Object {
    Test-Path -LiteralPath (Join-Path $_ "qmlimportscanner.exe") -PathType Leaf
} | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($qtHelperBin)) {
    throw "qmlimportscanner was not found below the selected Qt toolchain: $qtPrefix"
}
$previousPath = $env:PATH
try {
    $env:PATH = "$qtHelperBin;$qtBin;$previousPath"
    & $deployTool `
        --release `
        --qmldir (Join-Path $qtRoot "qml") `
        --dir $outputDirectoryPath `
        --compiler-runtime `
        --no-translations `
        --no-system-dxc-compiler `
        --no-quickcontrols2fluentwinui3styleimpl `
        --no-quickcontrols2fusion `
        --no-quickcontrols2fusionstyleimpl `
        --no-quickcontrols2imagine `
        --no-quickcontrols2imaginestyleimpl `
        --no-quickcontrols2material `
        --no-quickcontrols2materialstyleimpl `
        --no-quickcontrols2universal `
        --no-quickcontrols2universalstyleimpl `
        --no-quickcontrols2windowsstyleimpl `
        --verbose 0 `
        --include-plugins "qoffscreen" `
        --skip-plugin-types "qmltooling,generic" `
        $deployedExecutable
    if ($LASTEXITCODE -ne 0) {
        throw "windeployqt failed with exit code $LASTEXITCODE"
    }
}
finally {
    $env:PATH = $previousPath
}

# qmlimportscanner deploys every style referenced by the generic Controls
# module even when the application pins Qt Quick Controls to Basic. Remove the
# unused style modules before resolving the native dependency closure; the
# packaged smoke below verifies that the retained Basic style is self-contained.
$qtQuickRoot = Join-Path $outputDirectoryPath "qml\QtQuick"
$unusedStyleDirectories = @(
    "Controls\FluentWinUI3",
    "Controls\Fusion",
    "Controls\Imagine",
    "Controls\Material",
    "Controls\Universal",
    "Controls\Windows",
    "NativeStyle"
)
foreach ($relativeStyleDirectory in $unusedStyleDirectories) {
    $styleDirectory = Join-Path $qtQuickRoot $relativeStyleDirectory
    if (Test-Path -LiteralPath $styleDirectory -PathType Container) {
        Remove-Item -LiteralPath $styleDirectory -Recurse -Force
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $qtQuickRoot "Controls\Basic") -PathType Container)) {
    throw "The required Qt Quick Controls Basic style was not deployed"
}

# MSYS2's windeployqt currently does not copy the UCRT64 MinGW runtime even
# when --compiler-runtime is requested. Official MSVC Qt builds do not have
# these files and rely on windeployqt's compiler-runtime handling instead.
$mingwRuntimeNames = @("libgcc_s_seh-1.dll", "libstdc++-6.dll", "libwinpthread-1.dll")
$usesMingwRuntime = Test-Path -LiteralPath (Join-Path $qtBin "libwinpthread-1.dll") -PathType Leaf
if ($usesMingwRuntime) {
    foreach ($runtimeName in $mingwRuntimeNames) {
        $runtimeSource = Join-Path $qtBin $runtimeName
        if (-not (Test-Path -LiteralPath $runtimeSource -PathType Leaf)) {
            throw "Required MinGW runtime was not found: $runtimeSource"
        }
        Copy-Item -LiteralPath $runtimeSource -Destination $outputDirectoryPath
    }
}

# MSYS2 also builds Qt against shared UCRT64 libraries (ICU, HarfBuzz, zstd,
# and others). Resolve the PE dependency closure so the artifact cannot borrow
# those DLLs from the developer PATH.
$copiedDependencies = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
if ($usesMingwRuntime) {
    $objdumpTool = (Get-Command objdump -ErrorAction Stop).Source
    for ($pass = 0; $pass -lt 12; $pass += 1) {
    $addedThisPass = 0
    $dependencyNames = Get-ChildItem -LiteralPath $outputDirectoryPath -Recurse -File |
        Where-Object { $_.Extension -in ".exe", ".dll" } |
        ForEach-Object {
            & $objdumpTool -p $_.FullName 2>$null | ForEach-Object {
                if ($_ -match "DLL Name:\s*(.+)$") {
                    $Matches[1].Trim()
                }
            }
        } |
        Sort-Object -Unique
    foreach ($dependencyName in $dependencyNames) {
        if ($dependencyName.StartsWith("api-ms-", [StringComparison]::OrdinalIgnoreCase) -or
            $dependencyName.StartsWith("ext-ms-", [StringComparison]::OrdinalIgnoreCase) -or
            (Test-Path -LiteralPath (Join-Path $outputDirectoryPath $dependencyName)) -or
            (Test-Path -LiteralPath (Join-Path $env:SystemRoot "System32\$dependencyName"))) {
            continue
        }
        $dependencySource = Join-Path $qtBin $dependencyName
        if (-not (Test-Path -LiteralPath $dependencySource -PathType Leaf)) {
            throw "Unresolved non-system runtime dependency: $dependencyName"
        }
        Copy-Item -LiteralPath $dependencySource -Destination $outputDirectoryPath
        [void]$copiedDependencies.Add($dependencyName)
        $addedThisPass += 1
    }
        if ($addedThisPass -eq 0) {
            break
        }
        if ($pass -eq 11) {
            throw "Runtime dependency closure did not converge"
        }
    }
}
Write-Host "Additional MSYS2 runtime DLLs: $($copiedDependencies.Count)"

$licenseRoot = Join-Path $outputDirectoryPath "licenses"
New-Item -ItemType Directory -Path $licenseRoot -Force | Out-Null
$msysLicenseRoot = Join-Path $qtPrefix "share\licenses"
if ((Test-Path -LiteralPath (Join-Path $msysLicenseRoot "qt6-base") -PathType Container) -and
    (Test-Path -LiteralPath (Join-Path $msysLicenseRoot "qt6-declarative") -PathType Container)) {
    foreach ($qtLicensePackage in @("qt6-base", "qt6-declarative")) {
        Copy-Item `
            -LiteralPath (Join-Path $msysLicenseRoot $qtLicensePackage) `
            -Destination $licenseRoot `
            -Recurse
    }
}
elseif (Test-Path -LiteralPath (Join-Path $qtPrefix "LICENSES") -PathType Container) {
    Copy-Item `
        -LiteralPath (Join-Path $qtPrefix "LICENSES") `
        -Destination (Join-Path $licenseRoot "Qt") `
        -Recurse
}
else {
    $qtInstallRoot = [IO.Path]::GetFullPath((Join-Path $qtPrefix "..\.."))
    $sourceLicenseDirectories = @(Get-ChildItem `
        -LiteralPath $qtInstallRoot `
        -Directory `
        -Filter LICENSES `
        -Recurse `
        -ErrorAction SilentlyContinue | Where-Object {
            $_.Parent.Name -in @("qtbase", "qtdeclarative") -and
            $_.Parent.Parent.Name -eq "Src"
        })
    $sourceModules = @($sourceLicenseDirectories | ForEach-Object { $_.Parent.Name } | Sort-Object -Unique)
    if ($sourceModules.Count -ne 2) {
        throw "Qt base/declarative license texts were not found below the selected Qt installation: $qtInstallRoot"
    }
    $qtLicenseRoot = Join-Path $licenseRoot "Qt"
    New-Item -ItemType Directory -Path $qtLicenseRoot -Force | Out-Null
    foreach ($sourceLicenseDirectory in $sourceLicenseDirectories) {
        Copy-Item `
            -LiteralPath $sourceLicenseDirectory.FullName `
            -Destination (Join-Path $qtLicenseRoot $sourceLicenseDirectory.Parent.Name) `
            -Recurse
    }
}
Copy-Item `
    -LiteralPath (Join-Path $repoRoot "LICENSE") `
    -Destination (Join-Path $outputDirectoryPath "LICENSE.txt")
Copy-Item `
    -LiteralPath (Join-Path $qtRoot "THIRD_PARTY_NOTICES.txt") `
    -Destination (Join-Path $outputDirectoryPath "THIRD_PARTY_NOTICES.txt")

Write-Host "==> Running packaged offscreen smoke"
$smokeData = Join-Path $artifactRoot ".smoke-data"
if (Test-Path -LiteralPath $smokeData) {
    Remove-Item -LiteralPath $smokeData -Recurse -Force
}
try {
    $processInfo = [Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $deployedExecutable
    $processInfo.Arguments = '--smoke-ms 750 --data-root "{0}" --folder "{1}"' -f `
        $smokeData, (Join-Path $repoRoot "assets")
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.Environment["PATH"] = "$env:SystemRoot\System32;$env:SystemRoot"
    $processInfo.Environment["QT_QPA_PLATFORM"] = "offscreen"
    $smokeProcess = [Diagnostics.Process]::Start($processInfo)
    if (-not $smokeProcess.WaitForExit(20000)) {
        $smokeProcess.Kill($true)
        throw "Packaged smoke timed out after 20 seconds"
    }
    if ($smokeProcess.ExitCode -ne 0) {
        throw "Packaged smoke failed with exit code $($smokeProcess.ExitCode)"
    }
}
finally {
    if (Test-Path -LiteralPath $smokeData) {
        Remove-Item -LiteralPath $smokeData -Recurse -Force
    }
}

# qoffscreen is included only so the packaged smoke can run without a desktop.
# It is not part of the production payload.
$offscreenPlugin = Join-Path $outputDirectoryPath "platforms\qoffscreen.dll"
if (-not (Test-Path -LiteralPath $offscreenPlugin -PathType Leaf)) {
    throw "Packaged smoke dependency was not deployed: $offscreenPlugin"
}
Remove-Item -LiteralPath $offscreenPlugin -Force

$files = Get-ChildItem -LiteralPath $outputDirectoryPath -File -Recurse
$totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
$sha256 = (Get-FileHash -LiteralPath $deployedExecutable -Algorithm SHA256).Hash
Write-Host "Portable output: $outputDirectoryPath"
Write-Host "Files: $($files.Count)"
Write-Host "Bytes: $totalBytes"
Write-Host "PicLens.exe SHA256: $sha256"
