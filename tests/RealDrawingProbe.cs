using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Core;
using StructuralToolbox.Settings;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(RealDrawingProbe.Commands))]

namespace RealDrawingProbe
{
    public sealed class Commands
    {
        private const string ExecutionAssemblyName =
            "FourJTools.RealDrawingVerify";
        private const string VerificationMarkerTypeName =
            "FourJTools.Verification.RealDrawingVerificationMarker";
        private const string VerificationMarkerValue =
            "FOURJTOOLS_INTERNAL_REAL_DRAWING_VERIFY_V1";
        private static Document _armedDocument;
        private static string _outcomePath;
        private static bool _outcomeWritten;

        private sealed class LayerStats
        {
            public LayerStats(string name)
            {
                Name = name;
                Minimum = new Point3d(Double.MaxValue, Double.MaxValue, 0.0);
                Maximum = new Point3d(Double.MinValue, Double.MinValue, 0.0);
                Panels = new List<PanelRect>();
            }

            public string Name { get; private set; }
            public int Count { get; set; }
            public string SampleHandle { get; set; }
            public Point3d Minimum { get; private set; }
            public Point3d Maximum { get; private set; }
            public List<PanelRect> Panels { get; private set; }

            public void Include(Extents3d extents)
            {
                Minimum = new Point3d(
                    Math.Min(Minimum.X, extents.MinPoint.X),
                    Math.Min(Minimum.Y, extents.MinPoint.Y), 0.0);
                Maximum = new Point3d(
                    Math.Max(Maximum.X, extents.MaxPoint.X),
                    Math.Max(Maximum.Y, extents.MaxPoint.Y), 0.0);
            }
        }

