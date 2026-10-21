using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using StructuralToolbox.DrawingTranslation.Diagnostics;

internal static class DrawingTranslationDiagnosticsTests
{
    private static int failures;

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            Console.WriteLine("PASS " + message);
            return;
        }
        failures++;
        Console.WriteLine("FAIL " + message);
    }

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        string path = TranslationDiagnostics.LogPath();
        Check(path.EndsWith("DrawingTranslation.log",
            StringComparison.OrdinalIgnoreCase),
            "日志固定写到 DrawingTranslation.log");
        Check(path.IndexOf("FourSeasonsPVToolbox",
            StringComparison.OrdinalIgnoreCase) >= 0,
            "日志固定写到 FourSeasonsPVToolbox 目录");

        string marker = "diag-test-" + Guid.NewGuid().ToString("N");
        TranslationDiagnostics.Log(marker);
        Check(File.Exists(path), "日志文件会被创建");
        bool found = false;
        if (File.Exists(path))
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            found = text.Contains(marker);
        }
        Check(found, "日志内容包含本次写入的记录");

        IList<string> copies = TranslationDiagnostics.LoadedCopies(
            "DrawingTranslationDiagnosticsTests");
        Check(copies.Count == 1, "能列出当前进程里同名程序集，数量为 1");
        if (copies.Count == 1)
        {
            Check(copies[0].IndexOf("DrawingTranslationDiagnosticsTests",
                StringComparison.OrdinalIgnoreCase) >= 0,
                "列出的程序集带上文件路径");
            Check(copies[0].StartsWith("版本 ", StringComparison.Ordinal),
                "列出的程序集带上版本号");
        }

        IList<string> missing = TranslationDiagnostics.LoadedCopies(
            "NoSuchModuleForDiagnostics");
        Check(missing.Count == 0, "不存在的模块名返回空清单");

        List<string> many = new List<string>();
        many.Add("版本 0.2.2.0：C:\\old\\StructuralToolbox.DrawingTranslation.dll");
        many.Add("版本 0.2.1.0：C:\\new\\StructuralToolbox.DrawingTranslation.dll");
        string described = TranslationDiagnostics.DescribeCopies("X", many);
        Check(described.Contains("已加载 2 份"), "多份加载时报告份数");
        Check(described.Contains("必须完全退出"), "多份加载时给出重启提示");

        string single = TranslationDiagnostics.DescribeCopies("X",
            new List<string>(new string[] { "版本 0.2.2.0：C:\\a.dll" }));
        Check(single.Contains("已加载 1 份"), "单份加载时报告份数");
        Check(!single.Contains("必须完全退出"), "单份加载时不给重启提示");

        string empty = TranslationDiagnostics.DescribeCopies("X",
            new List<string>());
        Check(empty.Contains("已加载 0 份"), "未加载时报告 0 份");

        string stamp = TranslationDiagnostics.ModuleStamp();
        Check(stamp.StartsWith("模块 ", StringComparison.Ordinal),
            "版本指纹以“模块”开头");
        Check(stamp.IndexOf("DrawingTranslationDiagnosticsTests",
            StringComparison.OrdinalIgnoreCase) >= 0,
            "版本指纹带上当前运行的程序集文件名");

        Check(TranslationDiagnostics.DuplicateWarning().Length == 0,
            "只加载一份本模块时不追加重复加载提示");
        Check(TranslationDiagnostics.DuplicateWarningOf(
            "NoSuchModuleForDiagnostics").Length == 0,
            "未加载模块名时不追加重复加载提示");

        TranslationDiagnostics.ModuleCopy current = new TranslationDiagnostics.ModuleCopy(
            "0.2.3.0", "C:\\old\\StructuralToolbox.DrawingTranslation.dll");
        TranslationDiagnostics.ModuleCopy newer = new TranslationDiagnostics.ModuleCopy(
            "0.2.4.0", "C:\\new\\StructuralToolbox.DrawingTranslation.dll");
        List<TranslationDiagnostics.ModuleCopy> two =
            new List<TranslationDiagnostics.ModuleCopy>();
        two.Add(current);
        two.Add(newer);
        string startup = TranslationDiagnostics.StartupWarningFor(two, newer.Location);
        Check(startup.Contains("本次加载不会生效"), "NETLOAD 重复加载时提示本次不生效");
        Check(startup.Contains("0.2.3.0") && startup.Contains("0.2.4.0"),
            "NETLOAD 重复加载时列出两份版本");
        Check(startup.Contains(newer.Location), "NETLOAD 提示里给出应当加载的路径");

        List<TranslationDiagnostics.ModuleCopy> one =
            new List<TranslationDiagnostics.ModuleCopy>();
        one.Add(newer);
        Check(TranslationDiagnostics.StartupWarningFor(one, newer.Location).Length == 0,
            "只加载一份时 NETLOAD 不打扰用户");

        string stale = TranslationDiagnostics.StaleVersionWarningOf(current, two);
        Check(stale.Contains("旧版本"), "存在更新版本时报告当前是旧版本");
        Check(stale.Contains(newer.Location), "旧版本提示里列出更新的路径");
        Check(stale.Contains("本次不修改图纸"), "旧版本提示说明不会改图纸");
        Check(TranslationDiagnostics.StaleVersionWarningOf(newer, two).Length == 0,
            "当前已是最新版本时不阻止执行");
        List<TranslationDiagnostics.ModuleCopy> same =
            new List<TranslationDiagnostics.ModuleCopy>();
        same.Add(new TranslationDiagnostics.ModuleCopy("0.2.4.0", "C:\\a.dll"));
        Check(TranslationDiagnostics.StaleVersionWarningOf(newer, same).Length == 0,
            "同版本多份加载时不阻止执行");


        // 同一个会话里第二次 NETLOAD 同名程序集会被静默忽略，所以真正能
        // 拦住用户的是“磁盘上已经有更新版本”的比对。
        TranslationDiagnostics.ModuleCopy running =
            new TranslationDiagnostics.ModuleCopy("0.2.4.0",
                "C:\\deliver\\preview8\\StructuralToolbox.DrawingTranslation.dll");
        List<TranslationDiagnostics.ModuleCopy> disk =
            new List<TranslationDiagnostics.ModuleCopy>();
        disk.Add(running);
        disk.Add(new TranslationDiagnostics.ModuleCopy("0.2.5.0",
            "C:\\deliver\\preview9\\StructuralToolbox.DrawingTranslation.dll"));
        string staleDisk = TranslationDiagnostics.StaleDeliveryWarningOf(running, disk);
        Check(staleDisk.Contains("磁盘上已经有更新版本"), "磁盘上有更新版本时给出提示");
        Check(staleDisk.Contains("0.2.5.0"), "磁盘更新版本提示里带出版本号");
        Check(staleDisk.Contains("图纸不会修改"), "磁盘更新版本提示说明不会改图纸");
        Check(staleDisk.Contains("acad.exe"), "磁盘更新版本提示要求退出 AutoCAD");
        List<TranslationDiagnostics.ModuleCopy> onlySelf =
            new List<TranslationDiagnostics.ModuleCopy>();
        onlySelf.Add(running);
        Check(TranslationDiagnostics.StaleDeliveryWarningOf(running, onlySelf).Length == 0,
            "磁盘上没有更新版本时不打扰用户");
        List<TranslationDiagnostics.ModuleCopy> duplicated =
            new List<TranslationDiagnostics.ModuleCopy>();
        duplicated.Add(disk[1]);
        duplicated.Add(new TranslationDiagnostics.ModuleCopy("0.2.5.0",
            disk[1].Location));
        Check(TranslationDiagnostics.StaleDeliveryWarningOf(running, duplicated).
            Split(new string[] { "0.2.5.0" }, StringSplitOptions.None).Length == 2,
            "重复的更新版本只列一次");

        // 交付目录扫描：同级 preview 目录里放着同名 DLL 时能读到版本。
        string probeRoot = Path.Combine(Path.GetTempPath(),
            "FourSeasonsPVToolboxDiag-" + Guid.NewGuid().ToString("N"));
        string probeFolder = Path.Combine(probeRoot,
            "StructuralToolbox.DrawingTranslation_previewX_AutoCAD2020");
        string selfPath = Assembly.GetExecutingAssembly().Location;
        Directory.CreateDirectory(probeFolder);
        string probeFile = Path.Combine(probeFolder, Path.GetFileName(selfPath));
        File.Copy(selfPath, probeFile, true);
        IList<TranslationDiagnostics.ModuleCopy> scanned =
            TranslationDiagnostics.DeliveryCopies(probeFile);
        Check(scanned.Count == 1, "扫描同级交付目录能找到同名 DLL");
        if (scanned.Count == 1)
        {
            Check(scanned[0].Location.EndsWith(
                "StructuralToolbox.DrawingTranslation_previewX_AutoCAD2020" +
                Path.DirectorySeparatorChar + Path.GetFileName(selfPath),
                StringComparison.OrdinalIgnoreCase), "扫描结果给出 DLL 完整路径");
            Check(scanned[0].ParsedVersion() != null, "扫描结果能解析文件版本");
        }
        Check(TranslationDiagnostics.DeliveryCopies(String.Empty).Count == 0,
            "没有运行路径时扫描结果为空");
        try { Directory.Delete(probeRoot, true); }
        catch (System.Exception) { }

        Console.WriteLine(failures == 0
            ? "DrawingTranslationDiagnosticsTests: ALL PASS"
            : "DrawingTranslationDiagnosticsTests: FAILURES " + failures);
        return failures == 0 ? 0 : 1;
    }
}