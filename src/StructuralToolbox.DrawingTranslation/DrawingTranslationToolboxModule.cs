using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Contracts;
using StructuralToolbox.DrawingTranslation.UI;

namespace StructuralToolbox.DrawingTranslation
{
    public sealed class DrawingTranslationToolboxModule : IToolboxModule
    {
        public string Id { get { return "drawing-translation"; } }
        public string DisplayName { get { return "图纸文字翻译"; } }
        public string HomeButtonName { get { return "OpenDrawingTranslationTool"; } }
        public int DisplayOrder { get { return 250; } }
        public Size PreferredPaletteSize { get { return new Size(760, 720); } }

        public Control CreateControl()
        {
            return new DrawingTranslationToolboxControl();
        }
    }
}
