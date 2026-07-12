[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath,
    [Parameter(Mandatory = $true)]
    [string]$PayloadDirectory,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,
    [switch]$RequireSigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedMsi = (Resolve-Path -LiteralPath $MsiPath).Path
$resolvedPayload = (Resolve-Path -LiteralPath $PayloadDirectory).Path
$windowsInstaller = New-Object -ComObject WindowsInstaller.Installer
$database = $windowsInstaller.GetType().InvokeMember(
    "OpenDatabase",
    "InvokeMethod",
    $null,
    $windowsInstaller,
    @($resolvedMsi, 0))

function Invoke-MsiQuery([string]$Sql) {
    $view = $database.GetType().InvokeMember("OpenView", "InvokeMethod", $null, $database, @($Sql))
    try {
        [void]$view.GetType().InvokeMember("Execute", "InvokeMethod", $null, $view, $null)
        $rows = @()
        while ($true) {
            $record = $view.GetType().InvokeMember("Fetch", "InvokeMethod", $null, $view, $null)
            if ($null -eq $record) {
                break
            }
            $fieldCount = $record.GetType().InvokeMember("FieldCount", "GetProperty", $null, $record, $null)
            $values = for ($field = 1; $field -le $fieldCount; $field += 1) {
                $record.GetType().InvokeMember("StringData", "GetProperty", $null, $record, @($field))
            }
            $rows += ,[string[]]$values
        }
        return $rows
    }
    finally {
        [void]$view.GetType().InvokeMember("Close", "InvokeMethod", $null, $view, $null)
    }
}

$properties = @{}
foreach ($row in Invoke-MsiQuery "SELECT ``Property``,``Value`` FROM ``Property``") {
    $properties[$row[0]] = $row[1]
}
if ($properties["ProductName"] -ne "PicLens") {
    throw "Unexpected MSI ProductName: $($properties['ProductName'])"
}
if ($properties["ProductVersion"] -ne $ExpectedVersion) {
    throw "Unexpected MSI ProductVersion: $($properties['ProductVersion'])"
}
if ($properties["UpgradeCode"] -ne "{4B3899A4-2E9E-4B4F-9CF5-36F8D8D6767D}") {
    throw "The MSI upgrade identity changed unexpectedly: $($properties['UpgradeCode'])"
}
$upgradeRows = @(Invoke-MsiQuery "SELECT ``UpgradeCode``,``Attributes``,``ActionProperty`` FROM ``Upgrade``")
$upgradeIdentity = $properties["UpgradeCode"]
$matchingUpgradeRows = @($upgradeRows | Where-Object {
    $_[0] -eq $upgradeIdentity -and $_[2] -eq "WIX_UPGRADE_DETECTED"
})
if ($matchingUpgradeRows.Count -eq 0) {
    throw "The MSI does not contain a major-upgrade detection rule for $upgradeIdentity"
}

$fileRows = Invoke-MsiQuery "SELECT ``FileName``,``FileSize`` FROM ``File``"
$msiFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$msiBytes = 0L
foreach ($row in $fileRows) {
    $parts = $row[0].Split('|')
    [void]$msiFileNames.Add($parts[$parts.Length - 1])
    $msiBytes += [int64]$row[1]
}
$payloadFiles = Get-ChildItem -LiteralPath $resolvedPayload -Recurse -File |
    Where-Object Extension -ne ".pdb"
$payloadBytes = ($payloadFiles | Measure-Object -Property Length -Sum).Sum
if ($fileRows.Count -ne $payloadFiles.Count) {
    throw "MSI payload count mismatch. MSI=$($fileRows.Count); Portable=$($payloadFiles.Count)"
}
if ($msiBytes -ne $payloadBytes) {
    throw "MSI payload byte mismatch. MSI=$msiBytes; Portable=$payloadBytes"
}

$requiredFiles = @(
    "PicLens.exe",
    "qt.conf",
    "Qt6Core.dll",
    "qwindows.dll",
    "LICENSE.txt",
    "THIRD_PARTY_NOTICES.txt",
    "AppIcon.ico"
)
foreach ($requiredFile in $requiredFiles) {
    if (-not $msiFileNames.Contains($requiredFile)) {
        throw "Required MSI payload file is missing: $requiredFile"
    }
}
$compilerRuntimePresent = @(@(
    "libwinpthread-1.dll",
    "vcruntime140.dll",
    "vcruntime140_1.dll",
    "vc_redist.x64.exe"
) | Where-Object { $msiFileNames.Contains($_) })
if ($compilerRuntimePresent.Count -eq 0) {
    throw "No supported MinGW or MSVC compiler runtime was found in the MSI payload"
}

$shortcuts = Invoke-MsiQuery "SELECT ``Name``,``Target``,``WkDir`` FROM ``Shortcut``"
if ($shortcuts.Count -ne 3 -or
    $shortcuts[0] -ne "PicLens" -or
    $shortcuts[1] -ne "[INSTALLFOLDER]PicLens.exe" -or
    $shortcuts[2] -ne "INSTALLFOLDER") {
    throw "The expected PicLens Start Menu shortcut is missing"
}
$registryRows = @(Invoke-MsiQuery "SELECT ``Root``,``Key``,``Name`` FROM ``Registry``")
$machineRegistration = @($registryRows | Where-Object {
    $_[0] -eq "2" -and $_[1] -eq "Software\PicLens" -and $_[2] -eq "installed"
})
if ($machineRegistration.Count -ne 1) {
    throw "The per-machine shortcut component must use the HKLM PicLens key path"
}


# Expand an administrative image and compare the actual MSI contents against
# the portable payload. This catches misplaced and duplicated same-name files,
# which a file-count/total-byte comparison cannot detect.
$auditRoot = Join-Path ([IO.Path]::GetTempPath()) ("PicLens-msi-audit-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $auditRoot -Force | Out-Null
try {
    $process = Start-Process -FilePath "msiexec.exe" -ArgumentList @(
        "/a",
        ('"{0}"' -f $resolvedMsi),
        "/qn",
        "/norestart",
        ('TARGETDIR="{0}"' -f $auditRoot)
    ) -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -notin @(0, 3010)) {
        throw "MSI administrative extraction failed with exit code $($process.ExitCode)"
    }

    $executableCandidates = @(Get-ChildItem -LiteralPath $auditRoot -Filter "PicLens.exe" -File -Recurse |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.DirectoryName "qt.conf") -PathType Leaf })
    if ($executableCandidates.Count -ne 1) {
        throw "Expected one extracted PicLens payload root, found $($executableCandidates.Count)"
    }
    $extractedPayload = $executableCandidates[0].Directory.FullName

    function Get-ContentManifest([string]$Root) {
        $rootPrefix = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $manifest = @{}
        foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse | Where-Object Extension -ne ".pdb") {
            $relativePath = $file.FullName.Substring($rootPrefix.Length).Replace('\', '/')
            $manifest[$relativePath] = [pscustomobject]@{
                Length = $file.Length
                SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            }
        }
        return $manifest
    }

    $portableManifest = Get-ContentManifest $resolvedPayload
    $msiManifest = Get-ContentManifest $extractedPayload
    $missing = @($portableManifest.Keys | Where-Object { -not $msiManifest.ContainsKey($_) } | Sort-Object)
    $unexpected = @($msiManifest.Keys | Where-Object { -not $portableManifest.ContainsKey($_) } | Sort-Object)
    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        throw "MSI relative-path mismatch. Missing=[$($missing -join ', ')]; Unexpected=[$($unexpected -join ', ')]"
    }
    foreach ($relativePath in $portableManifest.Keys) {
        $portableEntry = $portableManifest[$relativePath]
        $msiEntry = $msiManifest[$relativePath]
        if ($portableEntry.Length -ne $msiEntry.Length -or $portableEntry.SHA256 -ne $msiEntry.SHA256) {
            throw "MSI content mismatch: $relativePath"
        }
    }

    if ($RequireSigned) {
        foreach ($signedFile in @($resolvedMsi, $executableCandidates[0].FullName)) {
            $signature = Get-AuthenticodeSignature -LiteralPath $signedFile
            if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid) {
                throw "Required Authenticode signature is not valid: $signedFile ($($signature.Status))"
            }
        }
    }
}
finally {
    Remove-Item -LiteralPath $auditRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "MSI database audit passed"
Write-Host "  Version: $ExpectedVersion"
Write-Host "  Files:   $($fileRows.Count)"
Write-Host "  Bytes:   $msiBytes"
Write-Host "  Upgrade: $($properties['UpgradeCode'])"
Write-Host "  Major upgrade rule: present"
Write-Host "  Relative paths, sizes, SHA256: exact match"
Write-Host "  Authenticode required: $RequireSigned"
