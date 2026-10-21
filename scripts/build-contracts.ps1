param(
    [string]$OutputDirectory = "",
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Contracts"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "work\module-builds\contracts"
}
$outputDirectoryFull = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputDirectoryFull -Force | Out-Null
$outputPath = Join-Path $outputDirectoryFull "StructuralToolbox.Contracts.dll"
$sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Sort-Object FullName | ForEach-Object { $_.FullName }

& $compiler /nologo /target:library /platform:x64 /optimize+ /warn:4 `
    /utf8output "/out:$outputPath" /reference:System.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sourceFiles
if ($LASTEXITCODE -ne 0) {
    throw "Contracts build failed with exit code $LASTEXITCODE"
}
Write-Host "Built: $outputPath"
if ($PassThru) {
    [pscustomobject]@{
        name = "StructuralToolbox.Contracts"
        path = $outputPath
        sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
    }
}
