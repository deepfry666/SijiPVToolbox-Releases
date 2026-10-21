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

$records = New-Object Collections.Generic.List[string]
function Add-Pass([string]$Message) {
    $records.Add("PASS: $Message")
    Write-Host "PASS: $Message"
}
function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    Add-Pass $Message
}
function Get-TreeFingerprint([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return "<absent>"
    }
    $root = [IO.Path]::GetFullPath($Path).TrimEnd("\")
    $items = @(Get-ChildItem -LiteralPath $root -Recurse -File |
        ForEach-Object {
            $relative = $_.FullName.Substring($root.Length).TrimStart("\")
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            "$relative|$hash"
        } | Sort-Object)
    $text = $items -join "`n"
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString(
            $algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}
function Invoke-Executable([string]$Path, [string[]]$Arguments) {
    $process = Start-Process -FilePath $Path -ArgumentList $Arguments `
        -Wait -PassThru
    return $process.ExitCode
}
function Get-CurrentUninstaller([string]$RegistryPath) {
    if (-not (Test-Path -LiteralPath $RegistryPath)) {
        throw "The test Apps & Features entry is missing: $RegistryPath"
    }
    return ([string](Get-ItemProperty -LiteralPath $RegistryPath).UninstallString).Trim('"')
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

function Assert-ExactBundleFileSet([string]$BundlePath,
    [object[]]$PayloadFiles, [string]$Context) {
    $payloads = @($PayloadFiles)
    $names = @($payloads | ForEach-Object { [string]$_.name } | Sort-Object)
    $expectedNames = @("StructuralToolbox", "StructuralToolbox.Ballast",
        "StructuralToolbox.Contracts", "StructuralToolbox.Rail",
        "StructuralToolbox.Update", "StructuralToolbox.DrawingTranslation") | Sort-Object
    Assert-Condition ($payloads.Count -eq 6 -and
        @(Compare-Object $expectedNames $names).Count -eq 0 -and
        @($payloads | Where-Object { [string]$_.series -ne "R23.1" }).Count -eq 0) `
        "$Context declares the six R23.1 release payloads"
    $expectedFiles = @("PackageContents.xml") + @($payloads | ForEach-Object {
        ([string]$_.stagedPath).Replace("\", "/").TrimStart("/")
    })
    $actualFiles = @(Get-RelativeBundleFiles $BundlePath)
    Assert-Condition (Test-ExactFileSet $actualFiles $expectedFiles) `
        ("$Context contains only PackageContents.xml and six release DLLs; actual: " +
        ($actualFiles -join ", "))
}

$buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
$workPrefix = [IO.Path]::GetFullPath((Join-Path $ProjectRoot "work")).TrimEnd("\") + "\"
Assert-Condition ($buildRoot.StartsWith($workPrefix,
    [StringComparison]::OrdinalIgnoreCase)) "runtime test build is under work/"

$resultPath = Join-Path $buildRoot "build-result.json"
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
Assert-Condition ([bool]$result.testBuild) `
    "runtime tests accept only the isolated test installer"
$payloadOrigin = $result.payloadOrigin
Assert-Condition ($null -ne $payloadOrigin -and
    [string]$payloadOrigin.mode -eq "frozen-release-candidate") `
    "isolated installer uses a frozen release-candidate payload"
$referenceBuildResultPath = [IO.Path]::GetFullPath(
    [string]$payloadOrigin.referenceBuildResult)
Assert-Condition ($referenceBuildResultPath.StartsWith($workPrefix,
    [StringComparison]::OrdinalIgnoreCase) -and
    (Test-Path -LiteralPath $referenceBuildResultPath -PathType Leaf)) `
    "frozen release-candidate build result exists under work/"
$referenceBuildResultSha256 = (Get-FileHash `
    -LiteralPath $referenceBuildResultPath -Algorithm SHA256).Hash
Assert-Condition ($referenceBuildResultSha256 -eq
    [string]$payloadOrigin.referenceBuildResultSha256) `
    "frozen release-candidate build-result hash matches"
$referenceResult = Get-Content -LiteralPath $referenceBuildResultPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-Condition (-not [bool]$referenceResult.testBuild -and
    [int]$referenceResult.schemaVersion -ge 6 -and
    [bool]$referenceResult.unsignedDistributionBuild -and
    [string]$referenceResult.releaseKind -eq "unsigned-distribution") `
    "frozen reference is a schema-6 unsigned distribution candidate"
Assert-Condition ([string]$referenceResult.installerSha256 -eq
    [string]$payloadOrigin.referenceInstallerSha256) `
    "isolated installer records the frozen candidate EXE hash"
$installerPath = [string]$result.installer
Assert-Condition (Test-Path -LiteralPath $installerPath -PathType Leaf) `
    "isolated test installer exists"
$testBuildResultSha256 = (Get-FileHash -LiteralPath $resultPath `
    -Algorithm SHA256).Hash
$testInstallerSha256 = (Get-FileHash -LiteralPath $installerPath `
    -Algorithm SHA256).Hash
Assert-Condition ($testInstallerSha256 -eq [string]$result.installerSha256) `
    "isolated test installer hash matches its build result"
$releaseVersion = ([version]([string]$result.appVersion)).ToString(3)

$releasePath = Join-Path $PSScriptRoot "release-v$releaseVersion.json"
$release = Get-Content -LiteralPath $releasePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$frozenReleasePath = Join-Path $PSScriptRoot "release-v0.3.3.json"
Assert-Condition (Test-Path -LiteralPath $frozenReleasePath -PathType Leaf) `
    "v0.3.3 release definition exists for runtime identity checks"
$frozenRelease = Get-Content -LiteralPath $frozenReleasePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$stableIdentityFields = @(
    "installerAppId",
    "testInstallerAppId",
    "bundleProductCode",
    "testBundleProductCode",
    "bundleUpgradeCode",
    "testBundleUpgradeCode"
)
foreach ($field in $stableIdentityFields) {
    try {
        $frozenGuid = ([guid]([string]$frozenRelease.$field)).ToString("B")
        $currentGuid = ([guid]([string]$release.$field)).ToString("B")
    } catch {
        throw "FAIL: stable runtime release GUID '$field' is missing or invalid"
    }
    Assert-Condition ($frozenGuid -eq $currentGuid) `
        "runtime release GUID $field is stable from v0.3.3 to v$releaseVersion"
}
$acceptedV020ProductCode =
    ([guid]"{5C2AE40B-D8F5-4025-B75E-13C0AEEB1251}").ToString("B")
foreach ($definition in @($frozenRelease, $release)) {
    $legacyCodes = @($definition.acceptedLegacyProductCodes | ForEach-Object {
        try { ([guid]([string]$_)).ToString("B") } catch { "<invalid>" }
    })
    Assert-Condition ($legacyCodes.Count -eq 1 -and
        $legacyCodes[0] -eq $acceptedV020ProductCode) `
        "runtime accepts the v0.2.0 ProductCode in release $($definition.releaseVersion)"
}
$testAppId = ([guid]([string]$release.testInstallerAppId)).ToString("D").ToUpperInvariant()
$testProductCode = ([guid]([string]$release.testBundleProductCode)).ToString("B").ToUpperInvariant()
$testUpgradeCode = ([guid]([string]$release.testBundleUpgradeCode)).ToString("B").ToUpperInvariant()
$uninstallRegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\" +
    "{$testAppId}_is1"
$testRoot = Join-Path $env:LOCALAPPDATA "FourJTools\InstallerTest"
$installedBundle = Join-Path $testRoot `
    "ApplicationPlugins\ZhStructureToolbox.Test.bundle"
$installedManifest = Join-Path $installedBundle "PackageContents.xml"

$formalBundle = Join-Path $env:ProgramFiles `
    "Autodesk\ApplicationPlugins\ZhStructureToolbox.bundle"
$settingsPath = Join-Path $env:APPDATA "StructuralToolbox"
$formalBefore = Get-TreeFingerprint $formalBundle
$settingsBefore = Get-TreeFingerprint $settingsPath

$session = [DateTime]::Now.ToString("yyyyMMdd-HHmmss")
$runtimeLogRoot = Join-Path $buildRoot ("runtime-logs\" + $session)
New-Item -ItemType Directory -Path $runtimeLogRoot -Force | Out-Null
$manifestBackup = $null
$installedPayloadEvidence = New-Object Collections.Generic.List[object]

try {
    Assert-Condition (-not (Test-Path -LiteralPath $uninstallRegistryPath)) `
        "isolated test installation is clean before the run"
    Assert-Condition (-not (Test-Path -LiteralPath $installedBundle)) `
        "isolated test bundle is absent before the run"

    $cadRunning = @(Get-Process -Name "acad", "accoreconsole" `
        -ErrorAction SilentlyContinue).Count -gt 0
    if ($cadRunning) {
        $log = Join-Path $runtimeLogRoot "01-install-cad-open.log"
        $exitCode = Invoke-Executable $installerPath @(
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=$log")
        Assert-Condition ($exitCode -ne 0) `
            "installer rejects an update while AutoCAD is running"
        Assert-Condition (-not (Test-Path -LiteralPath $installedBundle)) `
            "running-CAD rejection performs no bundle write"
    } else {
        $records.Add("SKIP: running-CAD install rejection (AutoCAD was not running)")
    }

    $log = Join-Path $runtimeLogRoot "02-install-success.log"
    $exitCode = Invoke-Executable $installerPath @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
        "/ALLOWRUNNINGCAD=1", "/LOG=$log")
    Assert-Condition ($exitCode -eq 0) "per-user test installation succeeds"
    Assert-Condition (Test-Path -LiteralPath $installedManifest -PathType Leaf) `
        "bundle is installed in the isolated per-user location"
    Assert-Condition (Test-Path -LiteralPath $uninstallRegistryPath) `
        "Apps & Features registration is created"

    [xml]$manifest = Get-Content -LiteralPath $installedManifest -Raw -Encoding UTF8
    Assert-Condition (([string]$manifest.ApplicationPackage.ProductCode) -eq
        $testProductCode) "installed bundle has the isolated ProductCode"
    Assert-Condition (([string]$manifest.ApplicationPackage.UpgradeCode) -eq
        $testUpgradeCode) "installed bundle has the isolated UpgradeCode"
    Assert-Condition (([version]$manifest.ApplicationPackage.AppVersion) -eq
        [version]$result.appVersion) "installed bundle version matches the test payload"
    Assert-Condition ($null -eq $manifest.SelectSingleNode(
        "//Command[@Global='STPVRAILANNOTATE']")) `
        "generated bundle omits the retired rail-annotation command"

    foreach ($payload in @($result.payloadFiles)) {
        $referencePayload = @($referenceResult.payloadFiles | Where-Object {
            [string]$_.series -eq [string]$payload.series -and
            [string]$_.name -eq [string]$payload.name
        })
        Assert-Condition ($referencePayload.Count -eq 1 -and
            [string]$referencePayload[0].sourceSha256 -eq
                [string]$payload.sourceSha256 -and
            [string]$referencePayload[0].stagedSha256 -eq
                [string]$payload.stagedSha256) `
            "isolated module matches frozen candidate: $($payload.name)"
        $installedModule = Join-Path $installedBundle `
            ([string]$payload.stagedPath -replace "/", "\")
        Assert-Condition (Test-Path -LiteralPath $installedModule -PathType Leaf) `
            "installed module exists: $($payload.name)"
        $installedModuleSha256 = (Get-FileHash -LiteralPath $installedModule `
            -Algorithm SHA256).Hash
        Assert-Condition ($installedModuleSha256 -eq
            [string]$payload.stagedSha256) `
            "installed module hash matches frozen candidate: $($payload.name)"
        $installedPayloadEvidence.Add([pscustomobject]@{
            Series = [string]$payload.series
            Name = [string]$payload.name
            SHA256 = $installedModuleSha256
        })
    }
    Assert-ExactBundleFileSet $installedBundle @($result.payloadFiles) `
        "installed bundle after fresh install"

    $log = Join-Path $runtimeLogRoot "03-upgrade-success.log"
    $exitCode = Invoke-Executable $installerPath @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
        "/ALLOWRUNNINGCAD=1", "/LOG=$log")
    Assert-Condition ($exitCode -eq 0) "same-version in-place upgrade succeeds"
    Assert-ExactBundleFileSet $installedBundle @($result.payloadFiles) `
        "installed bundle after same-version upgrade"

    $beforeRollback = Get-TreeFingerprint $installedBundle
    $log = Join-Path $runtimeLogRoot "04-rollback.log"
    $exitCode = Invoke-Executable $installerPath @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
        "/ALLOWRUNNINGCAD=1", "/FAILAFTERBACKUP=1", "/LOG=$log")
    Assert-Condition ($exitCode -ne 0) "simulated post-backup failure aborts installation"
    Assert-Condition ((Get-TreeFingerprint $installedBundle) -eq $beforeRollback) `
        "simulated failure restores the prior bundle byte-for-byte"
    Assert-Condition (@(Get-ChildItem (Split-Path -Parent $installedBundle) `
        -Directory -Filter "ZhStructureToolbox.Test.bundle.backup.*" `
        -ErrorAction SilentlyContinue).Count -eq 0) `
        "rollback leaves no backup directory"

    $manifestBackup = [IO.File]::ReadAllBytes($installedManifest)
    [xml]$foreignManifest = Get-Content -LiteralPath $installedManifest `
        -Raw -Encoding UTF8
    $foreignManifest.ApplicationPackage.ProductCode =
        "{11111111-1111-1111-1111-111111111111}"
    $foreignManifest.Save($installedManifest)
    $foreignHash = (Get-FileHash -LiteralPath $installedManifest `
        -Algorithm SHA256).Hash
    $log = Join-Path $runtimeLogRoot "05-foreign-product.log"
    $exitCode = Invoke-Executable $installerPath @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
        "/ALLOWRUNNINGCAD=1", "/LOG=$log")
    Assert-Condition ($exitCode -ne 0) "unknown same-name ProductCode is rejected"
    Assert-Condition ((Get-FileHash -LiteralPath $installedManifest `
        -Algorithm SHA256).Hash -eq $foreignHash) `
        "unknown same-name bundle is preserved"
    [IO.File]::WriteAllBytes($installedManifest, $manifestBackup)
    $manifestBackup = $null

    $manifestBackup = [IO.File]::ReadAllBytes($installedManifest)
    [xml]$newerManifest = Get-Content -LiteralPath $installedManifest `
        -Raw -Encoding UTF8
    $newerManifest.ApplicationPackage.AppVersion = "9.0.0.0"
    $newerManifest.Save($installedManifest)
    $newerHash = (Get-FileHash -LiteralPath $installedManifest `
        -Algorithm SHA256).Hash
    $log = Join-Path $runtimeLogRoot "06-downgrade-reject.log"
    $exitCode = Invoke-Executable $installerPath @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
        "/ALLOWRUNNINGCAD=1", "/LOG=$log")
    Assert-Condition ($exitCode -ne 0) "ordinary downgrade is rejected"
    Assert-Condition ((Get-FileHash -LiteralPath $installedManifest `
        -Algorithm SHA256).Hash -eq $newerHash) `
        "rejected downgrade preserves the newer bundle"
    $log = Join-Path $runtimeLogRoot "07-downgrade-maintenance.log"
    $exitCode = Invoke-Executable $installerPath @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
        "/ALLOWRUNNINGCAD=1", "/ALLOWDOWNGRADE=1", "/LOG=$log")
    Assert-Condition ($exitCode -eq 0) `
        "explicit test maintenance parameter permits a confirmed downgrade"
    $manifestBackup = $null

    $uninstaller = Get-CurrentUninstaller $uninstallRegistryPath
    if ($cadRunning) {
        $log = Join-Path $runtimeLogRoot "08-uninstall-cad-open.log"
        $exitCode = Invoke-Executable $uninstaller @(
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=$log")
        Assert-Condition ($exitCode -ne 0) `
            "uninstaller rejects removal while AutoCAD is running"
        Assert-Condition (Test-Path -LiteralPath $installedBundle) `
            "running-CAD uninstall rejection preserves the bundle"
    } else {
        $records.Add("SKIP: running-CAD uninstall rejection (AutoCAD was not running)")
    }

    $uninstaller = Get-CurrentUninstaller $uninstallRegistryPath
    $log = Join-Path $runtimeLogRoot "09-uninstall-success.log"
    $exitCode = Invoke-Executable $uninstaller @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
        "/ALLOWRUNNINGCAD=1", "/LOG=$log")
    Assert-Condition ($exitCode -eq 0) "test uninstall succeeds"
    Start-Sleep -Seconds 1
    Assert-Condition (-not (Test-Path -LiteralPath $installedBundle)) `
        "uninstall removes the owned bundle"
    Assert-Condition (-not (Test-Path -LiteralPath $uninstallRegistryPath)) `
        "uninstall removes the Apps & Features registration"
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $testRoot "Program"))) `
        "uninstall removes installer-owned program metadata"

    Assert-Condition ((Get-TreeFingerprint $settingsPath) -eq $settingsBefore) `
        "install, upgrade, and uninstall preserve user settings"
    Assert-Condition ((Get-TreeFingerprint $formalBundle) -eq $formalBefore) `
        "isolated runtime tests never modify the formal installed bundle"
    $installedShell = @($installedPayloadEvidence | Where-Object {
        [string]$_.Name -eq "StructuralToolbox"
    })
    Assert-Condition ($installedPayloadEvidence.Count -eq 6 -and
        @($installedPayloadEvidence | Where-Object {
            [string]$_.Series -ne "R23.1"
        }).Count -eq 0 -and $installedShell.Count -eq 1) `
        "runtime receipt identifies all six installed R23.1 release files"
    $installedPayloadFilesSha256 = Get-PayloadSetSha256 `
        $installedPayloadEvidence.ToArray() "Name" "SHA256"
    $records.Add("RELEASE_VERSION=$releaseVersion")
    $records.Add("FROZEN_BUILD_RESULT_SHA256=$referenceBuildResultSha256")
    $records.Add("FROZEN_INSTALLER_SHA256=$($payloadOrigin.referenceInstallerSha256)")
    $records.Add("TEST_BUILD_RESULT_SHA256=$testBuildResultSha256")
    $records.Add("TEST_INSTALLER_SHA256=$testInstallerSha256")
    $records.Add("INSTALLED_PAYLOAD_SERIES=$($installedShell[0].Series)")
    $records.Add("INSTALLED_PAYLOAD_SHA256=$($installedShell[0].SHA256)")
    $records.Add("INSTALLED_PAYLOAD_FILE_COUNT=$($installedPayloadEvidence.Count)")
    $records.Add("INSTALLED_PAYLOAD_FILES_SHA256=$installedPayloadFilesSha256")
    Add-Pass "RUNTIME_VERIFICATION=PASS"
} finally {
    if ($null -ne $manifestBackup -and
        (Test-Path -LiteralPath $installedManifest -PathType Leaf)) {
        [IO.File]::WriteAllBytes($installedManifest, $manifestBackup)
    }

    if (Test-Path -LiteralPath $uninstallRegistryPath) {
        try {
            $cleanupUninstaller = Get-CurrentUninstaller $uninstallRegistryPath
            $cleanupLog = Join-Path $runtimeLogRoot "cleanup-uninstall.log"
            $null = Invoke-Executable $cleanupUninstaller @(
                "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
                "/ALLOWRUNNINGCAD=1", "/LOG=$cleanupLog")
        } catch {
            $records.Add("WARNING: automatic test cleanup failed: $($_.Exception.Message)")
        }
    }

    $runtimeVerificationPath = Join-Path $buildRoot "runtime-verification.log"
    $records | Set-Content -LiteralPath $runtimeVerificationPath -Encoding UTF8
    Write-Host "Runtime verification log: $runtimeVerificationPath"
    Write-Host "Detailed Inno logs: $runtimeLogRoot"
}
