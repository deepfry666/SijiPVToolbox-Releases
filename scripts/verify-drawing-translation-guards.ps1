param([string]$AutoCADDir = "D:\\cad\\AutoCAD 2020")

# preview10 守卫验证（AutoCAD 2020 Core Console）
#
# 能在这里验证的：
#   1. NETLOAD 记录的模块版本、加载份数、重复加载提示；
#   2. 磁盘上存在更新版本时的拦截提示；
#   3. STPVTRANSLATEINFO 的自检输出。
# 不能在这里验证的：
#   STPVTRANSLATECONFIRM 的命令体依赖 WinForms 对话框，
#   AutoCAD 内核控制台无法编译整个命令体（实测命令入口不会执行），
#   因此命令级拦截必须在真实 AutoCAD 图形界面里验证。作为替代，本脚本
#   直接检查交付 DLL 的字符串表，确认发布的二进制确实带上了拦截提示。

$ErrorActionPreference = "Stop"
# 调用方可能把 PowerShell 7 的模块路径塞进 PSModulePath，让 5.1 找不到自带
# 模块（实测 Get-FileHash 会变成“无法识别的 cmdlet”）。只保留自带模块目录。
$windowsModules = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\Modules"
if (Test-Path -LiteralPath $windowsModules) { $env:PSModulePath = $windowsModules }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$coreConsole = Join-Path $AutoCADDir "accoreconsole.exe"
$compiler = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe"
$moduleName = "StructuralToolbox.DrawingTranslation.dll"
$currentDll = Join-Path $projectRoot ("outputs\\StructuralToolbox.DrawingTranslation_preview10_AutoCAD2020\\" + $moduleName)
$logPath = Join-Path $env:LOCALAPPDATA "FourSeasonsPVToolbox\\DrawingTranslation.log"
foreach ($required in @($coreConsole, $compiler, $currentDll)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "missing: $required" }
}

