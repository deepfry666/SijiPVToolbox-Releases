Set-StrictMode -Version 2.0

function Get-PayloadSetSha256([object[]]$Payloads,
    [string]$NameProperty, [string]$HashProperty) {
    $items = @($Payloads | ForEach-Object {
        $name = [string]$_.$NameProperty
        $hash = ([string]$_.$HashProperty).ToUpperInvariant()
        "$name|$hash"
    } | Sort-Object)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes($items -join "`n"))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}

function Get-StructuralSourceFingerprint([string]$ProjectRoot) {
    $projectRootPath = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd("\")
    $mainSourceRoot = Join-Path $projectRootPath "src\StructuralToolbox"
    if (-not (Test-Path -LiteralPath $mainSourceRoot -PathType Container)) {
        throw "Structural source root is missing: $mainSourceRoot"
    }
    $sourceRoots = @($mainSourceRoot)
    foreach ($relativeRoot in @(
        "src\StructuralToolbox.Contracts",
        "src\StructuralToolbox.Rail",
        "src\StructuralToolbox.Ballast",
        "src\StructuralToolbox.ArrayRecognition",
        "src\StructuralToolbox.Update",
        "src\StructuralToolbox.DrawingTranslation")) {
        $moduleSourceRoot = Join-Path $projectRootPath $relativeRoot
        if (Test-Path -LiteralPath $moduleSourceRoot -PathType Container) {
            $sourceRoots += $moduleSourceRoot
        }
    }
    $sourceFiles = @($sourceRoots | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -File
    })
    $items = @($sourceFiles |
        Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($projectRootPath.Length).
                TrimStart("\").Replace("\", "/")
            $hash = (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash
            "$relative|$hash"
        })
    if ($items.Count -eq 0) {
        throw "Structural source root contains no files."
    }
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes($items -join "`n"))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}

