[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory,

    [Parameter(Mandatory = $true)]
    [string]$BuildResult,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseEvidence,

    [Parameter(Mandatory = $true)]
    [string]$RealDrawingVerificationResult,

    [Parameter(Mandatory = $true)]
    [string[]]$AutoCADVerificationResults,

    [Parameter(Mandatory = $true)]
    [string]$PureCodeRegressionLog,

    [Parameter(Mandatory = $true)]
    [string]$InstallerArtifactVerificationLog,

    [Parameter(Mandatory = $true)]
    [string]$InstallerRuntimeVerificationLog,

    [Parameter(Mandatory = $true)]
    [string]$PublishReceipt
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0
. (Join-Path $PSScriptRoot "ReleaseTraceabilityAtomic.ps1")

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$workRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "work"))
$outputsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "outputs"))
$archiveBase = [IO.Path]::GetFullPath((Join-Path $workRoot "legacy-source"))
$utf8NoBom = New-Object Text.UTF8Encoding($false)

try {
    $parsedVersion = [version]$Version
} catch {
    throw "Version is invalid: $Version"
}
$version3 = "{0}.{1}.{2}" -f $parsedVersion.Major,
    $parsedVersion.Minor, [Math]::Max(0, $parsedVersion.Build)
$archiveRoot = Join-Path $archiveBase "v$version3"
if (Test-Path -LiteralPath $archiveRoot) {
    throw "Release archive already exists and will not be overwritten: $archiveRoot"
}
$stageRoot = Join-Path $archiveBase `
    (".staging-v{0}-{1}" -f $version3, [guid]::NewGuid().ToString("N"))

function Resolve-RequiredFile([string]$Value, [string]$Description) {
    $path = [IO.Path]::GetFullPath($Value)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$Description was not found: $path"
    }
    return $path
}

function Resolve-RequiredDirectory([string]$Value, [string]$Description) {
    $path = [IO.Path]::GetFullPath($Value)
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "$Description was not found: $path"
    }
    return $path.TrimEnd("\")
}

function Assert-Under([string]$Path, [string]$Root, [string]$Description) {
    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd("\") + "\"
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay under $Root`: $fullPath"
    }
}

