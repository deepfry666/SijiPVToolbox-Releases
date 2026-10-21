param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$packager = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\package-installer.ps1") -Raw -Encoding UTF8
$evidence = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\New-ReleaseEvidence.ps1") -Raw -Encoding UTF8
$cadVerifier = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\verify-autocad2020.ps1") -Raw -Encoding UTF8
$buildScript = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\build.ps1") -Raw -Encoding UTF8
$sourceBundleManifest = Get-Content -LiteralPath (Join-Path $projectRoot `
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml") -Raw -Encoding UTF8
$installerSource = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\installer\SijiPVToolbox.iss") -Raw -Encoding UTF8
$runtimeTest = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\installer\Test-InstallerRuntime.ps1") -Raw -Encoding UTF8
$artifactTest = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\installer\Test-InstallerArtifacts.ps1") -Raw -Encoding UTF8
$testScript = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\test.ps1") -Raw -Encoding UTF8
$traceabilityAtomicHelper = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\ReleaseTraceabilityAtomic.ps1") -Raw -Encoding UTF8
$toolboxCommands = Get-Content -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox\Commands\ToolboxCommands.cs") -Raw -Encoding UTF8
$verificationCommands = Get-Content -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.Rail\Commands\VerificationCommands.cs") -Raw -Encoding UTF8
$railDrawingService = Get-Content -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.Rail\Commands\RailDrawingService.cs") -Raw -Encoding UTF8
$settingsStore = Get-Content -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.Rail\Settings\RailSettingsStore.cs") -Raw -Encoding UTF8
$realDrawingVerifier = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\verify-real-drawing.ps1") -Raw -Encoding UTF8
$realDrawingBuilder = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\build-real-drawing-verifier.ps1") -Raw -Encoding UTF8
$realDrawingProbe = Get-Content -LiteralPath (Join-Path $projectRoot `
    "tests\RealDrawingProbe.cs") -Raw -Encoding UTF8
$realDrawingAssemblyInfo = Get-Content -LiteralPath (Join-Path $projectRoot `
    "tests\RealDrawingVerificationAssemblyInfo.cs") -Raw -Encoding UTF8
$archiveBuilder = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\New-ReleaseArchive.ps1") -Raw -Encoding UTF8
$archiveVerifier = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\Test-ReleaseArchive.ps1") -Raw -Encoding UTF8
$releasePublisher = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\Publish-TestRelease.ps1") -Raw -Encoding UTF8
$bundleBuilder = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\installer\New-MultiVersionBundle.ps1") -Raw -Encoding UTF8
$installerVerificationRecord = Get-Content -LiteralPath (Join-Path $projectRoot `
    "docs\安装器验证记录_v1.0.8.md") -Raw -Encoding UTF8
$publishAtomicityTests = Get-Content -LiteralPath (Join-Path $projectRoot `
    "tests\PublishAtomicityTests.ps1") -Raw -Encoding UTF8
$archiveRegressionTests = Get-Content -LiteralPath (Join-Path $projectRoot `
    "tests\ReleaseArchiveVerifierTests.ps1") -Raw -Encoding UTF8
$traceabilityAtomicityTests = Get-Content -LiteralPath (Join-Path $projectRoot `
    "tests\ReleaseTraceabilityAtomicityTests.ps1") -Raw -Encoding UTF8
$release = Get-Content -LiteralPath (Join-Path $projectRoot `
    "scripts\installer\release-v1.0.8.json") -Raw -Encoding UTF8 |
    ConvertFrom-Json
$frozenReleasePath = Join-Path $projectRoot `
    "scripts\installer\release-v0.3.3.json"
if (-not (Test-Path -LiteralPath $frozenReleasePath -PathType Leaf)) {
    throw "Frozen v0.3.3 release definition is missing: $frozenReleasePath"
}
$frozenRelease = Get-Content -LiteralPath $frozenReleasePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$passed = 0

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Message)
    if (-not $Text.Contains($Needle)) {
        throw "$Message; missing: $Needle"
    }
    $script:passed++
}

function Assert-Before {
    param([string]$Text, [string]$First, [string]$Second, [string]$Message)
    $firstIndex = $Text.IndexOf($First, [StringComparison]::Ordinal)
    $secondIndex = $Text.IndexOf($Second, [StringComparison]::Ordinal)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or
        $firstIndex -ge $secondIndex) {
        throw "$Message; expected '$First' before '$Second'"
    }
    $script:passed++
}

function Assert-NotContains {
    param([string]$Text, [string]$Needle, [string]$Message)
    if ($Text.Contains($Needle)) {
        throw "$Message; forbidden: $Needle"
    }
    $script:passed++
}

function Assert-After {
    param([string]$Text, [string]$First, [string]$Second, [string]$Message)
    $firstIndex = $Text.IndexOf($First, [StringComparison]::Ordinal)
    $secondIndex = $Text.LastIndexOf($Second, [StringComparison]::Ordinal)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or
        $firstIndex -ge $secondIndex) {
        throw "$Message; expected '$Second' after '$First'"
    }
    $script:passed++
}

Assert-Contains $packager '[string]$PayloadSignScript' `
    "Formal packaging must expose a payload signing hook"
Assert-Contains $packager '& $payloadSignScriptPath -FilePath $sourceModulePath' `
    "Fresh payload DLLs must be passed to the signing hook"
Assert-Before $packager '& $payloadSignScriptPath -FilePath $sourceModulePath' `
    '& $newBundleScript' `
    "Payload DLLs must be signed before bundle staging and inventory hashing"
Assert-Contains $packager 'Get-AuthenticodeSignature' `
    "Formal packaging must validate Authenticode signatures"
Assert-Contains $packager 'allowedSignerThumbprints' `
    "Formal packaging must bind signatures to the release signer allowlist"
Assert-Contains $packager 'Assert-AllowedAuthenticodeSignature $sourceModulePath' `
    "Formal source payloads must use an approved signer"
Assert-Contains $packager 'does not have a trusted Authenticode timestamp' `
    "Formal signatures must include a trusted timestamp"
Assert-Contains $packager 'Assert-AllowedAuthenticodeSignature $modulePath' `
    "Formal staged payloads must use an approved signer"
Assert-Contains $packager '$installerSignatureMetadata = Assert-AllowedAuthenticodeSignature' `
    "Formal installers must fail closed on an unapproved signer"
Assert-Contains $packager 'sourceSignature = $sourceSignatureMetadata' `
    "Build results must record source payload certificate metadata"
Assert-Contains $packager 'stagedSignature = $stagedSignatureMetadata' `
    "Build results must record staged payload certificate metadata"
Assert-Contains $packager 'installerSignature = $installerSignatureMetadata' `
    "Build results must record installer certificate metadata"
Assert-Contains $packager 'must use the " +' `
    "Formal build must enforce one signer across payload and installer"
Assert-Contains $packager 'same signing certificate for $($_.series)' `
    "Formal build must reject mixed approved signing certificates"
Assert-Contains $packager 'schemaVersion = 6' `
    "Build results must use the multi-module SDK, payload, and signer-bound schema"
Assert-Contains $packager 'classification = "NotSigned"' `
    "Unsigned artifacts must record the explicit NotSigned classification"
Assert-Contains $packager 'Assert-NotSignedAuthenticodeSignature $sourceModulePath' `
    "UNSIGNED source payloads must remain unsigned"
Assert-Contains $packager 'Assert-NotSignedAuthenticodeSignature $modulePath' `
    "UNSIGNED staged payloads must remain unsigned"
