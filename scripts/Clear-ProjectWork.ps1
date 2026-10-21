[CmdletBinding()]
param(
    [switch]$Apply,

    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}

$projectRootPath = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd("\")
$workRoot = [IO.Path]::GetFullPath((Join-Path $projectRootPath "work"))
$expectedParent = [IO.Path]::GetFullPath(
    [IO.Path]::GetDirectoryName($workRoot)).TrimEnd("\")
if ($expectedParent -ne $projectRootPath -or
    [IO.Path]::GetFileName($workRoot) -ne "work") {
    throw "Refusing cleanup because work is not a direct child of ProjectRoot."
}

$protectedNames = @("legacy-output", "keep")

function Get-ItemBytes([IO.FileSystemInfo]$Item) {
    if (-not $Item.PSIsContainer) {
        return [int64]$Item.Length
    }
    $measurement = Get-ChildItem -LiteralPath $Item.FullName -Recurse -File `
        -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum
    if ($null -eq $measurement -or $null -eq $measurement.Sum) {
        return [int64]0
    }
    return [int64]$measurement.Sum
}

if (-not (Test-Path -LiteralPath $workRoot -PathType Container)) {
    Write-Host "WORK_CLEANUP_TARGETS=0"
    Write-Host "WORK_CLEANUP_BYTES=0"
    Write-Host "WORK_CLEANUP=NOTHING_TO_DO"
    exit 0
}

$workPrefix = $workRoot.TrimEnd("\") + "\"
$targets = @(Get-ChildItem -LiteralPath $workRoot -Force | Where-Object {
    $protectedNames -notcontains $_.Name
})
$entries = @($targets | ForEach-Object {
    if (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing cleanup of a link or junction: $($_.FullName)"
    }
    $fullPath = [IO.Path]::GetFullPath($_.FullName)
    if (-not $fullPath.StartsWith($workPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing cleanup outside work: $fullPath"
    }
    [pscustomobject]@{
        Name = $_.Name
        FullPath = $fullPath
        Bytes = Get-ItemBytes $_
    }
} | Sort-Object Name)

$totalMeasurement = $entries | Measure-Object Bytes -Sum
if ($entries.Count -eq 0 -or $null -eq $totalMeasurement -or
    $null -eq $totalMeasurement.Sum) {
    $totalBytes = 0
} else {
    $totalBytes = [int64]$totalMeasurement.Sum
}

foreach ($entry in $entries) {
    Write-Host ("{0,-58} {1,12:N2} MiB" -f `
        $entry.Name, ($entry.Bytes / 1MB))
}
Write-Host ""
Write-Host "Protected: work\legacy-output, work\keep"
Write-Host "WORK_CLEANUP_TARGETS=$($entries.Count)"
Write-Host "WORK_CLEANUP_BYTES=$totalBytes"

if (-not $Apply) {
    Write-Host "WORK_CLEANUP=PREVIEW"
    Write-Host "No files were removed. Run again with -Apply to clean."
    exit 0
}

foreach ($entry in $entries) {
    if (-not (Test-Path -LiteralPath $entry.FullPath)) {
        continue
    }
    Remove-Item -LiteralPath $entry.FullPath -Recurse -Force
}

Write-Host "WORK_CLEANUP=APPLIED"
Write-Host ("Freed approximately {0:N2} MiB." -f ($totalBytes / 1MB))
