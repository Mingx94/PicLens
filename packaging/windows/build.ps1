[CmdletBinding()]
param([switch]$SkipMsi)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$appRoot = Join-Path $repo 'apps/windows'
[xml]$props = Get-Content -LiteralPath (Join-Path $appRoot 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Windows version' }
$distRoot = Join-Path $repo 'dist'
$payload = Join-Path $distRoot ('wpf-payload-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Push-Location $appRoot
try {
    dotnet publish src/PicLens.App/PicLens.App.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o $payload --nologo
    if ($LASTEXITCODE -ne 0) { throw 'WPF publish failed' }
    dotnet publish src/PicLens.Worker/PicLens.Worker.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o (Join-Path $payload 'worker') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Worker publish failed' }
    Copy-Item -LiteralPath (Join-Path $repo 'LICENSE'),(Join-Path $repo 'assets/AppIcon.ico'),(Join-Path $appRoot 'README.md') -Destination $payload
    $notices = Join-Path $payload 'licenses'
    New-Item -ItemType Directory -Path $notices -Force | Out-Null
    $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    Copy-Item -LiteralPath (Join-Path $nugetRoot 'skiasharp/3.119.4/LICENSE.txt') -Destination (Join-Path $notices 'SkiaSharp-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $nugetRoot 'skiasharp.nativeassets.win32/3.119.4/THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $notices 'Skia-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $appRoot 'THIRD-PARTY.md') -Destination $notices
    $runtime = Get-Content -Raw -LiteralPath (Join-Path $payload 'PicLens.runtimeconfig.json') | ConvertFrom-Json
    foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
        $package = Join-Path $nugetRoot ($framework.name.ToLowerInvariant() + '.runtime.win-x64/' + $framework.version)
        $license = if (Test-Path -LiteralPath (Join-Path $package 'LICENSE.TXT')) { Join-Path $package 'LICENSE.TXT' } else { Join-Path $package 'LICENSE' }
        Copy-Item -LiteralPath $license -Destination (Join-Path $notices ($framework.name + '-LICENSE.txt'))
        $thirdParty = Join-Path $package 'THIRD-PARTY-NOTICES.TXT'
        if (Test-Path -LiteralPath $thirdParty) { Copy-Item -LiteralPath $thirdParty -Destination (Join-Path $notices ($framework.name + '-NOTICES.txt')) }
    }
    # The App project reference can contribute helper launch stubs to the root.
    # The complete, self-contained helper is owned by the worker subdirectory.
    Get-ChildItem -LiteralPath $payload -File -Filter 'PicLens.Worker.*' | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    $name = "PicLens-$version-windows-x86_64"
    $zip = Join-Path $distRoot "$name.zip"
    Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -Force
    $outputs = @($zip)
    if (-not $SkipMsi) {
        dotnet build (Join-Path $PSScriptRoot 'PicLens.wixproj') -c Release -t:Rebuild "/p:AppVersion=$version" "/p:PayloadDir=$payload" "/p:OutputPath=$distRoot\" "/p:OutputName=$name" --nologo
        if ($LASTEXITCODE -ne 0) { throw 'MSI build failed' }
        $outputs += Join-Path $distRoot "$name.msi"
    }
    foreach ($artifact in $outputs) {
        $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($artifact))" | Set-Content -LiteralPath "$artifact.sha256" -Encoding ascii
        Write-Output $artifact
    }
    Write-Output "Payload: $payload"
    Write-Output 'Signing: unsigned'
} finally { Pop-Location }
