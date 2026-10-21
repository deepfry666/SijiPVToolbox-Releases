param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
. (Join-Path $projectRoot "scripts\ReleaseTraceabilityAtomic.ps1")
$fixtureRoot = Join-Path $projectRoot `
    ("work\release-traceability-atomicity-{0}" -f `
        [guid]::NewGuid().ToString("N"))
$releaseRoot = Join-Path $fixtureRoot "release"
$workRoot = Join-Path $fixtureRoot "work"
$archiveBase = Join-Path $fixtureRoot "legacy-source"
$utf8NoBom = New-Object Text.UTF8Encoding($false)
$evidenceName = "evidence.json"
$checksumName = "SHA256SUMS.txt"
$allowedNames = @("payload.bin", $evidenceName, $checksumName)
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

function Get-TextHash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-ChecksumExactFiles(
    [string]$ChecksumPath,
    [string]$FileRoot,
    [string[]]$ExpectedNames,
    [string]$Description) {
    $records = [Collections.Generic.Dictionary[string,string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($line in @(Get-Content -LiteralPath $ChecksumPath -Encoding UTF8)) {
        $match = [regex]::Match($line,
            '^([0-9A-F]{64})  ([^\\/\r\n]+)$',
            [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($line) -and
            $match.Success -and
            -not $records.ContainsKey($match.Groups[2].Value)) `
            "$Description must contain unique exact checksum records"
        $records.Add($match.Groups[2].Value,
            $match.Groups[1].Value.ToUpperInvariant())
    }
    Assert-Condition ($records.Count -eq $ExpectedNames.Count -and
        @(Compare-Object @($ExpectedNames | Sort-Object) `
            @($records.Keys | Sort-Object)).Count -eq 0) `
        "$Description must cover the exact filename set"
    foreach ($name in $ExpectedNames) {
        $filePath = Join-Path $FileRoot $name
        Assert-Condition ((Test-Path -LiteralPath $filePath -PathType Leaf) -and
            $records[$name] -eq (Get-TextHash $filePath)) `
            "$Description must re-hash $name"
    }
}

function Invoke-ControlFailure([string]$FailurePoint) {
    $expectedMessage = switch ($FailurePoint) {
        "AfterEvidenceStageWrite" {
            "Simulated failure after staged evidence write."
        }
        "AfterEvidenceCommit" {
            "Simulated failure after release evidence commit."
        }
        "AfterChecksumCommit" {
            "Simulated failure after release checksum commit."
        }
        default { throw "Unknown failure point: $FailurePoint" }
    }
    $caught = $null
    try {
        Invoke-ReleaseControlAtomicCommit `
            -WorkRoot $workRoot `
            -ReleaseRoot $releaseRoot `
            -EvidenceName $evidenceName `
            -ChecksumName $checksumName `
            -AllowedReleaseNames $allowedNames `
            -EvidenceJson $script:newEvidenceJson `
            -Encoding $utf8NoBom `
            -FailurePoint $FailurePoint | Out-Null
    } catch {
        $caught = $_
    }
    $actualMessage = if ($null -eq $caught) {
        "<no exception>"
    } else {
        $caught.Exception.Message
    }
    Assert-Condition ($null -ne $caught -and
        $actualMessage.Contains($expectedMessage)) `
        ("$FailurePoint must reach and raise its exact simulated failure; " +
            "actual: $actualMessage")
    Assert-Condition ((Get-TextHash (Join-Path $releaseRoot $evidenceName)) -eq
        $script:oldEvidenceHash) `
        "$FailurePoint must preserve the previous release evidence"
    Assert-Condition ((Get-TextHash (Join-Path $releaseRoot $checksumName)) -eq
        $script:oldChecksumHash) `
        "$FailurePoint must preserve the previous release checksum"
    $stagingBase = Join-Path $workRoot "release-evidence-staging"
    $stagingResidue = if (Test-Path -LiteralPath $stagingBase) {
        @(Get-ChildItem -LiteralPath $stagingBase -Force)
    } else {
        @()
    }
    Assert-Condition (@($stagingResidue).Count -eq 0) `
        "$FailurePoint must leave no evidence staging residue"
}

try {
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $archiveBase -Force | Out-Null
    Write-Text (Join-Path $releaseRoot "payload.bin") "payload"
    Write-Text (Join-Path $releaseRoot $evidenceName) `
        '{"schemaVersion":5,"releaseVersion":"0.3.2"}'
    Write-Text (Join-Path $releaseRoot $checksumName) "previous checksum"
    $oldEvidenceHash = Get-TextHash (Join-Path $releaseRoot $evidenceName)
    $oldChecksumHash = Get-TextHash (Join-Path $releaseRoot $checksumName)
    $newEvidenceJson =
        '{"schemaVersion":5,"releaseVersion":"0.3.3"}'

    Invoke-ControlFailure "AfterEvidenceStageWrite"
    Invoke-ControlFailure "AfterEvidenceCommit"
    Invoke-ControlFailure "AfterChecksumCommit"

    $rollbackCaught = $null
    try {
        Invoke-ReleaseControlAtomicCommit `
            -WorkRoot $workRoot `
            -ReleaseRoot $releaseRoot `
            -EvidenceName $evidenceName `
            -ChecksumName $checksumName `
            -AllowedReleaseNames $allowedNames `
            -EvidenceJson $newEvidenceJson `
            -Encoding $utf8NoBom `
            -FailurePoint "AfterEvidenceCommit" `
            -RollbackFailurePoint "EvidenceRestore" | Out-Null
    } catch {
        $rollbackCaught = $_
    }
    $preservedStages = @(Get-ChildItem -LiteralPath (Join-Path $workRoot `
        "release-evidence-staging") -Directory -Force)
    Assert-Condition ($null -ne $rollbackCaught -and
        $rollbackCaught.Exception.Message.Contains(
            "Simulated evidence restore failure.") -and
        $preservedStages.Count -eq 1 -and
        $rollbackCaught.Exception.Message.Contains(
            $preservedStages[0].FullName)) `
        "rollback failure must report the preserved staging path"
    $preservedEvidenceBackup = Join-Path $preservedStages[0].FullName `
        "previous-evidence.json"
    Assert-Condition ((Test-Path -LiteralPath $preservedEvidenceBackup `
            -PathType Leaf) -and
        (Get-TextHash $preservedEvidenceBackup) -eq $oldEvidenceHash) `
        "rollback failure must preserve the previous evidence backup"
    [IO.File]::Copy($preservedEvidenceBackup,
        (Join-Path $releaseRoot $evidenceName), $true)
    Remove-Item -LiteralPath $preservedStages[0].FullName -Recurse -Force
    Assert-Condition ((Get-TextHash (Join-Path $releaseRoot $evidenceName)) -eq
        $oldEvidenceHash) `
        "preserved previous evidence must support manual recovery"

    $committedEvidencePath = Invoke-ReleaseControlAtomicCommit `
        -WorkRoot $workRoot `
        -ReleaseRoot $releaseRoot `
        -EvidenceName $evidenceName `
        -ChecksumName $checksumName `
        -AllowedReleaseNames $allowedNames `
        -EvidenceJson $newEvidenceJson `
        -Encoding $utf8NoBom
    Assert-Condition ([string]::Equals($committedEvidencePath,
            (Join-Path $releaseRoot $evidenceName),
            [StringComparison]::OrdinalIgnoreCase)) `
        "successful control commit must return the evidence path"
    Assert-Condition ((Get-TextHash $committedEvidencePath) -ne
        $oldEvidenceHash) `
        "successful control commit must replace the previous evidence"
    $committedEvidence = Get-Content -LiteralPath $committedEvidencePath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Condition ([string]$committedEvidence.releaseVersion -eq "0.3.3") `
        "successful control commit must retain the staged evidence identity"
    Assert-ChecksumExactFiles (Join-Path $releaseRoot $checksumName) `
        $releaseRoot @($evidenceName, "payload.bin") `
        "successful replacement checksum"
    $stagingBase = Join-Path $workRoot "release-evidence-staging"
    Assert-Condition (-not (Test-Path -LiteralPath $stagingBase) -or
        @(Get-ChildItem -LiteralPath $stagingBase -Force).Count -eq 0) `
        "successful control commit must leave no staging residue"

    $firstMoveRoot = Join-Path $fixtureRoot "first-move-release"
    New-Item -ItemType Directory -Path $firstMoveRoot -Force | Out-Null
    Write-Text (Join-Path $firstMoveRoot "payload.bin") "first payload"
    foreach ($failurePoint in @("AfterEvidenceCommit", "AfterChecksumCommit")) {
        $firstMoveCaught = $null
        try {
            Invoke-ReleaseControlAtomicCommit `
                -WorkRoot $workRoot `
                -ReleaseRoot $firstMoveRoot `
                -EvidenceName $evidenceName `
                -ChecksumName $checksumName `
                -AllowedReleaseNames $allowedNames `
                -EvidenceJson $newEvidenceJson `
                -Encoding $utf8NoBom `
                -FailurePoint $failurePoint | Out-Null
        } catch {
            $firstMoveCaught = $_
        }
        Assert-Condition ($null -ne $firstMoveCaught -and
            -not (Test-Path -LiteralPath (Join-Path $firstMoveRoot `
                    $evidenceName)) -and
            -not (Test-Path -LiteralPath (Join-Path $firstMoveRoot `
                    $checksumName))) `
            "$failurePoint must delete newly moved control files on rollback"
    }
    $firstEvidencePath = Invoke-ReleaseControlAtomicCommit `
        -WorkRoot $workRoot `
        -ReleaseRoot $firstMoveRoot `
        -EvidenceName $evidenceName `
        -ChecksumName $checksumName `
        -AllowedReleaseNames $allowedNames `
        -EvidenceJson $newEvidenceJson `
        -Encoding $utf8NoBom
    Assert-Condition (Test-Path -LiteralPath $firstEvidencePath -PathType Leaf) `
        "first commit must move staged evidence into a new release"
    Assert-ChecksumExactFiles (Join-Path $firstMoveRoot $checksumName) `
        $firstMoveRoot @($evidenceName, "payload.bin") `
        "first move checksum"

    $stageRoot = Join-Path $archiveBase `
        (".staging-v9.8.7-{0}" -f [guid]::NewGuid().ToString("N"))
    $archiveRoot = Join-Path $archiveBase "v9.8.7"
    Write-Text (Join-Path $stageRoot "archive-manifest.json") `
        '{"archivedVersion":"9.8.7"}'
    $verifyCount = 0
    $verifier = {
        param([string]$CandidateArchive)
        $script:verifyCount++
        if ($script:verifyCount -eq 2) {
            throw "Simulated post-move verifier failure."
        }
        if (-not (Test-Path -LiteralPath (Join-Path $CandidateArchive `
                    "archive-manifest.json") -PathType Leaf)) {
            throw "Archive fixture control is missing."
        }
    }
    $caught = $null
    try {
        Invoke-ReleaseArchiveAtomicCommit `
            -StageRoot $stageRoot `
            -ArchiveRoot $archiveRoot `
            -ArchiveBase $archiveBase `
            -Verifier $verifier
    } catch {
        $caught = $_
    }
    Assert-Condition ($null -ne $caught -and
        $caught.Exception.Message.Contains("post-move verifier failure")) `
        "post-move archive verifier failure must propagate"
    Assert-Condition ($verifyCount -eq 2) `
        "archive verifier must run before and after the move"
    Assert-Condition (-not (Test-Path -LiteralPath $archiveRoot)) `
        "failed post-move verification must remove the destination"
    Assert-Condition (-not (Test-Path -LiteralPath $stageRoot)) `
        "failed post-move verification must leave no staging residue"
} finally {
    if (Test-Path -LiteralPath $fixtureRoot -PathType Container) {
        $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
        $safePrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot `
            "work\release-traceability-atomicity-"))
        if ($resolvedFixture.StartsWith($safePrefix,
                [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
        }
    }
}

Write-Host "PASS $passed release traceability atomicity tests"
