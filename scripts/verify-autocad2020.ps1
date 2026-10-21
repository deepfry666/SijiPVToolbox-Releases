param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020",
    [string]$InputDwg,
    [string]$Version,
    [switch]$SkipReleaseBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceManifestPath = Join-Path $projectRoot `
    "bundle\ZhStructureToolbox.bundle\PackageContents.xml"
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$sourceManifest = Get-Content -LiteralPath $sourceManifestPath `
        -Raw -Encoding UTF8
    $Version = [string]$sourceManifest.ApplicationPackage.AppVersion
}
try {
    $requestedVersion = [version]$Version
} catch {
    throw "Version is invalid: $Version"
}
$requestedVersion = [version]::new(
    $requestedVersion.Major,
    $requestedVersion.Minor,
    $(if ($requestedVersion.Build -lt 0) { 0 } else { $requestedVersion.Build }),
    $(if ($requestedVersion.Revision -lt 0) { 0 } else { $requestedVersion.Revision }))
$version3 = $requestedVersion.ToString(3)
$version4 = $requestedVersion.ToString(4)
$verificationRoot = Join-Path $projectRoot "work\verification-v$version3"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runRoot = Join-Path $verificationRoot ("runs\" + $timestamp)
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
$releaseAssembly = Join-Path $projectRoot `
    "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.dll"
$releaseModulePaths = @(
    (Join-Path $projectRoot `
        "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.Contracts.dll"),
    $releaseAssembly,
    (Join-Path $projectRoot `
        "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.Rail.dll"),
    (Join-Path $projectRoot `
        "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.Ballast.dll"),
    (Join-Path $projectRoot `
        "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.Update.dll"),
    (Join-Path $projectRoot `
        "bundle\ZhStructureToolbox.bundle\Contents\Windows\R23.1\StructuralToolbox.DrawingTranslation.dll")
)
$verificationAssembly = Join-Path $projectRoot `
    "work\verify\StructuralToolbox.Verify.dll"
$profilesRegistrySubKey =
    "Software\Autodesk\AutoCAD\R23.1\ACAD-3001:804\Profiles"
$profilesRegistryPath = "Registry::HKEY_CURRENT_USER\$profilesRegistrySubKey"
$verificationProfile = "<<未命名配置>>"

if ([string]::IsNullOrWhiteSpace($InputDwg)) {
    $templateName = -join @([char]27169, [char]26495, [char]22270) + ".dwg"
    $InputDwg = Join-Path $projectRoot $templateName
}
$InputDwg = [IO.Path]::GetFullPath($InputDwg)
if (-not (Test-Path -LiteralPath $InputDwg -PathType Leaf)) {
    throw "Verification input DWG was not found: $InputDwg"
}
if (-not (Test-Path -LiteralPath $coreConsole -PathType Leaf)) {
    throw "AutoCAD 2020 Core Console was not found: $coreConsole"
}
$coreConsole = [IO.Path]::GetFullPath($coreConsole)
$coreConsoleItem = Get-Item -LiteralPath $coreConsole
$coreConsoleEvidence = [ordered]@{
    Path = $coreConsoleItem.FullName
    SHA256 = (Get-FileHash -LiteralPath $coreConsoleItem.FullName `
        -Algorithm SHA256).Hash
    FileVersion = $coreConsoleItem.VersionInfo.FileVersion
    ProductVersion = $coreConsoleItem.VersionInfo.ProductVersion
    SignatureStatus = (Get-AuthenticodeSignature `
        -LiteralPath $coreConsoleItem.FullName).Status.ToString()
}
$existingQDrive = Get-PSDrive -Name Q -ErrorAction SilentlyContinue
$createdQMapping = $false
if ($null -ne $existingQDrive) {
    $qVerifier = "Q:\scripts\verify-autocad2020.ps1"
    if (-not (Test-Path -LiteralPath $qVerifier -PathType Leaf) -or
        (Get-FileHash -LiteralPath $qVerifier -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash) {
        throw "Drive Q: is already in use by another location."
    }
}
if ($SkipReleaseBuild -and @($releaseModulePaths | Where-Object {
        -not (Test-Path -LiteralPath $_ -PathType Leaf)
    }).Count -ne 0) {
    throw "SkipReleaseBuild requires all six existing R23.1 release DLLs."
}

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$inputCopy = Join-Path $runRoot "verification-input.dwg"
Copy-Item -LiteralPath $InputDwg -Destination $inputCopy -Force
$releaseBuildCommand = if ($SkipReleaseBuild) {
    "SKIPPED release build; verify existing bundle R23.1 StructuralToolbox.dll"
} else {
    "powershell -File scripts/build.ps1 -AutoCADDir `"$AutoCADDir`""
}
$commandLines = @(
    "# Commands executed by scripts/verify-autocad2020.ps1",
    $releaseBuildCommand,
    "powershell -File scripts/build.ps1 -AutoCADDir `"$AutoCADDir`" -InternalVerify",
    "`"$coreConsole`" /i Q:/work/verification-v$version3/runs/$timestamp/verification-input.dwg /s Q:/work/verification-v$version3/runs/$timestamp/coreconsole.scr /l chs /isolate FOURJVERIFY_$($timestamp.Replace('-', '')) Q:/work/verification-v$version3/runs/$timestamp/isolate"
)
[IO.File]::WriteAllLines((Join-Path $runRoot "commands.txt"),
    $commandLines, [Text.UTF8Encoding]::new($false))

