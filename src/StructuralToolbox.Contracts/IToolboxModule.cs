using System.Drawing;
using System.Windows.Forms;

namespace StructuralToolbox.Contracts
{
    public interface IToolboxModule
    {
        string Id { get; }
        string DisplayName { get; }
        string HomeButtonName { get; }
        int DisplayOrder { get; }
        Size PreferredPaletteSize { get; }
        Control CreateControl();
    }
}
