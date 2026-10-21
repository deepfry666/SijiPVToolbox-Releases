[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ReleaseDirectory,

    [string]$BuildResult,

    [string]$EvidenceBaseDirectory,

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
    [string]$PublishReceipt,

    [string]$AutoCADExecutable = "D:\cad\AutoCAD 2020\acad.exe"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0
. (Join-Path $PSScriptRoot "ReleaseTraceabilityAtomic.ps1")

$projectRoot = [IO.Path]::GetFullPath(
    (Split-Path -Parent $PSScriptRoot)).TrimEnd("\")
$projectPrefix = $projectRoot + "\"
$outputsRoot = [IO.Path]::GetFullPath(
    (Join-Path $projectRoot "outputs")).TrimEnd("\")
$outputsPrefix = $outputsRoot + "\"
$workRoot = [IO.Path]::GetFullPath(
    (Join-Path $projectRoot "work")).TrimEnd("\")
$workPrefix = $workRoot + "\"
$utf8NoBom = New-Object Text.UTF8Encoding($false)

function Resolve-ProjectPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path $projectRoot $Path))
}

function Assert-UnderRoot([string]$Path, [string]$Prefix, [string]$Description) {
    if (-not $Path.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay under $($Prefix.TrimEnd('\')): $Path"
    }
}

function Get-Version3([version]$Value) {
    return "{0}.{1}.{2}" -f $Value.Major, $Value.Minor,
        [Math]::Max(0, $Value.Build)
}

function Get-RelativeProjectPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($projectPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($projectPrefix.Length).Replace("\", "/")
    }
    return $fullPath
}

function Get-FileEvidence([IO.FileInfo]$File) {
    return [ordered]@{
        path = Get-RelativeProjectPath $File.FullName
        bytes = $File.Length
        sha256 = (Get-FileHash -LiteralPath $File.FullName `
            -Algorithm SHA256).Hash
    }
}

function Get-TextSha256([string]$Value) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes($Value))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}

function Get-PublicCertificateMetadata([object]$Certificate) {
    if ($null -eq $Certificate) {
        return $null
    }
    return [ordered]@{
        subject = [string]$Certificate.subject
        issuer = [string]$Certificate.issuer
        thumbprint = [string]$Certificate.thumbprint
        serialNumber = [string]$Certificate.serialNumber
        notBefore = [string]$Certificate.notBefore
        notAfter = [string]$Certificate.notAfter
    }
}

function Get-PublicSignatureMetadata([object]$Signature) {
    if ($null -eq $Signature) {
        return $null
    }
    return [ordered]@{
        classification = [string]$Signature.classification
        status = [string]$Signature.status
        signer = Get-PublicCertificateMetadata $Signature.signer
        timestampSigner =
            Get-PublicCertificateMetadata $Signature.timestampSigner
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

function Assert-UniqueTextValues([object[]]$Values, [string]$Description) {
    $series = @($Values | ForEach-Object { [string]$_ })
    $unique = @($series | Sort-Object -Unique)
    if ($series.Count -ne $unique.Count -or
        @($series | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        throw "$Description contains a blank or duplicate value."
    }
}

function Assert-ExactTextSet(
    [object[]]$Expected,
    [object[]]$Actual,
    [string]$Description) {
    Assert-UniqueTextValues $Expected "$Description expected set"
    Assert-UniqueTextValues $Actual "$Description actual set"
    if ($Expected.Count -eq 0 -and $Actual.Count -eq 0) {
        return
    }
    if ($Expected.Count -eq 0 -or $Actual.Count -eq 0) {
        throw "$Description differs: one set is empty."
    }
    $differences = @(Compare-Object -ReferenceObject @($Expected | Sort-Object) `
        -DifferenceObject @($Actual | Sort-Object))
    if ($differences.Count -gt 0) {
        $details = @($differences | ForEach-Object {
            "$($_.SideIndicator)$($_.InputObject)"
        }) -join ", "
        throw "$Description differs: $details"
    }
}

function Assert-SamePath(
    [string]$Actual,
    [string]$Expected,
    [string]$Description) {
    $actualFullPath = [IO.Path]::GetFullPath($Actual)
    $expectedFullPath = [IO.Path]::GetFullPath($Expected)
    if (-not [string]::Equals($actualFullPath, $expectedFullPath,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description path differs. Expected '$expectedFullPath', " +
            "found '$actualFullPath'."
    }
}

function Get-AssemblyMvid([string]$Path) {
    $assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($Path))
    return $assembly.ManifestModule.ModuleVersionId.ToString("D")
}

function Assert-AllNumbersNear(
    [object[]]$Values,
    [double]$Expected,
    [double]$Tolerance,
    [string]$Description) {
    $items = @($Values)
    if ($items.Count -eq 0) {
        throw "$Description contains no values."
    }
    foreach ($value in $items) {
        if ([Math]::Abs([double]$value - $Expected) -gt $Tolerance) {
            throw "$Description contains '$value'; expected $Expected."
        }
    }
}

function Assert-AllTextValues(
    [object[]]$Values,
    [string]$Expected,
    [string]$Description) {
    $items = @($Values)
    if ($items.Count -eq 0) {
        throw "$Description contains no values."
    }
    foreach ($value in $items) {
        if (-not [string]::Equals([string]$value, $Expected,
                [StringComparison]::Ordinal)) {
            throw "$Description contains '$value'; expected '$Expected'."
        }
    }
}

function Read-VerifiedHashManifest(
    [string]$ManifestPath,
    [string]$AllowedPrefix,
    [string]$Description) {
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "$Description is missing: $ManifestPath"
    }
    $manifestDirectory = Split-Path -Parent $ManifestPath
    $entries = [Collections.Generic.Dictionary[string,string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($line in @(Get-Content -LiteralPath $ManifestPath -Encoding UTF8)) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        $match = [regex]::Match($line,
            '^([0-9A-Fa-f]{64})\s+\*?(.+)$')
        if (-not $match.Success) {
            throw "$Description contains an invalid SHA256SUMS line."
        }
        $relativePath = $match.Groups[2].Value.Trim()
        if ([string]::IsNullOrWhiteSpace($relativePath) -or
            [IO.Path]::IsPathRooted($relativePath)) {
            throw "$Description contains a non-relative artifact path."
        }
        $artifactPath = [IO.Path]::GetFullPath((Join-Path $manifestDirectory `
            ($relativePath.Replace("/", "\"))))
        Assert-UnderRoot $artifactPath $AllowedPrefix "$Description artifact"
        if ($entries.ContainsKey($artifactPath)) {
            throw "$Description contains a duplicate artifact path: $relativePath"
        }
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "$Description artifact is missing: $relativePath"
        }
        $recordedHash = $match.Groups[1].Value.ToUpperInvariant()
        $actualHash = (Get-FileHash -LiteralPath $artifactPath `
            -Algorithm SHA256).Hash
        if ($recordedHash -ne $actualHash) {
            throw "$Description hash differs for: $relativePath"
        }
        $entries.Add($artifactPath, $recordedHash)
    }
    if ($entries.Count -eq 0) {
        throw "$Description contains no file hashes."
    }
    return $entries
}

function Assert-HashManifestContains(
    [Collections.Generic.Dictionary[string,string]]$Entries,
    [string]$Path,
    [string]$Description) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $Entries.ContainsKey($fullPath)) {
        throw "$Description does not contain: $fullPath"
    }
}

try {
    $requestedVersion = [version]$Version
} catch {
    throw "Version is invalid: $Version"
}
$version3 = Get-Version3 $requestedVersion
if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $ReleaseDirectory = "outputs\SijiPVToolbox_v${version3}_AutoCAD2020"
}
if ([string]::IsNullOrWhiteSpace($BuildResult)) {
    $BuildResult =
        "work\installer\release-candidate-v${version3}\build-result.json"
}
if ([string]::IsNullOrWhiteSpace($EvidenceBaseDirectory)) {
    $EvidenceBaseDirectory = "work\release-evidence-v${version3}"
}
$evidenceName = "四季光伏工具箱_v$version3`_发布证据.json"
$checksumName = "SHA256SUMS.txt"
$evidenceBaseRoot = Resolve-ProjectPath $EvidenceBaseDirectory
Assert-UnderRoot $evidenceBaseRoot $workPrefix "EvidenceBaseDirectory"

$releaseRoot = Resolve-ProjectPath $ReleaseDirectory
Assert-UnderRoot $releaseRoot $outputsPrefix "ReleaseDirectory"
if (-not (Test-Path -LiteralPath $releaseRoot -PathType Container)) {
    throw "Release directory was not found: $releaseRoot"
}

$buildResultPath = Resolve-ProjectPath $BuildResult
Assert-UnderRoot $buildResultPath $workPrefix "BuildResult"
if (-not (Test-Path -LiteralPath $buildResultPath -PathType Leaf)) {
    throw "Build result was not found: $buildResultPath"
}
$build = Get-Content -LiteralPath $buildResultPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$buildResultHash = (Get-FileHash -LiteralPath $buildResultPath `
    -Algorithm SHA256).Hash
if ([int]$build.schemaVersion -lt 6) {
    throw "Build result does not contain the schema-6 release provenance chain: $buildResultPath"
}
if ([bool]$build.testBuild) {
    throw "An isolated installer-test build cannot be published under outputs/."
}
$buildVersion = [version]([string]$build.appVersion)
if ((Get-Version3 $buildVersion) -ne $version3) {
    throw "Requested release v$version3 does not match build $buildVersion."
}
if ((Get-SourceCodeFingerprint) -ne [string]$build.sourceCodeSha256) {
    throw "Source code changed after the installer was built. Rebuild before evidence generation."
}

$releaseDefinitionPath = Join-Path $projectRoot `
    "scripts\installer\release-v$version3.json"
$releaseDefinition = Get-Content -LiteralPath $releaseDefinitionPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$definitionSignerThumbprints = @($releaseDefinition.allowedSignerThumbprints |
    ForEach-Object { ([string]$_ -replace "\s", "").ToUpperInvariant() })
$buildSignerThumbprints = @($build.allowedSignerThumbprints |
    ForEach-Object { ([string]$_ -replace "\s", "").ToUpperInvariant() })
Assert-ExactTextSet $definitionSignerThumbprints $buildSignerThumbprints `
    "Release definition and build signer allowlist"

$releaseKind = [string]$build.releaseKind
if ($releaseKind -eq "unsigned-distribution") {
    if (-not [bool]$build.unsignedDistributionBuild -or
        [bool]$build.testBuild -or [bool]$build.signed) {
        throw "UNSIGNED distribution kind does not match the build flags."
    }
    if ([string]$releaseDefinition.currentReleaseClassification -ne
            "UNSIGNED" -or
        [string]$releaseDefinition.authenticodeExpectation -ne "NotSigned" -or
        -not [bool]$releaseDefinition.publicDistributionAllowed) {
        throw "Unsigned output requires UNSIGNED, NotSigned, and publicDistributionAllowed=true."
    }
    $expectedReleaseName = "SijiPVToolbox_v${version3}_AutoCAD2020"
} elseif ($releaseKind -eq "signed-release") {
    if ([bool]$build.unsignedDistributionBuild -or -not [bool]$build.signed) {
        throw "Signed release kind does not match the build signature flags."
    }
    if ([string]$releaseDefinition.currentReleaseClassification -ne
            "SIGNED_RELEASE" -or
        -not [bool]$releaseDefinition.publicDistributionAllowed) {
        throw "Signed output requires SIGNED_RELEASE/publicDistributionAllowed=true."
    }
    $expectedReleaseName = "SijiPVToolbox_v${version3}_AutoCAD2020"
} else {
    throw "Unsupported release kind for outputs/: $releaseKind"
}
if ((Split-Path -Leaf $releaseRoot) -ne $expectedReleaseName) {
    throw "Release directory name must be '$expectedReleaseName'."
}

$outputsRoot = Join-Path $projectRoot "outputs"
$releaseRootNormalized = $releaseRoot.TrimEnd("\")
$outputArchivePath = Join-Path $outputsRoot "archive"
$outputArchiveEntries = @(Get-ChildItem -LiteralPath $outputsRoot -Force |
    Where-Object { $_.Name -ieq "archive" })
if ($outputArchiveEntries.Count -gt 1 -or
    ($outputArchiveEntries.Count -eq 1 -and
        -not $outputArchiveEntries[0].PSIsContainer)) {
    throw "outputs/archive must be a directory when present."
}
$unexpectedOutputEntries = @(Get-ChildItem -LiteralPath $outputsRoot -Force |
    Where-Object {
        -not ($_.PSIsContainer -and
            [string]::Equals($_.FullName.TrimEnd("\"),
                $releaseRootNormalized,
                [StringComparison]::OrdinalIgnoreCase)) -and
        -not ($_.PSIsContainer -and
            [string]::Equals($_.FullName.TrimEnd("\"),
                [IO.Path]::GetFullPath($outputArchivePath).TrimEnd("\"),
                [StringComparison]::OrdinalIgnoreCase))
    })
if ($unexpectedOutputEntries.Count -ne 0) {
    $unexpectedNames = @($unexpectedOutputEntries |
        ForEach-Object { $_.Name }) -join ", "
    throw "outputs must contain only the current release directory " +
        "'$expectedReleaseName' and outputs/archive. Archive historical " +
        "releases first: $unexpectedNames"
}
if (Test-Path -LiteralPath $outputArchivePath -PathType Container) {
    $archivedOutputFiles = @(Get-ChildItem -LiteralPath $outputArchivePath `
        -File -Force)
    $archivedOutputDirectories = @(Get-ChildItem -LiteralPath `
        $outputArchivePath -Directory -Force)
    if ($archivedOutputFiles.Count -ne 0) {
        throw "outputs/archive must contain release directories only."
    }
    foreach ($archivedOutputDirectory in $archivedOutputDirectories) {
        $archiveMatch = [regex]::Match($archivedOutputDirectory.Name,
            '^SijiPVToolbox_v(?<version>\d+\.\d+\.\d+)_AutoCAD2020$')
        if (-not $archiveMatch.Success -or
            [string]::Equals($archivedOutputDirectory.Name,
                $expectedReleaseName,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "outputs/archive contains an invalid release directory: " +
                $archivedOutputDirectory.Name
        }
        $archivedOutputSubdirectories = @(Get-ChildItem -LiteralPath `
            $archivedOutputDirectory.FullName -Directory -Force)
        $archivedOutputInstallers = @(Get-ChildItem -LiteralPath `
            $archivedOutputDirectory.FullName -File -Filter "*.exe")
        $expectedArchivedInstaller =
            "四季光伏工具箱_Setup_v$($archiveMatch.Groups['version'].Value).exe"
        if ($archivedOutputSubdirectories.Count -ne 0 -or
            $archivedOutputInstallers.Count -ne 1 -or
            $archivedOutputInstallers[0].Name -cne
                $expectedArchivedInstaller) {
            throw "outputs/archive release $($archivedOutputDirectory.Name) " +
                "must retain exactly its versioned installer EXE."
        }
    }
}

$releaseDirectories = @(Get-ChildItem -LiteralPath $releaseRoot -Recurse `
    -Directory -Force)
if ($releaseDirectories.Count -ne 0) {
    throw "Release directory must contain top-level files only; found " +
        "$($releaseDirectories.Count) subdirectories."
}
$allowedReleaseNames = @(
    "四季光伏工具箱_Setup_v$version3.exe"
)
$releaseFilesBeforeEvidence = @(Get-ChildItem -LiteralPath $releaseRoot -File -Force)
Assert-AtomicExactNameSet $allowedReleaseNames @($releaseFilesBeforeEvidence.Name) `
    "Release one-EXE allowlist"

$sourceInstaller = Resolve-ProjectPath ([string]$build.installer)
Assert-UnderRoot $sourceInstaller $workPrefix "Built installer"
if (-not (Test-Path -LiteralPath $sourceInstaller -PathType Leaf)) {
    throw "Built installer was not found: $sourceInstaller"
}
$sourceInstallerHash = (Get-FileHash -LiteralPath $sourceInstaller `
    -Algorithm SHA256).Hash
if ($sourceInstallerHash -ne [string]$build.installerSha256) {
    throw "Built installer hash no longer matches build-result.json."
}
$releaseInstallers = @(Get-ChildItem -LiteralPath $releaseRoot -File `
    -Filter "*.exe")
if ($releaseInstallers.Count -ne 1) {
    throw "Release directory must contain exactly one EXE; found $($releaseInstallers.Count)."
}
$releaseInstaller = $releaseInstallers[0]
$expectedInstallerName = "四季光伏工具箱_Setup_v$version3.exe"
if ($releaseInstaller.Name -ne $expectedInstallerName) {
    throw "Release installer must be named '$expectedInstallerName'."
}
$releaseInstallerHash = (Get-FileHash -LiteralPath $releaseInstaller.FullName `
    -Algorithm SHA256).Hash
if ($releaseInstallerHash -ne $sourceInstallerHash) {
    throw "Release EXE is not the installer recorded by build-result.json."
}
$releaseSignature = Get-AuthenticodeSignature -LiteralPath $releaseInstaller.FullName
$releaseIsSigned = $releaseSignature.Status -eq
    [Management.Automation.SignatureStatus]::Valid
$installerSignerThumbprint = $null
if ($releaseKind -eq "signed-release" -and -not $releaseIsSigned) {
    throw "Signed release EXE does not have a valid Authenticode signature."
}
if ($releaseKind -eq "signed-release") {
    if ($definitionSignerThumbprints.Count -eq 0 -or
        $null -eq $releaseSignature.SignerCertificate -or
        $null -eq $releaseSignature.TimeStamperCertificate) {
        throw "Signed release EXE lacks an approved signer or trusted timestamp."
    }
    $installerSignerThumbprint =
        $releaseSignature.SignerCertificate.Thumbprint.ToUpperInvariant()
    if ($definitionSignerThumbprints -notcontains $installerSignerThumbprint -or
        [string]$build.signatureClassification -ne "SignedAllowed" -or
        [string]$build.installerSignature.classification -ne "SignedAllowed" -or
        [string]$build.installerSignature.status -ne "Valid" -or
        [string]$build.installerSignature.signer.thumbprint -ne
            $installerSignerThumbprint -or
        $null -eq $build.installerSignature.timestampSigner) {
        throw "Signed release EXE metadata does not match the approved signer."
    }
}
if ($releaseKind -eq "unsigned-distribution" -and
    $releaseSignature.Status -ne
        [Management.Automation.SignatureStatus]::NotSigned) {
    throw "UNSIGNED EXE must have Authenticode status NotSigned; found " +
        "$($releaseSignature.Status)."
}
if ($releaseKind -eq "unsigned-distribution" -and
    ([string]$build.signatureClassification -ne "NotSigned" -or
    [string]$build.installerSignature.classification -ne "NotSigned" -or
    [string]$build.installerSignature.status -ne "NotSigned" -or
    $null -ne $build.installerSignature.signer -or
    $null -ne $build.installerSignature.timestampSigner)) {
    throw "UNSIGNED installer metadata is not explicitly NotSigned."
}
if ($releaseKind -eq "unsigned-distribution") {
    $releaseVersionInfo = $releaseInstaller.VersionInfo
    if ([string]$releaseVersionInfo.ProductName -ne "四季光伏工具箱" -or
        ([string]$releaseVersionInfo.ProductName).Contains("TEST_UNSIGNED") -or
        ([string]$releaseVersionInfo.FileDescription).Contains("TEST_UNSIGNED") -or
        ([string]$releaseVersionInfo.ProductName).Contains("未签名测试版") -or
        ([string]$releaseVersionInfo.FileDescription).Contains("未签名测试版")) {
        throw "UNSIGNED EXE metadata must use the normal product identity."
    }
} elseif (([string]$releaseInstaller.VersionInfo.ProductName).Contains(
        "TEST_UNSIGNED") -or
    ([string]$releaseInstaller.VersionInfo.FileDescription).Contains(
        "TEST_UNSIGNED")) {
    throw "Signed release EXE metadata contains TEST_UNSIGNED wording."
}

$payloadSignScriptEvidence = $null
if ($releaseKind -eq "signed-release") {
    $payloadSignScriptPath = [IO.Path]::GetFullPath(
        [string]$build.payloadSignScriptPath)
    if (-not (Test-Path -LiteralPath $payloadSignScriptPath -PathType Leaf) -or
        (Get-FileHash -LiteralPath $payloadSignScriptPath -Algorithm SHA256).Hash -ne
            [string]$build.payloadSignScriptSha256) {
        throw "Payload signing script is missing or changed after packaging."
    }
    $payloadSignScriptEvidence =
        Get-FileEvidence (Get-Item -LiteralPath $payloadSignScriptPath)
} elseif (-not [string]::IsNullOrWhiteSpace(
    [string]$build.payloadSignScriptPath)) {
    throw "UNSIGNED build must not invoke a payload signing script."
}

$bundleRoot = Resolve-ProjectPath ([string]$build.bundle)
Assert-UnderRoot $bundleRoot $workPrefix "Staged bundle"
if (-not (Test-Path -LiteralPath $bundleRoot -PathType Container)) {
    throw "Staged bundle was not found: $bundleRoot"
}
$bundleManifestPath = Join-Path $bundleRoot "PackageContents.xml"
if ((Get-FileHash -LiteralPath $bundleManifestPath -Algorithm SHA256).Hash -ne
    [string]$build.bundleManifestSha256) {
    throw "Staged bundle manifest no longer matches build-result.json."
}
[xml]$bundleManifest = Get-Content -LiteralPath $bundleManifestPath -Raw -Encoding UTF8
if ([version]([string]$bundleManifest.ApplicationPackage.AppVersion) -ne $buildVersion) {
    throw "Staged bundle version does not match build-result.json."
}

$inventoryPath = Resolve-ProjectPath ([string]$build.bundleInventory)
Assert-UnderRoot $inventoryPath $workPrefix "Bundle inventory"
if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) {
    throw "Bundle inventory was not found: $inventoryPath"
}
if ((Get-FileHash -LiteralPath $inventoryPath -Algorithm SHA256).Hash -ne
    [string]$build.bundleInventorySha256) {
    throw "Bundle inventory no longer matches build-result.json."
}
$inventory = Get-Content -LiteralPath $inventoryPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([int]$inventory.schemaVersion -lt 4) {
    throw "Bundle inventory does not contain the release-file hash chain."
}
if ([version]([string]$inventory.appVersion) -ne $buildVersion) {
    throw "Bundle inventory version does not match build-result.json."
}
$buildProvenancePath = Resolve-ProjectPath ([string]$build.buildProvenance)
Assert-UnderRoot $buildProvenancePath $workPrefix "SDK build provenance"
if (-not (Test-Path -LiteralPath $buildProvenancePath -PathType Leaf)) {
    throw "SDK build provenance was not found: $buildProvenancePath"
}
$buildProvenanceHash = (Get-FileHash -LiteralPath $buildProvenancePath `
    -Algorithm SHA256).Hash
if ($buildProvenanceHash -ne [string]$build.buildProvenanceSha256 -or
    $buildProvenanceHash -ne [string]$inventory.buildProvenanceSha256) {
    throw "SDK build provenance hash chain differs between provenance, inventory, and build."
}
$buildProvenance = Get-Content -LiteralPath $buildProvenancePath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
if ([version]([string]$buildProvenance.appVersion) -ne $buildVersion -or
    [string]$buildProvenance.sdkValidation.validation -ne "passed") {
    throw "SDK build provenance is not valid for this release."
}

$matrixPath = Join-Path $projectRoot `
    "bundle\manifest-template\compatibility-matrix.json"
$matrix = Get-Content -LiteralPath $matrixPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$matrixVerifiedTargets = @($matrix.targets |
    Where-Object { [string]$_.status -eq "verified" })
$matrixVerifiedSeries = @($matrixVerifiedTargets |
    ForEach-Object { [string]$_.series })
$manifestComponents = @($bundleManifest.ApplicationPackage.Components)
$manifestSeries = @($manifestComponents | ForEach-Object {
    [string]$_.RuntimeRequirements.SeriesMin
})
$inventorySeries = @($inventory.targets | ForEach-Object { [string]$_.series })
$buildPayloadSeries = @($build.payloadModules |
    ForEach-Object { [string]$_.series })
$buildVerifiedSeries = @($build.verifiedSeries |
    ForEach-Object { [string]$_ })
$provenanceSeries = @($buildProvenance.targets |
    ForEach-Object { [string]$_.series })

Assert-ExactTextSet $matrixVerifiedSeries $manifestSeries `
    "Compatibility matrix and staged manifest series"
Assert-ExactTextSet $matrixVerifiedSeries $inventorySeries `
    "Compatibility matrix and bundle inventory series"
Assert-ExactTextSet $matrixVerifiedSeries $buildPayloadSeries `
    "Compatibility matrix and build payload series"
Assert-ExactTextSet $matrixVerifiedSeries $buildVerifiedSeries `
    "Compatibility matrix and build verifiedSeries"
Assert-ExactTextSet $matrixVerifiedSeries $provenanceSeries `
    "Compatibility matrix and SDK build provenance series"

foreach ($target in $matrixVerifiedTargets) {
    $series = [string]$target.series
    $provenanceTarget = @($buildProvenance.targets | Where-Object {
        [string]$_.series -eq $series
    })
    $manifestComponent = @($manifestComponents | Where-Object {
        [string]$_.RuntimeRequirements.SeriesMin -eq $series
    })
    $inventoryTarget = @($inventory.targets | Where-Object {
        [string]$_.series -eq $series
    })
    $buildPayload = @($build.payloadModules | Where-Object {
        [string]$_.series -eq $series
    })
    if ($manifestComponent.Count -ne 1 -or $inventoryTarget.Count -ne 1 -or
        $buildPayload.Count -ne 1 -or $provenanceTarget.Count -ne 1) {
        throw "Series $series must have exactly one manifest, inventory, build, " +
            "and SDK provenance entry."
    }
    if ([string]$manifestComponent[0].RuntimeRequirements.SeriesMax -ne $series) {
        throw "Manifest component for $series is not bounded to one series."
    }
    $manifestModule = ([string]$manifestComponent[0].ComponentEntry.ModuleName `
        -replace "\\", "/") -replace "^[./]+", ""
    $expectedModule = ([string]$target.modulePath -replace "\\", "/") `
        -replace "^[./]+", ""
    $inventoryModule = ([string]$inventoryTarget[0].modulePath `
        -replace "\\", "/") -replace "^[./]+", ""
    $buildModule = ([string]$buildPayload[0].stagedPath `
        -replace "\\", "/") -replace "^[./]+", ""
    if ($manifestModule -ne $expectedModule -or
        $inventoryModule -ne $expectedModule -or
        $buildModule -ne $expectedModule) {
        throw "Module path metadata differs for $series."
    }
    if ([int]$inventoryTarget[0].year -ne [int]$target.year -or
        [int]$buildPayload[0].year -ne [int]$target.year -or
        [string]$inventoryTarget[0].runtimeFamily -ne
            [string]$target.runtimeFamily -or
        [string]$buildPayload[0].runtimeFamily -ne
            [string]$target.runtimeFamily -or
        [string]$inventoryTarget[0].status -ne "verified") {
        throw "Year, runtime family, or status metadata differs for $series."
    }
    $sdkValidation = $buildProvenance.sdkValidation
    $expectedApiVersion = [version]([string]$target.managedApiVersion + ".0.0")
    $coreApiEvidence = @($sdkValidation.sdkFiles | Where-Object {
        $_.name -in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")
    })
    if ([string]$sdkValidation.targetSeries -ne $series -or
        [int]$sdkValidation.targetYear -ne [int]$target.year -or
        [string]$sdkValidation.buildProfile -ne [string]$target.buildProfile -or
        $coreApiEvidence.Count -ne 3) {
        throw "SDK validation identity differs for $series."
    }
    foreach ($api in $coreApiEvidence) {
        $apiVersion = [version]([string]$api.assemblyVersion)
        if ($apiVersion.Major -ne $expectedApiVersion.Major -or
            $apiVersion.Minor -ne $expectedApiVersion.Minor -or
            [string]$api.signatureStatus -ne "Valid" -or
            -not ([string]$api.signerSubject).Contains("Autodesk")) {
            throw "SDK API provenance is invalid for ${series}: $($api.name)"
        }
    }
    $expectedSource = ([string]$target.sourceModule -replace "\\", "/")
    if ([string]$inventoryTarget[0].sourcePath -ne $expectedSource -or
        [string]$buildPayload[0].sourcePath -ne $expectedSource) {
        throw "Source module metadata differs for $series."
    }
}

$payloadEvidence = @()
foreach ($payload in @($build.payloadModules)) {
    $inventoryPayload = @($inventory.targets |
        Where-Object { [string]$_.series -eq [string]$payload.series })
    if ($inventoryPayload.Count -ne 1) {
        throw "Inventory must contain one payload for $($payload.series)."
    }
    $sourceModule = Resolve-ProjectPath ([string]$payload.sourcePath)
    Assert-UnderRoot $sourceModule $projectPrefix "Source DLL"
    $stagedModule = [IO.Path]::GetFullPath((Join-Path $bundleRoot `
        (([string]$payload.stagedPath) -replace "/", "\")))
    Assert-UnderRoot $stagedModule ($bundleRoot.TrimEnd("\") + "\") "Staged DLL"
    if (-not (Test-Path -LiteralPath $sourceModule -PathType Leaf) -or
        -not (Test-Path -LiteralPath $stagedModule -PathType Leaf)) {
        throw "Source or staged DLL is missing for $($payload.series)."
    }
    $sourceHash = (Get-FileHash -LiteralPath $sourceModule -Algorithm SHA256).Hash
    $stagedHash = (Get-FileHash -LiteralPath $stagedModule -Algorithm SHA256).Hash
    if ($sourceHash -ne [string]$payload.sourceSha256 -or
        $stagedHash -ne [string]$payload.stagedSha256 -or
        $sourceHash -ne $stagedHash -or
        $sourceHash -ne [string]$inventoryPayload[0].sourceSha256 -or
        $stagedHash -ne [string]$inventoryPayload[0].stagedSha256) {
        throw "DLL hash chain failed for $($payload.series)."
    }
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($stagedModule).Version
    if ($assemblyVersion -ne $buildVersion) {
        throw "DLL version for $($payload.series) is $assemblyVersion, expected $buildVersion."
    }
    $sourceSignature = Get-AuthenticodeSignature -LiteralPath $sourceModule
    $stagedSignature = Get-AuthenticodeSignature -LiteralPath $stagedModule
    if ($releaseKind -eq "unsigned-distribution" -and
        ($sourceSignature.Status -ne
            [Management.Automation.SignatureStatus]::NotSigned -or
        $stagedSignature.Status -ne
            [Management.Automation.SignatureStatus]::NotSigned -or
        [string]$payload.sourceSignature.classification -ne "NotSigned" -or
        [string]$payload.sourceSignature.status -ne "NotSigned" -or
        $null -ne $payload.sourceSignature.signer -or
        [string]$payload.stagedSignature.classification -ne "NotSigned" -or
        [string]$payload.stagedSignature.status -ne "NotSigned" -or
        $null -ne $payload.stagedSignature.signer)) {
        throw "UNSIGNED payload for $($payload.series) must be NotSigned."
    }
    if ($releaseKind -eq "signed-release" -and
        ($sourceSignature.Status -ne
            [Management.Automation.SignatureStatus]::Valid -or
        $stagedSignature.Status -ne
            [Management.Automation.SignatureStatus]::Valid -or
        $null -eq $sourceSignature.SignerCertificate -or
        $null -eq $stagedSignature.SignerCertificate -or
        $null -eq $sourceSignature.TimeStamperCertificate -or
        $null -eq $stagedSignature.TimeStamperCertificate -or
        $sourceSignature.SignerCertificate.Thumbprint.ToUpperInvariant() -ne
            $installerSignerThumbprint -or
        $stagedSignature.SignerCertificate.Thumbprint.ToUpperInvariant() -ne
            $installerSignerThumbprint -or
        [string]$payload.sourceSignature.classification -ne "SignedAllowed" -or
        [string]$payload.stagedSignature.classification -ne "SignedAllowed" -or
        [string]$payload.sourceSignature.signer.thumbprint -ne
            $installerSignerThumbprint -or
        [string]$payload.stagedSignature.signer.thumbprint -ne
            $installerSignerThumbprint -or
        $null -eq $payload.sourceSignature.timestampSigner -or
        $null -eq $payload.stagedSignature.timestampSigner)) {
        throw "Signed payload for $($payload.series) does not have a valid signature."
    }
    $payloadEvidence += [ordered]@{
        year = [int]$payload.year
        series = [string]$payload.series
        runtimeFamily = [string]$payload.runtimeFamily
        source = Get-FileEvidence (Get-Item -LiteralPath $sourceModule)
        staged = Get-FileEvidence (Get-Item -LiteralPath $stagedModule)
        assemblyVersion = $assemblyVersion.ToString()
        sourceSignatureStatus = $sourceSignature.Status.ToString()
        stagedSignatureStatus = $stagedSignature.Status.ToString()
        sourceSignature = Get-PublicSignatureMetadata $payload.sourceSignature
        stagedSignature = Get-PublicSignatureMetadata $payload.stagedSignature
    }
}
if ($payloadEvidence.Count -eq 0) {
    throw "Build result contains no payload modules."
}

$payloadFileEvidence = @()
foreach ($payload in @($build.payloadFiles)) {
    $inventoryTarget = @($inventory.targets | Where-Object {
        [string]$_.series -eq [string]$payload.series
    })
    $inventoryFile = @($inventoryTarget.files | Where-Object {
        [string]$_.name -eq [string]$payload.name
    })
    if ($inventoryTarget.Count -ne 1 -or $inventoryFile.Count -ne 1) {
        throw "Inventory must contain one file entry for $($payload.series)/$($payload.name)."
    }
    $sourceModule = Resolve-ProjectPath ([string]$payload.sourcePath)
    Assert-UnderRoot $sourceModule $projectPrefix "Source module DLL"
    $stagedModule = [IO.Path]::GetFullPath((Join-Path $bundleRoot `
        (([string]$payload.stagedPath) -replace "/", "\")))
    Assert-UnderRoot $stagedModule ($bundleRoot.TrimEnd("\") + "\") `
        "Staged module DLL"
    if (-not (Test-Path -LiteralPath $sourceModule -PathType Leaf) -or
        -not (Test-Path -LiteralPath $stagedModule -PathType Leaf)) {
        throw "Module DLL is missing for $($payload.series)/$($payload.name)."
    }
    $sourceHash = (Get-FileHash -LiteralPath $sourceModule -Algorithm SHA256).Hash
    $stagedHash = (Get-FileHash -LiteralPath $stagedModule -Algorithm SHA256).Hash
    if ($sourceHash -ne [string]$payload.sourceSha256 -or
        $stagedHash -ne [string]$payload.stagedSha256 -or
        $sourceHash -ne $stagedHash -or
        $sourceHash -ne [string]$inventoryFile[0].sourceSha256 -or
        $stagedHash -ne [string]$inventoryFile[0].stagedSha256) {
        throw "Module DLL hash chain failed for $($payload.series)/$($payload.name)."
    }
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($stagedModule).Version
    if ($assemblyVersion -ne $buildVersion) {
        throw "Module DLL version for $($payload.name) is $assemblyVersion, expected $buildVersion."
    }
    $sourceSignature = Get-AuthenticodeSignature -LiteralPath $sourceModule
    $stagedSignature = Get-AuthenticodeSignature -LiteralPath $stagedModule
    if ($releaseKind -eq "unsigned-distribution" -and
        ($sourceSignature.Status -ne
            [Management.Automation.SignatureStatus]::NotSigned -or
        $stagedSignature.Status -ne
            [Management.Automation.SignatureStatus]::NotSigned -or
        [string]$payload.sourceSignature.classification -ne "NotSigned" -or
        [string]$payload.stagedSignature.classification -ne "NotSigned")) {
        throw "UNSIGNED module DLL $($payload.name) must be NotSigned."
    }
    if ($releaseKind -eq "signed-release" -and
        ($sourceSignature.Status -ne
            [Management.Automation.SignatureStatus]::Valid -or
        $stagedSignature.Status -ne
            [Management.Automation.SignatureStatus]::Valid -or
        $null -eq $sourceSignature.SignerCertificate -or
        $null -eq $stagedSignature.SignerCertificate -or
        $sourceSignature.SignerCertificate.Thumbprint.ToUpperInvariant() -ne
            $installerSignerThumbprint -or
        $stagedSignature.SignerCertificate.Thumbprint.ToUpperInvariant() -ne
            $installerSignerThumbprint)) {
        throw "Signed module DLL $($payload.name) does not have a valid signature."
    }
    $payloadFileEvidence += [ordered]@{
        year = [int]$payload.year
        series = [string]$payload.series
        runtimeFamily = [string]$payload.runtimeFamily
        name = [string]$payload.name
        source = Get-FileEvidence (Get-Item -LiteralPath $sourceModule)
        staged = Get-FileEvidence (Get-Item -LiteralPath $stagedModule)
        assemblyVersion = $assemblyVersion.ToString()
        sourceSignatureStatus = $sourceSignature.Status.ToString()
        stagedSignatureStatus = $stagedSignature.Status.ToString()
        sourceSignature = Get-PublicSignatureMetadata $payload.sourceSignature
        stagedSignature = Get-PublicSignatureMetadata $payload.stagedSignature
    }
}
$expectedPayloadFileCount = @($inventory.targets | ForEach-Object {
    @($_.files).Count
} | Measure-Object -Sum).Sum
if ($payloadFileEvidence.Count -ne $expectedPayloadFileCount -or
    $payloadFileEvidence.Count -eq 0) {
    throw "Build result and inventory do not contain the same module DLL set."
}

$expectedRealDrawingSha256 =
    "95041A3827AC36BF021A354D3076215DCCE6ACC7A23203F23DAC7F896708D3E0"
$expectedRealDrawingRectangles = 3520
$expectedRealDrawingRails = 1760
$expectedRealDrawingToolEntities = $expectedRealDrawingRails
$expectedExecutionAssemblyName = "FourJTools.RealDrawingVerify"
$expectedExecutionMarker = "FOURJTOOLS_INTERNAL_REAL_DRAWING_VERIFY_V1"
$realDrawingResultPath = Resolve-ProjectPath $RealDrawingVerificationResult
Assert-UnderRoot $realDrawingResultPath $workPrefix `
    "Real drawing verification result"
if (-not (Test-Path -LiteralPath $realDrawingResultPath -PathType Leaf)) {
    throw "Real drawing verification result was not found: $realDrawingResultPath"
}
if ((Split-Path -Leaf $realDrawingResultPath) -ne "result.json") {
    throw "Real drawing verification result must be a result.json file."
}
$realDrawingRunRoot = (Split-Path -Parent $realDrawingResultPath).TrimEnd("\")
$realDrawingRunPrefix = $realDrawingRunRoot + "\"
Assert-UnderRoot $realDrawingRunRoot $workPrefix `
    "Real drawing verification run"
$realDrawing = Get-Content -LiteralPath $realDrawingResultPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$realDrawing.schemaVersion -lt 1 -or
    [string]$realDrawing.status -ne "PASS" -or
    [string]$realDrawing.mode -ne "RailsOnly") {
    throw "Release evidence requires one schema-1 PASS/RailsOnly real-drawing result."
}
Assert-SamePath ([string]$realDrawing.runDirectory) $realDrawingRunRoot `
    "Real drawing verification runDirectory"
if ([string]$realDrawing.verificationBasis -ne
        "same-source-internal-instrumentation" -or
    [bool]$realDrawing.formalCandidateExecuted -or
    -not ([string]$realDrawing.claimBoundary).Contains(
        "was not NETLOADed into CoreConsole")) {
    throw "Real-drawing result does not preserve the formal/internal execution boundary."
}

$expectedRealDrawingArtifacts = [ordered]@{
    environment = "environment.json"
    result = "result.json"
    commands = "commands.scr"
    coreConsoleLog = "coreconsole.log"
    preScan = "pre-scan.json"
    postScan = "post-scan.json"
    commandOutcome = "command-outcome.json"
    formalCandidateBuildResult = "formal-candidate-build-result.json"
    executionAssembly = "execution\FourJTools.RealDrawingVerify.dll"
    executionBuildRecord = "execution\real-drawing-verifier-build.json"
    executionBuildManifest = "execution\SHA256SUMS.txt"
    sha256Manifest = "SHA256SUMS.txt"
}
$realDrawingArtifacts = @{}
foreach ($artifactEntry in $expectedRealDrawingArtifacts.GetEnumerator()) {
    $artifactProperty = $realDrawing.artifacts.PSObject.Properties[
        [string]$artifactEntry.Key]
    if ($null -eq $artifactProperty -or
        [string]::IsNullOrWhiteSpace([string]$artifactProperty.Value)) {
        throw "Real-drawing result lacks artifact '$($artifactEntry.Key)'."
    }
    $artifactPath = Resolve-ProjectPath ([string]$artifactProperty.Value)
    $expectedArtifactPath = Join-Path $realDrawingRunRoot `
        ([string]$artifactEntry.Value)
    Assert-SamePath $artifactPath $expectedArtifactPath `
        "Real-drawing artifact '$($artifactEntry.Key)'"
    Assert-UnderRoot $artifactPath $realDrawingRunPrefix `
        "Real-drawing artifact '$($artifactEntry.Key)'"
    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "Real-drawing artifact is missing: $artifactPath"
    }
    $realDrawingArtifacts[[string]$artifactEntry.Key] = $artifactPath
}
Assert-SamePath $realDrawingArtifacts.result $realDrawingResultPath `
    "Real-drawing result artifact"

$realDrawingScanScriptPath = Join-Path $realDrawingRunRoot "scan-commands.scr"
$realDrawingScanLogPath = Join-Path $realDrawingRunRoot "scan.log"
$realDrawingWorkingCopyPath = Join-Path $realDrawingRunRoot "input-copy.dwg"
$realDrawingSettingsPath = Join-Path $realDrawingRunRoot `
    "isolate\Roaming\StructuralToolbox\rail-settings.xml"
foreach ($derivedArtifactPath in @(
    $realDrawingScanScriptPath,
    $realDrawingScanLogPath,
    $realDrawingWorkingCopyPath,
    $realDrawingSettingsPath)) {
    Assert-UnderRoot $derivedArtifactPath $realDrawingRunPrefix `
        "Derived real-drawing artifact"
    if (-not (Test-Path -LiteralPath $derivedArtifactPath -PathType Leaf)) {
        throw "Derived real-drawing artifact is missing: $derivedArtifactPath"
    }
}

$runManifestEntries = Read-VerifiedHashManifest `
    $realDrawingArtifacts.sha256Manifest $realDrawingRunPrefix `
    "Real-drawing run SHA256SUMS"
foreach ($manifestArtifactPath in @(
    $realDrawingArtifacts.environment,
    $realDrawingArtifacts.result,
    $realDrawingArtifacts.commands,
    $realDrawingArtifacts.coreConsoleLog,
    $realDrawingArtifacts.preScan,
    $realDrawingArtifacts.postScan,
    $realDrawingArtifacts.commandOutcome,
    $realDrawingArtifacts.formalCandidateBuildResult,
    $realDrawingArtifacts.executionAssembly,
    $realDrawingArtifacts.executionBuildRecord,
    $realDrawingArtifacts.executionBuildManifest,
    $realDrawingScanScriptPath,
    $realDrawingScanLogPath,
    $realDrawingWorkingCopyPath,
    $realDrawingSettingsPath)) {
    Assert-HashManifestContains $runManifestEntries $manifestArtifactPath `
        "Real-drawing run SHA256SUMS"
}
$executionDirectory = Split-Path -Parent `
    $realDrawingArtifacts.executionBuildManifest
$executionPrefix = $executionDirectory.TrimEnd("\") + "\"
$executionManifestEntries = Read-VerifiedHashManifest `
    $realDrawingArtifacts.executionBuildManifest $executionPrefix `
    "Internal execution SHA256SUMS"
foreach ($executionArtifactPath in @(
    $realDrawingArtifacts.executionAssembly,
    $realDrawingArtifacts.executionBuildRecord)) {
    Assert-HashManifestContains $executionManifestEntries `
        $executionArtifactPath "Internal execution SHA256SUMS"
}

$realPayload = @($build.payloadModules | Where-Object {
    [string]$_.series -eq "R23.1"
})
if ($realPayload.Count -ne 1 -or
    [int]$realPayload[0].year -ne 2020 -or
    [string]$realPayload[0].runtimeFamily -ne "legacy-netfx" -or
    [string]$build.sdkValidation.targetSeries -ne "R23.1" -or
    [int]$build.sdkValidation.targetYear -ne 2020 -or
    [string]$build.sdkValidation.validation -ne "passed") {
    throw "Real-drawing evidence requires the final R23.1/AutoCAD 2020 payload."
}
$realCandidatePath = Resolve-ProjectPath ([string]$realPayload[0].sourcePath)
Assert-UnderRoot $realCandidatePath $projectPrefix `
    "Formal real-drawing candidate"
if (-not (Test-Path -LiteralPath $realCandidatePath -PathType Leaf)) {
    throw "Formal real-drawing candidate is missing: $realCandidatePath"
}
$realCandidateHash = (Get-FileHash -LiteralPath $realCandidatePath `
    -Algorithm SHA256).Hash
