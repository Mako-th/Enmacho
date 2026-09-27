using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace TH09.Shell.Views;

internal sealed class RowRuledWrapPanel : Panel
{
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<RowRuledWrapPanel, double>(nameof(RowGap), 10);

    public static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<RowRuledWrapPanel, IBrush?>(nameof(RuleBrush));

    static RowRuledWrapPanel()
    {
        AffectsMeasure<RowRuledWrapPanel>(RowGapProperty);
        AffectsArrange<RowRuledWrapPanel>(RuleBrushProperty);
    }

    public double RowGap
    {
        get => GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    public IBrush? RuleBrush
    {
        get => GetValue(RuleBrushProperty);
        set => SetValue(RuleBrushProperty, value);
    }

    private readonly List<double> _rowTops = new();

    private readonly List<Rectangle> _rules = new();

    private List<(double Top, double Height, int First, int Count)> Rows(double width)
    {
        var rows = new List<(double, double, int, int)>();
        double x = 0, y = 0, rowH = 0;
        int first = 0, count = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            var c = Children[i];
            if (!c.IsVisible) { if (count == 0) first = i + 1; continue; }
            var s = c.DesiredSize;
            if (count > 0 && x + s.Width > width)
            {
                rows.Add((y, rowH, first, count));
                y += rowH + RowGap;
                x = 0; rowH = 0; first = i; count = 0;
            }
            x += s.Width;
            rowH = Math.Max(rowH, s.Height);
            count++;
        }
        if (count > 0) rows.Add((y, rowH, first, count));
        return rows;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var c in Children) c.Measure(availableSize);
        var rows = Rows(availableSize.Width);
        double w = 0, h = 0;
        foreach (var (top, height, first, count) in rows)
        {
            double rw = 0;
            for (int i = first; i < first + count; i++)
                if (Children[i].IsVisible) rw += Children[i].DesiredSize.Width;
            w = Math.Max(w, rw);
            h = top + height;
        }
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _rowTops.Clear();
        var rows = Rows(finalSize.Width);
        foreach (var (top, height, first, count) in rows)
        {
            if (top > 0) _rowTops.Add(top);
            double x = 0;
            for (int i = first; i < first + count; i++)
            {
                var c = Children[i];
                if (!c.IsVisible) continue;
                var s = c.DesiredSize;
                c.Arrange(new Rect(x, top, s.Width, height));
                x += s.Width;
            }
        }
        ArrangeRules(finalSize.Width);
        return finalSize;
    }

    private void ArrangeRules(double width)
    {
        int want = RuleBrush is null ? 0 : _rowTops.Count;
        while (_rules.Count < want)
        {
            var r = new Rectangle { Height = 1, IsHitTestVisible = false };
            _rules.Add(r);
            VisualChildren.Add(r);
        }
        while (_rules.Count > want)
        {
            var r = _rules[^1];
            _rules.RemoveAt(_rules.Count - 1);
            VisualChildren.Remove(r);
        }
        for (int i = 0; i < want; i++)
        {
            double y = Math.Round(_rowTops[i] - RowGap * 0.5);
            var r = _rules[i];
            r.Fill = RuleBrush;
            r.Measure(new Size(width, 1));
            r.Arrange(new Rect(0, y, width, 1));
        }
    }
}
