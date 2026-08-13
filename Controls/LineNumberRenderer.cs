using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Border = System.Windows.Controls.Border;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Canvas = System.Windows.Controls.Canvas;
using Size = System.Windows.Size;
using TextAlignment = System.Windows.TextAlignment;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using TextWrapping = System.Windows.TextWrapping;

namespace FlashStickNote.Controls;

public sealed class LineNumberRenderer
{
    private readonly TextBox _box;
    private readonly Canvas _host;
    private readonly Border _panel;
    private ScrollViewer? _scrollViewer;
    private TextBlock? _measureBlock;
    private List<double>? _starts;
    private string _measuredText = "\u0001";
    private double _measuredWidth = -1;
    private double _numberWidth = 24;
    private Brush _foreground = Brushes.Gray;

    public LineNumberRenderer(TextBox box, Canvas host, Border panel)
    {
        _box = box;
        _host = host;
        _panel = panel;
    }

    public void SetScrollViewer(ScrollViewer? viewer) => _scrollViewer = viewer;

    public void SetForeground(Brush brush) => _foreground = brush;

    public void Invalidate()
    {
        _measuredText = "\u0001";
        _measuredWidth = -1;
    }

    public void Update()
    {
        if (_panel.Visibility != Visibility.Visible)
        {
            return;
        }

        var text = _box.Text ?? "";
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var offset = _scrollViewer?.VerticalOffset ?? 0;
        var viewport = _scrollViewer?.ViewportHeight ?? 400;
        var wrap = _box.TextWrapping == TextWrapping.Wrap;
        var lineHeight = MeasureLineHeight();
        var width = _scrollViewer?.ViewportWidth ?? 0;
        var topPadding = _box.Padding.Top;

        List<double> starts;
        if (wrap)
        {
            if (_measuredText != text || Math.Abs(_measuredWidth - width) > 0.5)
            {
                _measuredText = text;
                _measuredWidth = width;
                _starts = MeasureLineStarts(text, lineHeight, width);
            }

            starts = _starts ?? new List<double> { 0 };
        }
        else
        {
            _starts = null;
            var logical = text.Length == 0 ? 1 : text.Split('\n').Length;
            starts = new List<double>(logical);
            for (var i = 0; i < logical; i++)
            {
                starts.Add(i * lineHeight);
            }
        }

        EnsurePanelWidth(starts.Count);
        Rebuild(starts, topPadding, offset, viewport, lineHeight);
    }

    private double MeasureLineHeight()
    {
        try
        {
            return MeasureTextHeight("0", null);
        }
        catch
        {
            return _box.FontSize * 1.4;
        }
    }

    private double MeasureTextHeight(string text, double? width)
    {
        if (_measureBlock == null)
        {
            _measureBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        }

        _measureBlock.FontFamily = _box.FontFamily;
        _measureBlock.FontSize = _box.FontSize;
        _measureBlock.Text = text.Length == 0 ? " " : text;
        _measureBlock.Width = width ?? double.NaN;
        _measureBlock.Measure(new Size(width ?? double.PositiveInfinity, double.PositiveInfinity));
        return _measureBlock.DesiredSize.Height;
    }

    private List<double> MeasureLineStarts(string text, double lineHeight, double maxWidth)
    {
        var starts = new List<double>();
        var lines = text.Length == 0 ? new[] { "" } : text.Split('\n');
        var y = 0.0;
        foreach (var line in lines)
        {
            starts.Add(y);
            y += Math.Max(MeasureTextHeight(line, maxWidth), lineHeight);
        }

        return starts;
    }

    private void EnsurePanelWidth(int count)
    {
        try
        {
            if (_measureBlock == null)
            {
                _measureBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
            }

            _measureBlock.FontFamily = _box.FontFamily;
            _measureBlock.FontSize = _box.FontSize;
            _measureBlock.Text = Math.Max(1, count).ToString();
            _measureBlock.Width = double.NaN;
            _measureBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var panelWidth = Math.Ceiling(_measureBlock.DesiredSize.Width + 20);
            _panel.Width = Math.Max(36, panelWidth);
            _numberWidth = Math.Max(10, panelWidth - 12);
        }
        catch
        {
        }
    }

    private void Rebuild(List<double> starts, double topPadding, double offset, double viewport, double lineHeight)
    {
        _host.Children.Clear();
        for (var k = 0; k < starts.Count; k++)
        {
            var top = topPadding + starts[k] - offset;
            var bottom = topPadding + (k + 1 < starts.Count ? starts[k + 1] : starts[k] + lineHeight) - offset;
            if (bottom < -lineHeight || top > viewport + lineHeight)
            {
                continue;
            }

            var number = new TextBlock
            {
                Text = (k + 1).ToString(),
                TextAlignment = TextAlignment.Right,
                Width = _numberWidth,
                FontFamily = _box.FontFamily,
                FontSize = _box.FontSize,
                Foreground = _foreground,
            };
            Canvas.SetTop(number, top);
            _host.Children.Add(number);
        }
    }
}