$runRoot = Join-Path $projectRoot ("work\\guard-verify-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$sourceRoot = Join-Path $projectRoot "src\\StructuralToolbox.DrawingTranslation"
$contractsPath = Join-Path $runRoot "StructuralToolbox.Contracts.dll"
& (Join-Path $PSScriptRoot "build-contracts.ps1") `
    -OutputDirectory $runRoot | Out-Null
if (-not (Test-Path -LiteralPath $contractsPath -PathType Leaf)) {
    throw "Contracts build was not created: $contractsPath"
}

# 造一份版本号更高的同名 DLL，模拟磁盘上已经交付了更新版本。
$newerRoot = Join-Path $runRoot "newer-build"
New-Item -ItemType Directory -Path $newerRoot -Force | Out-Null
$newerDll = Join-Path $newerRoot $moduleName
$sources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -ne "AssemblyInfo.cs" } |
    Sort-Object FullName | ForEach-Object { $_.FullName })
$tempInfo = Join-Path $newerRoot "AssemblyInfo.cs"
[IO.File]::WriteAllText($tempInfo, "using System.Reflection;" + [Environment]::NewLine +
    '[assembly: AssemblyVersion("0.9.9.0")]' + [Environment]::NewLine +
    '[assembly: AssemblyFileVersion("0.9.9.0")]' + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($true)))
$sources += $tempInfo
$arguments = @("/nologo", "/target:library", "/platform:x64", "/optimize+",
    "/utf8output", "/out:$newerDll",
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'acdbmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')",
    "/reference:System.dll", "/reference:System.Core.dll",
    "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
    "/reference:System.Net.Http.dll", "/reference:System.Security.dll",
    "/reference:System.Web.Extensions.dll", "/reference:$contractsPath")
$arguments += $sources
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "newer variant build failed" }

# 交付目录形态：同一个交付根目录下并排放着 preview10（本次要跑的）和 preview11（磁盘上的更新版本）。
# 造一份“旧版模块”替身：程序集名必须和正式模块一致（文件名决定程序集名），
# 但不注册 STPVTRANSLATEINFO，用来复现“旧交付已经注册命令的会话”。
# 不能用 outputs\archive\ 里的历史 DLL：那个目录被 .gitignore 排除，
# 干净检出时不存在，门禁必须是自包含的。
$oldRoot = Join-Path $runRoot "old-build"
New-Item -ItemType Directory -Path $oldRoot -Force | Out-Null
$oldDll = Join-Path $oldRoot $moduleName
$oldSource = Join-Path $oldRoot "LegacyTranslationModule.cs"
[IO.File]::WriteAllText($oldSource, @"
using Autodesk.AutoCAD.Runtime;

namespace LegacyTranslationFixture
{
    public class LegacyCommands
    {
        [CommandMethod("STPVTRANSLATE")]
        public static void Translate()
        {
        }

        [CommandMethod("STPVTRANSLATECONFIRM")]
        public static void Confirm()
        {
        }
    }
}
"@, (New-Object Text.UTF8Encoding($true)))
$oldArguments = @("/nologo", "/target:library", "/platform:x64", "/out:$oldDll",
    # AutoCAD 2020 的 Autodesk.AutoCAD.Runtime 在 accoremgd.dll 里，acmgd.dll 单独引用编不过。
    "/reference:$(Join-Path $AutoCADDir 'acmgd.dll')",
    "/reference:$(Join-Path $AutoCADDir 'accoremgd.dll')",
    "/reference:System.dll",
    $oldSource)
& $compiler $oldArguments
if ($LASTEXITCODE -ne 0) { throw "legacy variant build failed" }
$deliveryRoot = Join-Path $runRoot "delivery"
$currentFolder = Join-Path $deliveryRoot "StructuralToolbox.DrawingTranslation_preview10_AutoCAD2020"
$newerFolder = Join-Path $deliveryRoot "StructuralToolbox.DrawingTranslation_preview11_AutoCAD2020"
New-Item -ItemType Directory -Path $currentFolder -Force | Out-Null
New-Item -ItemType Directory -Path $newerFolder -Force | Out-Null
$currentDeliveryDll = Join-Path $currentFolder $moduleName
Copy-Item -LiteralPath $currentDll -Destination $currentDeliveryDll
Copy-Item -LiteralPath $newerDll -Destination (Join-Path $newerFolder $moduleName)
Copy-Item -LiteralPath $contractsPath -Destination $currentFolder
Copy-Item -LiteralPath $contractsPath -Destination $newerFolder

$existingQ = Get-PSDrive -Name Q -ErrorAction SilentlyContinue
$createdQ = $false
if ($null -eq $existingQ) {
    & subst.exe Q: $projectRoot
    if ($LASTEXITCODE -ne 0) { throw "could not map Q:" }
    $createdQ = $true
} elseif (-not (Test-Path -LiteralPath "Q:\\scripts")) {
    throw "Q: is mapped outside this project"
}
function Convert-ToQPath([string]$value) {
    $relative = $value.Substring($projectRoot.Length).TrimStart("\\")
    return "Q:/" + $relative.Replace("\\", "/")
}
# 用 Process 而不是 Start-Job 起内核控制台：这台机器内存很紧，
# 再开一个 PowerShell 作业会直接 OutOfMemoryException。
function Invoke-Bounded([string[]]$arguments, [int]$seconds, [string]$label) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $coreConsole
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.CreateNoWindow = $true
    $quoted = @()
    foreach ($argument in $arguments) {
        if ($argument -eq '' -or $argument -match '\s') { $quoted += ('"' + $argument + '"') } else { $quoted += $argument }
    }
    $info.Arguments = ($quoted -join ' ')
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $info
    $null = $process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($seconds * 1000)) {
        try { $process.Kill() } catch { }
        try { $process.WaitForExit(15000) | Out-Null } catch { }
        $process.Dispose()
        throw "$label 超时：accoreconsole 没有在 $seconds 秒内退出"
    }
    $process.WaitForExit()
    $text = $stdout.Result + $stderr.Result
    $process.Dispose()
    return $text.Replace([string][char]0, "")
}
function Get-LogLines() {
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { return @() }
    return @([IO.File]::ReadAllLines($logPath, (New-Object Text.UTF8Encoding($false))))
}
function Assert-Contains([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
        throw "$label 失败：没有找到「$needle」"
    }
    Write-Host "PASS $label"
}
function Assert-NotContains([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) {
        throw "$label 失败：不应该出现「$needle」"
    }
    Write-Host "PASS $label"
}
function Assert-True([bool]$condition, [string]$label) {
    if (-not $condition) { throw "$label 失败" }
    Write-Host "PASS $label"
}
function Invoke-Scenario([string]$name, [string[]]$dlls, [string[]]$extra) {
    $fixture = Join-Path $runRoot ($name + ".dwg")
    $templateName = (-join @([char]27169, [char]26495, [char]22270)) + ".dwg"
    Copy-Item -LiteralPath (Join-Path $projectRoot $templateName) -Destination $fixture
    $lines = @('(setvar "REPORTERROR" 0)', '(setvar "SECURELOAD" 0)')
    $lines += '_.NETLOAD'
    $lines += (Convert-ToQPath (Join-Path $currentFolder `
        "StructuralToolbox.Contracts.dll"))
    foreach ($dll in $dlls) {
        $lines += '_.NETLOAD'
        $lines += (Convert-ToQPath $dll)
    }
    $lines += $extra
    $lines += 'STPVTRANSLATEINFO'
    $lines += '_.QUIT'
    $scriptPath = Join-Path $runRoot ($name + ".scr")
    [IO.File]::WriteAllLines($scriptPath, $lines, [Text.Encoding]::ASCII)
    $before = @(Get-LogLines).Count
    $arguments = @("/i", (Convert-ToQPath $fixture), "/s", (Convert-ToQPath $scriptPath),
        "/l", "chs", "/isolate", ("STPV_GUARD_" + $name.ToUpperInvariant()),
        (Convert-ToQPath $runRoot))
    $output = Invoke-Bounded $arguments 180 $name
    [IO.File]::WriteAllText((Join-Path $runRoot ($name + ".console.log")), $output,
        (New-Object Text.UTF8Encoding($false)))
    $all = @(Get-LogLines)
    $new = @($all | Select-Object -Skip $before)
    return [pscustomobject]@{
        Console = $output
        Lines = $new
        Log = ($new -join [Environment]::NewLine)
    }
}

