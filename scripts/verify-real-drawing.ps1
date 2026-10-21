[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputDwg,

    [Parameter(Mandatory = $true)]
    [string]$AutoCADDir,

    [Parameter(Mandatory = $true)]
    [string]$CandidateDll,

    [Parameter(Mandatory = $true)]
    [string]$CandidateBuildResult,

    [ValidateSet("RailsOnly")]
    [string]$Mode = "RailsOnly",

    [ValidateRange(30, 86400)]
    [int]$TimeoutSeconds = 1200
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$candidateVersionPath = [IO.Path]::GetFullPath($CandidateDll)
if (-not (Test-Path -LiteralPath $candidateVersionPath -PathType Leaf)) {
    throw "Candidate DLL was not found: $candidateVersionPath"
}
$candidateVersionIdentity =
    [Reflection.AssemblyName]::GetAssemblyName($candidateVersionPath)
$expectedAssemblyVersion = $candidateVersionIdentity.Version
$releaseVersion3 = $expectedAssemblyVersion.ToString(3)
$verificationRoot = Join-Path $projectRoot `
    "work\real-drawing-verification-v$releaseVersion3"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
$runDirectory = Join-Path (Join-Path $verificationRoot "runs") $timestamp
$utf8NoBom = New-Object Text.UTF8Encoding($false)
$invariant = [Globalization.CultureInfo]::InvariantCulture
$verifierBuilder = Join-Path $projectRoot `
    "scripts\build-real-drawing-verifier.ps1"
$expectedExecutionAssemblyName = "FourJTools.RealDrawingVerify"
$expectedExecutionMarker = "FOURJTOOLS_INTERNAL_REAL_DRAWING_VERIFY_V1"
$profilesRegistrySubKey =
    "Software\Autodesk\AutoCAD\R23.1\ACAD-3001:804\Profiles"
$profilesRegistryPath = "Registry::HKEY_CURRENT_USER\$profilesRegistrySubKey"
$verificationProfile = "<<未命名配置>>"
$qMappedByThisRun = $false
$activeProcess = $null
$originalAutoCADProfile = $null
$autoCADProfileChanged = $false
$autoCADProfileRestored = $false
$failureMessage = $null
$originalHashBefore = $null
$originalHashAfter = $null
$inputPath = $null
$candidatePath = $null
$candidateBuildResultPath = $null
$candidateHashBefore = $null
$candidateHashAfter = $null
$candidateBuildResultHashBefore = $null
$candidateBuildResultHashAfter = $null
$formalCandidateMvid = $null
$executionAssemblyPath = $null
$executionAssemblyHash = $null
$executionAssemblyHashAfter = $null
$executionAssemblyMvid = $null
$executionBuildRecordPath = $null
$executionBuildRecordHash = $null
$executionBuildRecordHashAfter = $null
$candidateBuildResultEvidencePath = $null
$coreConsole = $null
$commandRun = $null
$scanRun = $null
$environment = $null
$settingsPath = Join-Path $runDirectory `
    "isolate\Roaming\StructuralToolbox\rail-settings.xml"
$userSettingsPath = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) `
    "StructuralToolbox\rail-settings.xml"
$userSettingsBefore = $null
$userSettingsAfter = $null
$isolatedSettingsHashBefore = $null
$isolatedSettingsHashAfter = $null

New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$result = [ordered]@{
    schemaVersion = 1
    status = "FAIL"
    mode = $Mode
    generatedLocal = (Get-Date).ToString("o", $invariant)
    generatedUtc = [DateTime]::UtcNow.ToString("o", $invariant)
    runDirectory = $runDirectory
    error = $null
    sourceDrawing = $null
    formalCandidate = $null
    executionAssembly = $null
    selection = $null
    command = $null
    assertions = @()
    artifacts = [ordered]@{}
}

function Write-Utf8Text([string]$Path, [string]$Text) {
    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Text, $script:utf8NoBom)
}

function Write-Json([string]$Path, $Value, [int]$Depth = 20) {
    Write-Utf8Text $Path ($Value | ConvertTo-Json -Depth $Depth)
}

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

function Get-AssemblyMvid([string]$Path) {
    $assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($Path))
    return $assembly.ManifestModule.ModuleVersionId.ToString("D")
}

function Assert-Verification([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "Verification assertion failed: $Message"
    }
    $script:result.assertions += $Message
}

