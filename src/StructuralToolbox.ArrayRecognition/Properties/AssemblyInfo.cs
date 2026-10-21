using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.ArrayRecognition.Commands;

[assembly: AssemblyTitle("StructuralToolbox.ArrayRecognition")]
[assembly: AssemblyDescription("光伏组件阵列识别独立模块")]
[assembly: AssemblyCompany("Four Seasons PV Toolbox")]
[assembly: AssemblyProduct("StructuralToolbox.ArrayRecognition")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: CommandClass(typeof(ArrayRecognitionCommands))]
