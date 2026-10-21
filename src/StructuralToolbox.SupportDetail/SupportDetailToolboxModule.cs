using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Contracts;
using StructuralToolbox.SupportDetail.UI;

namespace StructuralToolbox.SupportDetail
{
    public sealed class SupportDetailToolboxModule : IToolboxModule
    {
        public string Id { get { return "support-detail"; } }
        public string DisplayName { get { return "混凝土屋面支架大样"; } }
        public string HomeButtonName { get { return "OpenSupportDetailTool"; } }
        public int DisplayOrder { get { return 300; } }
        public Size PreferredPaletteSize { get { return new Size(460, 780); } }

        public Control CreateControl()
        {
            return new SupportDetailToolboxControl();
        }
    }
}
