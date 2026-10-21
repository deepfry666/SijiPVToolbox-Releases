using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Ballast.UI;
using StructuralToolbox.Contracts;

namespace StructuralToolbox.Ballast
{
    public sealed class BallastToolboxModule : IToolboxModule
    {
        public string Id { get { return "ballast"; } }
        public string DisplayName { get { return "自动布置配重"; } }
        public string HomeButtonName { get { return "OpenBallastTool"; } }
        public int DisplayOrder { get { return 200; } }
        public Size PreferredPaletteSize { get { return new Size(420, 760); } }

        public Control CreateControl()
        {
            BallastToolboxControl control = new BallastToolboxControl();
            BallastWorkflowBridge.Attach(control);
            return control;
        }
    }
}
