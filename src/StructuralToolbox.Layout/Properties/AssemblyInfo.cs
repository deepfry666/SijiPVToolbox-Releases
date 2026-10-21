using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Layout.Commands;

[assembly: AssemblyTitle("StructuralToolbox.Layout")]
[assembly: AssemblyDescription("四季光伏工具箱组件自动排布独立测试模块")]
[assembly: AssemblyCompany("Four Seasons PV Toolbox")]
[assembly: AssemblyProduct("StructuralToolbox.Layout")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.1.2.0")]
[assembly: AssemblyFileVersion("0.1.2.0")]
[assembly: CommandClass(typeof(LayoutCommands))]
