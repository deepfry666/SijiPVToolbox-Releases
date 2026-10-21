using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Contracts;
using StructuralToolbox.UI;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox
{
    public sealed class PluginEntry : IExtensionApplication
    {
        private static readonly TimeSpan RibbonCheckInterval = TimeSpan.FromSeconds(2.0);
        private static bool _idleAttached;
        private static DateTime _nextRibbonCheckUtc = DateTime.MinValue;
        private static string _lastRibbonError = String.Empty;

        public void Initialize()
        {
            ToolboxNavigation.Configure(ToolboxPalette.ShowModule);

            if (String.Equals(Process.GetCurrentProcess().ProcessName,
                "accoreconsole", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_idleAttached)
            {
                AcApplication.Idle += OnApplicationIdle;
                _idleAttached = true;
            }

            Document document = AcApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage(
                    "\n四季光伏工具箱 1.0.8 已加载。" +
                    "输入 JJJJ 打开首页。 ");
            }
        }

        public void Terminate()
        {
            ToolboxNavigation.Reset();
            try
            {
                ToolboxPalette.Shutdown();
            }
            catch
            {
            }

            if (_idleAttached)
            {
                try
                {
                    AcApplication.Idle -= OnApplicationIdle;
                }
                catch
                {
                }
                finally
                {
                    _idleAttached = false;
                }
            }
        }

        private static void OnApplicationIdle(object sender, EventArgs eventArgs)
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextRibbonCheckUtc)
                return;
            _nextRibbonCheckUtc = now.Add(RibbonCheckInterval);

            try
            {
                ToolboxRibbon.EnsureCreated(false);
                _lastRibbonError = String.Empty;
            }
            catch (System.Exception exception)
            {
                if (String.Equals(_lastRibbonError, exception.Message,
                    StringComparison.Ordinal))
                {
                    return;
                }

                _lastRibbonError = exception.Message;
                Document document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document != null)
                {
                    document.Editor.WriteMessage(
                        "\n四季光伏工具箱顶部页签暂未创建：{0}", exception.Message);
                }
            }
        }
    }
}
