param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox"
$railSourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Rail"
$railCommandsPath = Join-Path $railSourceRoot "Commands\RailCommands.cs"
$railServicePath = Join-Path $railSourceRoot "Commands\RailDrawingService.cs"
$toolboxCommandsPath = Join-Path $sourceRoot "Commands\ToolboxCommands.cs"
$shellPath = Join-Path $sourceRoot "UI\ToolboxShellControl.cs"
$toolboxPath = Join-Path $railSourceRoot "UI\ToolboxControl.cs"
$ribbonPath = Join-Path $sourceRoot "UI\ToolboxRibbon.cs"
$projectFilePath = Join-Path $railSourceRoot "StructuralToolbox.Rail.csproj"
$manifestPath = Join-Path $projectRoot `
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml"
$testScriptPath = Join-Path $projectRoot "scripts\test.ps1"
$buildScriptPath = Join-Path $projectRoot "scripts\build.ps1"
$installerRuntimePath = Join-Path $projectRoot `
    "scripts\installer\Test-InstallerRuntime.ps1"
$railCommands = Get-Content -LiteralPath $railCommandsPath -Raw -Encoding UTF8
$railService = Get-Content -LiteralPath $railServicePath -Raw -Encoding UTF8
$toolboxCommands = Get-Content -LiteralPath $toolboxCommandsPath -Raw -Encoding UTF8
$shell = Get-Content -LiteralPath $shellPath -Raw -Encoding UTF8
$toolbox = Get-Content -LiteralPath $toolboxPath -Raw -Encoding UTF8
$ribbon = Get-Content -LiteralPath $ribbonPath -Raw -Encoding UTF8
$projectFile = Get-Content -LiteralPath $projectFilePath -Raw -Encoding UTF8
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
$testScript = Get-Content -LiteralPath $testScriptPath -Raw -Encoding UTF8
$buildScript = Get-Content -LiteralPath $buildScriptPath -Raw -Encoding UTF8
$installerRuntime = Get-Content -LiteralPath $installerRuntimePath -Raw -Encoding UTF8
$passed = 0

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Message)
    if (-not $Text.Contains($Needle)) {
        throw "$Message; missing: $Needle"
    }
    $script:passed++
}

function Assert-NotContains {
    param([string]$Text, [string]$Needle, [string]$Message)
    if ($Text.Contains($Needle)) {
        throw "$Message; unexpected: $Needle"
    }
    $script:passed++
}

function Assert-FileAbsent {
    param([string]$Path, [string]$Message)
    if (Test-Path -LiteralPath $Path) {
        throw "$Message; unexpected file: $Path"
    }
    $script:passed++
}

function Assert-FilePresent {
    param([string]$Path, [string]$Message)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Message; missing file: $Path"
    }
    $script:passed++
}

$retiredCommand = "STPVRAILANNOTATE"
$activeRailSurface = @($railCommands, $toolboxCommands, $shell,
    $toolbox, $ribbon, $projectFile, $manifest,
    $buildScript) -join "`n"

Assert-Contains $railCommands '[CommandMethod("STPVRAIL"' `
    "The rail command remains available"
Assert-Contains $railCommands "EraseToolRailsForRuns" `
    "Rail redraw still replaces complete tool rail runs"
Assert-Contains $railCommands "RunPreview" `
    "Rail redraw still uses the rail-only preview flow"
Assert-Contains $railCommands "ConfirmReplaceToolEntities" `
    "Rail redraw still confirms replacement without dimension cleanup"
Assert-NotContains $railCommands "RailAnnotationDrawingService" `
    "Rail drawing must not invoke the annotation drawing service"
Assert-NotContains $railCommands "RailAnnotationPlanner" `
    "Rail drawing must not plan dimensions"
Assert-NotContains $railCommands "EnableAutoAnnotation" `
    "A legacy annotation setting must not re-enable dimensions"
Assert-NotContains $railCommands "EraseToolAnnotations" `
    "Rail redraw must not erase existing dimensions"
Assert-NotContains $railCommands $retiredCommand `
    "The retired annotation command must not be registered by the rail command"

Assert-NotContains $toolboxCommands $retiredCommand `
    "The retired annotation command must not be advertised"
Assert-Contains $toolboxCommands "STPVRAIL" `
    "About text must continue to advertise only the rail command"
Assert-NotContains $manifest $retiredCommand `
    "The bundle manifest must not expose the retired command"
Assert-NotContains $ribbon "联动生成导轨标注" `
    "Ribbon help text must not advertise linked dimensions"
Assert-NotContains $toolbox "OpenAnnotationSettings" `
    "The rail page must not expose annotation settings"
Assert-NotContains $toolbox "LinkAnnotationAfterDrawing" `
    "The rail page must not expose the annotation switch"
Assert-NotContains $shell "AnnotationToolboxControl" `
    "The palette must not construct an annotation page"
Assert-NotContains $shell "AnnotationPageShown" `
    "The palette must not expose annotation-page navigation"
Assert-NotContains $shell "ShowAnnotationPage" `
    "The palette must not retain an annotation-page route"
Assert-NotContains $projectFile "AnnotationToolboxControl.cs" `
    "The annotation settings page must not be compiled"
Assert-NotContains $projectFile "RailAnnotationCommands.cs" `
    "The retired standalone command must not be compiled"
Assert-FileAbsent (Join-Path $sourceRoot "Commands\RailAnnotationCommands.cs") `
    "The standalone rail annotation implementation is removed"
Assert-FileAbsent (Join-Path $sourceRoot "Commands\RailAnnotationDrawingService.cs") `
    "The rail annotation drawing service is removed"
Assert-FileAbsent (Join-Path $sourceRoot "Commands\RailEntityReader.cs") `
    "The annotation entity reader is removed"
Assert-FileAbsent (Join-Path $sourceRoot "Core\ExistingRailAnnotationSourceBuilder.cs") `
    "The existing-annotation source builder is removed"
Assert-FileAbsent (Join-Path $sourceRoot "Core\RailAnnotationPlanner.cs") `
    "The rail annotation planner is removed"
Assert-FileAbsent (Join-Path $sourceRoot "Core\RailAnnotationSourceModel.cs") `
    "The rail annotation source model is removed"
Assert-FileAbsent (Join-Path $sourceRoot "Core\ToolAnnotationOwnershipClassifier.cs") `
    "The rail annotation ownership classifier is removed"
Assert-FileAbsent (Join-Path $sourceRoot "UI\AnnotationToolboxControl.cs") `
    "The annotation settings UI is removed"

Assert-NotContains $activeRailSurface $retiredCommand `
    "Active source, manifest, and build/test entry points must omit the retired command"
Assert-NotContains $testScript "AnnotationToolboxControl.cs" `
    "UI tests must not compile the removed annotation page"

Write-Host "PASS $passed rail annotation removal source tests"
