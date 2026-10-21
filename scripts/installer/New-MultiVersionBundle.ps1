[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][version]$Version,
    [Parameter(Mandatory = $true)][string]$ProductCode,
    [Parameter(Mandatory = $true)][string]$UpgradeCode,
    [Parameter(Mandatory = $true)][string]$Publisher,
    [Parameter(Mandatory = $true)][string]$OutputBundle,
    [Parameter(Mandatory = $true)][string]$BuildProvenance,
    [string[]]$IncludeStatus = @("verified"),
    [string]$ProjectRoot,
    [string]$CompatibilityMatrix,
    [string]$ManifestTemplate
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}
if ([string]::IsNullOrWhiteSpace($CompatibilityMatrix)) {
    $CompatibilityMatrix = Join-Path $ProjectRoot `
        "bundle\manifest-template\compatibility-matrix.json"
}
if ([string]::IsNullOrWhiteSpace($ManifestTemplate)) {
    $ManifestTemplate = Join-Path $ProjectRoot `
        "bundle\manifest-template\PackageContents.base.xml"
}

function Assert-GuidText([string]$Value, [string]$Name) {
    $parsed = [guid]::Empty
    if (-not [guid]::TryParse($Value, [ref]$parsed) -or $parsed -eq [guid]::Empty) {
        throw "$Name is not a valid non-empty GUID: $Value"
    }
}

function Get-PeMachine([string]$AssemblyPath) {
    $stream = $null
    $reader = $null
    try {
        $stream = [IO.File]::OpenRead($AssemblyPath)
        $reader = New-Object IO.BinaryReader($stream)
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Invalid PE header: $AssemblyPath" }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw "Invalid PE signature: $AssemblyPath" }
        return $reader.ReadUInt16()
    } finally {
        if ($null -ne $reader) { $reader.Dispose() }
        elseif ($null -ne $stream) { $stream.Dispose() }
    }
}

function Set-XmlAttribute($Document, $Element, [string]$Name, [string]$Value) {
    $attribute = $Document.CreateAttribute($Name)
    $attribute.Value = $Value
    $null = $Element.Attributes.Append($attribute)
}

function Get-ProjectRelativePath([string]$Path, [string]$ProjectPrefix) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($ProjectPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Module is outside the project root: $fullPath"
    }
    return $fullPath.Substring($ProjectPrefix.Length).Replace("\", "/")
}

Assert-GuidText $ProductCode "ProductCode"
Assert-GuidText $UpgradeCode "UpgradeCode"
if ([String]::IsNullOrWhiteSpace($Publisher)) { throw "Publisher must not be empty." }

