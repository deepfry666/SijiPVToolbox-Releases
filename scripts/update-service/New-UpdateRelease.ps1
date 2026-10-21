[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory=$true)][string]$InstallerPath,
    [Parameter(Mandatory=$true)][string]$SourceRef,
    [Parameter(Mandatory=$true)][string]$NotesPath,
    [Parameter(Mandatory=$true)][datetime]$ReleaseDate,
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$GitHubRepository = "deepfry666/SijiPVToolbox-Releases",
    [string]$OutputDirectory
)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commit = & git -C $projectRoot rev-parse --verify --end-of-options "$SourceRef^{commit}"
if ($LASTEXITCODE -ne 0) { throw "SourceRef must resolve to a committed source revision." }
$commit = ([string]$commit).Trim()
$source = & git -C $projectRoot show "${commit}:src/StructuralToolbox/Properties/AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0 -or ($source -join "`n") -notmatch ('AssemblyVersion\("' + [regex]::Escape($Version) + '\.0"\)')) {
    throw "Source revision does not declare version $Version."
}
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.Extension -ne '.exe' -or $installer.VersionInfo.ProductVersion.Trim() -ne "$Version.0") {
    throw "Installer ProductVersion must equal $Version.0. Use an already verified complete installer."
}
$notesText = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $NotesPath).Path)
if ([string]::IsNullOrWhiteSpace($notesText)) { throw "Public release notes are required." }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot "outputs\update-release-v$Version" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count) {
    throw "OutputDirectory must be absent or empty. Never overwrite prepared releases."
}
$github = Join-Path $OutputDirectory 'github'
$name = "FourJ_PV_Toolbox_Setup_v$Version.exe"
$packageUrl = "https://github.com/$GitHubRepository/releases/download/v$Version/$name"
$notes = @($notesText -split '\r?\n' | Where-Object { $_.Trim() } | ForEach-Object { $_.TrimStart('-', ' ') })
New-Item -ItemType Directory -Path $github | Out-Null
Copy-Item -LiteralPath $installer.FullName -Destination (Join-Path $github $name)
$hash = (Get-FileHash -LiteralPath (Join-Path $github $name) -Algorithm SHA256).Hash.ToLowerInvariant()
$utf8 = New-Object Text.UTF8Encoding($false)
$manifest = [ordered]@{
    schemaVersion=1; product='FourJTooLs.StructuralToolbox'; channel='stable';
    version=$Version; url=$packageUrl; mirrors=@(); sha256=$hash;
    sizeBytes=$installer.Length; date=$ReleaseDate.ToString('yyyy-MM-dd'); notes=$notes
}
[IO.File]::WriteAllText((Join-Path $github 'stable.json'), (($manifest | ConvertTo-Json -Depth 4) + "`n"), $utf8)
[IO.File]::WriteAllText((Join-Path $github 'sha256.txt'), "$hash  $name`n", $utf8)
[IO.File]::WriteAllText((Join-Path $github 'RELEASE.md'), $notesText, $utf8)
$record = [ordered]@{ version=$Version; tag="v$Version"; sourceCommit=$commit; repository=$GitHubRepository;
    installer=$name; sha256=$hash; sizeBytes=$installer.Length; date=$ReleaseDate.ToString('yyyy-MM-dd') }
[IO.File]::WriteAllText((Join-Path $github 'release.json'), (($record | ConvertTo-Json) + "`n"), $utf8)
Write-Host "UPDATE_RELEASE_PREPARED=$OutputDirectory"