$realCandidateAssembly = [Reflection.AssemblyName]::GetAssemblyName(
    $realCandidatePath)
$realCandidateMvid = Get-AssemblyMvid $realCandidatePath
if ($realCandidateAssembly.Name -ne "StructuralToolbox" -or
    $realCandidateAssembly.Version -ne $buildVersion -or
    $realCandidateHash -ne [string]$realPayload[0].sourceSha256 -or
    $realCandidateHash -ne [string]$realPayload[0].stagedSha256 -or
    $realCandidateHash -ne [string]$build.sdkValidation.outputSha256) {
    throw "Formal candidate identity does not match the final R23.1 build payload."
}
Assert-SamePath ([string]$realDrawing.formalCandidate.path) `
    $realCandidatePath "Real-drawing formal candidate"
Assert-SamePath ([string]$realDrawing.formalCandidate.buildResultPath) `
    $buildResultPath "Real-drawing final build result"
Assert-SamePath ([string]$realDrawing.formalCandidate.buildResultEvidenceCopy) `
    $realDrawingArtifacts.formalCandidateBuildResult `
    "Real-drawing copied build result"
if ([string]$realDrawing.formalCandidate.assemblyName -ne
        "StructuralToolbox" -or
    [version]$realDrawing.formalCandidate.assemblyVersion -ne $buildVersion -or
    ([guid]([string]$realDrawing.formalCandidate.mvid)).ToString("D") -ne
        $realCandidateMvid -or
    [string]$realDrawing.formalCandidate.sha256Before -ne
        $realCandidateHash -or
    [string]$realDrawing.formalCandidate.sha256After -ne
        $realCandidateHash -or
    -not [bool]$realDrawing.formalCandidate.unchanged -or
    [string]$realDrawing.formalCandidate.buildResultSha256Before -ne
        $buildResultHash -or
    [string]$realDrawing.formalCandidate.buildResultSha256After -ne
        $buildResultHash -or
    -not [bool]$realDrawing.formalCandidate.buildResultUnchanged -or
    (Get-FileHash -LiteralPath `
        $realDrawingArtifacts.formalCandidateBuildResult `
        -Algorithm SHA256).Hash -ne $buildResultHash) {
    throw "Real-drawing formal candidate/build-result hash chain is invalid."
}

