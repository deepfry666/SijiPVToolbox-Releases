using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;
using StructuralToolbox.Contracts;

namespace StructuralToolbox.Ballast.UI
{
    public static class BallastWorkflowBridge
    {
        private const string ModuleId = "ballast";
        private static BallastToolboxControl _page;

        internal static void Attach(BallastToolboxControl page)
        {
            _page = page;
            page.Disposed += OnPageDisposed;
        }

        public static void Show()
        {
            if (!ToolboxNavigation.ShowModule(ModuleId))
            {
                Application.ShowAlertDialog(
                    "四季光伏工具箱主界面未加载，请输入 JJJJ 后重试。 ");
            }
        }

        public static void Busy(string message)
        {
            EnsurePage();
            if (_page != null)
                _page.ShowBusy(message);
        }

        public static void Error(string message)
        {
            EnsurePage();
            if (_page != null)
                _page.ShowError(message);
            else
                Application.ShowAlertDialog(message);
        }

        public static void Detection(int componentCount, int arrayCount,
            IList<int> rowCounts, IDictionary<int, int> arrayCounts,
            string details)
        {
            EnsurePage();
            if (_page != null)
            {
                _page.ApplyDetection(componentCount, arrayCount, rowCounts,
                    arrayCounts, details);
            }
        }

        public static void Generated(int frameCount, int ballastCount,
            int conflictFrameCount, int erasedFrameCount,
            int protectedFrameCount, string details)
        {
            EnsurePage();
            if (_page != null)
            {
                _page.ShowGenerated(frameCount, ballastCount,
                    conflictFrameCount, erasedFrameCount,
                    protectedFrameCount, details);
            }
        }

        public static void ArrayTableInserted()
        {
            EnsurePage();
            if (_page != null)
                _page.ShowArrayTableInserted();
        }

        private static void EnsurePage()
        {
            if (_page == null || _page.IsDisposed)
                ToolboxNavigation.ShowModule(ModuleId);
        }

        private static void OnPageDisposed(object sender, EventArgs eventArgs)
        {
            BallastToolboxControl page = sender as BallastToolboxControl;
            if (page != null)
                page.Disposed -= OnPageDisposed;
            if (ReferenceEquals(_page, page))
                _page = null;
        }
    }
}
