using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using StructuralToolbox.DrawingTranslation.Commands;
using StructuralToolbox.DrawingTranslation.Core;
using StructuralToolbox.DrawingTranslation.Api;
using StructuralToolbox.DrawingTranslation.UI;
using StructuralToolbox.DrawingTranslation;

internal static class DrawingTranslationUiLayoutTests
{
    private static readonly double[] DpiScales =
        new double[] { 1.0, 1.25, 1.5, 2.0 };

    private static readonly string[] ReviewDialogControls = new string[] {
        "TranslationReviewGrid", "ConfirmTranslationButton",
        "RetryTranslationButton",
        "CancelTranslationButton" };

    private static readonly string[] ApiDialogControls = new string[] {
        "ApiEndpoint", "ApiModel", "ApiKey", "ApiTargetLanguage",
        "ApiProxyModeCombo", "ApiProxyAddress",
        "ApiGlossary", "TranslationCacheEnabled", "TranslationCacheDays",
        "ClearTranslationCacheButton",
        "ApiTestStatusLabel", "TestApiButton", "FetchModelsButton",
        "SaveApiSettingsButton" };

    [STAThread]
    private static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveCadAssembly;
        try
        {
            Run();
            CheckReviewDialogDpi();
            CheckApiDialogDpi();
            CheckReviewDialogApply();
            CheckSettingsDialogSave();
            CheckMainToolboxModule();
            Console.WriteLine("PASS drawing translation UI layout tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL drawing translation UI layout: " +
                exception.GetType().FullName + " " + exception.Message);
            return 1;
        }
    }

    private static Assembly ResolveCadAssembly(object sender,
        ResolveEventArgs eventArgs)
    {
        string name = new AssemblyName(eventArgs.Name).Name;
        if (!String.Equals(name, "acmgd",
                StringComparison.OrdinalIgnoreCase) &&
            !String.Equals(name, "acdbmgd",
                StringComparison.OrdinalIgnoreCase) &&
            !String.Equals(name, "accoremgd",
                StringComparison.OrdinalIgnoreCase))
            return null;
        return Assembly.LoadFrom(Path.Combine(@"D:\cad\AutoCAD 2020",
            name + ".dll"));
    }

    private static void Run()
    {
            DrawingTextItem item = DrawingTextItemFactory.Create("1A",
                DrawingTextKind.DbText, "技术要求", false, false);
            IList<TranslationTarget> targets =
                new List<TranslationTarget> {
                    new TranslationTarget(ObjectId.Null, -1, -1, item)
                };
            using (TranslationReviewDialog dialog =
                new TranslationReviewDialog(targets, 2))
            {
                dialog.CreateControl();
                foreach (Size size in new [] {
                    new Size(740, 450), new Size(1020, 650) })
                {
                    dialog.ClientSize = size;
                    dialog.PerformLayout();
                    DataGridView grid = (DataGridView)dialog.Controls.Find(
                        "TranslationReviewGrid", true)[0];
                    Require(grid.Rows.Count == 1 &&
                        grid.Columns.Count == 3,
                        "translation grid is incomplete");
                    Rectangle gridBounds = ClientBounds(dialog, grid);
                    Require(grid.Width > 400 && grid.Height > 200 &&
                        gridBounds.Right <= dialog.ClientSize.Width &&
                        gridBounds.Bottom <= dialog.ClientSize.Height,
                        "translation grid does not fit dialog");
                    Button confirm = (Button)dialog.Controls.Find(
                        "ConfirmTranslationButton", true)[0];
                    Rectangle confirmBounds = ClientBounds(dialog, confirm);
                    Require(confirm.Text == "确认替换" &&
                        confirmBounds.Right <= dialog.ClientSize.Width &&
                        confirmBounds.Bottom <= dialog.ClientSize.Height,
                        "replacement confirmation is missing or clipped");
                    RequireInside(dialog, size.Width + "x" + size.Height,
                        confirm,
                        (Button)dialog.Controls.Find(
                            "CancelTranslationButton", true)[0]);
                    Require(dialog.Controls.Find("ApiSettingsButton", true).Length == 0 &&
                        dialog.Controls.Find("InsertTranslationButton", true).Length == 0 &&
                        dialog.Controls.Find("ReplaceTranslationButton", true).Length == 0,
                        "review dialog repeats global settings or write modes");
                }
            }
            using (TranslationApiSettingsDialog dialog =
                new TranslationApiSettingsDialog(
                    new TranslationApiSettingsStore(Path.Combine(
                        "work", "tests", "missing-api-settings.json"))))
            {
                dialog.CreateControl();
                ComboBox proxyMode = (ComboBox)dialog.Controls.Find(
                    "ApiProxyModeCombo", true)[0];
                TextBox proxyAddress = (TextBox)dialog.Controls.Find(
                    "ApiProxyAddress", true)[0];
                Require(proxyMode.SelectedIndex == 0 &&
                    proxyMode.Items.Count == 3 &&
                    !proxyAddress.Enabled,
                    "direct connection must be the default network setting");
                proxyMode.SelectedIndex = 2;
                Require(proxyAddress.Enabled,
                    "custom proxy address was not enabled");
                ComboBox model = (ComboBox)dialog.Controls.Find(
                    "ApiModel", true)[0];
                Require(model.DropDownStyle == ComboBoxStyle.DropDownList,
                    "model selection must use a fixed drop-down list");
                Require(dialog.Controls.Find("TranslationWorkflow", true).Length == 0,
                    "global settings must not repeat the write mode");
                foreach (Size size in new [] {
                    new Size(660, 590), new Size(760, 650),
                    new Size(900, 700) })
                {
                    dialog.ClientSize = size;
                    dialog.PerformLayout();
                    foreach (string name in new [] {
                        "ApiProxyModeCombo",
                        "ApiProxyAddress", "ApiTestStatusLabel",
                        "TestApiButton", "FetchModelsButton", "SaveApiSettingsButton" })
                    {
                        Control[] found = dialog.Controls.Find(name, true);
                        Require(found.Length == 1,
                            name + " is missing from API settings");
                        RequireInside(dialog,
                            size.Width + "x" + size.Height, found[0]);
                    }
                }
            }
    }

    private static void CheckReviewDialogDpi()
    {
        Dictionary<string, Size> baseline = null;
        foreach (double scale in DpiScales)
        {
            using (TranslationReviewDialog dialog =
                new TranslationReviewDialog(CreateTargets(), 2))
            {
                dialog.CreateControl();
                ApplyDpi(dialog, scale);
                dialog.ClientSize = Scale(new Size(1020, 650), scale);
                dialog.PerformLayout();
                RequireInsideClient(dialog, scale, "TranslationStatus",
                    "TranslationReviewGrid", "ConfirmTranslationButton",
                    "RetryTranslationButton",
                    "CancelTranslationButton");
                DataGridView grid = (DataGridView)dialog.Controls.Find(
                    "TranslationReviewGrid", true)[0];
                Require(grid.Rows.Count == 1 && grid.Columns.Count == 3,
                    "translation grid is incomplete at " + scale +
                    "x DPI");
                Require(grid.RowTemplate.Height >=
                    (int)Math.Ceiling(grid.Font.GetHeight() * 1.5),
                    "translation rows are too short for the scaled font at " +
                    scale + "x DPI");
                baseline = baseline ?? RequireScaled(baseline, dialog,
                    ReviewDialogControls, scale, "review dialog");
            }
        }
    }

    private static void CheckApiDialogDpi()
    {
        string path = Path.Combine("work", "tests", "dpi-api-settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Dictionary<string, Size> baseline = null;
        foreach (double scale in DpiScales)
        {
            using (TranslationApiSettingsDialog dialog =
                new TranslationApiSettingsDialog(
                    new TranslationApiSettingsStore(path)))
            {
                dialog.CreateControl();
                ApplyDpi(dialog, scale);
                dialog.ClientSize = Scale(new Size(760, 650), scale);
                dialog.PerformLayout();
                RequireInsideClient(dialog, scale, ApiDialogControls);
                baseline = baseline ?? RequireScaled(baseline, dialog,
                    ApiDialogControls, scale, "API panel");
            }
        }
    }

    private static void CheckReviewDialogApply()
    {
        const string translated = "Technical requirements";
        IList<TranslationTarget> targets = CreateTargets();
        string restored;
        string error;
        Require(targets[0].Item.Protection.TryRestore(translated, out restored,
            out error),
            "fixture translation must pass protection checks: " + error);
        using (TranslationReviewDialog dialog =
            new TranslationReviewDialog(targets, 0))
        {
            dialog.CreateControl();
            ((DataGridView)dialog.Controls.Find("TranslationReviewGrid",
                true)[0]).Rows[0].Cells[2].Value = translated;
            Button confirm = (Button)dialog.Controls.Find(
                "ConfirmTranslationButton", true)[0];
            confirm.Enabled = true;
            RaiseClick(confirm);
            Require(dialog.Edits != null && dialog.Edits.Count == 1 &&
                dialog.Edits[0].Value == restored,
                "confirming the review dialog did not collect the replacement");
        }
    }

    private static void CheckSettingsDialogSave()
    {
        const string key = "unit-test-key-not-a-real-secret";
        string path = Path.Combine("work", "tests", "dialog-api-settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path))
            File.Delete(path);
        TranslationApiSettingsStore store =
            new TranslationApiSettingsStore(path);
        TranslationApiSettings expected = new TranslationApiSettings {
            Endpoint = "https://example.invalid/v1", Model = "demo-model",
            ApiKey = key, TargetLanguage = "English",
            Workflow = TranslationWorkflow.ConfirmBeforeReplace,
            ProxyMode = TranslationProxyMode.System, ProxyAddress = "" };
        expected.Validate();
        using (TranslationApiSettingsDialog dialog =
            new TranslationApiSettingsDialog(store))
        {
            dialog.CreateControl();
            ((TextBox)dialog.Controls.Find("ApiEndpoint", true)[0]).Text =
                expected.Endpoint;
            ComboBox model = (ComboBox)dialog.Controls.Find(
                "ApiModel", true)[0];
            model.Items.Add(expected.Model);
            model.SelectedItem = expected.Model;
            ((TextBox)dialog.Controls.Find("ApiKey", true)[0]).Text = key;
            ((ComboBox)dialog.Controls.Find("ApiTargetLanguage", true)[0]).
                Text = expected.TargetLanguage;
            ((ComboBox)dialog.Controls.Find("ApiProxyModeCombo", true)[0]).
                SelectedIndex = 1;
            RaiseClick((Button)dialog.Controls.Find("SaveApiSettingsButton",
                true)[0]);
        }
        Require(File.Exists(path),
            "saving from the API panel did not write a settings file");
        TranslationApiSettings loaded = store.Load();
        Require(loaded.Endpoint == expected.Endpoint &&
            loaded.Model == expected.Model && loaded.ApiKey == key &&
            loaded.TargetLanguage == expected.TargetLanguage &&
            loaded.Workflow == TranslationWorkflow.ConfirmBeforeReplace &&
            loaded.ProxyMode == TranslationProxyMode.System,
            "API panel values did not round-trip");
        Require(File.ReadAllText(path).IndexOf(key,
            StringComparison.Ordinal) < 0,
            "API panel saved the key without protection");
        File.Delete(path);
    }

    private static void CheckMainToolboxModule()
    {
        DrawingTranslationToolboxModule module =
            new DrawingTranslationToolboxModule();
        Require(module.Id == "drawing-translation" &&
            module.DisplayName == "图纸文字翻译" &&
            module.PreferredPaletteSize.Width >= 700,
            "main toolbox translation module metadata is incomplete");
        using (Control control = module.CreateControl())
        {
            control.CreateControl();
            foreach (string name in new [] {
                "OpenDrawingTranslationSettings",
                "RunDrawingTranslationSelfTest",
                "RunDrawingTranslation",
                "RunDrawingTranslationConfirm",
                "ShowDrawingTranslationInfo" })
            {
                Control[] found = control.Controls.Find(name, true);
                Require(found.Length == 1 && found[0].Width > 0 &&
                    found[0].Height > 0,
                    "main toolbox translation control is missing: " + name);
            }
            Require(control.Controls.Find("RunDrawingTranslationInsert",
                true).Length == 0,
                "retired insert-below command is still shown on the home page");
            Button direct = (Button)control.Controls.Find(
                "RunDrawingTranslation", true)[0];
            Button confirm = (Button)control.Controls.Find(
                "RunDrawingTranslationConfirm", true)[0];
            Require(direct.Text == "直接替换" &&
                confirm.Text == "确认后替换",
                "main toolbox does not expose the two replacement modes clearly");
        }
    }

    private static IList<TranslationTarget> CreateTargets()
    {
        DrawingTextItem item = DrawingTextItemFactory.Create("1A",
            DrawingTextKind.DbText, "技术要求", false, false);
        return new List<TranslationTarget> {
            new TranslationTarget(ObjectId.Null, -1, -1, item) };
    }

    private static Rectangle ClientBounds(Form dialog, Control control)
    {
        int left = 0;
        int top = 0;
        Control current = control;
        while (current != null && current != dialog)
        {
            left += current.Left;
            top += current.Top;
            current = current.Parent;
        }
        return new Rectangle(left, top, control.Width, control.Height);
    }

    private static void RequireInside(Form dialog, string label,
        params Control[] controls)
    {
        foreach (Control control in controls)
        {
            if (!control.Visible)
                continue;
            Rectangle bounds = ClientBounds(dialog, control);
            Require(bounds.Right <= dialog.ClientSize.Width &&
                bounds.Bottom <= dialog.ClientSize.Height,
                control.Name + " is clipped at " + label + " (" +
                bounds.Right + "x" + bounds.Bottom + " > " +
                dialog.ClientSize.Width + "x" + dialog.ClientSize.Height +
                ")");
            RequireTextFits(control, control.Name, label);
        }
    }

    private static void ApplyDpi(Form dialog, double scale)
    {
        dialog.Font = new System.Drawing.Font(dialog.Font.FontFamily,
            dialog.Font.SizeInPoints * (float)scale, dialog.Font.Style);
        dialog.Scale(new SizeF((float)scale, (float)scale));
    }

    private static Size Scale(Size size, double scale)
    {
        return new Size((int)Math.Ceiling(size.Width * scale),
            (int)Math.Ceiling(size.Height * scale));
    }

    private static void RequireInsideClient(Form dialog, double scale,
        params string[] names)
    {
        foreach (string name in names)
        {
            Control[] found = dialog.Controls.Find(name, true);
            Require(found.Length == 1, name + " is missing from the dialog");
            Control control = found[0];
            Require(control.Width > 0 && control.Height > 0,
                name + " has no size at " + scale + "x DPI");
            RequireInside(dialog, scale + "x DPI", control);
        }
    }

    private static void RequireTextFits(Control control, string name,
        string label)
    {
        Label textLabel = control as Label;
        bool check = control is Button ||
            (textLabel != null && !textLabel.AutoEllipsis);
        if (!check || String.IsNullOrEmpty(control.Text))
            return;
        Size measured = TextRenderer.MeasureText(control.Text,
            control.Font);
        Require(measured.Width <= control.Width &&
            measured.Height <= control.Height,
            name + " text does not fit at " + label);
    }

    private static Dictionary<string, Size> RequireScaled(
        Dictionary<string, Size> baseline, Form dialog, string[] names,
        double scale, string label)
    {
        Dictionary<string, Size> current = new Dictionary<string, Size>();
        foreach (string name in names)
        {
            Control control = dialog.Controls.Find(name, true)[0];
            current[name] = control.Size;
            if (baseline == null)
                continue;
            int expected = (int)Math.Round(baseline[name].Width * scale);
            Require(control.Width >= (int)(expected * 0.75) &&
                control.Width <= (int)(expected * 1.25),
                label + " control " + name + " did not scale for " +
                scale + "x DPI (" + control.Width + " vs " + expected +
                ")");
        }
        return current;
    }

    private static void RaiseClick(Control control)
    {
        MethodInfo onClick = typeof(Control).GetMethod("OnClick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(onClick != null, "Control.OnClick was not found");
        onClick.Invoke(control, new object[] { EventArgs.Empty });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
