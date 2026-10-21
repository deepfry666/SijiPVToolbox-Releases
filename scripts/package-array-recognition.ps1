param([string]$Version = "0.1.0", [string]$AutoCADDir = "D:\cad\AutoCAD 2020")
$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$packageRoot = Join-Path $projectRoot `
    "outputs\StructuralToolbox.ArrayRecognition_v$($Version)_AutoCAD2020"
$buildRoot = Join-Path $projectRoot `
    "work\module-builds\array-recognition-package"
$build = & (Join-Path $PSScriptRoot "build-array-recognition.ps1") `
    -AutoCADDir $AutoCADDir -OutputDirectory $buildRoot -PassThru
if (Test-Path -LiteralPath $packageRoot -PathType Container) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $build.path -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.ArrayRecognition\README.md") `
    -Destination $packageRoot -Force
$manifest = [ordered]@{
    name = "StructuralToolbox.ArrayRecognition"
    displayName = "组件阵列识别独立模块"
    version = $Version
    host = "AutoCAD 2020 Win64"
    commands = @("STPVARRAYRECOGNIZE", "STPVARRAYTABLE")
    integratedIntoBallast = $true
    artifactSha256 = $build.sha256
}
[IO.File]::WriteAllText((Join-Path $packageRoot "manifest.json"),
    ($manifest | ConvertTo-Json -Depth 4) + "`n",
    [Text.UTF8Encoding]::new($false))
$names = @("StructuralToolbox.ArrayRecognition.dll", "README.md",
    "manifest.json")
$lines = foreach ($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $packageRoot $name) `
        -Algorithm SHA256).Hash
    "$hash  $name"
}
[IO.File]::WriteAllText((Join-Path $packageRoot "SHA256SUMS.txt"),
    ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "Standalone package: $packageRoot"
Get-ChildItem -LiteralPath $packageRoot -File | Select-Object Name,Length
