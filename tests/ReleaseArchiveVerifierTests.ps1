param()

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$archiveBase = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    "work\legacy-source")).TrimEnd("\")
$archiveVersion = "0.3.4"
$archiveRoot = Join-Path $archiveBase `
    (".staging-v0.3.4-{0}" -f [guid]::NewGuid().ToString("N"))
$verifierPath = Join-Path $projectRoot "scripts\Test-ReleaseArchive.ps1"
$utf8NoBom = New-Object Text.UTF8Encoding($false)
$regressionSuiteHash = "A" * 64
$evidenceName = "四季光伏工具箱_v${archiveVersion}_发布证据.json"
$releaseRoot = Join-Path $archiveRoot "release"
$releaseControlsRoot = Join-Path $archiveRoot "evidence\release-controls"
$evidencePath = Join-Path $releaseControlsRoot $evidenceName
$releaseChecksumPath = Join-Path $releaseControlsRoot "SHA256SUMS.txt"
$manifestPath = Join-Path $archiveRoot "archive-manifest.json"
$rootChecksumPath = Join-Path $archiveRoot "SHA256SUMS.txt"
$passed = 0
. (Join-Path $projectRoot "scripts\ReleaseTraceabilityAtomic.ps1")

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:passed++
}

function Write-Text([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    [IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Write-Evidence([int]$SchemaVersion) {
    $installerPath = Get-ChildItem -LiteralPath $releaseRoot -File `
        -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    $evidence = [ordered]@{
        schemaVersion = $SchemaVersion
        releaseVersion = $archiveVersion
        releaseKind = "unsigned-distribution"
        releaseClassification = "UNSIGNED"
        authenticodeExpectation = "NotSigned"
        publicDistributionAllowed = $true
        installer = [ordered]@{
            sha256 = $(if ($null -eq $installerPath) {
                "0" * 64
            } else {
                (Get-FileHash -LiteralPath $installerPath.FullName `
                    -Algorithm SHA256).Hash
            })
        }
        regressionSuiteSha256 = $regressionSuiteHash
    }
    Write-Text $evidencePath ($evidence | ConvertTo-Json -Depth 5)
}

function Write-ValidReleaseChecksum {
    $lines = @(Get-ChildItem -LiteralPath $releaseControlsRoot -File -Force |
        Where-Object { $_.FullName -ne $releaseChecksumPath } |
        Sort-Object Name | ForEach-Object {
            "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash, $_.Name
        })
    [IO.File]::WriteAllLines($releaseChecksumPath, $lines, $utf8NoBom)
}

function Write-RootChecksum {
    $prefix = $archiveRoot.TrimEnd("\") + "\"
    $lines = @(Get-ChildItem -LiteralPath $archiveRoot -Recurse -File -Force |
        Where-Object { $_.FullName -ne $rootChecksumPath } |
        Sort-Object FullName | ForEach-Object {
            $relative = $_.FullName.Substring($prefix.Length).Replace("\", "/")
            "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash, $relative
        })
    [IO.File]::WriteAllLines($rootChecksumPath, $lines, $utf8NoBom)
}

function Update-ArchiveControls {
    $prefix = $archiveRoot.TrimEnd("\") + "\"
    $inventory = @(Get-ChildItem -LiteralPath $archiveRoot -Recurse `
        -File -Force | Where-Object {
            $_.FullName -ne $manifestPath -and
            $_.FullName -ne $rootChecksumPath
        } | Sort-Object FullName | ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($prefix.Length).Replace("\", "/")
                bytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName `
                    -Algorithm SHA256).Hash
            }
        })
    $manifest = [ordered]@{
        schemaVersion = 3
        archivedVersion = $archiveVersion
        classification = "UNSIGNED"
        authenticodeExpectation = "NotSigned"
        publicDistributionAllowed = $true
        regressionSuiteSha256 =
            (Get-RegressionSuiteFingerprint (Join-Path $archiveRoot "files"))
        releaseControls = [ordered]@{
            evidencePath = "evidence/release-controls/$evidenceName"
            evidenceSha256 = (Get-FileHash -LiteralPath $evidencePath `
                -Algorithm SHA256).Hash
            checksumPath = "evidence/release-controls/SHA256SUMS.txt"
            checksumSha256 = (Get-FileHash -LiteralPath $releaseChecksumPath `
                -Algorithm SHA256).Hash
        }
        build = [ordered]@{
            sourceCodeSha256 = Get-StructuralSourceFingerprint `
                (Join-Path $archiveRoot "files")
            installerSha256 = (Get-FileHash -LiteralPath `
                (Get-ChildItem -LiteralPath $releaseRoot -File `
                    -Filter "*.exe" | Select-Object -First 1).FullName `
                -Algorithm SHA256).Hash
        }
        inventory = $inventory
    }
    Write-Text $manifestPath ($manifest | ConvertTo-Json -Depth 8)
    Write-RootChecksum
}

function Assert-VerifierFails([string]$ExpectedMessage, [string]$Description) {
    $caught = $null
    try {
        & $verifierPath -ArchiveDirectory $archiveRoot `
            -AllowStagingDirectory | Out-Null
    } catch {
        $caught = $_
    }
    Assert-Condition ($null -ne $caught -and
        $caught.Exception.Message.Contains($ExpectedMessage)) $Description
}

try {
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    foreach ($relativePath in @("scripts", "tests")) {
        Write-Text (Join-Path $archiveRoot `
            "files\$relativePath\fixture.txt") "$relativePath fixture"
    }
    Write-Text (Join-Path $archiveRoot `
        "files\src\StructuralToolbox\fixture.cs") "class Fixture {}"
    $regressionSuiteHash =
        Get-RegressionSuiteFingerprint (Join-Path $archiveRoot "files")
    $installerName =
        "四季光伏工具箱_Setup_v${archiveVersion}.exe"
    Write-Text (Join-Path $releaseRoot $installerName) `
        "installer fixture"
    Write-Evidence 5
    $settingsPath = Join-Path $archiveRoot `
        "evidence\real-drawing\isolate\Roaming\StructuralToolbox\rail-settings.xml"
    Write-Text $settingsPath "<RailSettings version=`"0.3.4`" />"
    $settingsRelative = "isolate/Roaming/StructuralToolbox/rail-settings.xml"
    $settingsHash = (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash
    Write-Text (Join-Path $archiveRoot `
        "evidence\real-drawing\SHA256SUMS.txt") `
        ("{0} *{1}" -f $settingsHash, $settingsRelative)
    Write-ValidReleaseChecksum
    Update-ArchiveControls

    & $verifierPath -ArchiveDirectory $archiveRoot `
        -AllowStagingDirectory | Out-Null
    Assert-Condition ($true) `
        "valid v0.3.4 one-EXE archive with schema-5 release controls must verify"

    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    $manifest.archivedVersion = "9.8.7"
    Write-Text $manifestPath ($manifest | ConvertTo-Json -Depth 8)
    Write-RootChecksum
    Assert-VerifierFails "version does not match archive manifest." `
        "directory and manifest version mismatch must be rejected"

    Update-ArchiveControls
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    $manifest.schemaVersion = 2
    $manifest.archivedVersion = "0.3.3"
    Write-Text $manifestPath ($manifest | ConvertTo-Json -Depth 8)
    Write-RootChecksum
    Assert-VerifierFails "version does not match archive manifest." `
        "manifest downgrade under a current-version directory must be rejected"

    Update-ArchiveControls
    Write-Text (Join-Path $releaseRoot "extra.bin") `
        "self-consistent extra release file"
    Write-ValidReleaseChecksum
    Update-ArchiveControls
    Assert-VerifierFails "Archive release one-EXE allowlist" `
        "self-consistent extra release file must be rejected"
    Remove-Item -LiteralPath (Join-Path $releaseRoot "extra.bin") -Force

    Update-ArchiveControls
    $rogueReleasePath = Join-Path $releaseRoot "rogue\untracked.bin"
    Write-Text $rogueReleasePath "not covered by release SHA256SUMS"
    Update-ArchiveControls
    Assert-VerifierFails `
        "Archived release directory must contain top-level files only." `
        "release subdirectory absent from release checksum must be rejected"
    Remove-Item -LiteralPath (Split-Path -Parent $rogueReleasePath) `
        -Recurse -Force

    Write-Evidence 4
    Write-ValidReleaseChecksum
    Update-ArchiveControls
    Assert-VerifierFails "Archived release evidence identity is invalid." `
        "self-consistent schema-4 evidence must be rejected for v0.3.4"

    Write-Evidence 5
    Write-ValidReleaseChecksum
    Update-ArchiveControls
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    $manifest.releaseControls.evidenceSha256 = "0" * 64
    Write-Text $manifestPath ($manifest | ConvertTo-Json -Depth 8)
    Write-RootChecksum
    Assert-VerifierFails "Archived release-control hash differs" `
        "incorrect evidence control hash must be rejected"

    Write-Evidence 5
    $badReleaseLines = @(("0" * 64) + "  " + $evidenceName)
    [IO.File]::WriteAllLines($releaseChecksumPath,
        $badReleaseLines, $utf8NoBom)
    Update-ArchiveControls
    Assert-VerifierFails "Archived release-control SHA256SUMS failed" `
        "incorrect release checksum entry must be rejected"

    Write-ValidReleaseChecksum
    Update-ArchiveControls
    & $verifierPath -ArchiveDirectory $archiveRoot `
        -AllowStagingDirectory | Out-Null
    Assert-Condition ($true) `
        "archive must verify again after restoring all release controls"
} finally {
    if (Test-Path -LiteralPath $archiveRoot -PathType Container) {
        $resolvedArchive = [IO.Path]::GetFullPath($archiveRoot)
        if ($resolvedArchive.StartsWith(
                ($archiveBase + "\.staging-v0.3.4-"),
                [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedArchive -Recurse -Force
        }
    }
}

Write-Host "PASS $passed release archive verifier tests"
