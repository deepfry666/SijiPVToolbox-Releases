using Autodesk.AutoCAD.DatabaseServices;
using StructuralToolbox.DrawingTranslation.Core;

namespace StructuralToolbox.DrawingTranslation.Commands
{
    internal sealed class TranslationTarget
    {
        internal TranslationTarget(ObjectId objectId, int row, int column,
            DrawingTextItem item)
        {
            ObjectId = objectId;
            Row = row;
            Column = column;
            Item = item;
        }

        internal ObjectId ObjectId { get; private set; }
        internal int Row { get; private set; }
        internal int Column { get; private set; }
        internal DrawingTextItem Item { get; private set; }
        internal bool IsTableCell { get { return Row >= 0; } }
    }

    internal sealed class TranslationEdit
    {
        internal TranslationEdit(TranslationTarget target, string value)
        {
            Target = target;
            Value = value;
        }

        internal TranslationTarget Target { get; private set; }
        internal string Value { get; private set; }
    }
}
