using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace FlashStickNote.Controls;

public sealed class NoteTextEditor : TextEditor
{
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.X &&
            Keyboard.Modifiers == ModifierKeys.Control &&
            TextArea.Selection.IsEmpty &&
            Document != null &&
            Document.TextLength > 0)
        {
            var line = Document.GetLineByOffset(TextArea.Caret.Offset);
            var startOffset = line.Offset;
            var endOffset = line.EndOffset + line.DelimiterLength;

            // The final empty line has no own delimiter, so select the prior delimiter too.
            if (startOffset == endOffset && line.PreviousLine != null)
            {
                startOffset = line.PreviousLine.EndOffset;
            }

            TextArea.Selection = Selection.Create(TextArea, startOffset, endOffset);
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down &&
            (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt)) == 0 &&
            !TextArea.Selection.IsEmpty)
        {
            var toStart = e.Key is Key.Left or Key.Up;
            var segment = TextArea.Selection.SurroundingSegment;
            var offset = toStart ? segment.Offset : segment.EndOffset;
            TextArea.ClearSelection();
            TextArea.Caret.Offset = offset;
            TextArea.Caret.BringCaretToView();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
