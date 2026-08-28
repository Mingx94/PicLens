[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$Sign,
    [string]$CertificateThumbprint = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($Version)) {
    $metadata = cargo metadata --no-deps --format-version 1 | ConvertFrom-Json
    $Version = ($metadata.packages | Where-Object name -eq "piclens-gpui").version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid Cargo package version: $Version" }
if ($Sign -and [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw "-Sign requires -CertificateThumbprint"
}

Push-Location $repoRoot
try {
    cargo build --release --locked -p piclens-gpui
    if ($LASTEXITCODE -ne 0) { throw "Release build failed" }
    $payload = Join-Path $repoRoot "dist\msi-payload"
    New-Item -ItemType Directory -Force -Path $payload | Out-Null
    Copy-Item target/release/piclens-gpui.exe (Join-Path $payload PicLens.exe) -Force
    Copy-Item LICENSE, README.md, assets/Fonts/NotoSansCJKtc-OFL.txt, assets/AppIcon.ico $payload -Force
    $output = Join-Path $repoRoot "dist\PicLens-$Version-windows-x86_64.msi"
    dotnet build installer/PicLens.wixproj --configuration Release --no-incremental `
        "/p:AppVersion=$Version" "/p:PayloadDir=$payload" "/p:OutputPath=$repoRoot\dist\" `
        "/p:OutputName=PicLens-$Version-windows-x86_64" "/p:SuppressValidation=true"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw "MSI build failed" }
    if ($Sign) {
        $signTool = (Get-Command signtool.exe -ErrorAction Stop).Source
        & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $output
        if ($LASTEXITCODE -ne 0) { throw "MSI signing failed" }
    }
    $hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($output))" | Set-Content "$output.sha256" -Encoding ascii
    Write-Host "MSI ready: $output"
    Write-Host "Signing: $(if ($Sign) { 'signed and timestamped' } else { 'unsigned' })"
}
finally { Pop-Location }
