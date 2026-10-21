using System.Drawing;
using System.Windows.Forms;

namespace Autodesk.AutoCAD.ApplicationServices
{
    public sealed class Document
    {
        public void SendStringToExecute(string command, bool activate,
            bool wrapUpInactiveDocument, bool echoCommand)
        {
        }
    }

    public sealed class DocumentCollection
    {
        public Document MdiActiveDocument { get; set; }
    }
}

namespace Autodesk.AutoCAD.ApplicationServices.Core
{
    public static class Application
    {
        private static readonly Autodesk.AutoCAD.ApplicationServices.DocumentCollection
            Documents = new Autodesk.AutoCAD.ApplicationServices.DocumentCollection();

        public static Autodesk.AutoCAD.ApplicationServices.DocumentCollection DocumentManager
        {
            get { return Documents; }
        }

        public static void ShowAlertDialog(string message)
        {
        }
    }
}

namespace Autodesk.AutoCAD.Colors
{
    public enum ColorMethod
    {
        ByAci,
        ByColor
    }

    public sealed class Color
    {
        private Color(ColorMethod method, int index, System.Drawing.Color value)
        {
            ColorMethod = method;
            ColorIndex = index;
            ColorValue = value;
        }

        public ColorMethod ColorMethod { get; private set; }
        public int ColorIndex { get; private set; }
        public System.Drawing.Color ColorValue { get; private set; }

        public static Color FromRgb(byte red, byte green, byte blue)
        {
            return new Color(ColorMethod.ByColor, 0,
                System.Drawing.Color.FromArgb(red, green, blue));
        }

        public static Color FromColorIndex(ColorMethod method, short index)
        {
            System.Drawing.Color value;
            if (index == 3)
                value = System.Drawing.Color.Lime;
            else if (index == 6)
                value = System.Drawing.Color.Magenta;
            else
                value = System.Drawing.Color.White;
            return new Color(method, index, value);
        }
    }
}

namespace Autodesk.AutoCAD.Windows
{
    public sealed class ColorDialog
    {
        public bool IncludeByBlockByLayer { get; set; }
        public Autodesk.AutoCAD.Colors.Color Color { get; set; }

        public DialogResult ShowDialog()
        {
            return DialogResult.Cancel;
        }
    }
}
