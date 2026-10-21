[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [Parameter(Mandatory = $true)][uri]$BaseUrl,
    [uri[]]$MirrorInstallerUrls = @(),
    [string]$OutputDirectory,
    [datetime]$ReleaseDate = (Get-Date),
    [string[]]$ReleaseNotes = @("完整安装器更新")
)

$ErrorActionPreference = "Stop"
foreach ($note in $ReleaseNotes) {
    if ($null -eq $note -or $note.Length -gt 200) { throw "Each release note must be at most 200 characters." }
}
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$templateRoot = Join-Path $projectRoot "src\StructuralToolbox.UpdateService"

if ($BaseUrl.Scheme -ne "https" -or $BaseUrl.UserInfo -or $BaseUrl.Query -or $BaseUrl.Fragment) {
    throw "BaseUrl must use HTTPS: $BaseUrl"
}
if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) {
    throw "Installer was not found: $InstallerPath"
}
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.Extension -ne ".exe") {
    throw "Installer must be a complete .exe package: $InstallerPath"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "work\update-site-v$Version"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    $existing = @(Get-ChildItem -LiteralPath $OutputDirectory -Force)
    if ($existing.Count -gt 0) {
        throw "OutputDirectory must be absent or empty: $OutputDirectory"
    }
}

$downloadFileName = "FourJ_PV_Toolbox_Setup_v$Version.exe"
$downloadRelativeUrl = "releases/$Version/$downloadFileName"
$baseText = $BaseUrl.AbsoluteUri.TrimEnd('/')
$downloadUrl = "$baseText/$downloadRelativeUrl"
$mirrorUrls = @($MirrorInstallerUrls | ForEach-Object {
    if ($_.Scheme -ne "https" -or $_.UserInfo -or $_.Fragment) { throw "MirrorInstallerUrls must use HTTPS: $_" }
    $_.AbsoluteUri
})
$mirrorUrls = @($mirrorUrls | Select-Object -Unique)
if ($mirrorUrls.Count -gt 4) { throw "At most four mirrors are allowed." }
$hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$sizeBytes = $installer.Length
$dateText = $ReleaseDate.ToString("yyyy-MM-dd")
$sizeMb = [Math]::Round($sizeBytes / 1MB, 1).ToString("0.0",
    [Globalization.CultureInfo]::InvariantCulture)

$assetsDirectory = Join-Path $OutputDirectory "assets"
$releasesDirectory = Join-Path $OutputDirectory ("releases\" + $Version)
New-Item -ItemType Directory -Path $assetsDirectory,$releasesDirectory -Force |
    Out-Null
Copy-Item -LiteralPath $installer.FullName `
    -Destination (Join-Path $releasesDirectory $downloadFileName)
Copy-Item -LiteralPath (Join-Path $templateRoot "site\assets\site.css") `
    -Destination (Join-Path $assetsDirectory "site.css")

$manifest = [ordered]@{
    schemaVersion = 1
    product = "FourJTooLs.StructuralToolbox"
    channel = "stable"
    version = $Version
    url = $downloadUrl
    mirrors = $mirrorUrls
    sha256 = $hash
    sizeBytes = $sizeBytes
    date = $dateText
    notes = @($ReleaseNotes)
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $OutputDirectory "stable.json"),
    $manifestJson + "`n", [Text.UTF8Encoding]::new($false))

$html = [IO.File]::ReadAllText(
    (Join-Path $templateRoot "site\index.template.html"))
$replacements = [ordered]@{
    "{{DOWNLOAD_URL}}" = $downloadRelativeUrl
    "{{VERSION}}" = $Version
    "{{SIZE_MB}}" = $sizeMb
    "{{RELEASE_DATE}}" = $dateText
    "{{SHA256}}" = $hash
}
foreach ($entry in $replacements.GetEnumerator()) {
    $html = $html.Replace([string]$entry.Key, [string]$entry.Value)
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory "index.html"),
    $html, [Text.UTF8Encoding]::new($false))

$deploymentNote = @"
四季光伏工具箱静态更新站点 v$Version

1. 使用 Deploy-UpdateSite.ps1 将本站点发布到：$baseText/
2. 服务器必须启用有效的 HTTPS 证书。
3. stable.json 应禁用缓存；releases/ 下的版本化 EXE 可长期缓存。
4. 安装包先上传并校验，stable.json 最后原子切换。保留旧版目录用于撤回。
5. 客户端地址固定，无需每次修改源码。发布流程见 docs/current/Git发布规范.md。
$baseText/stable.json
"@
[IO.File]::WriteAllText((Join-Path $OutputDirectory "DEPLOY.txt"),
    $deploymentNote.Trim() + "`n", [Text.UTF8Encoding]::new($false))

Write-Host "UPDATE_SITE_CREATED=$OutputDirectory"
Write-Host "MANIFEST_URL=$baseText/stable.json"
[pscustomobject]@{
    version = $Version
    directory = $OutputDirectory
    manifestUrl = "$baseText/stable.json"
    installerUrl = $downloadUrl
    sha256 = $hash
    sizeBytes = $sizeBytes
}
