param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$TargetSeries = "R23.1",
    [int]$ExpectedAutoCADYear = 2020,
    [string]$BuildProfile = "acad2020-netfx",
    [switch]$InternalVerify,
    [switch]$ValidateOnly,
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src\StructuralToolbox"
$railSourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Rail"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfAssemblyRoot = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

$knownSeriesByYear = @{
    2018 = "R22.0"
    2019 = "R23.0"
    2020 = "R23.1"
    2021 = "R24.0"
    2022 = "R24.1"
    2023 = "R24.2"
    2024 = "R24.3"
    2025 = "R25.0"
    2026 = "R25.1"
}

if ($TargetSeries -notmatch '^R(?<Major>\d+)\.(?<Minor>\d+)$') {
    throw "TargetSeries must use Autodesk Series syntax such as R23.1: $TargetSeries"
}
$seriesMajor = [int]$Matches.Major
$seriesMinor = [int]$Matches.Minor
if (-not $knownSeriesByYear.ContainsKey($ExpectedAutoCADYear) -or
    $knownSeriesByYear[$ExpectedAutoCADYear] -ne $TargetSeries) {
    throw "AutoCAD year $ExpectedAutoCADYear does not map to target Series $TargetSeries."
}
if ($seriesMajor -ge 25) {
    throw "Series $TargetSeries requires the independent .NET 8 build pipeline; " +
        "scripts/build.ps1 is the legacy .NET Framework pipeline."
}
if ($BuildProfile -ne "acad2020-netfx" -or $ExpectedAutoCADYear -ne 2020 -or
    $TargetSeries -ne "R23.1") {
    throw "The implemented release build profile is acad2020-netfx/R23.1 only. " +
        "Add and verify a separate profile before building another Series."
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "C# compiler was not found: $compiler"
}

$autoCADRoot = [IO.Path]::GetFullPath($AutoCADDir)
$acadExecutable = Join-Path $autoCADRoot "acad.exe"
if (-not (Test-Path -LiteralPath $acadExecutable -PathType Leaf)) {
    throw "AutoCAD executable was not found: $acadExecutable"
}
$acadVersion = (Get-Item -LiteralPath $acadExecutable).VersionInfo
if ($acadVersion.ProductMajorPart -ne $seriesMajor -or
    $acadVersion.ProductMinorPart -ne $seriesMinor) {
    throw "AutoCAD product version $($acadVersion.ProductVersion) does not match " +
        "declared Series $TargetSeries."
}

