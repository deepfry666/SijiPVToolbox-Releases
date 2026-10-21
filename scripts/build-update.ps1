[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$ContractsPath,
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Update"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "work\module-builds\update"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

if ([string]::IsNullOrWhiteSpace($ContractsPath)) {
    $ContractsPath = Join-Path $OutputDirectory "StructuralToolbox.Contracts.dll"
}
$ContractsPath = [IO.Path]::GetFullPath($ContractsPath)
if (-not (Test-Path -LiteralPath $ContractsPath -PathType Leaf)) {
    throw "Contracts assembly was not found: $ContractsPath"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = Join-Path $OutputDirectory "StructuralToolbox.Update.dll"
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Sort-Object FullName | ForEach-Object { $_.FullName })

& $compiler /nologo /target:library /platform:x64 /optimize+ /warn:4 `
    /utf8output "/out:$outputPath" "/reference:$ContractsPath" `
    /reference:System.dll /reference:System.Core.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    $sourceFiles
if ($LASTEXITCODE -ne 0) {
    throw "Update module build failed with exit code $LASTEXITCODE"
}

$record = [pscustomobject]@{
    name = "StructuralToolbox.Update"
    path = $outputPath
    sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
}
Write-Host "Built: $outputPath"
if ($PassThru) {
    $record
}
