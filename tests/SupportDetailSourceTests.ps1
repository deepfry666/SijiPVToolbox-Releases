param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.SupportDetail"
$passed = 0

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:passed++
}

$project = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "StructuralToolbox.SupportDetail.csproj") -Raw -Encoding UTF8
Assert-Condition ($project.Contains("StructuralToolbox.Contracts") -and
    -not $project.Contains("StructuralToolbox.Rail") -and
    -not $project.Contains("StructuralToolbox.Ballast") -and
    -not $project.Contains("StructuralToolbox.Layout")) `
    "support detail uses only the stable toolbox contract"

$module = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "SupportDetailToolboxModule.cs") -Raw -Encoding UTF8
$toolboxPage = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "UI\SupportDetailToolboxControl.cs") -Raw -Encoding UTF8
Assert-Condition ($module.Contains('Id { get { return "support-detail"; } }') -and
    $module.Contains('HomeButtonName { get { return "OpenSupportDetailTool"; } }') -and
    $module.Contains("SupportDetailToolboxControl")) `
    "support detail exposes a discoverable toolbox module"
foreach ($action in @("CreateSupportDetail", "EditSupportDetail",
        "InspectSupportDetail", "DeleteSupportDetail")) {
    Assert-Condition $toolboxPage.Contains('"' + $action + '"') `
        "toolbox page exposes action: $action"
}
Assert-Condition ($toolboxPage.Contains("SupportDetailToolboxDiagram") -and
    $toolboxPage.Contains("SupportDetailSettingsStore.Load")) `
    "toolbox page shows the current saved geometry diagram"

$commands = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "Commands\SupportDetailCommands.cs") -Raw -Encoding UTF8
foreach ($command in @(
        "STPVSUPPORTDETAIL",
        "STPVSUPPORTDETAILEDIT",
        "STPVSUPPORTDETAILINFO",
        "STPVSUPPORTDETAILDELETE",
        "STPVSUPPORTDETAILREOPENCHECK")) {
    Assert-Condition $commands.Contains($command) `
        "command is registered: $command"
}

$drawing = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "Commands\SupportDetailDrawingService.cs") -Raw -Encoding UTF8
Assert-Condition ($drawing.Contains("new Polyline") -and
    $drawing.Contains("new Line") -and
    $drawing.Contains("new AlignedDimension") -and
    $drawing.Contains("new Table")) `
    "main output uses native AutoCAD entities and dimensions"
Assert-Condition ($drawing.Contains("STPV_SUPPORT_DETAIL_INSTANCES") -and
    $drawing.Contains("TryReadEntityInstance") -and
    $drawing.Contains("ReadInstanceRecord") -and
    $drawing.Contains("EraseInstanceEntities")) `
    "DWG metadata supports select, read, and instance update"
Assert-Condition ($drawing.Contains("FixedDetailBlockName") -and
    $drawing.Contains("FIXED_NODE_DETAILS")) `
    "only the fixed detail library is intentionally inserted as a block"

$dialog = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "UI\SupportDetailDialog.cs") -Raw -Encoding UTF8
$diagram = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "UI\SupportDetailDiagram.cs") -Raw -Encoding UTF8
Assert-Condition ($dialog.Contains('"PierStations"') -and
    $dialog.Contains('"RailPositions"') -and
    $dialog.Contains('"BeamOverhang"') -and
    $dialog.Contains("SupportDetailDiagram") -and
    $diagram.Contains("AccessibleDescription") -and
    $diagram.Contains("geometry.Triangles[0].BeamPoint")) `
    "custom geometry inputs share a matching datum and symbol diagram"

$library = Join-Path $sourceRoot "Library\SupportDetailFixedDetails.dwg"
Assert-Condition (Test-Path -LiteralPath $library -PathType Leaf) `
    "fixed detail library exists"
Assert-Condition ((Get-Item -LiteralPath $library).Length -gt 100000) `
    "fixed detail library contains extracted source entities"
Assert-Condition ($project.Contains("<EmbeddedResource") -and
    $commands.Contains("GetManifestResourceStream") -and
    $commands.Contains("SpecialFolder.LocalApplicationData") -and
    $commands.Contains("File.Copy(temporary, cached, true)")) `
    "fixed detail library is embedded and repairs the user cache when needed"

$assemblyInfo = Get-Content -LiteralPath (Join-Path $sourceRoot `
    "Properties\AssemblyInfo.cs") -Raw -Encoding UTF8
$readme = Get-Content -LiteralPath (Join-Path $sourceRoot "README.md") `
    -Raw -Encoding UTF8
Assert-Condition ($assemblyInfo.Contains('AssemblyVersion("0.4.2.0")') -and
    $readme.Contains("v0.4.2") -and $readme.Contains("0.4.2.0")) `
    "assembly and README version labels are synchronized"

Write-Host "PASS $passed support detail source tests"
