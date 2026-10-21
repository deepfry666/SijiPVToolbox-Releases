param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$Version = "0.4.2",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$workRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "work"))
$expectedVersion = [version]$Version
$expectedVersion = [version]::new($expectedVersion.Major,
    $expectedVersion.Minor,
    $(if ($expectedVersion.Build -lt 0) { 0 } else { $expectedVersion.Build }),
    0)
$version3 = $expectedVersion.ToString(3)

if ([String]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $workRoot `
        "module-builds\support-detail-diagnostic-v$version3"
}
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $packageRoot.StartsWith(
        $workRoot.TrimEnd("\") + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "The standalone diagnostic package must remain under work/: $packageRoot"
}

$buildRoot = Join-Path $workRoot "module-builds\support-detail-package-build"
$build = & (Join-Path $PSScriptRoot "build-support-detail.ps1") `
    -AutoCADDir $AutoCADDir -OutputDirectory $buildRoot -PassThru
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName(
    $build.path).Version
if ($assemblyVersion -ne $expectedVersion) {
    throw "Assembly version $assemblyVersion does not match package $expectedVersion."
}
if ([String]::IsNullOrWhiteSpace([string]$build.librarySource) -or
    -not (Test-Path -LiteralPath $build.librarySource -PathType Leaf)) {
    throw "The fixed detail library source is missing from the module build."
}

$resourceName =
    "StructuralToolbox.SupportDetail.SupportDetailFixedDetails.dwg"
$resourceAssembly = [Reflection.Assembly]::LoadFrom($build.path)
if ($resourceAssembly.GetManifestResourceNames() -notcontains $resourceName) {
    throw "The support detail DLL does not contain the fixed detail library."
}

if (Test-Path -LiteralPath $packageRoot -PathType Container) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $build.path -Destination $packageRoot -Force
Copy-Item -LiteralPath $build.contracts -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot `
    "src\StructuralToolbox.SupportDetail\README.md") `
    -Destination (Join-Path $packageRoot "README.md") -Force

$artifactNames = @(
    "StructuralToolbox.Contracts.dll",
    "StructuralToolbox.SupportDetail.dll",
    "README.md"
)
$checksumLines = foreach ($name in $artifactNames) {
    $path = Join-Path $packageRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Package artifact is missing: $path"
    }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $packageRoot "SHA256SUMS.txt"),
    $checksumLines, [Text.UTF8Encoding]::new($false))

Write-Host "Diagnostic package: $packageRoot"
Write-Host "The fixed detail DWG is embedded in StructuralToolbox.SupportDetail.dll."
Get-ChildItem -LiteralPath $packageRoot -File |
    Sort-Object Name | Select-Object Name, Length, LastWriteTime
