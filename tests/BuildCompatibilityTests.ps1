param(
    [string]$AutoCADDir = "D:\cad\AutoCAD 2020"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $projectRoot "scripts\build.ps1"
$passed = 0

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:passed++
}

function Assert-BuildRejected([hashtable]$Arguments, [string]$ExpectedText) {
    try {
        & $buildScript @Arguments -ValidateOnly -PassThru | Out-Null
    } catch {
        if (-not $_.Exception.Message.Contains($ExpectedText)) {
            throw "FAIL: expected '$ExpectedText', received '$($_.Exception.Message)'"
        }
        $script:passed++
        return
    }
    throw "FAIL: incompatible build arguments were accepted"
}

$validation = & $buildScript -AutoCADDir $AutoCADDir -TargetSeries "R23.1" `
    -ExpectedAutoCADYear 2020 -BuildProfile "acad2020-netfx" `
    -ValidateOnly -PassThru
Assert-Condition ([string]$validation.validation -eq "passed" -and
    [string]$validation.targetSeries -eq "R23.1" -and
    [int]$validation.targetYear -eq 2020) `
    "AutoCAD 2020 R23.1 SDK validation passes"
$coreApis = @($validation.sdkFiles | Where-Object {
    $_.name -in @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")
})
Assert-Condition ($coreApis.Count -eq 3 -and
    @($coreApis | Where-Object {
        ([version]$_.assemblyVersion).Major -ne 23 -or
        ([version]$_.assemblyVersion).Minor -ne 1 -or
        [string]$_.signatureStatus -ne "Valid" -or
        -not ([string]$_.signerSubject).Contains("Autodesk")
    }).Count -eq 0) `
    "all core APIs are R23.1 and validly Autodesk-signed"

Assert-BuildRejected @{
    AutoCADDir = $AutoCADDir
    TargetSeries = "R24.0"
    ExpectedAutoCADYear = 2021
    BuildProfile = "acad2021-netfx"
} "implemented release build profile"
Assert-BuildRejected @{
    AutoCADDir = $AutoCADDir
    TargetSeries = "R23.1"
    ExpectedAutoCADYear = 2021
    BuildProfile = "acad2020-netfx"
} "does not map"
Assert-BuildRejected @{
    AutoCADDir = $AutoCADDir
    TargetSeries = "R25.0"
    ExpectedAutoCADYear = 2025
    BuildProfile = "acad2025-net8"
} "requires the independent .NET 8 build pipeline"
Assert-BuildRejected @{
    AutoCADDir = (Join-Path $projectRoot "work\missing-autocad-sdk")
    TargetSeries = "R23.1"
    ExpectedAutoCADYear = 2020
    BuildProfile = "acad2020-netfx"
} "AutoCAD executable was not found"

Write-Host "PASS $passed build compatibility tests"
