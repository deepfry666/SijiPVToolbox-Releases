using System.Drawing;
using System.Windows.Forms;
using StructuralToolbox.Contracts;
using StructuralToolbox.Update.UI;

namespace StructuralToolbox.Update
{
    public sealed class UpdateToolboxModule : IToolboxModule
    {
        public string Id { get { return "online-update"; } }
        public string DisplayName { get { return "检查软件更新"; } }
        public string HomeButtonName { get { return "OpenOnlineUpdate"; } }
        public int DisplayOrder { get { return 300; } }
        public Size PreferredPaletteSize { get { return new Size(360, 580); } }

        public Control CreateControl()
        {
            return new UpdateCheckControl(UpdateSourceResolver.ResolveAll());
        }
    }
}
