using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace FlashStickNote.Controls;

public sealed class NoteTextEditor : TextEditor
{
    private readonly CaretRenderer _caretRenderer;

    public NoteTextEditor()
    {
        _caretRenderer = new CaretRenderer(TextArea);
        TextArea.TextView.BackgroundRenderers.Add(_caretRenderer);
        TextArea.Caret.PositionChanged += (_, _) => RedrawCaret();
        GotKeyboardFocus += (_, _) => RedrawCaret();
        LostKeyboardFocus += (_, _) => RedrawCaret();
    }

    public void ConfigureCaret(string? style, double width, System.Windows.Media.Brush brush)
    {
        _caretRenderer.Style = style?.Trim().ToLowerInvariant() switch
        {
            "block" => CaretStyle.Block,
            "underline" => CaretStyle.Underline,
            _ => CaretStyle.Line,
        };
        _caretRenderer.Width = Math.Clamp(width, 1.0, 12.0);
        _caretRenderer.Brush = brush;
        TextArea.Caret.CaretBrush = System.Windows.Media.Brushes.Transparent;
        RedrawCaret();
    }

    private void RedrawCaret()
        => TextArea.TextView.InvalidateLayer(KnownLayer.Caret);

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.X &&
            Keyboard.Modifiers == ModifierKeys.Control &&
            TextArea.Selection.IsEmpty &&
            Document != null &&
            Document.TextLength > 0)
        {
            var range = LineCutSelector.GetRange(Document.Text, TextArea.Caret.Offset);
            TextArea.Selection = Selection.Create(TextArea, range.StartOffset, range.EndOffset);
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

    private enum CaretStyle
    {
        Line,
        Block,
        Underline,
    }

    private sealed class CaretRenderer : IBackgroundRenderer
    {
        private readonly TextArea _textArea;

        public CaretRenderer(TextArea textArea)
        {
            _textArea = textArea;
        }

        public KnownLayer Layer => KnownLayer.Caret;

        public CaretStyle Style { get; set; } = CaretStyle.Line;

        public double Width { get; set; } = 2.0;

        public System.Windows.Media.Brush Brush { get; set; } = System.Windows.Media.Brushes.Black;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!_textArea.IsKeyboardFocused || !textView.VisualLinesValid)
            {
                return;
            }

            var rectangle = _textArea.Caret.CalculateCaretRectangle();
            rectangle.Y -= textView.VerticalOffset;
            if (rectangle.Bottom < 0 || rectangle.Top > textView.ActualHeight)
            {
                return;
            }

            rectangle = Style switch
            {
                CaretStyle.Block => new Rect(
                    rectangle.X,
                    rectangle.Y,
                    Math.Max(rectangle.Width, _textArea.TextView.DefaultLineHeight * 0.55),
                    rectangle.Height),
                CaretStyle.Underline => new Rect(
                    rectangle.X,
                    rectangle.Bottom - Width,
                    Math.Max(rectangle.Width, _textArea.TextView.DefaultLineHeight * 0.55),
                    Width),
                _ => new Rect(rectangle.X, rectangle.Y, Width, rectangle.Height),
            };
            drawingContext.DrawRectangle(Brush, null, rectangle);
        }
    }
}
