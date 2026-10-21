param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$workRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    ("work\update-service-tests-" + [guid]::NewGuid().ToString("N"))))
$expectedPrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    "work\update-service-tests-") )
if (-not $workRoot.StartsWith($expectedPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe update-service test directory: $workRoot"
}

try {
    New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
    $fixture = Join-Path $workRoot "fixture-installer.exe"
    [IO.File]::WriteAllBytes($fixture,
        [Text.Encoding]::UTF8.GetBytes("FourJTooLs installer fixture"))
    $site = Join-Path $workRoot "site"
    & (Join-Path $projectRoot `
        "scripts\update-service\New-UpdateSite.ps1") `
        -Version 0.4.1 -InstallerPath $fixture `
        -BaseUrl "https://downloads.example.test/toolbox" `
        -MirrorInstallerUrls 'https://github.com/example/releases/download/v0.4.1/FourJ_PV_Toolbox_Setup_v0.4.1.exe' `
        -OutputDirectory $site -ReleaseNotes @("更新服务回归测试") |
        Out-Null
    & (Join-Path $projectRoot `
        "scripts\update-service\Test-UpdateSite.ps1") `
        -SiteDirectory $site -ExpectedVersion 0.4.1

    # Legacy schema 1 manifests without mirrors remain valid.
    $manifestPath = Join-Path $site 'stable.json'
    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $manifest.PSObject.Properties.Remove('mirrors')
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5))
    & (Join-Path $projectRoot 'scripts\update-service\Test-UpdateSite.ps1') -SiteDirectory $site

    # Reject insecure mirrors before activation.
    $manifest | Add-Member -NotePropertyName mirrors -NotePropertyValue @('http://unsafe.test/FourJ_PV_Toolbox_Setup_v0.4.1.exe')
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5))
    try {
        & (Join-Path $projectRoot 'scripts\update-service\Test-UpdateSite.ps1') -SiteDirectory $site
        throw 'Insecure mirror accepted.'
    } catch { if (-not $_.Exception.Message.Contains('mirror URL is invalid')) { throw } }
    $manifest.PSObject.Properties.Remove('mirrors')
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5))

    try {
        & (Join-Path $projectRoot `
            "scripts\update-service\New-UpdateSite.ps1") `
            -Version 0.4.1 -InstallerPath $fixture `
            -BaseUrl "http://downloads.example.test/toolbox" `
            -OutputDirectory (Join-Path $workRoot "http-site") |
            Out-Null
        throw "Plain HTTP update site was accepted."
    } catch {
        if (-not $_.Exception.Message.Contains("must use HTTPS")) {
            throw
        }
    }

    $package = Get-ChildItem -LiteralPath (Join-Path $site "releases") `
        -Filter "*.exe" -File -Recurse | Select-Object -First 1
    [IO.File]::AppendAllText($package.FullName, "tampered")
    try {
        & (Join-Path $projectRoot `
            "scripts\update-service\Test-UpdateSite.ps1") `
            -SiteDirectory $site -ExpectedVersion 0.4.1
        throw "Tampered update package was accepted."
    } catch {
        if (-not ($_.Exception.Message.Contains("size does not match") -or
                $_.Exception.Message.Contains("SHA-256 does not match"))) {
            throw
        }
    }

    Write-Host "UPDATE_SERVICE_TESTS=PASS"
} finally {
    if (Test-Path -LiteralPath $workRoot -PathType Container) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
