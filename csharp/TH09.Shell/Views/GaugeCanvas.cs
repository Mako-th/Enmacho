using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;


internal sealed class GaugeCanvas : Control
{
    public const double BarHeight = 6;
    public const double BarGap = 13;
    public const double BarTop = 8;
    public const double PadLeft = 26;

    public const double CanvasHeight = 46;

    public const double FontSize = 10;

    public const string WidestLevels = "99 / 99";

    public const double PadRightSlack = 8;
    public const double ValueGap = 4;

    public const double FillAlpha = .55;
    public const double ChargeWidth = 3.6;

    public const double FrameWidth = 0.6;

    public static readonly StyledProperty<HitWindowViewModel?> ModelProperty =
        AvaloniaProperty.Register<GaugeCanvas, HitWindowViewModel?>(nameof(Model));

    public static readonly StyledProperty<int> TickProperty =
        AvaloniaProperty.Register<GaugeCanvas, int>(nameof(Tick));

    public static readonly StyledProperty<string> ThemeNameProperty =
        AvaloniaProperty.Register<GaugeCanvas, string>(nameof(ThemeName), "dark");

    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<GaugeCanvas, int>(nameof(Revision));

    public static readonly StyledProperty<int> OnlySideProperty =
        AvaloniaProperty.Register<GaugeCanvas, int>(nameof(OnlySide));

    static GaugeCanvas()
    {
        AffectsRender<GaugeCanvas>(ModelProperty, TickProperty, ThemeNameProperty, RevisionProperty,
                                   OnlySideProperty);
        AffectsMeasure<GaugeCanvas>(OnlySideProperty);
    }

    public HitWindowViewModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public int Tick
    {
        get => GetValue(TickProperty);
        set => SetValue(TickProperty, value);
    }

    public string ThemeName
    {
        get => GetValue(ThemeNameProperty);
        set => SetValue(ThemeNameProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public int OnlySide
    {
        get => GetValue(OnlySideProperty);
        set => SetValue(OnlySideProperty, value);
    }

    public static double HeightFor(int onlySide) =>
        onlySide == 0 ? CanvasHeight : CanvasHeight - BarGap;

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 374 : availableSize.Width;
        return new Size(w, HeightFor(OnlySide));
    }

    private static double? _padRight;

    private static double PadRight()
    {
        _padRight ??= Math.Ceiling(Text(WidestLevels, Brushes.Black).Width) + PadRightSlack;
        return _padRight.Value;
    }

    public override void Render(DrawingContext ctx)
    {
        var vm = Model;
        if (vm?.Window is null) return;
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double mx = vm.GaugeMax;
        if (mx <= 0) return;

        var g = vm.GaugeAt(Tick);
        if (g is null) return;

        var T = BoardPalette.Of(ThemeName);
        double barW = w - PadLeft - PadRight();
        if (barW <= 0) return;

        var framePen = new Pen(T.Frame, FrameWidth);
        var markPen = new Pen(T.Dead, 1);

        int lo = OnlySide == 0 ? 1 : OnlySide;
        int hi = OnlySide == 0 ? 2 : OnlySide;
        for (int sd = lo; sd <= hi; sd++)
        {
            double y = BarTop + (sd - lo) * BarGap;
            var one = g.Value.Of(sd);
            var col = T.Side(sd);

            ctx.DrawRectangle(null, framePen, new Rect(PadLeft + 0.5, y + 0.5, barW, BarHeight));

            foreach (var (value, _) in vm.GaugeMarks)
            {
                double x = Math.Round(PadLeft + barW * (value / mx)) + 0.5;
                ctx.DrawLine(markPen, new Point(x, y + 1), new Point(x, y + BarHeight - 1));
            }

            if (one.Gauge is double val)
            {
                ctx.FillRectangle(T.Fade(col, FillAlpha),
                    new Rect(PadLeft + 1, y + 1, Math.Max(0, barW * (val / mx) - 1), BarHeight - 1));
                var ft = Text(Math.Floor(val + 0.5).ToString("0", CultureInfo.InvariantCulture), col);
                ctx.DrawText(ft, new Point(PadLeft - ValueGap - ft.Width,
                                           y + BarHeight / 2 - ft.Height / 2));
            }

            if (one.Charge is double chg && chg > 0)
            {
                double cy = y + BarHeight + ChargeWidth;
                var pen = new Pen(col, ChargeWidth);
                ctx.DrawLine(pen, new Point(PadLeft + 1, cy),
                    new Point(PadLeft + 1 + Math.Max(0, barW * (Math.Min(chg, mx) / mx) - 1), cy));
            }

            var lvs = new List<string>(2);
            if (one.CardLevel is double lv)
                lvs.Add(Math.Floor(lv + 0.5).ToString("0", CultureInfo.InvariantCulture));
            if (one.BossCardLevel is double bl)
                lvs.Add(Math.Floor(bl + 0.5).ToString("0", CultureInfo.InvariantCulture));
            if (lvs.Count > 0)
            {
                var ft = Text(string.Join(" / ", lvs), T.Dead);
                ctx.DrawText(ft, new Point(w - ValueGap - ft.Width, y + BarHeight / 2 - ft.Height / 2));
            }
        }

        if (vm.GaugeJa.Length > 0)
        {
            var ft = Text(vm.GaugeJa, T.Dead);
            ctx.DrawText(ft, new Point(PadLeft, BarTop + BarGap + BarHeight + 12 - ft.Height / 2));
        }
    }

    private static FormattedText Text(string s, IBrush brush) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, FontSize, brush);
}