$executionAssemblyPath = $realDrawingArtifacts.executionAssembly
$executionAssemblyHash = (Get-FileHash -LiteralPath $executionAssemblyPath `
    -Algorithm SHA256).Hash
$executionAssemblyIdentity = [Reflection.AssemblyName]::GetAssemblyName(
    $executionAssemblyPath)
$executionAssemblyMvid = Get-AssemblyMvid $executionAssemblyPath
$executionBuildRecordHash = (Get-FileHash -LiteralPath `
    $realDrawingArtifacts.executionBuildRecord -Algorithm SHA256).Hash
$executionBuildRecord = Get-Content -LiteralPath `
    $realDrawingArtifacts.executionBuildRecord -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([int]$executionBuildRecord.schemaVersion -lt 1 -or
    [string]$executionBuildRecord.verificationKind -ne
        "same-source-internal-instrumentation" -or
    [bool]$executionBuildRecord.formalCandidateExecutedInCoreConsole -or
    $executionAssemblyIdentity.Name -ne $expectedExecutionAssemblyName -or
    $executionAssemblyIdentity.Version -ne $buildVersion -or
    [string]$executionBuildRecord.executionAssembly.assemblyName -ne
        $expectedExecutionAssemblyName -or
    [version]$executionBuildRecord.executionAssembly.assemblyVersion -ne
        $buildVersion -or
    ([guid]([string]$executionBuildRecord.executionAssembly.mvid)).ToString("D") `
        -ne $executionAssemblyMvid -or
    [string]$executionBuildRecord.executionAssembly.sha256 -ne
        $executionAssemblyHash -or
    [string]$executionBuildRecord.executionAssembly.markerValue -ne
        $expectedExecutionMarker) {
    throw "Internal real-drawing execution assembly receipt is invalid."
}
Assert-SamePath ([string]$executionBuildRecord.formalCandidate.path) `
    $realCandidatePath "Internal receipt formal candidate"
Assert-SamePath ([string]$executionBuildRecord.formalCandidate.buildResultPath) `
    $buildResultPath "Internal receipt build result"