$requiredCadAssemblies = @(
    "acmgd.dll",
    "acdbmgd.dll",
    "accoremgd.dll",
    "AdWindows.dll"
)
$sdkFiles = @()
foreach ($assembly in $requiredCadAssemblies) {
    $assemblyPath = Join-Path $autoCADRoot $assembly
    if (-not (Test-Path -LiteralPath $assemblyPath)) {
        throw "AutoCAD managed API was not found: $assemblyPath"
    }

    $file = Get-Item -LiteralPath $assemblyPath
    $assemblyVersion = $null
    if ($assembly -in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")) {
        $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName(
            $assemblyPath).Version
        if ($assemblyVersion.Major -ne $seriesMajor -or
            $assemblyVersion.Minor -ne $seriesMinor) {
            throw "$assembly assembly version $assemblyVersion does not match " +
                "declared Series $TargetSeries."
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $assemblyPath
        if ($signature.Status -ne
                [Management.Automation.SignatureStatus]::Valid -or
            $null -eq $signature.SignerCertificate -or
            -not $signature.SignerCertificate.Subject.Contains("Autodesk")) {
            throw "$assembly is not validly signed by Autodesk: $assemblyPath"
        }
    } else {
        $signature = Get-AuthenticodeSignature -LiteralPath $assemblyPath
    }

    $sdkFiles += [ordered]@{
        name = $assembly
        assemblyVersion = if ($null -eq $assemblyVersion) {
            $null
        } else {
            $assemblyVersion.ToString()
        }
        productVersion = [string]$file.VersionInfo.ProductVersion
        sha256 = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
        signatureStatus = $signature.Status.ToString()
        signerSubject = if ($null -eq $signature.SignerCertificate) {
            $null
        } else {
            $signature.SignerCertificate.Subject
        }
    }
}

Write-Host "Validated AutoCAD $ExpectedAutoCADYear SDK: $TargetSeries"

function New-SdkValidationRecord([string]$OutputSha256, $Modules) {
    return [pscustomobject][ordered]@{
        schemaVersion = 1
        validation = "passed"
        targetYear = $ExpectedAutoCADYear
        targetSeries = $TargetSeries
        buildProfile = $BuildProfile
        runtimeFamily = "legacy-netfx"
        autoCADProductName = [string]$acadVersion.ProductName
        autoCADProductVersion = [string]$acadVersion.ProductVersion
        autoCADFileVersion = [string]$acadVersion.FileVersion
        autoCADSignatureStatus = (Get-AuthenticodeSignature `
            -LiteralPath $acadExecutable).Status.ToString()
        sdkFiles = $sdkFiles
        compilerVersion = (Get-Item -LiteralPath $compiler).VersionInfo.FileVersion
        outputSha256 = $OutputSha256
        modules = @($Modules)
    }
}

if ($ValidateOnly) {
    New-SdkValidationRecord $null @()
    return
}

function New-CompilerArguments([string]$OutputPath) {
    return @(
    "/nologo",
    "/target:library",
    "/platform:x64",
    "/optimize+",
    "/warn:4",
    "/utf8output",
    "/out:$OutputPath",
    "/reference:$(Join-Path $autoCADRoot 'acmgd.dll')",
    "/reference:$(Join-Path $autoCADRoot 'acdbmgd.dll')",
    "/reference:$(Join-Path $autoCADRoot 'accoremgd.dll')",
    "/reference:$(Join-Path $autoCADRoot 'AdWindows.dll')",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Xml.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll",
    "/reference:$(Join-Path $wpfAssemblyRoot 'WindowsBase.dll')",
    "/reference:$(Join-Path $wpfAssemblyRoot 'PresentationCore.dll')",
    "/reference:$(Join-Path $wpfAssemblyRoot 'PresentationFramework.dll')"
    )
}

if ($InternalVerify) {
    $outputDirectory = Join-Path $projectRoot "work\verify"
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $outputPath = Join-Path $outputDirectory "StructuralToolbox.Verify.dll"
    $arguments = New-CompilerArguments $outputPath
    $arguments += "/define:INTERNAL_VERIFY"
    $arguments += "/debug:pdbonly"
    $sourceFiles = @(
        (Join-Path $railSourceRoot "Core\DeterministicLaneSolver.cs"),
        (Join-Path $railSourceRoot "Core\Geometry.cs"),
        (Join-Path $railSourceRoot "Core\GeometryFingerprint.cs"),
        (Join-Path $railSourceRoot "Core\RailLayoutEngine.cs"),
        (Join-Path $railSourceRoot "Core\ToolMetadataCodec.cs"),
        (Join-Path $railSourceRoot "Settings\RailSettings.cs"),
        (Join-Path $railSourceRoot "Settings\RailSettingsStore.cs"),
        (Join-Path $railSourceRoot "Commands\PanelRectangleReader.cs"),
        (Join-Path $railSourceRoot "Commands\RailDrawingService.cs"),
        (Join-Path $railSourceRoot "Commands\VerificationCommands.cs"),
        (Join-Path $projectRoot "tests\VerificationAssemblyInfo.cs")
    )
    $arguments += $sourceFiles
    & $compiler $arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Internal verification build failed with exit code $LASTEXITCODE"
    }
    Write-Host "Built: $outputPath"
    if ($PassThru) {
        New-SdkValidationRecord ((Get-FileHash -LiteralPath $outputPath `
            -Algorithm SHA256).Hash) @()
    }
    return
}

