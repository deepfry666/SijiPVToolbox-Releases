using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox;
using StructuralToolbox.Commands;

[assembly: AssemblyTitle("StructuralToolbox")]
[assembly: AssemblyDescription("FourJTooLs photovoltaic structural toolbox")]
[assembly: AssemblyCompany("FourJTooLs")]
[assembly: AssemblyProduct("四季光伏工具箱")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: ComVisible(false)]
[assembly: Guid("5D65489E-5578-43B9-B36C-1C56A0D143ED")]
[assembly: AssemblyVersion("1.0.8.0")]
[assembly: AssemblyFileVersion("1.0.8.0")]
[assembly: ExtensionApplication(typeof(PluginEntry))]
[assembly: CommandClass(typeof(ToolboxCommands))]
