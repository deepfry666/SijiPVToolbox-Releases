[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateDll,

    [Parameter(Mandatory = $true)]
    [string]$CandidateBuildResult,

    [Parameter(Mandatory = $true)]
    [string]$AutoCADDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$railSourceRoot = Join-Path $projectRoot "src\StructuralToolbox.Rail"
$candidateVersionPath = [IO.Path]::GetFullPath($CandidateDll)
if (-not (Test-Path -LiteralPath $candidateVersionPath -PathType Leaf)) {
    throw "Candidate DLL was not found: $candidateVersionPath"
}
$candidateVersionIdentity =
    [Reflection.AssemblyName]::GetAssemblyName($candidateVersionPath)
$expectedAssemblyVersion = $candidateVersionIdentity.Version
$releaseVersion3 = $expectedAssemblyVersion.ToString(3)
$verificationRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot `
    "work\real-drawing-verification-v$releaseVersion3"))
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$expectedCandidateName = "StructuralToolbox"
$executionAssemblyName = "FourJTools.RealDrawingVerify"
$markerTypeName = "FourJTools.Verification.RealDrawingVerificationMarker"
$markerValue = "FOURJTOOLS_INTERNAL_REAL_DRAWING_VERIFY_V1"
$utf8NoBom = New-Object Text.UTF8Encoding($false)
$builderPath = [IO.Path]::GetFullPath($PSCommandPath)
$builderHashBefore = (Get-FileHash -LiteralPath $builderPath `
    -Algorithm SHA256).Hash

function Resolve-File([string]$Path, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$Label path is empty."
    }
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Label was not found: $fullPath"
    }
    return $fullPath
}

function Get-RequiredProperty($Object, [string]$Name, [string]$Label) {
    if ($null -eq $Object) {
        throw "$Label is missing."
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "$Label does not contain '$Name'."
    }
    return $property.Value
}

function Assert-UnderVerificationRoot([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $script:verificationRoot.TrimEnd("\") + "\"
    if (-not $fullPath.StartsWith($prefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputDirectory must be a child of $script:verificationRoot"
    }
    return $fullPath
}

function Get-SourceCodeFingerprint {
    $sourceRoots = @(
        (Join-Path $script:projectRoot "src\StructuralToolbox"),
        (Join-Path $script:projectRoot "src\StructuralToolbox.Contracts"),
        (Join-Path $script:projectRoot "src\StructuralToolbox.Rail"),
        (Join-Path $script:projectRoot "src\StructuralToolbox.Ballast"),
        (Join-Path $script:projectRoot "src\StructuralToolbox.ArrayRecognition"),
        (Join-Path $script:projectRoot "src\StructuralToolbox.Update"),
        (Join-Path $script:projectRoot "src\StructuralToolbox.DrawingTranslation")
    )
    $sourceFiles = @($sourceRoots | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -File
    })
    $items = @($sourceFiles |
        Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring(
                $script:projectRoot.Length).TrimStart("\")
            $hash = (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash
            "$($relative.Replace('\', '/'))|$hash"
        })
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes($items -join "`n"))).Replace("-", "")
    } finally {
        $algorithm.Dispose()
    }
}

function Get-RelativeProjectPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $script:projectRoot.TrimEnd("\") + "\"
    if (-not $fullPath.StartsWith($prefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Verification source is outside the project root: $fullPath"
    }
    return $fullPath.Substring($prefix.Length).Replace("\", "/")
}

function Get-SourceInventory([string[]]$Files) {
    return @($Files | Sort-Object -Unique | ForEach-Object {
        [ordered]@{
            path = Get-RelativeProjectPath $_
            sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
        }
    })
}

function Get-InventoryFingerprint($Inventory) {
    return (@($Inventory | ForEach-Object {
        ([string]$_.path) + "|" + ([string]$_.sha256)
    }) -join "`n")
}

function Get-AssemblyMvid([string]$Path) {
    $assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($Path))
    return $assembly.ManifestModule.ModuleVersionId.ToString("D")
}

function Get-PeMachine([string]$AssemblyPath) {
    $stream = $null
    $reader = $null
    try {
        $stream = [IO.File]::Open($AssemblyPath, [IO.FileMode]::Open,
            [IO.FileAccess]::Read, [IO.FileShare]::Read)
        $reader = New-Object IO.BinaryReader($stream)
        if ($reader.ReadUInt16() -ne 0x5A4D) {
            throw "Invalid PE header: $AssemblyPath"
        }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or $peOffset -gt $stream.Length - 6) {
            throw "Invalid PE offset: $AssemblyPath"
        }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            throw "Invalid PE signature: $AssemblyPath"
        }
        return $reader.ReadUInt16()
    } finally {
        if ($null -ne $reader) { $reader.Dispose() }
        elseif ($null -ne $stream) { $stream.Dispose() }
    }
}

