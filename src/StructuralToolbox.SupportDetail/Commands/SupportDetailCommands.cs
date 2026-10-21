using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.SupportDetail.Core;
using StructuralToolbox.SupportDetail.Settings;
using StructuralToolbox.SupportDetail.UI;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.SupportDetail.Commands
{
    public sealed class SupportDetailCommands
    {
        [CommandMethod("STPVSUPPORTDETAIL", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void CreateSupportDetail()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            try
            {
                SupportDetailSettings settings =
                    SupportDetailSettingsStore.Load();
                using (SupportDetailDialog dialog =
                    new SupportDetailDialog(settings))
                {
                    if (Autodesk.AutoCAD.ApplicationServices.Application.
                        ShowModalDialog(dialog) !=
                        System.Windows.Forms.DialogResult.OK)
                        return;
                    settings = dialog.ResultSettings;
                }

                SupportDetailBuildResult build =
                    SupportDetailGeometryBuilder.Build(settings);
                if (!build.IsValid)
                {
                    AcApplication.ShowAlertDialog(
                        "无法生成支架大样：\n" + String.Join("\n", build.Errors));
                    return;
                }
                if (!ConfirmWarnings(editor, build.Warnings))
                    return;

                PromptPointResult insertion = editor.GetPoint(
                    "\n指定支架大样基点（组件阵列低端的水平投影）: ");
                if (insertion.Status != PromptStatus.OK)
                    return;

                string instanceId = "SD" + Guid.NewGuid().ToString("N");
                SupportDetailDrawSummary summary;
                using (Transaction transaction =
                    document.Database.TransactionManager.StartTransaction())
                {
                    ObjectId layerId = SupportDetailDrawingService.EnsureLayer(
                        document.Database, transaction, settings.OutputLayer);
                    summary = SupportDetailDrawingService.AppendInstance(
                        document.Database, transaction, layerId, build.Geometry,
                        insertion.Value, instanceId, ResolveFixedLibraryPath());
                    transaction.Commit();
                }
                SupportDetailSettingsStore.Save(settings);
                editor.Regen();
                WriteSummary(editor, "已生成", instanceId, summary, 0);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                AcApplication.ShowAlertDialog(
                    "生成支架大样失败：" + exception.Message);
            }
            catch (System.Exception exception)
            {
                AcApplication.ShowAlertDialog(
                    "生成支架大样失败：" + exception.Message);
            }
        }

        [CommandMethod("STPVSUPPORTDETAILEDIT", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void EditSupportDetail()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            try
            {
                string role;
                SupportDetailInstanceData instance = PromptInstance(document,
                    "\n请选择要编辑的支架大样任意构件: ", out role);
                if (instance == null)
                    return;

                SupportDetailSettings settings;
                using (SupportDetailDialog dialog =
                    new SupportDetailDialog(instance.Settings))
                {
                    if (Autodesk.AutoCAD.ApplicationServices.Application.
                        ShowModalDialog(dialog) !=
                        System.Windows.Forms.DialogResult.OK)
                        return;
                    settings = dialog.ResultSettings;
                }
                SupportDetailBuildResult build =
                    SupportDetailGeometryBuilder.Build(settings);
                if (!build.IsValid)
                {
                    AcApplication.ShowAlertDialog(
                        "无法更新支架大样：\n" + String.Join("\n", build.Errors));
                    return;
                }
                if (!ConfirmWarnings(editor, build.Warnings))
                    return;

                int erased;
                SupportDetailDrawSummary summary;
                using (Transaction transaction =
                    document.Database.TransactionManager.StartTransaction())
                {
                    erased = SupportDetailDrawingService.EraseInstanceEntities(
                        document.Database, transaction, instance.InstanceId);
                    ObjectId layerId = SupportDetailDrawingService.EnsureLayer(
                        document.Database, transaction, settings.OutputLayer);
                    summary = SupportDetailDrawingService.AppendInstance(
                        document.Database, transaction, layerId, build.Geometry,
                        instance.Origin, instance.InstanceId,
                        ResolveFixedLibraryPath());
                    transaction.Commit();
                }
                SupportDetailSettingsStore.Save(settings);
                editor.Regen();
                WriteSummary(editor, "已更新", instance.InstanceId, summary,
                    erased);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                AcApplication.ShowAlertDialog(
                    "更新支架大样失败：" + exception.Message);
            }
            catch (System.Exception exception)
            {
                AcApplication.ShowAlertDialog(
                    "更新支架大样失败：" + exception.Message);
            }
        }

        [CommandMethod("STPVSUPPORTDETAILINFO", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void InspectSupportDetail()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            try
            {
                string role;
                SupportDetailInstanceData instance = PromptInstance(document,
                    "\n请选择支架大样任意构件: ", out role);
                if (instance == null)
                    return;
                int entityCount;
                using (Transaction transaction =
                    document.Database.TransactionManager.
                        StartOpenCloseTransaction())
                {
                    entityCount = SupportDetailDrawingService.FindInstanceEntities(
                        document.Database, transaction, instance.InstanceId).Count;
                    transaction.Commit();
                }
                SupportDetailSettings settings = instance.Settings;
                document.Editor.WriteMessage(
                    "\n支架实例：{0}；所选构件：{1}；关联实体：{2}。",
                    instance.InstanceId, role, entityCount);
                document.Editor.WriteMessage(
                    "\n组件 N={0}，L={1:0.###}，G={2:0.###}；倾角 α={3:0.###}°。",
                    settings.ComponentCount, settings.ComponentLength,
                    settings.ComponentGap, settings.SlopeDegrees);
                document.Editor.WriteMessage(
                    "\n墩子 P={0}，基准立柱 H={1:0.###}，斜梁伸出 E={2:0.###}。",
                    settings.PierCount, settings.ColumnHeight,
                    settings.BeamOverhang);
                document.Editor.WriteMessage(
                    "\n输入 STPVSUPPORTDETAILEDIT 可回读并修改整组参数。 ");
            }
            catch (System.Exception exception)
            {
                AcApplication.ShowAlertDialog(
                    "读取支架大样失败：" + exception.Message);
            }
        }

        [CommandMethod("STPVSUPPORTDETAILDELETE", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void DeleteSupportDetail()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            try
            {
                string role;
                SupportDetailInstanceData instance = PromptInstance(document,
                    "\n请选择要删除的支架大样任意构件: ", out role);
                if (instance == null)
                    return;
                PromptKeywordOptions options = new PromptKeywordOptions(
                    "\n将删除该实例的全部工具实体 [删除(D)/取消(X)] <取消>: ");
                options.AllowNone = true;
                options.Keywords.Add("D");
                options.Keywords.Add("X");
                options.Keywords.Default = "X";
                PromptResult result = editor.GetKeywords(options);
                if (result.Status != PromptStatus.OK ||
                    !String.Equals(result.StringResult, "D",
                        StringComparison.OrdinalIgnoreCase))
                    return;

                int erased;
                using (Transaction transaction =
                    document.Database.TransactionManager.StartTransaction())
                {
                    erased = SupportDetailDrawingService.EraseInstanceEntities(
                        document.Database, transaction, instance.InstanceId);
                    SupportDetailDrawingService.EraseInstanceRecord(
                        document.Database, transaction, instance.InstanceId);
                    transaction.Commit();
                }
                editor.Regen();
                editor.WriteMessage("\n已删除支架实例 {0}，共 {1} 个实体。",
                    instance.InstanceId, erased);
            }
            catch (System.Exception exception)
            {
                AcApplication.ShowAlertDialog(
                    "删除支架大样失败：" + exception.Message);
            }
        }

        [CommandMethod("STPVSUPPORTDETAILCHECK", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void CheckSupportDetailRoundTrip()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            string instanceId = "SDCHECK" + Guid.NewGuid().ToString("N");
            try
            {
                SupportDetailSettings settings = new SupportDetailSettings();
                settings.ComponentCount = 3;
                settings.SlopeDegrees = 12.5;
                settings.PierPositionMode = SupportDetailPositionMode.Manual;
                settings.PierStations = "200, 1500, 3000, 5000";
                settings.PierCountFollowsComponents = false;
                settings.PierCount = 4;
                settings.IncludeNodeDetails = true;
                settings.IncludeMaterialTable = true;
                SupportDetailBuildResult firstBuild =
                    SupportDetailGeometryBuilder.Build(settings);
                if (!firstBuild.IsValid)
                    throw new InvalidOperationException(
                        String.Join("; ", firstBuild.Errors));

                int firstEntityCount;
                int secondEntityCount;
                using (Transaction transaction =
                    document.Database.TransactionManager.StartTransaction())
                {
                    ObjectId layerId = SupportDetailDrawingService.EnsureLayer(
                        document.Database, transaction, settings.OutputLayer);
                    SupportDetailDrawSummary first =
                        SupportDetailDrawingService.AppendInstance(
                            document.Database, transaction, layerId,
                            firstBuild.Geometry, Point3d.Origin, instanceId,
                            ResolveFixedLibraryPath());
                    firstEntityCount = first.EntityCount;
                    SupportDetailInstanceData read =
                        SupportDetailDrawingService.ReadInstanceRecord(
                            document.Database, transaction, instanceId);
                    if (read.Settings.ComponentCount != 3 ||
                        Math.Abs(read.Settings.SlopeDegrees - 12.5) > 0.0001)
                    {
                        throw new InvalidOperationException(
                            "参数回读与写入值不一致。");
                    }
                    SupportDetailDrawingService.EraseInstanceEntities(
                        document.Database, transaction, instanceId);
                    read.Settings.ComponentCount = 4;
                    SupportDetailBuildResult secondBuild =
                        SupportDetailGeometryBuilder.Build(read.Settings);
                    SupportDetailDrawSummary second =
                        SupportDetailDrawingService.AppendInstance(
                            document.Database, transaction, layerId,
                            secondBuild.Geometry, read.Origin, instanceId,
                            ResolveFixedLibraryPath());
                    secondEntityCount = second.EntityCount;
                    SupportDetailInstanceData reread =
                        SupportDetailDrawingService.ReadInstanceRecord(
                            document.Database, transaction, instanceId);
                    if (reread.Settings.ComponentCount != 4)
                        throw new InvalidOperationException("实例更新后回读失败。");
                    int liveCount =
                        SupportDetailDrawingService.FindInstanceEntities(
                            document.Database, transaction, instanceId).Count;
                    if (liveCount != second.EntityCount)
                    {
                        throw new InvalidOperationException(
                            "实例实体计数与元数据关联结果不一致。");
                    }
                    transaction.Commit();
                }
                editor.Regen();
                editor.WriteMessage(
                    "\nSUPPORT_DETAIL_CHECK=PASS INSTANCE={0} FIRST={1} SECOND={2}",
                    instanceId, firstEntityCount, secondEntityCount);
            }
            catch (System.Exception exception)
            {
                editor.WriteMessage(
                    "\nSUPPORT_DETAIL_CHECK=FAIL INSTANCE={0} MESSAGE={1}",
                    instanceId, exception.Message);
                throw;
            }
        }

        [CommandMethod("STPVSUPPORTDETAILREOPENCHECK", CommandFlags.Modal |
            CommandFlags.NoPaperSpace)]
        public void CheckReopenedSupportDetail()
        {
            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;
            Editor editor = document.Editor;
            try
            {
                string instanceId = null;
                int entityCount = 0;
                int nativeMainEntityCount = 0;
                using (Transaction transaction = document.Database.
                    TransactionManager.StartOpenCloseTransaction())
                {
                    BlockTableRecord modelSpace =
                        (BlockTableRecord)transaction.GetObject(
                            SymbolUtilityServices.GetBlockModelSpaceId(
                                document.Database), OpenMode.ForRead);
                    foreach (ObjectId objectId in modelSpace)
                    {
                        Entity entity = transaction.GetObject(objectId,
                            OpenMode.ForRead, false) as Entity;
                        string candidateId;
                        string role;
                        if (!SupportDetailDrawingService.TryReadEntityInstance(
                            entity, out candidateId, out role) ||
                            !candidateId.StartsWith("SDCHECK",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (instanceId == null)
                            instanceId = candidateId;
                        if (!String.Equals(instanceId, candidateId,
                            StringComparison.OrdinalIgnoreCase))
                            continue;
                        entityCount++;
                        if ((role.StartsWith("BEAM",
                                StringComparison.OrdinalIgnoreCase) ||
                            role.StartsWith("PANEL",
                                StringComparison.OrdinalIgnoreCase) ||
                            role.StartsWith("PIER_",
                                StringComparison.OrdinalIgnoreCase) ||
                            role.StartsWith("COLUMN_",
                                StringComparison.OrdinalIgnoreCase)) &&
                            !(entity is BlockReference))
                        {
                            nativeMainEntityCount++;
                        }
                    }
                    if (String.IsNullOrWhiteSpace(instanceId))
                        throw new InvalidOperationException(
                            "保存后的 DWG 中找不到检查实例。");
                    SupportDetailInstanceData record =
                        SupportDetailDrawingService.ReadInstanceRecord(
                            document.Database, transaction, instanceId);
                    if (record.Settings.ComponentCount != 4)
                        throw new InvalidOperationException(
                            "保存后参数记录未保留更新值。");
                    if (entityCount < 40 || nativeMainEntityCount < 10)
                        throw new InvalidOperationException(
                            "保存后关联实体不完整，或主构件被锁成整体块。");
                    transaction.Commit();
                }
                editor.WriteMessage(
                    "\nSUPPORT_DETAIL_REOPEN=PASS INSTANCE={0} " +
                    "ENTITIES={1} NATIVE_MAIN={2}",
                    instanceId, entityCount, nativeMainEntityCount);
            }
            catch (System.Exception exception)
            {
                editor.WriteMessage(
                    "\nSUPPORT_DETAIL_REOPEN=FAIL MESSAGE={0}",
                    exception.Message);
                throw;
            }
        }

        private static SupportDetailInstanceData PromptInstance(
            Document document, string message, out string role)
        {
            role = null;
            PromptEntityOptions options = new PromptEntityOptions(message);
            PromptEntityResult selected = document.Editor.GetEntity(options);
            if (selected.Status != PromptStatus.OK)
                return null;
            string instanceId;
            using (Transaction transaction =
                document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                Entity entity = transaction.GetObject(selected.ObjectId,
                    OpenMode.ForRead, false) as Entity;
                if (!SupportDetailDrawingService.TryReadEntityInstance(entity,
                    out instanceId, out role))
                {
                    AcApplication.ShowAlertDialog(
                        "所选对象不是本模块生成的支架大样构件。");
                    return null;
                }
                SupportDetailInstanceData instance =
                    SupportDetailDrawingService.ReadInstanceRecord(
                        document.Database, transaction, instanceId);
                transaction.Commit();
                return instance;
            }
        }

        private static bool ConfirmWarnings(Editor editor,
            System.Collections.Generic.IList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
                return true;
            editor.WriteMessage("\n支架几何提示：");
            for (int index = 0; index < warnings.Count; index++)
                editor.WriteMessage("\n- " + warnings[index]);
            PromptKeywordOptions options = new PromptKeywordOptions(
                "\n是否继续生成 [继续(C)/返回修改(X)] <返回修改>: ");
            options.AllowNone = true;
            options.Keywords.Add("C");
            options.Keywords.Add("X");
            options.Keywords.Default = "X";
            PromptResult result = editor.GetKeywords(options);
            return result.Status == PromptStatus.OK &&
                String.Equals(result.StringResult, "C",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveFixedLibraryPath()
        {
            Assembly assembly = typeof(SupportDetailCommands).Assembly;
            string directory = Path.GetDirectoryName(assembly.Location);
            if (!String.IsNullOrWhiteSpace(directory))
            {
                string adjacent = Path.Combine(directory,
                    "SupportDetailFixedDetails.dwg");
                if (File.Exists(adjacent))
                    return adjacent;
            }

            const string resourceName =
                "StructuralToolbox.SupportDetail.SupportDetailFixedDetails.dwg";
            string version = assembly.GetName().Version == null
                ? "current" : assembly.GetName().Version.ToString();
            string cacheDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "StructuralToolbox", "support-detail", version);
            Directory.CreateDirectory(cacheDirectory);
            string cached = Path.Combine(cacheDirectory,
                "SupportDetailFixedDetails.dwg");
            if (File.Exists(cached) && new FileInfo(cached).Length > 0)
                return cached;

            string temporary = cached + ".tmp-" +
                Guid.NewGuid().ToString("N");
            try
            {
                using (Stream resource = assembly.GetManifestResourceStream(
                    resourceName))
                {
                    if (resource == null)
                        return String.Empty;
                    using (FileStream output = File.Create(temporary))
                        resource.CopyTo(output);
                }
                File.Copy(temporary, cached, true);
                return File.Exists(cached) ? cached : String.Empty;
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static void WriteSummary(Editor editor, string action,
            string instanceId, SupportDetailDrawSummary summary, int erased)
        {
            editor.WriteMessage(
                "\n{0}支架实例 {1}：{2} 个原生实体，{3} 个尺寸，" +
                "{4} 个支墩/立柱。",
                action, instanceId, summary.EntityCount,
                summary.DimensionCount, summary.PierCount);
            if (erased > 0)
                editor.WriteMessage("\n本次替换旧实体 {0} 个。", erased);
            if (!summary.FixedDetailsInserted)
            {
                editor.WriteMessage(
                    "\n固定节点库文件未找到，已生成简化节点占位；" +
                    "主支架和参数回读不受影响。");
            }
            editor.WriteMessage(
                "\n可选中任意构件后运行 STPVSUPPORTDETAILEDIT 修改。 ");
        }
    }
}
