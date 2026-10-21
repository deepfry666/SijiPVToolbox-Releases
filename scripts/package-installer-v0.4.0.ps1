[CmdletBinding()]
param(
    [switch]$TestCurrentPayload,

    [string]$OutputDirectory,

    [string]$IsccPath,

    [string]$SignToolName,

    [string]$SignToolCommand,

    [string]$PayloadSignScript,

    [Alias("AllowUnsignedDevelopmentBuild")]
    [switch]$AllowUnsignedDistributionBuild,

    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",

    [string]$FrozenBuildResult
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = Split-Path -Parent $PSScriptRoot
$installerRoot = Join-Path $PSScriptRoot "installer"
$releasePath = Join-Path $installerRoot "release-v0.4.0.json"
$matrixPath = Join-Path $projectRoot `
    "bundle\manifest-template\compatibility-matrix.json"
$guidePath = Join-Path $projectRoot "docs\安装与兼容性说明_v0.4.0.md"
$issPath = Join-Path $installerRoot "SijiPVToolbox.iss"
$buildScriptPath = Join-Path $PSScriptRoot "build.ps1"
$newBundleScript = Join-Path $installerRoot "New-MultiVersionBundle.ps1"
$manifestTemplatePath = Join-Path $projectRoot `
    "bundle\manifest-template\PackageContents.base.xml"
$sourceManifestPath = Join-Path $projectRoot `
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml"

function Resolve-Iscc([string]$RequestedPath) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = [IO.Path]::GetFullPath($RequestedPath)
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "ISCC.exe was not found: $resolved"
        }
        return $resolved
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }
    throw "Inno Setup 6 compiler was not found. Install it with: " +
        "winget install --id JRSoftware.InnoSetup --exact"
}

function Get-Version3([version]$Version) {
    return "{0}.{1}.{2}" -f $Version.Major, $Version.Minor,
        [Math]::Max(0, $Version.Build)
}

function Get-Version4([version]$Version) {
    return "{0}.{1}.{2}.{3}" -f $Version.Major, $Version.Minor,
        [Math]::Max(0, $Version.Build), [Math]::Max(0, $Version.Revision)
}

function Remove-GuidBraces([string]$Value) {
    return ([guid]$Value).ToString("D").ToUpperInvariant()
}

function Normalize-CertificateThumbprint([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Certificate thumbprints must not be empty."
    }
    $normalized = ($Value -replace "\s", "").ToUpperInvariant()
    if ($normalized -notmatch "^[0-9A-F]{40}$") {
        throw "Certificate thumbprint must contain exactly 40 hexadecimal characters: $Value"
    }
    return $normalized
}

function Get-CertificateMetadata(
    [Security.Cryptography.X509Certificates.X509Certificate2]$Certificate) {
    if ($null -eq $Certificate) {
        return $null
    }
    return [ordered]@{
        subject = $Certificate.Subject
        issuer = $Certificate.Issuer
        thumbprint = Normalize-CertificateThumbprint $Certificate.Thumbprint
        serialNumber = $Certificate.SerialNumber
        notBefore = $Certificate.NotBefore.ToUniversalTime().ToString("o")
        notAfter = $Certificate.NotAfter.ToUniversalTime().ToString("o")
    }
}

function Get-AuthenticodeMetadata([string]$FilePath) {
    $signature = Get-AuthenticodeSignature -LiteralPath $FilePath
    $classification = if ($signature.Status -eq
        [Management.Automation.SignatureStatus]::Valid) {
        "Signed"
    } elseif ($signature.Status -eq
        [Management.Automation.SignatureStatus]::NotSigned) {
        "NotSigned"
    } else {
        "Invalid"
    }
    return [ordered]@{
        classification = $classification
        status = $signature.Status.ToString()
        statusMessage = $signature.StatusMessage
        signer = Get-CertificateMetadata $signature.SignerCertificate
        timestampSigner = Get-CertificateMetadata $signature.TimeStamperCertificate
    }
}

function Assert-AllowedAuthenticodeSignature(
    [string]$FilePath,
    [string]$Description,
    [string[]]$AllowedThumbprints) {
    $signature = Get-AuthenticodeSignature -LiteralPath $FilePath
    if ($signature.Status -ne
        [Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate) {
        throw "$Description does not have a valid Authenticode signature: $FilePath"
    }
    if ($null -eq $signature.TimeStamperCertificate) {
        throw "$Description does not have a trusted Authenticode timestamp: $FilePath"
    }
    $thumbprint = Normalize-CertificateThumbprint `
        $signature.SignerCertificate.Thumbprint
    if ($AllowedThumbprints -notcontains $thumbprint) {
        throw "$Description was signed by an unapproved certificate " +
            "${thumbprint}: $FilePath"
    }
    return [ordered]@{
        classification = "SignedAllowed"
        status = $signature.Status.ToString()
        statusMessage = $signature.StatusMessage
        signer = Get-CertificateMetadata $signature.SignerCertificate
        timestampSigner = Get-CertificateMetadata $signature.TimeStamperCertificate
    }
}

