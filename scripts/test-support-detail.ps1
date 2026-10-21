param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testRoot = Join-Path $projectRoot "work\support-detail-tests"
$testOutput = Join-Path $testRoot "SupportDetailGeometryTests.exe"
$uiTestOutput = Join-Path $testRoot "SupportDetailUiTests.exe"
$metadataTestOutput = Join-Path $testRoot `
    "SupportDetailMetadataCodecTests.exe"
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

$sources = @(
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\Settings\SupportDetailSettings.cs"),
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\Core\SupportDetailGeometry.cs"),
    (Join-Path $projectRoot "tests\SupportDetailGeometryTests.cs")
)

& $compiler /nologo /target:exe /optimize+ /warn:4 `
    "/out:$testOutput" $sources
if ($LASTEXITCODE -ne 0) {
    throw "Support detail test build failed with exit code $LASTEXITCODE"
}

& $testOutput
if ($LASTEXITCODE -ne 0) {
    throw "Support detail tests failed with exit code $LASTEXITCODE"
}

$metadataSources = @(
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\Settings\SupportDetailSettings.cs"),
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\Core\SupportDetailMetadataCodec.cs"),
    (Join-Path $projectRoot "tests\SupportDetailMetadataCodecTests.cs")
)

& $compiler /nologo /target:exe /optimize+ /warn:4 `
    /reference:System.dll /reference:System.Xml.dll `
    "/out:$metadataTestOutput" $metadataSources
if ($LASTEXITCODE -ne 0) {
    throw "Support detail metadata test build failed with exit code $LASTEXITCODE"
}

& $metadataTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Support detail metadata tests failed with exit code $LASTEXITCODE"
}

$uiSources = @(
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\Settings\SupportDetailSettings.cs"),
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\Core\SupportDetailGeometry.cs"),
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\UI\SupportDetailDiagram.cs"),
    (Join-Path $projectRoot `
        "src\StructuralToolbox.SupportDetail\UI\SupportDetailDialog.cs"),
    (Join-Path $projectRoot "tests\SupportDetailUiTests.cs")
)

& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 `
    /reference:System.dll /reference:System.Core.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/out:$uiTestOutput" $uiSources
if ($LASTEXITCODE -ne 0) {
    throw "Support detail UI test build failed with exit code $LASTEXITCODE"
}

& $uiTestOutput
if ($LASTEXITCODE -ne 0) {
    throw "Support detail UI tests failed with exit code $LASTEXITCODE"
}

& (Join-Path $projectRoot "tests\SupportDetailSourceTests.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Support detail source tests failed with exit code $LASTEXITCODE"
}