function Get-FileState([string]$Path) {
    $exists = Test-Path -LiteralPath $Path -PathType Leaf
    return [pscustomobject][ordered]@{
        path = [IO.Path]::GetFullPath($Path)
        exists = $exists
        bytes = if ($exists) { (Get-Item -LiteralPath $Path).Length } else { $null }
        sha256 = if ($exists) {
            (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        } else {
            $null
        }
    }
}

function Test-FileStatesEqual($Before, $After) {
    if ($null -eq $Before -or $null -eq $After) {
        return $false
    }
    return [bool]$Before.exists -eq [bool]$After.exists -and
        [string]$Before.sha256 -eq [string]$After.sha256 -and
        [string]$Before.bytes -eq [string]$After.bytes
}

function Assert-Near([double]$Actual, [double]$Expected,
    [double]$Tolerance, [string]$Message) {
    Assert-Verification ([Math]::Abs($Actual - $Expected) -le $Tolerance) `
        ($Message + " (actual=$Actual expected=$Expected)")
}

function Get-ObjectPropertyValue($Object, [string]$Name, $DefaultValue = 0) {
    if ($null -eq $Object) {
        return $DefaultValue
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $DefaultValue
    }
    return $property.Value
}

function Get-ObjectValueSum($Object) {
    if ($null -eq $Object) {
        return 0
    }
    $sum = 0
    foreach ($property in $Object.PSObject.Properties) {
        $sum += [int]$property.Value
    }
    return $sum
}

function Assert-AllNumbersNear($Values, [double]$Expected,
    [double]$Tolerance, [string]$Message) {
    $items = @($Values)
    Assert-Verification ($items.Count -gt 0) ($Message + " has values")
    foreach ($value in $items) {
        Assert-Near ([double]$value) $Expected $Tolerance $Message
    }
}

function Convert-ToQPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $script:projectRoot.TrimEnd("\") + "\"
    if (-not $fullPath.StartsWith($prefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the Q: project mapping: $fullPath"
    }
    return "Q:/" + $fullPath.Substring($prefix.Length).Replace("\", "/")
}

function Format-Coordinate([double]$Value) {
    return $Value.ToString("0.###############", $script:invariant)
}

function New-SettingsFile([string]$Path) {
    $document = New-Object Xml.XmlDocument
    $declaration = $document.CreateXmlDeclaration("1.0", "utf-8", $null)
    $null = $document.AppendChild($declaration)
    $root = $document.CreateElement("RailSettings")
    $null = $document.AppendChild($root)
    $values = [ordered]@{
        RailCount = "2"
        TotalSpacing = "1400"
        MaximumContinuityGap = "20"
        EndOverhang = "100"
        RailLayerName = "导轨"
        RailGlobalWidth = "0"
        RailColorApplication = "ByLayer"
        RailColorKind = "Aci"
        RailColorIndex = "6"
        RailColorRed = "255"
        RailColorGreen = "0"
        RailColorBlue = "255"
    }
    foreach ($entry in $values.GetEnumerator()) {
        $element = $document.CreateElement([string]$entry.Key)
        $element.InnerText = [string]$entry.Value
        $null = $root.AppendChild($element)
    }

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $settings = New-Object Xml.XmlWriterSettings
    $settings.Encoding = $script:utf8NoBom
    $settings.Indent = $true
    $writer = [Xml.XmlWriter]::Create($Path, $settings)
    try {
        $document.Save($writer)
    } finally {
        $writer.Dispose()
    }
}

function Test-QDriveOccupied {
    if ($null -ne (Get-PSDrive -Name Q -ErrorAction SilentlyContinue)) {
        return $true
    }
    foreach ($drive in [IO.DriveInfo]::GetDrives()) {
        if ([string]::Equals($drive.Name, "Q:\",
                [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return Test-Path -LiteralPath "Q:\"
}

function Start-QMapping {
    if (Test-QDriveOccupied) {
        throw "Drive Q: is already occupied; verification refuses to reuse it."
    }
    & subst.exe Q: $script:projectRoot | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath "Q:\")) {
        throw "Could not map project root to Q:."
    }
    $script:qMappedByThisRun = $true
}

function Stop-QMapping {
    if (-not $script:qMappedByThisRun) {
        return
    }
    & subst.exe Q: /D | Out-Null
    $script:qMappedByThisRun = $false
}

function Stop-ActiveProcess {
    if ($null -eq $script:activeProcess) {
        return $false
    }
    try {
        if (-not $script:activeProcess.HasExited) {
            $script:activeProcess.Kill()
            $script:activeProcess.WaitForExit()
            return $true
        }
    } catch {
    } finally {
        $script:activeProcess = $null
    }
    return $false
}

function Start-AutoCADVerificationProfile {
    if (-not (Test-Path -LiteralPath $script:profilesRegistryPath `
            -PathType Container)) {
        throw "AutoCAD profile registry key was not found: $script:profilesRegistryPath"
    }
    $verificationGeneralPath = Join-Path (Join-Path `
        $script:profilesRegistryPath $script:verificationProfile) `
        "General Configuration"
    $verificationGeneral = Get-ItemProperty `
        -LiteralPath $verificationGeneralPath -ErrorAction SilentlyContinue
    if ($null -eq $verificationGeneral -or
        [string]::IsNullOrWhiteSpace(
            [string]$verificationGeneral.TempDirectory)) {
        throw "Verification profile lacks TempDirectory: $script:verificationProfile"
    }

    $script:originalAutoCADProfile = [string](Get-Item `
        -LiteralPath $script:profilesRegistryPath).GetValue("")
    if (-not [string]::Equals($script:originalAutoCADProfile,
            $script:verificationProfile, [StringComparison]::Ordinal)) {
        $profilesKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(
            $script:profilesRegistrySubKey, $true)
        if ($null -eq $profilesKey) {
            throw "AutoCAD profile registry key could not be opened for verification."
        }
        try {
            $profilesKey.SetValue("", $script:verificationProfile,
                [Microsoft.Win32.RegistryValueKind]::String)
            $script:autoCADProfileChanged = $true
        } finally {
            $profilesKey.Dispose()
        }
    }
    $activeProfile = [string](Get-Item `
        -LiteralPath $script:profilesRegistryPath).GetValue("")
    if (-not [string]::Equals($activeProfile, $script:verificationProfile,
            [StringComparison]::Ordinal)) {
        throw "AutoCAD verification profile switch did not take effect."
    }
}

function Restore-AutoCADVerificationProfile {
    if ($null -eq $script:originalAutoCADProfile) {
        return
    }
    if ($script:autoCADProfileChanged) {
        $profilesKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(
            $script:profilesRegistrySubKey, $true)
        if ($null -eq $profilesKey) {
            throw "AutoCAD profile registry key could not be opened for restoration."
        }
        try {
            $profilesKey.SetValue("", $script:originalAutoCADProfile,
                [Microsoft.Win32.RegistryValueKind]::String)
        } finally {
            $profilesKey.Dispose()
        }
    }
    $profileAfter = [string](Get-Item `
        -LiteralPath $script:profilesRegistryPath).GetValue("")
    $script:autoCADProfileRestored = [string]::Equals($profileAfter,
        $script:originalAutoCADProfile, [StringComparison]::Ordinal)
    if (-not $script:autoCADProfileRestored) {
        throw "AutoCAD profile was not restored after real-drawing verification."
    }
}

