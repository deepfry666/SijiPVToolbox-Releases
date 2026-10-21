using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using StructuralToolbox.Contracts;

namespace StructuralToolbox.UI
{
    internal static class ToolboxModuleLoader
    {
        public static IList<IToolboxModule> Discover(out IList<string> errors)
        {
            Dictionary<string, IToolboxModule> modules =
                new Dictionary<string, IToolboxModule>(
                    StringComparer.OrdinalIgnoreCase);
            List<string> failures = new List<string>();
            List<Assembly> assemblies = new List<Assembly>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                AddAssembly(assemblies, assembly);

            string directory = Path.GetDirectoryName(
                typeof(ToolboxModuleLoader).Assembly.Location);
            if (!String.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                foreach (string path in Directory.GetFiles(directory,
                    "StructuralToolbox.*.dll"))
                {
                    try
                    {
                        AddAssembly(assemblies, Assembly.LoadFrom(path));
                    }
                    catch (Exception exception)
                    {
                        failures.Add(Path.GetFileName(path) + ": " +
                            exception.Message);
                    }
                }
            }

            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                    foreach (Exception loaderException in exception.LoaderExceptions)
                    {
                        if (loaderException != null)
                            failures.Add(assembly.GetName().Name + ": " +
                                loaderException.Message);
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(assembly.GetName().Name + ": " + exception.Message);
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || !type.IsClass ||
                        !typeof(IToolboxModule).IsAssignableFrom(type) ||
                        type.GetConstructor(Type.EmptyTypes) == null)
                    {
                        continue;
                    }

                    try
                    {
                        IToolboxModule module =
                            (IToolboxModule)Activator.CreateInstance(type);
                        Validate(module);
                        if (modules.ContainsKey(module.Id))
                            throw new InvalidOperationException(
                                "模块标识重复：" + module.Id);
                        modules.Add(module.Id, module);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(type.FullName + ": " + exception.Message);
                    }
                }
            }

            List<IToolboxModule> result =
                new List<IToolboxModule>(modules.Values);
            result.Sort(delegate(IToolboxModule left, IToolboxModule right)
            {
                int order = left.DisplayOrder.CompareTo(right.DisplayOrder);
                return order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(
                    left.DisplayName, right.DisplayName);
            });
            errors = failures;
            return result;
        }

        private static void AddAssembly(IList<Assembly> assemblies,
            Assembly candidate)
        {
            if (candidate == null)
                return;
            foreach (Assembly existing in assemblies)
            {
                if (String.Equals(existing.FullName, candidate.FullName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            assemblies.Add(candidate);
        }

        private static void Validate(IToolboxModule module)
        {
            if (module == null || String.IsNullOrWhiteSpace(module.Id) ||
                String.IsNullOrWhiteSpace(module.DisplayName) ||
                String.IsNullOrWhiteSpace(module.HomeButtonName))
            {
                throw new InvalidOperationException("模块元数据不完整。");
            }
            if (module.PreferredPaletteSize.Width <= 0 ||
                module.PreferredPaletteSize.Height <= 0)
            {
                throw new InvalidOperationException("模块窗口尺寸无效。");
            }
        }
    }
}
