param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "ReleaseTraceabilityAtomic.ps1")
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $projectRoot "work\tests"
$laneSolverTestOutput = Join-Path $testDirectory `
    "DeterministicLaneSolverTests.exe"
$layoutTestOutput = Join-Path $testDirectory "RailLayoutEngineTests.exe"
$fingerprintTestOutput = Join-Path $testDirectory "GeometryFingerprintTests.exe"
$metadataCodecTestOutput = Join-Path $testDirectory "ToolMetadataCodecTests.exe"
$overlapSolverTestOutput = Join-Path $testDirectory `
    "AnnotationOverlapSolverTests.exe"
$settingsTestOutput = Join-Path $testDirectory "RailSettingsTests.exe"
$uiLayoutTestOutput = Join-Path $testDirectory "ToolboxUiLayoutTests.exe"
$quantityTestOutput = Join-Path $testDirectory "QuantityStatisticsTests.exe"
$updateCoreTestOutput = Join-Path $testDirectory "UpdateCoreTests.exe"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null

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

function Invoke-RegressionScript([string]$Path) {
    $powerShellPath = if ($PSVersionTable.PSEdition -eq "Core") { (Get-Command pwsh -ErrorAction Stop).Source } else { (Get-Command powershell.exe -ErrorAction Stop).Source }
    & $powerShellPath -NoProfile -ExecutionPolicy Bypass -File $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Regression script failed with exit code $LASTEXITCODE`: $Path"
    }
}

Invoke-RegressionScript (Join-Path $projectRoot `
    "scripts\test-array-recognition.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "scripts\test-ballast.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "scripts\test-drawing-translation.ps1")

$laneSolverSources = @(
    (Join-Path $projectRoot `
        "src\StructuralToolbox.Rail\Core\DeterministicLaneSolver.cs"),
    (Join-Path $projectRoot "tests\DeterministicLaneSolverTests.cs")
)

& $compiler /nologo /target:exe /optimize+ `
    "/out:$laneSolverTestOutput" $laneSolverSources
if ($LASTEXITCODE -ne 0) {
    throw "Deterministic lane solver test build failed with exit code $LASTEXITCODE"
}

& $laneSolverTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Deterministic lane solver tests failed with exit code $LASTEXITCODE"
}

$layoutSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\Geometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\RailLayoutEngine.cs"),
    (Join-Path $projectRoot "tests\RailLayoutEngineTests.cs")
)

& $compiler /nologo /target:exe /optimize+ "/out:$layoutTestOutput" $layoutSources
if ($LASTEXITCODE -ne 0) {
    throw "Rail layout test build failed with exit code $LASTEXITCODE"
}

& $layoutTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Rail layout tests failed with exit code $LASTEXITCODE"
}

$fingerprintSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\Geometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\GeometryFingerprint.cs"),
    (Join-Path $projectRoot "tests\GeometryFingerprintTests.cs")
)

& $compiler /nologo /target:exe /optimize+ `
    "/out:$fingerprintTestOutput" $fingerprintSources
if ($LASTEXITCODE -ne 0) {
    throw "Geometry fingerprint test build failed with exit code $LASTEXITCODE"
}

& $fingerprintTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Geometry fingerprint tests failed with exit code $LASTEXITCODE"
}

$metadataCodecSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\ToolMetadataCodec.cs"),
    (Join-Path $projectRoot "tests\ToolMetadataCodecTests.cs")
)

& $compiler /nologo /target:exe /optimize+ `
    "/out:$metadataCodecTestOutput" $metadataCodecSources
if ($LASTEXITCODE -ne 0) {
    throw "Tool metadata codec test build failed with exit code $LASTEXITCODE"
}

& $metadataCodecTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Tool metadata codec tests failed with exit code $LASTEXITCODE"
}

$overlapSolverSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\AnnotationOverlapSolver.cs"),
    (Join-Path $projectRoot "tests\AnnotationOverlapSolverTests.cs")
)

& $compiler /nologo /target:exe /optimize+ `
    "/out:$overlapSolverTestOutput" $overlapSolverSources
if ($LASTEXITCODE -ne 0) {
    throw "Annotation overlap solver test build failed with exit code $LASTEXITCODE"
}

& $overlapSolverTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Annotation overlap solver tests failed with exit code $LASTEXITCODE"
}

$settingsSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Settings\RailSettings.cs"),
    (Join-Path $projectRoot "tests\RailSettingsTests.cs")
)

& $compiler /nologo /target:exe /optimize+ /reference:System.Xml.dll `
    "/out:$settingsTestOutput" $settingsSources
