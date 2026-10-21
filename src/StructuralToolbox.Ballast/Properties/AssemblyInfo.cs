using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Ballast.Commands;

[assembly: AssemblyTitle("StructuralToolbox.Ballast")]
[assembly: AssemblyDescription("四季光伏工具箱自动布置配重独立模块")]
[assembly: AssemblyCompany("Four Seasons PV Toolbox")]
[assembly: AssemblyProduct("StructuralToolbox.Ballast")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.8.0")]
[assembly: AssemblyFileVersion("1.0.8.0")]
[assembly: CommandClass(typeof(BallastCommands))]