function Invoke-OldSessionScenario {
    # 旧版 DLL 已经注册命令的会话里再 NETLOAD 新版：AutoCAD 静默忽略，
    # 命令仍由旧版执行。这正是用户反复看到旧报错的原因。
    # 用户现场加载过的 preview2 已归档到 outputs\archive\（该目录被
    # .gitignore 排除，干净检出时不存在），门禁改用上面编译的同名旧版替身。
    if (-not (Test-Path -LiteralPath $oldDll -PathType Leaf)) { throw "missing legacy fixture: $oldDll" }
    $fixture = Join-Path $runRoot "oldsession.dwg"
    $templateName = (-join @([char]27169, [char]26495, [char]22270)) + ".dwg"
    Copy-Item -LiteralPath (Join-Path $projectRoot $templateName) -Destination $fixture
    $lines = @('(setvar "REPORTERROR" 0)', '(setvar "SECURELOAD" 0)',
        '_.NETLOAD', (Convert-ToQPath $contractsPath),
        '_.NETLOAD', (Convert-ToQPath $oldDll),
        '_.NETLOAD', (Convert-ToQPath $currentDll),
        'STPVTRANSLATEINFO', '_.QUIT')
    $scriptPath = Join-Path $runRoot "oldsession.scr"
    [IO.File]::WriteAllLines($scriptPath, $lines, [Text.Encoding]::ASCII)
    $before = @(Get-LogLines).Count
    # 控制台输出是 UTF-16LE 且不带 BOM，必须显式按 Unicode 读取。
    $consoleFile = Join-Path $runRoot "oldsession.console.txt"
    $command = '"' + $coreConsole + '" /i "' + (Convert-ToQPath $fixture) +
        '" /s "' + (Convert-ToQPath $scriptPath) + '" /l chs /isolate STPV_GUARD_OLDSESSION "' +
        (Convert-ToQPath $runRoot) + '" > "' + $consoleFile + '" 2>&1'
    & cmd.exe /c $command | Out-Null
    $console = [IO.File]::ReadAllText($consoleFile, [Text.Encoding]::Unicode)
    $new = @(Get-LogLines | Select-Object -Skip $before)
    return [pscustomobject]@{
        Console = $console
        Log = ($new -join [Environment]::NewLine)
    }
}
try {
    $single = Invoke-Scenario "single" @($currentDll) @()
    Assert-Contains $single.Log "模块 NETLOAD：版本 0.3.0.0" "单独加载 preview10 时记录 NETLOAD 版本"
    Assert-Contains $single.Log "已加载 1 份" "单独加载时只有一份模块"
    Assert-NotContains $single.Log "模块 NETLOAD 警告" "单独加载时不提示重复加载"
    Assert-NotContains $single.Log "磁盘上已经有更新版本" "磁盘上没有更新版本时不打扰用户"
    Assert-Contains $single.Log "图纸文字翻译模块版本 0.3.0.0" "STPVTRANSLATEINFO 报告 0.3.0.0"

    $dupe = Invoke-Scenario "dupe" @($currentDll, $newerDll) @()
    Assert-Contains $dupe.Log "图纸文字翻译模块版本 0.3.0.0" "同会话再次 NETLOAD 后命令仍执行最先加载的 0.3.0.0"
    Assert-Contains $dupe.Log "已加载 1 份" "同会话第二次 NETLOAD 不增加加载份数"
    $netLoadLines = @($dupe.Lines | Where-Object { $_.Contains("模块 NETLOAD") })
    Assert-True ($netLoadLines.Count -eq 1) "同会话第二次 NETLOAD 只留下 1 条记录（AutoCAD 静默忽略同名程序集）"
    Assert-Contains ($netLoadLines -join [Environment]::NewLine) "0.3.0.0" "生效记录指向 preview10"

    $stale = Invoke-Scenario "stale" @($currentDeliveryDll) @()
    Assert-Contains $stale.Log "模块 NETLOAD 提示" "磁盘上存在更新版本时 NETLOAD 当场给出提示"
    Assert-Contains $stale.Log "磁盘上已经有更新版本" "提示说明磁盘上已有更新版本"
    Assert-Contains $stale.Log "0.9.9.0" "提示列出磁盘上的新版本号"
    Assert-Contains $stale.Log "本次操作不会用上新版本" "提示说明本次操作不会用上新版本"
    Assert-Contains $stale.Log "图纸文字翻译模块版本 0.3.0.0" "STPVTRANSLATEINFO 仍能报告正在执行的版本"
    $infoLines = @($stale.Lines | Where-Object { $_.Contains("STPVTRANSLATEINFO") })
    Assert-Contains ($infoLines -join [Environment]::NewLine) "磁盘上已经有更新版本" "STPVTRANSLATEINFO 同时报出磁盘更新版本"

    # 旧版已经加载的会话里再 NETLOAD 新版：静默忽略，没有新版自检输出。
    $oldSession = Invoke-OldSessionScenario
    Assert-Contains $oldSession.Console "未知命令" "旧版会话里 NETLOAD 新版被静默忽略：命令仍由旧版执行"
    Assert-NotContains $oldSession.Console "图纸文字翻译模块版本" "旧版会话里 STPVTRANSLATEINFO 不会输出新版自检"
    Assert-NotContains $oldSession.Log "模块 NETLOAD" "旧版会话里 NETLOAD 新版不会运行新版初始化"

    # 命令体在 AutoCAD 内核控制台无法执行，改为核对交付二进制的提示字符串。
    $binary = [IO.File]::ReadAllBytes($currentDll)
    $binaryText = [Text.Encoding]::Unicode.GetString($binary) +
        [Environment]::NewLine +
        [Text.Encoding]::Unicode.GetString($binary, 1, $binary.Length - 1)
    Assert-Contains $binaryText "中止：" "交付 DLL 内含命令级拦截日志文案"
    Assert-Contains $binaryText "磁盘上已经有更新版本" "交付 DLL 内含磁盘更新版本提示"
    Assert-Contains $binaryText "本次操作不会用上新版本" "交付 DLL 内含“本次操作不会用上新版本”说明"
    Assert-Contains $binaryText "已加载 0 份（未找到本模块程序集）" "交付 DLL 内含模块加载自检文案"
    Assert-Contains $binaryText "STPVTRANSLATETEST" "交付 DLL 内含接口自检命令"
    Assert-Contains $binaryText "STPVTRANSLATECONFIRM" "交付 DLL 内含确认后替换命令"
    Assert-NotContains $binaryText "STPVTRANSLATEINSERT" "交付 DLL 不再包含下方插入命令"
    Assert-Contains $binaryText "STPV_TRANSLATE_SELFTEST_PASS samples=" "交付 DLL 内含接口自检 PASS 记录文案"
    Assert-Contains $binaryText "STPV_TRANSLATE_SELFTEST_FAIL" "交付 DLL 内含接口自检 FAIL 记录文案"
} finally {
    if ($createdQ) { & subst.exe Q: /D | Out-Null }
}
Write-Host ("GUARD_VERIFY_DIR=" + $runRoot)
Write-Host "PASS preview10 守卫（重复加载 + 磁盘新版本）"