function Get-SignerSubject($Signature) {
    if ($null -eq $Signature.SignerCertificate) {
        return $null
    }
    return [string]$Signature.SignerCertificate.Subject
}

$candidatePath = Resolve-File $CandidateDll "Candidate DLL"
$candidateBuildResultPath = Resolve-File $CandidateBuildResult `
    "Candidate build result"
$autoCADRoot = [IO.Path]::GetFullPath($AutoCADDir)
$outputRoot = Assert-UnderVerificationRoot $OutputDirectory
$compilerPath = Resolve-File $compiler "C# compiler"

if (Test-Path -LiteralPath $outputRoot -PathType Leaf) {
    throw "OutputDirectory is a file: $outputRoot"
}
if (Test-Path -LiteralPath $outputRoot -PathType Container) {
    $existing = @(Get-ChildItem -LiteralPath $outputRoot -Force)
    if ($existing.Count -ne 0) {
        throw "OutputDirectory must be empty so evidence cannot be overwritten: $outputRoot"
    }
} else {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
}

$candidateHashBefore = (Get-FileHash -LiteralPath $candidatePath `
    -Algorithm SHA256).Hash
$candidateBuildResultHashBefore = (Get-FileHash `
    -LiteralPath $candidateBuildResultPath -Algorithm SHA256).Hash
$sourceCodeHashBefore = Get-SourceCodeFingerprint
$candidateAssembly = [Reflection.AssemblyName]::GetAssemblyName($candidatePath)
if ($candidateAssembly.Name -ne $expectedCandidateName -or
    $candidateAssembly.Version -ne $expectedAssemblyVersion) {
    throw "Candidate must be StructuralToolbox $expectedAssemblyVersion; actual: " +
        "$($candidateAssembly.Name) $($candidateAssembly.Version)."
}
if ((Get-PeMachine $candidatePath) -ne 0x8664) {
    throw "Formal candidate must be an x64 PE (Machine 0x8664)."
}

$build = Get-Content -LiteralPath $candidateBuildResultPath -Raw `
    -Encoding UTF8 | ConvertFrom-Json
$schemaVersion = [int](Get-RequiredProperty $build "schemaVersion" `
    "Candidate build result")
if ($schemaVersion -lt 5) {
    throw "Candidate build result must use schemaVersion 5 or newer."
}
if ([bool](Get-RequiredProperty $build "testBuild" `
        "Candidate build result")) {
    throw "Candidate build result must describe the release candidate, not an installer-test build."
}
if ([version](Get-RequiredProperty $build "appVersion" `
        "Candidate build result") -ne $expectedAssemblyVersion) {
    throw "Candidate build result version is not $expectedAssemblyVersion."
}
if ([string](Get-RequiredProperty $build "sourceCodeSha256" `
        "Candidate build result") -ne $sourceCodeHashBefore) {
    throw "Current StructuralToolbox source fingerprint differs from CandidateBuildResult."
}

$payloads = @(Get-RequiredProperty $build "payloadModules" `
    "Candidate build result")
$payload = @($payloads | Where-Object {
    [string](Get-RequiredProperty $_ "series" "Payload") -eq "R23.1"
})
if ($payload.Count -ne 1) {
    throw "Candidate build result must contain exactly one R23.1 payload."
}
$verifiedSeries = @(Get-RequiredProperty $build "verifiedSeries" `
    "Candidate build result")
