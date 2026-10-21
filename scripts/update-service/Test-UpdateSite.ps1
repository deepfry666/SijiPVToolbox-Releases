[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SiteDirectory,
    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"
$siteRoot = [IO.Path]::GetFullPath($SiteDirectory)
foreach ($relativePath in @("index.html", "stable.json", "assets\site.css")) {
    $path = Join-Path $siteRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Update site is missing required file: $relativePath"
    }
}

$manifest = Get-Content -LiteralPath (Join-Path $siteRoot "stable.json") `
    -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or
    $manifest.product -ne "FourJTooLs.StructuralToolbox" -or
    $manifest.channel -ne "stable") {
    throw "stable.json identity fields are invalid."
}
if ($manifest.version -notmatch '^\d+\.\d+\.\d+$') {
    throw "stable.json version is invalid: $($manifest.version)"
}
if ($ExpectedVersion -and $manifest.version -ne $ExpectedVersion) {
    throw "stable.json version $($manifest.version) does not match $ExpectedVersion."
}
$packageUri = [uri]$manifest.url
if ($packageUri.Scheme -ne "https") {
    throw "Installer URL must use HTTPS: $packageUri"
}
$packageName = [uri]::UnescapeDataString($packageUri.Segments[-1])
if ($packageName -cne "FourJ_PV_Toolbox_Setup_v$($manifest.version).exe") {
    throw "Installer filename must use the canonical versioned name."
}
$mirrors = @()
if ($manifest.PSObject.Properties.Name -contains "mirrors") { $mirrors = @($manifest.mirrors) }
if ($mirrors.Count -gt 4) { throw "At most four mirrors are allowed." }
foreach ($mirrorText in $mirrors) {
    $mirrorUri = [uri]$mirrorText
    if ($mirrorUri.Scheme -ne "https" -or $mirrorUri.UserInfo -or $mirrorUri.Fragment -or
        -not $mirrorUri.AbsolutePath.EndsWith("/$packageName",
            [StringComparison]::Ordinal)) {
        throw "Manifest mirror URL is invalid: $mirrorText"
    }
}
$expectedPackageSuffix = "/releases/$($manifest.version)/$packageName"
if (-not $packageUri.AbsolutePath.EndsWith($expectedPackageSuffix,
        [StringComparison]::Ordinal)) {
    throw "Installer URL must use a versioned releases path."
}
$packagePath = Join-Path $siteRoot `
    (Join-Path (Join-Path "releases" ([string]$manifest.version)) $packageName)
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "Manifest installer is missing: $packageName"
}
$actualFile = Get-Item -LiteralPath $packagePath
$actualHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
if ($actualFile.Length -ne [long]$manifest.sizeBytes) {
    throw "Installer size does not match stable.json."
}
if ($actualHash -ne [string]$manifest.sha256) {
    throw "Installer SHA-256 does not match stable.json."
}

$html = Get-Content -LiteralPath (Join-Path $siteRoot "index.html") -Raw -Encoding UTF8
if ($html -match '\{\{[A-Z_]+\}\}') {
    throw "index.html still contains an unreplaced template token."
}
if (-not $html.Contains("v$($manifest.version)") -or
    -not $html.Contains("releases/$($manifest.version)/$packageName")) {
    throw "index.html does not point to the current manifest installer."
}

Write-Host "UPDATE_SITE_TEST=PASS"
Write-Host "VERSION=$($manifest.version)"
Write-Host "INSTALLER_SHA256=$($actualHash.ToLowerInvariant())"
