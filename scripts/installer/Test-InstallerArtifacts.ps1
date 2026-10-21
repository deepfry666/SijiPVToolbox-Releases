[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BuildDirectory,

    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

$messages = New-Object Collections.Generic.List[string]
function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $messages.Add("PASS: $Message")
}

function Get-RelativeBundleFiles([string]$Root) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd("\")
    return @(Get-ChildItem -LiteralPath $rootPath -Recurse -File |
        ForEach-Object {
            $_.FullName.Substring($rootPath.Length).TrimStart("\").Replace("\", "/")
        } | Sort-Object)
}

function Test-ExactFileSet([string[]]$Actual, [string[]]$Expected) {
    if ($Actual.Count -ne $Expected.Count) {
        return $false
    }
    return @(Compare-Object -ReferenceObject @($Expected | Sort-Object) `
        -DifferenceObject @($Actual | Sort-Object)).Count -eq 0
}

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

function Get-SourceCodeFingerprint {
    $projectFullPath = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd("\")
    $sourceRoots = @(
        (Join-Path $projectFullPath "src\StructuralToolbox"),
        (Join-Path $projectFullPath "src\StructuralToolbox.Contracts"),
        (Join-Path $projectFullPath "src\StructuralToolbox.Rail"),
        (Join-Path $projectFullPath "src\StructuralToolbox.Ballast"),
        (Join-Path $projectFullPath "src\StructuralToolbox.ArrayRecognition"),
        (Join-Path $projectFullPath "src\StructuralToolbox.Update"),
        (Join-Path $projectFullPath "src\StructuralToolbox.DrawingTranslation")
    )
    $sourceFiles = @($sourceRoots | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -File
    })
    $items = @($sourceFiles |
        Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($projectFullPath.Length).TrimStart("\")
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

$buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
$resultPath = Join-Path $buildRoot "build-result.json"
Assert-Condition (Test-Path -LiteralPath $resultPath -PathType Leaf) `
    "build-result.json exists"
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
Assert-Condition ([int]$result.schemaVersion -ge 6) `
    "build result uses the schema-6 multi-module provenance chain"
Assert-Condition ((Get-SourceCodeFingerprint) -eq [string]$result.sourceCodeSha256) `
    "source code is unchanged since the installer build"

$installerPath = [string]$result.installer
$bundlePath = [string]$result.bundle
Assert-Condition (Test-Path -LiteralPath $installerPath -PathType Leaf) `
    "compiled installer exists"
Assert-Condition ((Get-Item -LiteralPath $installerPath).Length -gt 1MB) `
    "compiled installer is a nontrivial PE artifact"
Assert-Condition ((Get-FileHash -LiteralPath $installerPath `
    -Algorithm SHA256).Hash -eq [string]$result.installerSha256) `
    "compiled installer hash matches build-result.json"
Assert-Condition (Test-Path -LiteralPath $bundlePath -PathType Container) `
    "staged bundle exists"

$inventoryPath = [string]$result.bundleInventory
Assert-Condition (Test-Path -LiteralPath $inventoryPath -PathType Leaf) `
    "bundle inventory exists"
Assert-Condition ((Get-FileHash -LiteralPath $inventoryPath `
    -Algorithm SHA256).Hash -eq [string]$result.bundleInventorySha256) `
    "bundle inventory hash matches build-result.json"
$inventory = Get-Content -LiteralPath $inventoryPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
Assert-Condition ([int]$inventory.schemaVersion -ge 2) `
    "bundle inventory uses the source/staged hash schema"
$provenancePath = [string]$result.buildProvenance
Assert-Condition (Test-Path -LiteralPath $provenancePath -PathType Leaf) `
    "SDK build provenance exists"
Assert-Condition ((Get-FileHash -LiteralPath $provenancePath `
    -Algorithm SHA256).Hash -eq [string]$result.buildProvenanceSha256 -and
    [string]$inventory.buildProvenanceSha256 -eq
        [string]$result.buildProvenanceSha256) `
    "SDK build provenance hash is chained through inventory and build result"

$manifestPath = Join-Path $bundlePath "PackageContents.xml"
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
$manifestVersion = [version]([string]$manifest.ApplicationPackage.AppVersion)
Assert-Condition ($manifestVersion -eq [version]([string]$result.appVersion)) `
    "installer, inventory, and bundle use the same version"
Assert-Condition ([version]([string]$inventory.appVersion) -eq $manifestVersion) `
    "bundle inventory version matches the staged manifest"
Assert-Condition ((Get-FileHash -LiteralPath $manifestPath `
    -Algorithm SHA256).Hash -eq [string]$result.bundleManifestSha256) `
    "staged manifest hash matches build-result.json"

$matrixPath = Join-Path $ProjectRoot `
    "bundle\manifest-template\compatibility-matrix.json"
$matrix = Get-Content -LiteralPath $matrixPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$verifiedSeries = @($matrix.targets |
    Where-Object { $_.status -eq "verified" } |
    ForEach-Object { [string]$_.series })
$verifiedTargets = @($matrix.targets | Where-Object { $_.status -eq "verified" })
Assert-Condition ($verifiedTargets.Count -eq 1) `
    "current pipeline has exactly one verified AutoCAD target"
$verifiedTarget = $verifiedTargets[0]
Assert-Condition (-not [string]::IsNullOrWhiteSpace(
        [string]$verifiedTarget.buildProfile) -and
    -not [string]::IsNullOrWhiteSpace(
        [string]$verifiedTarget.managedApiVersion)) `
    "verified target declares build profile and managed API version"
$sdkValidation = $result.sdkValidation
Assert-Condition ($null -ne $sdkValidation -and
    [string]$sdkValidation.validation -eq "passed" -and
    [string]$sdkValidation.targetSeries -eq [string]$verifiedTarget.series -and
    [int]$sdkValidation.targetYear -eq [int]$verifiedTarget.year -and
    [string]$sdkValidation.buildProfile -eq [string]$verifiedTarget.buildProfile) `
    "build result binds SDK provenance to the verified target"
$coreSdkFiles = @($sdkValidation.sdkFiles | Where-Object {
    $_.name -in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")
})
Assert-Condition ($coreSdkFiles.Count -eq 3) `
    "SDK provenance records all three core managed APIs"
foreach ($sdkFile in $coreSdkFiles) {
    $apiVersion = [version]([string]$sdkFile.assemblyVersion)
    Assert-Condition (("{0}.{1}" -f $apiVersion.Major, $apiVersion.Minor) -eq
        [string]$verifiedTarget.managedApiVersion -and
        [string]$sdkFile.signatureStatus -eq "Valid" -and
        ([string]$sdkFile.signerSubject).Contains("Autodesk")) `
        "SDK API $($sdkFile.name) matches Series and Autodesk signature"
}
$candidateSeries = @($matrix.targets |
    Where-Object { $_.status -eq "candidate" } |
    ForEach-Object { [string]$_.series })
$components = @($manifest.ApplicationPackage.Components)
$activeSeries = @($components | ForEach-Object {
    [string]$_.RuntimeRequirements.SeriesMin
})
Assert-Condition ($activeSeries.Count -eq $verifiedSeries.Count) `
    "release manifest activates only the verified target count"
foreach ($series in $verifiedSeries) {
    Assert-Condition ($activeSeries -contains $series) `
        "verified series $series is active"
}
foreach ($series in $candidateSeries) {
    Assert-Condition (-not ($activeSeries -contains $series)) `
        "candidate series $series is not active"
}

$expectedBundleFiles = New-Object Collections.Generic.List[string]
$expectedBundleFiles.Add("PackageContents.xml")
foreach ($component in $components) {
    $series = [string]$component.RuntimeRequirements.SeriesMin
    Assert-Condition ($series -eq [string]$component.RuntimeRequirements.SeriesMax) `
        "component $series is bounded to one AutoCAD series"
    $inventoryTarget = @($inventory.targets |
        Where-Object { [string]$_.series -eq $series })
    Assert-Condition ($inventoryTarget.Count -eq 1) `
        "inventory contains exactly one component for $series"
    $resultTarget = @($result.payloadModules |
        Where-Object { [string]$_.series -eq $series })
    Assert-Condition ($resultTarget.Count -eq 1) `
        "build result contains exactly one component for $series"
    $inventoryFiles = @($inventoryTarget[0].files)
    $resultFiles = @($result.payloadFiles |
        Where-Object { [string]$_.series -eq $series })
    Assert-Condition ($inventoryFiles.Count -eq 6 -and $resultFiles.Count -eq 6) `
        "component $series contains all six approved release files"
    $projectPrefix = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd("\") + "\"
    $bundlePrefix = [IO.Path]::GetFullPath($bundlePath).TrimEnd("\") + "\"
    foreach ($file in $inventoryFiles) {
        $sourceModule = [IO.Path]::GetFullPath((Join-Path $ProjectRoot `
            ([string]$file.sourcePath -replace "/", "\")))
        $moduleName = ([string]$file.modulePath -replace "/", "\")
        $modulePath = [IO.Path]::GetFullPath((Join-Path $bundlePath $moduleName))
        $resultFile = @($resultFiles | Where-Object {
            [string]$_.name -eq [string]$file.name
        })
        Assert-Condition ($resultFile.Count -eq 1) `
            "result contains module $($file.name) for $series"
        Assert-Condition ($sourceModule.StartsWith($projectPrefix,
            [StringComparison]::OrdinalIgnoreCase) -and
            $modulePath.StartsWith($bundlePrefix,
            [StringComparison]::OrdinalIgnoreCase)) `
            "module $($file.name) stays inside project and bundle roots"
        Assert-Condition ((Test-Path -LiteralPath $sourceModule -PathType Leaf) -and
            (Test-Path -LiteralPath $modulePath -PathType Leaf)) `
            "module $($file.name) source and staged files exist"
        $sourceHash = (Get-FileHash -LiteralPath $sourceModule -Algorithm SHA256).Hash
        $stagedHash = (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash
        Assert-Condition ($sourceHash -eq [string]$file.sourceSha256 -and
            $stagedHash -eq [string]$file.stagedSha256 -and
            $sourceHash -eq $stagedHash -and
            [string]$resultFile[0].sourceSha256 -eq $sourceHash -and
            [string]$resultFile[0].stagedSha256 -eq $stagedHash) `
            "module $($file.name) hash chain is complete"
        Assert-Condition ([Reflection.AssemblyName]::GetAssemblyName(
            $modulePath).Version -eq $manifestVersion) `
            "module $($file.name) assembly version matches manifest"
        $expectedBundleFiles.Add($moduleName.Replace("\", "/"))
    }
    $entryLeaves = @($component.ComponentEntry | ForEach-Object {
        Split-Path -Leaf (([string]$_.ModuleName).Replace("/", "\"))
    })
    Assert-Condition ($entryLeaves.Count -eq 4 -and
        $entryLeaves -contains "StructuralToolbox.dll" -and
        $entryLeaves -contains "StructuralToolbox.Rail.dll" -and
        $entryLeaves -contains "StructuralToolbox.Ballast.dll" -and
        $entryLeaves -contains "StructuralToolbox.DrawingTranslation.dll" -and
        $entryLeaves -notcontains "StructuralToolbox.SupportDetail.dll") `
        "manifest declares only shell, rail, ballast, and drawing translation command assemblies"
}
$actualBundleFiles = @(Get-RelativeBundleFiles $bundlePath)
Assert-Condition (Test-ExactFileSet $actualBundleFiles `
        @($expectedBundleFiles.ToArray())) `
    ("staged bundle contains only PackageContents.xml and six release DLLs; actual: " +
    ($actualBundleFiles -join ", "))

$issPath = Join-Path $PSScriptRoot "SijiPVToolbox.iss"
$iss = Get-Content -LiteralPath $issPath -Raw -Encoding UTF8
$requiredFragments = @(
    "PrivilegesRequired=lowest",
    "PrivilegesRequiredOverridesAllowed=dialog commandline",
    "{userappdata}\Autodesk\ApplicationPlugins",
    "{autopf}\Autodesk\ApplicationPlugins",
    "acad.exe",
    "acdbmgd.dll",
    "VersionMatchesSeries",
    "VersionMatchesSeries(FileVersion, Series)",
    "VersionMatchesSeries(ProductVersion, Series)",
    "AutoCADExecutableVersionMatches",
    "System.Software.ProductVersion",
    "ReleaseVersion",
    "ManagedApiVersion",
    "CanInstall := IdentityMatches and ListContains(VerifiedSeriesList, Series)",
    "ConclusionText := '可以安装'",
    "ConclusionText := '当前不支持'",
    "请先启动或重启 AutoCAD，再在命令行输入 JJJJ",
    "DisableWelcomePage=yes",
    "DisableReadyPage=yes",
    "安装信息",
    "主要功能",
    "自动画导轨",
    "自动布置配重",
    "导轨长度和配重数量统计",
    "不提供自定义安装地址",
    "仅为我安装",
    "为所有用户安装",
    "已有版本会自动升级",
    "accoreconsole.exe",
    "IsAcceptedProductCode",
    "OtherInstallScopeMessage",
    "BackupBundle",
    "RestoreBackup",
    "InitializeUninstall",
    "UninstallDisplayName"
)
foreach ($fragment in $requiredFragments) {
    Assert-Condition ($iss.Contains($fragment)) `
        "installer source contains required control: $fragment"
}
Assert-Condition (-not $iss.Contains("TEST_UNSIGNED") -and
    -not $iss.Contains("未签名测试版")) `
    "installer UI contains no internal test-release classification"
$filesSection = [regex]::Match($iss,
    '(?ms)^\[Files\]\s*(?<body>.*?)(?=^\[|\z)')
$fileSourceEntryCount = if ($filesSection.Success) {
    [regex]::Matches($filesSection.Groups["body"].Value,
        '(?im)^\s*Source\s*:').Count
} else {
    0
}
Assert-Condition ($filesSection.Success -and $fileSourceEntryCount -eq 1 -and
    $filesSection.Groups["body"].Value.Contains(
        'Source: "{#PayloadBundleDir}\*"') -and
    -not $iss.Contains("[Icons]")) `
    "installer embeds exactly one source: the validated AutoCAD Bundle payload"
Assert-Condition (-not ($iss -match "(?i)SetSystemVariable\s*\(")) `
    "installer does not set AutoCAD system variables"
Assert-Condition (-not ($iss -match "(?i)RegWrite.*(SECURELOAD|TRUSTEDPATHS)")) `
    "installer does not write SECURELOAD or TRUSTEDPATHS"

$buildVersion = [version]([string]$result.appVersion)
$releaseVersion = "{0}.{1}.{2}" -f $buildVersion.Major,
    $buildVersion.Minor, [Math]::Max(0, $buildVersion.Build)
$releasePath = Join-Path $PSScriptRoot "release-v$releaseVersion.json"
$release = Get-Content -LiteralPath $releasePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$frozenReleasePath = Join-Path $PSScriptRoot "release-v0.3.3.json"
Assert-Condition (Test-Path -LiteralPath $frozenReleasePath -PathType Leaf) `
    "v0.3.3 release definition exists for stable identity checks"
$frozenRelease = Get-Content -LiteralPath $frozenReleasePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
Assert-Condition (
    [version]([string]$frozenRelease.releaseVersion) -eq [version]"0.3.3" -and
    ([version]([string]$release.releaseVersion)).ToString(3) -eq
        $releaseVersion) `
    "stable identity check binds v0.3.3 to current v$releaseVersion"
$expectedStableGuids = [ordered]@{
    installerAppId = "{0DB1FAD5-EC98-434F-8AF6-E16B3462A678}"
    testInstallerAppId = "{9AB1205B-9640-4A83-A9A0-912285618A0D}"
    bundleProductCode = "{9C42F595-68E0-4945-B8D7-62BE70975D5F}"
    testBundleProductCode = "{6069B95D-EFB6-431B-8275-CAC66D2B19C4}"
    bundleUpgradeCode = "{7D31C7E3-7805-4611-9B3E-5946775A86EC}"
    testBundleUpgradeCode = "{2451814C-C7C6-46AC-A303-422227D5FE09}"
}
foreach ($field in $expectedStableGuids.Keys) {
    $expectedGuid = ([guid]$expectedStableGuids[$field]).ToString("B")
    try {
        $frozenGuid = ([guid]([string]$frozenRelease.$field)).ToString("B")
        $currentGuid = ([guid]([string]$release.$field)).ToString("B")
    } catch {
        throw "FAIL: stable release GUID '$field' is missing or invalid"
    }
    Assert-Condition ($frozenGuid -eq $expectedGuid -and
        $currentGuid -eq $expectedGuid) `
        "stable release GUID $field is unchanged from v0.3.3 to v$releaseVersion"
}
$acceptedV020ProductCode =
    ([guid]"{5C2AE40B-D8F5-4025-B75E-13C0AEEB1251}").ToString("B")
foreach ($definition in @($frozenRelease, $release)) {
    $legacyCodes = @($definition.acceptedLegacyProductCodes | ForEach-Object {
        try { ([guid]([string]$_)).ToString("B") } catch { "<invalid>" }
    })
    Assert-Condition ($legacyCodes.Count -eq 1 -and
        $legacyCodes[0] -eq $acceptedV020ProductCode) `
        "accepted v0.2.0 ProductCode is retained by release $($definition.releaseVersion)"
}
$ids = @(
    [string]$release.installerAppId,
    [string]$release.bundleProductCode,
    [string]$release.bundleUpgradeCode
)
foreach ($id in $ids) {
    $parsed = [guid]::Empty
    Assert-Condition ([guid]::TryParse($id, [ref]$parsed) -and
        $parsed -ne [guid]::Empty) "release identity is a non-empty GUID: $id"
}

$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
$expectedReleaseKind = if ([bool]$result.testBuild) {
    "isolated-installer-test"
} elseif ([bool]$result.unsignedDistributionBuild) {
    "unsigned-distribution"
} else {
    "signed-release"
}
Assert-Condition ([string]$result.releaseKind -eq $expectedReleaseKind) `
    "release kind matches the build mode"
Assert-Condition ([bool]$result.signed -eq ($signature.Status -eq
    [Management.Automation.SignatureStatus]::Valid)) `
    "installer signature status matches build-result.json"
if ([bool]$result.testBuild) {
    Assert-Condition ([string]$result.payloadOrigin.mode -eq
        "frozen-release-candidate") `
        "isolated installer payload is frozen from a release candidate"
    Assert-Condition ((Split-Path -Leaf $installerPath) -like
        "*_INSTALLER_TEST.exe") `
        "isolated installer filename identifies installer testing"
} elseif ([bool]$result.unsignedDistributionBuild) {
    Assert-Condition ([string]$result.payloadOrigin.mode -eq "fresh-sdk-build") `
        "UNSIGNED distribution comes from a fresh SDK-validated build"
    Assert-Condition ((Split-Path -Leaf $installerPath) -eq
        "四季光伏工具箱_Setup_v$releaseVersion.exe") `
        "unsigned installer uses the normal transferable filename"
    $installerVersionInfo = (Get-Item -LiteralPath $installerPath).VersionInfo
    Assert-Condition (-not ([string]$installerVersionInfo.ProductName).Contains(
            "TEST_UNSIGNED") -and
        -not ([string]$installerVersionInfo.FileDescription).Contains(
            "TEST_UNSIGNED") -and
        ([string]$installerVersionInfo.ProductName).Trim() -eq "四季光伏工具箱") `
        "unsigned installer PE metadata uses the normal product identity"
    Assert-Condition ($signature.Status -eq
        [Management.Automation.SignatureStatus]::NotSigned -and
        [string]$result.signatureClassification -eq "NotSigned" -and
        [string]$result.authenticodeExpectation -eq "NotSigned" -and
        -not [bool]$result.signed) `
        "unsigned installer truthfully records Authenticode NotSigned"
} else {
    Assert-Condition ((Split-Path -Leaf $installerPath) -notlike
        "*TEST_UNSIGNED*") `
        "formal installer filename contains no test-release label"
}
if ([bool]$result.testBuild -or
    ($result.PSObject.Properties.Name -contains "unsignedDistributionBuild" -and
        [bool]$result.unsignedDistributionBuild)) {
    Assert-Condition ($buildRoot.StartsWith(
        ([IO.Path]::GetFullPath((Join-Path $ProjectRoot "work")).TrimEnd("\") + "\"),
        [StringComparison]::OrdinalIgnoreCase)) `
        "non-release installer remains under work/"
    $messages.Add("INFO: non-release installer signature status is $($signature.Status)")
} else {
    Assert-Condition ($signature.Status -eq
        [Management.Automation.SignatureStatus]::Valid) `
        "formal installer has a valid Authenticode signature"
    $payloadSignScriptPath = [string]$result.payloadSignScriptPath
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($payloadSignScriptPath) -and
        (Test-Path -LiteralPath $payloadSignScriptPath -PathType Leaf)) `
        "formal build records its payload signing script"
    Assert-Condition ((Get-FileHash -LiteralPath $payloadSignScriptPath `
        -Algorithm SHA256).Hash -eq [string]$result.payloadSignScriptSha256) `
        "payload signing script hash matches build-result.json"
}

$sourceManifestPath = Join-Path $ProjectRoot `
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml"
$currentSourceHash = (Get-FileHash -LiteralPath $sourceManifestPath `
    -Algorithm SHA256).Hash
Assert-Condition ($currentSourceHash -eq [string]$result.sourceManifestSha256) `
    "source PackageContents.xml is unchanged since this build"

$inputHashes = @(
    [pscustomobject]@{
        Path = (Join-Path $ProjectRoot "scripts\build.ps1")
        Expected = [string]$result.buildScriptSha256
        Name = "build script"
    },
    [pscustomobject]@{
        Path = (Join-Path $ProjectRoot `
            "scripts\package-installer.ps1")
        Expected = [string]$result.packagerSha256
        Name = "installer packager"
    },
    [pscustomobject]@{
        Path = (Join-Path $ProjectRoot "scripts\installer\New-MultiVersionBundle.ps1")
        Expected = [string]$result.bundleBuilderSha256
        Name = "bundle builder"
    },
    [pscustomobject]@{
        Path = (Join-Path $ProjectRoot `
            "bundle\manifest-template\PackageContents.base.xml")
        Expected = [string]$result.manifestTemplateSha256
        Name = "manifest template"
    },
    [pscustomobject]@{
        Path = $matrixPath
        Expected = [string]$result.compatibilityMatrixSha256
        Name = "compatibility matrix"
    },
    [pscustomobject]@{
        Path = $releasePath
        Expected = [string]$result.releaseDefinitionSha256
        Name = "release definition"
    },
    [pscustomobject]@{
        Path = $issPath
        Expected = [string]$result.installerSourceSha256
        Name = "installer source"
    },
    [pscustomobject]@{
        Path = (Join-Path $ProjectRoot `
            "docs\安装与兼容性说明_v$releaseVersion.md")
        Expected = [string]$result.installGuideSha256
        Name = "embedded install guide"
    }
)
foreach ($input in $inputHashes) {
    Assert-Condition (Test-Path -LiteralPath $input.Path -PathType Leaf) `
        "$($input.Name) exists"
    Assert-Condition ((Get-FileHash -LiteralPath $input.Path `
        -Algorithm SHA256).Hash -eq $input.Expected) `
        "$($input.Name) is unchanged since this build"
}

$verifiedPayloads = @($result.payloadModules | Where-Object {
    [string]$_.series -eq "R23.1"
})
$verifiedPayloadFiles = @($result.payloadFiles | Where-Object {
    [string]$_.series -eq "R23.1"
})
Assert-Condition ($verifiedPayloads.Count -eq 1) `
    "artifact receipt identifies exactly one R23.1 payload"
Assert-Condition ($verifiedPayloadFiles.Count -eq 6) `
    "artifact receipt identifies all six R23.1 release files"
$payloadFilesSha256 = Get-PayloadSetSha256 $verifiedPayloadFiles `
    "name" "sourceSha256"
$messages.Add("RELEASE_VERSION=$releaseVersion")
$messages.Add("SOURCE_CODE_SHA256=$($result.sourceCodeSha256)")
$messages.Add("BUILD_RESULT_SHA256=$((Get-FileHash -LiteralPath $resultPath `
    -Algorithm SHA256).Hash)")
$messages.Add("INSTALLER_SHA256=$($result.installerSha256)")
$messages.Add("PAYLOAD_SHA256=$($verifiedPayloads[0].sourceSha256)")
$messages.Add("PAYLOAD_FILE_COUNT=$($verifiedPayloadFiles.Count)")
$messages.Add("PAYLOAD_FILES_SHA256=$payloadFilesSha256")
$messages.Add("PASS: INSTALLER_ARTIFACT_VERIFICATION=PASS")

$logPath = Join-Path $buildRoot "verification.log"
$messages | Set-Content -LiteralPath $logPath -Encoding UTF8
$messages | ForEach-Object { Write-Host $_ }
Write-Host "Verification log: $logPath"