function Get-RegressionSuiteFingerprint([string]$ProjectRoot) {
    $projectRootPath = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd("\")
    $projectPrefix = $projectRootPath + "\"
    $items = @(
        foreach ($relativeRoot in @("scripts", "tests")) {
            $root = Join-Path $projectRootPath $relativeRoot
            if (-not (Test-Path -LiteralPath $root -PathType Container)) {
                throw "Regression-suite root is missing: $root"
            }
            Get-ChildItem -LiteralPath $root -Recurse -File -Force |
                Where-Object {
                    $_.FullName -notmatch "[\\/](bin|obj)[\\/]"
                } | ForEach-Object {
                    $relative = $_.FullName.Substring($projectPrefix.Length).
                        Replace("\", "/")
                    $hash = (Get-FileHash -LiteralPath $_.FullName `
                        -Algorithm SHA256).Hash
                    "$relative|$hash"
                }
        }
    )
    $items = @($items | Sort-Object)
    if ($items.Count -eq 0) {
        throw "Regression suite contains no files."
    }
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes($items -join "`n"))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}

function Assert-AtomicChildPath(
    [string]$Path,
    [string]$Prefix,
    [string]$Description) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($Prefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description escaped its protected root: $fullPath"
    }
    return $fullPath
}

function Assert-AtomicExactNameSet(
    [string[]]$Expected,
    [string[]]$Actual,
    [string]$Description) {
    $expectedItems = @($Expected | ForEach-Object { [string]$_ })
    $actualItems = @($Actual | ForEach-Object { [string]$_ })
    if (@($expectedItems | Where-Object {
            [string]::IsNullOrWhiteSpace($_)
        }).Count -ne 0 -or
        @($actualItems | Where-Object {
            [string]::IsNullOrWhiteSpace($_)
        }).Count -ne 0 -or
        @($expectedItems | Sort-Object -Unique).Count -ne
            $expectedItems.Count -or
        @($actualItems | Sort-Object -Unique).Count -ne $actualItems.Count -or
        $expectedItems.Count -ne $actualItems.Count -or
        @(Compare-Object @($expectedItems | Sort-Object) `
            @($actualItems | Sort-Object)).Count -ne 0) {
        throw "$Description does not match the exact expected file set."
    }
}

function Remove-AtomicStagingDirectory(
    [string]$Path,
    [string]$AllowedPrefix) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return
    }
    $resolvedPath = Assert-AtomicChildPath $Path $AllowedPrefix `
        "Atomic staging directory"
    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Invoke-ReleaseControlAtomicCommit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkRoot,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseRoot,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceName,

        [Parameter(Mandatory = $true)]
        [string]$ChecksumName,

        [Parameter(Mandatory = $true)]
        [string[]]$AllowedReleaseNames,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceJson,

        [Parameter(Mandatory = $true)]
        [Text.Encoding]$Encoding,

        [ValidateSet("None", "AfterEvidenceStageWrite",
            "AfterEvidenceCommit", "AfterChecksumCommit")]
        [string]$FailurePoint = "None",

        [ValidateSet("None", "EvidenceRestore", "ChecksumRestore",
            "EvidenceDelete", "ChecksumDelete")]
        [string]$RollbackFailurePoint = "None"
    )

    $workRootPath = [IO.Path]::GetFullPath($WorkRoot).TrimEnd("\")
    $releaseRootPath = [IO.Path]::GetFullPath($ReleaseRoot).TrimEnd("\")
    $releasePrefix = $releaseRootPath + "\"
    $evidencePath = Assert-AtomicChildPath `
        (Join-Path $releaseRootPath $EvidenceName) $releasePrefix `
        "Release evidence"
    $checksumPath = Assert-AtomicChildPath `
        (Join-Path $releaseRootPath $ChecksumName) $releasePrefix `
        "Release checksum"

    $nonControlFiles = @(Get-ChildItem -LiteralPath $releaseRootPath `
        -File -Force | Where-Object {
            $_.Name -notin @($EvidenceName, $ChecksumName)
        })
    $prospectiveNames = @($nonControlFiles | ForEach-Object { $_.Name })
    $prospectiveNames += @($EvidenceName, $ChecksumName)
    Assert-AtomicExactNameSet $AllowedReleaseNames $prospectiveNames `
        "Prospective release file allowlist"

    $stageBase = [IO.Path]::GetFullPath((Join-Path $workRootPath `
        "release-evidence-staging")).TrimEnd("\")
    $stagePrefix = $stageBase + "\v"
    $stageRoot = [IO.Path]::GetFullPath((Join-Path $stageBase `
        ("v-{0}" -f [guid]::NewGuid().ToString("N"))))
    $null = Assert-AtomicChildPath $stageRoot $stagePrefix `
        "Release evidence staging directory"
    $stagedEvidencePath = Join-Path $stageRoot $EvidenceName
    $stagedChecksumPath = Join-Path $stageRoot $ChecksumName
    $evidenceBackupPath = Join-Path $stageRoot "previous-evidence.json"
    $checksumBackupPath = Join-Path $stageRoot "previous-SHA256SUMS.txt"
    $evidenceDiscardPath = Join-Path $stageRoot "discarded-evidence.json"
    $checksumDiscardPath = Join-Path $stageRoot "discarded-SHA256SUMS.txt"
    $evidenceExisted = Test-Path -LiteralPath $evidencePath -PathType Leaf
    $checksumExisted = Test-Path -LiteralPath $checksumPath -PathType Leaf
    $evidenceCommitted = $false
    $checksumCommitted = $false
    $commitSucceeded = $false
    $preserveStaging = $false

    New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
    try {
        [IO.File]::WriteAllText($stagedEvidencePath, $EvidenceJson, $Encoding)
        if ($FailurePoint -eq "AfterEvidenceStageWrite") {
            throw "Simulated failure after staged evidence write."
        }
        $stagedEvidence = Get-Content -LiteralPath $stagedEvidencePath `
            -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([int]$stagedEvidence.schemaVersion -ne 5 -or
            [string]::IsNullOrWhiteSpace(
                [string]$stagedEvidence.releaseVersion)) {
            throw "Staged release evidence identity is invalid."
        }

        $checksumRecords = @($nonControlFiles | ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Hash = (Get-FileHash -LiteralPath $_.FullName `
                    -Algorithm SHA256).Hash
            }
        })
        $checksumRecords += [pscustomobject]@{
            Name = $EvidenceName
            Hash = (Get-FileHash -LiteralPath $stagedEvidencePath `
                -Algorithm SHA256).Hash
        }
        $checksumLines = @($checksumRecords | Sort-Object Name |
            ForEach-Object { "{0}  {1}" -f $_.Hash, $_.Name })
        [IO.File]::WriteAllLines($stagedChecksumPath,
            $checksumLines, $Encoding)
        if ($checksumLines.Count -ne ($AllowedReleaseNames.Count - 1) -or
            @($checksumLines | Where-Object {
                $_ -notmatch '^[0-9A-F]{64}  .+$'
            }).Count -ne 0) {
            throw "Staged release checksum failed self-validation."
        }

        if ($evidenceExisted) {
            [IO.File]::Replace($stagedEvidencePath, $evidencePath,
                $evidenceBackupPath, $true)
        } else {
            [IO.File]::Move($stagedEvidencePath, $evidencePath)
        }
        $evidenceCommitted = $true
        if ($FailurePoint -eq "AfterEvidenceCommit") {
            throw "Simulated failure after release evidence commit."
        }

        if ($checksumExisted) {
            [IO.File]::Replace($stagedChecksumPath, $checksumPath,
                $checksumBackupPath, $true)
        } else {
            [IO.File]::Move($stagedChecksumPath, $checksumPath)
        }
        $checksumCommitted = $true
        if ($FailurePoint -eq "AfterChecksumCommit") {
            throw "Simulated failure after release checksum commit."
        }
        $commitSucceeded = $true
    } catch {
        $commitError = $_
        $rollbackErrors = New-Object Collections.Generic.List[string]
        if ($checksumCommitted) {
            try {
                if ($checksumExisted -and
                    (Test-Path -LiteralPath $checksumBackupPath -PathType Leaf)) {
                    if ($RollbackFailurePoint -eq "ChecksumRestore") {
                        throw "Simulated checksum restore failure."
                    }
                    [IO.File]::Replace($checksumBackupPath, $checksumPath,
                        $checksumDiscardPath, $true)
                } elseif (-not $checksumExisted) {
                    if ($RollbackFailurePoint -eq "ChecksumDelete") {
                        throw "Simulated new checksum delete failure."
                    }
                    [IO.File]::Delete($checksumPath)
                } else {
                    throw "Previous checksum backup is missing."
                }
            } catch {
                $rollbackErrors.Add("SHA256SUMS.txt: $($_.Exception.Message)")
            }
        }
        if ($evidenceCommitted) {
            try {
                if ($evidenceExisted -and
                    (Test-Path -LiteralPath $evidenceBackupPath -PathType Leaf)) {
                    if ($RollbackFailurePoint -eq "EvidenceRestore") {
                        throw "Simulated evidence restore failure."
                    }
                    [IO.File]::Replace($evidenceBackupPath, $evidencePath,
                        $evidenceDiscardPath, $true)
                } elseif (-not $evidenceExisted) {
                    if ($RollbackFailurePoint -eq "EvidenceDelete") {
                        throw "Simulated new evidence delete failure."
                    }
                    [IO.File]::Delete($evidencePath)
                } else {
                    throw "Previous release evidence backup is missing."
                }
            } catch {
                $rollbackErrors.Add("release evidence: $($_.Exception.Message)")
            }
        }
        if ($rollbackErrors.Count -gt 0) {
            $preserveStaging = $true
            throw (("Release control commit failed: {0}. " +
                "Rollback also failed for {1}. Recovery artifacts preserved " +
                "at: {2}") -f $commitError.Exception.Message,
                ($rollbackErrors -join "; "), $stageRoot)
        }
        throw $commitError
    } finally {
        if ($preserveStaging) {
            Write-Warning "Recovery artifacts preserved at: $stageRoot"
        } else {
            try {
                Remove-AtomicStagingDirectory $stageRoot $stagePrefix
            } catch {
                if ($commitSucceeded) {
                    Write-Warning ("Release controls committed, but staging " +
                        "cleanup failed: $($_.Exception.Message)")
                } else {
                    Write-Warning ("Release control rollback completed, but " +
                        "staging cleanup failed: $($_.Exception.Message)")
                }
            }
        }
    }

    return $evidencePath
}

function Invoke-ReleaseArchiveAtomicCommit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$StageRoot,

        [Parameter(Mandatory = $true)]
        [string]$ArchiveRoot,

        [Parameter(Mandatory = $true)]
        [string]$ArchiveBase,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Verifier
    )

    $archiveBasePath = [IO.Path]::GetFullPath($ArchiveBase).TrimEnd("\")
    $stagePrefix = $archiveBasePath + "\.staging-v"
    $archivePrefix = $archiveBasePath + "\v"
    $stagePath = Assert-AtomicChildPath $StageRoot $stagePrefix `
        "Release archive staging directory"
    $archivePath = Assert-AtomicChildPath $ArchiveRoot $archivePrefix `
        "Release archive destination"
    if (Test-Path -LiteralPath $archivePath) {
        throw "Release archive already exists and will not be overwritten: $archivePath"
    }

    $archiveCommitted = $false
    try {
        & $Verifier $stagePath
        Move-Item -LiteralPath $stagePath -Destination $archivePath
        $archiveCommitted = $true
        & $Verifier $archivePath
    } catch {
        $archiveError = $_
        $rollbackErrors = New-Object Collections.Generic.List[string]
        if ($archiveCommitted -and
            (Test-Path -LiteralPath $archivePath -PathType Container)) {
            try {
                if (Test-Path -LiteralPath $stagePath) {
                    throw "Archive staging path unexpectedly exists before rollback."
                }
                Move-Item -LiteralPath $archivePath -Destination $stagePath
                $archiveCommitted = $false
            } catch {
                $rollbackErrors.Add("archive directory: $($_.Exception.Message)")
            }
        }
        try {
            Remove-AtomicStagingDirectory $stagePath $stagePrefix
        } catch {
            $rollbackErrors.Add("archive staging cleanup: $($_.Exception.Message)")
        }
        if ($rollbackErrors.Count -gt 0) {
            throw ("Archive creation failed: {0}. Rollback also failed for {1}" -f
                $archiveError.Exception.Message, ($rollbackErrors -join "; "))
        }
        throw $archiveError
    }
}
