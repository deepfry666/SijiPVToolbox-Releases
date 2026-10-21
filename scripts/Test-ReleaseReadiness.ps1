[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet("Source", "Built", "Published", "Installed")]
    [string]$Phase = "Source",

    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}

$projectRoot = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd("\")
$passes = New-Object Collections.Generic.List[string]
$warnings = New-Object Collections.Generic.List[string]
$failures = New-Object Collections.Generic.List[string]

function Add-Pass([string]$Message) {
    $passes.Add($Message)
    Write-Host "PASS: $Message"
}

function Add-Warning([string]$Message) {
    $warnings.Add($Message)
    Write-Warning $Message
}

function Add-Failure([string]$Message) {
    $failures.Add($Message)
    Write-Host "BLOCK: $Message" -ForegroundColor Red
}

function Assert-Condition([bool]$Condition, [string]$Message) {
    if ($Condition) {
        Add-Pass $Message
    } else {
        Add-Failure $Message
    }
}

function Resolve-ProjectPath([string]$RelativePath) {
    return [IO.Path]::GetFullPath((Join-Path $projectRoot $RelativePath))
}

function Get-Text([string]$RelativePath) {
    $path = Resolve-ProjectPath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Failure "Required file is missing: $RelativePath"
        return $null
    }
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

function Assert-Text([string]$RelativePath, [string]$Expected,
    [string]$Description) {
    $text = Get-Text $RelativePath
    if ($null -eq $text) {
        return
    }
    Assert-Condition ($text.Contains($Expected)) $Description
}

function Get-NormalizedVersion([version]$Value, [int]$Parts) {
    if ($Parts -eq 3) {
        return "{0}.{1}.{2}" -f $Value.Major, $Value.Minor,
            [Math]::Max(0, $Value.Build)
    }
    return "{0}.{1}.{2}.{3}" -f $Value.Major, $Value.Minor,
        [Math]::Max(0, $Value.Build), [Math]::Max(0, $Value.Revision)
}

function Get-ProjectReferences([string]$RelativePath) {
    $text = Get-Text $RelativePath
    if ($null -eq $text) {
        return @()
    }
    return @([regex]::Matches($text,
        '<ProjectReference\s+Include="([^"]+)"') | ForEach-Object {
            [IO.Path]::GetFileNameWithoutExtension($_.Groups[1].Value)
        } | Sort-Object -Unique)
}

function Test-ExactSet([string[]]$Actual, [string[]]$Expected) {
    if ($Actual.Count -ne $Expected.Count) {
        return $false
    }
    return @(Compare-Object -ReferenceObject @($Expected | Sort-Object) `
        -DifferenceObject @($Actual | Sort-Object)).Count -eq 0
}

try {
    $parsedVersion = [version]$Version
} catch {
    throw "Version is invalid: $Version"
}
$version3 = Get-NormalizedVersion $parsedVersion 3
$version4 = Get-NormalizedVersion $parsedVersion 4
$phaseRank = @{ Source = 0; Built = 1; Published = 2; Installed = 3 }
$rank = [int]$phaseRank[$Phase]

Write-Host "Release readiness: v$version3 ($Phase)"
Write-Host "Project root: $projectRoot"

$releaseRelative = "scripts\installer\release-v$version3.json"
$releaseText = Get-Text $releaseRelative
$release = $null
if ($null -ne $releaseText) {
    try {
        $release = $releaseText | ConvertFrom-Json
        Add-Pass "release definition JSON is valid"
    } catch {
        Add-Failure "release definition JSON is invalid: $releaseRelative"
    }
}
if ($null -ne $release) {
    Assert-Condition ([string]$release.releaseVersion -eq $version3) `
        "release definition uses v$version3"
    Assert-Condition ([string]$release.assemblyVersion -eq $version4) `
        "release definition uses assembly version $version4"
    foreach ($field in @("installerAppId", "testInstallerAppId",
        "bundleProductCode", "testBundleProductCode", "bundleUpgradeCode",
        "testBundleUpgradeCode")) {
        try {
            $null = [guid]([string]$release.$field)
            Add-Pass "release identity $field is a GUID"
        } catch {
            Add-Failure "release identity $field is missing or invalid"
        }
    }
}

$assemblyInfoFiles = @(
    "src\StructuralToolbox\Properties\AssemblyInfo.cs",
    "src\StructuralToolbox.Contracts\Properties\AssemblyInfo.cs",
    "src\StructuralToolbox.Rail\Properties\AssemblyInfo.cs",
    "src\StructuralToolbox.Ballast\Properties\AssemblyInfo.cs",
    "src\StructuralToolbox.Update\Properties\AssemblyInfo.cs",
    "src\StructuralToolbox.DrawingTranslation\Properties\AssemblyInfo.cs",
    "tests\RealDrawingVerificationAssemblyInfo.cs"
)
foreach ($file in $assemblyInfoFiles) {
    Assert-Text $file "AssemblyVersion(`"$version4`")" `
        "$file has AssemblyVersion $version4"
    Assert-Text $file "AssemblyFileVersion(`"$version4`")" `
        "$file has AssemblyFileVersion $version4"
}

$sourceManifestRelative =
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml"
$sourceManifestPath = Resolve-ProjectPath $sourceManifestRelative
$sourceManifest = $null
if (Test-Path -LiteralPath $sourceManifestPath -PathType Leaf) {
    try {
        [xml]$sourceManifest = Get-Content -LiteralPath $sourceManifestPath `
            -Raw -Encoding UTF8
        Add-Pass "source Bundle manifest XML is valid"
        Assert-Condition (
            [string]$sourceManifest.ApplicationPackage.AppVersion -eq $version4) `
            "source Bundle manifest uses $version4"
    } catch {
        Add-Failure "source Bundle manifest is invalid"
    }
} else {
    Add-Failure "source Bundle manifest is missing"
}

Assert-Text "src\StructuralToolbox\PluginEntry.cs" `
    "四季光伏工具箱 $version3 已加载" "load message uses v$version3"
Assert-Text "src\StructuralToolbox\Commands\ToolboxCommands.cs" `
    "四季光伏工具箱 $version3" "About dialog uses v$version3"
Assert-Text "src\StructuralToolbox\UI\ToolboxHomeControl.cs" `
    "版本 $version3" "home page uses v$version3"
Assert-Text "src\StructuralToolbox.Rail\Commands\VerificationCommands.cs" `
    "version is not $version4" "internal verification expects $version4"
Assert-Text "src\StructuralToolbox.Rail\Commands\RailDrawingService.cs" `
    "ToolEntityVersion = `"$version3`"" "rail metadata uses v$version3"
Assert-Text "scripts\test.ps1" "RELEASE_VERSION=$version3" `
    "regression suite reports v$version3"
Assert-Text "README.md" "当前发布版本为``v$version3``" `
    "README reports v$version3"
Assert-Text "README.md" "``$version4``" `
    "README reports assembly version $version4"

foreach ($document in @(
    "docs\安装与兼容性说明_v$version3.md",
    "docs\安装器实施记录_v$version3.md",
    "docs\安装器验证记录_v$version3.md")) {
    Assert-Condition (Test-Path -LiteralPath (Resolve-ProjectPath $document) `
        -PathType Leaf) "release document exists: $document"
}

