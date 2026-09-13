[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packagePath = Join-Path $PSScriptRoot 'Package.wxs'
[xml]$document = Get-Content -Raw -LiteralPath $packagePath
$namespace = [Xml.XmlNamespaceManager]::new($document.NameTable)
$namespace.AddNamespace('w', 'http://wixtoolset.org/schemas/v4/wxs')

function Require-Node([string]$XPath, [string]$Message) {
    $node = $document.SelectSingleNode($XPath, $namespace)
    if ($null -eq $node) { throw $Message }
    $node
}

$package = Require-Node '/w:Wix/w:Package' '找不到 WiX Package。'
if ($package.Scope -ne 'perUserOrMachine') { throw 'MSI 必須支援目前使用者與所有使用者兩種安裝範圍。' }
if ($package.UpgradeCode -ne '{4B3899A4-2E9E-4B4F-9CF5-36F8D8D6767D}') { throw 'UpgradeCode 不可變更。' }
if ($package.Codepage -ne 'UTF-8') { throw '含繁體中文介面的 MSI 必須使用 UTF-8 database code page。' }

$shortcutMarker = Require-Node '//w:RegistryValue[@Key="Software\PicLens" and @Name="installed"]' '找不到開始功能表捷徑的登錄 key path。'
if ($shortcutMarker.Root -ne 'HKMU') { throw '開始功能表捷徑 key path 必須跟隨 MSI 安裝範圍。' }

$scopeMarker = Require-Node '//w:RegistryValue[@Key="Software\PicLens" and @Name="installScope"]' '找不到安裝範圍登錄標記。'
if ($scopeMarker.Root -ne 'HKMU') { throw '安裝範圍標記必須跟隨 MSI 安裝範圍。' }
Require-Node '//w:Feature[@Id="Main"]/w:ComponentRef[@Id="InstallScopeMarker"]' '主要功能未包含安裝範圍標記。' | Out-Null
Require-Node '//w:Property[@Id="PICLENS_EXISTING_PER_MACHINE"]/w:RegistrySearch[@Root="HKLM" and @Name="installScope" and @Type="raw"]' '找不到所有使用者安裝範圍搜尋。' | Out-Null
Require-Node '//w:Property[@Id="PICLENS_EXISTING_PER_USER"]/w:RegistrySearch[@Root="HKCU" and @Name="installScope" and @Type="raw"]' '找不到目前使用者安裝範圍搜尋。' | Out-Null
Require-Node '//w:Property[@Id="PICLENS_EXISTING_LEGACY_PER_MACHINE"]/w:DirectorySearch[@Path="[ProgramFiles64Folder]PicLens"]/w:FileSearch[@Name="PicLens.exe"]' '找不到舊版所有使用者安裝搜尋。' | Out-Null

$scopeProperty = Require-Node '//w:Property[@Id="PICLENS_INSTALL_SCOPE"]' '找不到安裝範圍選擇屬性。'
if ($scopeProperty.Value -ne 'perUser') { throw '新安裝必須預設只為目前使用者安裝。' }

Require-Node '//w:Dialog[@Id="PicLensInstallScopeDlg"]' '找不到安裝範圍選擇畫面。' | Out-Null
Require-Node '//w:Dialog[@Id="PicLensInstallScopeDlg"]//w:Publish[@Property="ALLUSERS" and @Value="2"]' '目前使用者選項未設定 ALLUSERS=2。' | Out-Null
Require-Node '//w:Dialog[@Id="PicLensInstallScopeDlg"]//w:Publish[@Property="MSIINSTALLPERUSER" and @Value="1"]' '目前使用者選項未設定 MSIINSTALLPERUSER=1。' | Out-Null
Require-Node '//w:Dialog[@Id="PicLensInstallScopeDlg"]//w:Publish[@Property="ALLUSERS" and @Value="1"]' '所有使用者選項未設定 ALLUSERS=1。' | Out-Null

$expectedMachineCondition = 'PICLENS_EXISTING_PER_MACHINE OR PICLENS_EXISTING_LEGACY_PER_MACHINE'
$machineScope = Require-Node '//w:SetProperty[@Id="ALLUSERS" and @Action="UseExistingPerMachineScope" and @Value="1" and @After="AppSearch"]' '所有使用者升級未在 AppSearch 後設定 ALLUSERS=1。'
if ($machineScope.Condition -ne $expectedMachineCondition) { throw '所有使用者 ALLUSERS 條件不正確。' }

$expectedUserCondition = 'PICLENS_EXISTING_PER_USER AND NOT PICLENS_EXISTING_PER_MACHINE AND NOT PICLENS_EXISTING_LEGACY_PER_MACHINE'
$userScope = Require-Node '//w:SetProperty[@Id="ALLUSERS" and @Action="UseExistingPerUserScope" and @Value="2" and @After="UseExistingPerMachineScope"]' '目前使用者升級未設定 ALLUSERS=2。'
if ($userScope.Condition -ne $expectedUserCondition) { throw '目前使用者升級範圍條件不正確。' }
Require-Node '//w:SetProperty[@Id="MSIINSTALLPERUSER" and @Action="UseExistingPerUserInstallContext" and @Value="1" and @After="UseExistingPerUserScope"]' '目前使用者升級未設定 MSIINSTALLPERUSER=1。' | Out-Null
if ($null -ne $document.SelectSingleNode('//w:SetProperty[@Id="PICLENS_INSTALL_SCOPE"]', $namespace)) { throw '升級不需要改寫只供安裝畫面使用的範圍選項。' }

Require-Node '//w:InstallUISequence/w:FindRelatedProducts[@After="UseExistingPerUserInstallContext"]' 'UI sequence 必須在恢復安裝範圍後搜尋相關產品。' | Out-Null
Require-Node '//w:InstallExecuteSequence/w:FindRelatedProducts[@After="UseExistingPerUserInstallContext"]' 'Execute sequence 必須在恢復安裝範圍後搜尋相關產品。' | Out-Null

$show = Require-Node '//w:InstallUISequence/w:Show[@Dialog="PicLensInstallScopeDlg" and @Before="CostInitialize"]' '安裝範圍畫面必須在計算安裝路徑前顯示。'
if ($show.Condition -ne 'NOT Installed AND NOT WIX_UPGRADE_DETECTED AND NOT PICLENS_EXISTING_PER_MACHINE AND NOT PICLENS_EXISTING_PER_USER AND NOT PICLENS_EXISTING_LEGACY_PER_MACHINE') { throw '安裝範圍畫面的顯示條件不正確。' }

$projectPath = Join-Path $PSScriptRoot 'PicLens.wixproj'
[xml]$project = Get-Content -Raw -LiteralPath $projectPath
$uiExtension = $project.SelectSingleNode('/Project/ItemGroup/PackageReference[@Include="WixToolset.UI.wixext" and @Version="6.0.2"]')
if ($null -eq $uiExtension) { throw 'WiX 專案缺少相同版本的 UI extension。' }
$suppressedIces = $project.SelectSingleNode('/Project/PropertyGroup/SuppressIces')
if ($null -eq $suppressedIces -or $suppressedIces.InnerText -ne 'ICE57') { throw '雙範圍開始功能表捷徑只能抑制已知誤判的 ICE57。' }

$lifecyclePath = Join-Path $PSScriptRoot 'test-lifecycle.ps1'
$tokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($lifecyclePath, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw "Windows MSI 生命週期腳本語法錯誤：$($parseErrors[0].Message)" }

$lifecycle = Get-Content -Raw -LiteralPath $lifecyclePath
foreach ($required in @(
        "[ValidateSet('perUser', 'perMachine')]",
        "[string]`$InstallScope = 'perUser'",
        "@('ALLUSERS=2', 'MSIINSTALLPERUSER=1')",
        "@('ALLUSERS=1')",
        "'Programs/PicLens/PicLens.exe'",
        "'PicLens/PicLens.exe'",
        "'HKCU:\Software\PicLens'",
        "'HKLM:\Software\PicLens'",
        '$shortcutRegistryMarker = $scopeRegistryMarker',
        '$candidateProperties = if ($PreviousMsiPath) { @() } else { $scopeProperties }',
        "'installScope'"
    )) {
    if (-not $lifecycle.Contains($required)) { throw "Windows MSI 生命週期腳本缺少安裝範圍契約：$required" }
}

Write-Output 'Windows MSI authoring contract passed.'
