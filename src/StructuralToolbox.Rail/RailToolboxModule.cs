using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Contracts;
using StructuralToolbox.UI;

namespace StructuralToolbox.Rail
{
    public sealed class RailToolboxModule : IToolboxModule
    {
        public string Id { get { return "rail"; } }
        public string DisplayName { get { return "自动画导轨"; } }
        public string HomeButtonName { get { return "OpenRailTool"; } }
        public int DisplayOrder { get { return 100; } }
        public Size PreferredPaletteSize { get { return new Size(400, 720); } }

        public Control CreateControl()
        {
            return new ToolboxControl();
        }
    }
}