$projectExpectations = [ordered]@{
    "src\StructuralToolbox.Contracts\StructuralToolbox.Contracts.csproj" = @()
    "src\StructuralToolbox\StructuralToolbox.csproj" =
        @("StructuralToolbox.Contracts")
    "src\StructuralToolbox.Rail\StructuralToolbox.Rail.csproj" =
        @("StructuralToolbox.Contracts")
    "src\StructuralToolbox.Ballast\StructuralToolbox.Ballast.csproj" =
        @("StructuralToolbox.Contracts")
    "src\StructuralToolbox.ArrayRecognition\StructuralToolbox.ArrayRecognition.csproj" =
        @()
    "src\StructuralToolbox.SupportDetail\StructuralToolbox.SupportDetail.csproj" =
        @("StructuralToolbox.Contracts")
    "src\StructuralToolbox.Update\StructuralToolbox.Update.csproj" =
        @("StructuralToolbox.Contracts")
    "src\StructuralToolbox.DrawingTranslation\StructuralToolbox.DrawingTranslation.csproj" =
        @("StructuralToolbox.Contracts")
}
foreach ($entry in $projectExpectations.GetEnumerator()) {
    $actualReferences = @(Get-ProjectReferences $entry.Key)
    $expectedReferences = @($entry.Value)
    Assert-Condition (Test-ExactSet $actualReferences $expectedReferences) `
        "$($entry.Key) keeps the approved module boundary"
}

Assert-Text "scripts\package-installer.ps1" `
    '[Parameter(Mandatory = $true)]' `
    "generic packager requires an explicit version"
Assert-Text "scripts\package-installer.ps1" `
    'release-v$requestedVersion3.json' `
    "generic packager resolves the versioned release definition"
Assert-Text "scripts\New-ReleaseEvidence.ps1" `
    'release-v$version3.json' `
    "release evidence resolves the versioned release definition"

$matrixPath = Resolve-ProjectPath `
    "bundle\manifest-template\compatibility-matrix.json"
if (Test-Path -LiteralPath $matrixPath -PathType Leaf) {
    try {
        $matrix = Get-Content -LiteralPath $matrixPath -Raw -Encoding UTF8 |
            ConvertFrom-Json
        $verifiedTargets = @($matrix.targets | Where-Object {
            [string]$_.status -eq "verified"
        })
        Assert-Condition ($verifiedTargets.Count -gt 0) `
            "compatibility matrix has at least one verified target"
        foreach ($target in $verifiedTargets) {
            $payloadPath = Resolve-ProjectPath `
                (([string]$target.sourceModule).Replace("/", "\"))
            Assert-Condition (Test-Path -LiteralPath $payloadPath -PathType Leaf) `
                "verified target $($target.series) has a source payload"
        }
    } catch {
        Add-Failure "compatibility matrix is invalid: $($_.Exception.Message)"
    }
} else {
    Add-Failure "compatibility matrix is missing"
}

$bundleDirectory = Resolve-ProjectPath `
    "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1"
$moduleNames = @(
    "StructuralToolbox.dll",
    "StructuralToolbox.Contracts.dll",
    "StructuralToolbox.Rail.dll",
    "StructuralToolbox.Ballast.dll",
    "StructuralToolbox.Update.dll",
    "StructuralToolbox.DrawingTranslation.dll"
)
if ($rank -ge 1) {
    $actualBundleFiles = if (Test-Path -LiteralPath $bundleDirectory) {
        @(Get-ChildItem -LiteralPath $bundleDirectory -File | ForEach-Object {
            $_.Name
        })
    } else {
        @()
    }
    Assert-Condition (Test-ExactSet $actualBundleFiles $moduleNames) `
        "built Bundle contains exactly the six approved release DLLs"
    foreach ($moduleName in $moduleNames) {
        $modulePath = Join-Path $bundleDirectory $moduleName
        if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
            Add-Failure "built module is missing: $moduleName"
            continue
        }
        try {
            $fileVersion = (Get-Item -LiteralPath $modulePath).VersionInfo.FileVersion
            Assert-Condition ([version]$fileVersion -eq [version]$version4) `
                "$moduleName has assembly version $version4"
        } catch {
            Add-Failure "$moduleName does not expose a valid file version"
        }
    }
}

$releaseDirectoryName = "SijiPVToolbox_v${version3}_AutoCAD2020"
$releaseDirectory = Resolve-ProjectPath ("outputs\" + $releaseDirectoryName)
$installerName = "四季光伏工具箱_Setup_v$version3.exe"
if ($null -ne $release) {
    $installerName = [string]$release.setupBaseName + "_v$version3.exe"
}
if ($rank -ge 2) {
    $outputDirectories = @(Get-ChildItem -LiteralPath `
        (Resolve-ProjectPath "outputs") -Directory -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match '^SijiPVToolbox_v\d+\.\d+\.\d+_AutoCAD2020$'
        } | ForEach-Object { $_.Name })
    Assert-Condition (Test-ExactSet $outputDirectories @($releaseDirectoryName)) `
        "outputs contains only the current toolbox release directory"
    $releaseFiles = if (Test-Path -LiteralPath $releaseDirectory) {
        @(Get-ChildItem -LiteralPath $releaseDirectory -File |
            ForEach-Object { $_.Name })
    } else {
        @()
    }
    Assert-Condition (Test-ExactSet $releaseFiles @($installerName)) `
        "published release contains exactly $installerName"
    $installerPath = Join-Path $releaseDirectory $installerName
    if ((Test-Path -LiteralPath $installerPath -PathType Leaf) -and
        $null -ne $release) {
        $signature = Get-AuthenticodeSignature -LiteralPath $installerPath
        Assert-Condition ($signature.Status.ToString() -eq
            [string]$release.authenticodeExpectation) `
            "published installer signature matches the release definition"
        Add-Pass ("published installer SHA-256 " +
            (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash)
    }

    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($null -ne $git) {
        $releaseStatusArguments = @(
            "-C", $projectRoot, "status", "--porcelain", "--",
            "src/StructuralToolbox",
            "src/StructuralToolbox.Contracts",
            "src/StructuralToolbox.Rail",
            "src/StructuralToolbox.Ballast",
            "src/StructuralToolbox.ArrayRecognition",
            "src/StructuralToolbox.SupportDetail",
            "src/StructuralToolbox.Update",
            "src/StructuralToolbox.DrawingTranslation",
            "scripts", "tests", "bundle", "docs",
            "README.md", "CHANGELOG.md"
        )
        $releaseStatus = @(& $git.Source @releaseStatusArguments 2>$null)
        Assert-Condition ($releaseStatus.Count -eq 0) `
            "release-relevant Git paths are committed"
    } else {
        Add-Warning "git is unavailable; committed release state was not checked"
    }
}

if ($rank -ge 3) {
    $installedRoots = @(
        (Join-Path $env:APPDATA `
            "Autodesk\ApplicationPlugins\ZhStructureToolbox.bundle"),
        (Join-Path $env:ProgramFiles `
            "Autodesk\ApplicationPlugins\ZhStructureToolbox.bundle")
    )
    $existingRoots = @($installedRoots | Where-Object {
        Test-Path -LiteralPath $_ -PathType Container
    })
    Assert-Condition ($existingRoots.Count -le 1) `
        "at most one installation scope contains the toolbox"
    if ($existingRoots.Count -eq 0) {
        Add-Warning "toolbox is not installed"
    } else {
        $installedRoot = $existingRoots[0]
        Add-Pass "installed Bundle found at $installedRoot"
        $installedManifestPath = Join-Path $installedRoot "PackageContents.xml"
        if (Test-Path -LiteralPath $installedManifestPath -PathType Leaf) {
            [xml]$installedManifest = Get-Content -LiteralPath `
                $installedManifestPath -Raw -Encoding UTF8
            $installedVersion =
                [string]$installedManifest.ApplicationPackage.AppVersion
            if ($installedVersion -eq $version4) {
                Add-Pass "installed Bundle reports $version4"
            } else {
                Add-Warning "installed Bundle reports $installedVersion; release is $version4"
            }
        } else {
            Add-Failure "installed Bundle manifest is missing"
        }
        foreach ($moduleName in $moduleNames) {
            $installedMatches = @(Get-ChildItem -LiteralPath $installedRoot `
                -Recurse -File -Filter $moduleName)
            if ($installedMatches.Count -ne 1) {
                Add-Failure "installed Bundle must contain one $moduleName"
                continue
            }
            $sourcePath = Join-Path $bundleDirectory $moduleName
            if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
                $installedHash = (Get-FileHash -LiteralPath `
                    $installedMatches[0].FullName -Algorithm SHA256).Hash
                $sourceHash = (Get-FileHash -LiteralPath $sourcePath `
                    -Algorithm SHA256).Hash
                if ($installedHash -eq $sourceHash) {
                    Add-Pass "installed $moduleName matches the built release"
                } else {
                    Add-Warning "installed $moduleName differs from the built release; reinstall and restart AutoCAD"
                }
            }
        }
    }
    $cadProcesses = @(Get-Process acad,accoreconsole -ErrorAction SilentlyContinue)
    if ($cadProcesses.Count -gt 0) {
        Add-Warning "AutoCAD or Core Console is running; installed DLLs cannot be replaced safely"
    } else {
        Add-Pass "AutoCAD and Core Console are closed"
    }
}

Write-Host ""
Write-Host ("RELEASE_READINESS_PASSES=" + $passes.Count)
Write-Host ("RELEASE_READINESS_WARNINGS=" + $warnings.Count)
Write-Host ("RELEASE_READINESS_BLOCKERS=" + $failures.Count)
if ($failures.Count -gt 0) {
    Write-Error ("RELEASE_READINESS=BLOCKED`n" + ($failures -join "`n"))
    exit 1
}
Write-Host "RELEASE_READINESS=PASS"