Assert-Contains $packager '$installerSignatureMetadata = Assert-NotSignedAuthenticodeSignature' `
    "UNSIGNED installers must remain unsigned"
Assert-Before $packager 'Assert-FileSha256 $payloadSignScriptPath' `
    '& $payloadSignScriptPath -FilePath $sourceModulePath' `
    "Payload signing script hash must be checked before invocation"
Assert-After $packager '& $payloadSignScriptPath -FilePath $sourceModulePath' `
    'Assert-FileSha256 $payloadSignScriptPath' `
    "Payload signing script hash must be checked after invocation"
Assert-Before $packager '& $payloadSignScriptPath -FilePath $sourceModulePath' `
    'Assert-AllowedAuthenticodeSignature $sourceModulePath' `
    "Payload signing must finish before the signed source is accepted"
if (-not ($release.PSObject.Properties.Name -contains "allowedSignerThumbprints")) {
    throw "Release definition must declare allowedSignerThumbprints"
}
$passed++
if (@($release.allowedSignerThumbprints).Count -ne 0) {
    throw "Development release must not invent a formal signer thumbprint"
}
$passed++
if ([string]$release.currentReleaseClassification -ne "UNSIGNED" -or
    [string]$release.authenticodeExpectation -ne "NotSigned" -or
    -not [bool]$release.publicDistributionAllowed) {
    throw "Current release must be UNSIGNED, NotSigned, and public-distributable"
}
$passed++

if ([version]([string]$frozenRelease.releaseVersion) -ne [version]"0.3.3" -or
    [version]([string]$release.releaseVersion) -ne [version]"1.0.8") {
    throw "Stable identity comparison must bind frozen v0.3.3 to current v1.0.8"
}
$passed++
Assert-Contains $railDrawingService `
    'internal const string ToolEntityVersion = "1.0.8";' `
    "New drawing entities must carry the current v1.0.8 metadata version"
$expectedStableGuids = [ordered]@{
    installerAppId = "{0DB1FAD5-EC98-434F-8AF6-E16B3462A678}"
    testInstallerAppId = "{9AB1205B-9640-4A83-A9A0-912285618A0D}"
    bundleProductCode = "{9C42F595-68E0-4945-B8D7-62BE70975D5F}"
    testBundleProductCode = "{6069B95D-EFB6-431B-8275-CAC66D2B19C4}"
    bundleUpgradeCode = "{7D31C7E3-7805-4611-9B3E-5946775A86EC}"
    testBundleUpgradeCode = "{2451814C-C7C6-46AC-A303-422227D5FE09}"
}
foreach ($field in $expectedStableGuids.Keys) {
    try {
        $expectedGuid = ([guid]$expectedStableGuids[$field]).ToString("B")
        $frozenGuid = ([guid]([string]$frozenRelease.$field)).ToString("B")
        $currentGuid = ([guid]([string]$release.$field)).ToString("B")
    } catch {
        throw "Stable release GUID '$field' is missing or invalid"
    }
    if ($frozenGuid -ne $expectedGuid -or $currentGuid -ne $expectedGuid) {
        throw "Stable release GUID '$field' changed between v0.3.3 and v1.0.8"
    }
    $passed++
}
$acceptedV020ProductCode =
    ([guid]"{5C2AE40B-D8F5-4025-B75E-13C0AEEB1251}").ToString("B")
foreach ($definition in @($frozenRelease, $release)) {
    $legacyCodes = @($definition.acceptedLegacyProductCodes | ForEach-Object {
        try { ([guid]([string]$_)).ToString("B") } catch { "<invalid>" }
    })
    if ($legacyCodes.Count -ne 1 -or
        $legacyCodes[0] -ne $acceptedV020ProductCode) {
        throw "Accepted v0.2.0 ProductCode is missing or changed"
    }
    $passed++
}
Assert-Contains $buildScript '[string]$TargetSeries = "R23.1"' `
    "Build must explicitly declare the target AutoCAD Series"
Assert-Contains $buildScript 'assembly version $assemblyVersion does not match' `
    "Build must reject managed APIs from another Series"
Assert-Contains $buildScript 'SignerCertificate.Subject.Contains("Autodesk")' `
    "Build must validate Autodesk signatures on core managed APIs"
Assert-Contains $buildScript 'requires the independent .NET 8 build pipeline' `
    "Legacy build must reject .NET 8 AutoCAD targets"
Assert-Contains $packager '-BuildProfile ([string]$buildTarget.buildProfile)' `
    "Packager must bind build invocation to the matrix build profile"
Assert-Contains $packager 'sdkValidation = $sdkBuildMetadata' `
    "Build result must preserve SDK validation provenance"
Assert-Contains $packager '[string]$FrozenBuildResult' `
    "Installer runtime build must accept a frozen release candidate"
Assert-Contains $packager '-BuildProvenance $buildProvenancePath' `
    "Bundle generation must receive the SDK build provenance"
Assert-Contains $packager 'sourcePath = ([string]$_.sourceModule).Replace("\", "/")' `
    "SDK provenance paths must use literal slash normalization"
Assert-NotContains $packager '-replace "\", "/"' `
    "SDK provenance paths must not use an invalid backslash regex"
Assert-Contains $runtimeTest 'isolated installer uses a frozen release-candidate payload' `
    "Runtime tests must reject a freshly rebuilt unrelated payload"
Assert-Contains $runtimeTest 'installed module hash matches frozen candidate' `
    "Runtime tests must hash every module actually installed"
Assert-Contains $testScript 'PURE_CODE_REGRESSION=PASS' `
    "Pure-code regression must emit a terminal success marker"
Assert-Contains $artifactTest 'INSTALLER_ARTIFACT_VERIFICATION=PASS' `
    "Installer artifact verification must emit a terminal success marker"
Assert-Contains $artifactTest 'PAYLOAD_FILE_COUNT=' `
    "Installer artifact receipt must count every module DLL"
Assert-Contains $artifactTest 'PAYLOAD_FILES_SHA256=' `
    "Installer artifact receipt must bind the complete module DLL set"
Assert-Contains $runtimeTest 'RUNTIME_VERIFICATION=PASS' `
    "Installer runtime verification must emit a terminal success marker"