$projectFullPath = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd("\")
$projectPrefix = $projectFullPath + "\"
$workPrefix = [IO.Path]::GetFullPath((Join-Path $projectFullPath "work")).TrimEnd("\") + "\"
$outputFullPath = [IO.Path]::GetFullPath($OutputBundle).TrimEnd("\")
if (-not $outputFullPath.EndsWith(".bundle", [StringComparison]::OrdinalIgnoreCase) -or
    $outputFullPath.Length -le $projectFullPath.Length + 8 -or
    -not $outputFullPath.StartsWith($workPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputBundle must be a specific .bundle directory under work/: $outputFullPath"
}

$matrix = Get-Content -LiteralPath $CompatibilityMatrix -Raw -Encoding UTF8 |
    ConvertFrom-Json
$targets = @($matrix.targets | Where-Object { $IncludeStatus -contains $_.status })
if ($targets.Count -eq 0) { throw "No compatibility targets matched." }

$provenancePath = [IO.Path]::GetFullPath($BuildProvenance)
if (-not $provenancePath.StartsWith($workPrefix,
        [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
    throw "BuildProvenance must be an existing file under work/: $provenancePath"
}
$provenance = Get-Content -LiteralPath $provenancePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([version]([string]$provenance.appVersion) -ne $Version -or
    [string]$provenance.sdkValidation.validation -ne "passed") {
    throw "BuildProvenance version or SDK validation is invalid."
}

[xml]$sourceManifest = Get-Content -LiteralPath (Join-Path $ProjectRoot `
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml") -Raw -Encoding UTF8
$sourceEntries = @($sourceManifest.SelectNodes("//Components/ComponentEntry"))
if ($sourceEntries.Count -ne 4) {
    throw "Source manifest must declare exactly shell, rail, ballast, and drawing translation command entries."
}

$preparedTargets = @()
foreach ($target in $targets) {
    $provenanceTarget = @($provenance.targets | Where-Object {
        [string]$_.series -eq [string]$target.series
    })
    if ($provenanceTarget.Count -ne 1) {
        throw "BuildProvenance has no unique target for $($target.series)."
    }
    $files = @($provenanceTarget[0].files)
    if ($files.Count -ne 6) {
        throw "Target $($target.series) must contain shell, contracts, rail, ballast, update, and drawing translation DLLs."
    }
    $expectedNames = @("StructuralToolbox", "StructuralToolbox.Contracts",
        "StructuralToolbox.Rail", "StructuralToolbox.Ballast",
        "StructuralToolbox.Update", "StructuralToolbox.DrawingTranslation")
    $actualNames = @($files | ForEach-Object { [string]$_.name } |
        Sort-Object -Unique)
    if (@(Compare-Object $expectedNames $actualNames).Count -ne 0) {
        throw "Target $($target.series) contains an unexpected module set: " +
            ($actualNames -join ", ")
    }

    $targetDirectory = Split-Path -Parent (
        ([string]$target.modulePath -replace "/", "\").TrimStart("\"))
    $preparedFiles = @()
    foreach ($file in $files) {
        $source = [IO.Path]::GetFullPath((Join-Path $ProjectRoot `
            ([string]$file.sourcePath -replace "/", "\")))
        if (-not $source.StartsWith($projectPrefix,
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Module source is missing or outside the project: $source"
        }
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ($sourceHash -ne [string]$file.sourceSha256) {
            throw "BuildProvenance hash mismatch: $source"
        }
        $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($source)
        if ($assemblyName.Version -ne $Version) {
            throw "Module $($assemblyName.Name) is $($assemblyName.Version), expected $Version."
        }
        if ((Get-PeMachine $source) -ne 0x8664) {
            throw "Module is not x64: $source"
        }
        $relative = Join-Path $targetDirectory (Split-Path -Leaf $source)
        $targetPath = [IO.Path]::GetFullPath((Join-Path $outputFullPath $relative))
        if (-not $targetPath.StartsWith($outputFullPath + "\",
            [StringComparison]::OrdinalIgnoreCase)) {
            throw "Module target escapes the bundle: $relative"
        }
        $preparedFiles += [pscustomobject]@{
            Name = $assemblyName.Name
            Source = $source
            SourceRelative = Get-ProjectRelativePath $source $projectPrefix
            SourceSha256 = $sourceHash
            RelativeModule = $relative
            Target = $targetPath
            StagedSha256 = $null
        }
    }
    $preparedTargets += [pscustomobject]@{
        Definition = $target
        Files = $preparedFiles
    }
}

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}
New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null

[xml]$manifest = Get-Content -LiteralPath $ManifestTemplate -Raw -Encoding UTF8
$package = $manifest.ApplicationPackage
$package.SetAttribute("AppVersion", $Version.ToString(4))
$package.SetAttribute("ProductCode", ([guid]$ProductCode).ToString("B").ToUpperInvariant())
$package.SetAttribute("UpgradeCode", ([guid]$UpgradeCode).ToString("B").ToUpperInvariant())
$package.CompanyDetails.SetAttribute("Name", $Publisher)
$marker = $manifest.SelectSingleNode("//comment()[contains(., 'GENERATED_COMPONENTS')]")
if ($null -eq $marker) { throw "Manifest component marker was not found." }

foreach ($preparedTarget in $preparedTargets) {
    foreach ($file in $preparedTarget.Files) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $file.Target) `
            -Force | Out-Null
        Copy-Item -LiteralPath $file.Source -Destination $file.Target -Force
        $file.StagedSha256 = (Get-FileHash -LiteralPath $file.Target `
            -Algorithm SHA256).Hash
        if ($file.StagedSha256 -ne $file.SourceSha256) {
            throw "Staged module hash mismatch: $($file.RelativeModule)"
        }
    }

    $definition = $preparedTarget.Definition
    $components = $manifest.CreateElement("Components")
    Set-XmlAttribute $manifest $components "Description" `
        ("AutoCAD {0} Win64 ({1})" -f $definition.year, $definition.status)
    $requirements = $manifest.CreateElement("RuntimeRequirements")
    Set-XmlAttribute $manifest $requirements "OS" "Win64"
    Set-XmlAttribute $manifest $requirements "Platform" "AutoCAD*"
    Set-XmlAttribute $manifest $requirements "SeriesMin" ([string]$definition.series)
    Set-XmlAttribute $manifest $requirements "SeriesMax" ([string]$definition.series)
    $null = $components.AppendChild($requirements)

    foreach ($sourceEntry in $sourceEntries) {
        $entry = $manifest.ImportNode($sourceEntry, $true)
        $sourceLeaf = Split-Path -Leaf (([string]$sourceEntry.ModuleName).Replace("/", "\"))
        $moduleFile = @($preparedTarget.Files | Where-Object {
            (Split-Path -Leaf $_.Target) -eq $sourceLeaf
        })
        if ($moduleFile.Count -ne 1) {
            throw "Manifest module does not match exactly one built file: $sourceLeaf"
        }
        $entry.SetAttribute("ModuleName", "./" +
            ($moduleFile[0].RelativeModule -replace "\\", "/"))
        $seriesSuffix = (([string]$definition.series) -replace "\.", "_")
        if (-not ([string]$entry.AppName).EndsWith("." + $seriesSuffix,
            [StringComparison]::OrdinalIgnoreCase)) {
            $entry.SetAttribute("AppName", ([string]$entry.AppName) + "." +
                $seriesSuffix)
        }
        $null = $components.AppendChild($entry)
    }
    $null = $package.InsertBefore($components, $marker)
}
$null = $package.RemoveChild($marker)

$manifestPath = Join-Path $outputFullPath "PackageContents.xml"
$settings = New-Object Xml.XmlWriterSettings
$settings.Encoding = New-Object Text.UTF8Encoding($false)
$settings.Indent = $true
$settings.NewLineChars = "`r`n"
$writer = [Xml.XmlWriter]::Create($manifestPath, $settings)
try { $manifest.Save($writer) } finally { $writer.Dispose() }

$inventory = [ordered]@{
    schemaVersion = 4
    appVersion = $Version.ToString(4)
    productCode = ([guid]$ProductCode).ToString("B").ToUpperInvariant()
    upgradeCode = ([guid]$UpgradeCode).ToString("B").ToUpperInvariant()
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    buildProvenanceSha256 = (Get-FileHash -LiteralPath $provenancePath `
        -Algorithm SHA256).Hash
    targets = @($preparedTargets | ForEach-Object {
        $primary = @($_.Files | Where-Object { $_.Name -eq "StructuralToolbox" })[0]
        [ordered]@{
            year = [int]$_.Definition.year
            series = [string]$_.Definition.series
            status = [string]$_.Definition.status
            runtimeFamily = [string]$_.Definition.runtimeFamily
            sourcePath = $primary.SourceRelative
            sourceSha256 = $primary.SourceSha256
            modulePath = ($primary.RelativeModule -replace "\\", "/")
            stagedSha256 = $primary.StagedSha256
            sha256 = $primary.StagedSha256
            files = @($_.Files | ForEach-Object {
                [ordered]@{
                    name = $_.Name
                    sourcePath = $_.SourceRelative
                    sourceSha256 = $_.SourceSha256
                    modulePath = ($_.RelativeModule -replace "\\", "/")
                    stagedSha256 = $_.StagedSha256
                }
            })
        }
    })
}
$inventoryPath = Join-Path (Split-Path -Parent $outputFullPath) `
    "bundle-inventory.json"
$inventory | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $inventoryPath -Encoding UTF8
Write-Output $outputFullPath
