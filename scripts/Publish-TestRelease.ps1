[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$BuildResult,

    [Parameter(DontShow = $true)]
    [ValidateSet("", "AfterReleaseMove")]
    [string]$InternalTestFailure = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$outputsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "outputs"))
$workRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "work"))
$utf8NoBom = New-Object Text.UTF8Encoding($false)
if (-not [string]::IsNullOrWhiteSpace($InternalTestFailure) -and
    $projectRoot.IndexOf("\work\publish-atomic-regression\",
        [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw "Internal publish fault injection is restricted to its work fixture."
}

try {
    $parsedVersion = [version]$Version
} catch {
    throw "Version is invalid: $Version"
}
$version3 = "{0}.{1}.{2}" -f $parsedVersion.Major,
    $parsedVersion.Minor, [Math]::Max(0, $parsedVersion.Build)
function Resolve-RequiredFile([string]$Value, [string]$Description) {
    $path = [IO.Path]::GetFullPath($Value)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$Description was not found: $path"
    }
    return $path
}

function Assert-Under([string]$Path, [string]$Root, [string]$Description) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd("\") + "\"
    if (-not ([IO.Path]::GetFullPath($Path)).StartsWith($prefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay under $Root`: $Path"
    }
}

$buildResultPath = Resolve-RequiredFile $BuildResult "Build result"
Assert-Under $buildResultPath $workRoot "Build result"
$build = Get-Content -LiteralPath $buildResultPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$buildVersion = [version]([string]$build.appVersion)
if ([int]$build.schemaVersion -lt 5 -or [bool]$build.testBuild -or
    -not [bool]$build.unsignedDistributionBuild -or
    [string]$build.releaseKind -ne "unsigned-distribution" -or
    [string]$build.releaseClassification -ne "UNSIGNED" -or
    [string]$build.authenticodeExpectation -ne "NotSigned" -or
    -not [bool]$build.publicDistributionAllowed -or
    $buildVersion.ToString(3) -ne $version3) {
    throw "Build result is not the v$version3 UNSIGNED distribution candidate."
}

$installerPath = Resolve-RequiredFile ([string]$build.installer) `
    "Candidate installer"
Assert-Under $installerPath $workRoot "Candidate installer"
if ((Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash -ne
    [string]$build.installerSha256) {
    throw "Candidate installer hash differs from build-result.json."
}

New-Item -ItemType Directory -Path $outputsRoot -Force | Out-Null
$releaseName = "SijiPVToolbox_v${version3}_AutoCAD2020"
$releaseRoot = Join-Path $outputsRoot $releaseName
$archiveRoot = Join-Path $outputsRoot "archive"
$releaseDirectoryPattern = '^SijiPVToolbox_v\d+\.\d+\.\d+_AutoCAD2020$'
$independentDeliveryPattern =
    '^StructuralToolbox\.[A-Za-z0-9.]+_v\d+\.\d+\.\d+_AutoCAD2020$'
$translationPreviewPattern =
    '^StructuralToolbox\.DrawingTranslation_preview[1-9]\d*_AutoCAD2020$'
$updateReleasePattern = '^update-release-v\d+\.\d+\.\d+$'
$existingEntries = @(Get-ChildItem -LiteralPath $outputsRoot -Force)
$existingArchiveEntries = @($existingEntries | Where-Object {
    $_.Name -ieq "archive"
})
if ($existingArchiveEntries.Count -gt 1 -or
    ($existingArchiveEntries.Count -eq 1 -and
        -not $existingArchiveEntries[0].PSIsContainer)) {
    throw "outputs/archive must be a directory when present."
}
$unsupportedEntries = @($existingEntries | Where-Object {
    $_.Name -ine "archive" -and
    $_.Name -notmatch $releaseDirectoryPattern -and
    (-not $_.PSIsContainer -or
        ($_.Name -notmatch $independentDeliveryPattern -and
         $_.Name -notmatch $translationPreviewPattern -and
         $_.Name -notmatch $updateReleasePattern))
})
if ($unsupportedEntries.Count -gt 0) {
    throw "outputs contains an unsupported entry '$($unsupportedEntries[0].Name)'. " +
        "Only toolbox release directories, outputs/archive, and versioned " +
        "independent or update-release deliverables are allowed."
}
$existingReleaseEntries = @($existingEntries | Where-Object {
    $_.PSIsContainer -and $_.Name -match $releaseDirectoryPattern
})
$archivedReleaseNames = New-Object Collections.Generic.List[string]
foreach ($entry in $existingReleaseEntries) {
    if ([string]::Equals($entry.Name, $releaseName,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Release directory already exists and will not be overwritten: $releaseRoot"
    }
    $archiveDestination = Join-Path $archiveRoot $entry.Name
    if (Test-Path -LiteralPath $archiveDestination) {
        throw "Release archive already exists and will not be overwritten: $archiveDestination"
    }
    $archivedReleaseNames.Add($entry.Name)
}
if (Test-Path -LiteralPath $releaseRoot) {
    throw "Release directory already exists and will not be overwritten: $releaseRoot"
}
$stageRoot = Join-Path $outputsRoot `
    (".staging-{0}-{1}" -f $releaseName, [guid]::NewGuid().ToString("N"))
$archiveStageRoot = if ($archivedReleaseNames.Count -gt 0) {
    Join-Path $outputsRoot (".staging-archive-{0}" -f [guid]::NewGuid().ToString("N"))
} else {
    $null
}

$receiptRoot = Join-Path $workRoot ("publish-v{0}" -f $version3)
New-Item -ItemType Directory -Path $receiptRoot -Force | Out-Null
$receiptPath = Join-Path $receiptRoot `
    ((Get-Date -Format "yyyyMMdd-HHmmss-fff") + "-publish-result.json")
if (Test-Path -LiteralPath $receiptPath) {
    throw "Publish receipt already exists and will not be overwritten: $receiptPath"
}
$receiptStagePath = Join-Path $receiptRoot `
    (".staging-{0}-publish-result.json" -f [guid]::NewGuid().ToString("N"))
$receiptSafePrefix = $receiptRoot.TrimEnd("\") + "\.staging-"
$releaseCommitted = $false
$receiptCommitted = $false
$archiveRootCreated = $false
$stagedArchiveNames = New-Object Collections.Generic.List[string]
$committedArchiveNames = New-Object Collections.Generic.List[string]

New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
try {
    $installerName = (-join @(
        [char]0x56DB, [char]0x5B63, [char]0x5149, [char]0x4F0F,
        [char]0x5DE5, [char]0x5177, [char]0x7BB1
    )) + "_Setup_v${version3}.exe"
    Copy-Item -LiteralPath $installerPath `
        -Destination (Join-Path $stageRoot $installerName)

    if ((Get-FileHash -LiteralPath (Join-Path $stageRoot $installerName) `
        -Algorithm SHA256).Hash -ne [string]$build.installerSha256) {
        throw "Published installer copy differs from the candidate."
    }

    $stagedInstallerPath = Join-Path $stageRoot $installerName
    $receipt = [ordered]@{
        schemaVersion = 3
        status = "PASS"
        releaseVersion = $version3
        classification = "UNSIGNED"
        authenticodeExpectation = "NotSigned"
        generatedLocal = [DateTime]::Now.ToString("o")
        releaseDirectory = "outputs/$releaseName"
        archivedReleaseDirectories = @($archivedReleaseNames | ForEach-Object {
            "outputs/archive/$_"
        })
        buildResultSha256 = (Get-FileHash -LiteralPath $buildResultPath `
            -Algorithm SHA256).Hash
        installerFileName = $installerName
        installerSha256 = (Get-FileHash -LiteralPath $stagedInstallerPath `
            -Algorithm SHA256).Hash
    }
    [IO.File]::WriteAllText($receiptStagePath,
        ($receipt | ConvertTo-Json -Depth 5), $utf8NoBom)
    $stagedReceipt = Get-Content -LiteralPath $receiptStagePath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$stagedReceipt.schemaVersion -ne 3 -or
        [string]$stagedReceipt.status -ne "PASS" -or
        [string]$stagedReceipt.releaseVersion -ne $version3 -or
        [string]$stagedReceipt.classification -ne "UNSIGNED" -or
        [string]$stagedReceipt.buildResultSha256 -ne
            [string]$receipt.buildResultSha256 -or
        [string]$stagedReceipt.installerSha256 -ne
            [string]$build.installerSha256) {
        throw "Staged publish receipt failed self-validation."
    }

    if ($archivedReleaseNames.Count -gt 0) {
        New-Item -ItemType Directory -Path $archiveStageRoot -Force | Out-Null
        foreach ($archivedName in $archivedReleaseNames) {
            Move-Item -LiteralPath (Join-Path $outputsRoot $archivedName) `
                -Destination (Join-Path $archiveStageRoot $archivedName)
            $stagedArchiveNames.Add($archivedName)
        }
    }
    Move-Item -LiteralPath $stageRoot -Destination $releaseRoot
    $releaseCommitted = $true
    if ($archivedReleaseNames.Count -gt 0) {
        if (-not (Test-Path -LiteralPath $archiveRoot -PathType Container)) {
            New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
            $archiveRootCreated = $true
        }
        foreach ($archivedName in $archivedReleaseNames) {
            Move-Item -LiteralPath (Join-Path $archiveStageRoot $archivedName) `
                -Destination (Join-Path $archiveRoot $archivedName)
            $committedArchiveNames.Add($archivedName)
        }
    }
    if ($null -ne $archiveStageRoot -and
        (Test-Path -LiteralPath $archiveStageRoot -PathType Container)) {
        Remove-Item -LiteralPath $archiveStageRoot -Recurse -Force
    }
    if ($InternalTestFailure -eq "AfterReleaseMove") {
        throw "Simulated receipt commit failure after release move."
    }
    [IO.File]::Move($receiptStagePath, $receiptPath)
    $receiptCommitted = $true
} catch {
    $publishError = $_
    $rollbackErrors = New-Object Collections.Generic.List[string]
    if ($releaseCommitted -and -not $receiptCommitted -and
        (Test-Path -LiteralPath $releaseRoot -PathType Container)) {
        try {
            if (Test-Path -LiteralPath $stageRoot) {
                throw "Staging path unexpectedly exists before release rollback."
            }
            Move-Item -LiteralPath $releaseRoot -Destination $stageRoot
            $releaseCommitted = $false
        } catch {
            $rollbackErrors.Add("release directory: $($_.Exception.Message)")
        }
    }
    if ($null -ne $archiveStageRoot) {
        if ($committedArchiveNames.Count -gt 0 -and
            -not (Test-Path -LiteralPath $archiveStageRoot -PathType Container)) {
            try {
                New-Item -ItemType Directory -Path $archiveStageRoot -Force | Out-Null
            } catch {
                $rollbackErrors.Add("archive staging directory: $($_.Exception.Message)")
            }
        }
        foreach ($archivedName in $committedArchiveNames) {
            $archiveDestination = Join-Path $archiveRoot $archivedName
            $archiveStageDestination = Join-Path $archiveStageRoot $archivedName
            try {
                if (Test-Path -LiteralPath $archiveStageDestination) {
                    throw "Archive staging destination unexpectedly exists."
                }
                if (Test-Path -LiteralPath $archiveDestination) {
                    Move-Item -LiteralPath $archiveDestination `
                        -Destination $archiveStageDestination
                }
            } catch {
                $rollbackErrors.Add("archived release $archivedName`: $($_.Exception.Message)")
            }
        }
        foreach ($archivedName in $stagedArchiveNames) {
            $archiveStageDestination = Join-Path $archiveStageRoot $archivedName
            $originalDestination = Join-Path $outputsRoot $archivedName
            try {
                if (Test-Path -LiteralPath $archiveStageDestination) {
                    if (Test-Path -LiteralPath $originalDestination) {
                        throw "Original release destination unexpectedly exists."
                    }
                    Move-Item -LiteralPath $archiveStageDestination `
                        -Destination $originalDestination
                }
            } catch {
                $rollbackErrors.Add("release restore $archivedName`: $($_.Exception.Message)")
            }
        }
    }
    if ($archiveRootCreated -and
        (Test-Path -LiteralPath $archiveRoot -PathType Container) -and
        @(Get-ChildItem -LiteralPath $archiveRoot -Force).Count -eq 0) {
        try {
            Remove-Item -LiteralPath $archiveRoot -Force
        } catch {
            $rollbackErrors.Add("archive directory: $($_.Exception.Message)")
        }
    }
    if (Test-Path -LiteralPath $stageRoot -PathType Container) {
        $resolvedStage = [IO.Path]::GetFullPath($stageRoot)
        $safePrefix = $outputsRoot.TrimEnd("\") + "\.staging-"
        if ($resolvedStage.StartsWith($safePrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedStage -Recurse -Force
        }
    }
    if ($null -ne $archiveStageRoot -and
        (Test-Path -LiteralPath $archiveStageRoot -PathType Container)) {
        $resolvedArchiveStage = [IO.Path]::GetFullPath($archiveStageRoot)
        $archiveStagePrefix = $outputsRoot.TrimEnd("\") + "\.staging-archive-"
        if ($resolvedArchiveStage.StartsWith($archiveStagePrefix,
                [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedArchiveStage -Recurse -Force
        }
    }
    if (Test-Path -LiteralPath $receiptStagePath -PathType Leaf) {
        $resolvedReceiptStage = [IO.Path]::GetFullPath($receiptStagePath)
        if ($resolvedReceiptStage.StartsWith($receiptSafePrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
            [IO.File]::Delete($resolvedReceiptStage)
        }
    }
    if ($rollbackErrors.Count -gt 0) {
        throw ("Publish failed: {0}. Rollback also failed for {1}" -f
            $publishError.Exception.Message, ($rollbackErrors -join "; "))
    }
    throw $publishError
}

Write-Host "TEST_RELEASE_PUBLISH=PASS"
Write-Host "Release: $releaseRoot"
if ($archivedReleaseNames.Count -gt 0) {
    Write-Host ("Archived releases: " +
        (@($archivedReleaseNames | ForEach-Object {
            "outputs/archive/$_"
        }) -join ", "))
}
Write-Host "Receipt: $receiptPath"
