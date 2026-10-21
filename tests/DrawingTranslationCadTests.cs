using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.DrawingTranslation.Api;
using StructuralToolbox.DrawingTranslation.Commands;
using StructuralToolbox.DrawingTranslation.Core;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

public sealed class DrawingTranslationCadTests
{
    private static List<TranslationEdit> _rollback;

    [CommandMethod("STPVTRANSLATIONREPLACECHECK", CommandFlags.Modal)]
    public void CheckReplacement()
    {
        Document document = AcApplication.DocumentManager.MdiActiveDocument;
        Database database = document.Database;
        ObjectId[] ids = CreateSources(database);
        int originalCount = CountEntities(database);
        int skipped;
        IList<TranslationTarget> targets = TranslationCommands.ReadTargets(
            database, ids, out skipped);
        Require(targets.Count == 4 && skipped == 0,
            "replacement sources were not read");
        List<TranslationEdit> edits = new List<TranslationEdit>();
        foreach (TranslationTarget target in targets)
            edits.Add(new TranslationEdit(target,
                target.IsTableCell ? "STPV_REPLACED_TABLE_" + target.Row :
                target.Item.Kind == DrawingTextKind.DbText
                    ? "STPV_REPLACED_DB with a deliberately long translated " +
                        "sentence that must wrap inside the original text width"
                    : "STPV_REPLACED_MT"));
        int stale;
        int replaced = TranslationCommands.ReplaceEdits(database, edits,
            out stale);
        Require(replaced == 4 && stale == 0 &&
            CountEntities(database) == originalCount,
            "replacement changed entity count or skipped a source");
        using (Transaction transaction = database.TransactionManager.
            StartOpenCloseTransaction())
        {
            int wrappedDb = 0;
            BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database),
                OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                MText wrapped = transaction.GetObject(id, OpenMode.ForRead)
                    as MText;
                if (wrapped == null || !wrapped.Contents.StartsWith(
                        "STPV_REPLACED_DB", StringComparison.Ordinal))
                    continue;
                Require(wrapped.Width >= wrapped.TextHeight * 4.0 &&
                    wrapped.ActualHeight > wrapped.TextHeight,
                    "converted DBText did not wrap within a stable width");
                wrappedDb++;
            }
            Require(wrappedDb == 1,
                "DBText was not converted to one wrapped MText entity");
            Require(((MText)transaction.GetObject(ids[1], OpenMode.ForRead)).
                Contents == "STPV_REPLACED_MT", "MText was not replaced in place");
            Table table = (Table)transaction.GetObject(ids[2], OpenMode.ForRead);
            Require(table.Cells[0, 0].TextString == "STPV_REPLACED_TABLE_0" &&
                table.Cells[1, 0].TextString == "STPV_REPLACED_TABLE_1",
                "table cells were not replaced in place");
            transaction.Commit();
        }
        document.Editor.WriteMessage("\nSTPV_TRANSLATION_REPLACE_PASS");
    }

    [CommandMethod("STPVTRANSLATIONREPLACEREOPEN", CommandFlags.Modal)]
    public void CheckReplacementReopen()
    {
        Database database = AcApplication.DocumentManager.MdiActiveDocument.Database;
        int found = 0;
        using (Transaction transaction = database.TransactionManager.
            StartOpenCloseTransaction())
        {
            BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database),
                OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                Entity entity = transaction.GetObject(id, OpenMode.ForRead)
                    as Entity;
                DBText db = entity as DBText;
                MText mt = entity as MText;
                Table table = entity as Table;
                if (mt != null && mt.Contents.StartsWith("STPV_REPLACED_DB",
                    StringComparison.Ordinal) && mt.Width >= mt.TextHeight * 4.0 &&
                    mt.ActualHeight > mt.TextHeight) found++;
                if (mt != null && mt.Contents == "STPV_REPLACED_MT") found++;
                if (table != null && table.Rows.Count == 2 &&
                    table.Cells[0, 0].TextString == "STPV_REPLACED_TABLE_0" &&
                    table.Cells[1, 0].TextString == "STPV_REPLACED_TABLE_1")
                    found++;
            }
            transaction.Commit();
        }
        Require(found == 3, "replaced entities did not survive reopen");
        AcApplication.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
            "\nSTPV_TRANSLATION_REPLACE_REOPEN_PASS");
    }

    private static int CountEntities(Database database)
    {
        int count = 0;
        using (Transaction transaction = database.TransactionManager.
            StartOpenCloseTransaction())
        {
            BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database),
                OpenMode.ForRead);
            foreach (ObjectId ignored in model) count++;
            transaction.Commit();
        }
        return count;
    }

    private static ObjectId[] CreateSources(Database database)
    {
        ObjectId[] ids = new ObjectId[3];
        using (Transaction transaction = database.TransactionManager.
            StartTransaction())
        {
            ObjectId modelId = SymbolUtilityServices.
                GetBlockModelSpaceId(database);
            BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                modelId, OpenMode.ForWrite);

            DBText dbText = new DBText();
            dbText.SetDatabaseDefaults(database);
            dbText.Position = new Point3d(1000, 1000, 0);
            dbText.Height = 100;
            dbText.TextString = "STPV_SOURCE_DB";
            ids[0] = model.AppendEntity(dbText);
            transaction.AddNewlyCreatedDBObject(dbText, true);

            MText mText = new MText();
            mText.SetDatabaseDefaults(database);
            mText.Location = new Point3d(3000, 1000, 0);
            mText.TextHeight = 100;
            mText.Contents = "STPV_SOURCE_MT";
            ids[1] = model.AppendEntity(mText);
            transaction.AddNewlyCreatedDBObject(mText, true);

            Table table = new Table();
            table.SetDatabaseDefaults(database);
            table.TableStyle = database.Tablestyle;
            table.SetSize(2, 1);
            table.Position = new Point3d(5000, 1000, 0);
            table.SetRowHeight(300);
            table.SetColumnWidth(1500);
            table.Cells[0, 0].TextHeight = 100;
            table.Cells[0, 0].TextString = "STPV_SOURCE_TABLE_A";
            table.Cells[1, 0].TextHeight = 100;
            table.Cells[1, 0].TextString = "STPV_SOURCE_TABLE_B";
            table.GenerateLayout();
            ids[2] = model.AppendEntity(table);
            transaction.AddNewlyCreatedDBObject(table, true);
            transaction.Commit();
        }
        return ids;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    [CommandMethod("STPVTRANSLATIONLIVE", CommandFlags.Modal)]
    public void RunLiveTranslation()
    {
        ExecuteLiveTranslation();
    }

    private void ExecuteLiveTranslation()
    {
        Document document = AcApplication.DocumentManager.MdiActiveDocument;
        Editor editor = document.Editor;
        Database database = document.Database;
        _rollback = null;
        try
        {
            ObjectId[] ids = CollectTextIds(database);
            int skipped;
            IList<TranslationTarget> targets = TranslationCommands.ReadTargets(
                database, ids, out skipped);
            Require(targets.Count > 0, "模型空间中没有可翻译的文字。");
            WriteTextDump(database, "translation-before");
            TranslationApiSettings settings = TranslationApiSettingsStore.
                ForCurrentUser().Load();
            settings.Validate();
            IList<string> translated = TranslateTargets(settings, targets);
            List<TranslationEdit> edits = new List<TranslationEdit>();
            List<TranslationEdit> rollback = new List<TranslationEdit>();
            for (int index = 0; index < targets.Count; index++)
            {
                string restored;
                string error;
                Require(targets[index].Item.Protection.TryRestore(
                    translated[index], out restored, out error),
                    "第 " + (index + 1).ToString() + " 项译文无效：" + error);
                if (String.Equals(restored, targets[index].Item.RawContent,
                    StringComparison.Ordinal))
                    continue;
                edits.Add(new TranslationEdit(targets[index], restored));
                rollback.Add(new TranslationEdit(targets[index],
                    targets[index].Item.RawContent));
            }
            Require(edits.Count > 0, "译文与原文相同，没有可写入的内容。");
            _rollback = rollback;
            int stale;
            int written = TranslationCommands.ReplaceEdits(database, edits,
                out stale);
            Require(written == edits.Count && stale == 0,
                "写入结果不符：成功 " + written.ToString() + "/" +
                edits.Count.ToString() + "，跳过 " + stale.ToString() + "。");
            int lines = WriteTextDump(database, "translation-after");
            _rollback = null;
            string detail = "targets=" + targets.Count.ToString() +
                " written=" + written.ToString() + " skipped=" +
                skipped.ToString() + " lines=" + lines.ToString() + " model=" +
                settings.Model + " endpoint=" +
                settings.ChatCompletionsUri().AbsoluteUri;
            editor.WriteMessage("\nSTPV_TRANSLATION_LIVE_PASS " + detail);
        }
        catch (System.Exception exception)
        {
            RestoreSources(database, _rollback);
            _rollback = null;
            editor.WriteMessage("\nSTPV_TRANSLATION_LIVE_FAIL " +
                exception.Message);
        }
    }

    private static IList<string> TranslateTargets(
        TranslationApiSettings settings, IList<TranslationTarget> targets)
    {
        List<string> source = new List<string>();
        foreach (TranslationTarget target in targets)
            source.Add(target.Item.TranslatableContent);
        using (OpenAiCompatibleTranslator translator =
            OpenAiCompatibleTranslator.Create(settings,
                TimeSpan.FromSeconds(180)))
        {
            return translator.TranslateAsync(settings, source,
                CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    [CommandMethod("STPVTRANSLATIONLIVECHECK", CommandFlags.Modal)]
    public void CheckLiveDump()
    {
        Document document = AcApplication.DocumentManager.MdiActiveDocument;
        Database database = document.Database;
        List<string> lines = CollectTextLines(database);
        File.WriteAllLines(DumpPath(database, "translation-check"), lines.ToArray(),
            new UTF8Encoding(false));
        int ideographs;
        int fullWidth;
        CountCjk(lines, out ideographs, out fullWidth);
        document.Editor.WriteMessage("\nSTPV_TRANSLATION_LIVECHECK lines=" +
            lines.Count.ToString() + " words=" + WordCount(lines).ToString() +
            " cjk=" + ideographs.ToString() + " fullwidth=" +
            fullWidth.ToString());
    }

    [CommandMethod("STPVTRANSLATIONLIVEAPI", CommandFlags.Modal)]
    public void ProbeLiveApi()
    {
        Document document = AcApplication.DocumentManager.MdiActiveDocument;
        Editor editor = document.Editor;
        try
        {
            TranslationApiSettings settings = TranslationApiSettingsStore.
                ForCurrentUser().Load();
            settings.Validate();
            IList<string> models;
            using (OpenAiCompatibleTranslator translator =
                OpenAiCompatibleTranslator.Create(settings,
                    TimeSpan.FromSeconds(60)))
            {
                models = translator.ListModelsAsync(settings,
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            IList<string> sample;
            using (OpenAiCompatibleTranslator translator =
                OpenAiCompatibleTranslator.Create(settings,
                    TimeSpan.FromSeconds(60)))
            {
                sample = translator.TranslateAsync(settings,
                    new List<string> { "结构工具箱连接测试" },
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            Require(models.Count > 0, "模型列表为空。");
            Require(sample.Count == 1 &&
                !String.IsNullOrWhiteSpace(sample[0]),
                "连接测试没有返回译文。");
            editor.WriteMessage("\nSTPV_TRANSLATION_LIVE_API_PASS models=" +
                models.Count.ToString() + " first=" + models[0] +
                " sample=" + sample[0]);
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage("\nSTPV_TRANSLATION_LIVE_API_FAIL " +
                exception.Message);
        }
    }
    private static int WordCount(IEnumerable<string> lines)
    {
        int total = 0;
        foreach (string line in lines)
        {
            int separator = line.LastIndexOf('|');
            string content = separator < 0 ? line : line.Substring(separator + 1);
            foreach (string word in content.Split(new char[] { ' ', '\t' }))
                if (word.Trim().Length > 0) total++;
        }
        return total;
    }

    private static void CountCjk(IEnumerable<string> lines, out int ideographs,
        out int fullWidth)
    {
        ideographs = 0;
        fullWidth = 0;
        foreach (string line in lines)
        {
            int separator = line.LastIndexOf('|');
            string content = separator < 0 ? line : line.Substring(separator + 1);
            foreach (char character in content)
            {
                if (character >= '\u3400' && character <= '\u9fff')
                    ideographs++;
                else if (character >= '\uff01' && character <= '\uff60')
                    fullWidth++;
            }
        }
    }

    internal static string DumpPath(Database database, string tag)
    {
        string file = database.Filename;
        string directory = String.IsNullOrEmpty(file)
            ? Path.GetTempPath() : Path.GetDirectoryName(file);
        string name = String.IsNullOrEmpty(file)
            ? "unnamed" : Path.GetFileNameWithoutExtension(file);
        return Path.Combine(directory, name + "." + tag + ".txt");
    }

    internal static int WriteTextDump(Database database, string tag)
    {
        List<string> lines = CollectTextLines(database);
        File.WriteAllLines(DumpPath(database, tag), lines.ToArray(),
            new UTF8Encoding(false));
        return lines.Count;
    }

    internal static List<string> CollectTextLines(Database database)
    {
        List<string> lines = new List<string>();
        using (Transaction transaction = database.TransactionManager.
            StartOpenCloseTransaction())
        {
            BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database),
                OpenMode.ForRead, false);
            foreach (ObjectId id in model)
            {
                Entity entity = transaction.GetObject(id, OpenMode.ForRead,
                    false) as Entity;
                if (entity == null)
                    continue;
                DBText dbText = entity as DBText;
                MText mText = entity as MText;
                Table table = entity as Table;
                if (dbText != null)
                    lines.Add("TEXT|" + entity.Handle + "|" +
                        Y(dbText.Position) + "|" + Flatten(dbText.TextString));
                else if (mText != null)
                    lines.Add("MTEXT|" + entity.Handle + "|" +
                        Y(mText.Location) + "|" + Flatten(mText.Contents));
                else if (table != null)
                {
                    for (int row = 0; row < table.Rows.Count; row++)
                    {
                        for (int column = 0; column < table.Columns.Count;
                            column++)
                        {
                            string content;
                            try
                            {
                                content = Flatten(table.Cells[row, column].
                                    Contents[0].TextString);
                            }
                            catch (System.Exception)
                            {
                                continue;
                            }
                            lines.Add("CELL|" + entity.Handle + ":" +
                                row.ToString() + ":" + column.ToString() +
                                "|" + Y(table.Position) + "|" + content);
                        }
                    }
                }
            }
            transaction.Commit();
        }
        return lines;
    }

    private static string Y(Point3d point)
    {
        return point.Y.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Flatten(string value)
    {
        if (String.IsNullOrEmpty(value))
            return String.Empty;
        string flattened = value.Replace("\r", " ").Replace("\n", " ");
        while (flattened.IndexOf("  ", StringComparison.Ordinal) >= 0)
            flattened = flattened.Replace("  ", " ");
        return flattened.Trim();
    }

    private static ObjectId[] CollectTextIds(Database database)
    {
        List<ObjectId> ids = new List<ObjectId>();
        using (Transaction transaction = database.TransactionManager.
            StartOpenCloseTransaction())
        {
            BlockTableRecord model = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database),
                OpenMode.ForRead, false);
            foreach (ObjectId id in model)
            {
                Entity entity = transaction.GetObject(id, OpenMode.ForRead,
                    false) as Entity;
                if (entity is DBText || entity is MText || entity is Table)
                    ids.Add(id);
            }
            transaction.Commit();
        }
        return ids.ToArray();
    }
    private static void RestoreSources(Database database,
        IList<TranslationEdit> rollback)
    {
        if (rollback == null || rollback.Count == 0)
            return;
        using (Transaction transaction = database.TransactionManager.
            StartOpenCloseTransaction())
        {
            foreach (TranslationEdit edit in rollback)
            {
                TranslationTarget target = edit.Target;
                Entity entity = transaction.GetObject(target.ObjectId,
                    OpenMode.ForWrite, false) as Entity;
                if (entity == null)
                    continue;
                DBText dbText = entity as DBText;
                MText mText = entity as MText;
                if (dbText != null)
                    dbText.TextString = edit.Value;
                else if (mText != null)
                    mText.Contents = edit.Value;
                else
                {
                    Table table = entity as Table;
                    if (table != null && target.Row >= 0 &&
                        target.Column >= 0 &&
                        target.Row < table.Rows.Count &&
                        target.Column < table.Columns.Count)
                    {
                        table.Cells[target.Row, target.Column].
                            Contents[0].TextString = edit.Value;
                        table.GenerateLayout();
                    }
                }
            }
            transaction.Commit();
        }
    }
}