function Assert-NotSignedAuthenticodeSignature(
    [string]$FilePath,
    [string]$Description) {
    $signature = Get-AuthenticodeSignature -LiteralPath $FilePath
    if ($signature.Status -ne
        [Management.Automation.SignatureStatus]::NotSigned -or
        $null -ne $signature.SignerCertificate) {
        throw "$Description must have Authenticode status NotSigned; " +
            "actual status is $($signature.Status): $FilePath"
    }
    return [ordered]@{
        classification = "NotSigned"
        status = $signature.Status.ToString()
        statusMessage = $signature.StatusMessage
        signer = $null
        timestampSigner = $null
    }
}

function Assert-FileSha256(
    [string]$FilePath,
    [string]$ExpectedSha256,
    [string]$Description) {
    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
        throw "$Description is missing: $FilePath"
    }
    $actual = (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash
    if ($actual -ne $ExpectedSha256) {
        throw "$Description changed during installer packaging: $FilePath"
    }
}

function Get-SourceCodeFingerprint {
    $sourceRoots = @(
        (Join-Path $projectRoot "src\StructuralToolbox"),
        (Join-Path $projectRoot "src\StructuralToolbox.Contracts"),
        (Join-Path $projectRoot "src\StructuralToolbox.Rail"),
        (Join-Path $projectRoot "src\StructuralToolbox.Ballast")
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

function Get-CriticalInputHashes {
    $hashes = [ordered]@{}
    $paths = [ordered]@{
        buildScript = $buildScriptPath
        packager = $PSCommandPath
        bundleBuilder = $newBundleScript
        manifestTemplate = $manifestTemplatePath
        compatibilityMatrix = $matrixPath
        releaseDefinition = $releasePath
        installerSource = $issPath
        installGuide = $guidePath
    }
    foreach ($entry in $paths.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
            throw "Critical build input is missing: $($entry.Value)"
        }
        $hashes[$entry.Key] = (Get-FileHash -LiteralPath $entry.Value `
            -Algorithm SHA256).Hash
    }
    return $hashes
}

function Assert-CriticalInputHashesUnchanged($Expected) {
    $actual = Get-CriticalInputHashes
    foreach ($entry in $Expected.GetEnumerator()) {
        if ([string]$actual[$entry.Key] -ne [string]$entry.Value) {
            throw "Critical build input changed during packaging: $($entry.Key)"
        }
    }
}

$release = Get-Content -LiteralPath $releasePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$matrix = Get-Content -LiteralPath $matrixPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
[xml]$sourceManifest = Get-Content -LiteralPath $sourceManifestPath -Raw -Encoding UTF8
$declaredBuildVersion = [version]([string]$release.assemblyVersion)
$allowedSignerThumbprints = @()
if ($release.PSObject.Properties.Name -contains "allowedSignerThumbprints") {
    $allowedSignerThumbprints = @($release.allowedSignerThumbprints |
        ForEach-Object { Normalize-CertificateThumbprint ([string]$_) } |
        Sort-Object -Unique)
}

$activeTargets = @($matrix.targets | Where-Object { $_.status -eq "verified" })
if ($activeTargets.Count -ne 1) {
    throw "The current release pipeline requires exactly one verified target; " +
        "found $($activeTargets.Count). Add a separate build profile before " +
        "publishing more than one AutoCAD Series."
}
$buildTarget = $activeTargets[0]
if ([string]::IsNullOrWhiteSpace([string]$buildTarget.sourceModule) -or
    [string]::IsNullOrWhiteSpace([string]$buildTarget.buildProfile) -or
    [string]::IsNullOrWhiteSpace([string]$buildTarget.managedApiVersion)) {
    throw "The verified target lacks sourceModule/buildProfile/managedApiVersion provenance."
}
if ([string]$buildTarget.runtimeFamily -ne "legacy-netfx") {
    throw "The current package pipeline cannot build runtime family " +
        "$($buildTarget.runtimeFamily)."
}

if ($TestCurrentPayload -and $AllowUnsignedDistributionBuild) {
    throw "TestCurrentPayload and AllowUnsignedDistributionBuild are mutually exclusive."
}
if (-not [string]::IsNullOrWhiteSpace($FrozenBuildResult) -and
    -not $TestCurrentPayload) {
    throw "FrozenBuildResult is accepted only for the isolated installer runtime test."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $kind = if ($TestCurrentPayload) { "test" } else { "release-candidate" }
    $OutputDirectory = Join-Path $projectRoot `
        ("work\installer\{0}-v{1}" -f $kind,
            (Get-Version3 $declaredBuildVersion))
}
$outputFullPath = [IO.Path]::GetFullPath($OutputDirectory)
$installerWorkRoot = [IO.Path]::GetFullPath(
    (Join-Path $projectRoot "work\installer")).TrimEnd("\") + "\"
if (-not $outputFullPath.StartsWith($installerWorkRoot,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer builds must stay under work\installer: $outputFullPath"
}

$frozenInputPath = $null
if (-not [string]::IsNullOrWhiteSpace($FrozenBuildResult)) {
    $frozenInputPath = [IO.Path]::GetFullPath($FrozenBuildResult)
    $outputPrefix = $outputFullPath.TrimEnd("\") + "\"
    if (-not $frozenInputPath.StartsWith($installerWorkRoot,
            [StringComparison]::OrdinalIgnoreCase) -or
        $frozenInputPath.StartsWith($outputPrefix,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $frozenInputPath -PathType Leaf)) {
        throw "FrozenBuildResult must be an existing file under a different " +
            "work\installer build directory: $frozenInputPath"
    }
}

# A failed rebuild must never leave stale success markers or stale EXEs.
if (Test-Path -LiteralPath $outputFullPath -PathType Container) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}

# Release candidates start from a fresh SDK-validated build. The isolated
# installer may reuse a frozen candidate so its installed payload is byte-identical.
$criticalInputHashesBefore = Get-CriticalInputHashes
$sourceManifestHashBefore = (Get-FileHash -LiteralPath $sourceManifestPath `
    -Algorithm SHA256).Hash
$sourceCodeHashBeforeBuild = Get-SourceCodeFingerprint
$sdkBuildMetadata = $null
$payloadOrigin = $null
if ([string]::IsNullOrWhiteSpace($FrozenBuildResult)) {
    $buildOutput = @(& $buildScriptPath -AutoCADDir $AutoCADDir `
        -TargetSeries ([string]$buildTarget.series) `
        -ExpectedAutoCADYear ([int]$buildTarget.year) `
        -BuildProfile ([string]$buildTarget.buildProfile) -PassThru)
    $sdkRecords = @($buildOutput | Where-Object {
        $null -ne $_ -and $_.PSObject.Properties.Name -contains "validation"
    })
    foreach ($message in @($buildOutput | Where-Object {
        $null -eq $_ -or -not ($_.PSObject.Properties.Name -contains "validation")
    })) {
        if ($null -ne $message) { Write-Host $message }
    }
    if ($sdkRecords.Count -ne 1 -or
        [string]$sdkRecords[0].validation -ne "passed") {
        throw "The SDK-validated build did not return exactly one provenance record."
    }
    $sdkBuildMetadata = $sdkRecords[0]
    $payloadOrigin = [ordered]@{
        mode = "fresh-sdk-build"
        referenceBuildResult = $null
        referenceBuildResultSha256 = $null
        referenceInstallerSha256 = $null
    }
} else {
    $frozenPath = $frozenInputPath
    $frozen = Get-Content -LiteralPath $frozenPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    if ([int]$frozen.schemaVersion -lt 6 -or [bool]$frozen.testBuild -or
        -not [bool]$frozen.unsignedDistributionBuild -or
        [string]$frozen.releaseKind -ne "unsigned-distribution" -or
        [version]([string]$frozen.appVersion) -ne $declaredBuildVersion) {
        throw "FrozenBuildResult is not a schema-6 unsigned candidate for this version."
    }
    if ([string]$frozen.sourceCodeSha256 -ne $sourceCodeHashBeforeBuild -or
        [string]$frozen.sourceManifestSha256 -ne $sourceManifestHashBefore) {
        throw "Source code or source manifest differs from the frozen release candidate."
    }
    $frozenInputProperties = [ordered]@{
        buildScript = "buildScriptSha256"
        packager = "packagerSha256"
        bundleBuilder = "bundleBuilderSha256"
        manifestTemplate = "manifestTemplateSha256"
        compatibilityMatrix = "compatibilityMatrixSha256"
        releaseDefinition = "releaseDefinitionSha256"
        installerSource = "installerSourceSha256"
        installGuide = "installGuideSha256"
    }
    foreach ($entry in $frozenInputProperties.GetEnumerator()) {
        if ([string]$criticalInputHashesBefore[$entry.Key] -ne
            [string]$frozen.($entry.Value)) {
            throw "Critical input differs from FrozenBuildResult: $($entry.Key)"
        }
    }
    foreach ($target in $activeTargets) {
        $sourceModulePath = [IO.Path]::GetFullPath((Join-Path $projectRoot `
            ([string]$target.sourceModule -replace "/", "\")))
        $frozenPayload = @($frozen.payloadModules | Where-Object {
            [string]$_.series -eq [string]$target.series
        })
        if ($frozenPayload.Count -ne 1 -or
            (Get-FileHash -LiteralPath $sourceModulePath -Algorithm SHA256).Hash -ne
                [string]$frozenPayload[0].sourceSha256) {
            throw "Current payload is not byte-identical to FrozenBuildResult for " +
                "$($target.series)."
        }
        $frozenFiles = @($frozen.payloadFiles | Where-Object {
            [string]$_.series -eq [string]$target.series
        })
        foreach ($module in @($frozen.sdkValidation.modules)) {
            $modulePath = [IO.Path]::GetFullPath([string]$module.path)
            $frozenFile = @($frozenFiles | Where-Object {
                [string]$_.name -eq [string]$module.name
            })
            if ($frozenFile.Count -ne 1 -or
                (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash -ne
                    [string]$frozenFile[0].sourceSha256) {
                throw "Current module is not byte-identical to FrozenBuildResult: " +
                    [string]$module.name
            }
        }
    }
    $sdkBuildMetadata = $frozen.sdkValidation
    if ($null -eq $sdkBuildMetadata -or
        [string]$sdkBuildMetadata.validation -ne "passed") {
        throw "FrozenBuildResult lacks passed SDK provenance."
    }
    $payloadOrigin = [ordered]@{
        mode = "frozen-release-candidate"
        referenceBuildResult = $frozenPath
        referenceBuildResultSha256 = (Get-FileHash -LiteralPath $frozenPath `
            -Algorithm SHA256).Hash
        referenceInstallerSha256 = [string]$frozen.installerSha256
    }
}
$sourceCodeHashAfterBuild = Get-SourceCodeFingerprint
if ($sourceCodeHashAfterBuild -ne $sourceCodeHashBeforeBuild) {
    throw "Source code changed during the release build. Retry from a stable tree."
}
Assert-CriticalInputHashesUnchanged $criticalInputHashesBefore

$currentModule = Join-Path $projectRoot `
    ([string]$buildTarget.sourceModule -replace "/", "\")
$buildVersion = [Reflection.AssemblyName]::GetAssemblyName($currentModule).Version
if ($buildVersion -ne $declaredBuildVersion) {
    throw "Payload version $buildVersion does not match release definition " +
        "$declaredBuildVersion."
}
if ([string]$sdkBuildMetadata.targetSeries -ne [string]$buildTarget.series -or
    [int]$sdkBuildMetadata.targetYear -ne [int]$buildTarget.year -or
    [string]$sdkBuildMetadata.buildProfile -ne [string]$buildTarget.buildProfile -or
    [string]$sdkBuildMetadata.runtimeFamily -ne
        [string]$buildTarget.runtimeFamily) {
    throw "SDK provenance does not match the verified compatibility target."
}
$expectedApiVersion = [version]([string]$buildTarget.managedApiVersion + ".0.0")
$coreSdkFiles = @($sdkBuildMetadata.sdkFiles | Where-Object {
    $_.name -in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")
})
if ($coreSdkFiles.Count -ne 3) {
    throw "SDK provenance must contain the three core AutoCAD managed APIs."
}
foreach ($sdkFile in $coreSdkFiles) {
    $actualApiVersion = [version]([string]$sdkFile.assemblyVersion)
    if ($actualApiVersion.Major -ne $expectedApiVersion.Major -or
        $actualApiVersion.Minor -ne $expectedApiVersion.Minor -or
        [string]$sdkFile.signatureStatus -ne "Valid" -or
        -not ([string]$sdkFile.signerSubject).Contains("Autodesk")) {
        throw "SDK provenance for $($sdkFile.name) is not valid for " +
            "$($buildTarget.series)."
    }
}
if ([string]$sdkBuildMetadata.outputSha256 -ne
    (Get-FileHash -LiteralPath $currentModule -Algorithm SHA256).Hash) {
    throw "SDK build output hash does not match the current payload."
}

if ($TestCurrentPayload) {
    $productCode = [string]$release.testBundleProductCode
    $upgradeCode = [string]$release.testBundleUpgradeCode
    $installerAppId = [string]$release.testInstallerAppId
    $legacyProductCodes = ""
    $bundleDirectoryName = "ZhStructureToolbox.Test.bundle"
    $displayName = "四季光伏工具箱（安装器测试）"
    $outputSuffix = "_INSTALLER_TEST"
    $testBuildValue = "1"
    $unsignedDistributionBuildValue = "0"
} else {
    $productCode = [string]$release.bundleProductCode
    $upgradeCode = [string]$release.bundleUpgradeCode
    $installerAppId = [string]$release.installerAppId
    $legacyProductCodes = @($release.acceptedLegacyProductCodes) -join ";"
    $bundleDirectoryName = [string]$release.bundleDirectoryName
    if ($AllowUnsignedDistributionBuild) {
        $displayName = [string]$release.applicationName
        $outputSuffix = ""
        $unsignedDistributionBuildValue = "1"
    } else {
        $displayName = [string]$release.applicationName
        $outputSuffix = ""
        $unsignedDistributionBuildValue = "0"
    }
    $testBuildValue = "0"
}

$guideText = Get-Content -LiteralPath $guidePath -Raw -Encoding UTF8
$aboutSourceText = Get-Content -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox\Commands\ToolboxCommands.cs") -Raw -Encoding UTF8
if ($AllowUnsignedDistributionBuild) {
    if ([string]$release.currentReleaseClassification -ne "UNSIGNED" -or
        [string]$release.authenticodeExpectation -ne "NotSigned" -or
        -not [bool]$release.publicDistributionAllowed) {
        throw "Unsigned distribution requires UNSIGNED, NotSigned, and publicDistributionAllowed=true."
    }
    if (-not $guideText.Contains("未知发布者") -or
        $aboutSourceText.Contains("TEST_UNSIGNED") -or
        $aboutSourceText.Contains("未签名测试版")) {
        throw "The unsigned guide or user-visible About text violates the release boundary."
    }
} elseif (-not $TestCurrentPayload) {
    if ([string]$release.currentReleaseClassification -ne "SIGNED_RELEASE" -or
        -not [bool]$release.publicDistributionAllowed) {
        throw "A formal build requires SIGNED_RELEASE/publicDistributionAllowed=true."
    }
    foreach ($forbiddenNotice in @("TEST_UNSIGNED", "未签名测试版")) {
        if ($guideText.Contains($forbiddenNotice) -or
            $aboutSourceText.Contains($forbiddenNotice)) {
            throw "A signed release cannot embed test-release wording: $forbiddenNotice"
        }
    }
}

if (-not $TestCurrentPayload -and -not $AllowUnsignedDistributionBuild) {
    if ([string]::IsNullOrWhiteSpace($SignToolName) -or
        [string]::IsNullOrWhiteSpace($SignToolCommand) -or
        [string]::IsNullOrWhiteSpace($PayloadSignScript)) {
        throw "A formal installer requires SignToolName, SignToolCommand, and " +
            "PayloadSignScript. " +
            "Use -AllowUnsignedDistributionBuild when publishing without a certificate."
    }
    if ($allowedSignerThumbprints.Count -eq 0) {
        throw "A formal installer requires at least one allowedSignerThumbprints " +
            "entry in the release definition."
    }
}
if ([string]::IsNullOrWhiteSpace($SignToolName) -xor
    [string]::IsNullOrWhiteSpace($SignToolCommand)) {
    throw "SignToolName and SignToolCommand must be supplied together."
}
$payloadSignScriptPath = $null
$payloadSignScriptSha256 = $null
if (-not [string]::IsNullOrWhiteSpace($PayloadSignScript)) {
    if ($TestCurrentPayload -or $AllowUnsignedDistributionBuild) {
        throw "PayloadSignScript is accepted only for a formal signed release."
    }
    $payloadSignScriptPath = [IO.Path]::GetFullPath($PayloadSignScript)
    if (-not (Test-Path -LiteralPath $payloadSignScriptPath -PathType Leaf)) {
        throw "Payload signing script was not found: $payloadSignScriptPath"
    }
    $payloadSignScriptSha256 = (Get-FileHash -LiteralPath `
        $payloadSignScriptPath -Algorithm SHA256).Hash
}

if (-not $TestCurrentPayload -and -not $AllowUnsignedDistributionBuild) {
    foreach ($module in @($sdkBuildMetadata.modules)) {
        $sourceModulePath = [IO.Path]::GetFullPath([string]$module.path)
        Assert-FileSha256 $payloadSignScriptPath $payloadSignScriptSha256 `
            "Payload signing script"
        & $payloadSignScriptPath -FilePath $sourceModulePath
        Assert-FileSha256 $payloadSignScriptPath $payloadSignScriptSha256 `
            "Payload signing script"
        $null = Assert-AllowedAuthenticodeSignature $sourceModulePath `
            "Formal source payload $($module.name)" `
            $allowedSignerThumbprints
    }
}

New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null
$stagingRoot = Join-Path $outputFullPath "staging"
$payloadBundle = Join-Path $stagingRoot $bundleDirectoryName
$buildProvenancePath = Join-Path $outputFullPath "sdk-build-provenance.json"
$buildProvenance = [ordered]@{
    schemaVersion = 1
    appVersion = $buildVersion.ToString(4)
    sourceCodeSha256 = $sourceCodeHashAfterBuild
    sdkValidation = $sdkBuildMetadata
    payloadOrigin = $payloadOrigin
    targets = @($activeTargets | ForEach-Object {
        $sourceModulePath = [IO.Path]::GetFullPath((Join-Path $projectRoot `
            ([string]$_.sourceModule -replace "/", "\")))
        $moduleFiles = @($sdkBuildMetadata.modules | ForEach-Object {
            $modulePath = [IO.Path]::GetFullPath([string]$_.path)
            [ordered]@{
                name = [string]$_.name
                sourcePath = $modulePath.Substring($projectRoot.Length).TrimStart(
                    "\").Replace("\", "/")
                sourceSha256 = (Get-FileHash -LiteralPath $modulePath `
                    -Algorithm SHA256).Hash
            }
        })
        [ordered]@{
            year = [int]$_.year
            series = [string]$_.series
            runtimeFamily = [string]$_.runtimeFamily
            sourcePath = ([string]$_.sourceModule).Replace("\", "/")
            sourceSha256 = (Get-FileHash -LiteralPath $sourceModulePath `
                -Algorithm SHA256).Hash
            compiledOutputSha256 = [string]$sdkBuildMetadata.outputSha256
            files = $moduleFiles
        }
    })
    generatedUtc = [DateTime]::UtcNow.ToString("o")
}
$buildProvenance | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $buildProvenancePath -Encoding UTF8
$buildProvenanceSha256 = (Get-FileHash -LiteralPath $buildProvenancePath `
    -Algorithm SHA256).Hash

& $newBundleScript -Version $buildVersion -ProductCode $productCode `
    -UpgradeCode $upgradeCode -Publisher ([string]$release.publisher) `
    -OutputBundle $payloadBundle -BuildProvenance $buildProvenancePath

$inventoryPath = Join-Path $stagingRoot "bundle-inventory.json"
if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) {
    throw "Bundle inventory was not generated: $inventoryPath"
}
$inventory = Get-Content -LiteralPath $inventoryPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([int]$inventory.schemaVersion -lt 3 -or
    [string]$inventory.buildProvenanceSha256 -ne $buildProvenanceSha256) {
    throw "Bundle inventory does not retain the SDK build provenance hash."
}

foreach ($target in $activeTargets) {
    $inventoryTarget = @($inventory.targets |
        Where-Object { [string]$_.series -eq [string]$target.series })
    if ($inventoryTarget.Count -ne 1) {
        throw "Bundle inventory must contain exactly one payload for $($target.series)."
    }
    $sourceModulePath = [IO.Path]::GetFullPath((Join-Path $projectRoot `
        ([string]$target.sourceModule -replace "/", "\")))
    $modulePath = Join-Path $payloadBundle `
        ([string]$target.modulePath -replace "/", "\")
    $sourceModuleHash = (Get-FileHash -LiteralPath $sourceModulePath `
        -Algorithm SHA256).Hash
    $stagedModuleHash = (Get-FileHash -LiteralPath $modulePath `
        -Algorithm SHA256).Hash
    if ($sourceModuleHash -ne [string]$inventoryTarget[0].sourceSha256 -or
        $stagedModuleHash -ne [string]$inventoryTarget[0].stagedSha256 -or
        $sourceModuleHash -ne $stagedModuleHash) {
        throw "Payload hash chain failed for $($target.series): " +
            "source=$sourceModuleHash, staged=$stagedModuleHash"
    }
    if (-not $TestCurrentPayload -and -not $AllowUnsignedDistributionBuild) {
        $null = Assert-AllowedAuthenticodeSignature $modulePath `
            "Formal staged payload for $($target.series)" `
            $allowedSignerThumbprints
    } elseif ($AllowUnsignedDistributionBuild) {
        $null = Assert-NotSignedAuthenticodeSignature $sourceModulePath `
            "UNSIGNED source payload for $($target.series)"
        $null = Assert-NotSignedAuthenticodeSignature $modulePath `
            "UNSIGNED staged payload for $($target.series)"
    }
    foreach ($file in @($inventoryTarget[0].files)) {
        $sourceFile = [IO.Path]::GetFullPath((Join-Path $projectRoot `
            ([string]$file.sourcePath -replace "/", "\")))
        $stagedFile = Join-Path $payloadBundle `
            ([string]$file.modulePath -replace "/", "\")
        $sourceFileHash = (Get-FileHash -LiteralPath $sourceFile `
            -Algorithm SHA256).Hash
        $stagedFileHash = (Get-FileHash -LiteralPath $stagedFile `
            -Algorithm SHA256).Hash
        if ($sourceFileHash -ne [string]$file.sourceSha256 -or
            $stagedFileHash -ne [string]$file.stagedSha256 -or
            $sourceFileHash -ne $stagedFileHash) {
            throw "Module hash chain failed: $($file.name)"
        }
        if (-not $TestCurrentPayload -and -not $AllowUnsignedDistributionBuild) {
            $null = Assert-AllowedAuthenticodeSignature $stagedFile `
                "Formal staged module $($file.name)" $allowedSignerThumbprints
        } elseif ($AllowUnsignedDistributionBuild) {
            $null = Assert-NotSignedAuthenticodeSignature $sourceFile `
                "UNSIGNED source module $($file.name)"
            $null = Assert-NotSignedAuthenticodeSignature $stagedFile `
                "UNSIGNED staged module $($file.name)"
        }
    }
}

$verifiedSeries = @($matrix.targets |
    Where-Object { $_.status -eq "verified" } |
    ForEach-Object { $_.series }) -join ";"
$candidateLegacySeries = @($matrix.targets |
    Where-Object {
        $_.status -eq "candidate" -and $_.runtimeFamily -eq "legacy-netfx"
    } | ForEach-Object { $_.series }) -join ";"
$candidateNet8Series = @($matrix.targets |
    Where-Object {
        $_.status -eq "candidate" -and $_.runtimeFamily -eq "net8-windows"
    } | ForEach-Object { $_.series }) -join ";"

$iscc = Resolve-Iscc $IsccPath
$version3 = Get-Version3 $buildVersion
$version4 = Get-Version4 $buildVersion
$baseName = [string]$release.setupBaseName + "_v" + $version3 + $outputSuffix
$compilerLogPath = Join-Path $outputFullPath "iscc-build.log"

$arguments = @(
    "/Qp",
    ("/DAppVersion=" + $version3),
    ("/DAssemblyVersion=" + $version4),
    ("/DInstallerAppId=" + (Remove-GuidBraces $installerAppId)),
    ("/DProductCode=" + ([guid]$productCode).ToString("B").ToUpperInvariant()),
    ("/DUpgradeCode=" + ([guid]$upgradeCode).ToString("B").ToUpperInvariant()),
    ("/DLegacyProductCodes=" + $legacyProductCodes),
    ("/DPublisher=" + [string]$release.publisher),
    ("/DAppDisplayName=" + $displayName),
    ("/DPayloadBundleDir=" + $payloadBundle),
    ("/DVerifiedTargetLabel=AutoCAD {0} {1}" -f
        [int]$buildTarget.year, [string]$buildTarget.series),
    ("/DOutputDir=" + $outputFullPath),
    ("/DOutputBaseFilename=" + $baseName),
    ("/DVerifiedSeries=" + $verifiedSeries),
    ("/DCandidateLegacySeries=" + $candidateLegacySeries),
    ("/DCandidateNet8Series=" + $candidateNet8Series),
    ("/DTestBuild=" + $testBuildValue),
    ("/DUnsignedDistributionBuild=" + $unsignedDistributionBuildValue),
    ("/DBundleDirectoryName=" + $bundleDirectoryName),
    "/DPipelineInvocation=FOURJTOOLS_PACKAGE_PIPELINE_V1"
)
if (-not [string]::IsNullOrWhiteSpace($SignToolName)) {
    $arguments += "/DSignToolName=$SignToolName"
    $arguments += "/S$SignToolName=$SignToolCommand"
}
$arguments += $issPath

$compilerOutput = @(& $iscc $arguments 2>&1)
$compilerExitCode = $LASTEXITCODE
$compilerOutput | Set-Content -LiteralPath $compilerLogPath -Encoding UTF8
if ($compilerExitCode -ne 0) {
    throw "Inno Setup compilation failed with exit code $compilerExitCode. " +
        "See $compilerLogPath"
}

$installerPath = Join-Path $outputFullPath ($baseName + ".exe")
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Inno Setup reported success but the installer is missing: $installerPath"
}
$installerSignatureMetadata = Get-AuthenticodeMetadata $installerPath
if (-not $TestCurrentPayload -and -not $AllowUnsignedDistributionBuild) {
    $installerSignatureMetadata = Assert-AllowedAuthenticodeSignature `
        $installerPath "Formal installer" $allowedSignerThumbprints
} elseif ($AllowUnsignedDistributionBuild) {
    $installerSignatureMetadata = Assert-NotSignedAuthenticodeSignature `
        $installerPath "UNSIGNED installer"
}

$sourceManifestHashAfter = (Get-FileHash -LiteralPath $sourceManifestPath `
    -Algorithm SHA256).Hash
if ($sourceManifestHashAfter -ne $sourceManifestHashBefore) {
    throw "The source PackageContents.xml was modified during installer packaging."
}
$sourceCodeHashBeforeResult = Get-SourceCodeFingerprint
if ($sourceCodeHashBeforeResult -ne $sourceCodeHashAfterBuild) {
    throw "Source code changed during installer packaging. Rebuild before release."
}
Assert-CriticalInputHashesUnchanged $criticalInputHashesBefore

foreach ($target in $activeTargets) {
    $inventoryTarget = @($inventory.targets |
        Where-Object { [string]$_.series -eq [string]$target.series })
    $sourceModulePath = [IO.Path]::GetFullPath((Join-Path $projectRoot `
        ([string]$target.sourceModule -replace "/", "\")))
    $modulePath = Join-Path $payloadBundle `
        ([string]$target.modulePath -replace "/", "\")
    $sourceModuleHash = (Get-FileHash -LiteralPath $sourceModulePath `
        -Algorithm SHA256).Hash
    $stagedModuleHash = (Get-FileHash -LiteralPath $modulePath `
        -Algorithm SHA256).Hash
    if ($sourceModuleHash -ne [string]$inventoryTarget[0].sourceSha256 -or
        $stagedModuleHash -ne [string]$inventoryTarget[0].stagedSha256 -or
        $sourceModuleHash -ne $stagedModuleHash) {
        throw "Payload changed during installer compilation for $($target.series)."
    }
    foreach ($file in @($inventoryTarget[0].files)) {
        $sourceFile = [IO.Path]::GetFullPath((Join-Path $projectRoot `
            ([string]$file.sourcePath -replace "/", "\")))
        $stagedFile = Join-Path $payloadBundle `
            ([string]$file.modulePath -replace "/", "\")
        if ((Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash -ne
                [string]$file.sourceSha256 -or
            (Get-FileHash -LiteralPath $stagedFile -Algorithm SHA256).Hash -ne
                [string]$file.stagedSha256) {
            throw "Module changed during installer compilation: $($file.name)"
        }
    }
}

$releaseKind = if ($TestCurrentPayload) {
    "isolated-installer-test"
} elseif ($AllowUnsignedDistributionBuild) {
    "unsigned-distribution"
} else {
    "signed-release"
}
$payloadModules = @($inventory.targets | ForEach-Object {
    $sourceModulePath = [IO.Path]::GetFullPath((Join-Path $projectRoot `
        ([string]$_.sourcePath -replace "/", "\")))
    $stagedModulePath = Join-Path $payloadBundle `
        ([string]$_.modulePath -replace "/", "\")
    if (-not $TestCurrentPayload -and -not $AllowUnsignedDistributionBuild) {
        $sourceSignatureMetadata = Assert-AllowedAuthenticodeSignature `
            $sourceModulePath "Formal source payload for $($_.series)" `
            $allowedSignerThumbprints
        $stagedSignatureMetadata = Assert-AllowedAuthenticodeSignature `
            $stagedModulePath "Formal staged payload for $($_.series)" `
            $allowedSignerThumbprints
        $installerSignerThumbprint =
            [string]$installerSignatureMetadata.signer.thumbprint
        if ([string]$sourceSignatureMetadata.signer.thumbprint -ne
                $installerSignerThumbprint -or
            [string]$stagedSignatureMetadata.signer.thumbprint -ne
                $installerSignerThumbprint) {
            throw "Formal source, staged payload, and installer must use the " +
                "same signing certificate for $($_.series)."
        }
    } elseif ($AllowUnsignedDistributionBuild) {
        $sourceSignatureMetadata = Assert-NotSignedAuthenticodeSignature `
            $sourceModulePath "UNSIGNED source payload for $($_.series)"
        $stagedSignatureMetadata = Assert-NotSignedAuthenticodeSignature `
            $stagedModulePath "UNSIGNED staged payload for $($_.series)"
    } else {
        $sourceSignatureMetadata = Get-AuthenticodeMetadata $sourceModulePath
        $stagedSignatureMetadata = Get-AuthenticodeMetadata $stagedModulePath
    }
    [ordered]@{
        year = [int]$_.year
        series = [string]$_.series
        runtimeFamily = [string]$_.runtimeFamily
        sourcePath = [string]$_.sourcePath
        sourceSha256 = [string]$_.sourceSha256
        sourceSignature = $sourceSignatureMetadata
        stagedPath = [string]$_.modulePath
        stagedSha256 = [string]$_.stagedSha256
        stagedSignature = $stagedSignatureMetadata
    }
})
$payloadFiles = @($inventory.targets | ForEach-Object {
    $target = $_
    @($target.files | ForEach-Object {
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $projectRoot `
            ([string]$_.sourcePath -replace "/", "\")))
        $stagedPath = Join-Path $payloadBundle `
            ([string]$_.modulePath -replace "/", "\")
        [ordered]@{
            year = [int]$target.year
            series = [string]$target.series
            runtimeFamily = [string]$target.runtimeFamily
            name = [string]$_.name
            sourcePath = [string]$_.sourcePath
            sourceSha256 = [string]$_.sourceSha256
            sourceSignature = Get-AuthenticodeMetadata $sourcePath
            stagedPath = [string]$_.modulePath
            stagedSha256 = [string]$_.stagedSha256
            stagedSignature = Get-AuthenticodeMetadata $stagedPath
        }
    })
})
$result = [ordered]@{
    schemaVersion = 6
    testBuild = [bool]$TestCurrentPayload
    unsignedDistributionBuild = [bool]$AllowUnsignedDistributionBuild
    releaseKind = $releaseKind
    releaseClassification = [string]$release.currentReleaseClassification
    authenticodeExpectation = [string]$release.authenticodeExpectation
    publicDistributionAllowed = [bool]$release.publicDistributionAllowed
    appVersion = $version4
    installer = $installerPath
    installerSha256 = (Get-FileHash -LiteralPath $installerPath `
        -Algorithm SHA256).Hash
    bundle = $payloadBundle
    bundleManifestSha256 = (Get-FileHash -LiteralPath `
        (Join-Path $payloadBundle "PackageContents.xml") -Algorithm SHA256).Hash
    bundleInventory = $inventoryPath
    bundleInventorySha256 = (Get-FileHash -LiteralPath $inventoryPath `
        -Algorithm SHA256).Hash
    buildProvenance = $buildProvenancePath
    buildProvenanceSha256 = $buildProvenanceSha256
    payloadModules = $payloadModules
    payloadFiles = $payloadFiles
    verifiedSeries = @($verifiedSeries -split ";" | Where-Object { $_ })
    candidateSeries = @((($candidateLegacySeries + ";" + $candidateNet8Series) `
        -split ";") | Where-Object { $_ })
    sdkValidation = $sdkBuildMetadata
    payloadOrigin = $payloadOrigin
    sourceManifestSha256 = $sourceManifestHashAfter
    sourceCodeSha256 = $sourceCodeHashBeforeResult
    buildScriptSha256 = [string]$criticalInputHashesBefore.buildScript
    packagerSha256 = [string]$criticalInputHashesBefore.packager
    bundleBuilderSha256 = [string]$criticalInputHashesBefore.bundleBuilder
    manifestTemplateSha256 = [string]$criticalInputHashesBefore.manifestTemplate
    compatibilityMatrixSha256 = [string]$criticalInputHashesBefore.compatibilityMatrix
    releaseDefinitionSha256 = [string]$criticalInputHashesBefore.releaseDefinition
    installerSourceSha256 = [string]$criticalInputHashesBefore.installerSource
    installGuideSha256 = [string]$criticalInputHashesBefore.installGuide
    payloadSignScriptPath = $payloadSignScriptPath
    payloadSignScriptSha256 = $payloadSignScriptSha256
    allowedSignerThumbprints = @($allowedSignerThumbprints)
    installerSignature = $installerSignatureMetadata
    signatureClassification = [string]$installerSignatureMetadata.classification
    signed = ([string]$installerSignatureMetadata.status -eq "Valid")
    generatedUtc = [DateTime]::UtcNow.ToString("o")
}
$resultPath = Join-Path $outputFullPath "build-result.json"
$result | ConvertTo-Json -Depth 7 |
    Set-Content -LiteralPath $resultPath -Encoding UTF8

Write-Host "Installer: $installerPath"
Write-Host "Build result: $resultPath"