Assert-SamePath ([string]$executionBuildRecord.executionAssembly.path) `
    $executionAssemblyPath "Internal receipt execution assembly"
if ([string]$executionBuildRecord.formalCandidate.assemblyName -ne
        "StructuralToolbox" -or
    [version]$executionBuildRecord.formalCandidate.assemblyVersion -ne
        $buildVersion -or
    ([guid]([string]$executionBuildRecord.formalCandidate.mvid)).ToString("D") `
        -ne $realCandidateMvid -or
    [string]$executionBuildRecord.formalCandidate.sha256 -ne
        $realCandidateHash -or
    [string]$executionBuildRecord.formalCandidate.buildResultSha256 -ne
        $buildResultHash -or
    [int]$executionBuildRecord.formalCandidate.buildResultSchemaVersion -ne
        [int]$build.schemaVersion -or
    [string]$executionBuildRecord.formalCandidate.sourceCodeSha256 -ne
        [string]$build.sourceCodeSha256 -or
    [string]$executionBuildRecord.formalCandidate.payloadSeries -ne "R23.1" -or
    [string]$executionBuildRecord.sourceCodeSha256Before -ne
        [string]$build.sourceCodeSha256 -or
    [string]$executionBuildRecord.sourceCodeSha256After -ne
        [string]$build.sourceCodeSha256 -or
    [string]$executionBuildRecord.formalCandidateSha256Before -ne
        $realCandidateHash -or
    [string]$executionBuildRecord.formalCandidateSha256After -ne
        $realCandidateHash -or
    [string]$executionBuildRecord.candidateBuildResultSha256Before -ne
        $buildResultHash -or
    [string]$executionBuildRecord.candidateBuildResultSha256After -ne
        $buildResultHash -or
    [string]$executionBuildRecord.sdkValidation.targetSeries -ne "R23.1" -or
    [int]$executionBuildRecord.sdkValidation.targetYear -ne 2020) {
    throw "Internal receipt is not fully bound to the final R23.1 candidate."
}
Assert-SamePath ([string]$realDrawing.executionAssembly.path) `
    $executionAssemblyPath "Real-drawing execution assembly"
Assert-SamePath ([string]$realDrawing.executionAssembly.buildRecordPath) `
    $realDrawingArtifacts.executionBuildRecord `
    "Real-drawing execution receipt"
if ([string]$realDrawing.executionAssembly.assemblyName -ne
        $expectedExecutionAssemblyName -or
    [version]$realDrawing.executionAssembly.assemblyVersion -ne $buildVersion -or
    ([guid]([string]$realDrawing.executionAssembly.mvid)).ToString("D") -ne
        $executionAssemblyMvid -or
    [string]$realDrawing.executionAssembly.marker -ne
        $expectedExecutionMarker -or
    [string]$realDrawing.executionAssembly.sha256Before -ne
        $executionAssemblyHash -or
    [string]$realDrawing.executionAssembly.sha256After -ne
        $executionAssemblyHash -or
    -not [bool]$realDrawing.executionAssembly.unchanged -or
    [string]$realDrawing.executionAssembly.buildRecordSha256Before -ne
        $executionBuildRecordHash -or
    [string]$realDrawing.executionAssembly.buildRecordSha256After -ne
        $executionBuildRecordHash -or
    -not [bool]$realDrawing.executionAssembly.buildRecordUnchanged) {
    throw "Real-drawing result does not match the internal execution receipt."
}

$realDrawingEnvironment = Get-Content -LiteralPath `
    $realDrawingArtifacts.environment -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$realDrawingEnvironment.schemaVersion -lt 1 -or
    [string]$realDrawingEnvironment.mode -ne "RailsOnly" -or
    [string]$realDrawingEnvironment.verificationBasis -ne
        "same-source-internal-instrumentation" -or
    [bool]$realDrawingEnvironment.formalCandidateExecuted) {
    throw "Real-drawing environment does not preserve RailsOnly/internal-only execution."
}
Assert-SamePath ([string]$realDrawingEnvironment.projectRoot) $projectRoot `
    "Real-drawing environment project root"
Assert-SamePath ([string]$realDrawingEnvironment.runDirectory) `
    $realDrawingRunRoot "Real-drawing environment run directory"