if ($LASTEXITCODE -ne 0) {
    throw "Rail settings test build failed with exit code $LASTEXITCODE"
}

& $settingsTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Rail settings tests failed with exit code $LASTEXITCODE"
}

$quantitySources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastStatistics.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Core\RailStatistics.cs"),
    (Join-Path $projectRoot "tests\QuantityStatisticsTests.cs")
)

& $compiler /nologo /target:exe /optimize+ `
    "/out:$quantityTestOutput" $quantitySources
if ($LASTEXITCODE -ne 0) {
    throw "Quantity statistics test build failed with exit code $LASTEXITCODE"
}

& $quantityTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Quantity statistics tests failed with exit code $LASTEXITCODE"
}

$updateCoreSources = @(
    (Join-Path $projectRoot "src\StructuralToolbox.Update\Core\UpdateException.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Update\Core\UpdateManifest.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Update\Core\UpdateManifestParser.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Update\Core\UpdateService.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Update\Core\VersionComparer.cs"),
    (Join-Path $projectRoot "tests\UpdateCoreTests.cs")
)

& $compiler /nologo /target:exe /optimize+ /reference:System.Core.dll `
    "/out:$updateCoreTestOutput" $updateCoreSources
if ($LASTEXITCODE -ne 0) {
    throw "Update core test build failed with exit code $LASTEXITCODE"
}

& $updateCoreTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Update core tests failed with exit code $LASTEXITCODE"
}

$uiLayoutSources = @(
    (Join-Path $projectRoot "tests\AutoCadUiStubs.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Contracts\IToolboxModule.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Contracts\ToolboxNavigation.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Settings\RailSettings.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\Settings\RailSettingsStore.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Core\BallastGeometry.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Settings\BallastSettings.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\Settings\BallastSettingsStore.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\UI\BallastPlacementDiagram.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\UI\BallastArrayGapDiagram.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\UI\ToolboxControl.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Rail\RailToolboxModule.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\UI\BallastToolboxControl.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\UI\BallastWorkflowBridge.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox.Ballast\BallastToolboxModule.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox\UI\ToolboxHomeControl.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox\UI\ToolboxModuleLoader.cs"),
    (Join-Path $projectRoot "src\StructuralToolbox\UI\ToolboxShellControl.cs"),
    (Join-Path $projectRoot "tests\ToolboxUiLayoutTests.cs")
)
$uiLayoutSources += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.Update") -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -ne "AssemblyInfo.cs" } |
    Sort-Object FullName | ForEach-Object { $_.FullName })

& $compiler /nologo /target:exe /platform:x64 /optimize+ `
    /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Xml.dll `
    "/out:$uiLayoutTestOutput" $uiLayoutSources
if ($LASTEXITCODE -ne 0) {
    throw "Toolbox UI layout test build failed with exit code $LASTEXITCODE"
}

& $uiLayoutTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Toolbox UI layout tests failed with exit code $LASTEXITCODE"
}

& (Join-Path $projectRoot "scripts\test-ballast-diagram.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Ballast placement diagram test script failed with exit code $LASTEXITCODE"
}

& (Join-Path $projectRoot "scripts\test-support-detail.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Support detail test script failed with exit code $LASTEXITCODE"
}

Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\RailUpdateSafetySourceTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\RailAnnotationRetirementTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\ModuleIsolationTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\ReleaseTraceabilitySourceTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\LegacyEntrypointSafetyTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\PublishAtomicityTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\ReleaseTraceabilityAtomicityTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\ReleaseArchiveVerifierTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\BuildCompatibilityTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\ReleaseWorkflowTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\ProjectWorkCleanupTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\GitHubUpdateReleaseTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\UpdateServiceTests.ps1")
Invoke-RegressionScript (Join-Path $projectRoot `
    "tests\UpdateUiResponsivenessSourceTests.ps1")

Write-Host "RELEASE_VERSION=1.0.8"
Write-Host ("SOURCE_CODE_SHA256=" + (Get-SourceCodeFingerprint))
Write-Host ("REGRESSION_SUITE_SHA256=" +
    (Get-RegressionSuiteFingerprint $projectRoot))
Write-Host "PURE_CODE_REGRESSION=PASS"
