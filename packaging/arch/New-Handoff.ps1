# Requires PowerShell 7, Git and Python 3. No install, commit or network access.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
& $Python (Join-Path $PSScriptRoot 'handoff.py') --output $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Handoff failed ($LASTEXITCODE)." }