Assert-SamePath ([string]$realDrawingEnvironment.formalCandidate.path) `
    $realCandidatePath "Environment formal candidate"
Assert-SamePath ([string]$realDrawingEnvironment.formalCandidate.buildResultPath) `
    $buildResultPath "Environment candidate build result"
Assert-SamePath `
    ([string]$realDrawingEnvironment.formalCandidate.buildResultEvidenceCopy) `
    $realDrawingArtifacts.formalCandidateBuildResult `
    "Environment copied build result"
Assert-SamePath ([string]$realDrawingEnvironment.executionAssembly.path) `
    $executionAssemblyPath "Environment execution assembly"
Assert-SamePath ([string]$realDrawingEnvironment.executionAssembly.buildRecordPath) `
    $realDrawingArtifacts.executionBuildRecord "Environment execution receipt"
if ([string]$realDrawingEnvironment.formalCandidate.sha256 -ne
        $realCandidateHash -or
    [string]$realDrawingEnvironment.formalCandidate.sha256After -ne
        $realCandidateHash -or
    -not [bool]$realDrawingEnvironment.formalCandidate.unchanged -or
    [string]$realDrawingEnvironment.formalCandidate.mvid -ne
        $realCandidateMvid -or
    [string]$realDrawingEnvironment.formalCandidate.buildResultSha256 -ne
        $buildResultHash -or
    [string]$realDrawingEnvironment.formalCandidate.buildResultSha256After -ne
        $buildResultHash -or
    -not [bool]$realDrawingEnvironment.formalCandidate.buildResultUnchanged -or
    [string]$realDrawingEnvironment.executionAssembly.assemblyName -ne
        $expectedExecutionAssemblyName -or
    [string]$realDrawingEnvironment.executionAssembly.mvid -ne
        $executionAssemblyMvid -or
    [string]$realDrawingEnvironment.executionAssembly.sha256 -ne
        $executionAssemblyHash -or
    [string]$realDrawingEnvironment.executionAssembly.sha256After -ne
        $executionAssemblyHash -or
    -not [bool]$realDrawingEnvironment.executionAssembly.unchanged -or
    [string]$realDrawingEnvironment.executionAssembly.buildRecordSha256 -ne
        $executionBuildRecordHash -or
    [string]$realDrawingEnvironment.executionAssembly.buildRecordSha256After `
        -ne $executionBuildRecordHash -or
    -not [bool]$realDrawingEnvironment.executionAssembly.buildRecordUnchanged) {
    throw "Real-drawing environment candidate/execution hash chain is invalid."
}

$realCoreConsolePath = [IO.Path]::GetFullPath(
    [string]$realDrawingEnvironment.autoCAD.coreConsolePath)
if (-not (Test-Path -LiteralPath $realCoreConsolePath -PathType Leaf)) {
    throw "Real-drawing CoreConsole executable is missing."
}
$realCoreConsoleFile = Get-Item -LiteralPath $realCoreConsolePath
$realCoreConsoleHash = (Get-FileHash -LiteralPath $realCoreConsolePath `
    -Algorithm SHA256).Hash
$realCoreConsoleSignature = Get-AuthenticodeSignature `
    -LiteralPath $realCoreConsolePath
if ($realCoreConsoleFile.Name -ne "accoreconsole.exe" -or
    [string]$realCoreConsoleFile.VersionInfo.ProductName -ne "AcCoreConsole" -or
    -not ([string]$realCoreConsoleFile.VersionInfo.FileVersion).StartsWith(
        "23.1.", [StringComparison]::Ordinal) -or
    [string]$realDrawingEnvironment.autoCAD.coreConsoleFileVersion -ne
        [string]$realCoreConsoleFile.VersionInfo.FileVersion -or
    [string]$realDrawingEnvironment.autoCAD.coreConsoleProductVersion -ne
        [string]$realCoreConsoleFile.VersionInfo.ProductVersion -or
    [string]$realDrawingEnvironment.autoCAD.coreConsoleSha256 -ne
        $realCoreConsoleHash -or
    $realCoreConsoleSignature.Status -ne
        [Management.Automation.SignatureStatus]::Valid -or
    $null -eq $realCoreConsoleSignature.SignerCertificate -or
    -not $realCoreConsoleSignature.SignerCertificate.Subject.Contains(
        "Autodesk")) {
    throw "Real-drawing execution host is not Autodesk AutoCAD 2020 R23.1."
}

$preScan = Get-Content -LiteralPath $realDrawingArtifacts.preScan `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$postScan = Get-Content -LiteralPath $realDrawingArtifacts.postScan `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$commandOutcome = Get-Content -LiteralPath `
    $realDrawingArtifacts.commandOutcome -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$preScan.schemaVersion -lt 2 -or
    [int]$postScan.schemaVersion -lt 2 -or
    [int]$commandOutcome.schemaVersion -lt 1 -or
    [string]$commandOutcome.status -ne "ENDED" -or
    [string]$commandOutcome.globalCommandName -ne "STPVRAIL") {
    throw "Real-drawing pre/post scan or command outcome is invalid."
}
foreach ($runtimeSnapshot in @(
    [pscustomobject]@{ Name = "pre-scan"; Value = $preScan },
    [pscustomobject]@{ Name = "command-ended"; Value = $commandOutcome.snapshot },
    [pscustomobject]@{ Name = "post-scan"; Value = $postScan })) {
    $identity = $runtimeSnapshot.Value.executionAssembly
    if ([string]$identity.name -ne $expectedExecutionAssemblyName -or
        [version]$identity.assemblyVersion -ne $buildVersion -or
        ([guid]([string]$identity.mvid)).ToString("D") -ne
            $executionAssemblyMvid -or
        [string]$identity.sha256 -ne $executionAssemblyHash -or
        [string]$identity.verificationMarker -ne $expectedExecutionMarker) {
        throw "$($runtimeSnapshot.Name) did not execute the bound internal assembly."
    }
}

if ([int]$preScan.largestPanelLayer.count -ne
        $expectedRealDrawingRectangles -or
    [int]$preScan.plan.recognizedPanelCount -ne
        $expectedRealDrawingRectangles -or
    [int]$preScan.plan.railSegmentCount -ne $expectedRealDrawingRails -or
    [int]$preScan.rails.count -ne 0 -or
    [int]$preScan.nativeDimensions.count -lt 0 -or
    [int]$preScan.toolEntities.count -ne 0) {
    throw "Real-drawing pre-scan does not match the frozen " +
        "3520/1760 rail-only baseline."
}
$preOutputLayerEntities = 0
foreach ($property in $preScan.toolEntities.outputLayerEntities.PSObject.Properties) {
    $preOutputLayerEntities += [int]$property.Value
}
if ($preOutputLayerEntities -ne 0) {
    throw "Real-drawing working copy contains pre-existing output-layer entities."
}

if ([int]$realDrawing.selection.recognizedRectangles -ne
        $expectedRealDrawingRectangles -or
    [int]$realDrawing.selection.plannedRails -ne
        $expectedRealDrawingRails -or
    [string]$realDrawing.command.name -ne "STPVRAIL" -or
    [string]$realDrawing.command.requestedMode -ne "RailsOnly" -or
    [string]$realDrawing.command.observerStatus -ne "ENDED" -or
    [int]$realDrawing.command.completedRails -ne
        $expectedRealDrawingRails -or
    [int]$realDrawing.command.savedRails -ne $expectedRealDrawingRails -or
    [int]$realDrawing.command.nativeDimensionsBefore -ne
        [int]$preScan.nativeDimensions.count -or
    [int]$realDrawing.command.nativeDimensionsDuringCommand -ne
        ([int]$preScan.nativeDimensions.count + 1) -or
    [int]$realDrawing.command.nativeDimensionsAfter -ne
        [int]$preScan.nativeDimensions.count -or
    [bool]$realDrawing.command.process.timedOut -or
    [int]$realDrawing.command.process.exitCode -ne 0) {
    throw "Real-drawing result summary does not match the rail-only baseline run."
}
if ([int]$commandOutcome.snapshot.rails.count -ne
        $expectedRealDrawingRails -or
    [int]$commandOutcome.snapshot.nativeDimensions.count -ne
        ([int]$preScan.nativeDimensions.count + 1) -or
    [int]$postScan.rails.count -ne $expectedRealDrawingRails -or
    [int]$postScan.nativeDimensions.count -ne
        [int]$preScan.nativeDimensions.count) {
    throw "Planned, command-ended, and saved rail-only counts differ."
}
$toolVersionProperty = $postScan.toolEntities.versions.PSObject.Properties[
    $version3]
if ([int]$postScan.toolEntities.count -ne
        $expectedRealDrawingToolEntities -or
    [int]$postScan.toolEntities.metadataV3Count -ne
        $expectedRealDrawingToolEntities -or
    $null -eq $toolVersionProperty -or
    [int]$toolVersionProperty.Value -ne $expectedRealDrawingToolEntities -or
    [int]$postScan.toolEntities.invalidXDataCount -ne 0 -or
    -not [bool]$postScan.toolEntities.allMetadataV3) {
    throw "Saved rail entities are not all v$version3 entities with valid META_V3 metadata."
}

$realSourcePath = Resolve-ProjectPath ([string]$realDrawing.sourceDrawing.path)
Assert-UnderRoot $realSourcePath $projectPrefix "Real source drawing"
if (-not (Test-Path -LiteralPath $realSourcePath -PathType Leaf)) {
    throw "Real source drawing is missing: $realSourcePath"
}
$realSourceHash = (Get-FileHash -LiteralPath $realSourcePath `
    -Algorithm SHA256).Hash
