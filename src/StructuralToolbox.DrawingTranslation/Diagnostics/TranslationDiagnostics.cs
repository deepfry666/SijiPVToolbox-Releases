using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace StructuralToolbox.DrawingTranslation.Diagnostics
{
    internal static class TranslationDiagnostics
    {
        internal const string ModuleAssemblyName =
            "StructuralToolbox.DrawingTranslation";

        internal static string LogPath()
        {
            string directory = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "FourSeasonsPVToolbox");
            return Path.Combine(directory, "DrawingTranslation.log");
        }

        internal static void Log(string message)
        {
            try
            {
                string path = LogPath();
                string directory = Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(path, String.Format(
                    CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss} {1}{2}",
                    DateTime.Now, message, Environment.NewLine), Encoding.UTF8);
            }
            catch (System.Exception)
            {
            }
        }

        internal static string VersionOf(Assembly assembly)
        {
            if (assembly == null)
                return "（未知）";
            try
            {
                return assembly.GetName().Version.ToString();
            }
            catch (System.Exception)
            {
                return "（未知）";
            }
        }

        internal static string LocationOf(Assembly assembly)
        {
            if (assembly == null)
                return "（未知）";
            try
            {
                string location = assembly.Location;
                return String.IsNullOrEmpty(location) ? "（无文件路径）" :
                    location;
            }
            catch (System.Exception)
            {
                return "（无法读取路径）";
            }
        }

        internal static IList<string> LoadedCopies(string assemblyName)
        {
            List<string> copies = new List<string>();
            foreach (ModuleCopy copy in LoadedModuleCopies(assemblyName))
                copies.Add(copy.Describe());
            return copies;
        }

        internal static IList<ModuleCopy> LoadedModuleCopies(string assemblyName)
        {
            List<ModuleCopy> copies = new List<ModuleCopy>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.
                GetAssemblies())
            {
                string name;
                try
                {
                    name = assembly.GetName().Name;
                }
                catch (System.Exception)
                {
                    continue;
                }
                if (!String.Equals(name, assemblyName,
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                copies.Add(new ModuleCopy(VersionOf(assembly),
                    LocationOf(assembly)));
            }
            return copies;
        }

        internal static string DescribeCopies(string assemblyName,
            IList<string> copies)
        {
            if (copies == null || copies.Count == 0)
                return "已加载 0 份（未找到本模块程序集）";
            StringBuilder builder = new StringBuilder();
            builder.Append("已加载 ").Append(copies.Count).Append(" 份");
            foreach (string copy in copies)
                builder.Append("\n    ").Append(copy);
            if (copies.Count > 1)
                builder.Append("\n注意：同一个 AutoCAD 会话里加载了多份本模块，" +
                    "命令只会执行最先加载的那一份；换新版本前必须完全退出 " +
                    "AutoCAD 再 NETLOAD。");
            return builder.ToString();
        }

        internal static string DescribeLoadedModule()
        {
            return DescribeCopies(ModuleAssemblyName,
                LoadedCopies(ModuleAssemblyName));
        }

        // 用户截图里的旧文案无法和交付版本对应，消息框、命令行和日志
        // 都要自带“现在跑的是哪一份 DLL”的指纹：模块版本加文件名。
        internal static string ModuleStamp()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string file = "（未知文件）";
            string location = LocationOf(assembly);
            if (!String.IsNullOrEmpty(location) &&
                location != "（无文件路径）" && location != "（无法读取路径）")
            {
                try { file = Path.GetFileName(location); }
                catch (System.Exception) { }
            }
            return String.Format(CultureInfo.InvariantCulture, "模块 {0} · {1}",
                VersionOf(assembly), file);
        }

        // 同一个 AutoCAD 会话里加载了多份模块时，命令只会执行最先加载的
        // 那一份。失败提示必须直接说出来，否则用户会反复 NETLOAD 新版却
        // 始终在执行旧代码。
        internal static string DuplicateWarning()
        {
            return DuplicateWarningOf(ModuleAssemblyName);
        }

        internal static string DuplicateWarningOf(string assemblyName)
        {
            IList<string> copies = LoadedCopies(assemblyName);
            if (copies == null || copies.Count <= 1)
                return String.Empty;
            StringBuilder builder = new StringBuilder();
            builder.Append("\n注意：本会话已加载 ").Append(copies.Count)
                .Append(" 份本模块，命令只会执行最先加载的那一份：");
            foreach (string copy in copies)
                builder.Append("\n    ").Append(copy);
            builder.Append("\n请完全退出 AutoCAD（确认没有残留 acad.exe 进程）" +
                "后重新 NETLOAD 新版。");
            return builder.ToString();
        }


        internal sealed class ModuleCopy
        {
            internal ModuleCopy(string version, string location)
            {
                Version = version;
                Location = location;
            }

            internal string Version { get; private set; }

            internal string Location { get; private set; }

            internal string Describe()
            {
                return String.Format(CultureInfo.InvariantCulture,
                    "版本 {0}：{1}", Version, Location);
            }

            internal Version ParsedVersion()
            {
                if (String.IsNullOrEmpty(Version))
                    return null;
                try
                {
                    return new Version(Version);
                }
                catch (System.Exception)
                {
                    return null;
                }
            }
        }

        // NETLOAD 的那一刻就能判断“这一次加载不会生效”：AutoCAD 不会替
        // 换已经注册的命令。不说清楚，用户会以为已经在跑新版本。
        internal static string StartupWarning()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return StartupWarningOf(ModuleAssemblyName, LocationOf(assembly));
        }

        internal static string StartupWarningOf(string assemblyName,
            string preferredPath)
        {
            return StartupWarningFor(LoadedModuleCopies(assemblyName),
                preferredPath);
        }

        internal static string StartupWarningFor(IList<ModuleCopy> copies,
            string preferredPath)
        {
            if (copies == null || copies.Count <= 1)
                return String.Empty;
            StringBuilder builder = new StringBuilder();
            builder.Append("本模块在当前 AutoCAD 会话里已经加载过 ")
                .Append(copies.Count).Append(" 份：");
            foreach (ModuleCopy copy in copies)
                builder.Append("\n    ").Append(copy.Describe());
            builder.Append("\n\n本次加载不会生效：AutoCAD 不会替换已经注册的" +
                "命令，STPVTRANSLATE 仍然执行最早加载的那一份代码。")
                .Append("\n请完全退出 AutoCAD（任务管理器里确认没有残留 " +
                    "acad.exe 进程）后重新打开，再 NETLOAD：");
            if (!String.IsNullOrEmpty(preferredPath))
                builder.Append("\n    ").Append(preferredPath);
            return builder.ToString();
        }

        // 命令执行前先确认当前这一份不是被新版本顶替掉的旧代码。
        internal static string StaleVersionWarning()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return StaleVersionWarningOf(new ModuleCopy(VersionOf(assembly),
                LocationOf(assembly)), LoadedModuleCopies(ModuleAssemblyName));
        }

        internal static string StaleVersionWarningOf(ModuleCopy executing,
            IList<ModuleCopy> copies)
        {
            if (executing == null || copies == null)
                return String.Empty;
            Version current = executing.ParsedVersion();
            if (current == null)
                return String.Empty;
            List<ModuleCopy> newer = new List<ModuleCopy>();
            foreach (ModuleCopy copy in copies)
            {
                Version other = copy == null ? null : copy.ParsedVersion();
                if (other != null && other > current)
                    newer.Add(copy);
            }
            if (newer.Count == 0)
                return String.Empty;
            StringBuilder builder = new StringBuilder();
            builder.Append("当前执行的是旧版本 ").Append(executing.Describe())
                .Append("，本会话已经加载了更新的版本：");
            foreach (ModuleCopy copy in newer)
                builder.Append("\n    ").Append(copy.Describe());
            builder.Append("\n本次不修改图纸。请完全退出 AutoCAD（任务管理器里" +
                "确认没有残留 acad.exe 进程）后重新打开，再 NETLOAD 新版本。");
            return builder.ToString();
        }

        // 只检测会话内已加载的多份模块还不够：同一个会话里再次 NETLOAD
        // 同名程序集时 AutoCAD 会静默忽略，用户会以为已经在跑新版。这里
        // 直接比较磁盘上的交付版本，执行前就拦住旧版本。
        internal static string StaleDeliveryWarning()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string location = LocationOf(assembly);
            return StaleDeliveryWarningOf(new ModuleCopy(VersionOf(assembly),
                location), DeliveryCopies(location));
        }

        internal static string StaleDeliveryWarningOf(ModuleCopy executing,
            IList<ModuleCopy> delivered)
        {
            if (executing == null || delivered == null)
                return String.Empty;
            Version current = executing.ParsedVersion();
            if (current == null)
                return String.Empty;
            List<ModuleCopy> newer = new List<ModuleCopy>();
            foreach (ModuleCopy copy in delivered)
            {
                Version other = copy == null ? null : copy.ParsedVersion();
                if (other == null || other <= current)
                    continue;
                bool known = false;
                foreach (ModuleCopy seen in newer)
                {
                    if (String.Equals(seen.Location, copy.Location,
                        StringComparison.OrdinalIgnoreCase))
                        known = true;
                }
                if (!known)
                    newer.Add(copy);
            }
            if (newer.Count == 0)
                return String.Empty;
            StringBuilder builder = new StringBuilder();
            builder.Append("当前会话执行的是 ").Append(executing.Describe())
                .Append("，但磁盘上已经有更新版本：");
            foreach (ModuleCopy copy in newer)
                builder.Append("\n    ").Append(copy.Describe());
            builder.Append("\nAutoCAD 不会替换已经注册的命令，本次操作不会用上" +
                "新版本，图纸不会修改。请完全退出 AutoCAD（任务管理器里确认" +
                "没有残留 acad.exe 进程）后重新打开，再 NETLOAD 上面列出的" +
                "新版本。");
            return builder.ToString();
        }

        // 交付目录形如 outputs\StructuralToolbox.DrawingTranslation_preview8_
        // AutoCAD2020\，这里扫描同一个交付根目录下的同名 DLL，找出更新版本。
        internal static IList<ModuleCopy> DeliveryCopies(string runningLocation)
        {
            List<ModuleCopy> copies = new List<ModuleCopy>();
            try
            {
                if (String.IsNullOrEmpty(runningLocation) ||
                    runningLocation == "（无文件路径）" ||
                    runningLocation == "（无法读取路径）")
                    return copies;
                string directory = Path.GetDirectoryName(runningLocation);
                if (String.IsNullOrEmpty(directory))
                    return copies;
                string parent = Path.GetDirectoryName(directory);
                if (String.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                    return copies;
                string fileName = Path.GetFileName(runningLocation);
                if (String.IsNullOrEmpty(fileName))
                    return copies;
                string[] folders = Directory.GetDirectories(parent,
                    "StructuralToolbox.DrawingTranslation_*");
                foreach (string folder in folders)
                {
                    string candidate = Path.Combine(folder, fileName);
                    if (!File.Exists(candidate))
                        continue;
                    string version = FileVersionOf(candidate);
                    if (String.IsNullOrEmpty(version))
                        continue;
                    copies.Add(new ModuleCopy(version, candidate));
                }
            }
            catch (System.Exception)
            {
            }
            return copies;
        }

        internal static string FileVersionOf(string path)
        {
            try
            {
                System.Diagnostics.FileVersionInfo info =
                    System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                if (info == null)
                    return String.Empty;
                if (!String.IsNullOrEmpty(info.FileVersion))
                    return info.FileVersion.Trim();
                return String.IsNullOrEmpty(info.ProductVersion)
                    ? String.Empty : info.ProductVersion.Trim();
            }
            catch (System.Exception)
            {
                return String.Empty;
            }
        }
    }
}