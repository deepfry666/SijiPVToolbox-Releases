param()

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$fixtureBase = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    "work\publish-atomic-regression")).TrimEnd("\")
$fixtureRoot = Join-Path $fixtureBase ([guid]::NewGuid().ToString("N"))
$utf8NoBom = New-Object Text.UTF8Encoding($false)
$installerStem = (-join @(
    [char]0x56DB, [char]0x5B63, [char]0x5149, [char]0x4F0F,
    [char]0x5DE5, [char]0x5177, [char]0x7BB1
))
$installerNameV034 = $installerStem + "_Setup_v0.3.4.exe"
$installerNameV035 = $installerStem + "_Setup_v0.3.6.exe"
$passed = 0

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

try {
    $fixtureScripts = Join-Path $fixtureRoot "scripts"
    $fixtureWork = Join-Path $fixtureRoot "work"
    $fixtureOutputs = Join-Path $fixtureRoot "outputs"
    $candidateRoot = Join-Path $fixtureWork "candidate"
    $nextCandidateRoot = Join-Path $fixtureWork "candidate-v0.3.6"
    foreach ($directory in @($fixtureScripts, $fixtureOutputs,
            $candidateRoot, $nextCandidateRoot)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $publisherPath = Join-Path $fixtureScripts "Publish-TestRelease.ps1"
    Copy-Item -LiteralPath (Join-Path $projectRoot `
        "scripts\Publish-TestRelease.ps1") -Destination $publisherPath
    $installerPath = Join-Path $candidateRoot "candidate.exe"
    Write-Text $installerPath "isolated unsigned installer fixture"
    $installerHash = (Get-FileHash -LiteralPath $installerPath `
        -Algorithm SHA256).Hash
    $buildResultPath = Join-Path $candidateRoot "build-result.json"
    $buildResult = [ordered]@{
        schemaVersion = 5
        testBuild = $false
        unsignedDistributionBuild = $true
        releaseKind = "unsigned-distribution"
        releaseClassification = "UNSIGNED"
        authenticodeExpectation = "NotSigned"
        publicDistributionAllowed = $true
        appVersion = "0.3.4.0"
        installer = $installerPath
        installerSha256 = $installerHash
    }
    Write-Text $buildResultPath ($buildResult | ConvertTo-Json -Depth 5)

    $pwshPath = if ($PSVersionTable.PSEdition -eq "Core") { (Get-Command pwsh -ErrorAction Stop).Source } else { (Get-Command powershell.exe -ErrorAction Stop).Source }
    $oldErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $failureOutput = @(& $pwshPath -NoProfile -File $publisherPath `
        -Version 0.3.4 `
        -BuildResult $buildResultPath `
        -InternalTestFailure AfterReleaseMove 2>&1 |
        ForEach-Object { $_.ToString() })
    $failureExitCode = $LASTEXITCODE
    $ErrorActionPreference = $oldErrorAction

    Assert-Condition ($failureExitCode -ne 0) `
        "fault-injected publish must fail"
    Assert-Condition (($failureOutput -join "`n").Contains(
        "Simulated receipt commit failure after release move.")) `
        "fault-injected publish must reach the post-move failure point"
    Assert-Condition (@(Get-ChildItem -LiteralPath $fixtureOutputs -Force).Count `
        -eq 0) "failed publish must leave outputs empty"
    $receiptRoot = Join-Path $fixtureWork "publish-v0.3.4"
    Assert-Condition (@(Get-ChildItem -LiteralPath $receiptRoot -Force `
        -ErrorAction SilentlyContinue).Count -eq 0) `
        "failed publish must leave no committed or staged receipt"

    & $pwshPath -NoProfile -File $publisherPath `
        -Version 0.3.4 `
        -BuildResult $buildResultPath | Out-Null
    Assert-Condition ($LASTEXITCODE -eq 0) `
        "isolated publish must succeed without fault injection"
    $releaseRoot = Join-Path $fixtureOutputs `
        "SijiPVToolbox_v0.3.4_AutoCAD2020"
    Assert-Condition (@(Get-ChildItem -LiteralPath $releaseRoot -File).Count -eq 1) `
        "successful publish must commit the exact one-EXE release"
    $receipts = @(Get-ChildItem -LiteralPath $receiptRoot -File `
        -Filter "*-publish-result.json")
    Assert-Condition ($receipts.Count -eq 1) `
        "successful publish must commit exactly one receipt"
    $receipt = Get-Content -LiteralPath $receipts[0].FullName `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Condition ([string]$receipt.status -eq "PASS" -and
        [string]$receipt.installerSha256 -eq $installerHash) `
        "successful receipt must bind the committed installer"

    $invalidPreviewRoot = Join-Path $fixtureOutputs `
        "StructuralToolbox.DrawingTranslation_preview0_AutoCAD2020"
    New-Item -ItemType Directory -Path $invalidPreviewRoot | Out-Null
    $oldErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $invalidPreviewOutput = @(& $pwshPath -NoProfile -File $publisherPath `
        -Version 0.3.4 -BuildResult $buildResultPath 2>&1 |
        ForEach-Object { $_.ToString() })
    $invalidPreviewExitCode = $LASTEXITCODE
    $ErrorActionPreference = $oldErrorAction
    Assert-Condition ($invalidPreviewExitCode -ne 0 -and
        ($invalidPreviewOutput -join "`n").Contains("unsupported entry")) `
        "invalid translation preview names must still be rejected"
    Assert-Condition ((Get-FileHash -LiteralPath (Join-Path $releaseRoot `
        $installerNameV034) -Algorithm SHA256).Hash -eq $installerHash) `
        "unknown directory rejection must preserve the published installer"
    Remove-Item -LiteralPath $invalidPreviewRoot

    $independentRoot = Join-Path $fixtureOutputs `
        "StructuralToolbox.Layout_v0.1.2_AutoCAD2020"
    $independentPayload = Join-Path $independentRoot `
        "StructuralToolbox.Layout.dll"
    Write-Text $independentPayload "independent delivery fixture"
    $independentHash = (Get-FileHash -LiteralPath $independentPayload `
        -Algorithm SHA256).Hash
    $updateReleaseRoot = Join-Path $fixtureOutputs "update-release-v0.3.4"
    $updateReleasePayload = Join-Path $updateReleaseRoot "stable.json"
    Write-Text $updateReleasePayload '{"version":"0.3.4"}'
    $updateReleaseHash = (Get-FileHash -LiteralPath $updateReleasePayload `
        -Algorithm SHA256).Hash
    $translationPreviewRoot = Join-Path $fixtureOutputs `
        "StructuralToolbox.DrawingTranslation_preview10_AutoCAD2020"
    $translationPreviewPayload = Join-Path $translationPreviewRoot `
        "StructuralToolbox.DrawingTranslation.dll"
    Write-Text $translationPreviewPayload "translation preview fixture"
    $translationPreviewHash = (Get-FileHash -LiteralPath $translationPreviewPayload `
        -Algorithm SHA256).Hash

    $nextInstallerPath = Join-Path $nextCandidateRoot "candidate.exe"
    Write-Text $nextInstallerPath "isolated unsigned installer fixture v0.3.6"
    $nextInstallerHash = (Get-FileHash -LiteralPath $nextInstallerPath `
        -Algorithm SHA256).Hash
    $nextBuildResultPath = Join-Path $nextCandidateRoot "build-result.json"
    $nextBuildResult = [ordered]@{
        schemaVersion = 5
        testBuild = $false
        unsignedDistributionBuild = $true
        releaseKind = "unsigned-distribution"
        releaseClassification = "UNSIGNED"
        authenticodeExpectation = "NotSigned"
        publicDistributionAllowed = $true
        appVersion = "0.3.6.0"
        installer = $nextInstallerPath
        installerSha256 = $nextInstallerHash
    }
    Write-Text $nextBuildResultPath ($nextBuildResult | ConvertTo-Json -Depth 5)

    $oldErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $secondFailureOutput = @(& $pwshPath -NoProfile -File $publisherPath `
        -Version 0.3.6 `
        -BuildResult $nextBuildResultPath `
        -InternalTestFailure AfterReleaseMove 2>&1 |
        ForEach-Object { $_.ToString() })
    $secondFailureExitCode = $LASTEXITCODE
    $ErrorActionPreference = $oldErrorAction
    Assert-Condition ($secondFailureExitCode -ne 0) `
        "fault-injected replacement publish must fail"
    Assert-Condition (($secondFailureOutput -join "`n").Contains(
        "Simulated receipt commit failure after release move.")) `
        "replacement fault injection must reach the post-archive failure point"
    $archiveRoot = Join-Path $fixtureOutputs "archive"
    $archivedReleaseRoot = Join-Path $archiveRoot `
        "SijiPVToolbox_v0.3.4_AutoCAD2020"
    $currentReleaseRoot = Join-Path $fixtureOutputs `
        "SijiPVToolbox_v0.3.4_AutoCAD2020"
    $nextReleaseRoot = Join-Path $fixtureOutputs `
        "SijiPVToolbox_v0.3.6_AutoCAD2020"
    Assert-Condition ((Test-Path -LiteralPath $currentReleaseRoot -PathType Container) -and
        -not (Test-Path -LiteralPath $archivedReleaseRoot) -and
        -not (Test-Path -LiteralPath $nextReleaseRoot)) `
        "failed replacement publish must restore the prior release and archive state"
    Assert-Condition ((Test-Path -LiteralPath $independentPayload -PathType Leaf) -and
        (Get-FileHash -LiteralPath $independentPayload -Algorithm SHA256).Hash -eq
            $independentHash) `
        "failed replacement publish must preserve independent deliverables"
    Assert-Condition ((Test-Path -LiteralPath $updateReleasePayload -PathType Leaf) -and
        (Get-FileHash -LiteralPath $updateReleasePayload -Algorithm SHA256).Hash -eq
            $updateReleaseHash) `
        "failed replacement publish must preserve versioned update releases"
    Assert-Condition ((Get-FileHash -LiteralPath $translationPreviewPayload `
        -Algorithm SHA256).Hash -eq $translationPreviewHash) `
        "failed replacement publish must preserve translation previews"
    Assert-Condition (@(Get-ChildItem -LiteralPath $fixtureOutputs -Force |
            Where-Object { $_.Name -like ".staging-*" }).Count -eq 0) `
        "failed replacement publish must leave no output staging directories"

    & $pwshPath -NoProfile -File $publisherPath `
        -Version 0.3.6 -BuildResult $nextBuildResultPath | Out-Null
    Assert-Condition ($LASTEXITCODE -eq 0) `
        "replacement publish must succeed with automatic archival"
    Assert-Condition ((Test-Path -LiteralPath $archivedReleaseRoot -PathType Container) -and
        (Test-Path -LiteralPath $nextReleaseRoot -PathType Container)) `
        "replacement publish must retain the prior release in outputs/archive"
    $archivedInstallerPath = Join-Path $archivedReleaseRoot `
        $installerNameV034
    $nextPublishedInstallerPath = Join-Path $nextReleaseRoot `
        $installerNameV035
    Assert-Condition ((Get-FileHash -LiteralPath $archivedInstallerPath `
        -Algorithm SHA256).Hash -eq $installerHash -and
        (Get-FileHash -LiteralPath $nextPublishedInstallerPath `
            -Algorithm SHA256).Hash -eq $nextInstallerHash) `
        "automatic archival must preserve both versioned installer hashes"
    Assert-Condition ((Test-Path -LiteralPath $independentPayload -PathType Leaf) -and
        (Get-FileHash -LiteralPath $independentPayload -Algorithm SHA256).Hash -eq
            $independentHash) `
        "successful replacement publish must preserve independent deliverables"
    Assert-Condition ((Test-Path -LiteralPath $updateReleasePayload -PathType Leaf) -and
        (Get-FileHash -LiteralPath $updateReleasePayload -Algorithm SHA256).Hash -eq
            $updateReleaseHash) `
        "successful replacement publish must preserve versioned update releases"
    Assert-Condition ((Get-FileHash -LiteralPath $translationPreviewPayload `
        -Algorithm SHA256).Hash -eq $translationPreviewHash) `
        "successful replacement publish must preserve translation previews"
    $nextReceiptRoot = Join-Path $fixtureWork "publish-v0.3.6"
    $nextReceipts = @(Get-ChildItem -LiteralPath $nextReceiptRoot -File `
        -Filter "*-publish-result.json")
    Assert-Condition ($nextReceipts.Count -eq 1) `
        "replacement publish must commit exactly one receipt"
    $nextReceipt = Get-Content -LiteralPath $nextReceipts[0].FullName `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Condition ([string]$nextReceipt.status -eq "PASS" -and
        [string]$nextReceipt.installerSha256 -eq $nextInstallerHash -and
        @($nextReceipt.archivedReleaseDirectories).Count -eq 1 -and
        [string](@($nextReceipt.archivedReleaseDirectories)[0]) -eq
            "outputs/archive/SijiPVToolbox_v0.3.4_AutoCAD2020") `
        "replacement receipt must bind the new installer"
} finally {
    if (Test-Path -LiteralPath $fixtureRoot -PathType Container) {
        $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
        $safePrefix = $fixtureBase + "\"
        if ($resolvedFixture.StartsWith($safePrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
        }
    }
}

Write-Host "PASS $passed publish atomicity tests"