Assert-SamePath ([string]$realDrawingEnvironment.sourceDrawing.path) `
    $realSourcePath "Environment source drawing"
Assert-SamePath ([string]$realDrawingEnvironment.workingCopy.path) `
    $realDrawingWorkingCopyPath "Environment working drawing"
Assert-SamePath ([string]$realDrawing.settingsIsolation.isolatedPath) `
    $realDrawingSettingsPath "Isolated real-drawing settings"
if ($realSourceHash -ne $expectedRealDrawingSha256 -or
    [string]$realDrawing.sourceDrawing.sha256Before -ne $realSourceHash -or
    [string]$realDrawing.sourceDrawing.sha256After -ne $realSourceHash -or
    -not [bool]$realDrawing.sourceDrawing.unchanged -or
    [string]$realDrawingEnvironment.sourceDrawing.sha256 -ne $realSourceHash -or
    [string]$realDrawingEnvironment.workingCopy.initialSha256 -ne
        $realSourceHash -or
    [string]$realDrawing.settingsIsolation.isolatedSha256Before -ne
        [string]$realDrawing.settingsIsolation.isolatedSha256After -or
    -not [bool]$realDrawing.settingsIsolation.isolatedUnchanged -or
    -not [bool]$realDrawing.settingsIsolation.userUnchanged) {
    throw "Real source drawing or isolated settings changed during verification."
}
$realCommandLogText = Get-Content -LiteralPath `
    $realDrawingArtifacts.coreConsoleLog -Raw -Encoding UTF8

$realDrawingArtifactEvidence = [ordered]@{
    result = Get-FileEvidence (Get-Item -LiteralPath $realDrawingResultPath)
    environment = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.environment)
    preScan = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.preScan)
    postScan = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.postScan)
    commandOutcome = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.commandOutcome)
    commandScript = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.commands)
    commandLog = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.coreConsoleLog)
    scanScript = Get-FileEvidence (Get-Item -LiteralPath $realDrawingScanScriptPath)
    scanLog = Get-FileEvidence (Get-Item -LiteralPath $realDrawingScanLogPath)
    workingDrawing = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingWorkingCopyPath)
    formalCandidateBuildResultCopy = Get-FileEvidence `
        (Get-Item -LiteralPath `
            $realDrawingArtifacts.formalCandidateBuildResult)
    executionAssembly = Get-FileEvidence `
        (Get-Item -LiteralPath $executionAssemblyPath)
    executionBuildReceipt = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.executionBuildRecord)
    executionHashManifest = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.executionBuildManifest)
    runHashManifest = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingArtifacts.sha256Manifest)
    isolatedSettings = Get-FileEvidence `
        (Get-Item -LiteralPath $realDrawingSettingsPath)
}
$realDrawingVerificationEvidence = [ordered]@{
    status = "PASS"
    mode = "RailsOnly"
    verificationBasis = "same-source-internal-instrumentation"
    formalCandidateExecuted = $false
    claimBoundary = "The formal StructuralToolbox.dll was hash/MVID/build-result bound but was not loaded or executed. The rail-only STPVRAIL command was executed only by the same-source internal FourJTools.RealDrawingVerify assembly."
    sourceDrawing = Get-FileEvidence (Get-Item -LiteralPath $realSourcePath)
    host = [ordered]@{
        year = 2020
        series = "R23.1"
        fileName = $realCoreConsoleFile.Name
        fileVersion = [string]$realCoreConsoleFile.VersionInfo.FileVersion
        productVersion = [string]$realCoreConsoleFile.VersionInfo.ProductVersion
        sha256 = $realCoreConsoleHash
        signatureStatus = $realCoreConsoleSignature.Status.ToString()
        signerSubject = $realCoreConsoleSignature.SignerCertificate.Subject
    }
    formalCandidate = [ordered]@{
        assemblyName = "StructuralToolbox"
        assemblyVersion = $realCandidateAssembly.Version.ToString()
        mvid = $realCandidateMvid
        sha256 = $realCandidateHash
        payloadSeries = "R23.1"
        buildResultSha256 = $buildResultHash
    }
    executionAssembly = [ordered]@{
        assemblyName = $expectedExecutionAssemblyName
        assemblyVersion = $executionAssemblyIdentity.Version.ToString()
        mvid = $executionAssemblyMvid
        sha256 = $executionAssemblyHash
        marker = $expectedExecutionMarker
        buildReceiptSha256 = $executionBuildRecordHash
    }
    baseline = [ordered]@{
        recognizedRectangles = $expectedRealDrawingRectangles
        plannedRails = $expectedRealDrawingRails
        savedRails = $expectedRealDrawingRails
        nativeDimensionsUnchanged = $true
        allMetadataV3 = $true
        sourceDrawingUnchanged = $true
    }
    artifacts = $realDrawingArtifactEvidence
}

