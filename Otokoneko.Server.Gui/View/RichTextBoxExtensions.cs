using System.Windows.Controls;
using System.Windows.Documents;

namespace Otokoneko.Server.Gui
{
    internal static class RichTextBoxExtensions
    {
        public static int GetCaretPosition(this RichTextBox richTextBox)
        {
            return richTextBox.Document.ContentStart.GetOffsetToPosition(richTextBox.CaretPosition);
        }

        public static int GetEndPosition(this RichTextBox richTextBox)
        {
            return richTextBox.Document.ContentStart.GetOffsetToPosition(richTextBox.Document.ContentEnd);
        }

        public static TextPointer GetEndPointer(this RichTextBox richTextBox)
        {
            return richTextBox.Document.ContentEnd;
        }

        public static TextPointer GetPointerAt(this RichTextBox richTextBox, int position)
        {
            return richTextBox.Document.ContentStart.GetPositionAtOffset(position);
        }

        public static void SetCaretToEnd(this RichTextBox richTextBox)
        {
            richTextBox.CaretPosition = richTextBox.GetEndPointer();
        }
    }
}