function Invoke-CoreConsole([string]$ScriptPath, [string]$LogPath,
    [string]$IsolationKey, [string]$IsolationDirectory,
    [string]$IndependentAppData, [string]$SettingsPath) {
    $qInput = Convert-ToQPath $script:workingCopy
    $qScript = Convert-ToQPath $ScriptPath
    $qIsolation = Convert-ToQPath $IsolationDirectory
    $qAppData = Convert-ToQPath $IndependentAppData
    $localAppData = Join-Path (Split-Path -Parent $IndependentAppData) "Local"
    $tempDirectory = Join-Path $localAppData "Temp"
    New-Item -ItemType Directory -Path $IsolationDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $IndependentAppData -Force | Out-Null
    New-Item -ItemType Directory -Path $localAppData -Force | Out-Null
    New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null

    $arguments = "/i `"$qInput`" /s `"$qScript`" /l chs " +
        "/isolate $IsolationKey `"$qIsolation`""
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = $script:coreConsole
    $startInfo.Arguments = $arguments
    $startInfo.WorkingDirectory = $script:autoCADRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $unicode = New-Object Text.UnicodeEncoding($false, $false)
    $startInfo.StandardOutputEncoding = $unicode
    $startInfo.StandardErrorEncoding = $unicode
    $startInfo.EnvironmentVariables["APPDATA"] = $qAppData.Replace("/", "\")
    $startInfo.EnvironmentVariables["LOCALAPPDATA"] =
        (Convert-ToQPath $localAppData).Replace("/", "\")
    $startInfo.EnvironmentVariables["TEMP"] =
        (Convert-ToQPath $tempDirectory).Replace("/", "\")
    $startInfo.EnvironmentVariables["TMP"] =
        (Convert-ToQPath $tempDirectory).Replace("/", "\")
    $startInfo.EnvironmentVariables["FOURJTOOLS_SETTINGS_PATH"] =
        (Convert-ToQPath $SettingsPath).Replace("/", "\")

    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "CoreConsole process did not start."
    }
    $script:activeProcess = $process
    $processId = $process.Id
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $completed = $process.WaitForExit($script:TimeoutSeconds * 1000)
    $killed = $false
    if (-not $completed) {
        $killed = Stop-ActiveProcess
    } else {
        $process.WaitForExit()
        $script:activeProcess = $null
    }

    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result
    $logText = ($stdout + [Environment]::NewLine + $stderr).Replace(
        [char]0, [char]32)
    Write-Utf8Text $LogPath $logText
    $exitCode = if ($completed) { $process.ExitCode } else { $null }
    $process.Dispose()
    return [pscustomobject][ordered]@{
        processId = $processId
        exitCode = $exitCode
        timedOut = -not $completed
        killedByVerifier = $killed
        logPath = $LogPath
        logContainsNull = ([IO.File]::ReadAllText($LogPath).IndexOf([char]0) -ge 0)
    }
}

function Write-HashManifest([string]$Path, [string[]]$Files) {
    $lines = New-Object Collections.Generic.List[string]
    foreach ($file in $Files | Sort-Object -Unique) {
        if ([string]::IsNullOrWhiteSpace($file) -or
            -not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }
        $fullPath = [IO.Path]::GetFullPath($file)
        $relativePath = $fullPath.Substring(
            $script:runDirectory.TrimEnd("\").Length + 1).Replace("\", "/")
        $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        $lines.Add($hash + " *" + $relativePath)
    }
    Write-Utf8Text $Path (($lines -join [Environment]::NewLine) +
        [Environment]::NewLine)
}

try {
    $inputPath = Resolve-File $InputDwg "Input DWG"
    if ([IO.Path]::GetExtension($inputPath) -ne ".dwg") {
        throw "InputDwg must be a .dwg file: $inputPath"
    }
    $candidatePath = Resolve-File $CandidateDll "Candidate DLL"
    $candidateBuildResultPath = Resolve-File $CandidateBuildResult `
        "Candidate build result"
    $userSettingsBefore = Get-FileState $userSettingsPath
    $autoCADRoot = [IO.Path]::GetFullPath($AutoCADDir)
    $coreConsole = Resolve-File (Join-Path $autoCADRoot "accoreconsole.exe") `
        "AutoCAD Core Console"
    $null = Resolve-File (Join-Path $autoCADRoot "acmgd.dll") "acmgd.dll"
    $null = Resolve-File (Join-Path $autoCADRoot "acdbmgd.dll") "acdbmgd.dll"
    $null = Resolve-File (Join-Path $autoCADRoot "accoremgd.dll") "accoremgd.dll"
    $null = Resolve-File $verifierBuilder "Real drawing verifier builder"

    $candidateAssembly = [Reflection.AssemblyName]::GetAssemblyName($candidatePath)
    Assert-Verification ($candidateAssembly.Name -eq "StructuralToolbox") `
        "candidate assembly name is StructuralToolbox"
    Assert-Verification ($candidateAssembly.Version -eq $expectedAssemblyVersion) `
        "candidate assembly version is exactly $expectedAssemblyVersion"
    $candidateHashBefore = (Get-FileHash -LiteralPath $candidatePath `
        -Algorithm SHA256).Hash
    $candidateBuildResultHashBefore = (Get-FileHash `
        -LiteralPath $candidateBuildResultPath -Algorithm SHA256).Hash
    $formalCandidateMvid = Get-AssemblyMvid $candidatePath

    $originalHashBefore = (Get-FileHash -LiteralPath $inputPath `
        -Algorithm SHA256).Hash
    $workingCopy = Join-Path $runDirectory "input-copy.dwg"
    $script:workingCopy = $workingCopy
    Copy-Item -LiteralPath $inputPath -Destination $workingCopy
    Assert-Verification ((Get-FileHash -LiteralPath $workingCopy `
        -Algorithm SHA256).Hash -eq $originalHashBefore) `
        "working DWG copy initially matches the source SHA-256"

    $candidateBuildResultEvidencePath = Join-Path $runDirectory `
        "formal-candidate-build-result.json"
    Copy-Item -LiteralPath $candidateBuildResultPath `
        -Destination $candidateBuildResultEvidencePath
    Assert-Verification ((Get-FileHash `
        -LiteralPath $candidateBuildResultEvidencePath -Algorithm SHA256).Hash -eq
        $candidateBuildResultHashBefore) `
        "evidence copy matches the supplied CandidateBuildResult SHA-256"

    $executionDirectory = Join-Path $runDirectory "execution"
    New-Item -ItemType Directory -Path $executionDirectory -Force | Out-Null
    $builderOutput = @(& $verifierBuilder -CandidateDll $candidatePath `
        -CandidateBuildResult $candidateBuildResultPath `
        -AutoCADDir $autoCADRoot -OutputDirectory $executionDirectory -PassThru)
    $builderRecords = @($builderOutput | Where-Object {
        $null -ne $_ -and $_.PSObject.Properties.Name -contains "validation"
    })
    if ($builderRecords.Count -ne 1 -or
        [string]$builderRecords[0].validation -ne "passed") {
        throw "Internal verifier build did not return exactly one passed record."
    }
    $builderRecord = $builderRecords[0]
    $executionAssemblyPath = Resolve-File `
        ([string]$builderRecord.executionAssemblyPath) `
        "Internal real drawing verification assembly"
    $executionBuildRecordPath = Resolve-File `
        ([string]$builderRecord.buildRecordPath) `
        "Internal real drawing verification build record"
    $executionAssemblyHash = (Get-FileHash `
        -LiteralPath $executionAssemblyPath -Algorithm SHA256).Hash
    $executionBuildRecordHash = (Get-FileHash `
        -LiteralPath $executionBuildRecordPath -Algorithm SHA256).Hash
    $executionAssemblyIdentity =
        [Reflection.AssemblyName]::GetAssemblyName($executionAssemblyPath)
    $executionAssemblyMvid = Get-AssemblyMvid $executionAssemblyPath
    Assert-Verification ($executionAssemblyIdentity.Name -eq
        $expectedExecutionAssemblyName) `
        "execution assembly name is FourJTools.RealDrawingVerify"
    Assert-Verification ($executionAssemblyIdentity.Version -eq
        $expectedAssemblyVersion) `
        "execution assembly version is exactly $expectedAssemblyVersion"
    Assert-Verification ([string]$builderRecord.verificationKind -eq
        "same-source-internal-instrumentation") `
        "execution assembly is explicitly classified as same-source internal instrumentation"
    Assert-Verification ([string]$builderRecord.formalCandidateSha256 -eq
        $candidateHashBefore) `
        "internal build record is bound to the formal candidate SHA-256"
    Assert-Verification ([string]$builderRecord.candidateBuildResultSha256 -eq
        $candidateBuildResultHashBefore) `
        "internal build record is bound to CandidateBuildResult SHA-256"
    Assert-Verification ([string]$builderRecord.executionAssemblySha256 -eq
        $executionAssemblyHash) `
        "internal build record matches the execution assembly SHA-256"
    Assert-Verification ([string]$builderRecord.executionAssemblyMvid -eq
        $executionAssemblyMvid) `
        "internal build record matches the execution assembly MVID"
    Assert-Verification ([string]$builderRecord.executionMarker -eq
        $expectedExecutionMarker) `
        "internal build record contains the required verification marker"

    Start-AutoCADVerificationProfile
    Start-QMapping
    $isolationKey = "FOURJ_REAL_V" +
        ($releaseVersion3 -replace "\.", "") + "_" +
        $timestamp.Replace("-", "")
    $isolationDirectory = Join-Path $runDirectory "isolate"
    $independentAppData = Join-Path $isolationDirectory "Roaming"
    New-SettingsFile $settingsPath
    $isolatedSettingsHashBefore = (Get-FileHash -LiteralPath $settingsPath `
        -Algorithm SHA256).Hash

    $qExecutionAssembly = Convert-ToQPath $executionAssemblyPath
    $preScanPath = Join-Path $runDirectory "pre-scan.json"
    $postScanPath = Join-Path $runDirectory "post-scan.json"
    $commandOutcomePath = Join-Path $runDirectory "command-outcome.json"
    $scanScriptPath = Join-Path $runDirectory "scan-commands.scr"
    $scanLogPath = Join-Path $runDirectory "scan.log"
    $commandScriptPath = Join-Path $runDirectory "commands.scr"
    $commandLogPath = Join-Path $runDirectory "coreconsole.log"

    $scanCommands = @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '_.NETLOAD',
        $qExecutionAssembly,
        'ST_REAL_SCAN',
        (Convert-ToQPath $preScanPath),
        '_.QUIT'
    )
    Write-Utf8Text $scanScriptPath (($scanCommands -join "`r`n") + "`r`n")
    $scanRun = Invoke-CoreConsole $scanScriptPath $scanLogPath `
        ($isolationKey + "_SCAN") $isolationDirectory $independentAppData `
        $settingsPath
    Assert-Verification (-not $scanRun.timedOut) `
        "pre-scan CoreConsole completed before timeout"
    Assert-Verification ($scanRun.exitCode -eq 0) `
        "pre-scan CoreConsole exit code is zero"
    Assert-Verification (-not $scanRun.logContainsNull) `
        "pre-scan log is UTF-8 text without NUL characters"
    Assert-Verification (Test-Path -LiteralPath $preScanPath -PathType Leaf) `
        "pre-scan JSON exists"
    $preScan = Get-Content -Raw -Encoding UTF8 $preScanPath | ConvertFrom-Json

    Assert-Verification ($preScan.executionAssembly.name -eq
        $expectedExecutionAssemblyName) `
        "pre-scan ran from FourJTools.RealDrawingVerify"
    Assert-Verification ([version]$preScan.executionAssembly.assemblyVersion -eq
        $expectedAssemblyVersion) `
        "pre-scan execution assembly version is exactly $expectedAssemblyVersion"
    Assert-Verification ([string]$preScan.executionAssembly.mvid -eq
        $executionAssemblyMvid) `
        "pre-scan execution assembly MVID matches the internal build record"
    Assert-Verification ([string]$preScan.executionAssembly.sha256 -eq
        $executionAssemblyHash) `
        "pre-scan execution assembly SHA-256 matches the internal build record"
    Assert-Verification ([string]$preScan.executionAssembly.verificationMarker -eq
        $expectedExecutionMarker) `
        "pre-scan execution assembly contains the required internal marker"
    $expectedExecutionQPath = $qExecutionAssembly.Replace("/", "\")
    $actualExecutionPath =
        ([string]$preScan.executionAssembly.location).Replace("/", "\")
    Assert-Verification ($actualExecutionPath -eq $expectedExecutionQPath) `
        "pre-scan reports the exact Q:-mapped internal execution assembly path"

    Assert-Verification ($null -ne $preScan.largestPanelLayer) `
        "a rectangular panel layer was detected"
    Assert-Verification ([int]$preScan.largestPanelLayer.count -gt 0) `
        "recognized input panel count is greater than zero"
    Assert-Verification ([int]$preScan.plan.recognizedPanelCount -eq
        [int]$preScan.largestPanelLayer.count) `
        "plan input count matches the selected largest rectangle layer"
    Assert-Verification ([int]$preScan.plan.railSegmentCount -gt 0) `
        "planned rail count is greater than zero"
    Assert-Verification ($preScan.database.insunits -eq "Millimeters") `
        "input DWG units are millimeters"
    Assert-Verification ([int]$preScan.toolEntities.count -eq 0) `
        "working copy has no pre-existing FOURJTOOLS entities"
    Assert-Verification ((Get-ObjectValueSum `
        $preScan.toolEntities.outputLayerEntities) -eq 0) `
        "working copy has no entities on configured output layers"

    $expectedSettingsQPath = Convert-ToQPath $settingsPath
    $actualSettingsQPath = ([string]$preScan.settings.path).Replace("\", "/")
    Assert-Verification ($actualSettingsQPath -eq $expectedSettingsQPath) `
        "internal execution assembly reads only the isolated rail-settings.xml"
    Assert-Verification ([int]$preScan.settings.railCount -eq 2) `
        "isolated rail count is 2"
    Assert-Near ([double]$preScan.settings.totalSpacing) 1400.0 0.000001 `
        "isolated total rail spacing is 1400"
    Assert-Near ([double]$preScan.settings.maximumContinuityGap) 20.0 0.000001 `
        "isolated continuity gap is 20"
    Assert-Near ([double]$preScan.settings.endOverhang) 100.0 0.000001 `
        "isolated end overhang is 100"
    Assert-Verification ($preScan.settings.railLayerName -eq "导轨") `
        "isolated rail layer is 导轨"
    Assert-Near ([double]$preScan.settings.railGlobalWidth) 0.0 0.000001 `
        "isolated rail global width is zero"
    Assert-Verification ($preScan.settings.railColorKind -eq "Aci" -and
        [int]$preScan.settings.railColorIndex -eq 6) `
        "isolated rail color is ACI 6"

    if ([bool]$preScan.configuredLayers.rail.exists) {
        Assert-Verification ([int]$preScan.configuredLayers.rail.colorIndex -eq 6) `
            "existing empty rail layer already uses ACI 6"
    }
    $minimum = $preScan.largestPanelLayer.minimum
    $maximum = $preScan.largestPanelLayer.maximum
    $width = [Math]::Abs([double]$maximum.x - [double]$minimum.x)
    $height = [Math]::Abs([double]$maximum.y - [double]$minimum.y)
    $margin = [Math]::Max(100.0, [Math]::Max($width, $height) * 0.001)
    $firstCorner = (Format-Coordinate ([double]$minimum.x - $margin)) + "," +
        (Format-Coordinate ([double]$minimum.y - $margin))
    $secondCorner = (Format-Coordinate ([double]$maximum.x + $margin)) + "," +
        (Format-Coordinate ([double]$maximum.y + $margin))
    $templateX = [double]$maximum.x + [Math]::Max(10000.0, $margin * 10.0)
    $templateY = [double]$maximum.y + [Math]::Max(10000.0, $margin * 10.0)
    $templateFirst = (Format-Coordinate $templateX) + "," +
        (Format-Coordinate $templateY)
    $templateSecond = (Format-Coordinate ($templateX + 1000.0)) + "," +
        (Format-Coordinate $templateY)
    $templateLine = (Format-Coordinate ($templateX + 500.0)) + "," +
        (Format-Coordinate ($templateY + 500.0))

    $commandLines = New-Object Collections.Generic.List[string]
    foreach ($line in @(
        '(setvar "REPORTERROR" 0)',
        '(setvar "SECURELOAD" 0)',
        '(setvar "CMDECHO" 1)',
        '_.NETLOAD',
        $qExecutionAssembly,
        '_.DIMLINEAR',
        $templateFirst,
        $templateSecond,
        $templateLine,
        '(setq fjRealTemplate (entlast))',
        'ST_REAL_ARM',
        (Convert-ToQPath $commandOutcomePath),
        'STPVRAIL',
        ('(handent "' + [string]$preScan.largestPanelLayer.sampleHandle + '")'),
        $firstCorner,
        $secondCorner,
        '(entlast)')) {
        $commandLines.Add($line)
    }
    $commandLines.Add("C")
    foreach ($line in @(
        '(entdel fjRealTemplate)',
        '_.QSAVE',
        'ST_REAL_SCAN',
        (Convert-ToQPath $postScanPath),
        '_.QSAVE',
        '_.QUIT')) {
        $commandLines.Add($line)
    }
    Write-Utf8Text $commandScriptPath (($commandLines -join "`r`n") + "`r`n")

    $environmentPath = Join-Path $runDirectory "environment.json"
    $environment = [ordered]@{
        schemaVersion = 1
        generatedLocal = (Get-Date).ToString("o", $invariant)
        projectRoot = $projectRoot
        runDirectory = $runDirectory
        mode = $Mode
        timeoutSeconds = $TimeoutSeconds
        sourceDrawing = [ordered]@{
            path = $inputPath
            bytes = (Get-Item -LiteralPath $inputPath).Length
            sha256 = $originalHashBefore
        }
        workingCopy = [ordered]@{
            path = $workingCopy
            initialSha256 = (Get-FileHash -LiteralPath $workingCopy `
                -Algorithm SHA256).Hash
        }
        verificationBasis = "same-source-internal-instrumentation"
        formalCandidateExecuted = $false
        claimBoundary = "The formal StructuralToolbox.dll is metadata-inspected and hash-bound, but is not NETLOADed into CoreConsole and no CAD command from it is executed."
        formalCandidate = [ordered]@{
            path = $candidatePath
            assemblyName = $candidateAssembly.Name
            assemblyVersion = $candidateAssembly.Version.ToString()
            mvid = $formalCandidateMvid
            bytes = (Get-Item -LiteralPath $candidatePath).Length
            sha256 = $candidateHashBefore
            buildResultPath = $candidateBuildResultPath
            buildResultEvidenceCopy = $candidateBuildResultEvidencePath
            buildResultSha256 = $candidateBuildResultHashBefore
        }
        executionAssembly = [ordered]@{
            path = $executionAssemblyPath
            assemblyName = $executionAssemblyIdentity.Name
            assemblyVersion = $executionAssemblyIdentity.Version.ToString()
            mvid = $executionAssemblyMvid
            sha256 = $executionAssemblyHash
            marker = $expectedExecutionMarker
            buildRecordPath = $executionBuildRecordPath
            buildRecordSha256 = $executionBuildRecordHash
            builderPath = $verifierBuilder
            builderSha256 = (Get-FileHash -LiteralPath $verifierBuilder `
                -Algorithm SHA256).Hash
        }
        autoCAD = [ordered]@{
            directory = $autoCADRoot
            coreConsolePath = $coreConsole
            coreConsoleFileVersion = (Get-Item -LiteralPath $coreConsole).VersionInfo.FileVersion
            coreConsoleProductVersion = (Get-Item -LiteralPath $coreConsole).VersionInfo.ProductVersion
            coreConsoleSha256 = (Get-FileHash -LiteralPath $coreConsole `
                -Algorithm SHA256).Hash
        }
        isolation = [ordered]@{
            qDrive = "Q:"
            isolationKey = $isolationKey
            userDataDirectory = $isolationDirectory
            independentAppData = $independentAppData
            settingsPath = $settingsPath
            settingsSha256Before = $isolatedSettingsHashBefore
            overrideVariable = "FOURJTOOLS_SETTINGS_PATH"
            userSettingsBefore = $userSettingsBefore
        }
        selection = [ordered]@{
            layer = [string]$preScan.largestPanelLayer.name
            recognizedRectangles = [int]$preScan.largestPanelLayer.count
            sampleHandle = [string]$preScan.largestPanelLayer.sampleHandle
            firstCorner = $firstCorner
            secondCorner = $secondCorner
        }
    }
    Write-Json $environmentPath $environment

    $commandRun = Invoke-CoreConsole $commandScriptPath $commandLogPath `
        ($isolationKey + "_COMMAND") $isolationDirectory $independentAppData `
        $settingsPath
    Assert-Verification (-not $commandRun.logContainsNull) `
        "command log is UTF-8 text without NUL characters"
    $commandLog = Get-Content -Raw -Encoding UTF8 $commandLogPath
    Assert-Verification (-not $commandRun.timedOut) `
        "STPVRAIL CoreConsole completed before timeout"
    Assert-Verification ($commandRun.exitCode -eq 0) `
        "STPVRAIL CoreConsole exit code is zero"
    Assert-Verification (Test-Path -LiteralPath $commandOutcomePath -PathType Leaf) `
        "STPVRAIL command outcome JSON exists"
    Assert-Verification (Test-Path -LiteralPath $postScanPath -PathType Leaf) `
        "post-command scan JSON exists"

    $commandOutcome = Get-Content -Raw -Encoding UTF8 $commandOutcomePath |
        ConvertFrom-Json
    $postScan = Get-Content -Raw -Encoding UTF8 $postScanPath | ConvertFrom-Json
    Assert-Verification ($commandOutcome.status -eq "ENDED") `
        "actual STPVRAIL command ended normally"
    Assert-Verification ($commandOutcome.globalCommandName -eq "STPVRAIL") `
        "observed command is STPVRAIL"
    $runtimeSnapshots = @(
        [pscustomobject]@{ Name = "command-ended"; Value = $commandOutcome.snapshot },
        [pscustomobject]@{ Name = "post-scan"; Value = $postScan }
    )
    foreach ($runtimeSnapshot in $runtimeSnapshots) {
        $identity = $runtimeSnapshot.Value.executionAssembly
        Assert-Verification ([string]$identity.name -eq
            $expectedExecutionAssemblyName) `
            "$($runtimeSnapshot.Name) used the internal execution assembly"
        Assert-Verification ([version]$identity.assemblyVersion -eq
            $expectedAssemblyVersion) `
            "$($runtimeSnapshot.Name) execution assembly version is $expectedAssemblyVersion"
        Assert-Verification ([string]$identity.mvid -eq
            $executionAssemblyMvid) `
            "$($runtimeSnapshot.Name) execution assembly MVID is unchanged"
        Assert-Verification ([string]$identity.sha256 -eq
            $executionAssemblyHash) `
            "$($runtimeSnapshot.Name) execution assembly SHA-256 is unchanged"
        Assert-Verification ([string]$identity.verificationMarker -eq
            $expectedExecutionMarker) `
            "$($runtimeSnapshot.Name) contains the internal verification marker"
    }

    $plannedRails = [int]$preScan.plan.railSegmentCount
    $completedRails = [int]$commandOutcome.snapshot.rails.count
    $postRails = [int]$postScan.rails.count
    Assert-Verification ($completedRails -eq $plannedRails) `
        "command-ended rail count equals the planned rail count"
    Assert-Verification ($postRails -eq $plannedRails) `
        "saved DWG rail count equals the planned rail count"
    Assert-AllNumbersNear $postScan.rails.globalWidths 0.0 0.000001 `
        "all rail global widths are zero"
    Assert-Verification ([bool]$postScan.configuredLayers.rail.exists -and
        [int]$postScan.configuredLayers.rail.colorIndex -eq 6) `
        "saved rail layer exists and uses ACI 6"

    $toolEntityCount = [int]$postScan.toolEntities.count
    Assert-Verification ([int]$postScan.toolEntities.invalidXDataCount -eq 0) `
        "saved tool entities have no invalid FOURJTOOLS XData"
    Assert-Verification ([int]$postScan.toolEntities.metadataV3Count -eq
        $toolEntityCount -and [bool]$postScan.toolEntities.allMetadataV3) `
        "every saved tool entity has complete META_V3 metadata"

    $preNativeDimensions = [int]$preScan.nativeDimensions.count
    $commandNativeDimensions = [int]$commandOutcome.snapshot.nativeDimensions.count
    $postNativeDimensions = [int]$postScan.nativeDimensions.count
    Assert-Verification ($commandNativeDimensions -eq $preNativeDimensions + 1) `
        "command-ended snapshot contains only the temporary native dimension"
    Assert-Verification ($postNativeDimensions -eq $preNativeDimensions) `
        "saved DWG native dimension count is unchanged"

    $isolatedSettingsHashAfter = (Get-FileHash -LiteralPath $settingsPath `
        -Algorithm SHA256).Hash
    Assert-Verification ($isolatedSettingsHashAfter -eq
        $isolatedSettingsHashBefore) `
        "isolated verification settings SHA-256 is unchanged"
    $userSettingsAfter = Get-FileState $userSettingsPath
    Assert-Verification (Test-FileStatesEqual $userSettingsBefore `
        $userSettingsAfter) `
        "real user rail-settings.xml existence, size, and SHA-256 are unchanged"

    $result.status = "PASS"
    $result.selection = [ordered]@{
        layer = [string]$preScan.largestPanelLayer.name
        recognizedRectangles = [int]$preScan.largestPanelLayer.count
        sampleHandle = [string]$preScan.largestPanelLayer.sampleHandle
        plannedRails = $plannedRails
        nativeDimensionsBefore = $preNativeDimensions
        nativeDimensionsAfter = $postNativeDimensions
    }
    $result.command = [ordered]@{
        name = "STPVRAIL"
        requestedMode = $Mode
        observerStatus = [string]$commandOutcome.status
        completedRails = $completedRails
        savedRails = $postRails
        nativeDimensionsBefore = $preNativeDimensions
        nativeDimensionsDuringCommand = $commandNativeDimensions
        nativeDimensionsAfter = $postNativeDimensions
        process = $commandRun
    }
} catch {
    $failureMessage = $_.Exception.ToString()
    $result.error = $failureMessage
} finally {
    $killedInFinally = Stop-ActiveProcess
    try {
        Restore-AutoCADVerificationProfile
    } catch {
        if ($null -eq $failureMessage) {
            $failureMessage = $_.Exception.ToString()
            $result.error = $failureMessage
            $result.status = "FAIL"
        }
    }
    try {
        Stop-QMapping
    } catch {
        if ($null -eq $failureMessage) {
            $failureMessage = $_.Exception.ToString()
            $result.error = $failureMessage
            $result.status = "FAIL"
        }
    }

    if ($null -ne $inputPath -and
        (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
        $originalHashAfter = (Get-FileHash -LiteralPath $inputPath `
            -Algorithm SHA256).Hash
    }

    try {
        if ($null -ne $userSettingsBefore) {
            $userSettingsAfter = Get-FileState $userSettingsPath
        }
        if (Test-Path -LiteralPath $settingsPath -PathType Leaf) {
            $isolatedSettingsHashAfter = (Get-FileHash -LiteralPath $settingsPath `
                -Algorithm SHA256).Hash
        }
    } catch {
        if ($null -eq $failureMessage) {
            $failureMessage = $_.Exception.ToString()
            $result.error = $failureMessage
            $result.status = "FAIL"
        }
    }
    $userSettingsUnchanged = Test-FileStatesEqual $userSettingsBefore `
        $userSettingsAfter
    if ($null -ne $userSettingsBefore -and -not $userSettingsUnchanged -and
        $null -eq $failureMessage) {
        $failureMessage = "Real user rail-settings.xml changed during verification."
        $result.error = $failureMessage
        $result.status = "FAIL"
    }
    $result.settingsIsolation = [ordered]@{
        overrideVariable = "FOURJTOOLS_SETTINGS_PATH"
        isolatedPath = $settingsPath
        isolatedSha256Before = $isolatedSettingsHashBefore
        isolatedSha256After = $isolatedSettingsHashAfter
        isolatedUnchanged = $null -ne $isolatedSettingsHashBefore -and
            $isolatedSettingsHashBefore -eq $isolatedSettingsHashAfter
        userPath = $userSettingsPath
        userBefore = $userSettingsBefore
        userAfter = $userSettingsAfter
        userUnchanged = $userSettingsUnchanged
    }

    try {
        if ($null -ne $candidatePath -and
            (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
            $candidateHashAfter = (Get-FileHash -LiteralPath $candidatePath `
                -Algorithm SHA256).Hash
        }
        if ($null -ne $candidateBuildResultPath -and
            (Test-Path -LiteralPath $candidateBuildResultPath -PathType Leaf)) {
            $candidateBuildResultHashAfter = (Get-FileHash `
                -LiteralPath $candidateBuildResultPath -Algorithm SHA256).Hash
        }
        if ($null -ne $executionAssemblyPath -and
            (Test-Path -LiteralPath $executionAssemblyPath -PathType Leaf)) {
            $executionAssemblyHashAfter = (Get-FileHash `
                -LiteralPath $executionAssemblyPath -Algorithm SHA256).Hash
        }
        if ($null -ne $executionBuildRecordPath -and
            (Test-Path -LiteralPath $executionBuildRecordPath -PathType Leaf)) {
            $executionBuildRecordHashAfter = (Get-FileHash `
                -LiteralPath $executionBuildRecordPath -Algorithm SHA256).Hash
        }
    } catch {
        if ($null -eq $failureMessage) {
            $failureMessage = $_.Exception.ToString()
            $result.error = $failureMessage
            $result.status = "FAIL"
        }
    }

    $formalCandidateUnchanged = $null -ne $candidateHashBefore -and
        $candidateHashBefore -eq $candidateHashAfter
    $candidateBuildResultUnchanged =
        $null -ne $candidateBuildResultHashBefore -and
        $candidateBuildResultHashBefore -eq $candidateBuildResultHashAfter
    $executionAssemblyUnchanged = $null -ne $executionAssemblyHash -and
        $executionAssemblyHash -eq $executionAssemblyHashAfter
    $executionBuildRecordUnchanged = $null -ne $executionBuildRecordHash -and
        $executionBuildRecordHash -eq $executionBuildRecordHashAfter
    foreach ($immutabilityCheck in @(
        [pscustomobject]@{
            Applicable = $null -ne $candidateHashBefore
            Passed = $formalCandidateUnchanged
            Message = "Formal candidate SHA-256 changed during verification."
        },
        [pscustomobject]@{
            Applicable = $null -ne $candidateBuildResultHashBefore
            Passed = $candidateBuildResultUnchanged
            Message = "CandidateBuildResult SHA-256 changed during verification."
        },
        [pscustomobject]@{
            Applicable = $null -ne $executionAssemblyHash
            Passed = $executionAssemblyUnchanged
            Message = "Internal execution assembly SHA-256 changed during verification."
        },
        [pscustomobject]@{
            Applicable = $null -ne $executionBuildRecordHash
            Passed = $executionBuildRecordUnchanged
            Message = "Internal verifier build record changed during verification."
        })) {
        if ($immutabilityCheck.Applicable -and
            -not $immutabilityCheck.Passed -and $null -eq $failureMessage) {
            $failureMessage = $immutabilityCheck.Message
            $result.error = $failureMessage
            $result.status = "FAIL"
        }
    }

    $sourceUnchanged = $null -ne $originalHashBefore -and
        $originalHashBefore -eq $originalHashAfter
    if (-not $sourceUnchanged -and $null -eq $failureMessage) {
        $failureMessage = "Original DWG SHA-256 changed during verification."
        $result.error = $failureMessage
        $result.status = "FAIL"
    }

    $result.sourceDrawing = [ordered]@{
        path = $inputPath
        sha256Before = $originalHashBefore
        sha256After = $originalHashAfter
        unchanged = $sourceUnchanged
    }
    $result.verificationBasis = "same-source-internal-instrumentation"
    $result.formalCandidateExecuted = $false
    $result.claimBoundary = "The formal StructuralToolbox.dll was metadata-inspected and hash-bound, but was not NETLOADed into CoreConsole and no CAD command from it was executed."
    $result.formalCandidate = [ordered]@{
        path = $candidatePath
        assemblyName = if ($null -eq $candidatePath) { $null } else {
            "StructuralToolbox"
        }
        assemblyVersion = if ($null -eq $candidatePath) { $null } else {
            $expectedAssemblyVersion.ToString()
        }
        mvid = $formalCandidateMvid
        sha256Before = $candidateHashBefore
        sha256After = $candidateHashAfter
        unchanged = $formalCandidateUnchanged
        buildResultPath = $candidateBuildResultPath
        buildResultEvidenceCopy = $candidateBuildResultEvidencePath
        buildResultSha256Before = $candidateBuildResultHashBefore
        buildResultSha256After = $candidateBuildResultHashAfter
        buildResultUnchanged = $candidateBuildResultUnchanged
    }
    $result.executionAssembly = [ordered]@{
        path = $executionAssemblyPath
        assemblyName = $expectedExecutionAssemblyName
        assemblyVersion = $expectedAssemblyVersion.ToString()
        mvid = $executionAssemblyMvid
        marker = $expectedExecutionMarker
        sha256Before = $executionAssemblyHash
        sha256After = $executionAssemblyHashAfter
        unchanged = $executionAssemblyUnchanged
        buildRecordPath = $executionBuildRecordPath
        buildRecordSha256Before = $executionBuildRecordHash
        buildRecordSha256After = $executionBuildRecordHashAfter
        buildRecordUnchanged = $executionBuildRecordUnchanged
    }
    if ($null -ne $environment) {
        $environment.isolation.settingsSha256After = $isolatedSettingsHashAfter
        $environment.isolation.userSettingsAfter = $userSettingsAfter
        $environment.isolation.userSettingsUnchanged = $userSettingsUnchanged
        $environment.formalCandidate.sha256After = $candidateHashAfter
        $environment.formalCandidate.unchanged = $formalCandidateUnchanged
        $environment.formalCandidate.buildResultSha256After =
            $candidateBuildResultHashAfter
        $environment.formalCandidate.buildResultUnchanged =
            $candidateBuildResultUnchanged
        $environment.executionAssembly.sha256After = $executionAssemblyHashAfter
        $environment.executionAssembly.unchanged = $executionAssemblyUnchanged
        $environment.executionAssembly.buildRecordSha256After =
            $executionBuildRecordHashAfter
        $environment.executionAssembly.buildRecordUnchanged =
            $executionBuildRecordUnchanged
        Write-Json (Join-Path $runDirectory "environment.json") $environment
    }
    if ($killedInFinally) {
        $result.command = [ordered]@{
            processKilledByVerifier = $true
            reason = "Verifier cleanup after failure"
        }
    }

    $environmentPath = Join-Path $runDirectory "environment.json"
    $resultPath = Join-Path $runDirectory "result.json"
    $commandScriptPath = Join-Path $runDirectory "commands.scr"
    $commandLogPath = Join-Path $runDirectory "coreconsole.log"
    $scanScriptPath = Join-Path $runDirectory "scan-commands.scr"
    $scanLogPath = Join-Path $runDirectory "scan.log"
    $preScanPath = Join-Path $runDirectory "pre-scan.json"
    $postScanPath = Join-Path $runDirectory "post-scan.json"
    $commandOutcomePath = Join-Path $runDirectory "command-outcome.json"
    $workingCopy = Join-Path $runDirectory "input-copy.dwg"
    $expectedExecutionAssemblyPath = Join-Path $runDirectory `
        "execution\FourJTools.RealDrawingVerify.dll"
    $executionPdbPath = Join-Path $runDirectory `
        "execution\FourJTools.RealDrawingVerify.pdb"
    $expectedExecutionBuildRecordPath = Join-Path $runDirectory `
        "execution\real-drawing-verifier-build.json"
    $executionBuildManifestPath = Join-Path $runDirectory `
        "execution\SHA256SUMS.txt"
    $candidateBuildResultEvidencePath = Join-Path $runDirectory `
        "formal-candidate-build-result.json"
    $settingsPath = Join-Path $runDirectory `
        "isolate\Roaming\StructuralToolbox\rail-settings.xml"
    $result.artifacts = [ordered]@{
        environment = $environmentPath
        result = $resultPath
        commands = $commandScriptPath
        coreConsoleLog = $commandLogPath
        preScan = $preScanPath
        postScan = $postScanPath
        commandOutcome = $commandOutcomePath
        formalCandidateBuildResult = $candidateBuildResultEvidencePath
        executionAssembly = $expectedExecutionAssemblyPath
        executionBuildRecord = $expectedExecutionBuildRecordPath
        executionBuildManifest = $executionBuildManifestPath
        sha256Manifest = (Join-Path $runDirectory "SHA256SUMS.txt")
    }
    Write-Json $resultPath $result
    Write-HashManifest (Join-Path $runDirectory "SHA256SUMS.txt") @(
        $environmentPath, $resultPath, $commandScriptPath, $commandLogPath,
        $scanScriptPath, $scanLogPath, $preScanPath, $postScanPath,
        $commandOutcomePath, $workingCopy, $candidateBuildResultEvidencePath,
        $expectedExecutionAssemblyPath, $executionPdbPath,
        $expectedExecutionBuildRecordPath, $executionBuildManifestPath,
        $settingsPath)
}

if ($result.status -ne "PASS") {
    Write-Error ("REAL_DRAWING_VERIFICATION=FAIL`n" + $failureMessage +
        "`nEvidence: " + $runDirectory)
    exit 1
}

Write-Host "REAL_DRAWING_VERIFICATION=PASS"
Write-Host "Evidence: $runDirectory"
