[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SiteDirectory,
    [Parameter(Mandatory = $true)][ValidatePattern('^(?!-)[A-Za-z0-9._@-]+$')][string]$SshHost,
    [Parameter(Mandatory = $true)][ValidatePattern('^/[A-Za-z0-9_/-]+$')][string]$RemoteRoot,
    [switch]$AllowRollback
)
$ErrorActionPreference = "Stop"
$RemoteRoot = $RemoteRoot.TrimEnd('/')
if ($RemoteRoot.Split('/').Count -lt 4) { throw "Use the full product site directory, not a system root." }
$site = [IO.Path]::GetFullPath($SiteDirectory)
& (Join-Path $PSScriptRoot "Test-UpdateSite.ps1") -SiteDirectory $site
$manifest = Get-Content (Join-Path $site "stable.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$version = [string]$manifest.version
$stage = "$RemoteRoot/.staging-$version-$([guid]::NewGuid().ToString('N'))"
& ssh -o BatchMode=yes -o ConnectTimeout=10 $SshHost "mkdir -p '$stage/assets' '$stage/releases/$version'"
if ($LASTEXITCODE -ne 0) { throw "Cannot create VPS staging directory. Configure SSH key access first." }
foreach ($relative in @("index.html", "stable.json", "assets/site.css", "releases/$version/FourJ_PV_Toolbox_Setup_v$version.exe")) {
    & scp -o BatchMode=yes -o ConnectTimeout=10 (Join-Path $site $relative) "$SshHost`:$stage/$relative"
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $relative. stable.json was not switched." }
}
& scp -o BatchMode=yes -o ConnectTimeout=10 (Join-Path $PSScriptRoot "activate-update-site.py") "$SshHost`:$stage/activate.py"
if ($LASTEXITCODE -ne 0) { throw "Activation helper upload failed." }
$rollbackFlag = if ($AllowRollback) { "--allow-rollback" } else { "" }
& ssh -o BatchMode=yes -o ConnectTimeout=10 $SshHost "python3 '$stage/activate.py' '$RemoteRoot' '$stage' $rollbackFlag"
if ($LASTEXITCODE -ne 0) { throw "VPS activation failed. Inspect its error before retrying." }
Write-Host "UPDATE_SITE_DEPLOYED=$version"