$outputDirectory = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    "bundle\ZhStructureToolbox.bundle\Contents\Windows\$TargetSeries"))
$moduleBuildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    "work\module-builds"))
$stagingRoot = Join-Path $moduleBuildRoot `
    ("full-{0}" -f [guid]::NewGuid().ToString("N"))
$stagingOutput = Join-Path $stagingRoot "output"
$backupOutput = Join-Path $stagingRoot "backup"
$moduleFileNames = @(
    "StructuralToolbox.dll",
    "StructuralToolbox.Contracts.dll",
    "StructuralToolbox.Rail.dll",
    "StructuralToolbox.Ballast.dll",
    "StructuralToolbox.Update.dll",
    "StructuralToolbox.DrawingTranslation.dll"
)
$retiredModuleFileNames = @("StructuralToolbox.SupportDetail.dll")
$committedFiles = New-Object Collections.Generic.List[string]
$backupFiles = @{}
$buildCompleted = $false

function Assert-BundleFilesWritable([string]$Directory, [string[]]$FileNames) {
    foreach ($fileName in $FileNames) {
        $path = Join-Path $Directory $fileName
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            continue
        }
        $opened = $false
        $lastError = $null
        for ($attempt = 0; $attempt -lt 8 -and -not $opened; $attempt++) {
            try {
                $stream = [IO.File]::Open($path, [IO.FileMode]::Open,
                    [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                $stream.Dispose()
                $opened = $true
            } catch {
                $lastError = $_.Exception.Message
                if ($attempt -lt 7) {
                    Start-Sleep -Milliseconds 250
                }
            }
        }
        if (-not $opened) {
            throw "Bundle module is locked and the existing Bundle was not changed: " +
                "$path. Close AutoCAD or the process reading this file. $lastError"
        }
    }
}

function Restore-CommittedBundle {
    $errors = New-Object Collections.Generic.List[string]
    foreach ($destination in @($script:committedFiles) | Select-Object -Reverse) {
        try {
            $backup = [string]$script:backupFiles[$destination]
            if (-not [string]::IsNullOrWhiteSpace($backup) -and
                (Test-Path -LiteralPath $backup -PathType Leaf)) {
                Copy-Item -LiteralPath $backup -Destination $destination -Force
            } elseif (Test-Path -LiteralPath $destination -PathType Leaf) {
                Remove-Item -LiteralPath $destination -Force
            }
        } catch {
            $errors.Add("$destination`: $($_.Exception.Message)")
        }
    }
    if ($errors.Count -gt 0) {
        throw "Bundle commit failed and rollback also failed: " +
            ($errors -join "; ")
    }
}