$requiredVerificationMarkers = @(
    "STVERIFY-DONE",
    "DIMENSIONS_UNCHANGED=True"
)
$verifiedRuntimeSeries =
    [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$runtimeVerificationEvidence = @()
foreach ($verificationResultValue in $AutoCADVerificationResults) {
    $verificationResultPath = Resolve-ProjectPath $verificationResultValue
    Assert-UnderRoot $verificationResultPath $workPrefix `
        "AutoCAD verification result"
    if (-not (Test-Path -LiteralPath $verificationResultPath -PathType Leaf)) {
        throw "AutoCAD verification result was not found: $verificationResultPath"
    }
    $verificationRunRoot = (Split-Path -Parent $verificationResultPath).TrimEnd("\")
    $verificationRunPrefix = $verificationRunRoot + "\"
    $verification = Get-Content -LiteralPath $verificationResultPath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$verification.Status -ne "PASS" -or
        [int]$verification.CoreConsoleExitCode -ne 0) {
        throw "AutoCAD verification did not pass: $verificationResultPath"
    }
    $environmentPath = Resolve-ProjectPath `
        ([string]$verification.EnvironmentLog)
    $coreConsoleLogPath = Resolve-ProjectPath `
        ([string]$verification.CoreConsoleLog)
    $outputDwgPath = Resolve-ProjectPath ([string]$verification.OutputDwg)
    foreach ($evidenceFilePath in @(
        $environmentPath, $coreConsoleLogPath, $outputDwgPath)) {
        Assert-UnderRoot $evidenceFilePath $verificationRunPrefix `
            "AutoCAD verification evidence"
        if (-not (Test-Path -LiteralPath $evidenceFilePath -PathType Leaf)) {
            throw "AutoCAD verification evidence is missing: $evidenceFilePath"
        }
    }
    if ((Get-FileHash -LiteralPath $outputDwgPath -Algorithm SHA256).Hash -ne
        [string]$verification.OutputDwgSHA256) {
        throw "AutoCAD verification output DWG hash no longer matches result.json."
    }
    $environmentHash = (Get-FileHash -LiteralPath $environmentPath `
        -Algorithm SHA256).Hash
    $coreConsoleLogHash = (Get-FileHash -LiteralPath $coreConsoleLogPath `
        -Algorithm SHA256).Hash
    if ([string]::IsNullOrWhiteSpace(
            [string]$verification.EnvironmentSHA256) -or
        $environmentHash -ne [string]$verification.EnvironmentSHA256) {
        throw "AutoCAD verification environment hash no longer matches result.json."
    }
    if ([string]::IsNullOrWhiteSpace(
            [string]$verification.CoreConsoleLogSHA256) -or
        $coreConsoleLogHash -ne [string]$verification.CoreConsoleLogSHA256) {
        throw "AutoCAD verification CoreConsole log hash no longer matches result.json."
    }

    $coreConsoleText = [IO.File]::ReadAllText($coreConsoleLogPath,
        [Text.Encoding]::UTF8)
    $successMatches = [regex]::Matches($coreConsoleText,
        'STVERIFY-DONE[^\r\n]*')
    $failureMatches = [regex]::Matches($coreConsoleText,
        'STVERIFY-FAIL[^\r\n]*')
    if ($successMatches.Count -ne 1 -or $failureMatches.Count -ne 0) {
        throw "CoreConsole log must contain one success marker and no failure marker."
    }
    $successMarker = $successMatches[0].Value
    if ([string]$verification.SuccessMarker -ne $successMarker) {
        throw "result.json success marker does not match the CoreConsole log."
    }
    foreach ($requiredMarker in $requiredVerificationMarkers) {
        if (-not $successMarker.Contains($requiredMarker)) {
            throw "AutoCAD verification is missing '$requiredMarker': " +
                $verificationResultPath
        }
    }
    $loadedHashMatch = [regex]::Match($successMarker,
        '(?:^|\s)RELEASE_SHA256=([0-9A-Fa-f]{64})(?:\s|$)')
    $loadedMvidMatch = [regex]::Match($successMarker,
        '(?:^|\s)RELEASE_MVID=([0-9A-Fa-f-]{36})(?:\s|$)')
    $loadedPathMatch = [regex]::Match($successMarker,
        '(?:^|\s)RELEASE_PATH="([^"]+)"(?:\s|$)')
    if (-not $loadedHashMatch.Success -or -not $loadedMvidMatch.Success -or
        -not $loadedPathMatch.Success) {
        throw "CoreConsole marker does not identify the actually loaded release assembly."
    }
    $loadedDllHash = $loadedHashMatch.Groups[1].Value.ToUpperInvariant()
    $loadedMvid = [guid]$loadedMvidMatch.Groups[1].Value
    $reportedLoadedDllPath =
        [IO.Path]::GetFullPath($loadedPathMatch.Groups[1].Value)
    $loadedDllPath = $reportedLoadedDllPath
    if ($reportedLoadedDllPath.StartsWith("Q:\",
        [StringComparison]::OrdinalIgnoreCase)) {
        $loadedDllPath = [IO.Path]::GetFullPath((Join-Path $projectRoot `
            $reportedLoadedDllPath.Substring(3)))
    }

    $verificationEnvironment = Get-Content -LiteralPath $environmentPath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    $series = [string]$verificationEnvironment.AutoCADSeries
    if ([string]::IsNullOrWhiteSpace($series) -or
        -not $verifiedRuntimeSeries.Add($series)) {
        throw "AutoCAD verification series is missing or duplicated: $series"
    }
    if (-not [bool]$verificationEnvironment.ReleaseBuildSkipped) {
        throw "Release evidence requires -SkipReleaseBuild so the tested DLL " +
            "is byte-identical to the packaged DLL: $verificationResultPath"
    }
    $payload = @($build.payloadModules |
        Where-Object { [string]$_.series -eq $series })
    if ($payload.Count -ne 1) {
        throw "AutoCAD verification series is not a packaged payload: $series"
    }
    $expectedLoadedDllPath = Resolve-ProjectPath ([string]$payload[0].sourcePath)
    if (-not [string]::Equals($loadedDllPath, $expectedLoadedDllPath,
        [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($reportedLoadedDllPath,
            [string]$verificationEnvironment.LoadedReleaseAssembly.Path,
            [StringComparison]::OrdinalIgnoreCase) -or
        $loadedMvid.ToString("D") -ne
            [string]$verificationEnvironment.LoadedReleaseAssembly.MVID) {
        throw "Actually loaded release path or MVID does not match the evidence."
    }
    if ([string]::IsNullOrWhiteSpace($loadedDllHash) -or
        $loadedDllHash -ne [string]$payload[0].sourceSha256 -or
        $loadedDllHash -ne [string]$payload[0].stagedSha256 -or
        $loadedDllHash -ne
            [string]$verificationEnvironment.ReleaseAssemblySHA256BeforeInternalBuild -or
        $loadedDllHash -ne
            [string]$verificationEnvironment.LoadedReleaseAssembly.SHA256) {
        throw "AutoCAD verification DLL hash does not match the packaged " +
            "source/staged payload for $series."
    }

    $matrixTarget = @($matrixVerifiedTargets | Where-Object {
        [string]$_.series -eq $series
    })
    if ($matrixTarget.Count -ne 1 -or
        [int]$verificationEnvironment.AutoCADYear -ne
            [int]$matrixTarget[0].year) {
        throw "AutoCAD verification host year does not match the compatibility matrix."
    }
    $coreConsoleEvidence = $verificationEnvironment.CoreConsole
    if ($null -eq $coreConsoleEvidence -or
        [string]::IsNullOrWhiteSpace([string]$coreConsoleEvidence.Path)) {
        throw "AutoCAD verification environment does not identify CoreConsole."
    }
    $coreConsolePath = [IO.Path]::GetFullPath([string]$coreConsoleEvidence.Path)
    if (-not (Test-Path -LiteralPath $coreConsolePath -PathType Leaf)) {
        throw "Verified CoreConsole executable is missing: $coreConsolePath"
    }
    $coreConsoleFile = Get-Item -LiteralPath $coreConsolePath
    $coreConsoleHash = (Get-FileHash -LiteralPath $coreConsolePath `
        -Algorithm SHA256).Hash
    $coreConsoleSignature = Get-AuthenticodeSignature -LiteralPath $coreConsolePath
    if ($coreConsoleHash -ne [string]$coreConsoleEvidence.SHA256 -or
        [string]$coreConsoleFile.VersionInfo.FileVersion -ne
            [string]$coreConsoleEvidence.FileVersion -or
        [string]$coreConsoleFile.VersionInfo.ProductVersion -ne
            [string]$coreConsoleEvidence.ProductVersion -or
        [string]$coreConsoleFile.VersionInfo.ProductName -ne "AcCoreConsole" -or
        $coreConsoleSignature.Status -ne
            [Management.Automation.SignatureStatus]::Valid -or
        [string]$coreConsoleEvidence.SignatureStatus -ne "Valid" -or
        $null -eq $coreConsoleSignature.SignerCertificate -or
        -not $coreConsoleSignature.SignerCertificate.Subject.Contains("Autodesk")) {
        throw "CoreConsole identity, version, hash, or Autodesk signature is invalid."
    }
    $seriesVersion = $series.Substring(1)
    if (-not ([string]$coreConsoleFile.VersionInfo.FileVersion).StartsWith(
        $seriesVersion + ".", [StringComparison]::Ordinal)) {
        throw "CoreConsole file version does not match AutoCAD series $series."
    }
    if (-not [string]::IsNullOrWhiteSpace($AutoCADExecutable)) {
        $expectedCadPath = [IO.Path]::GetFullPath($AutoCADExecutable)
        if (-not [string]::Equals(
            [string](Split-Path -Parent $coreConsolePath),
            [string](Split-Path -Parent $expectedCadPath),
            [StringComparison]::OrdinalIgnoreCase)) {
            throw "CoreConsole and AutoCAD executable directories differ."
        }
    }
    $relativeLoadedDllPath = Get-RelativeProjectPath $loadedDllPath
    $publicSuccessMarker = [regex]::Replace($successMarker,
        'RELEASE_PATH="[^"]+"',
        'RELEASE_PATH="' + $relativeLoadedDllPath + '"')
    $runtimeVerificationEvidence += [ordered]@{
        year = [int]$verificationEnvironment.AutoCADYear
        series = $series
        status = [string]$verification.Status
        successMarker = $publicSuccessMarker
        successMarkerSha256 = Get-TextSha256 $successMarker
        loadedReleaseDllPath = $relativeLoadedDllPath
        resolvedReleaseDllPath = $relativeLoadedDllPath
        loadedReleaseDllSha256 = $loadedDllHash
        loadedReleaseMvid = $loadedMvid.ToString("D")
        releaseBuildSkipped =
            [bool]$verificationEnvironment.ReleaseBuildSkipped
        coreConsole = [ordered]@{
            fileName = $coreConsoleFile.Name
            fileVersion = [string]$coreConsoleFile.VersionInfo.FileVersion
            productVersion = [string]$coreConsoleFile.VersionInfo.ProductVersion
            sha256 = $coreConsoleHash
            signatureStatus = $coreConsoleSignature.Status.ToString()
            signerSubject = $coreConsoleSignature.SignerCertificate.Subject
        }
        result = Get-FileEvidence (Get-Item -LiteralPath $verificationResultPath)
        environment = Get-FileEvidence (Get-Item -LiteralPath $environmentPath)
        coreConsoleLog = Get-FileEvidence `
            (Get-Item -LiteralPath $coreConsoleLogPath)
        outputDwg = Get-FileEvidence (Get-Item -LiteralPath $outputDwgPath)
    }
}
$runtimeSeries = @($runtimeVerificationEvidence |
    ForEach-Object { [string]$_.series })
Assert-ExactTextSet $matrixVerifiedSeries $runtimeSeries `
    "Compatibility matrix and runtime verification series"

function Get-RequiredVerificationLog(
    [string]$Value,
    [string]$Description,
    [string[]]$RequiredText) {
    $path = Resolve-ProjectPath $Value
    Assert-UnderRoot $path $workPrefix $Description
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$Description is missing: $path"
    }
    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    foreach ($marker in $RequiredText) {
        if (-not $content.Contains($marker)) {
            throw "$Description lacks required marker '$marker': $path"
        }
    }
    return [pscustomobject]@{
        Path = $path
        Content = $content
        Evidence = Get-FileEvidence (Get-Item -LiteralPath $path)
    }
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

$pureCodeLog = Get-RequiredVerificationLog $PureCodeRegressionLog `
    "Pure-code regression log" @(
        "rail layout tests",
        "release traceability source tests",
        "PURE_CODE_REGRESSION=PASS")
$installerArtifactLog = Get-RequiredVerificationLog `
    $InstallerArtifactVerificationLog "Installer artifact verification log" @(
        "PASS: SDK build provenance hash is chained through inventory and build result",
        "PASS: unsigned installer uses the normal transferable filename",
        "PASS: INSTALLER_ARTIFACT_VERIFICATION=PASS")
$installerRuntimeLog = Get-RequiredVerificationLog `
    $InstallerRuntimeVerificationLog "Installer runtime verification log" @(
        "PASS: isolated installer uses a frozen release-candidate payload",
        "PASS: installed payload hash matches frozen candidate",
        "PASS: RUNTIME_VERIFICATION=PASS")

$publishReceiptPath = Resolve-ProjectPath $PublishReceipt
Assert-UnderRoot $publishReceiptPath $workPrefix "Publish receipt"
if (-not (Test-Path -LiteralPath $publishReceiptPath -PathType Leaf) -or
    (Split-Path -Leaf $publishReceiptPath) -notlike "*-publish-result.json") {
    throw "Publish receipt is missing or has an invalid filename: $publishReceiptPath"
}
$publishReceipt = Get-Content -LiteralPath $publishReceiptPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$publishReceipt.schemaVersion -lt 2 -or
    [string]$publishReceipt.status -ne "PASS" -or
    [string]$publishReceipt.releaseVersion -ne $version3 -or
    [string]$publishReceipt.classification -ne
        [string]$releaseDefinition.currentReleaseClassification) {
    throw "Publish receipt identity is invalid for v$version3."
}
Assert-SamePath (Resolve-ProjectPath ([string]$publishReceipt.releaseDirectory)) `
    $releaseRoot "Publish receipt release directory"
if ([string]$publishReceipt.buildResultSha256 -ne $buildResultHash -or
    [string]$publishReceipt.installerFileName -ne $releaseInstaller.Name -or
    [string]$publishReceipt.installerSha256 -ne $releaseInstallerHash) {
    throw "Publish receipt is not bound to this build and one-EXE release."
}

$pureVersion = Get-SingleReceiptValue $pureCodeLog.Content `
    "RELEASE_VERSION" "Pure-code regression log"
$pureSourceHash = Get-SingleReceiptValue $pureCodeLog.Content `
    "SOURCE_CODE_SHA256" "Pure-code regression log"
$pureRegressionSuiteHash = Get-SingleReceiptValue $pureCodeLog.Content `
    "REGRESSION_SUITE_SHA256" "Pure-code regression log"
$currentRegressionSuiteHash = Get-RegressionSuiteFingerprint $projectRoot
if ($pureVersion -ne $version3 -or
    $pureSourceHash -ne [string]$build.sourceCodeSha256 -or
    $pureRegressionSuiteHash -ne $currentRegressionSuiteHash) {
    throw "Pure-code regression receipt is not bound to this source, " +
        "regression suite, and version."
}
$artifactVersion = Get-SingleReceiptValue $installerArtifactLog.Content `
    "RELEASE_VERSION" "Installer artifact verification log"
$artifactSourceHash = Get-SingleReceiptValue $installerArtifactLog.Content `
    "SOURCE_CODE_SHA256" "Installer artifact verification log"
$artifactBuildHash = Get-SingleReceiptValue $installerArtifactLog.Content `
    "BUILD_RESULT_SHA256" "Installer artifact verification log"
$artifactInstallerHash = Get-SingleReceiptValue $installerArtifactLog.Content `
    "INSTALLER_SHA256" "Installer artifact verification log"
$artifactPayloadHash = Get-SingleReceiptValue $installerArtifactLog.Content `
    "PAYLOAD_SHA256" "Installer artifact verification log"
$artifactPayloadFileCount = Get-SingleReceiptValue `
    $installerArtifactLog.Content "PAYLOAD_FILE_COUNT" `
    "Installer artifact verification log"
$artifactPayloadFilesHash = Get-SingleReceiptValue `
    $installerArtifactLog.Content "PAYLOAD_FILES_SHA256" `
    "Installer artifact verification log"
$receiptPayload = @($build.payloadModules | Where-Object {
    [string]$_.series -eq "R23.1"
})
$receiptPayloadFiles = @($build.payloadFiles | Where-Object {
    [string]$_.series -eq "R23.1"
})
$expectedPayloadFilesHash = Get-PayloadSetSha256 $receiptPayloadFiles `
    "name" "sourceSha256"
if ($artifactVersion -ne $version3 -or
    $artifactSourceHash -ne [string]$build.sourceCodeSha256 -or
    $artifactBuildHash -ne $buildResultHash -or
    $artifactInstallerHash -ne [string]$build.installerSha256 -or
    $receiptPayload.Count -ne 1 -or
    $artifactPayloadHash -ne [string]$receiptPayload[0].sourceSha256 -or
    $artifactPayloadFileCount -ne "6" -or
    $receiptPayloadFiles.Count -ne 6 -or
    $artifactPayloadFilesHash -ne $expectedPayloadFilesHash) {
    throw "Installer artifact receipt is not bound to this final candidate."
}

$runtimeTestDirectory = Split-Path -Parent $installerRuntimeLog.Path
$runtimeTestBuildResultPath = Join-Path $runtimeTestDirectory "build-result.json"
if (-not (Test-Path -LiteralPath $runtimeTestBuildResultPath -PathType Leaf)) {
    throw "Installer runtime verification lacks its build-result.json."
}
$runtimeTestBuild = Get-Content -LiteralPath $runtimeTestBuildResultPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$runtimeOrigin = $runtimeTestBuild.payloadOrigin
$runtimeVersion = Get-SingleReceiptValue $installerRuntimeLog.Content `
    "RELEASE_VERSION" "Installer runtime verification log"
$runtimeFrozenBuildHash = Get-SingleReceiptValue $installerRuntimeLog.Content `
    "FROZEN_BUILD_RESULT_SHA256" "Installer runtime verification log"
$runtimeFrozenInstallerHash = Get-SingleReceiptValue $installerRuntimeLog.Content `
    "FROZEN_INSTALLER_SHA256" "Installer runtime verification log"
$runtimeTestBuildHash = Get-SingleReceiptValue $installerRuntimeLog.Content `
    "TEST_BUILD_RESULT_SHA256" "Installer runtime verification log"
$runtimeTestInstallerHash = Get-SingleReceiptValue $installerRuntimeLog.Content `
    "TEST_INSTALLER_SHA256" "Installer runtime verification log"
$runtimeInstalledPayloadSeries = Get-SingleReceiptValue `
    $installerRuntimeLog.Content "INSTALLED_PAYLOAD_SERIES" `
    "Installer runtime verification log"
$runtimeInstalledPayloadHash = Get-SingleReceiptValue `
    $installerRuntimeLog.Content "INSTALLED_PAYLOAD_SHA256" `
    "Installer runtime verification log"
$runtimeInstalledPayloadFileCount = Get-SingleReceiptValue `
    $installerRuntimeLog.Content "INSTALLED_PAYLOAD_FILE_COUNT" `
    "Installer runtime verification log"
$runtimeInstalledPayloadFilesHash = Get-SingleReceiptValue `
    $installerRuntimeLog.Content "INSTALLED_PAYLOAD_FILES_SHA256" `
    "Installer runtime verification log"
$runtimeInstallerPath = Resolve-ProjectPath ([string]$runtimeTestBuild.installer)
Assert-UnderRoot $runtimeInstallerPath $workPrefix "Runtime test installer"
$runtimePayload = @($runtimeTestBuild.payloadModules | Where-Object {
    [string]$_.series -eq $runtimeInstalledPayloadSeries
})
$runtimePayloadFiles = @($runtimeTestBuild.payloadFiles | Where-Object {
    [string]$_.series -eq $runtimeInstalledPayloadSeries
})
$expectedRuntimePayloadFilesHash = Get-PayloadSetSha256 $runtimePayloadFiles `
    "name" "stagedSha256"
if (-not [bool]$runtimeTestBuild.testBuild -or
    ([version]([string]$runtimeTestBuild.appVersion)).ToString(3) -ne $version3 -or
    [string]$runtimeOrigin.mode -ne "frozen-release-candidate" -or
    [IO.Path]::GetFullPath([string]$runtimeOrigin.referenceBuildResult) -ne
        $buildResultPath -or
    [string]$runtimeOrigin.referenceBuildResultSha256 -ne
        (Get-FileHash -LiteralPath $buildResultPath -Algorithm SHA256).Hash -or
    [string]$runtimeOrigin.referenceInstallerSha256 -ne
        [string]$build.installerSha256 -or
    $runtimeVersion -ne $version3 -or
    $runtimeFrozenBuildHash -ne $buildResultHash -or
    $runtimeFrozenInstallerHash -ne [string]$build.installerSha256 -or
    $runtimeTestBuildHash -ne
        (Get-FileHash -LiteralPath $runtimeTestBuildResultPath `
            -Algorithm SHA256).Hash -or
    -not (Test-Path -LiteralPath $runtimeInstallerPath -PathType Leaf) -or
    $runtimeTestInstallerHash -ne
        (Get-FileHash -LiteralPath $runtimeInstallerPath -Algorithm SHA256).Hash -or
    $runtimeTestInstallerHash -ne [string]$runtimeTestBuild.installerSha256 -or
    $runtimeInstalledPayloadSeries -ne "R23.1" -or
    $runtimePayload.Count -ne 1 -or
    $runtimeInstalledPayloadHash -ne [string]$runtimePayload[0].stagedSha256 -or
    $runtimeInstalledPayloadHash -ne [string]$receiptPayload[0].sourceSha256 -or
    $runtimeInstalledPayloadFileCount -ne "6" -or
    $runtimePayloadFiles.Count -ne 6 -or
    $runtimeInstalledPayloadFilesHash -ne $expectedRuntimePayloadFilesHash -or
    $runtimeInstalledPayloadFilesHash -ne $expectedPayloadFilesHash) {
    throw "Installer runtime verification is not bound to this final release candidate."
}
$verificationLogs = [ordered]@{
    pureCodeRegression = $pureCodeLog.Evidence
    installerArtifacts = $installerArtifactLog.Evidence
    installerRuntime = $installerRuntimeLog.Evidence
    installerRuntimeBuildResult = Get-FileEvidence `
        (Get-Item -LiteralPath $runtimeTestBuildResultPath)
    publishReceipt = Get-FileEvidence (Get-Item -LiteralPath $publishReceiptPath)
}

$criticalInputs = @(
    [pscustomobject]@{ Path = "scripts\build.ps1"; Hash = [string]$build.buildScriptSha256 },
    [pscustomobject]@{ Path = "scripts\package-installer.ps1"; Hash = [string]$build.packagerSha256 },
    [pscustomobject]@{ Path = "scripts\installer\New-MultiVersionBundle.ps1"; Hash = [string]$build.bundleBuilderSha256 },
    [pscustomobject]@{ Path = "bundle\manifest-template\PackageContents.base.xml"; Hash = [string]$build.manifestTemplateSha256 },
    [pscustomobject]@{ Path = "bundle\manifest-template\compatibility-matrix.json"; Hash = [string]$build.compatibilityMatrixSha256 },
    [pscustomobject]@{ Path = "scripts\installer\release-v$version3.json"; Hash = [string]$build.releaseDefinitionSha256 },
    [pscustomobject]@{ Path = "scripts\installer\SijiPVToolbox.iss"; Hash = [string]$build.installerSourceSha256 },
    [pscustomobject]@{ Path = "docs\安装与兼容性说明_v$version3.md"; Hash = [string]$build.installGuideSha256 },
    [pscustomobject]@{ Path = "bundle\ZhStructureToolbox.bundle\PackageContents.xml"; Hash = [string]$build.sourceManifestSha256 }
)
$criticalInputEvidence = @()
foreach ($input in $criticalInputs) {
    $path = Resolve-ProjectPath $input.Path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Critical build input is missing: $path"
    }
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actualHash -ne $input.Hash) {
        throw "Critical build input changed after packaging: $($input.Path)"
    }
    $criticalInputEvidence += Get-FileEvidence (Get-Item -LiteralPath $path)
}

$sourceFiles = @()
foreach ($relativeRoot in @("src", "tests", "scripts", "bundle\manifest-template")) {
    $absoluteRoot = Join-Path $projectRoot $relativeRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
        continue
    }
    $sourceFiles += Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File |
        Where-Object {
            $_.FullName -notmatch "[\\/](bin|obj)[\\/]" -and
            $_.Extension -notin @(".exe", ".dll", ".pdb")
        }
}
foreach ($relativePath in @(
    "README.md",
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml",
    "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.dll",
    "docs\安装与兼容性说明_v$version3.md",
    "docs\安装器实施记录_v$version3.md",
    "docs\安装器验证记录_v$version3.md"
)) {
    $absolutePath = Join-Path $projectRoot $relativePath
    if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
        $sourceFiles += Get-Item -LiteralPath $absolutePath
    }
}
$sourceFiles = @($sourceFiles |
    Sort-Object FullName -Unique |
    ForEach-Object { Get-FileEvidence $_ })

$deliverableFiles = @(Get-ChildItem -LiteralPath $releaseRoot -Recurse -File |
    Sort-Object FullName |
    ForEach-Object { Get-FileEvidence $_ })

$evidence = [ordered]@{
    schemaVersion = 5
    application = "四季光伏工具箱"
    brand = "FourJTooLs"
    releaseVersion = $version3
    releaseKind = $releaseKind
    releaseClassification = [string]$releaseDefinition.currentReleaseClassification
    authenticodeExpectation = [string]$releaseDefinition.authenticodeExpectation
    publicDistributionAllowed = [bool]$releaseDefinition.publicDistributionAllowed
    signed = $releaseIsSigned
    signatureClassification = [string]$build.signatureClassification
    allowedSignerThumbprints = @($buildSignerThumbprints)
    installerSignature =
        Get-PublicSignatureMetadata $build.installerSignature
    payloadSignScript = $payloadSignScriptEvidence
    verifiedSeries = @($build.verifiedSeries)
    generatedLocal = [DateTime]::Now.ToString("yyyy-MM-ddTHH:mm:ssK")
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    releaseDirectory = Get-RelativeProjectPath $releaseRoot
    buildResult = Get-FileEvidence (Get-Item -LiteralPath $buildResultPath)
    installer = Get-FileEvidence $releaseInstaller
    bundleInventory = Get-FileEvidence (Get-Item -LiteralPath $inventoryPath)
    sdkBuildProvenance = Get-FileEvidence `
        (Get-Item -LiteralPath $buildProvenancePath)
    payloadModules = $payloadEvidence
    payloadFiles = $payloadFileEvidence
    runtimeVerifications = $runtimeVerificationEvidence
    realDrawingVerification = $realDrawingVerificationEvidence
    regressionSuiteSha256 = $currentRegressionSuiteHash
    verificationLogs = $verificationLogs
    criticalBuildInputs = $criticalInputEvidence
    sourceInventory = $sourceFiles
    deliverableInventory = $deliverableFiles
}
$evidenceJson = $evidence | ConvertTo-Json -Depth 10
if ([regex]::IsMatch($evidenceJson, '(?i)[a-z]:\\')) {
    throw "Public release evidence contains a local absolute Windows path."
}
New-Item -ItemType Directory -Path $evidenceBaseRoot -Force | Out-Null
$runLeaf = (Get-Date -Format "yyyyMMdd-HHmmss-fff") + "-" +
    [guid]::NewGuid().ToString("N").Substring(0, 8)
$evidenceRoot = Join-Path $evidenceBaseRoot $runLeaf
$stageRoot = Join-Path $evidenceBaseRoot (".staging-" + [guid]::NewGuid().ToString("N"))
$stagePrefix = $evidenceBaseRoot.TrimEnd("\") + "\.staging-"
if (Test-Path -LiteralPath $evidenceRoot) {
    throw "Release evidence run already exists and will not be overwritten: $evidenceRoot"
}
try {
    New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
    $stagedEvidencePath = Join-Path $stageRoot $evidenceName
    $stagedChecksumPath = Join-Path $stageRoot $checksumName
    [IO.File]::WriteAllText($stagedEvidencePath, $evidenceJson, $utf8NoBom)
    $stagedEvidence = Get-Content -LiteralPath $stagedEvidencePath `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$stagedEvidence.schemaVersion -ne 5 -or
        [string]$stagedEvidence.releaseVersion -ne $version3 -or
        [string]$stagedEvidence.releaseClassification -ne
            [string]$releaseDefinition.currentReleaseClassification) {
        throw "Staged release evidence identity is invalid."
    }
    $evidenceHash = (Get-FileHash -LiteralPath $stagedEvidencePath `
        -Algorithm SHA256).Hash
    [IO.File]::WriteAllText($stagedChecksumPath,
        ("{0}  {1}{2}" -f $evidenceHash, $evidenceName,
            [Environment]::NewLine), $utf8NoBom)
    Move-Item -LiteralPath $stageRoot -Destination $evidenceRoot
} catch {
    if (Test-Path -LiteralPath $stageRoot -PathType Container) {
        $resolvedStage = [IO.Path]::GetFullPath($stageRoot)
        if ($resolvedStage.StartsWith($stagePrefix,
                [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedStage -Recurse -Force
        }
    }
    throw
}

Write-Output (Join-Path $evidenceRoot $evidenceName)
