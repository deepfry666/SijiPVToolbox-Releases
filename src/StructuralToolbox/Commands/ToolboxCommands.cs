using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.UI;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace StructuralToolbox.Commands
{
    public sealed class ToolboxCommands
    {
        [CommandMethod("JJJJ", CommandFlags.Modal)]
        public void ShowToolboxShortcut()
        {
            ShowToolboxCore();
        }

        [CommandMethod("STTOOLBOX", CommandFlags.Modal)]
        public void ShowToolbox()
        {
            ShowToolboxCore();
        }

        private static void ShowToolboxCore()
        {
            try
            {
                ToolboxRibbon.EnsureCreated(false);
            }
            catch (System.Exception exception)
            {
                Document document = AcApplication.DocumentManager.MdiActiveDocument;
                if (document != null)
                    document.Editor.WriteMessage("\n顶部页签恢复失败：{0}", exception.Message);
            }
            ToolboxPalette.ShowHome();
        }

        [CommandMethod("STTOOLBAR", CommandFlags.Modal)]
        public void ShowToolbar()
        {
            try
            {
                if (!ToolboxRibbon.EnsureCreated(true))
                {
                    AcApplication.ShowAlertDialog(
                        "AutoCAD 顶部 Ribbon 尚未初始化，请稍后重试 STTOOLBAR。 ");
                }
            }
            catch (System.Exception exception)
            {
                AcApplication.ShowAlertDialog("顶部页签恢复失败：" + exception.Message);
            }
        }

        [CommandMethod("STABOUT", CommandFlags.Modal)]
        public void ShowAbout()
        {
            AcApplication.ShowAlertDialog(
                "四季光伏工具箱 1.0.8\n" +
                "功能：自动画导轨、自动布置配重、图纸文字翻译、检查软件更新\n" +
                "命令：JJJJ / STPVRAIL / STPVBALLAST / STPVTRANSLATE / STPVTRANSLATECONFIRM\n" +
                "核心运行已验证：AutoCAD 2020 (R23.1) Win64\n" +
                "完整 GUI、探索者交互和系统级升级：待验收");
        }
    }
}
