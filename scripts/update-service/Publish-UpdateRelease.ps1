[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReleaseDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$root = [IO.Path]::GetFullPath($ReleaseDirectory)
$assets = Join-Path $root 'github'
$record = Get-Content -LiteralPath (Join-Path $assets 'release.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version = [string]$record.version
if ($version -notmatch '^\d+\.\d+\.\d+$' -or $record.tag -ne "v$version" -or
    $record.sourceCommit -notmatch '^[0-9a-f]{40}$' -or
    $record.repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid release identity.' }
$tag = [string]$record.tag
$repository = [string]$record.repository
$name = "FourJ_PV_Toolbox_Setup_v$version.exe"
$requiredAssets = @($name, 'stable.json', 'sha256.txt', 'release.json', 'RELEASE.md')
foreach ($file in $requiredAssets) {
    if (-not (Test-Path -LiteralPath (Join-Path $assets $file) -PathType Leaf)) {
        throw "Prepared GitHub asset is missing: $file"
    }
}
if ((Get-FileHash (Join-Path $assets $name) -Algorithm SHA256).Hash -ne $record.sha256) {
    throw 'Prepared installer changed.'
}
$manifest = Get-Content (Join-Path $assets 'stable.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'FourJTooLs.StructuralToolbox' -or
    $manifest.channel -ne 'stable' -or $manifest.version -ne $version -or
    $manifest.sha256 -ne $record.sha256 -or $manifest.sizeBytes -ne $record.sizeBytes -or
    [string]$manifest.url -ne "https://github.com/$repository/releases/download/$tag/$name" -or
    @($manifest.mirrors).Count -ne 0) { throw 'GitHub release metadata mismatch.' }
$repoInfo = & gh api "repos/$repository" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $repoInfo.private) { throw 'Download repository must exist and be public.' }
$existingTag = & git -C $projectRoot tag --list $tag
if ($existingTag) {
    $tagCommit = & git -C $projectRoot rev-parse "$tag^{commit}"
    if ($tagCommit -ne $record.sourceCommit) { throw 'Source tag already points to a different commit.' }
} else {
    & git -C $projectRoot tag -a $tag ([string]$record.sourceCommit) -m "Release $tag"
    if ($LASTEXITCODE -ne 0) { throw 'Source tag creation failed.' }
}
& git -C $projectRoot push origin "refs/tags/$tag"
if ($LASTEXITCODE -ne 0) { throw 'Source tag push failed.' }
$releasesText = & gh release list --repo $repository --limit 1000 --json tagName,isDraft
if ($LASTEXITCODE -ne 0) { throw 'Cannot list GitHub releases.' }
$existingRelease = @($releasesText | ConvertFrom-Json | Where-Object { $_.tagName -eq $tag })
if ($existingRelease.Count -eq 0) {
    & gh release create $tag --repo $repository --draft --title "四季光伏工具箱 $tag" --notes-file (Join-Path $assets 'RELEASE.md')
    if ($LASTEXITCODE -ne 0) { throw 'GitHub draft release creation failed.' }
    $isDraft = $true
} else { $isDraft = $existingRelease[0].isDraft }
# Explicit allowlist: never upload a source checkout, DWG or credentials.
$names = @($name, 'stable.json', 'sha256.txt', 'release.json')
if ($isDraft) {
    foreach ($file in $names) {
        & gh release upload $tag (Join-Path $assets $file) --repo $repository --clobber
        if ($LASTEXITCODE -ne 0) { throw "Draft asset upload failed: $file" }
    }
}
$verify = Join-Path $projectRoot ("work\release-download-check-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verify | Out-Null
foreach ($file in $names) {
    & gh release download $tag --repo $repository --pattern $file --dir $verify
    if ($LASTEXITCODE -ne 0 -or (Get-FileHash (Join-Path $verify $file) -Algorithm SHA256).Hash -ne
        (Get-FileHash (Join-Path $assets $file) -Algorithm SHA256).Hash) { throw "GitHub roundtrip failed: $file" }
}
if ($isDraft) {
    & gh release edit $tag --repo $repository --notes-file (Join-Path $assets 'RELEASE.md')
    if ($LASTEXITCODE -ne 0) { throw 'Draft release notes update failed.' }
    & gh release edit $tag --repo $repository --draft=false --latest
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release publication failed.' }
}
Write-Host "GITHUB_RELEASE=https://github.com/$repository/releases/tag/$tag"