foreach ($receiptName in @(
    "RELEASE_VERSION",
    "FROZEN_BUILD_RESULT_SHA256",
    "FROZEN_INSTALLER_SHA256",
    "TEST_BUILD_RESULT_SHA256",
    "TEST_INSTALLER_SHA256",
    "INSTALLED_PAYLOAD_SERIES",
    "INSTALLED_PAYLOAD_SHA256",
    "INSTALLED_PAYLOAD_FILE_COUNT",
    "INSTALLED_PAYLOAD_FILES_SHA256")) {
    Assert-Contains $runtimeTest ('$records.Add("' + $receiptName + '=') `
        "Installer runtime verification must emit the $receiptName receipt"
}
Assert-Contains $runtimeTest `
    'release-v0.3.3.json' `
    "Runtime verification must read the frozen v0.3.3 release identity"
Assert-Contains $runtimeTest 'runtime accepts the v0.2.0 ProductCode' `
    "Runtime verification must retain the accepted v0.2.0 product identity"
Assert-Contains $installerSource 'VersionMatchesSeries' `
    "Installer must compare detected file versions with registry Series"
Assert-Contains $installerSource 'VersionMatchesSeries(FileVersion, Series)' `
    "Installer identity must validate the acad.exe file version against Series"
Assert-Contains $installerSource 'VersionMatchesSeries(ProductVersion, Series)' `
    "Installer must accept acad.exe ProductVersion when a host patches fixed FileVersion"
Assert-Contains $installerSource 'AutoCADExecutableVersionMatches' `
    "Installer must centralize acad.exe fixed/product version matching"
Assert-Contains $installerSource 'System.Software.ProductVersion' `
    "Installer must read the locale-independent acad.exe ProductVersion property"
Assert-Contains $installerSource 'acdbmgd.dll版本' `
    "Installer must log the managed API version used for identity checking"
Assert-Contains $installerSource 'CreateOutputMsgPage(wpWelcome' `
    "Installer must use a compact non-scrolling information page"
if (-not [regex]::IsMatch($installerSource,
        "(?s)CreateOutputMsgPage\(wpWelcome,\s*'安装信息',\s*" +
        "'四季光伏工具箱将安装到 AutoCAD 的标准插件目录。'\s*\+\s*" +
        "Chr\(13\)\s*\+\s*Chr\(10\)\s*\+\s*GetCadPageSubCaption,\s*" +
        "CadDetectionText\);")) {
    throw "Installer information page must call CreateOutputMsgPage with four parameters"
}
$passed++
Assert-Contains $installerSource '主要功能' `
    "Installer information page must summarize user-facing features"
Assert-Contains $installerSource '不提供自定义安装地址' `
    "Installer must explain why AutoCAD plugin paths are fixed"
Assert-Contains $installerSource "'- 仅为我安装：' + GetUserBundlePath" `
    "Installer must show the current-user plugin path"
Assert-Contains $installerSource "'- 为所有用户安装：' + GetSystemBundlePath" `
    "Installer must show the all-users plugin path"
Assert-Contains $installerSource '已有版本会自动升级' `
    "Installer must explain the existing-user upgrade path"
Assert-Contains $installerSource `
    'CanInstall := IdentityMatches and ListContains(VerifiedSeriesList, Series)' `
    "Installer must reject candidate hosts even when their identity is coherent"
Assert-Contains $installerSource "ConclusionText := '可以安装'" `
    "Installer must retain an installable host conclusion"
Assert-Contains $installerSource "ConclusionText := '当前不支持'" `
    "Installer must retain an unsupported host conclusion"
Assert-Contains $installerSource 'ValidateBundleAt' `
    "Installer must validate an existing managed bundle before upgrade"
Assert-Contains $installerSource '普通安装禁止降级' `
    "Installer must explain and enforce the downgrade block"
Assert-Contains $installerSource `
    "请先启动或重启 AutoCAD，再在命令行输入 JJJJ" `
    "Finish page must explain how to open the toolbox after CAD restart"
Assert-Contains $installerSource 'DisableWelcomePage=yes' `
    "Installer must omit the redundant welcome page"
Assert-Contains $installerSource 'DisableReadyPage=yes' `
    "Installer must omit the redundant ready page"
Assert-NotContains $installerSource 'TEST_UNSIGNED' `
    "Installer source must not expose the former internal classification"
Assert-NotContains $installerSource '未签名测试版' `
    "Installer source must not expose the former test-build warning"
Assert-Contains $installerSource '#error This installer must be compiled by scripts/package-installer.ps1.' `
    "Installer source must reject uncontrolled direct compilation"
Assert-Contains $installerSource 'PipelineInvocation != "FOURJTOOLS_PACKAGE_PIPELINE_V1"' `
    "Installer source must require the controlled packaging marker"
Assert-Contains $installerSource 'Signed builds must define SignToolName' `
    "Installer source must fail closed when a formal build has no signer"
Assert-Contains $installerSource '#define OutputBaseFilename "四季光伏工具箱_Setup_v1.0.8"' `
    "Installer default output filename must match v1.0.8"

$verificationSource = Get-Content (Join-Path $projectRoot `
    "src\StructuralToolbox.Rail\Commands\VerificationCommands.cs") -Raw -Encoding UTF8
Assert-Contains $verificationSource 'new Version(1, 0, 8, 0)' `
    "AutoCAD verifier must compare release modules against v1.0.8.0"
Assert-Contains $installerSource 'OutputBaseFilename={#OutputBaseFilename}' `
    "Installer output filename must use the controlled pipeline value"
Assert-Contains $installerSource 'UnsignedDistributionBuild' `
    "Installer source must distinguish the truthful unsigned distribution mode"
Assert-Contains $installerSource 'Result := IsTestBuild and' `
    "Downgrade bypass must be restricted to isolated installer tests"
Assert-Contains $installerSource 'function NextButtonClick(CurPageID: Integer): Boolean;' `
    "Unsupported AutoCAD hosts must be blocked before the install step"
Assert-Contains $installerSource "Log('Verified installer target: '" `
    "Installer log must name the verified AutoCAD target"
Assert-Contains $packager '/DPipelineInvocation=FOURJTOOLS_PACKAGE_PIPELINE_V1' `
    "Packager must provide the controlled installer compilation marker"
Assert-Contains $packager '/DVerifiedTargetLabel=AutoCAD' `
    "Packager must pass the verified target label to the installer UI"
Assert-Contains $artifactTest `
    'release-v0.3.3.json' `
    "Artifact verification must bind stable IDs to the frozen v0.3.3 definition"
Assert-Contains $artifactTest `
    'Assert-Condition (Test-ExactFileSet $actualBundleFiles' `
    "Artifact verification must enforce the exact staged Bundle file allowlist"
Assert-Contains $artifactTest `
    '$fileSourceEntryCount -eq 1' `
    "Artifact verification must allow exactly one Inno Files source entry"
Assert-Contains $artifactTest `
    'Source: "{#PayloadBundleDir}\*"' `
    "Artifact verification must bind the sole Inno source to the validated Bundle"
Assert-Contains $runtimeTest `
    'Assert-ExactBundleFileSet $installedBundle @($result.payloadFiles)' `
    "Runtime verification must enforce the exact installed Bundle file allowlist"
if ([regex]::Matches($runtimeTest,
        [regex]::Escape(
            'Assert-ExactBundleFileSet $installedBundle @($result.payloadFiles)')).Count `
        -ne 2) {
    throw "Runtime verification must check the installed Bundle after fresh install and same-version upgrade"
}
$passed++
Assert-Contains $evidence '[string[]]$AutoCADVerificationResults' `
    "Release evidence must require explicit AutoCAD verification results"
Assert-Contains $evidence 'ReleaseBuildSkipped' `
    "Release evidence must require verification without rebuilding the payload"
Assert-Contains $evidence 'loadedDllHash -ne [string]$payload[0].sourceSha256' `
    "Runtime verification DLL must match the packaged source hash"
Assert-Contains $evidence 'loadedDllHash -ne [string]$payload[0].stagedSha256' `
    "Runtime verification DLL must match the staged payload hash"
Assert-Contains $evidence 'DIMENSIONS_UNCHANGED=True' `
    "Release evidence must require the rail-only dimension-preservation marker"
Assert-Contains $evidence 'nativeDimensionsUnchanged = $true' `
    "Release evidence must record unchanged native dimensions"
Assert-Contains $evidence 'unexpectedOutputEntries' `
    "Release evidence must reject historical siblings under outputs"
Assert-Contains $evidence 'Get-SingleReceiptValue' `
    "Release evidence must parse versioned verification receipts"
Assert-Contains $testScript 'SOURCE_CODE_SHA256=' `
    "Pure-code regression must emit its source fingerprint receipt"
Assert-Contains $testScript 'REGRESSION_SUITE_SHA256=' `
    "Pure-code regression must emit its scripts-and-tests fingerprint receipt"
Assert-Contains $testScript 'ReleaseTraceabilityAtomic.ps1' `
    "Pure-code regression must use the shared fingerprint implementation"
Assert-Contains $artifactTest 'BUILD_RESULT_SHA256=' `
    "Artifact verification must emit its build-result receipt"
Assert-Contains $archiveBuilder 'Release archive already exists and will not be overwritten' `
    "Release archives must be immutable once created"
Assert-Contains $archiveBuilder 'formalCandidateExecutedInRealDrawingRun = $false' `
    "Archive manifest must preserve the real-drawing execution boundary"
Assert-Contains $archiveBuilder 'schemaVersion -ne 5' `
    "Archive creation must require schema-5 release evidence"
Assert-Contains $archiveBuilder 'releaseControls = [ordered]@{' `
    "Archive manifest must bind the release evidence and release checksum"
Assert-Contains $archiveBuilder 'Get-SingleReceiptValue' `
    "Archive creation must independently parse exact-one verification receipts"
Assert-Contains $archiveBuilder 'REGRESSION_SUITE_SHA256' `
    "Archive creation must bind the exact regression-suite receipt"
Assert-Contains $archiveBuilder 'Get-RegressionSuiteFingerprint $sourceDestination' `
    "Archive creation must re-hash copied scripts and tests"
Assert-Contains $archiveBuilder 'Get-StructuralSourceFingerprint $sourceDestination' `
    "Archive creation must re-hash copied production source"
Assert-Contains $archiveBuilder 'Installer artifact receipt is not bound to this candidate' `
    "Archive creation must bind artifact verification to the final candidate"
Assert-Contains $archiveBuilder 'Installer runtime receipt is not bound to this candidate' `
    "Archive creation must bind runtime verification to the final candidate"
Assert-Contains $archiveBuilder `
    'isolate\Roaming\StructuralToolbox\rail-settings.xml' `
    "Archive must retain the isolated settings referenced by real-drawing checksums"
Assert-Contains $archiveBuilder 'scripts\Test-ReleaseArchive.ps1' `
    "Archive creation must invoke the independent archive verifier"
Assert-Contains $archiveBuilder 'Invoke-ReleaseArchiveAtomicCommit' `
    "Archive creation must use the dynamically tested atomic commit helper"
Assert-Contains $archiveBuilder `
    'Release directory must contain top-level files only.' `
    "Archive creation must reject recursive release payloads"
Assert-Contains $archiveBuilder '$expectedReleaseFileNames = @(' `
    "Archive creation must define the exact release-file allowlist"
Assert-Contains $archiveBuilder `
    '"四季光伏工具箱_Setup_v${version3}.exe"' `
    "Archive release allowlist must retain exactly the transferable installer"
Assert-Contains $archiveBuilder `
    '"evidence\release-controls\$releaseEvidenceName"' `
    "Archive must retain schema-5 evidence outside the release directory"
Assert-Contains $archiveBuilder `
    '"evidence\release-controls\SHA256SUMS.txt"' `
    "Archive must retain the detached evidence checksum"
Assert-Contains $archiveBuilder 'schemaVersion = 3' `
    "v0.3.4 archive manifests must use the one-EXE schema"
Assert-Contains $archiveBuilder 'classification = "UNSIGNED"' `
    "v0.3.4 archive manifests must preserve the truthful release classification"
Assert-Contains $archiveBuilder `
    'Assert-AtomicExactNameSet $expectedReleaseFileNames' `
    "Archive creation must enforce the exact release-file allowlist"
Assert-Before $archiveBuilder `
    '$archiveCopyPlanBeforeValidation = Get-ArchiveCopyPlan' `
    '$archiveCopyPlanAfterValidation = Get-ArchiveCopyPlan' `
    "Archive creation must freeze inputs before semantic validation"
Assert-Contains $archiveBuilder `
    'Assert-ArchiveCopyPlansEqual $archiveCopyPlanBeforeValidation' `
    "Archive creation must reject inputs changed during semantic validation"
Assert-Contains $archiveBuilder `
    'Assert-ArchiveStageMatchesPlan $archiveCopyPlanBeforeValidation $stageRoot' `
    "Archive creation must compare every staged file with the frozen copy plan"
Assert-Contains $archiveBuilder `
    '$sourceDestination "scripts\New-ReleaseArchive.ps1"' `
    "Archive manifest must hash its staged generator"
Assert-Contains $archiveBuilder `
    '$stagedBuildResultPath -Algorithm SHA256).Hash' `
    "Archive manifest must hash the staged build result"
Assert-Contains $archiveBuilder `
    '$stagedInstallerPath -Algorithm SHA256).Hash' `
    "Archive manifest must hash the staged installer"
Assert-Contains $archiveBuilder `
    '$stagedReleaseEvidencePath -Algorithm SHA256).Hash' `
    "Archive manifest must hash staged release evidence"
Assert-Contains $archiveBuilder `
    '$stagedReleaseChecksumPath -Algorithm SHA256).Hash' `
    "Archive manifest must hash the staged release checksum"
Assert-Contains $archiveBuilder '$rootChecksumPath = Join-Path $rootPath "SHA256SUMS.txt"' `
    "Archive inventory must identify only the root checksum control file"
Assert-Contains $archiveBuilder '$_.FullName -ne $rootChecksumPath' `
    "Archive inventory must retain nested SHA256SUMS evidence files"
Assert-NotContains $archiveBuilder `
    '$_.Name -notin @("archive-manifest.json", "SHA256SUMS.txt")' `
    "Archive inventory must not exclude control filenames at every depth"
Assert-Contains $archiveVerifier 'Archive manifest contains an unsafe or duplicate path' `
    "Archive verification must reject unsafe manifest paths"
Assert-Contains $archiveVerifier 'ARCHIVE_VERIFICATION=PASS' `
    "Archive verification must emit an explicit success marker"
Assert-Contains $archiveVerifier '[int]$archivedEvidence.schemaVersion -ne 5' `
    "Archive verifier must independently require schema-5 release evidence"
Assert-Contains $archiveVerifier 'Archived release-control SHA256SUMS failed' `
    "Archive verifier must independently verify the archived evidence checksum"
Assert-Contains $archiveVerifier `
    '$archiveLeaf = Split-Path -Leaf $archiveRoot' `
    "Archive verifier must bind directory identity before choosing a schema"
Assert-Contains $archiveVerifier `
    'Archive directory version does not match archive manifest.' `
    "Archive verifier must reject version-directory downgrades"
Assert-Contains $archiveVerifier `
    'Archived release directory must contain top-level files only.' `
    "Archive verifier must reject release subdirectories"
Assert-Contains $archiveVerifier `
    'Get-RegressionSuiteFingerprint $archivedFilesRoot' `
    "Archive verifier must re-hash archived scripts and tests"
Assert-Contains $archiveVerifier `
    'Get-StructuralSourceFingerprint $archivedFilesRoot' `
    "Archive verifier must re-hash archived production source"
Assert-Contains $archiveVerifier `
    'Archived installer is not bound to archive-manifest.json.' `
    "Archive verifier must independently bind the one EXE to the archived build"
Assert-Contains $archiveVerifier `
    '$expectedManifestSchema = if ($archiveVersion -ge [version]"0.3.4")' `
    "Archive verifier must select the v0.3.4 one-EXE manifest schema"
Assert-Contains $archiveVerifier `
    '} elseif ($archiveVersion -ge [version]"0.3.3") {' `
    "Archive verifier must retain schema-2 compatibility for frozen v0.3.3 archives"
Assert-Contains $releasePublisher '$archiveRoot = Join-Path $outputsRoot "archive"' `
    "Publishing must reserve a versioned outputs archive"
Assert-Contains $releasePublisher '$independentDeliveryPattern' `
    "Publishing must recognize versioned independent deliverables"
Assert-Contains $releasePublisher `
    'Only toolbox release directories, outputs/archive, and versioned ' `
    "Publishing must reject untracked output entries"
Assert-Contains $releasePublisher 'Release archive already exists and will not be overwritten' `
    "Publishing must never overwrite an archived release"
Assert-Contains $releasePublisher '$stagedArchiveNames' `
    "Publishing must track staged historical releases for rollback"
Assert-Contains $releasePublisher '$committedArchiveNames' `
    "Publishing must track committed historical releases for rollback"
Assert-Contains $releasePublisher 'archivedReleaseDirectories' `
    "Publishing receipt must record archived release directories"
Assert-Contains $releasePublisher 'Published installer copy differs from the candidate' `
    "Publishing must re-hash the copied candidate installer"
Assert-Contains $releasePublisher 'TEST_RELEASE_PUBLISH=PASS' `
    "Publishing must emit an explicit success marker"
Assert-Before $releasePublisher '[IO.File]::WriteAllText($receiptStagePath' `
    'Move-Item -LiteralPath $stageRoot -Destination $releaseRoot' `
    "Publishing must stage and validate its receipt before committing the release"
Assert-Contains $releasePublisher `
    'Move-Item -LiteralPath $releaseRoot -Destination $stageRoot' `
    "Publishing must roll the release directory back if receipt commit fails"
Assert-Contains $releasePublisher '[IO.File]::Move($receiptStagePath, $receiptPath)' `
    "Publishing must atomically commit a non-overwriting receipt"
Assert-Contains $releasePublisher `
    'Internal publish fault injection is restricted to its work fixture.' `
    "Publish fault injection must be restricted to the isolated work fixture"
Assert-Contains $publishAtomicityTests `
    'failed publish must leave outputs empty' `
    "Publish atomicity regression must prove outputs rollback"
Assert-Contains $publishAtomicityTests `
    'failed replacement publish must preserve independent deliverables' `
    "Publish rollback must preserve independent deliverables"
Assert-Contains $publishAtomicityTests `
    'successful replacement publish must preserve independent deliverables' `
    "Successful publish must preserve independent deliverables"
Assert-Contains $publishAtomicityTests `
    'failed publish must leave no committed or staged receipt' `
    "Publish atomicity regression must prove receipt cleanup"
Assert-Contains $publishAtomicityTests `
    'automatic archival' `
    "Publish atomicity regression must prove automatic historical archival"
Assert-Contains $publishAtomicityTests `
    'automatic archival must preserve both versioned installer hashes' `
    "Publish atomicity regression must prove archived EXE byte preservation"
Assert-Contains $archiveRegressionTests 'Write-Evidence 4' `
    "Archive regression must contain a self-consistent obsolete-schema negative case"
Assert-Contains $archiveRegressionTests `
    'incorrect evidence control hash must be rejected' `
    "Archive regression must reject an incorrect evidence control hash"
Assert-Contains $archiveRegressionTests `
    'incorrect release checksum entry must be rejected' `
    "Archive regression must reject an incorrect release checksum"
Assert-Contains $archiveRegressionTests `
    'directory and manifest version mismatch must be rejected' `
    "Archive regression must reject directory-version mismatches"
Assert-Contains $archiveRegressionTests `
    'manifest downgrade under a current-version directory must be rejected' `
    "Archive regression must reject schema downgrades under a newer directory"
Assert-Contains $archiveRegressionTests `
    'release subdirectory absent from release checksum must be rejected' `
    "Archive regression must reject recursively injected release files"
Assert-Contains $archiveRegressionTests `
    'self-consistent extra release file must be rejected' `
    "Archive regression must reject an extra release file even after re-hashing"
Assert-Contains $testScript 'tests\PublishAtomicityTests.ps1' `
    "Pure-code regression must execute publish atomicity tests"
Assert-Contains $testScript 'tests\ReleaseTraceabilityAtomicityTests.ps1' `
    "Pure-code regression must execute release-control atomicity tests"
Assert-Contains $testScript 'tests\ReleaseArchiveVerifierTests.ps1' `
    "Pure-code regression must execute archive verifier positive and negative tests"
Assert-Contains $releasePublisher `
    'Assert-Under $installerPath $workRoot "Candidate installer"' `
    "Publishing must accept the installer only from the private work tree"
Assert-Contains $releasePublisher `
    '[char]0x56DB' `
    "Publishing must construct the normal Unicode one-EXE filename"
Assert-Contains $evidence 'schemaVersion = 5' `
    "Release evidence must use the SDK/runtime/log/real-drawing-bound schema"
Assert-Contains $evidence 'schema-6 release provenance chain' `
    "Release evidence must reject build results without release provenance"
Assert-Contains $evidence 'payloadFiles = $payloadFileEvidence' `
    "Release evidence must publish provenance for every module DLL"
Assert-Contains $evidence 'PAYLOAD_FILES_SHA256' `
    "Release evidence must verify the complete artifact module set"
Assert-Contains $evidence 'INSTALLED_PAYLOAD_FILES_SHA256' `
    "Release evidence must verify the complete installed module set"
Assert-Contains $evidence 'Assert-ExactTextSet $matrixVerifiedSeries $manifestSeries' `
    "Release evidence must match the compatibility matrix to the manifest"
Assert-Contains $evidence 'Assert-ExactTextSet $matrixVerifiedSeries $inventorySeries' `
    "Release evidence must match the compatibility matrix to inventory"
Assert-Contains $evidence 'Assert-ExactTextSet $matrixVerifiedSeries $buildPayloadSeries' `
    "Release evidence must match the compatibility matrix to build payloads"
Assert-Contains $evidence 'Assert-ExactTextSet $matrixVerifiedSeries $runtimeSeries' `
    "Release evidence must match the compatibility matrix to runtime evidence"
Assert-Contains $evidence 'CoreConsoleLogSHA256' `
    "Release evidence must re-hash the CoreConsole log"
Assert-Contains $evidence 'EnvironmentSHA256' `
    "Release evidence must re-hash the environment record"
Assert-Contains $evidence '$successMatches.Count -ne 1' `
    "Release evidence must derive one success marker from the raw log"
Assert-Contains $evidence 'RELEASE_PATH="([^"]+)"' `
    "Release evidence must parse the actually loaded assembly path"
Assert-Contains $evidence 'loadedMvid.ToString("D")' `
    "Release evidence must bind the loaded assembly MVID"
Assert-Contains $evidence 'CoreConsole file version does not match AutoCAD series' `
    "Release evidence must bind the host executable version to its series"
Assert-Contains $evidence '$coreConsoleSignature.SignerCertificate.Subject.Contains("Autodesk")' `
    "Release evidence must require an Autodesk-signed CoreConsole"
Assert-Contains $evidence 'UNSIGNED EXE must have Authenticode status NotSigned' `
    "UNSIGNED evidence must reject a signed or invalid EXE"
Assert-Contains $evidence 'UNSIGNED payload for $($payload.series) must be NotSigned' `
    "UNSIGNED evidence must reject signed or invalid payload DLLs"
Assert-Contains $evidence '$allowedReleaseNames' `
    "Release evidence must enforce the output allowlist"
Assert-Contains $evidence '[string]$PureCodeRegressionLog' `
    "Release evidence must require the pure-code regression log"
Assert-Contains $evidence '[string]$InstallerArtifactVerificationLog' `
    "Release evidence must require installer artifact verification"
Assert-Contains $evidence '[string]$InstallerRuntimeVerificationLog' `
    "Release evidence must require installer runtime verification"
Assert-Contains $evidence '[string]$PublishReceipt' `
    "Release evidence must require the publish receipt"
Assert-Contains $evidence `
    'Move-Item -LiteralPath $stageRoot -Destination $evidenceRoot' `
    "Release evidence must atomically commit a private work directory"
Assert-Contains $evidence `
    'Assert-AtomicExactNameSet $allowedReleaseNames' `
    "Release evidence must preserve the one-EXE output directory"
Assert-Contains $evidence 'REGRESSION_SUITE_SHA256' `
    "Release evidence must bind the exact regression-suite receipt"
Assert-Contains $evidence 'regressionSuiteSha256 = $currentRegressionSuiteHash' `
    "Schema-5 evidence must record its scripts-and-tests fingerprint"
Assert-Contains $traceabilityAtomicHelper 'release-evidence-staging' `
    "Release controls must be prepared under work staging"
Assert-Contains $traceabilityAtomicHelper '[IO.File]::Replace($stagedEvidencePath' `
    "Existing release evidence must be replaced atomically with a backup"
Assert-Contains $traceabilityAtomicHelper '[IO.File]::Replace($stagedChecksumPath' `
    "Existing release checksum must be replaced atomically with a backup"
Assert-Contains $traceabilityAtomicHelper 'Recovery artifacts preserved at:' `
    "Rollback failure must report the preserved recovery directory"
foreach ($moduleRoot in @(
    'src\StructuralToolbox.Contracts',
    'src\StructuralToolbox.Rail',
    'src\StructuralToolbox.Ballast',
    'src\StructuralToolbox.ArrayRecognition',
    'src\StructuralToolbox.Update',
    'src\StructuralToolbox.DrawingTranslation')) {
    Assert-Contains $traceabilityAtomicHelper $moduleRoot `
        "Source fingerprints must include $moduleRoot"
}
Assert-Contains $traceabilityAtomicHelper 'function Get-PayloadSetSha256' `
    "Release receipts must use a deterministic complete-module fingerprint"
Assert-Contains $traceabilityAtomicityTests 'AfterEvidenceStageWrite' `
    "Atomicity regression must inject staged evidence write failure"
Assert-Contains $traceabilityAtomicityTests 'AfterEvidenceCommit' `
    "Atomicity regression must inject failure after evidence replacement"
Assert-Contains $traceabilityAtomicityTests 'AfterChecksumCommit' `
    "Atomicity regression must inject failure after both control replacements"
Assert-Contains $traceabilityAtomicityTests `
    'successful replacement checksum' `
    "Atomicity regression must exercise the release-control success path"
Assert-Contains $traceabilityAtomicityTests `
    '-RollbackFailurePoint "EvidenceRestore"' `
    "Atomicity regression must inject a rollback restore failure"
Assert-Contains $traceabilityAtomicityTests `
    'rollback failure must preserve the previous evidence backup' `
    "Atomicity regression must retain prior evidence for manual recovery"
Assert-Contains $traceabilityAtomicityTests `
    'must delete newly moved control files on rollback' `
    "Atomicity regression must cover first-move rollback deletion"
Assert-Contains $traceabilityAtomicityTests `
    'first commit must move staged evidence into a new release' `
    "Atomicity regression must cover a successful first control commit"
Assert-Contains $traceabilityAtomicityTests `
    'post-move archive verifier failure must propagate' `
    "Atomicity regression must exercise post-move archive rollback"
Assert-Contains $evidence 'Installer runtime verification is not bound to this final release candidate' `
    "Release evidence must bind runtime testing to the final candidate"
Assert-Contains $evidence 'PURE_CODE_REGRESSION=PASS' `
    "Release evidence must require completed pure-code regression"
Assert-Contains $evidence 'INSTALLER_ARTIFACT_VERIFICATION=PASS' `
    "Release evidence must require completed installer artifact verification"
Assert-Contains $evidence 'RUNTIME_VERIFICATION=PASS' `
    "Release evidence must require completed installer runtime verification"
Assert-Contains $evidence '$checksumName = "SHA256SUMS.txt"' `
    "Private release evidence must include a detached checksum list"
Assert-Contains $evidence 'Signed release EXE metadata contains TEST_UNSIGNED wording' `
    "Formal release evidence must reject stale test-release metadata"
Assert-Contains $evidence 'Payload signing script is missing or changed after packaging' `
    "Signed release evidence must preserve the signing script hash"
Assert-Contains $realDrawingVerifier '[ValidateSet("RailsOnly")]' `
    "Real-drawing verification must expose only the rail-only mode"
Assert-Contains $realDrawingVerifier '[string]$Mode = "RailsOnly"' `
    "Real-drawing verification must default to rail-only execution"
Assert-Contains $realDrawingVerifier 'nativeDimensionsDuringCommand' `
    "Real-drawing verification must record native dimensions during the command"
Assert-Contains $installerVerificationRecord '最终发布目录只含' `
    "Installer verification record must state the one-EXE acceptance rule"
Assert-Contains $installerVerificationRecord 'NotSigned' `
    "Installer verification record must state the truthful signature expectation"
Assert-Contains $installerVerificationRecord '原始 DWG 不被测试覆盖' `
    "Installer verification record must protect the original drawing"
Assert-Contains $cadVerifier 'AutoCADSeries = "R23.1"' `
    "AutoCAD verification evidence must record the exact host series"
Assert-Contains $verificationCommands 'new Version(1, 0, 8, 0)' `
    "CoreConsole verification must attest the current assembly version"
Assert-Contains $cadVerifier '[switch]$SkipReleaseBuild' `
    "AutoCAD verification must support testing the already packaged DLL"
Assert-Contains $toolboxCommands '核心运行已验证' `
    "STABOUT must distinguish the verified CoreConsole scope"
Assert-Contains $toolboxCommands '完整 GUI、探索者交互和系统级升级：待验收' `
    "STABOUT must state the remaining GUI and upgrade acceptance scope"
Assert-NotContains $toolboxCommands '当前已验证宿主' `
    "STABOUT must not overstate the verified compatibility scope"

$settingsOverrideBlock = [regex]::Match($settingsStore,
    '(?s)#if INTERNAL_VERIFY(?<body>.*?)#endif')
if (-not $settingsOverrideBlock.Success -or
    -not $settingsOverrideBlock.Groups['body'].Value.Contains(
        'FOURJTOOLS_SETTINGS_PATH')) {
    throw "Internal settings override is not guarded by #if INTERNAL_VERIFY"
}
$settingsStoreOutsideOverride = $settingsStore.Remove(
    $settingsOverrideBlock.Index, $settingsOverrideBlock.Length)
Assert-NotContains $settingsStoreOutsideOverride 'FOURJTOOLS_SETTINGS_PATH' `
    "Formal release settings code must completely ignore the verification variable"
Assert-Contains $buildScript '/define:INTERNAL_VERIFY' `
    "Internal verifier build must explicitly define INTERNAL_VERIFY"
Assert-Contains $buildScript 'Settings\RailSettingsStore.cs' `
    "Internal verifier build must compile the guarded settings-store code"
Assert-Contains $realDrawingVerifier `
    '$startInfo.EnvironmentVariables["FOURJTOOLS_SETTINGS_PATH"]' `
    "Real drawing verification must pass the explicit isolated settings path"
Assert-Contains $realDrawingVerifier 'isolatedSettingsHashBefore' `
    "Real drawing verification must record the isolated settings hash before use"
Assert-Contains $realDrawingVerifier 'isolatedSettingsHashAfter' `
    "Real drawing verification must record the isolated settings hash after use"
Assert-Contains $realDrawingVerifier 'userSettingsBefore' `
    "Real drawing verification must record the real user settings state before use"
Assert-Contains $realDrawingVerifier 'userSettingsAfter' `
    "Real drawing verification must record the real user settings state after use"

Assert-Contains $realDrawingBuilder '[string]$CandidateBuildResult' `
    "Internal real-drawing builds must require the frozen candidate build result"
Assert-Contains $realDrawingBuilder `
    'work\real-drawing-verification-v$releaseVersion3' `
    "Internal real-drawing output must remain under the private work tree"
Assert-NotContains $realDrawingBuilder 'outputs\' `
    "Internal verification assemblies must never be written to outputs"
Assert-Contains $realDrawingBuilder `
    'Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" }' `
    "Internal builds must reuse the release source-fingerprint exclusion rule"
Assert-Contains $realDrawingBuilder 'schemaVersion 5 or newer' `
    "Internal builds must reject obsolete candidate provenance"
foreach ($moduleRoot in @(
    'src\StructuralToolbox',
    'src\StructuralToolbox.Contracts',
    'src\StructuralToolbox.Rail',
    'src\StructuralToolbox.Ballast',
    'src\StructuralToolbox.ArrayRecognition',
    'src\StructuralToolbox.Update',
    'src\StructuralToolbox.DrawingTranslation')) {
    Assert-Contains $realDrawingBuilder $moduleRoot "Internal builds must fingerprint release module root $moduleRoot"
}
foreach ($fingerprintConsumer in @(
    [pscustomobject]@{ Name = 'installer packager'; Text = $packager },
    [pscustomobject]@{ Name = 'installer verifier'; Text = $artifactTest },
    [pscustomobject]@{ Name = 'release evidence'; Text = $evidence },
    [pscustomobject]@{ Name = 'release archive'; Text = $archiveBuilder })) {
    Assert-Contains ([string]$fingerprintConsumer.Text) `
        'src\StructuralToolbox.ArrayRecognition' `
        "$($fingerprintConsumer.Name) fingerprints the linked array-recognition source"
    Assert-Contains ([string]$fingerprintConsumer.Text) `
        'src\StructuralToolbox.DrawingTranslation' `
        "$($fingerprintConsumer.Name) fingerprints the drawing translation source"
}
Assert-Contains $realDrawingBuilder 'sourceCodeSha256' `
    "Internal builds must bind to the formal candidate source fingerprint"
Assert-Contains $realDrawingBuilder 'payloadSourceHash -ne $candidateHashBefore' `
    "Internal builds must bind the candidate to the R23.1 source payload hash"
Assert-Contains $realDrawingBuilder 'payloadStagedHash -ne $candidateHashBefore' `
    "Internal builds must bind the candidate to the R23.1 staged payload hash"
Assert-Contains $realDrawingBuilder 'outputSha256' `
    "Internal builds must bind the candidate to SDK build output provenance"
Assert-Contains $realDrawingBuilder 'Get-PeMachine $candidatePath' `
    "Internal builds must require an x64 formal candidate"
Assert-Contains $realDrawingBuilder 'Get-PeMachine $outputPath' `
    "Internal builds must require an x64 execution assembly"
Assert-Contains $realDrawingBuilder '/langversion:5' `
    "Internal real-drawing builds must preserve AutoCAD 2020 C# compatibility"
Assert-Contains $realDrawingBuilder '/define:INTERNAL_VERIFY' `
    "Internal real-drawing builds must explicitly enable guarded instrumentation"
Assert-Contains $realDrawingBuilder `
    'actualAssemblyVersion.Major -ne 23' `
    "Internal builds must independently validate the R23 managed API major version"
Assert-Contains $realDrawingBuilder `
    'actualAssemblyVersion.Minor -ne 1' `
    "Internal builds must independently validate the R23.1 managed API minor version"
Assert-Contains $realDrawingBuilder 'SignatureStatus]::Valid' `
    "Internal builds must independently require valid Autodesk API signatures"
Assert-Contains $realDrawingBuilder 'signerSubject.Contains("Autodesk")' `
    "Internal builds must independently require Autodesk SDK signers"
Assert-Contains $realDrawingBuilder 'builderHashBefore' `
    "Internal build receipts must record the builder hash before compilation"
Assert-Contains $realDrawingBuilder 'builderHashAfter' `
    "Internal build receipts must record the builder hash after compilation"
Assert-Contains $realDrawingBuilder 'sourceInventory' `
    "Internal build receipts must inventory every compiled source"
Assert-Contains $realDrawingBuilder `
    'FourJTools.RealDrawingVerify' `
    "Internal execution assembly must have a non-release assembly name"
Assert-Contains $realDrawingBuilder `
    'FOURJTOOLS_INTERNAL_REAL_DRAWING_VERIFY_V1' `
    "Internal execution assembly must carry an explicit marker"
Assert-Contains $realDrawingBuilder 'Commands\RailCommands.cs' `
    "Internal execution assembly must compile the actual STPVRAIL command"
Assert-Contains $realDrawingBuilder '$railSourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Rail"' "Internal execution must compile rail sources from their isolated module"
Assert-Contains $realDrawingBuilder 'Commands\QuantityTableService.cs' "Internal execution must compile every service referenced by STPVRAIL"
Assert-Contains $realDrawingProbe 'internal static class InternalRealDrawingTiming' "Real drawing instrumentation must provide command timing state"
Assert-Contains $realDrawingProbe 'internal static void BeginCommand()' "Real drawing timing must mark command start"
Assert-Contains $realDrawingProbe 'internal static void EndCommand()' "Real drawing timing must mark command completion"
Assert-Contains $realDrawingProbe 'internal static Dictionary<string, object> CreateSnapshot()' "Real drawing timing must emit a serializable snapshot"
Assert-NotContains $realDrawingBuilder 'Commands\VerificationCommands.cs' `
    "Real-drawing execution must not include the synthetic fixture command class"
Assert-NotContains $realDrawingBuilder 'Commands\RailAnnotationCommands.cs' `
    "Real-drawing execution must not register unrelated annotation commands"
Assert-NotContains $realDrawingBuilder 'Properties\AssemblyInfo.cs' `
    "Internal execution must not compile the formal release AssemblyInfo"
Assert-NotContains $realDrawingBuilder '/reference:$candidatePath' `
    "Internal execution must not reference or load the formal candidate DLL"

Assert-Contains $realDrawingProbe 'typeof(RailSettings).Assembly' `
    "The probe must derive its execution identity from the compiled rail settings type"
Assert-Contains $realDrawingProbe `
    'report["executionAssembly"] = ExecutionAssemblyIdentity();' `
    "The probe must report the actual execution assembly"
Assert-Contains $realDrawingProbe 'AppDomain.CurrentDomain.GetAssemblies()' `
    "The probe must inspect the runtime for an ambiguously loaded formal assembly"
Assert-Contains $realDrawingProbe 'formalCandidateLoaded' `
    "The probe must fail when StructuralToolbox is already loaded"
Assert-Contains $realDrawingProbe 'FileSha256(executionAssembly.Location)' `
    "The probe must hash the assembly actually loaded by CoreConsole"
Assert-Contains $realDrawingProbe 'ManifestModule.ModuleVersionId' `
    "The probe must report the loaded execution assembly MVID"
Assert-Contains $realDrawingProbe 'VerificationMarkerValue' `
    "The probe must validate the internal assembly marker"
Assert-NotContains $realDrawingProbe 'ResolveCandidateAssembly' `
    "The probe must not discover a formal candidate by assembly name"
Assert-NotContains $realDrawingProbe 'report["candidate"]' `
    "The probe report must not mislabel the internal execution assembly as candidate"

Assert-Contains $realDrawingAssemblyInfo `
    'AssemblyTitle("FourJTools.RealDrawingVerify")' `
    "Dedicated AssemblyInfo must identify the internal verifier"
Assert-Contains $realDrawingAssemblyInfo `
    'FOURJTOOLS_INTERNAL_REAL_DRAWING_VERIFY_V1' `
    "Dedicated AssemblyInfo must define the internal marker"
Assert-Contains $realDrawingAssemblyInfo 'CommandClass(typeof(RailCommands))' `
    "Dedicated AssemblyInfo must register the actual rail command"
Assert-NotContains $realDrawingAssemblyInfo 'ExtensionApplication' `
    "Internal real-drawing execution must not register the release plugin entry point"
Assert-NotContains $realDrawingAssemblyInfo 'VerificationCommands' `
    "Internal real-drawing execution must not register synthetic verification commands"

Assert-Contains $realDrawingVerifier '[string]$CandidateBuildResult' `
    "Real-drawing verification must require candidate provenance"
Assert-Contains $realDrawingVerifier '-CandidateBuildResult $candidateBuildResultPath' `
    "Real-drawing verification must pass candidate provenance to the internal builder"
Assert-Contains $realDrawingVerifier '$qExecutionAssembly' `
    "CoreConsole must load only the dedicated internal execution assembly"
Assert-NotContains $realDrawingVerifier '$qCandidate' `
    "CoreConsole scripts must never NETLOAD the formal candidate"
Assert-NotContains $realDrawingVerifier '$qProbe' `
    "The probe must not be built or loaded as a second ambiguous assembly"
$netloadLines = [regex]::Matches($realDrawingVerifier,
    "(?m)^\s*'_.NETLOAD',\s*$")
if ($netloadLines.Count -ne 2) {
    throw "Real drawing verifier must contain exactly two isolated CoreConsole NETLOAD steps"
}
$passed++
Assert-Contains $realDrawingVerifier `
    'verificationBasis = "same-source-internal-instrumentation"' `
    "Real-drawing evidence must state the same-source instrumentation boundary"
Assert-Contains $realDrawingVerifier 'formalCandidateExecuted = $false' `
    "Real-drawing evidence must state that the formal CAD command was not executed"
Assert-Contains $realDrawingVerifier 'candidateHashBefore' `
    "Real-drawing evidence must hash the formal candidate before execution"
Assert-Contains $realDrawingVerifier 'candidateHashAfter' `
    "Real-drawing evidence must hash the formal candidate after execution"
Assert-Contains $realDrawingVerifier 'candidateBuildResultHashBefore' `
    "Real-drawing evidence must hash candidate provenance before execution"
Assert-Contains $realDrawingVerifier 'candidateBuildResultHashAfter' `
    "Real-drawing evidence must hash candidate provenance after execution"
Assert-Contains $realDrawingVerifier 'executionAssemblyHashAfter' `
    "Real-drawing evidence must hash the internal execution assembly after use"
Assert-Contains $realDrawingVerifier 'executionBuildRecordHashAfter' `
    "Real-drawing evidence must preserve the internal build receipt"
Assert-Contains $realDrawingVerifier 'preScan.executionAssembly.sha256' `
    "Pre-scan must bind the loaded internal assembly SHA-256"
Assert-Contains $realDrawingVerifier 'runtimeSnapshot.Value.executionAssembly' `
    "Command-ended and post-scan evidence must bind the loaded internal assembly"
Assert-Contains $realDrawingVerifier 'formal-candidate-build-result.json' `
    "Run evidence must retain a byte-identical candidate build result"
Assert-Contains $bundleBuilder '$expectedNames = @("StructuralToolbox", "StructuralToolbox.Contracts"' `
    "Bundle creation must require the exact shell and feature module set"
Assert-Contains $bundleBuilder '"StructuralToolbox.Update", "StructuralToolbox.DrawingTranslation")' `
    "Bundle creation must include the update module in the exact payload set"
Assert-Contains $bundleBuilder '"StructuralToolbox.DrawingTranslation")' `
    "Bundle creation must include the drawing translation module in the exact payload set"
Assert-Contains $bundleBuilder '$files.Count -ne 6' `
    "Bundle creation must require exactly six release DLLs"
Assert-Contains $artifactTest '$entryLeaves.Count -eq 4' `
    "Installer artifact verification must require four command assemblies"
Assert-Contains $artifactTest `
    '$entryLeaves -contains "StructuralToolbox.DrawingTranslation.dll"' `
    "Installer artifact verification must require the drawing translation command assembly"
Assert-Contains $cadVerifier `
    'Q:/bundle/ZhStructureToolbox.bundle/Contents/Windows/R23.1/StructuralToolbox.DrawingTranslation.dll' `
    "AutoCAD verification must NETLOAD the drawing translation module"
Assert-Contains $verificationCommands 'MODULES=6' `
    "Internal verification must attest all six release modules"
Assert-Contains $verificationCommands `
    'Assembly translationAssembly = FindReleaseAssembly(' `
    "Internal verification must require the drawing translation assembly"
Assert-Contains $verificationCommands '"STPVTRANSLATE");' `
    "Internal verification must attest the drawing translation command"
Assert-Contains $cadVerifier 'MODULES=([0-9]+)' `
    "AutoCAD verification retains module-count attestation"
Assert-Contains $sourceBundleManifest 'Global="STPVTRANSLATE"' `
    "Bundle manifest must expose direct replacement"
Assert-Contains $sourceBundleManifest 'Global="STPVTRANSLATECONFIRM"' `
    "Bundle manifest must expose confirmed replacement"
Assert-NotContains $sourceBundleManifest 'STPVTRANSLATEINSERT' `
    "Bundle manifest must not expose the retired insert-below command"
Assert-Contains $buildScript 'StructuralToolbox.DrawingTranslation.dll' `
    "Release build retains the drawing translation payload"

Assert-Contains $evidence '[string]$RealDrawingVerificationResult' `
    "Release evidence must require a real-drawing result"
Assert-Contains $evidence `
    'Assert-UnderRoot $realDrawingResultPath $workPrefix' `
    "The real-drawing result must remain under work"
Assert-Contains $evidence `
    '[string]$realDrawing.status -ne "PASS"' `
    "Release evidence must require a PASS real-drawing result"
Assert-Contains $evidence `
    '[string]$realDrawing.mode -ne "RailsOnly"' `
    "Release evidence must require rail-only real-drawing execution"
Assert-Contains $evidence `
    '$expectedRealDrawingRectangles = 3520' `
    "Release evidence must freeze the real-drawing rectangle baseline"
Assert-Contains $evidence '$expectedRealDrawingRails = 1760' `
    "Release evidence must freeze planned and saved rail counts"
Assert-Contains $evidence '$expectedRealDrawingToolEntities = $expectedRealDrawingRails' `
    "Release evidence must freeze rail-only tool entity counts"
Assert-Contains $evidence `
    '"95041A3827AC36BF021A354D3076215DCCE6ACC7A23203F23DAC7F896708D3E0"' `
    "Release evidence must bind the frozen source drawing SHA-256"
Assert-Contains $evidence `
    '$realDrawing.formalCandidateExecuted' `
    "Release evidence must reject execution of the formal candidate"
Assert-Contains $evidence `
    '$expectedExecutionAssemblyName = "FourJTools.RealDrawingVerify"' `
    "Release evidence must identify the internal execution assembly"
Assert-Contains $evidence `
    'same-source-internal-instrumentation' `
    "Release evidence must state the same-source verification basis"
Assert-Contains $evidence `
    '$realCandidateMvid = Get-AssemblyMvid $realCandidatePath' `
    "Release evidence must independently read the formal candidate MVID"
Assert-Contains $evidence `
    '$buildResultHash = (Get-FileHash -LiteralPath $buildResultPath' `
    "Release evidence must independently hash the final build result"
Assert-Contains $evidence `
    '[string]$_.series -eq "R23.1"' `
    "Release evidence must select the final R23.1 payload"
Assert-Contains $evidence `
    '[string]$build.sdkValidation.targetSeries -ne "R23.1"' `
    "Release evidence must bind the final build to R23.1 SDK provenance"
Assert-Contains $evidence 'Read-VerifiedHashManifest' `
    "Release evidence must independently verify SHA256SUMS files"
Assert-Contains $evidence '$runManifestEntries' `
    "Release evidence must verify the complete real-drawing run manifest"
Assert-Contains $evidence '$executionManifestEntries' `
    "Release evidence must verify the internal build manifest"
Assert-Contains $evidence `
    '$postScan.toolEntities.metadataV3Count' `
    "Release evidence must independently require full META_V3 coverage"
Assert-Contains $evidence `
    '$postScan.nativeDimensions.count -ne' `
    "Release evidence must independently require unchanged native dimensions"
Assert-Contains $evidence '$outputArchivePath = Join-Path $outputsRoot "archive"' `
    "Release evidence must recognize the versioned output archive"
Assert-Contains $evidence 'outputs/archive must contain release directories only.' `
    "Release evidence must validate archived output structure"
Assert-Contains $evidence `
    '[string]$realDrawing.sourceDrawing.sha256Before -ne $realSourceHash' `
    "Release evidence must independently compare the original DWG pre-hash"
Assert-Contains $evidence `
    '[string]$realDrawing.sourceDrawing.sha256After -ne $realSourceHash' `
    "Release evidence must independently compare the original DWG post-hash"
Assert-Contains $evidence 'executionBuildReceipt = Get-FileEvidence' `
    "Public evidence must hash-record the internal build receipt"
Assert-Contains $evidence 'commandLog = Get-FileEvidence' `
    "Public evidence must hash-record the command log"
Assert-Contains $evidence 'scanLog = Get-FileEvidence' `
    "Public evidence must hash-record the pre-scan log"
Assert-Contains $evidence 'workingDrawing = Get-FileEvidence' `
    "Public evidence must hash-record the modified working DWG"
Assert-Contains $evidence `
    'realDrawingVerification = $realDrawingVerificationEvidence' `
    "Public evidence must include the sanitized real-drawing evidence"

Assert-Contains $evidence 'Get-PublicSignatureMetadata $build.installerSignature' "Public evidence must remove local paths from installer signature messages"
Assert-Contains $evidence 'Get-PublicSignatureMetadata $payload.sourceSignature' "Public evidence must remove local paths from source signature messages"
Assert-Contains $evidence 'Get-PublicSignatureMetadata $payload.stagedSignature' "Public evidence must remove local paths from staged signature messages"
Assert-Contains $evidence '$publicSuccessMarker' "Public evidence must sanitize the verification-drive path from the marker"
Assert-Contains $evidence 'successMarkerSha256 = Get-TextSha256 $successMarker' "Public evidence must retain a hash anchor for the original marker"
Assert-Contains $evidence 'loadedReleaseDllPath = $relativeLoadedDllPath' "Public evidence must publish the loaded DLL as a project-relative path"
Assert-Contains $evidence 'Public release evidence contains a local absolute Windows path' "Evidence generation must fail if a local absolute path remains"
Assert-NotContains $evidence 'installerSignature = $build.installerSignature' "Public evidence must not copy raw signature status messages"

Write-Host "PASS $passed release traceability source tests"
