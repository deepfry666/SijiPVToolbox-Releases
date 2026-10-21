using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using StructuralToolbox.Commands;

[assembly: AssemblyTitle("FourJTools.RealDrawingVerify")]
[assembly: AssemblyDescription(
    "FourJTooLs same-source internal real-drawing verification assembly")]
[assembly: AssemblyCompany("FourJTooLs")]
[assembly: AssemblyProduct("四季光伏工具箱内部实图验证程序集")]
[assembly: AssemblyConfiguration("INTERNAL_VERIFY")]
[assembly: AssemblyVersion("1.0.8.0")]
[assembly: AssemblyFileVersion("1.0.8.0")]
[assembly: CommandClass(typeof(RailCommands))]

namespace FourJTools.Verification
{
    public static class RealDrawingVerificationMarker
    {
        public const string Value =
            "FOURJTOOLS_INTERNAL_REAL_DRAWING_VERIFY_V1";
    }
}
