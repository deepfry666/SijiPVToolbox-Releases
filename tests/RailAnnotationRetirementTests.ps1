param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$passed = 0
function Assert-Condition([bool]$condition,[string]$message) {
    if (-not $condition) { throw "FAIL: $message" }
    $script:passed++
}
$railRoot = Join-Path $projectRoot 'src\StructuralToolbox.Rail'
$source = @(Get-ChildItem -LiteralPath $railRoot -Recurse -File -Filter '*.cs' |
    Where-Object { $_.Name -ne 'VerificationCommands.cs' } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"
foreach ($command in @('STPVRAILDIM','STPVRAILANNOTATE','GDBZ')) {
    Assert-Condition (-not $source.Contains('CommandMethod("' + $command + '"')) "No rail annotation command registration: $command"
}
Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'src\StructuralToolbox.RailAnnotation'))) 'Independent annotation source has been retired'
Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $railRoot 'Resources\GDBZ_V1.0.lsp'))) 'Bundled GDBZ resource has been removed'
Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $railRoot 'RailDimensionToolboxModule.cs'))) 'Annotation home module has been removed'
[xml]$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'bundle\ZhStructureToolbox.bundle\PackageContents.xml') -Raw -Encoding UTF8
foreach ($command in @('STPVRAILDIM','STPVRAILANNOTATE','GDBZ')) {
    Assert-Condition ($null -eq $manifest.SelectSingleNode("//Command[@Global='$command']")) "Annotation command is absent from bundle: $command"
}
foreach ($command in @('STPVRAIL','STPVTRANSLATE','STPVTRANSLATECONFIRM')) {
    Assert-Condition ($null -ne $manifest.SelectSingleNode("//Command[@Global='$command']")) "Retained drawing/translation command is available: $command"
}
$ribbon = Get-Content -LiteralPath (Join-Path $projectRoot 'src\StructuralToolbox\UI\ToolboxRibbon.cs') -Raw -Encoding UTF8
Assert-Condition (-not $ribbon.Contains('ConfigureRailDimensionButton')) 'Ribbon does not recreate annotation buttons'
Assert-Condition ($ribbon.Contains('STRUCTURAL_TOOLBOX_RAIL_DIMENSION_BUTTON') -and $ribbon.Contains('legacyAnnotationButtonFound')) 'Ribbon migration rejects previous annotation buttons'
$build = Get-Content -LiteralPath (Join-Path $projectRoot 'scripts\build-rail.ps1') -Raw -Encoding UTF8
Assert-Condition (-not $build.Contains('GDBZ')) 'Release build no longer embeds GDBZ'
foreach ($script in @('build','test','package','verify')) {
    $name = if ($script -eq 'verify') { 'verify-rail-annotation-autocad.ps1' } else { $script + '-rail-annotation.ps1' }
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $projectRoot ('scripts\' + $name)))) "Retired standalone entry is unavailable: $name"
}
Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'outputs\StructuralToolbox.RailAnnotation_v0.1.0_AutoCAD2020'))) 'Annotation deliverable no longer appears among active outputs'
Write-Host "PASS $passed rail annotation retirement tests"