if (@($verifiedSeries | Where-Object { [string]$_ -eq "R23.1" }).Count -ne 1) {
    throw "Candidate build result must classify R23.1 as verified."
}
if ([int](Get-RequiredProperty $payload[0] "year" "R23.1 payload") -ne 2020 -or
    [string](Get-RequiredProperty $payload[0] "runtimeFamily" `
        "R23.1 payload") -ne "legacy-netfx") {
    throw "R23.1 payload year or runtime family is invalid."
}
$payloadSourceHash = [string](Get-RequiredProperty $payload[0] `
    "sourceSha256" "R23.1 payload")
$payloadStagedHash = [string](Get-RequiredProperty $payload[0] `
    "stagedSha256" "R23.1 payload")
if ($payloadSourceHash -ne $candidateHashBefore -or
    $payloadStagedHash -ne $candidateHashBefore) {
    throw "Candidate SHA-256 does not match both source and staged R23.1 payload hashes."
}

$sdkValidation = Get-RequiredProperty $build "sdkValidation" `
    "Candidate build result"
if ([string](Get-RequiredProperty $sdkValidation "validation" `
        "SDK validation") -ne "passed" -or
    [string](Get-RequiredProperty $sdkValidation "targetSeries" `
        "SDK validation") -ne "R23.1" -or
    [int](Get-RequiredProperty $sdkValidation "targetYear" `
        "SDK validation") -ne 2020 -or
    [string](Get-RequiredProperty $sdkValidation "buildProfile" `
        "SDK validation") -ne "acad2020-netfx" -or
    [string](Get-RequiredProperty $sdkValidation "runtimeFamily" `
        "SDK validation") -ne "legacy-netfx") {
    throw "Candidate SDK validation is not the verified AutoCAD 2020/R23.1 profile."
}
if ([string](Get-RequiredProperty $sdkValidation "outputSha256" `
        "SDK validation") -ne $candidateHashBefore) {
    throw "Candidate SHA-256 differs from the SDK-validated build output."
}

$acadExecutable = Resolve-File (Join-Path $autoCADRoot "acad.exe") "acad.exe"
$acadVersion = (Get-Item -LiteralPath $acadExecutable).VersionInfo
if ([string]$acadVersion.ProductVersion -ne
        [string](Get-RequiredProperty $sdkValidation "autoCADProductVersion" `
            "SDK validation") -or
    [string]$acadVersion.FileVersion -ne
        [string](Get-RequiredProperty $sdkValidation "autoCADFileVersion" `
            "SDK validation")) {
    throw "AutoCAD executable identity differs from CandidateBuildResult."
}

$sdkRecords = @(Get-RequiredProperty $sdkValidation "sdkFiles" "SDK validation")
$requiredSdkNames = @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll", "AdWindows.dll")
$sdkEvidence = @()
foreach ($name in $requiredSdkNames) {
    $sdkPath = Resolve-File (Join-Path $autoCADRoot $name) $name
    $expected = @($sdkRecords | Where-Object {
        [string](Get-RequiredProperty $_ "name" "SDK file record") -eq $name
    })
    if ($expected.Count -ne 1) {
        throw "CandidateBuildResult must contain exactly one SDK record for $name."
    }
    $hash = (Get-FileHash -LiteralPath $sdkPath -Algorithm SHA256).Hash
    if ($hash -ne [string](Get-RequiredProperty $expected[0] "sha256" `
            "$name SDK record")) {
        throw "$name SHA-256 differs from CandidateBuildResult."
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $sdkPath
    $signerSubject = Get-SignerSubject $signature
    $file = Get-Item -LiteralPath $sdkPath
    $expectedAssemblyVersionText = [string](Get-RequiredProperty $expected[0] `
        "assemblyVersion" "$name SDK record")
    $actualAssemblyVersionText = $null
    if ($name -in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")) {
        $actualAssemblyVersion =
            [Reflection.AssemblyName]::GetAssemblyName($sdkPath).Version
        $actualAssemblyVersionText = $actualAssemblyVersion.ToString()
        if ($actualAssemblyVersion.Major -ne 23 -or
            $actualAssemblyVersion.Minor -ne 1 -or
            $signature.Status -ne
                [Management.Automation.SignatureStatus]::Valid -or
            [string]::IsNullOrWhiteSpace($signerSubject) -or
            -not $signerSubject.Contains("Autodesk")) {
            throw "$name is not an independently valid Autodesk R23.1 managed API."
        }
    }
    if ([string]$actualAssemblyVersionText -ne
            [string]$expectedAssemblyVersionText -or
        [string]$file.VersionInfo.ProductVersion -ne
            [string](Get-RequiredProperty $expected[0] "productVersion" `
                "$name SDK record")) {
        throw "$name version identity differs from CandidateBuildResult."
    }
    if ($signature.Status.ToString() -ne
            [string](Get-RequiredProperty $expected[0] "signatureStatus" `
                "$name SDK record") -or
        [string]$signerSubject -ne
            [string](Get-RequiredProperty $expected[0] "signerSubject" `
                "$name SDK record")) {
        throw "$name signature identity differs from CandidateBuildResult."
    }
    $sdkEvidence += [ordered]@{
        name = $name
        path = $sdkPath
        sha256 = $hash
        assemblyVersion = $actualAssemblyVersionText
        productVersion = [string]$file.VersionInfo.ProductVersion
        signatureStatus = $signature.Status.ToString()
        signerSubject = $signerSubject
    }
}

$compilerVersion = (Get-Item -LiteralPath $compilerPath).VersionInfo.FileVersion
if ([string]$compilerVersion -ne
        [string](Get-RequiredProperty $sdkValidation "compilerVersion" `
            "SDK validation")) {
    throw "C# compiler version differs from CandidateBuildResult."
}

$coreSources = @(Get-ChildItem -LiteralPath (Join-Path $railSourceRoot "Core") `
    -Filter "*.cs" -File | Sort-Object FullName | ForEach-Object { $_.FullName })
$sourceFiles = @($coreSources + @(
    (Join-Path $railSourceRoot "Settings\RailSettings.cs"),
    (Join-Path $railSourceRoot "Settings\RailSettingsStore.cs"),
    (Join-Path $railSourceRoot "Commands\PanelRectangleReader.cs"),
    (Join-Path $railSourceRoot "Commands\QuantityTableService.cs"),
    (Join-Path $railSourceRoot "Commands\RailDrawingService.cs"),
    (Join-Path $railSourceRoot "Commands\RailCommands.cs"),
    (Join-Path $projectRoot "tests\RealDrawingProbe.cs"),
    (Join-Path $projectRoot "tests\RealDrawingVerificationAssemblyInfo.cs")
))
foreach ($sourceFile in $sourceFiles) {
    $null = Resolve-File $sourceFile "Verification source"
}
$sourceInventoryBefore = Get-SourceInventory $sourceFiles
$sourceInventoryFingerprintBefore = Get-InventoryFingerprint $sourceInventoryBefore

$outputPath = Join-Path $outputRoot ($executionAssemblyName + ".dll")
$pdbPath = Join-Path $outputRoot ($executionAssemblyName + ".pdb")
$recordPath = Join-Path $outputRoot "real-drawing-verifier-build.json"
$manifestPath = Join-Path $outputRoot "SHA256SUMS.txt"
$references = @(
    "/reference:$(Join-Path $autoCADRoot 'acmgd.dll')",
    "/reference:$(Join-Path $autoCADRoot 'acdbmgd.dll')",
    "/reference:$(Join-Path $autoCADRoot 'accoremgd.dll')",
    "/reference:$(Join-Path $autoCADRoot 'AdWindows.dll')",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:System.Xml.dll"
)
$compileArguments = @(
    "/nologo", "/target:library", "/platform:x64", "/optimize+",
    "/warn:4", "/utf8output", "/debug:pdbonly", "/langversion:5",
    "/define:INTERNAL_VERIFY",
    "/out:$outputPath"
) + $references + $sourceFiles

& $compilerPath $compileArguments
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
    throw "Real drawing verification assembly build failed with exit code $LASTEXITCODE."
}

$candidateHashAfter = (Get-FileHash -LiteralPath $candidatePath `
    -Algorithm SHA256).Hash
$candidateBuildResultHashAfter = (Get-FileHash `
    -LiteralPath $candidateBuildResultPath -Algorithm SHA256).Hash
$sourceCodeHashAfter = Get-SourceCodeFingerprint
$sourceInventoryAfter = Get-SourceInventory $sourceFiles
$builderHashAfter = (Get-FileHash -LiteralPath $builderPath `
    -Algorithm SHA256).Hash
if ($candidateHashAfter -ne $candidateHashBefore) {
    throw "Formal candidate changed while the internal verifier was built."
}
if ($candidateBuildResultHashAfter -ne $candidateBuildResultHashBefore) {
    throw "Candidate build result changed while the internal verifier was built."
}
if ($builderHashAfter -ne $builderHashBefore) {
    throw "Real drawing verifier builder changed while it was running."
}
if ($sourceCodeHashAfter -ne $sourceCodeHashBefore -or
    (Get-InventoryFingerprint $sourceInventoryAfter) -ne
        $sourceInventoryFingerprintBefore) {
    throw "Source changed while the internal verifier was built."
}
foreach ($sdk in $sdkEvidence) {
    if ((Get-FileHash -LiteralPath $sdk.path -Algorithm SHA256).Hash -ne
            [string]$sdk.sha256) {
        throw "$($sdk.name) changed while the internal verifier was built."
    }
}

$executionAssembly = [Reflection.AssemblyName]::GetAssemblyName($outputPath)
if ($executionAssembly.Name -ne $executionAssemblyName -or
    $executionAssembly.Version -ne $expectedAssemblyVersion) {
    throw "Internal execution assembly identity is invalid: " +
        "$($executionAssembly.Name) $($executionAssembly.Version)."
}
if ((Get-PeMachine $outputPath) -ne 0x8664) {
    throw "Internal execution assembly must be an x64 PE (Machine 0x8664)."
}
$loadedExecutionAssembly = [Reflection.Assembly]::LoadFile($outputPath)
$markerType = $loadedExecutionAssembly.GetType($markerTypeName, $false)
if ($null -eq $markerType) {
    throw "Internal verification marker type is missing."
}
$markerField = $markerType.GetField("Value",
    [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static)
if ($null -eq $markerField -or [string]$markerField.GetValue($null) -ne $markerValue) {
    throw "Internal verification marker value is invalid."
}

$record = [ordered]@{
    schemaVersion = 1
    verificationKind = "same-source-internal-instrumentation"
    formalCandidateExecutedInCoreConsole = $false
    claimBoundary = "The formal candidate is metadata-inspected and hash-bound, but is not NETLOADed into CoreConsole and no CAD command from it is executed."
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    projectRoot = $projectRoot
    formalCandidate = [ordered]@{
        path = $candidatePath
        assemblyName = $candidateAssembly.Name
        assemblyVersion = $candidateAssembly.Version.ToString()
        mvid = Get-AssemblyMvid $candidatePath
        peMachine = "0x8664"
        sha256 = $candidateHashBefore
        buildResultPath = $candidateBuildResultPath
        buildResultSha256 = $candidateBuildResultHashBefore
        buildResultSchemaVersion = $schemaVersion
        sourceCodeSha256 = $sourceCodeHashBefore
        payloadSeries = "R23.1"
    }
    executionAssembly = [ordered]@{
        path = $outputPath
        assemblyName = $executionAssembly.Name
        assemblyVersion = $executionAssembly.Version.ToString()
        mvid = $loadedExecutionAssembly.ManifestModule.ModuleVersionId.ToString("D")
        peMachine = "0x8664"
        sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
        bytes = (Get-Item -LiteralPath $outputPath).Length
        markerType = $markerTypeName
        markerValue = $markerValue
        pdbPath = if (Test-Path -LiteralPath $pdbPath -PathType Leaf) {
            $pdbPath
        } else {
            $null
        }
    }
    compiler = [ordered]@{
        path = $compilerPath
        fileVersion = $compilerVersion
        sha256 = (Get-FileHash -LiteralPath $compilerPath -Algorithm SHA256).Hash
        languageVersion = "5"
        platform = "x64"
        defineConstants = @("INTERNAL_VERIFY")
        options = @(
            "/target:library", "/platform:x64", "/optimize+", "/warn:4",
            "/utf8output", "/debug:pdbonly", "/langversion:5",
            "/define:INTERNAL_VERIFY"
        )
    }
    builder = [ordered]@{
        path = $builderPath
        sha256Before = $builderHashBefore
        sha256After = $builderHashAfter
        unchanged = $builderHashBefore -eq $builderHashAfter
    }
    instrumentation = [ordered]@{
        probePath = Join-Path $projectRoot "tests\RealDrawingProbe.cs"
        probeSha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot `
            "tests\RealDrawingProbe.cs") -Algorithm SHA256).Hash
        assemblyInfoPath = Join-Path $projectRoot `
            "tests\RealDrawingVerificationAssemblyInfo.cs"
        assemblyInfoSha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot `
            "tests\RealDrawingVerificationAssemblyInfo.cs") -Algorithm SHA256).Hash
        sourceSetPolicy = "Core plus rail command/services/settings plus probe and dedicated AssemblyInfo"
        excludedReleaseSources = @(
            "UI", "PluginEntry.cs", "Properties/AssemblyInfo.cs",
            "Commands/VerificationCommands.cs"
        )
    }
    sdkValidation = [ordered]@{
        targetYear = 2020
        targetSeries = "R23.1"
        buildProfile = "acad2020-netfx"
        autoCADDirectory = $autoCADRoot
        sdkFiles = $sdkEvidence
    }
    sourceInventory = $sourceInventoryBefore
    sourceCodeSha256Before = $sourceCodeHashBefore
    sourceCodeSha256After = $sourceCodeHashAfter
    formalCandidateSha256Before = $candidateHashBefore
    formalCandidateSha256After = $candidateHashAfter
    candidateBuildResultSha256Before = $candidateBuildResultHashBefore
    candidateBuildResultSha256After = $candidateBuildResultHashAfter
}
[IO.File]::WriteAllText($recordPath,
    ($record | ConvertTo-Json -Depth 12), $utf8NoBom)

$hashFiles = @($outputPath, $recordPath)
if (Test-Path -LiteralPath $pdbPath -PathType Leaf) {
    $hashFiles += $pdbPath
}
$hashLines = @($hashFiles | Sort-Object | ForEach-Object {
    $relative = [IO.Path]::GetFileName($_)
    $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
    $hash + " *" + $relative
})
[IO.File]::WriteAllText($manifestPath,
    (($hashLines -join [Environment]::NewLine) + [Environment]::NewLine),
    $utf8NoBom)

Write-Host "Built internal real-drawing verifier: $outputPath"
Write-Host "FORMAL_CANDIDATE_EXECUTED=NO"
Write-Host "VERIFICATION_BASIS=SAME_SOURCE_INTERNAL_INSTRUMENTATION"

if ($PassThru) {
    [pscustomobject][ordered]@{
        validation = "passed"
        verificationKind = "same-source-internal-instrumentation"
        formalCandidatePath = $candidatePath
        formalCandidateSha256 = $candidateHashBefore
        candidateBuildResultPath = $candidateBuildResultPath
        candidateBuildResultSha256 = $candidateBuildResultHashBefore
        sourceCodeSha256 = $sourceCodeHashBefore
        executionAssemblyPath = $outputPath
        executionAssemblySha256 =
            (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
        executionAssemblyMvid =
            $loadedExecutionAssembly.ManifestModule.ModuleVersionId.ToString("D")
        executionMarker = $markerValue
        builderSha256 = $builderHashBefore
        buildRecordPath = $recordPath
        buildRecordSha256 =
            (Get-FileHash -LiteralPath $recordPath -Algorithm SHA256).Hash
        hashManifestPath = $manifestPath
    }
}
