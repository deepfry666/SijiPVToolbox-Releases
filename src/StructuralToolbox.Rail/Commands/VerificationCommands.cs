#if INTERNAL_VERIFY
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Core;
using StructuralToolbox.Settings;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.Commands
{
    public sealed class VerificationCommands
    {
        private sealed class ReleaseAssemblyAttestation
        {
            public Assembly Assembly { get; set; }
            public string Location { get; set; }
            public string Sha256 { get; set; }
            public string Mvid { get; set; }
        }

        [CommandMethod("ST_INTERNAL_VERIFY_RAILS", CommandFlags.Modal)]
        public void VerifyRails()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;

            Editor editor = document.Editor;
            try
            {
                editor.WriteMessage("\nSTVERIFY-PHASE COMMAND_START");
                ReleaseAssemblyAttestation release =
                    VerifyReleaseCommandSurface();
                editor.WriteMessage("\nSTVERIFY-PHASE RELEASE_ATTESTED");

                List<PanelRect> panels = ReadLargestPanelLayer(document.Database);
                if (panels.Count == 0)
                    throw new InvalidOperationException(
                        "No rectangular panels found.");
                editor.WriteMessage("\nSTVERIFY-PHASE INPUT_SCAN_COMPLETE");

                RailSettings settings = RailSettingsStore.Load();
                RailLayoutOptions options = new RailLayoutOptions();
                options.RailCount = settings.RailCount;
                options.TotalSpacing = settings.TotalSpacing;
                options.MaximumContinuityGap = settings.MaximumContinuityGap;
                options.EndOverhang = settings.EndOverhang;
                RailLayoutResult layout = RailLayoutEngine.Create(panels, options);
                if (layout.Segments.Count == 0)
                    throw new InvalidOperationException(
                        "No rail segments were generated.");

                RailDrawingStyle style = RailDrawingService.CreateStyle(settings);
                int dimensionsBefore = CountDimensions(document.Database);
                int createdRails;
                using (Transaction transaction = document.Database
                    .TransactionManager.StartTransaction())
                {
                    ObjectId layerId = RailDrawingService.GetOrCreateRailLayer(
                        document.Database, transaction, style);
                    createdRails = RailDrawingService.AppendRails(
                        document.Database, transaction, layerId, layout, style,
                        Guid.NewGuid().ToString("N"), null);
                    transaction.Commit();
                }

                int dimensionsAfter = CountDimensions(document.Database);
                if (createdRails != layout.Segments.Count)
                    throw new InvalidOperationException(
                        "The rail-only verification did not create every rail segment.");
                if (dimensionsAfter != dimensionsBefore)
                    throw new InvalidOperationException(
                        "The rail-only verification changed existing dimensions.");

                editor.WriteMessage("\nSTVERIFY-PHASE RAIL_GENERATION_COMPLETE");
                editor.WriteMessage(
                    "\nSTVERIFY-DONE RELEASE_SHA256={0} RELEASE_MVID={1} " +
                    "RELEASE_PATH=\"{2}\" MODULES=6 RAILS={3} " +
                    "DIMENSIONS_UNCHANGED=True",
                    release.Sha256, release.Mvid, release.Location, createdRails);
            }
            catch (System.Exception exception)
            {
                editor.WriteMessage("\nSTVERIFY-FAIL TYPE={0} MESSAGE={1}",
                    exception.GetType().FullName, exception.Message);
                throw;
            }
        }

        private static ReleaseAssemblyAttestation VerifyReleaseCommandSurface()
        {
            Assembly contractsAssembly = FindReleaseAssembly(
                "StructuralToolbox.Contracts");
            Assembly shellAssembly = FindReleaseAssembly("StructuralToolbox");
            Assembly railAssembly = FindReleaseAssembly("StructuralToolbox.Rail");
            Assembly ballastAssembly = FindReleaseAssembly(
                "StructuralToolbox.Ballast");
            Assembly updateAssembly = FindReleaseAssembly(
                "StructuralToolbox.Update");
            Assembly translationAssembly = FindReleaseAssembly(
                "StructuralToolbox.DrawingTranslation");
            Assembly[] modules = new Assembly[]
            {
                contractsAssembly, shellAssembly, railAssembly,
                ballastAssembly, updateAssembly, translationAssembly
            };
            foreach (Assembly module in modules)
                RequireAnnotationAbsent(module);
            string moduleDirectory = null;
            for (int index = 0; index < modules.Length; index++)
            {
                if (!Equals(modules[index].GetName().Version,
                    new Version(1, 0, 8, 0)))
                {
                    throw new InvalidOperationException(
                        modules[index].GetName().Name +
                        " version is not 1.0.8.0.");
                }
                string directory = Path.GetDirectoryName(
                    Path.GetFullPath(modules[index].Location));
                if (index == 0)
                    moduleDirectory = directory;
                else if (!String.Equals(moduleDirectory, directory,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Release module DLLs were loaded from different directories.");
                }
            }

            RequireType(contractsAssembly,
                "StructuralToolbox.Contracts.IToolboxModule");
            RequireCommand(shellAssembly,
                "StructuralToolbox.Commands.ToolboxCommands", "JJJJ");
            RequireCommand(railAssembly,
                "StructuralToolbox.Commands.RailCommands", "STPVRAIL");
            RequireCommand(ballastAssembly,
                "StructuralToolbox.Ballast.Commands.BallastCommands",
                "STPVBALLAST");
            RequireType(updateAssembly,
                "StructuralToolbox.Update.UpdateToolboxModule");
            RequireCommand(translationAssembly,
                "StructuralToolbox.DrawingTranslation.Commands.TranslationCommands",
                "STPVTRANSLATE");
            RequireType(translationAssembly,
                "StructuralToolbox.DrawingTranslation.DrawingTranslationToolboxModule");
            RequireCommand(translationAssembly,
                "StructuralToolbox.DrawingTranslation.Commands.TranslationCommands",
                "STPVTRANSLATECONFIRM");

            string location = shellAssembly.Location;
            if (String.IsNullOrWhiteSpace(location))
                throw new InvalidOperationException(
                    "Release assembly does not expose a physical location.");
            string canonicalLocation = Path.GetFullPath(location);
            if (!File.Exists(canonicalLocation))
                throw new InvalidOperationException(
                    "Loaded release assembly path does not exist: " +
                    canonicalLocation);

            return new ReleaseAssemblyAttestation
            {
                Assembly = shellAssembly,
                Location = canonicalLocation,
                Sha256 = ComputeSha256(canonicalLocation),
                Mvid = shellAssembly.ManifestModule.ModuleVersionId.ToString("D")
            };
        }

        private static Assembly FindReleaseAssembly(string assemblyName)
        {
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < loadedAssemblies.Length; index++)
            {
                if (String.Equals(loadedAssemblies[index].GetName().Name,
                    assemblyName, StringComparison.Ordinal))
                {
                    return loadedAssemblies[index];
                }
            }
            throw new InvalidOperationException(
                "Release module was not loaded before verification: " +
                assemblyName + ".dll");
        }

        private static void RequireType(Assembly assembly, string typeName)
        {
            if (assembly.GetType(typeName, false, false) == null)
                throw new InvalidOperationException("Missing release type: " + typeName);
        }

        private static List<PanelRect> ReadLargestPanelLayer(Database database)
        {
            Dictionary<string, List<PanelRect>> panelsByLayer =
                new Dictionary<string, List<PanelRect>>(
                    StringComparer.OrdinalIgnoreCase);
            using (Transaction transaction = database.TransactionManager
                .StartOpenCloseTransaction())
            {
                BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(database),
                    OpenMode.ForRead);
                foreach (ObjectId objectId in modelSpace)
                {
                    Polyline polyline = transaction.GetObject(
                        objectId, OpenMode.ForRead, false) as Polyline;
                    PanelRect panel;
                    string reason;
                    if (polyline == null || !PanelRectangleReader.TryRead(
                        polyline, out panel, out reason))
                    {
                        continue;
                    }

                    List<PanelRect> layerPanels;
                    if (!panelsByLayer.TryGetValue(polyline.Layer,
                        out layerPanels))
                    {
                        layerPanels = new List<PanelRect>();
                        panelsByLayer.Add(polyline.Layer, layerPanels);
                    }
                    layerPanels.Add(panel);
                }
                transaction.Commit();
            }

            List<PanelRect> largest = new List<PanelRect>();
            foreach (KeyValuePair<string, List<PanelRect>> pair in panelsByLayer)
            {
                if (pair.Value.Count > largest.Count)
                    largest = pair.Value;
            }
            return largest;
        }

        private static int CountDimensions(Database database)
        {
            int count = 0;
            using (Transaction transaction = database.TransactionManager
                .StartOpenCloseTransaction())
            {
                BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(database),
                    OpenMode.ForRead);
                foreach (ObjectId objectId in modelSpace)
                {
                    if (transaction.GetObject(objectId, OpenMode.ForRead,
                        false) is Dimension)
                    {
                        count++;
                    }
                }
                transaction.Commit();
            }
            return count;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(
                    sha256.ComputeHash(stream)).Replace("-", String.Empty);
            }
        }

        private static void RequireCommand(Assembly assembly,
            string typeName, string commandName)
        {
            Type commandType = assembly.GetType(typeName, false, false);
            if (commandType == null)
                throw new InvalidOperationException("Missing command class: " + typeName);
            MethodInfo[] methods = commandType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                object[] attributes = methods[methodIndex].GetCustomAttributes(
                    typeof(CommandMethodAttribute), false);
                for (int attributeIndex = 0;
                    attributeIndex < attributes.Length; attributeIndex++)
                {
                    CommandMethodAttribute commandAttribute =
                        (CommandMethodAttribute)attributes[attributeIndex];
                    if (String.Equals(commandAttribute.GlobalName,
                        commandName, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }
            throw new InvalidOperationException(
                "Missing command registration: " + commandName);
        }

        private static void RequireAnnotationAbsent(Assembly assembly)
        {
            foreach (string resource in assembly.GetManifestResourceNames())
                if (resource.IndexOf("GDBZ", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException("Retired annotation resource: " + resource);
            foreach (Type type in assembly.GetTypes())
            {
                if (type.Name.StartsWith("RailDimension", StringComparison.Ordinal) ||
                    type.Name.StartsWith("RailAnnotation", StringComparison.Ordinal))
                    throw new InvalidOperationException("Retired annotation type: " + type.FullName);
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance |
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    foreach (CommandMethodAttribute attribute in method.GetCustomAttributes(
                        typeof(CommandMethodAttribute), false))
                        if (attribute.GlobalName == "STPVRAILDIM" ||
                            attribute.GlobalName == "STPVRAILANNOTATE" || attribute.GlobalName == "GDBZ")
                            throw new InvalidOperationException("Retired annotation command: " + attribute.GlobalName);
            }
        }
    }
}
#endif
