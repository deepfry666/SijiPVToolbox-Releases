using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Commands;

[assembly: AssemblyTitle("StructuralToolbox.Rail")]
[assembly: AssemblyDescription("四季光伏工具箱自动画导轨独立模块")]
[assembly: AssemblyCompany("FourJTooLs")]
[assembly: AssemblyProduct("四季光伏工具箱")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.8.0")]
[assembly: AssemblyFileVersion("1.0.8.0")]
[assembly: CommandClass(typeof(RailCommands))]
[assembly: CommandClass(typeof(AnnotationOverlapCommands))]
#if INTERNAL_VERIFY
[assembly: CommandClass(typeof(VerificationCommands))]
#endif