try {
    New-Item -ItemType Directory -Path $stagingOutput,$backupOutput `
        -Force | Out-Null

    $contracts = & (Join-Path $PSScriptRoot "build-contracts.ps1") `
        -OutputDirectory $stagingOutput -PassThru
    $rail = & (Join-Path $PSScriptRoot "build-rail.ps1") `
        -AutoCADDir $autoCADRoot -OutputDirectory $stagingOutput `
        -ContractsPath $contracts.path -PassThru
    $ballast = & (Join-Path $PSScriptRoot "build-ballast.ps1") `
        -AutoCADDir $autoCADRoot -OutputDirectory $stagingOutput `
        -ContractsPath $contracts.path -PassThru
    $update = & (Join-Path $PSScriptRoot "build-update.ps1") `
        -OutputDirectory $stagingOutput `
        -ContractsPath $contracts.path -PassThru
    $translation = & (Join-Path $PSScriptRoot "build-drawing-translation.ps1") `
        -AutoCADDir $autoCADRoot -OutputDirectory $stagingOutput `
        -ContractsPath $contracts.path -MainToolbox

    $stagedShellPath = Join-Path $stagingOutput "StructuralToolbox.dll"
    $arguments = New-CompilerArguments $stagedShellPath
    $arguments += "/debug-"
    $arguments += "/reference:$($contracts.path)"
    $arguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
        Sort-Object FullName | ForEach-Object { $_.FullName }
    & $compiler $arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Toolbox shell build failed with exit code $LASTEXITCODE"
    }

    $actualStagedFiles = @(Get-ChildItem -LiteralPath $stagingOutput -File |
        ForEach-Object { $_.Name })
    if ($actualStagedFiles.Count -ne $moduleFileNames.Count -or
        @(Compare-Object ($moduleFileNames | Sort-Object) `
            ($actualStagedFiles | Sort-Object)).Count -ne 0) {
        throw "Staged build must contain exactly the six release module DLLs: " +
            ($actualStagedFiles -join ", ")
    }

    [xml]$sourceManifest = Get-Content -LiteralPath (Join-Path $projectRoot `
        "bundle\ZhStructureToolbox.bundle\PackageContents.xml") `
        -Raw -Encoding UTF8
    $expectedVersion = [version](
        [string]$sourceManifest.ApplicationPackage.AppVersion)
    foreach ($fileName in $moduleFileNames) {
        $stagedPath = Join-Path $stagingOutput $fileName
        $fileVersion = [version](
            (Get-Item -LiteralPath $stagedPath).VersionInfo.FileVersion)
        if ($fileVersion -ne $expectedVersion) {
            throw "$fileName version is $fileVersion; expected $expectedVersion."
        }
    }

    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    Assert-BundleFilesWritable $outputDirectory `
        @($moduleFileNames + $retiredModuleFileNames)
    foreach ($fileName in $moduleFileNames) {
        $sourcePath = Join-Path $stagingOutput $fileName
        $destinationPath = Join-Path $outputDirectory $fileName
        if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
            $backupPath = Join-Path $backupOutput $fileName
            Copy-Item -LiteralPath $destinationPath -Destination $backupPath
            $backupFiles[$destinationPath] = $backupPath
        } else {
            $backupFiles[$destinationPath] = $null
        }
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
        $committedFiles.Add($destinationPath)
    }
    foreach ($fileName in $retiredModuleFileNames) {
        $destinationPath = Join-Path $outputDirectory $fileName
        if (-not (Test-Path -LiteralPath $destinationPath -PathType Leaf)) {
            continue
        }
        $backupPath = Join-Path $backupOutput $fileName
        Copy-Item -LiteralPath $destinationPath -Destination $backupPath
        $backupFiles[$destinationPath] = $backupPath
        Remove-Item -LiteralPath $destinationPath -Force
        $committedFiles.Add($destinationPath)
    }
    $buildCompleted = $true
} catch {
    $buildError = $_
    if ($committedFiles.Count -gt 0) {
        Restore-CommittedBundle
    }
    throw $buildError
} finally {
    if (Test-Path -LiteralPath $stagingRoot -PathType Container) {
        $resolvedStage = [IO.Path]::GetFullPath($stagingRoot)
        $safePrefix = $moduleBuildRoot.TrimEnd("\") + "\full-"
        if ($resolvedStage.StartsWith($safePrefix,
                [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedStage -Recurse -Force
        }
    }
}

if (-not $buildCompleted) {
    throw "Six-module build did not commit."
}

$moduleRecords = @($moduleFileNames | ForEach-Object {
    $path = Join-Path $outputDirectory $_
    Write-Host "Built: $path"
    [pscustomobject]@{
        name = [IO.Path]::GetFileNameWithoutExtension($_)
        path = $path
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
})
$shell = @($moduleRecords | Where-Object { $_.name -eq "StructuralToolbox" })[0]
if ($PassThru) {
    New-SdkValidationRecord $shell.sha256 $moduleRecords
}
