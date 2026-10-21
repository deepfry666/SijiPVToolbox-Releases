using System;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.UI
{
    internal static class ToolboxRibbon
    {
        private const string TabId = "STRUCTURAL_TOOLBOX_TAB";
        private const string PanelId = "STRUCTURAL_TOOLBOX_PV_PANEL";
        private const string PanelSourceId = "STRUCTURAL_TOOLBOX_PV_PANEL_SOURCE";
        private const string RailButtonId = "STRUCTURAL_TOOLBOX_RAIL_BUTTON";
        private const string BallastButtonId = "STRUCTURAL_TOOLBOX_BALLAST_BUTTON";
        private const string LegacyAnnotationButtonId =
            "STRUCTURAL_TOOLBOX_ANNOTATION_BUTTON";
        private const string HomeButtonId = "STRUCTURAL_TOOLBOX_HOME_BUTTON";

        public static bool EnsureCreated(bool activate)
        {
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
                return false;

            for (int index = 0; index < ribbon.Tabs.Count; index++)
            {
                if (String.Equals(ribbon.Tabs[index].Id, TabId, StringComparison.Ordinal))
                {
                    RibbonTab existing = ribbon.Tabs[index];
                    RefreshTab(existing, activate);
                    return true;
                }
            }

            RibbonTab tab = new RibbonTab();
            tab.Id = TabId;
            tab.Title = "四季光伏工具箱";
            tab.IsVisible = true;
            tab.IsEnabled = true;

            tab.Panels.Add(CreatePanel());

            ribbon.Tabs.Add(tab);
            if (activate)
                tab.IsActive = true;
            return true;
        }

        private static void RefreshTab(RibbonTab tab, bool activate)
        {
            tab.Title = "四季光伏工具箱";
            tab.IsVisible = true;
            tab.IsEnabled = true;

            RibbonPanelSource source = FindToolboxPanelSource(tab);
            RibbonButton railButton = null;
            RibbonButton ballastButton = null;
            RibbonButton homeButton = null;
            bool legacyAnnotationButtonFound = false;
            if (source != null)
            {
                for (int index = 0; index < source.Items.Count; index++)
                {
                    RibbonButton button = source.Items[index] as RibbonButton;
                    if (button == null)
                        continue;
                    if (String.Equals(button.Id, RailButtonId, StringComparison.Ordinal) ||
                        HasCommand(button, "STPVRAIL"))
                    {
                        railButton = button;
                    }
                    else if (String.Equals(button.Id, BallastButtonId,
                        StringComparison.Ordinal) || HasCommand(button, "STPVBALLAST"))
                    {
                        ballastButton = button;
                    }
                    else if (String.Equals(button.Id, LegacyAnnotationButtonId,
                        StringComparison.Ordinal) ||
                        String.Equals(button.Id, "STRUCTURAL_TOOLBOX_RAIL_DIMENSION_BUTTON",
                            StringComparison.Ordinal) || HasCommand(button, "STPVRAILDIM"))
                        legacyAnnotationButtonFound = true;
                    else if (String.Equals(button.Id, HomeButtonId, StringComparison.Ordinal) ||
                        HasCommand(button, "JJJJ") || HasCommand(button, "STTOOLBOX"))
                    {
                        homeButton = button;
                    }
                }
            }

            if (source == null || railButton == null || ballastButton == null ||
                homeButton == null || legacyAnnotationButtonFound)
            {
                tab.Panels.Clear();
                tab.Panels.Add(CreatePanel());
            }
            else
            {
                source.Id = PanelSourceId;
                source.Title = "光伏结构";
                ConfigureRailButton(railButton);
                ConfigureBallastButton(ballastButton);
                ConfigureHomeButton(homeButton);
            }

            if (activate)
                tab.IsActive = true;
        }

        private static RibbonPanelSource FindToolboxPanelSource(RibbonTab tab)
        {
            for (int index = 0; index < tab.Panels.Count; index++)
            {
                RibbonPanel panel = tab.Panels[index];
                if (panel == null || panel.Source == null)
                    continue;
                if (String.Equals(panel.Id, PanelId, StringComparison.Ordinal) ||
                    String.Equals(panel.Source.Id, PanelSourceId, StringComparison.Ordinal) ||
                    String.Equals(panel.Source.Title, "光伏结构", StringComparison.Ordinal))
                {
                    panel.Id = PanelId;
                    return panel.Source;
                }
            }
            return null;
        }

        private static RibbonPanel CreatePanel()
        {
            RibbonPanelSource panelSource = new RibbonPanelSource();
            panelSource.Id = PanelSourceId;
            panelSource.Title = "光伏结构";
            RibbonPanel panel = new RibbonPanel();
            panel.Id = PanelId;
            panel.Source = panelSource;

            RibbonButton railButton = new RibbonButton();
            ConfigureRailButton(railButton);
            panelSource.Items.Add(railButton);

            RibbonButton ballastButton = new RibbonButton();
            ConfigureBallastButton(ballastButton);
            panelSource.Items.Add(ballastButton);

            RibbonButton toolboxButton = new RibbonButton();
            ConfigureHomeButton(toolboxButton);
            panelSource.Items.Add(toolboxButton);

            return panel;
        }

        private static void ConfigureRailButton(RibbonButton button)
        {
            button.Id = RailButtonId;
            button.Text = "自动画导轨";
            button.ShowText = true;
            button.Size = RibbonItemSize.Large;
            button.CommandParameter = "STPVRAIL ";
            button.CommandHandler = new RibbonCommandHandler();
            button.ToolTip = "选择组件并预览导轨。";
        }

        private static void ConfigureBallastButton(RibbonButton button)
        {
            button.Id = BallastButtonId;
            button.Text = "自动布置配重";
            button.ShowText = true;
            button.Size = RibbonItemSize.Large;
            button.CommandParameter = "STPVBALLAST ";
            button.CommandHandler = new RibbonCommandHandler();
            button.ToolTip = "在左侧工具箱中识别阵列并布置配重。";
        }


        private static void ConfigureHomeButton(RibbonButton button)
        {
            button.Id = HomeButtonId;
            button.Text = "工具箱首页";
            button.ShowText = true;
            button.Size = RibbonItemSize.Standard;
            button.CommandParameter = "JJJJ ";
            button.CommandHandler = new RibbonCommandHandler();
            button.ToolTip = "打开四季光伏工具箱首页。";
        }

        private static bool HasCommand(RibbonButton button, string command)
        {
            string parameter = button.CommandParameter as string;
            return parameter != null && String.Equals(parameter.Trim(), command,
                StringComparison.OrdinalIgnoreCase);
        }

        private sealed class RibbonCommandHandler : ICommand
        {
            public bool CanExecute(object parameter)
            {
                return AcApplication.DocumentManager.MdiActiveDocument != null;
            }

            public void Execute(object parameter)
            {
                Document document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document == null)
                    return;

                string command = parameter as string;
                if (!String.IsNullOrWhiteSpace(command))
                    document.SendStringToExecute(command, true, false, false);
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}