function Invoke-LoggedCommand {
    param(
        [scriptblock]$Command,
        [string]$LogPath,
        [string]$FailureMessage
    )

    $commandOutput = & $Command *>&1
    $exitCode = $LASTEXITCODE
    $text = ($commandOutput | Out-String).Replace([string][char]0, "")
    [IO.File]::WriteAllText($LogPath, $text,
        [Text.UTF8Encoding]::new($false))
    Write-Host $text
    if ($exitCode -ne 0) {
        throw "$FailureMessage Exit code: $exitCode. Log: $LogPath"
    }
}

$buildReleaseLog = Join-Path $runRoot "01-build-release.log"
if ($SkipReleaseBuild) {
    $existingReleaseHash = (Get-FileHash -LiteralPath $releaseAssembly `
        -Algorithm SHA256).Hash
    $skipText = "SKIPPED: release build.`r`n" +
        "Existing assembly: $releaseAssembly`r`n" +
        "SHA-256: $existingReleaseHash`r`n"
    [IO.File]::WriteAllText($buildReleaseLog, $skipText,
        [Text.UTF8Encoding]::new($false))
    Write-Host $skipText
} else {
    Invoke-LoggedCommand -LogPath $buildReleaseLog `
        -FailureMessage "Release build failed." -Command {
            & (Join-Path $projectRoot "scripts\build.ps1") `
                -AutoCADDir $AutoCADDir
        }
}
if (-not (Test-Path -LiteralPath $releaseAssembly -PathType Leaf)) {
    throw "Expected R23.1 release assembly is missing: $releaseAssembly"
}
$releaseAssemblyVersion =
    [Reflection.AssemblyName]::GetAssemblyName($releaseAssembly).Version
if ($releaseAssemblyVersion -ne [version]$version4) {
    throw "Release assembly version is $releaseAssemblyVersion; expected $version4."
}
$releaseAssemblyHashBeforeVerificationBuild =
    (Get-FileHash -LiteralPath $releaseAssembly -Algorithm SHA256).Hash
$buildVerificationLog = Join-Path $runRoot "02-build-internal-verifier.log"
Invoke-LoggedCommand -LogPath $buildVerificationLog `
    -FailureMessage "Internal verifier build failed." -Command {
        & (Join-Path $projectRoot "scripts\build.ps1") `
            -AutoCADDir $AutoCADDir -InternalVerify
    }

if (-not (Test-Path -LiteralPath $releaseAssembly -PathType Leaf) -or
    -not (Test-Path -LiteralPath $verificationAssembly -PathType Leaf)) {
    throw "Expected release or verification assembly is missing after build."
}
$releaseAssemblyHashAfterVerificationBuild =
    (Get-FileHash -LiteralPath $releaseAssembly -Algorithm SHA256).Hash
if ($releaseAssemblyHashAfterVerificationBuild -ne
    $releaseAssemblyHashBeforeVerificationBuild) {
    throw "Internal verifier build changed the release assembly. Retry from a stable workspace."
}
$expectedReleaseAssemblyHash = $releaseAssemblyHashAfterVerificationBuild

$scriptPath = Join-Path $runRoot "coreconsole.scr"
$scriptLines = @(
    '(setvar "REPORTERROR" 0)',
    '(setvar "SECURELOAD" 0)',
    '_.NETLOAD',
    'Q:/bundle/ZhStructureToolbox.bundle/Contents/Windows/R23.1/StructuralToolbox.Contracts.dll',
    '_.NETLOAD',
    'Q:/bundle/ZhStructureToolbox.bundle/Contents/Windows/R23.1/StructuralToolbox.dll',
    '_.NETLOAD',
    'Q:/bundle/ZhStructureToolbox.bundle/Contents/Windows/R23.1/StructuralToolbox.Rail.dll',
    '_.NETLOAD',
    'Q:/bundle/ZhStructureToolbox.bundle/Contents/Windows/R23.1/StructuralToolbox.Ballast.dll',
    '_.NETLOAD',
    'Q:/bundle/ZhStructureToolbox.bundle/Contents/Windows/R23.1/StructuralToolbox.Update.dll',
    '_.NETLOAD',
    'Q:/bundle/ZhStructureToolbox.bundle/Contents/Windows/R23.1/StructuralToolbox.DrawingTranslation.dll',
    '_.NETLOAD',
    'Q:/work/verify/StructuralToolbox.Verify.dll',
    'ST_INTERNAL_VERIFY_RAILS',
    '_.QSAVE',
    '_.QUIT'
)
[IO.File]::WriteAllLines($scriptPath, $scriptLines,
    [Text.Encoding]::ASCII)

$environment = [ordered]@{
    VerificationVersion = $version3
    VerificationTimestamp = (Get-Date).ToString("o")
    AutoCADYear = 2020
    AutoCADSeries = "R23.1"
    ReleaseBuildSkipped = [bool]$SkipReleaseBuild
    ReleaseAssemblySHA256BeforeInternalBuild =
        $releaseAssemblyHashBeforeVerificationBuild
    ReleaseModules = @($releaseModulePaths | ForEach-Object {
        $item = Get-Item -LiteralPath $_
        [ordered]@{
            Name = $item.BaseName
            Path = $item.FullName
            SHA256 = (Get-FileHash -LiteralPath $item.FullName `
                -Algorithm SHA256).Hash
        }
    })
    ProjectRoot = $projectRoot
    OriginalInput = $InputDwg
    VerificationInputCopy = $inputCopy
    OperatingSystem = (Get-CimInstance Win32_OperatingSystem | Select-Object `
        Caption, Version, BuildNumber, OSArchitecture)
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    CoreConsole = $coreConsoleEvidence
    AutoCADRegistry = (Get-ItemProperty `
        "HKLM:\SOFTWARE\Autodesk\AutoCAD\R23.1\ACAD-3001:804" `
        -ErrorAction SilentlyContinue | Select-Object ProductName, Release,
        Language, AcadLocation, ProductCode)
    AutoCADFiles = @(
        "acad.exe",
        "accoreconsole.exe",
        "acmgd.dll",
        "acdbmgd.dll",
        "accoremgd.dll"
    ) | ForEach-Object {
        $path = Join-Path $AutoCADDir $_
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $item = Get-Item -LiteralPath $path
            [ordered]@{
                Name = $_
                Path = $item.FullName
                FileVersion = $item.VersionInfo.FileVersion
                ProductVersion = $item.VersionInfo.ProductVersion
                Length = $item.Length
                SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
                SignatureStatus = (Get-AuthenticodeSignature `
                    -LiteralPath $path).Status.ToString()
            }
        }
    }
    ExpectedReleaseAssembly = [ordered]@{
        Path = $releaseAssembly
        SHA256 = $expectedReleaseAssemblyHash
    }
    LoadedReleaseAssembly = $null
    InternalVerificationAssembly = [ordered]@{
        Path = $verificationAssembly
        SHA256 = (Get-FileHash -LiteralPath $verificationAssembly `
            -Algorithm SHA256).Hash
    }
    InputCopySHA256 = (Get-FileHash -LiteralPath $inputCopy `
        -Algorithm SHA256).Hash
    AutoCADGuiProcessesAtStart = @(Get-Process acad -ErrorAction SilentlyContinue |
        Select-Object Id, ProcessName, StartTime, Path, MainWindowTitle)
}
$environmentPath = Join-Path $runRoot "environment.json"
$environment | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $environmentPath -Encoding utf8