        [CommandMethod("ST_REAL_SCAN", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void Scan()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;

            string outputPath = PromptOutputPath(document, "Output JSON path: ");
            if (outputPath == null)
            {
                document.Editor.WriteMessage("\nST_REAL_SCAN_CANCELLED");
                return;
            }

            WriteJson(outputPath, Inspect(document.Database));
            document.Editor.WriteMessage("\nST_REAL_SCAN_DONE PATH={0}", outputPath);
        }

        [CommandMethod("ST_REAL_ARM", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void ArmCommandObservation()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;

            string outputPath = PromptOutputPath(document,
                "Command outcome JSON path: ");
            if (outputPath == null)
            {
                document.Editor.WriteMessage("\nST_REAL_ARM_CANCELLED");
                return;
            }

            Disarm();
            _armedDocument = document;
            _outcomePath = outputPath;
            _outcomeWritten = false;
            document.CommandEnded += OnCommandEnded;
            document.CommandCancelled += OnCommandCancelled;
            document.CommandFailed += OnCommandFailed;
            document.Editor.WriteMessage("\nST_REAL_ARMED PATH={0}", outputPath);
        }

        private static string PromptOutputPath(Document document, string message)
        {
            Autodesk.AutoCAD.EditorInput.PromptStringOptions options =
                new Autodesk.AutoCAD.EditorInput.PromptStringOptions("\n" + message);
            options.AllowSpaces = true;
            Autodesk.AutoCAD.EditorInput.PromptResult result =
                document.Editor.GetString(options);
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK ||
                String.IsNullOrWhiteSpace(result.StringResult))
            {
                return null;
            }
            return Path.GetFullPath(result.StringResult.Trim());
        }

        private static void OnCommandEnded(object sender, CommandEventArgs eventArgs)
        {
            CompleteObservation("ENDED", eventArgs);
        }

        private static void OnCommandCancelled(object sender,
            CommandEventArgs eventArgs)
        {
            CompleteObservation("CANCELLED", eventArgs);
        }

        private static void OnCommandFailed(object sender, CommandEventArgs eventArgs)
        {
            CompleteObservation("FAILED", eventArgs);
        }

        private static void CompleteObservation(string status,
            CommandEventArgs eventArgs)
        {
            if (_outcomeWritten || eventArgs == null ||
                !String.Equals(eventArgs.GlobalCommandName, "STPVRAIL",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _outcomeWritten = true;
            Document document = _armedDocument;
            string outputPath = _outcomePath;
            try
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["schemaVersion"] = 1;
                result["generatedUtc"] = DateTime.UtcNow.ToString(
                    "o", CultureInfo.InvariantCulture);
                result["status"] = status;
                result["globalCommandName"] = eventArgs.GlobalCommandName;
                result["timing"] = StructuralToolbox.Commands.
                    InternalRealDrawingTiming.CreateSnapshot();
                result["snapshot"] = document == null
                    ? null
                    : Inspect(document.Database);
                WriteJson(outputPath, result);
            }
            catch (System.Exception exception)
            {
                Dictionary<string, object> failure = new Dictionary<string, object>();
                failure["schemaVersion"] = 1;
                failure["generatedUtc"] = DateTime.UtcNow.ToString(
                    "o", CultureInfo.InvariantCulture);
                failure["status"] = "PROBE_FAILED";
                failure["globalCommandName"] = eventArgs.GlobalCommandName;
                failure["error"] = exception.ToString();
                try
                {
                    WriteJson(outputPath, failure);
                }
                catch
                {
                }
            }
            finally
            {
                Disarm();
            }
        }

        private static void Disarm()
        {
            Document document = _armedDocument;
            _armedDocument = null;
            _outcomePath = null;
            if (document == null)
                return;
            document.CommandEnded -= OnCommandEnded;
            document.CommandCancelled -= OnCommandCancelled;
            document.CommandFailed -= OnCommandFailed;
        }

        private static Dictionary<string, object> Inspect(Database database)
        {
            Dictionary<string, LayerStats> panelLayers =
                new Dictionary<string, LayerStats>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> toolKinds =
                new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> toolVersions =
                new Dictionary<string, int>(StringComparer.Ordinal);
            HashSet<string> toolRuns = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> outputLayerEntities =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<double> railLengths = new List<double>();
            List<double> railWidths = new List<double>();
            List<string> railColorMethods = new List<string>();
            List<int> railColorIndexes = new List<int>();
            string nativeDimensionHandle = null;
            int nativeDimensionCount = 0;
            int entityCount = 0;
            int invalidToolXDataCount = 0;
            int metadataV3Count = 0;
            MethodInfo rectangleReader = ResolveRectangleReader();
            RailSettings settings = RailSettingsStore.Load();

            using (Transaction transaction =
                database.TransactionManager.StartOpenCloseTransaction())
            {
                BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(database),
                    OpenMode.ForRead);
                foreach (ObjectId id in modelSpace)
                {
                    Entity entity = transaction.GetObject(
                        id, OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                        continue;
                    entityCount++;

                    Polyline polyline = entity as Polyline;
                    PanelRect panel;
                    if (polyline != null && TryReadRectangle(
                        rectangleReader, polyline, out panel))
                    {
                        LayerStats stats;
                        if (!panelLayers.TryGetValue(polyline.Layer, out stats))
                        {
                            stats = new LayerStats(polyline.Layer);
                            panelLayers.Add(polyline.Layer, stats);
                        }
                        stats.Count++;
                        stats.Panels.Add(panel);
                        if (stats.SampleHandle == null)
                            stats.SampleHandle = polyline.Handle.ToString();
                        try
                        {
                            stats.Include(polyline.GeometricExtents);
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception)
                        {
                        }
                    }

                    if (entity is Dimension)
                    {
                        nativeDimensionCount++;
                        if (nativeDimensionHandle == null)
                            nativeDimensionHandle = entity.Handle.ToString();
                    }

                    string kind;
                    string version;
                    string runId;
                    bool hasToolXData;
                    Dictionary<string, object> toolMetadata;
                    if (TryReadToolXData(entity, out kind, out version, out runId,
                        out hasToolXData, out toolMetadata))
                    {
                        Increment(toolKinds, kind);
                        Increment(toolVersions, version);
                        toolRuns.Add(runId);
                        if (toolMetadata != null)
                            metadataV3Count++;
                        if (String.Equals(kind, "RAIL", StringComparison.Ordinal))
                        {
                            if (polyline != null)
                            {
                                railLengths.Add(polyline.Length);
                                railWidths.Add(polyline.ConstantWidth);
                                railColorMethods.Add(polyline.Color.ColorMethod.ToString());
                                railColorIndexes.Add(polyline.Color.ColorIndex);
                            }
                        }
                    }
                    else if (hasToolXData)
                    {
                        invalidToolXDataCount++;
                    }

                    if (String.Equals(entity.Layer, settings.RailLayerName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Increment(outputLayerEntities, entity.Layer);
                    }
                }

                List<LayerStats> orderedLayers = panelLayers.Values
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                LayerStats largest = orderedLayers.Count == 0
                    ? null
                    : orderedLayers[0];

                Dictionary<string, object> report = new Dictionary<string, object>();
                report["schemaVersion"] = 2;
                report["generatedUtc"] = DateTime.UtcNow.ToString(
                    "o", CultureInfo.InvariantCulture);
                report["database"] = new Dictionary<string, object>
                {
                    { "filename", database.Filename },
                    { "insunits", database.Insunits.ToString() },
                    { "entityCount", entityCount },
                    { "extmin", Point(database.Extmin) },
                    { "extmax", Point(database.Extmax) }
                };
                report["executionAssembly"] = ExecutionAssemblyIdentity();
                report["settings"] = Settings(settings);
                report["largestPanelLayer"] = largest == null
                    ? null
                    : Layer(largest);
                report["panelLayers"] = orderedLayers.Take(30)
                    .Select(Layer).ToList();
                report["plan"] = largest == null
                    ? null
                    : Plan(largest.Panels, settings);
                report["nativeDimensions"] = new Dictionary<string, object>
                {
                    { "count", nativeDimensionCount },
                    { "sampleHandle", nativeDimensionHandle }
                };
                int toolEntityCount = toolKinds.Values.Sum();
                report["toolEntities"] = new Dictionary<string, object>
                {
                    { "count", toolEntityCount },
                    { "kinds", toolKinds },
                    { "versions", toolVersions },
                    { "runCount", toolRuns.Count },
                    { "invalidXDataCount", invalidToolXDataCount },
                    { "metadataV3Count", metadataV3Count },
                    { "allMetadataV3", toolEntityCount > 0 &&
                        metadataV3Count == toolEntityCount &&
                        invalidToolXDataCount == 0 },
                    { "outputLayerEntities", outputLayerEntities }
                };
                report["configuredLayers"] = new Dictionary<string, object>
                {
                    { "rail", LayerRecord(database, transaction,
                        settings.RailLayerName) }
                };
                report["rails"] = NumericSummary(railLengths, railWidths,
                    railColorMethods, railColorIndexes);
                transaction.Commit();
                return report;
            }
        }

        private static MethodInfo ResolveRectangleReader()
        {
            Assembly executionAssembly = ResolveExecutionAssembly();
            Type type = executionAssembly.GetType(
                "StructuralToolbox.Commands.PanelRectangleReader", true);
            MethodInfo method = type.GetMethod("TryRead",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("PanelRectangleReader.TryRead missing.");
            return method;
        }

        private static Assembly ResolveExecutionAssembly()
        {
            bool formalCandidateLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(item => String.Equals(item.GetName().Name,
                    "StructuralToolbox", StringComparison.OrdinalIgnoreCase));
            if (formalCandidateLoaded)
            {
                throw new InvalidOperationException(
                    "A formal StructuralToolbox assembly is already loaded. " +
                    "The isolated internal verifier refuses ambiguous command registration.");
            }

            Assembly executionAssembly = typeof(RailSettings).Assembly;
            if (!String.Equals(executionAssembly.GetName().Name,
                    ExecutionAssemblyName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The real-drawing probe is not running from the isolated " +
                    "internal verification assembly.");
            }

            Type markerType = executionAssembly.GetType(
                VerificationMarkerTypeName, false);
            FieldInfo markerField = markerType == null
                ? null
                : markerType.GetField("Value", BindingFlags.Public |
                    BindingFlags.Static);
            string marker = markerField == null
                ? null
                : markerField.GetValue(null) as string;
            if (!String.Equals(marker, VerificationMarkerValue,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The internal real-drawing verification marker is missing.");
            }
            return executionAssembly;
        }

        private static bool TryReadRectangle(MethodInfo reader, Polyline polyline,
            out PanelRect panel)
        {
            panel = null;
            try
            {
                object[] arguments = { polyline, null, null };
                bool valid = (bool)reader.Invoke(null, arguments);
                panel = arguments[1] as PanelRect;
                return valid && panel != null;
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, object> ExecutionAssemblyIdentity()
        {
            Assembly executionAssembly = ResolveExecutionAssembly();
            return new Dictionary<string, object>
            {
                { "name", executionAssembly.GetName().Name },
                { "assemblyVersion",
                    executionAssembly.GetName().Version.ToString() },
                { "location", executionAssembly.Location },
                { "mvid",
                    executionAssembly.ManifestModule.ModuleVersionId.ToString() },
                { "sha256", FileSha256(executionAssembly.Location) },
                { "verificationMarker", VerificationMarkerValue }
            };
        }

        private static string FileSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", String.Empty);
            }
        }

        private static Dictionary<string, object> Settings(RailSettings settings)
        {
            return new Dictionary<string, object>
            {
                { "path", RailSettingsStore.SettingsPath },
                { "railCount", settings.RailCount },
                { "totalSpacing", settings.TotalSpacing },
                { "maximumContinuityGap", settings.MaximumContinuityGap },
                { "endOverhang", settings.EndOverhang },
                { "railLayerName", settings.RailLayerName },
                { "railGlobalWidth", settings.RailGlobalWidth },
                { "railColorApplication", settings.RailColorApplication.ToString() },
                { "railColorKind", settings.RailColorKind.ToString() },
                { "railColorIndex", settings.RailColorIndex }
            };
        }

        private static Dictionary<string, object> Plan(IList<PanelRect> panels,
            RailSettings settings)
        {
            RailLayoutOptions layoutOptions = new RailLayoutOptions();
            layoutOptions.RailCount = settings.RailCount;
            layoutOptions.TotalSpacing = settings.TotalSpacing;
            layoutOptions.MaximumContinuityGap = settings.MaximumContinuityGap;
            layoutOptions.EndOverhang = settings.EndOverhang;
            RailLayoutResult layout = RailLayoutEngine.Create(
                panels, layoutOptions);
            return new Dictionary<string, object>
            {
                { "recognizedPanelCount", panels.Count },
                { "panelGroupCount", layout.PanelGroupCount },
                { "arrayIslandCount", layout.ArrayIslands.Count },
                { "componentRowCount", layout.ComponentRows.Count },
                { "railSegmentCount", layout.Segments.Count }
            };
        }

        private static bool TryReadToolXData(Entity entity, out string kind,
            out string version, out string runId, out bool hasToolXData,
            out Dictionary<string, object> metadata)
        {
            kind = String.Empty;
            version = String.Empty;
            runId = String.Empty;
            hasToolXData = false;
            metadata = null;
            using (ResultBuffer buffer =
                entity.GetXDataForApplication("FOURJTOOLS"))
            {
                if (buffer == null)
                    return false;
                hasToolXData = true;
                TypedValue[] values = buffer.AsArray();
                if (values.Length < 4)
                    return false;
                kind = Convert.ToString(values[1].Value,
                    CultureInfo.InvariantCulture);
                version = Convert.ToString(values[2].Value,
                    CultureInfo.InvariantCulture);
                runId = Convert.ToString(values[3].Value,
                    CultureInfo.InvariantCulture);
                if (values.Length > 4 &&
                    String.Equals(Convert.ToString(values[4].Value,
                        CultureInfo.InvariantCulture), "META_V3",
                        StringComparison.Ordinal))
                {
                    metadata = new Dictionary<string, object>(
                        StringComparer.Ordinal);
                    for (int index = 5; index + 1 < values.Length; index += 2)
                    {
                        string key = Convert.ToString(values[index].Value,
                            CultureInfo.InvariantCulture);
                        if (!String.IsNullOrWhiteSpace(key))
                            metadata[key] = values[index + 1].Value;
                    }
                }
                return !String.IsNullOrWhiteSpace(kind) &&
                    !String.IsNullOrWhiteSpace(runId);
            }
        }

        private static Dictionary<string, object> Layer(LayerStats stats)
        {
            return new Dictionary<string, object>
            {
                { "name", stats.Name },
                { "count", stats.Count },
                { "sampleHandle", stats.SampleHandle },
                { "minimum", Point(stats.Minimum) },
                { "maximum", Point(stats.Maximum) }
            };
        }

        private static Dictionary<string, object> LayerRecord(Database database,
            Transaction transaction, string layerName)
        {
            LayerTable layers = (LayerTable)transaction.GetObject(
                database.LayerTableId, OpenMode.ForRead);
            if (!layers.Has(layerName))
                return new Dictionary<string, object> { { "exists", false } };
            LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(
                layers[layerName], OpenMode.ForRead);
            return new Dictionary<string, object>
            {
                { "exists", true },
                { "name", layer.Name },
                { "colorMethod", layer.Color.ColorMethod.ToString() },
                { "colorIndex", layer.Color.ColorIndex }
            };
        }

        private static Dictionary<string, object> Point(Point3d point)
        {
            return new Dictionary<string, object>
            {
                { "x", point.X },
                { "y", point.Y },
                { "z", point.Z }
            };
        }

        private static Dictionary<string, object> NumericSummary(
            IList<double> lengths, IList<double> widths,
            IList<string> colorMethods, IList<int> colorIndexes)
        {
            return new Dictionary<string, object>
            {
                { "count", lengths.Count },
                { "lengthMin", lengths.Count == 0 ? (object)null : lengths.Min() },
                { "lengthMax", lengths.Count == 0 ? (object)null : lengths.Max() },
                { "globalWidths", widths.Distinct().OrderBy(item => item).ToList() },
                { "colorMethods", colorMethods.Distinct().OrderBy(item => item).ToList() },
                { "colorIndexes", colorIndexes.Distinct().OrderBy(item => item).ToList() }
            };
        }

        private static void Increment(IDictionary<string, int> values, string key)
        {
            int count;
            values.TryGetValue(key ?? String.Empty, out count);
            values[key ?? String.Empty] = count + 1;
        }

        private static void WriteJson(string outputPath,
            Dictionary<string, object> value)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (String.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Output directory is missing.");
            Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath,
                new JavaScriptSerializer().Serialize(value),
                new System.Text.UTF8Encoding(false));
        }
    }
}

#if INTERNAL_VERIFY
namespace StructuralToolbox.Commands
{
    internal static class InternalRealDrawingTiming
    {
        private static readonly object SyncRoot = new object();
        private static DateTime _commandStartedUtc;
        private static long _commandStartTimestamp;
        private static long _commandElapsedTicks;
        private static long _planningStartTimestamp;
        private static long _planningElapsedTicks;
        private static bool _commandActive;
        private static bool _planningActive;

        internal static void BeginCommand()
        {
            lock (SyncRoot)
            {
                _commandStartedUtc = DateTime.UtcNow;
                _commandStartTimestamp =
                    System.Diagnostics.Stopwatch.GetTimestamp();
                _commandElapsedTicks = 0L;
                _planningStartTimestamp = 0L;
                _planningElapsedTicks = 0L;
                _commandActive = true;
                _planningActive = false;
            }
        }

        internal static void BeginPlanning()
        {
            lock (SyncRoot)
            {
                if (!_commandActive || _planningActive)
                    return;
                _planningStartTimestamp =
                    System.Diagnostics.Stopwatch.GetTimestamp();
                _planningActive = true;
            }
        }

        internal static void EndPlanning()
        {
            lock (SyncRoot)
            {
                EndPlanningLocked(System.Diagnostics.Stopwatch.GetTimestamp());
            }
        }

        internal static void EndCommand()
        {
            lock (SyncRoot)
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                EndPlanningLocked(now);
                if (_commandActive)
                    _commandElapsedTicks = Math.Max(
                        0L, now - _commandStartTimestamp);
                _commandActive = false;
            }
        }

        internal static Dictionary<string, object> CreateSnapshot()
        {
            lock (SyncRoot)
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                long commandTicks = _commandActive
                    ? Math.Max(0L, now - _commandStartTimestamp)
                    : _commandElapsedTicks;
                long planningTicks = _planningElapsedTicks;
                if (_planningActive)
                    planningTicks += Math.Max(
                        0L, now - _planningStartTimestamp);
                return new Dictionary<string, object>
                {
                    {
                        "commandStartedUtc",
                        _commandStartedUtc == DateTime.MinValue
                            ? null
                            : (object)_commandStartedUtc.ToString(
                                "o", CultureInfo.InvariantCulture)
                    },
                    { "commandActive", _commandActive },
                    { "planningActive", _planningActive },
                    { "commandElapsedMilliseconds", ToMilliseconds(commandTicks) },
                    { "planningElapsedMilliseconds", ToMilliseconds(planningTicks) }
                };
            }
        }

        private static void EndPlanningLocked(long now)
        {
            if (!_planningActive)
                return;
            _planningElapsedTicks += Math.Max(
                0L, now - _planningStartTimestamp);
            _planningActive = false;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
#endif