function Get-RelativeProjectPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $projectRoot.TrimEnd("\") + "\"
    if (-not $fullPath.StartsWith($prefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the project root: $fullPath"
    }
    return $fullPath.Substring($prefix.Length).Replace("\", "/")
}

function Copy-FileTo([string]$Source, [string]$Destination) {
    $parent = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Copy-DirectoryTo(
    [string]$Source,
    [string]$Destination,
    [string[]]$ExcludedTopLevelDirectories = @()) {
    $sourceRoot = Resolve-RequiredDirectory $Source "Archive source directory"
    $sourcePrefix = $sourceRoot + "\"
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force) {
        $relative = $file.FullName.Substring($sourcePrefix.Length)
        $topLevel = $relative.Split('\')[0]
        if ($ExcludedTopLevelDirectories -contains $topLevel) {
            continue
        }
        Copy-FileTo $file.FullName (Join-Path $Destination $relative)
    }
}

function Get-ArchiveCopyPlan(
    [string]$RealSettingsPath,
    [string]$RealSourcePath) {
    $records = New-Object Collections.Generic.List[object]
    $archivePaths = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)

    function Add-PlannedFile([string]$Source, [string]$ArchivePath) {
        $sourcePath = Resolve-RequiredFile $Source "Planned archive input"
        $normalizedArchivePath = $ArchivePath.Replace("\", "/").TrimStart('/')
        if ([string]::IsNullOrWhiteSpace($normalizedArchivePath) -or
            $normalizedArchivePath.Split('/') -contains ".." -or
            -not $archivePaths.Add($normalizedArchivePath)) {
            throw "Archive copy plan contains an unsafe or duplicate path: " +
                $normalizedArchivePath
        }
        $item = Get-Item -LiteralPath $sourcePath
        $records.Add([pscustomobject]@{
            SourcePath = $item.FullName
            ArchivePath = $normalizedArchivePath
            Bytes = $item.Length
            Sha256 = (Get-FileHash -LiteralPath $item.FullName `
                -Algorithm SHA256).Hash
        }) | Out-Null
    }

    function Add-PlannedDirectory(
        [string]$Source,
        [string]$ArchiveDirectory,
        [string[]]$ExcludedTopLevelDirectories = @()) {
        $sourceRoot = Resolve-RequiredDirectory $Source `
            "Planned archive directory"
        $sourcePrefix = $sourceRoot + "\"
        foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse `
            -File -Force) {
            $relative = $file.FullName.Substring($sourcePrefix.Length)
            if ($ExcludedTopLevelDirectories -contains $relative.Split('\')[0]) {
                continue
            }
            Add-PlannedFile $file.FullName `
                ($ArchiveDirectory.TrimEnd("/", "\") + "/" +
                    $relative.Replace("\", "/"))
        }
    }

    foreach ($relativePath in @("AGENTS.md", "README.md")) {
        Add-PlannedFile (Join-Path $projectRoot $relativePath) `
            ("files/" + $relativePath.Replace("\", "/"))
    }
    foreach ($relativePath in @("src", "scripts", "tests", "bundle", "docs")) {
        Add-PlannedDirectory (Join-Path $projectRoot $relativePath) `
            ("files/" + $relativePath)
    }
    Add-PlannedDirectory $releaseRoot "release"
    Add-PlannedFile $releaseEvidencePath `
        ("evidence/release-controls/" + $releaseEvidenceName)
    Add-PlannedFile $releaseChecksumPath `
        "evidence/release-controls/SHA256SUMS.txt"
    Add-PlannedDirectory (Split-Path -Parent $buildResultPath) `
        "evidence/release-candidate"
    Add-PlannedDirectory (Split-Path -Parent $runtimeLogPath) `
        "evidence/installer-runtime"
    Add-PlannedFile $pureLogPath "evidence/logs/pure-code-regression.log"
    Add-PlannedFile $artifactLogPath "evidence/logs/installer-artifacts.log"
    Add-PlannedFile $runtimeLogPath "evidence/logs/installer-runtime.log"
    Add-PlannedFile $publishReceiptPath "evidence/logs/publish-result.json"
    for ($index = 0; $index -lt $autoCadResultPaths.Count; $index++) {
        Add-PlannedDirectory (Split-Path -Parent $autoCadResultPaths[$index]) `
            ("evidence/autocad-{0}" -f $index) @("isolate", "temp")
    }
    Add-PlannedDirectory (Split-Path -Parent $realResultPath) `
        "evidence/real-drawing" @("isolate", "temp")
    Add-PlannedFile $RealSettingsPath `
        "evidence/real-drawing/isolate/Roaming/StructuralToolbox/rail-settings.xml"
    Add-PlannedFile $RealSourcePath `
        ("evidence/real-drawing-source/" + (Split-Path -Leaf $RealSourcePath))
    return @($records | Sort-Object ArchivePath)
}

function Assert-ArchiveCopyPlansEqual(
    [object[]]$Expected,
    [object[]]$Actual,
    [string]$Description) {
    $expectedItems = @($Expected)
    $actualItems = @($Actual)
    Assert-AtomicExactNameSet @($expectedItems.ArchivePath) `
        @($actualItems.ArchivePath) "$Description path set"
    foreach ($expectedItem in $expectedItems) {
        $matches = @($actualItems | Where-Object {
            [string]::Equals([string]$_.ArchivePath,
                [string]$expectedItem.ArchivePath,
                [StringComparison]::OrdinalIgnoreCase)
        })
        if ($matches.Count -ne 1 -or
            -not [string]::Equals([string]$matches[0].SourcePath,
                [string]$expectedItem.SourcePath,
                [StringComparison]::OrdinalIgnoreCase) -or
            [int64]$matches[0].Bytes -ne [int64]$expectedItem.Bytes -or
            [string]$matches[0].Sha256 -ne [string]$expectedItem.Sha256) {
            throw "$Description differs for $($expectedItem.ArchivePath)."
        }
    }
}

function Assert-ArchiveStageMatchesPlan(
    [object[]]$Plan,
    [string]$StageRoot) {
    $stagePath = [IO.Path]::GetFullPath($StageRoot).TrimEnd("\")
    $stagePrefix = $stagePath + "\"
    $stagedFiles = @(Get-ChildItem -LiteralPath $stagePath -Recurse `
        -File -Force | ForEach-Object {
            [pscustomobject]@{
                ArchivePath = $_.FullName.Substring($stagePrefix.Length).
                    Replace("\", "/")
                Bytes = $_.Length
                Sha256 = (Get-FileHash -LiteralPath $_.FullName `
                    -Algorithm SHA256).Hash
            }
        })
    Assert-AtomicExactNameSet @($Plan.ArchivePath) @($stagedFiles.ArchivePath) `
        "Staged archive copy path set"
    foreach ($planned in $Plan) {
        $matches = @($stagedFiles | Where-Object {
            [string]::Equals([string]$_.ArchivePath,
                [string]$planned.ArchivePath,
                [StringComparison]::OrdinalIgnoreCase)
        })
        if ($matches.Count -ne 1 -or
            [int64]$matches[0].Bytes -ne [int64]$planned.Bytes -or
            [string]$matches[0].Sha256 -ne [string]$planned.Sha256) {
            throw "Staged archive differs from the validated copy plan: " +
                $planned.ArchivePath
        }
    }
}

function Get-StagedInputEvidence(
    [string]$StageRoot,
    [string]$RelativePath,
    [string]$Role) {
    $path = Resolve-RequiredFile (Join-Path $StageRoot `
        $RelativePath.Replace("/", "\")) "Staged $Role"
    $item = Get-Item -LiteralPath $path
    return [ordered]@{
        role = $Role
        path = $RelativePath.Replace("\", "/")
        bytes = $item.Length
        sha256 = (Get-FileHash -LiteralPath $item.FullName `
            -Algorithm SHA256).Hash
    }
}

function Get-SourceCodeFingerprint {
    $sourceRoots = @(
        (Join-Path $projectRoot "src\StructuralToolbox"),
        (Join-Path $projectRoot "src\StructuralToolbox.Contracts"),
        (Join-Path $projectRoot "src\StructuralToolbox.Rail"),
        (Join-Path $projectRoot "src\StructuralToolbox.Ballast"),
        (Join-Path $projectRoot "src\StructuralToolbox.ArrayRecognition"),
        (Join-Path $projectRoot "src\StructuralToolbox.Update"),
        (Join-Path $projectRoot "src\StructuralToolbox.DrawingTranslation")
    )
    $sourceFiles = @($sourceRoots | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -File
    })
    $items = @($sourceFiles |
        Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($projectRoot.Length).TrimStart("\")
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            "$($relative.Replace('\', '/'))|$hash"
        })
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes($items -join "`n"))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}

function Get-ArchiveInventory([string]$Root) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd("\")
    $prefix = $rootPath + "\"
    $rootManifestPath = Join-Path $rootPath "archive-manifest.json"
    $rootChecksumPath = Join-Path $rootPath "SHA256SUMS.txt"
    return @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force |
        Where-Object {
            $_.FullName -ne $rootManifestPath -and
            $_.FullName -ne $rootChecksumPath
        } |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($prefix.Length).Replace("\", "/")
                bytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName `
                    -Algorithm SHA256).Hash
            }
        })
}

function Get-RequiredVerificationLog(
    [string]$Path,
    [string]$Description,
    [string[]]$RequiredText) {
    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    foreach ($marker in $RequiredText) {
        if (-not $content.Contains($marker)) {
            throw "$Description lacks required marker '$marker'."
        }
    }
    return $content
}

function Get-SingleReceiptValue(
    [string]$Content,
    [string]$Name,
    [string]$Description) {
    $pattern = "(?m)^" + [regex]::Escape($Name) + "=([^\r\n]+)\r?$"
    $matches = [regex]::Matches($Content, $pattern)
    if ($matches.Count -ne 1) {
        throw "$Description must contain exactly one $Name receipt."
    }
    return $matches[0].Groups[1].Value.Trim()
}

$releaseRoot = Resolve-RequiredDirectory $ReleaseDirectory "Release directory"
Assert-Under $releaseRoot $outputsRoot "Release directory"
$expectedReleaseName = "SijiPVToolbox_v${version3}_AutoCAD2020"
if ((Split-Path -Leaf $releaseRoot) -ne $expectedReleaseName) {
    throw "Release directory must be the exact v$version3 output."
}
$expectedReleaseFileNames = @(
    "四季光伏工具箱_Setup_v${version3}.exe"
)
$releaseTopLevelDirectories = @(Get-ChildItem -LiteralPath $releaseRoot `
    -Directory -Force)
if ($releaseTopLevelDirectories.Count -ne 0) {
    throw "Release directory must contain top-level files only."
}
$releaseTopLevelFiles = @(Get-ChildItem -LiteralPath $releaseRoot -File -Force)
Assert-AtomicExactNameSet $expectedReleaseFileNames `
    @($releaseTopLevelFiles.Name) "Release top-level file allowlist"
$buildResultPath = Resolve-RequiredFile $BuildResult "Build result"
$releaseEvidencePath = Resolve-RequiredFile $ReleaseEvidence `
    "Schema-5 release evidence"
$releaseEvidenceName = "四季光伏工具箱_v$version3`_发布证据.json"
if ((Split-Path -Leaf $releaseEvidencePath) -ne $releaseEvidenceName) {
    throw "Release evidence filename is invalid for v$version3."
}
$releaseChecksumPath = Resolve-RequiredFile (Join-Path `
    (Split-Path -Parent $releaseEvidencePath) "SHA256SUMS.txt") `
    "Release evidence SHA256SUMS"
$releaseEvidenceRoot = Split-Path -Parent $releaseEvidencePath
if (@(Get-ChildItem -LiteralPath $releaseEvidenceRoot -Directory -Force).Count -ne 0) {
    throw "Release evidence directory must contain top-level files only."
}
Assert-AtomicExactNameSet @($releaseEvidenceName, "SHA256SUMS.txt") `
    @((Get-ChildItem -LiteralPath $releaseEvidenceRoot -File -Force).Name) `
    "Release evidence two-file allowlist"
$realResultPath = Resolve-RequiredFile $RealDrawingVerificationResult `
    "Real-drawing result"
$pureLogPath = Resolve-RequiredFile $PureCodeRegressionLog "Pure-code log"
$artifactLogPath = Resolve-RequiredFile $InstallerArtifactVerificationLog `
    "Installer artifact log"
$runtimeLogPath = Resolve-RequiredFile $InstallerRuntimeVerificationLog `
    "Installer runtime log"
$publishReceiptPath = Resolve-RequiredFile $PublishReceipt "Publish receipt"
foreach ($path in @($buildResultPath, $releaseEvidencePath,
    $releaseChecksumPath, $realResultPath, $pureLogPath,
    $artifactLogPath, $runtimeLogPath, $publishReceiptPath)) {
    Assert-Under $path $workRoot "Evidence input"
}
$autoCadResultPaths = @($AutoCADVerificationResults | ForEach-Object {
    $path = Resolve-RequiredFile $_ "AutoCAD verification result"
    Assert-Under $path $workRoot "AutoCAD verification result"
    $path
})
if ($autoCadResultPaths.Count -eq 0) {
    throw "At least one AutoCAD verification result is required."
}

$realResult = Get-Content -LiteralPath $realResultPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$realRunRoot = (Split-Path -Parent $realResultPath).TrimEnd("\")
$realSettingsPath = Resolve-RequiredFile (Join-Path $realRunRoot `
    "isolate\Roaming\StructuralToolbox\rail-settings.xml") `
    "Real-drawing isolated settings"
Assert-Under $realSettingsPath $realRunRoot "Real-drawing isolated settings"
$realSourcePath = Resolve-RequiredFile `
    ([string]$realResult.sourceDrawing.path) "Real source drawing"
Assert-Under $realSourcePath $projectRoot "Real source drawing"
$archiveCopyPlanBeforeValidation = Get-ArchiveCopyPlan `
    $realSettingsPath $realSourcePath

$build = Get-Content -LiteralPath $buildResultPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([int]$build.schemaVersion -lt 6 -or [bool]$build.testBuild -or
    -not [bool]$build.unsignedDistributionBuild -or
    [string]$build.releaseKind -ne "unsigned-distribution" -or
    [string]$build.releaseClassification -ne "UNSIGNED" -or
    [string]$build.authenticodeExpectation -ne "NotSigned" -or
    -not [bool]$build.publicDistributionAllowed -or
    ([version]([string]$build.appVersion)).ToString(3) -ne $version3 -or
    [string]$build.sourceCodeSha256 -ne (Get-SourceCodeFingerprint)) {
    throw "Build result is not the final source-bound v$version3 candidate."
}
$candidatePayload = @($build.payloadModules | Where-Object {
    [string]$_.series -eq "R23.1"
})
$verifiedSeries = @($build.verifiedSeries)
if ($candidatePayload.Count -ne 1 -or
    $verifiedSeries.Count -ne 1 -or
    [string]$verifiedSeries[0] -ne "R23.1") {
    throw "Build result must contain exactly one verified R23.1 payload."
}
$releaseInstallerPath = Resolve-RequiredFile (Join-Path $releaseRoot `
    "四季光伏工具箱_Setup_v${version3}.exe") `
    "Published installer"
$releaseInstallerHash = (Get-FileHash -LiteralPath $releaseInstallerPath `
    -Algorithm SHA256).Hash
if ($releaseInstallerHash -ne [string]$build.installerSha256) {
    throw "Published installer hash differs from the final build result."
}
if ([string]$realResult.status -ne "PASS" -or
    [string]$realResult.mode -ne "RailsOnly" -or
    [bool]$realResult.formalCandidateExecuted) {
    throw "Real-drawing verification result is not a PASS/RailsOnly bounded result."
}
foreach ($path in $autoCadResultPaths) {
    $autoResult = Get-Content -LiteralPath $path -Raw -Encoding UTF8 |
        ConvertFrom-Json
    if ([string]$autoResult.Status -ne "PASS" -or
        [int]$autoResult.CoreConsoleExitCode -ne 0) {
        throw "AutoCAD verification result did not pass: $path"
    }
}

$buildResultHash = (Get-FileHash -LiteralPath $buildResultPath `
    -Algorithm SHA256).Hash
$pureLogContent = Get-RequiredVerificationLog $pureLogPath "Pure-code log" @(
    "rail layout tests",
    "release traceability source tests",
    "PURE_CODE_REGRESSION=PASS")
$artifactLogContent = Get-RequiredVerificationLog $artifactLogPath `
    "Installer artifact log" @(
        "PASS: SDK build provenance hash is chained through inventory and build result",
        "PASS: unsigned installer uses the normal transferable filename",
        "PASS: INSTALLER_ARTIFACT_VERIFICATION=PASS")
$runtimeLogContent = Get-RequiredVerificationLog $runtimeLogPath `
    "Installer runtime log" @(
        "PASS: isolated installer uses a frozen release-candidate payload",
        "PASS: installed payload hash matches frozen candidate",
        "PASS: RUNTIME_VERIFICATION=PASS")

$pureVersion = Get-SingleReceiptValue $pureLogContent "RELEASE_VERSION" `
    "Pure-code log"
$pureSourceHash = Get-SingleReceiptValue $pureLogContent `
    "SOURCE_CODE_SHA256" "Pure-code log"
$pureRegressionSuiteHash = Get-SingleReceiptValue $pureLogContent `
    "REGRESSION_SUITE_SHA256" "Pure-code log"
$currentRegressionSuiteHash = Get-RegressionSuiteFingerprint $projectRoot
if ($pureVersion -ne $version3 -or
    $pureSourceHash -ne [string]$build.sourceCodeSha256 -or
    $pureRegressionSuiteHash -ne $currentRegressionSuiteHash) {
    throw "Pure-code receipt is not bound to this source, version, and " +
        "regression suite."
}

$artifactVersion = Get-SingleReceiptValue $artifactLogContent `
    "RELEASE_VERSION" "Installer artifact log"
$artifactSourceHash = Get-SingleReceiptValue $artifactLogContent `
    "SOURCE_CODE_SHA256" "Installer artifact log"
$artifactBuildHash = Get-SingleReceiptValue $artifactLogContent `
    "BUILD_RESULT_SHA256" "Installer artifact log"
$artifactInstallerHash = Get-SingleReceiptValue $artifactLogContent `
    "INSTALLER_SHA256" "Installer artifact log"
$artifactPayloadHash = Get-SingleReceiptValue $artifactLogContent `
    "PAYLOAD_SHA256" "Installer artifact log"
$artifactPayloadFileCount = Get-SingleReceiptValue $artifactLogContent `
    "PAYLOAD_FILE_COUNT" "Installer artifact log"
$artifactPayloadFilesHash = Get-SingleReceiptValue $artifactLogContent `
    "PAYLOAD_FILES_SHA256" "Installer artifact log"
$candidatePayloadFiles = @($build.payloadFiles | Where-Object {
    [string]$_.series -eq "R23.1"
})
$expectedPayloadFilesHash = Get-PayloadSetSha256 $candidatePayloadFiles `
    "name" "sourceSha256"
if ($artifactVersion -ne $version3 -or
    $artifactSourceHash -ne [string]$build.sourceCodeSha256 -or
    $artifactBuildHash -ne $buildResultHash -or
    $artifactInstallerHash -ne [string]$build.installerSha256 -or
    $artifactPayloadHash -ne [string]$candidatePayload[0].sourceSha256 -or
    $artifactPayloadFileCount -ne "6" -or
    $candidatePayloadFiles.Count -ne 6 -or
    $artifactPayloadFilesHash -ne $expectedPayloadFilesHash) {
    throw "Installer artifact receipt is not bound to this candidate."
}

$runtimeTestBuildResultPath = Resolve-RequiredFile (Join-Path `
    (Split-Path -Parent $runtimeLogPath) "build-result.json") `
    "Installer runtime test build result"
Assert-Under $runtimeTestBuildResultPath $workRoot `
    "Installer runtime test build result"
$runtimeTestBuild = Get-Content -LiteralPath $runtimeTestBuildResultPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$runtimeOrigin = $runtimeTestBuild.payloadOrigin
$runtimeVersion = Get-SingleReceiptValue $runtimeLogContent `
    "RELEASE_VERSION" "Installer runtime log"
$runtimeFrozenBuildHash = Get-SingleReceiptValue $runtimeLogContent `
    "FROZEN_BUILD_RESULT_SHA256" "Installer runtime log"
$runtimeFrozenInstallerHash = Get-SingleReceiptValue $runtimeLogContent `
    "FROZEN_INSTALLER_SHA256" "Installer runtime log"
$runtimeTestBuildHash = Get-SingleReceiptValue $runtimeLogContent `
    "TEST_BUILD_RESULT_SHA256" "Installer runtime log"
$runtimeTestInstallerHash = Get-SingleReceiptValue $runtimeLogContent `
    "TEST_INSTALLER_SHA256" "Installer runtime log"
$runtimeInstalledPayloadSeries = Get-SingleReceiptValue $runtimeLogContent `
    "INSTALLED_PAYLOAD_SERIES" "Installer runtime log"
$runtimeInstalledPayloadHash = Get-SingleReceiptValue $runtimeLogContent `
    "INSTALLED_PAYLOAD_SHA256" "Installer runtime log"
$runtimeInstalledPayloadFileCount = Get-SingleReceiptValue $runtimeLogContent `
    "INSTALLED_PAYLOAD_FILE_COUNT" "Installer runtime log"
$runtimeInstalledPayloadFilesHash = Get-SingleReceiptValue $runtimeLogContent `
    "INSTALLED_PAYLOAD_FILES_SHA256" "Installer runtime log"
$runtimeInstallerPath = Resolve-RequiredFile ([string]$runtimeTestBuild.installer) `
    "Installer runtime test installer"
Assert-Under $runtimeInstallerPath $workRoot "Installer runtime test installer"
$runtimePayload = @($runtimeTestBuild.payloadModules | Where-Object {
    [string]$_.series -eq $runtimeInstalledPayloadSeries
})
$runtimePayloadFiles = @($runtimeTestBuild.payloadFiles | Where-Object {
    [string]$_.series -eq $runtimeInstalledPayloadSeries
})
$expectedRuntimePayloadFilesHash = Get-PayloadSetSha256 $runtimePayloadFiles `
    "name" "stagedSha256"
if ([int]$runtimeTestBuild.schemaVersion -lt 6 -or
    -not [bool]$runtimeTestBuild.testBuild -or
    ([version]([string]$runtimeTestBuild.appVersion)).ToString(3) -ne $version3 -or
    [string]$runtimeTestBuild.sourceCodeSha256 -ne
        [string]$build.sourceCodeSha256 -or
    [string]$runtimeOrigin.mode -ne "frozen-release-candidate" -or
    [IO.Path]::GetFullPath([string]$runtimeOrigin.referenceBuildResult) -ne
        $buildResultPath -or
    [string]$runtimeOrigin.referenceBuildResultSha256 -ne $buildResultHash -or
    [string]$runtimeOrigin.referenceInstallerSha256 -ne
        [string]$build.installerSha256 -or
    $runtimeVersion -ne $version3 -or
    $runtimeFrozenBuildHash -ne $buildResultHash -or
    $runtimeFrozenInstallerHash -ne [string]$build.installerSha256 -or
    $runtimeTestBuildHash -ne
        (Get-FileHash -LiteralPath $runtimeTestBuildResultPath `
            -Algorithm SHA256).Hash -or
    $runtimeTestInstallerHash -ne
        (Get-FileHash -LiteralPath $runtimeInstallerPath -Algorithm SHA256).Hash -or
    $runtimeTestInstallerHash -ne [string]$runtimeTestBuild.installerSha256 -or
    $runtimeInstalledPayloadSeries -ne "R23.1" -or
    $runtimePayload.Count -ne 1 -or
    $runtimeInstalledPayloadHash -ne [string]$runtimePayload[0].stagedSha256 -or
    $runtimeInstalledPayloadHash -ne [string]$candidatePayload[0].sourceSha256 -or
    $runtimeInstalledPayloadFileCount -ne "6" -or
    $runtimePayloadFiles.Count -ne 6 -or
    $runtimeInstalledPayloadFilesHash -ne $expectedRuntimePayloadFilesHash -or
    $runtimeInstalledPayloadFilesHash -ne $expectedPayloadFilesHash) {
    throw "Installer runtime receipt is not bound to this candidate."
}

$publishReceipt = Get-Content -LiteralPath $publishReceiptPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$publishReceipt.schemaVersion -lt 2 -or
    [string]$publishReceipt.status -ne "PASS" -or
    [string]$publishReceipt.releaseVersion -ne $version3 -or
    [string]$publishReceipt.classification -ne "UNSIGNED" -or
    [string]$publishReceipt.releaseDirectory -ne
        (Get-RelativeProjectPath $releaseRoot) -or
    [string]$publishReceipt.buildResultSha256 -ne $buildResultHash -or
    [string]$publishReceipt.installerFileName -ne
        (Split-Path -Leaf $releaseInstallerPath) -or
    [string]$publishReceipt.installerSha256 -ne $releaseInstallerHash) {
    throw "Publish receipt is not bound to this release/build result."
}

$releaseEvidence = Get-Content -LiteralPath $releaseEvidencePath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedSourceDrawingSha256 =
    "95041A3827AC36BF021A354D3076215DCCE6ACC7A23203F23DAC7F896708D3E0"
$expectedEvidencePayloadFiles = @($candidatePayloadFiles | ForEach-Object {
    "$([string]$_.name)|$(([string]$_.sourceSha256).ToUpperInvariant())"
} | Sort-Object)
$recordedEvidencePayloadFiles = @($releaseEvidence.payloadFiles | ForEach-Object {
    "$([string]$_.name)|$(([string]$_.source.sha256).ToUpperInvariant())"
} | Sort-Object)
if ([int]$releaseEvidence.schemaVersion -ne 5 -or
    [string]$releaseEvidence.releaseVersion -ne $version3 -or
    [string]$releaseEvidence.releaseKind -ne "unsigned-distribution" -or
    [string]$releaseEvidence.releaseClassification -ne "UNSIGNED" -or
    [string]$releaseEvidence.authenticodeExpectation -ne "NotSigned" -or
    -not [bool]$releaseEvidence.publicDistributionAllowed -or
    [string]$releaseEvidence.releaseDirectory -ne
        (Get-RelativeProjectPath $releaseRoot) -or
    [string]$releaseEvidence.buildResult.sha256 -ne $buildResultHash -or
    [string]$releaseEvidence.installer.sha256 -ne
        [string]$build.installerSha256 -or
    $recordedEvidencePayloadFiles.Count -ne 6 -or
    @(Compare-Object $expectedEvidencePayloadFiles `
        $recordedEvidencePayloadFiles).Count -ne 0 -or
    [string]$releaseEvidence.regressionSuiteSha256 -ne
        $currentRegressionSuiteHash -or
    [string]$releaseEvidence.verificationLogs.pureCodeRegression.sha256 -ne
        (Get-FileHash -LiteralPath $pureLogPath -Algorithm SHA256).Hash -or
    [string]$releaseEvidence.verificationLogs.installerArtifacts.sha256 -ne
        (Get-FileHash -LiteralPath $artifactLogPath -Algorithm SHA256).Hash -or
    [string]$releaseEvidence.verificationLogs.installerRuntime.sha256 -ne
        (Get-FileHash -LiteralPath $runtimeLogPath -Algorithm SHA256).Hash -or
    [string]$releaseEvidence.verificationLogs.installerRuntimeBuildResult.sha256 -ne
        (Get-FileHash -LiteralPath $runtimeTestBuildResultPath `
            -Algorithm SHA256).Hash -or
    [string]$releaseEvidence.verificationLogs.publishReceipt.sha256 -ne
        (Get-FileHash -LiteralPath $publishReceiptPath -Algorithm SHA256).Hash -or
    [string]$releaseEvidence.realDrawingVerification.status -ne "PASS" -or
    [string]$releaseEvidence.realDrawingVerification.mode -ne "RailsOnly" -or
    [bool]$releaseEvidence.realDrawingVerification.formalCandidateExecuted -or
    [string]$releaseEvidence.realDrawingVerification.sourceDrawing.sha256 -ne
        $expectedSourceDrawingSha256 -or
    [int]$releaseEvidence.realDrawingVerification.baseline.recognizedRectangles -ne
        3520 -or
    [int]$releaseEvidence.realDrawingVerification.baseline.savedRails -ne 1760 -or
    -not [bool]$releaseEvidence.realDrawingVerification.baseline.nativeDimensionsUnchanged -or
    -not [bool]$releaseEvidence.realDrawingVerification.baseline.allMetadataV3 -or
    -not [bool]$releaseEvidence.realDrawingVerification.baseline.sourceDrawingUnchanged -or
    [string]$releaseEvidence.realDrawingVerification.artifacts.result.sha256 -ne
        (Get-FileHash -LiteralPath $realResultPath -Algorithm SHA256).Hash) {
    throw "Schema-5 release evidence is not bound to the supplied final evidence."
}
$expectedAutoCadHashes = @($autoCadResultPaths | ForEach-Object {
    (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
} | Sort-Object)
$recordedAutoCadHashes = @($releaseEvidence.runtimeVerifications |
    ForEach-Object { [string]$_.result.sha256 } | Sort-Object)
if ($expectedAutoCadHashes.Count -ne $recordedAutoCadHashes.Count -or
    @(Compare-Object $expectedAutoCadHashes $recordedAutoCadHashes).Count -ne 0) {
    throw "Schema-5 release evidence is not bound to all AutoCAD results."
}

$releaseChecksumByName = @{}
foreach ($line in Get-Content -LiteralPath $releaseChecksumPath -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $match = [regex]::Match($line, '^([0-9A-Fa-f]{64})  ([^\\/\r\n]+)$')
    if (-not $match.Success -or
        $releaseChecksumByName.ContainsKey($match.Groups[2].Value)) {
        throw "Release evidence SHA256SUMS contains an invalid or duplicate entry: $line"
    }
    $releaseChecksumByName[$match.Groups[2].Value] =
        $match.Groups[1].Value.ToUpperInvariant()
}
if ($releaseChecksumByName.Count -ne 1 -or
    -not $releaseChecksumByName.ContainsKey($releaseEvidenceName) -or
    $releaseChecksumByName[$releaseEvidenceName] -ne
        (Get-FileHash -LiteralPath $releaseEvidencePath -Algorithm SHA256).Hash) {
    throw "Release evidence SHA256SUMS must cover exactly the schema-5 evidence."
}

$archiveCopyPlanAfterValidation = Get-ArchiveCopyPlan `
    $realSettingsPath $realSourcePath
Assert-ArchiveCopyPlansEqual $archiveCopyPlanBeforeValidation `
    $archiveCopyPlanAfterValidation `
    "Archive inputs changed during semantic validation"

New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
try {
    $sourceDestination = Join-Path $stageRoot "files"
    foreach ($relativePath in @("AGENTS.md", "README.md")) {
        Copy-FileTo (Join-Path $projectRoot $relativePath) `
            (Join-Path $sourceDestination $relativePath)
    }
    foreach ($relativePath in @("src", "scripts", "tests", "bundle", "docs")) {
        Copy-DirectoryTo (Join-Path $projectRoot $relativePath) `
            (Join-Path $sourceDestination $relativePath)
    }
    $archivedSourceHash = Get-StructuralSourceFingerprint $sourceDestination
    $archivedRegressionSuiteHash =
        Get-RegressionSuiteFingerprint $sourceDestination
    if ($archivedSourceHash -ne [string]$build.sourceCodeSha256 -or
        $archivedRegressionSuiteHash -ne $currentRegressionSuiteHash) {
        throw "Archived source or regression suite changed during archive creation."
    }

    Copy-DirectoryTo $releaseRoot (Join-Path $stageRoot "release")
    Copy-FileTo $releaseEvidencePath (Join-Path $stageRoot `
        "evidence\release-controls\$releaseEvidenceName")
    Copy-FileTo $releaseChecksumPath (Join-Path $stageRoot `
        "evidence\release-controls\SHA256SUMS.txt")
    Copy-DirectoryTo (Split-Path -Parent $buildResultPath) `
        (Join-Path $stageRoot "evidence\release-candidate")
    Copy-DirectoryTo (Split-Path -Parent $runtimeLogPath) `
        (Join-Path $stageRoot "evidence\installer-runtime")
    Copy-FileTo $pureLogPath `
        (Join-Path $stageRoot "evidence\logs\pure-code-regression.log")
    Copy-FileTo $artifactLogPath `
        (Join-Path $stageRoot "evidence\logs\installer-artifacts.log")
    Copy-FileTo $runtimeLogPath `
        (Join-Path $stageRoot "evidence\logs\installer-runtime.log")
    Copy-FileTo $publishReceiptPath `
        (Join-Path $stageRoot "evidence\logs\publish-result.json")

    for ($index = 0; $index -lt $autoCadResultPaths.Count; $index++) {
        Copy-DirectoryTo (Split-Path -Parent $autoCadResultPaths[$index]) `
            (Join-Path $stageRoot ("evidence\autocad-{0}" -f $index)) `
            @("isolate", "temp")
    }
    Copy-DirectoryTo (Split-Path -Parent $realResultPath) `
        (Join-Path $stageRoot "evidence\real-drawing") `
        @("isolate", "temp")
    Copy-FileTo $realSettingsPath (Join-Path $stageRoot `
        "evidence\real-drawing\isolate\Roaming\StructuralToolbox\rail-settings.xml")

    Copy-FileTo $realSourcePath (Join-Path $stageRoot `
        ("evidence\real-drawing-source\" +
            (Split-Path -Leaf $realSourcePath)))

    Assert-ArchiveStageMatchesPlan $archiveCopyPlanBeforeValidation $stageRoot

    $stagedBuildResultPath = Join-Path $stageRoot `
        "evidence\release-candidate\build-result.json"
    $stagedBuild = Get-Content -LiteralPath $stagedBuildResultPath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    $stagedReleaseRoot = Join-Path $stageRoot "release"
    $stagedInstallerPath = Join-Path $stagedReleaseRoot `
        "四季光伏工具箱_Setup_v${version3}.exe"
    $stagedReleaseEvidencePath = Join-Path $stageRoot `
        "evidence\release-controls\$releaseEvidenceName"
    $stagedReleaseChecksumPath = Join-Path $stageRoot `
        "evidence\release-controls\SHA256SUMS.txt"
    $stagedInputEvidence = @(
        Get-StagedInputEvidence $stageRoot `
            "evidence/release-candidate/build-result.json" `
            "final-build-result"
        Get-StagedInputEvidence $stageRoot `
            ("evidence/release-controls/" + $releaseEvidenceName) `
            "release-evidence"
        Get-StagedInputEvidence $stageRoot `
            "evidence/real-drawing/result.json" "real-drawing-result"
        Get-StagedInputEvidence $stageRoot `
            "evidence/logs/pure-code-regression.log" "pure-code-log"
        Get-StagedInputEvidence $stageRoot `
            "evidence/logs/installer-artifacts.log" "installer-artifact-log"
        Get-StagedInputEvidence $stageRoot `
            "evidence/logs/installer-runtime.log" "installer-runtime-log"
        Get-StagedInputEvidence $stageRoot `
            "evidence/logs/publish-result.json" "publish-receipt"
    )
    for ($index = 0; $index -lt $autoCadResultPaths.Count; $index++) {
        $stagedInputEvidence += Get-StagedInputEvidence $stageRoot `
            ("evidence/autocad-{0}/result.json" -f $index) "autocad-result"
    }

    $manifest = [ordered]@{
        schemaVersion = 3
        application = "四季光伏工具箱"
        archivedVersion = $version3
        classification = "UNSIGNED"
        authenticodeExpectation = "NotSigned"
        publicDistributionAllowed = $true
        archivedLocal = [DateTime]::Now.ToString("o")
        archivedUtc = [DateTime]::UtcNow.ToString("o")
        generator = [ordered]@{
            path = "scripts/New-ReleaseArchive.ps1"
            sha256 = (Get-FileHash -LiteralPath (Join-Path `
                $sourceDestination "scripts\New-ReleaseArchive.ps1") `
                -Algorithm SHA256).Hash
            verifierPath = "scripts/Test-ReleaseArchive.ps1"
            verifierSha256 = (Get-FileHash -LiteralPath (Join-Path `
                $sourceDestination "scripts\Test-ReleaseArchive.ps1") `
                -Algorithm SHA256).Hash
        }
        build = [ordered]@{
            sourceCodeSha256 = Get-StructuralSourceFingerprint $sourceDestination
            buildResultSha256 = (Get-FileHash -LiteralPath `
                $stagedBuildResultPath -Algorithm SHA256).Hash
            installerSha256 = (Get-FileHash -LiteralPath `
                $stagedInstallerPath -Algorithm SHA256).Hash
            verifiedSeries = @($stagedBuild.verifiedSeries)
        }
        verificationBoundary = [ordered]@{
            formalCandidateExecutedInRealDrawingRun = $false
            realDrawingExecutionAssembly = "FourJTools.RealDrawingVerify"
            realDrawingBasis = "same-source-internal-instrumentation"
        }
        regressionSuiteSha256 =
            (Get-RegressionSuiteFingerprint $sourceDestination)
        releaseControls = [ordered]@{
            evidencePath = "evidence/release-controls/$releaseEvidenceName"
            evidenceSha256 = (Get-FileHash -LiteralPath `
                $stagedReleaseEvidencePath -Algorithm SHA256).Hash
            checksumPath = "evidence/release-controls/SHA256SUMS.txt"
            checksumSha256 = (Get-FileHash -LiteralPath `
                $stagedReleaseChecksumPath -Algorithm SHA256).Hash
        }
        inputs = $stagedInputEvidence
        inventory = @(Get-ArchiveInventory $stageRoot)
    }
    $manifestPath = Join-Path $stageRoot "archive-manifest.json"
    [IO.File]::WriteAllText($manifestPath,
        ($manifest | ConvertTo-Json -Depth 10), $utf8NoBom)

    $checksumPath = Join-Path $stageRoot "SHA256SUMS.txt"
    $stagePrefix = [IO.Path]::GetFullPath($stageRoot).TrimEnd("\") + "\"
    $checksumLines = @(Get-ChildItem -LiteralPath $stageRoot -Recurse `
        -File -Force | Where-Object { $_.FullName -ne $checksumPath } |
        Sort-Object FullName | ForEach-Object {
            $relative = $_.FullName.Substring($stagePrefix.Length).
                Replace("\", "/")
            "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash, $relative
        })
    [IO.File]::WriteAllLines($checksumPath, $checksumLines, $utf8NoBom)

    $archiveVerifierPath = Join-Path $projectRoot `
        "scripts\Test-ReleaseArchive.ps1"
    $archiveVerifier = {
        param([string]$CandidateArchive)
        $candidateLeaf = Split-Path -Leaf $CandidateArchive
        if ($candidateLeaf.StartsWith(".staging-v",
                [StringComparison]::OrdinalIgnoreCase)) {
            & $archiveVerifierPath -ArchiveDirectory $CandidateArchive `
                -AllowStagingDirectory
        } else {
            & $archiveVerifierPath -ArchiveDirectory $CandidateArchive
        }
    }
    Invoke-ReleaseArchiveAtomicCommit `
        -StageRoot $stageRoot `
        -ArchiveRoot $archiveRoot `
        -ArchiveBase $archiveBase `
        -Verifier $archiveVerifier
} catch {
    if (Test-Path -LiteralPath $stageRoot -PathType Container) {
        Remove-AtomicStagingDirectory $stageRoot `
            ($archiveBase.TrimEnd("\") + "\.staging-v")
    }
    throw
}

Write-Host "RELEASE_ARCHIVE=PASS"
Write-Host "Archive: $archiveRoot"