if (-not (Test-Path -LiteralPath $profilesRegistryPath -PathType Container)) {
    throw "AutoCAD profile registry key was not found: $profilesRegistryPath"
}
$verificationProfilePath = Join-Path $profilesRegistryPath $verificationProfile
$verificationGeneralPath = Join-Path $verificationProfilePath "General Configuration"
$verificationGeneral = Get-ItemProperty -LiteralPath $verificationGeneralPath `
    -ErrorAction SilentlyContinue
if ($null -eq $verificationGeneral -or
    [string]::IsNullOrWhiteSpace([string]$verificationGeneral.TempDirectory)) {
    throw "Verification profile lacks TempDirectory: $verificationProfile"
}
$originalProfile = [string](Get-Item -LiteralPath $profilesRegistryPath).GetValue("")
$profileChanged = $false
$profileStarted = Get-Date
$profileDuring = $originalProfile
$profileAfter = $originalProfile
$profileRestored = $true

if ($null -eq $existingQDrive) {
    subst.exe Q: $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Could not map Q: to the project root."
    }
    $createdQMapping = $true
}
try {
    if (-not [string]::Equals($originalProfile, $verificationProfile,
            [StringComparison]::Ordinal)) {
        $profilesKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(
            $profilesRegistrySubKey, $true)
        if ($null -eq $profilesKey) {
            throw "AutoCAD profile registry key could not be opened for verification."
        }
        try {
            $profilesKey.SetValue("", $verificationProfile,
                [Microsoft.Win32.RegistryValueKind]::String)
        }
        finally {
            $profilesKey.Dispose()
        }
        $profileChanged = $true
    }
    $profileDuring = [string](Get-Item -LiteralPath $profilesRegistryPath).GetValue("")
    if (-not [string]::Equals($profileDuring, $verificationProfile,
            [StringComparison]::Ordinal)) {
        throw "AutoCAD verification profile switch did not take effect."
    }

    $relativeRunPath = "work\verification-v$version3\runs\$timestamp"
    $qRunRoot = "Q:\" + $relativeRunPath
    $qInput = Join-Path $qRunRoot "verification-input.dwg"
    $qScript = Join-Path $qRunRoot "coreconsole.scr"
    $isolateRoot = Join-Path $qRunRoot "isolate"
    $tempRoot = Join-Path $qRunRoot "temp"
    New-Item -ItemType Directory -Path $isolateRoot,$tempRoot -Force | Out-Null

    $oldTemp = $env:TEMP
    $oldTmp = $env:TMP
    $env:TEMP = $tempRoot
    $env:TMP = $tempRoot
    try {
        $releaseHashImmediatelyBeforeRun =
            (Get-FileHash -LiteralPath $releaseAssembly -Algorithm SHA256).Hash
        if ($releaseHashImmediatelyBeforeRun -ne
            $expectedReleaseAssemblyHash) {
            throw "Release assembly changed before Core Console started."
        }
        $coreConsoleHashImmediatelyBeforeRun =
            (Get-FileHash -LiteralPath $coreConsole -Algorithm SHA256).Hash
        if ($coreConsoleHashImmediatelyBeforeRun -ne
            [string]$coreConsoleEvidence.SHA256) {
            throw "Core Console executable changed before verification started."
        }
        $coreOutput = & $coreConsole /i $qInput /s $qScript /l chs `
            /isolate ("FOURJVERIFY_" + $timestamp.Replace("-", "")) `
            $isolateRoot 2>&1
        $coreExitCode = $LASTEXITCODE
    }
    finally {
        $env:TEMP = $oldTemp
        $env:TMP = $oldTmp
    }

    $coreText = ($coreOutput | Out-String).Replace([string][char]0, "")
    $coreLog = Join-Path $runRoot "03-coreconsole.log"
    [IO.File]::WriteAllText($coreLog, $coreText,
        [Text.UTF8Encoding]::new($false))
    $coreConsoleLogHash =
        (Get-FileHash -LiteralPath $coreLog -Algorithm SHA256).Hash
    Write-Host $coreText
    $successMarker = [regex]::Match($coreText,
        'STVERIFY-DONE[^\r\n]*')
    $failureMarker = [regex]::Match($coreText,
        'STVERIFY-FAIL[^\r\n]*')
    if ($coreExitCode -ne 0 -or -not $successMarker.Success -or
        $failureMarker.Success) {
        throw "Core Console verification failed or did not emit its success marker. Exit code: $coreExitCode. Log: $coreLog"
    }

    $releaseHashMarker = [regex]::Match($successMarker.Value,
        '(?:^|\s)RELEASE_SHA256=([0-9A-Fa-f]{64})(?=\s|$)')
    $releaseMvidMarker = [regex]::Match($successMarker.Value,
        '(?:^|\s)RELEASE_MVID=([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-' +
        '[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})(?=\s|$)')
    $releasePathMarker = [regex]::Match($successMarker.Value,
        '(?:^|\s)RELEASE_PATH="([^"]+)"(?=\s|$)')
    $moduleCountMarker = [regex]::Match($successMarker.Value,
        '(?:^|\s)MODULES=([0-9]+)(?=\s|$)')
    if (-not $releaseHashMarker.Success -or
        -not $releaseMvidMarker.Success -or
        -not $releasePathMarker.Success -or
        -not $moduleCountMarker.Success -or
        [int]$moduleCountMarker.Groups[1].Value -ne 6) {
        throw "Core Console success marker lacks six-module attestation."
    }
    $reportedReleaseHash =
        $releaseHashMarker.Groups[1].Value.ToUpperInvariant()
    $reportedReleaseMvid =
        ([guid]$releaseMvidMarker.Groups[1].Value).ToString("D")
    $reportedReleasePath = [IO.Path]::GetFullPath(
        $releasePathMarker.Groups[1].Value)
    if (-not (Test-Path -LiteralPath $reportedReleasePath -PathType Leaf)) {
        throw "Core Console reported a release assembly path that does not exist: $reportedReleasePath"
    }
    $reportedReleasePathHash =
        (Get-FileHash -LiteralPath $reportedReleasePath -Algorithm SHA256).Hash
    $releaseHashAfterRun =
        (Get-FileHash -LiteralPath $releaseAssembly -Algorithm SHA256).Hash
    $coreConsoleHashAfterRun =
        (Get-FileHash -LiteralPath $coreConsole -Algorithm SHA256).Hash
    if ($reportedReleaseHash -ne $expectedReleaseAssemblyHash -or
        $reportedReleasePathHash -ne $expectedReleaseAssemblyHash -or
        $releaseHashAfterRun -ne $expectedReleaseAssemblyHash) {
        throw "Core Console loaded release hash does not match the verified payload."
    }
    if ($coreConsoleHashAfterRun -ne [string]$coreConsoleEvidence.SHA256) {
        throw "Core Console executable changed during verification."
    }

    $environment.LoadedReleaseAssembly = [ordered]@{
        Path = $reportedReleasePath
        SHA256 = $reportedReleaseHash
        MVID = $reportedReleaseMvid
    }
    $environment | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $environmentPath -Encoding utf8
    $environmentHash =
        (Get-FileHash -LiteralPath $environmentPath -Algorithm SHA256).Hash

    $result = [ordered]@{
        Status = "PASS"
        CoreConsoleExitCode = $coreExitCode
        CoreConsole = $coreConsoleEvidence
        SuccessMarker = $successMarker.Value
        LoadedReleaseAssembly = $environment.LoadedReleaseAssembly
        OutputDwg = $inputCopy
        OutputDwgSHA256 = (Get-FileHash -LiteralPath $inputCopy `
            -Algorithm SHA256).Hash
        CoreConsoleLog = $coreLog
        CoreConsoleLogSHA256 = $coreConsoleLogHash
        EnvironmentLog = $environmentPath
        EnvironmentSHA256 = $environmentHash
    }
    $result | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $runRoot "result.json") -Encoding utf8
    Write-Host ("PASS: " + $successMarker.Value)
    Write-Host ("Evidence: " + $runRoot)

    foreach ($generatedRoot in @($isolateRoot, $tempRoot)) {
        $resolvedGeneratedRoot = [IO.Path]::GetFullPath($generatedRoot)
        $resolvedQRunRoot = [IO.Path]::GetFullPath($qRunRoot)
        if (-not $resolvedGeneratedRoot.StartsWith(
            $resolvedQRunRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a generated directory outside the run root: $resolvedGeneratedRoot"
        }
        if (Test-Path -LiteralPath $resolvedGeneratedRoot) {
            Remove-Item -LiteralPath $resolvedGeneratedRoot -Recurse -Force
        }
    }
}
finally {
    if ($profileChanged) {
        $profilesKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(
            $profilesRegistrySubKey, $true)
        if ($null -eq $profilesKey) {
            throw "AutoCAD profile registry key could not be opened for restoration."
        }
        try {
            $profilesKey.SetValue("", $originalProfile,
                [Microsoft.Win32.RegistryValueKind]::String)
        }
        finally {
            $profilesKey.Dispose()
        }
    }
    $profileAfter = [string](Get-Item -LiteralPath $profilesRegistryPath).GetValue("")
    $profileRestored = [string]::Equals($profileAfter, $originalProfile,
        [StringComparison]::Ordinal)
    $profileFinished = Get-Date
    $profileLog =
        "ProfileRegistrySubKey=$profilesRegistrySubKey " +
        "Before=$originalProfile Target=$verificationProfile " +
        "During=$profileDuring After=$profileAfter " +
        "Started=$($profileStarted.ToString('o')) " +
        "Finished=$($profileFinished.ToString('o')) " +
        "Restored=$profileRestored`r`n"
    [IO.File]::WriteAllText((Join-Path $runRoot "profile-environment.txt"),
        $profileLog, [Text.UTF8Encoding]::new($false))
    if ($createdQMapping) {
        subst.exe Q: /d | Out-Null
    }
}
