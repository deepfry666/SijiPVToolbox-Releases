[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchiveDirectory,

    [switch]$AllowStagingDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0
. (Join-Path $PSScriptRoot "ReleaseTraceabilityAtomic.ps1")

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$archiveBase = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    "work\legacy-source"))
$archivePrefix = $archiveBase.TrimEnd("\") + "\"
$archiveRoot = [IO.Path]::GetFullPath($ArchiveDirectory)
if (-not $archiveRoot.StartsWith($archivePrefix,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw "ArchiveDirectory must be under work\legacy-source: $archiveRoot"
}
if (-not (Test-Path -LiteralPath $archiveRoot -PathType Container)) {
    throw "Archive directory was not found: $archiveRoot"
}

$manifestPath = Join-Path $archiveRoot "archive-manifest.json"
$checksumPath = Join-Path $archiveRoot "SHA256SUMS.txt"
foreach ($requiredPath in @($manifestPath, $checksumPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required archive control file is missing: $requiredPath"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
try {
    $archiveVersion = [version]([string]$manifest.archivedVersion)
} catch {
    throw "Archive manifest version is invalid."
}
$archiveVersion3 = $archiveVersion.ToString(3)
if ([string]$manifest.archivedVersion -ne $archiveVersion3) {
    throw "Archive manifest version must use three normalized components."
}
$archiveParent = [IO.Path]::GetFullPath(
    (Split-Path -Parent $archiveRoot)).TrimEnd("\")
if (-not [string]::Equals($archiveParent, $archiveBase.TrimEnd("\"),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Archive directory must be a direct child of work\legacy-source."
}
$archiveLeaf = Split-Path -Leaf $archiveRoot
if ($AllowStagingDirectory) {
    $stagingPattern = '^\.staging-v' + [regex]::Escape($archiveVersion3) +
        '-[0-9a-f]{32}$'
    if ($archiveLeaf -notmatch $stagingPattern) {
        throw "Archive staging directory version does not match archive manifest."
    }
} elseif (-not [string]::Equals($archiveLeaf, "v$archiveVersion3",
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Archive directory version does not match archive manifest."
}
$expectedManifestSchema = if ($archiveVersion -ge [version]"0.3.4") {
    3
} elseif ($archiveVersion -ge [version]"0.3.3") {
    2
} else {
    1
}
$expectedClassification = if ($archiveVersion -ge [version]"0.3.4") {
    "UNSIGNED"
} else {
    "TEST_UNSIGNED"
}
if ([int]$manifest.schemaVersion -ne $expectedManifestSchema -or
    [string]::IsNullOrWhiteSpace([string]$manifest.archivedVersion) -or
    [string]$manifest.classification -ne $expectedClassification -or
    ($archiveVersion -ge [version]"0.3.4" -and
        ([string]$manifest.authenticodeExpectation -ne "NotSigned" -or
        -not [bool]$manifest.publicDistributionAllowed))) {
    throw "Archive manifest identity is invalid."
}

function Get-RelativeArchivePath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $archiveRoot.TrimEnd("\") + "\"
    if (-not $fullPath.StartsWith($prefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Archive file escaped the archive root: $fullPath"
    }
    return $fullPath.Substring($prefix.Length).Replace("\", "/")
}

if ($archiveVersion -ge [version]"0.3.3") {
    $isOneExeArchive = $archiveVersion -ge [version]"0.3.4"
    $expectedReleaseNames = if ($isOneExeArchive) {
        @("四季光伏工具箱_Setup_v${archiveVersion3}.exe")
    } else {
        @(
            "四季光伏工具箱_Setup_v${archiveVersion3}_TEST_UNSIGNED.exe",
            "安装说明.md",
            "验证摘要.md",
            "四季光伏工具箱_v${archiveVersion3}_发布证据.json",
            "SHA256SUMS.txt"
        )
    }
    $releaseRoot = Join-Path $archiveRoot "release"
    $releaseDirectories = @(Get-ChildItem -LiteralPath $releaseRoot `
        -Directory -Force)
    if ($releaseDirectories.Count -ne 0) {
        throw "Archived release directory must contain top-level files only."
    }
    $releaseTopLevelFiles = @(Get-ChildItem -LiteralPath `
        $releaseRoot -File -Force)
    Assert-AtomicExactNameSet $expectedReleaseNames `
        @($releaseTopLevelFiles.Name) `
        $(if ($isOneExeArchive) {
            "Archive release one-EXE allowlist"
        } else {
            "Archive release five-file allowlist"
        })
    $archivedInstallers = @($releaseTopLevelFiles | Where-Object {
        $_.Extension -eq ".exe"
    })
    if ($archivedInstallers.Count -ne 1 -or
        [string]$manifest.build.installerSha256 -cnotmatch
            '^[0-9A-F]{64}$' -or
        (Get-FileHash -LiteralPath $archivedInstallers[0].FullName `
            -Algorithm SHA256).Hash -ne
            [string]$manifest.build.installerSha256) {
        throw "Archived installer is not bound to archive-manifest.json."
    }
    $archivedFilesRoot = Join-Path $archiveRoot "files"
    $controlDirectory = if ($isOneExeArchive) {
        "evidence/release-controls"
    } else {
        "release"
    }
    $expectedEvidenceRelative = $controlDirectory +
        "/四季光伏工具箱_v" + $archiveVersion3 + "_发布证据.json"
    $expectedChecksumRelative = $controlDirectory + "/SHA256SUMS.txt"
    if ([string]$manifest.releaseControls.evidencePath -ne
            $expectedEvidenceRelative -or
        [string]$manifest.releaseControls.checksumPath -ne
            $expectedChecksumRelative) {
        throw "Archive release-control paths are missing or invalid."
    }
    $archivedEvidencePath = [IO.Path]::GetFullPath((Join-Path $archiveRoot `
        ([string]$manifest.releaseControls.evidencePath).Replace("/", "\")))
    $archivedReleaseChecksumPath = [IO.Path]::GetFullPath((Join-Path $archiveRoot `
        ([string]$manifest.releaseControls.checksumPath).Replace("/", "\")))
    foreach ($controlPath in @($archivedEvidencePath,
        $archivedReleaseChecksumPath)) {
        if (-not $controlPath.StartsWith(($archiveRoot.TrimEnd("\") + "\"),
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $controlPath -PathType Leaf)) {
            throw "Archived release control is missing or escaped the archive."
        }
    }
    if ((Get-FileHash -LiteralPath $archivedEvidencePath `
            -Algorithm SHA256).Hash -ne
            [string]$manifest.releaseControls.evidenceSha256 -or
        (Get-FileHash -LiteralPath $archivedReleaseChecksumPath `
            -Algorithm SHA256).Hash -ne
            [string]$manifest.releaseControls.checksumSha256) {
        throw "Archived release-control hash differs from archive-manifest.json."
    }
    $archivedEvidence = Get-Content -LiteralPath $archivedEvidencePath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    $regressionSuiteHash = [string]$manifest.regressionSuiteSha256
    $archivedRegressionSuiteHash =
        Get-RegressionSuiteFingerprint $archivedFilesRoot
    $archivedSourceHash = Get-StructuralSourceFingerprint $archivedFilesRoot
    if ([int]$archivedEvidence.schemaVersion -ne 5 -or
        [string]$archivedEvidence.releaseVersion -ne
            $archiveVersion3 -or
        ($isOneExeArchive -and
            ([string]$archivedEvidence.releaseKind -ne
                "unsigned-distribution" -or
            [string]$archivedEvidence.releaseClassification -ne "UNSIGNED" -or
            [string]$archivedEvidence.authenticodeExpectation -ne "NotSigned" -or
            -not [bool]$archivedEvidence.publicDistributionAllowed)) -or
        [string]$archivedEvidence.installer.sha256 -ne
            [string]$manifest.build.installerSha256 -or
        $regressionSuiteHash -cnotmatch '^[0-9A-F]{64}$' -or
        [string]$archivedEvidence.regressionSuiteSha256 -ne
            $regressionSuiteHash -or
        $archivedRegressionSuiteHash -ne $regressionSuiteHash -or
        [string]$manifest.build.sourceCodeSha256 -cnotmatch
            '^[0-9A-F]{64}$' -or
        $archivedSourceHash -ne [string]$manifest.build.sourceCodeSha256) {
        throw "Archived release evidence identity is invalid."
    }

    $releaseChecksumByName = @{}
    foreach ($line in Get-Content -LiteralPath $archivedReleaseChecksumPath `
        -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $match = [regex]::Match($line,
            '^([0-9A-Fa-f]{64})  ([^\\/\r\n]+)$')
        if (-not $match.Success -or
            $releaseChecksumByName.ContainsKey($match.Groups[2].Value)) {
            throw "Archived release-control SHA256SUMS contains an invalid entry."
        }
        $releaseChecksumByName[$match.Groups[2].Value] =
            $match.Groups[1].Value.ToUpperInvariant()
    }
    $controlFiles = @(Get-ChildItem -LiteralPath `
        (Split-Path -Parent $archivedReleaseChecksumPath) -File -Force |
        Where-Object { $_.FullName -ne $archivedReleaseChecksumPath })
    if ($controlFiles.Count -ne $releaseChecksumByName.Count) {
        throw "Archived release-control SHA256SUMS does not cover the exact file set."
    }
    foreach ($controlFile in $controlFiles) {
        if (-not $releaseChecksumByName.ContainsKey($controlFile.Name) -or
            $releaseChecksumByName[$controlFile.Name] -ne
                (Get-FileHash -LiteralPath $controlFile.FullName `
                    -Algorithm SHA256).Hash) {
            throw "Archived release-control SHA256SUMS failed: $($controlFile.Name)"
        }
    }
}

$inventoryByPath = @{}
$manifestInventory = if ($expectedManifestSchema -eq 1) {
    if ([int]$manifest.sourceInventoryCount -ne
        @($manifest.sourceInventory).Count) {
        throw "Legacy archive source inventory count is invalid."
    }
    @($manifest.sourceInventory)
} else {
    @($manifest.inventory)
}
foreach ($entry in $manifestInventory) {
    $recordedPath = [string]$entry.path
    $relativePath = if ($expectedManifestSchema -eq 1) {
        "files/" + $recordedPath
    } else {
        $recordedPath
    }
    if ([string]::IsNullOrWhiteSpace($relativePath) -or
        [IO.Path]::IsPathRooted($relativePath) -or
        $relativePath.Split('/') -contains ".." -or
        $inventoryByPath.ContainsKey($relativePath)) {
        throw "Archive manifest contains an unsafe or duplicate path: $relativePath"
    }
    $inventoryByPath[$relativePath] = $entry
}

$actualInventoryFiles = @(Get-ChildItem -LiteralPath $archiveRoot -Recurse `
    -File -Force | Where-Object {
        $_.FullName -ne $manifestPath -and $_.FullName -ne $checksumPath
    })
if ($actualInventoryFiles.Count -ne $inventoryByPath.Count) {
    throw "Archive inventory count differs from the copied file count."
}
foreach ($file in $actualInventoryFiles) {
    $relativePath = Get-RelativeArchivePath $file.FullName
    if (-not $inventoryByPath.ContainsKey($relativePath)) {
        throw "Archive file is absent from the manifest: $relativePath"
    }
    $entry = $inventoryByPath[$relativePath]
    if ([int64]$entry.bytes -ne $file.Length -or
        [string]$entry.sha256 -ne
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
        throw "Archive file size or SHA-256 differs: $relativePath"
    }
}

$checksumByPath = @{}
foreach ($line in Get-Content -LiteralPath $checksumPath -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }
    $match = [regex]::Match($line,
        '^([0-9A-F]{64})  ([^\r\n]+)$',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        throw "Invalid SHA256SUMS line: $line"
    }
    $relativePath = $match.Groups[2].Value
    if ([IO.Path]::IsPathRooted($relativePath) -or
        $relativePath.Split('/') -contains ".." -or
        $checksumByPath.ContainsKey($relativePath)) {
        throw "SHA256SUMS contains an unsafe or duplicate path: $relativePath"
    }
    $checksumByPath[$relativePath] = $match.Groups[1].Value.ToUpperInvariant()
}

$checksummedFiles = @(Get-ChildItem -LiteralPath $archiveRoot -Recurse `
    -File -Force | Where-Object { $_.FullName -ne $checksumPath })
if ($checksummedFiles.Count -ne $checksumByPath.Count) {
    throw "SHA256SUMS count differs from the archive file count."
}
foreach ($file in $checksummedFiles) {
    $relativePath = Get-RelativeArchivePath $file.FullName
    if (-not $checksumByPath.ContainsKey($relativePath) -or
        $checksumByPath[$relativePath] -ne
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
        throw "SHA256SUMS verification failed: $relativePath"
    }
}

Write-Host ("ARCHIVE_VERSION=" + [string]$manifest.archivedVersion)
Write-Host ("ARCHIVE_FILE_COUNT=" + $actualInventoryFiles.Count)
Write-Host "ARCHIVE_VERIFICATION=PASS"
