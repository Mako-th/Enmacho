using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;


internal sealed class TrendPalette
{
    public static readonly string[] StatusColors =
        ["#5bc8ff", "#ff8a5b", "#8bd450", "#ffd166", "#c792ea", "#4dd0c1"];

    public static readonly double[][] Dashes =
        [[], [6, 4], [2, 3], [10, 4, 2, 4], [1, 3], [8, 3, 1, 3]];

    public static readonly double[] SideDash = [6, 4];

    private static readonly Dictionary<string, TrendPalette> Cache = new(StringComparer.Ordinal);

    public static TrendPalette Of(string themeName)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(themeName, out var p))
            {
                p = new TrendPalette(themeName == "light");
                Cache[themeName] = p;
            }
            return p;
        }
    }

    public IBrush Grid { get; }
    public IBrush Line { get; }
    public IBrush Rule { get; }
    public IBrush RuleFg { get; }
    public IBrush Sub { get; }
    public IBrush Fg { get; }

    private readonly IBrush[] _status;
    private readonly IBrush[] _statusFaint;

    public const byte FaintAlpha = 0x33;

    private TrendPalette(bool light)
    {
        Grid = Solid(light ? "#e6e8ee" : "#1e2330");
        Line = Solid(light ? "#d8dbe4" : "#272c38");
        Rule = Solid(light ? "#b9bfcc" : "#3a4152");
        RuleFg = Solid(light ? "#8b93a5" : "#5c6579");
        Sub = Solid(light ? "#666e80" : "#828a9e");
        Fg = Solid(light ? "#14161c" : "#e7e9f0");
        _status = StatusColors.Select(Solid).ToArray();
        _statusFaint = StatusColors.Select(h => Faint(h, FaintAlpha)).ToArray();
    }

    private static IBrush Solid(string hex) => new SolidColorBrush(Color.Parse(hex)).ToImmutable();

    private static IBrush Faint(string hex, byte alpha)
    {
        var c = Color.Parse(hex);
        return new ImmutableSolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }

    public IBrush Status(int bi) => _status[((bi % _status.Length) + _status.Length) % _status.Length];

    private static int SideIndex(int side) => side == 2 ? 1 : side == 1 ? 0 : 2;

    public IBrush SideColor(int side) => _status[SideIndex(side)];

    public IBrush SideFaint(int side) => _statusFaint[SideIndex(side)];

    public static IImmutableBrush BandFill(int side, double alpha)
    {
        var c = side == 2 ? (255, 138, 91) : (91, 200, 255);
        return new ImmutableSolidColorBrush(Color.FromArgb(
            (byte)Math.Round(Math.Min(0.95, alpha) * 255), (byte)c.Item1, (byte)c.Item2, (byte)c.Item3));
    }
}

internal sealed class TimelineCanvas : Control
{
    public const double Font = 11;
    public const double LineWidth = 1.6;
    public const double Marker = 4;
    public const double OverlayPlotH = 420;
    public const double StackPanelH = 130;

    public static readonly StyledProperty<TimelineViewModel?> ModelProperty =
        AvaloniaProperty.Register<TimelineCanvas, TimelineViewModel?>(nameof(Model));

    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<TimelineCanvas, int>(nameof(Revision));

    public static readonly StyledProperty<string> ThemeNameProperty =
        AvaloniaProperty.Register<TimelineCanvas, string>(nameof(ThemeName), "dark");

    static TimelineCanvas()
    {
        AffectsRender<TimelineCanvas>(ModelProperty, RevisionProperty, ThemeNameProperty);
        AffectsMeasure<TimelineCanvas>(ModelProperty, RevisionProperty);
    }

    public TimelineViewModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public string ThemeName
    {
        get => GetValue(ThemeNameProperty);
        set => SetValue(ThemeNameProperty, value);
    }

    public int LastDrawnPoints { get; private set; }

    public static double SoloPlotH => Math.Round(Font * 13);

    public double SoloGap => LineHeight * 3 + 10;

    public const double SoloTop = 16;

    public const int SoloTicks = 4;

    private const double SoloLabelGap = 6;

    private double LineHeight => Math.Round(Font * 1.35);
    private double BandHeight => Math.Max(12, Math.Round(Font * 1.45));

    private bool IsSoloMode => Model?.IsSolo == true;
    private double PadL => Math.Round((IsSoloMode ? 6.2 : 5.2) * Font);
    private double PadR => Math.Round((IsSoloMode ? 2.0 : 1.4) * Font);
    private double PadT => Math.Round(Font * 1.1);

    private static List<List<TrendSeries>> ByBase(TimelineViewModel vm)
    {
        var outList = new List<List<TrendSeries>>();
        var at = new Dictionary<string, List<TrendSeries>>(StringComparer.Ordinal);
        foreach (var s in vm.Series)
        {
            if (!s.IsOn) continue;
            if (!at.TryGetValue(s.Base, out var list))
            {
                at[s.Base] = list = [];
                outList.Add(list);
            }
            list.Add(s);
        }
        return outList;
    }

    private double PlotHeight(TimelineViewModel vm)
        => vm.IsOverlay ? OverlayPlotH
         : vm.IsSolo ? Math.Max(1, ByBase(vm).Count) * (SoloPlotH + SoloGap)
         : StackPanelH * Math.Max(1, ByBase(vm).Count);

    private double BandArea(TimelineViewModel vm)
    {
        int n = vm.Bands.Count(b => b.IsOn);
        return n == 0 ? 0 : n * (BandHeight + 4) + 8;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var vm = Model;
        double w = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        if (vm is null || !vm.HasRound) return new Size(w, 60);
        double h = PadT + PlotHeight(vm) + LineHeight + 4 + BandArea(vm) + 6;
        return new Size(w, h);
    }

    public override void Render(DrawingContext ctx)
    {
        LastDrawnPoints = 0;
        var vm = Model;
        if (vm is null || !vm.HasRound) return;
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var T = TrendPalette.Of(ThemeName);

        double x0 = PadL, x1 = w - PadR, padT = PadT;
        double plotH = PlotHeight(vm), lh = LineHeight;
        double t0 = vm.T0, t1 = vm.T1;
        double X(double t) => x0 + (t - t0) / Math.Max(1e-9, t1 - t0) * (x1 - x0);

        if (!vm.IsSolo) DrawTimeAxis(ctx, T, t0, t1, X, padT, padT + plotH, x0);

        var panels = new List<(double Y0, double Y1, List<TrendSeries> List)>();
        if (vm.IsOverlay)
        {
            panels.Add((padT, padT + plotH, vm.Series.Where(s => s.IsOn).ToList()));
        }
        else if (vm.IsSolo)
        {
            var groups = ByBase(vm);
            for (int k = 0; k < groups.Count; k++)
            {
                double top = padT + k * (SoloPlotH + SoloGap) + SoloTop;
                panels.Add((top, top + SoloPlotH, groups[k]));
            }
        }
        else
        {
            var groups = ByBase(vm);
            double each = plotH / Math.Max(1, groups.Count);
            for (int k = 0; k < groups.Count; k++)
                panels.Add((padT + k * each, padT + (k + 1) * each - 10, groups[k]));
        }

        foreach (var (y0, y1, list) in panels)
        {
            if (vm.IsSolo) DrawTimeAxis(ctx, T, t0, t1, X, y0, y1, x0);
            ctx.DrawLine(new Pen(T.Line, 1), new Point(x0, y1 + 0.5), new Point(x1, y1 + 0.5));
            if (vm.IsSolo)
                ctx.DrawLine(new Pen(T.Line, 1), new Point(x0 + 0.5, y0), new Point(x0 + 0.5, y1));
            foreach (var s in list)
            {
                double Y(double v) => y1 - (v - s.Lo) / Math.Max(1e-9, s.Hi - s.Lo) * (y1 - y0);
                foreach (var (mv, mt) in s.Marks)
                {
                    if (mv < s.Lo || mv > s.Hi) continue;
                    double yy = Math.Round(Y(mv)) + 0.5;
                    ctx.DrawLine(new Pen(T.Rule, 1), new Point(x0, yy), new Point(x1, yy));
                    if (mt.Length == 0) continue;
                    var ft = Text(mt, Font, T.RuleFg);
                    ctx.DrawText(ft, new Point(x1 - 3 - ft.Width, yy - 3 - ft.Height * 0.5));
                }
                DrawSeries(ctx, vm, T, s, X, Y);
                if (!vm.IsOverlay && ReferenceEquals(list[0], s))
                    DrawAxisLabels(ctx, T, s, vm.IsSolo, Y, x0, x1, y0, y1, lh);
            }
        }

        DrawEvents(ctx, vm, T, X, padT, plotH);
        DrawBands(ctx, vm, T, X, x0, x1, padT + plotH + lh + 4);
    }

    private void DrawTimeAxis(DrawingContext ctx, TrendPalette T, double t0, double t1,
                              Func<double, double> X, double yTop, double yBot, double x0)
    {
        double span = t1 - t0;
        double step = span > 120 ? 30 : span > 60 ? 10 : span > 20 ? 5 : 1;
        var pen = new Pen(T.Grid, 1);
        for (double t = Math.Ceiling(t0 / step) * step; t <= t1; t += step)
        {
            double px = Math.Round(X(t)) + 0.5;
            ctx.DrawLine(pen, new Point(px, yTop), new Point(px, yBot));
            var ft = Text(AxisTime(t), Font, T.Sub);
            ctx.DrawText(ft, new Point(px - ft.Width * 0.5, yBot + 2));
        }
    }

    private void DrawSeries(DrawingContext ctx, TimelineViewModel vm, TrendPalette T,
                            TrendSeries s, Func<double, double> X, Func<double, double> Y)
    {
        var brush = vm.ByStatus ? T.Status(s.Bi) : T.SideColor(s.Side);
        var dash = vm.ByStatus
            ? (s.Side == 2 ? TrendPalette.SideDash : [])
            : TrendPalette.Dashes[((s.Bi % 6) + 6) % 6];
        var pen = new Pen(brush, LineWidth, dash.Length == 0 ? null : new DashStyle(dash, 0),
                          PenLineCap.Round, PenLineJoin.Round);

        var geo = new StreamGeometry();
        int drawn = 0;
        using (var g = geo.Open())
        {
            bool open = false;
            for (int k = 0; k < s.X.Length; k++)
            {
                if (s.Y[k] is not double v) { if (open) g.EndFigure(false); open = false; continue; }
                var p = new Point(X(s.X[k]), Y(v));
                if (!open) { g.BeginFigure(p, false); open = true; }
                else g.LineTo(p);
                drawn++;
            }
            if (open) g.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geo);
        LastDrawnPoints += drawn;
    }

    private void DrawAxisLabels(DrawingContext ctx, TrendPalette T, TrendSeries s, bool solo,
                                Func<double, double> Y, double x0, double x1,
                                double y0, double y1, double lh)
    {
        if (solo)
        {
            var grid = new Pen(T.Grid, 1);
            for (int k = 0; k <= SoloTicks; k++)
            {
                double v = s.Lo + (s.Hi - s.Lo) * k / SoloTicks;
                double yy = Math.Round(Y(v)) + 0.5;
                var ft = Text(Fmt(v), Font, T.Sub);
                ctx.DrawText(ft, new Point(x0 - 6 - ft.Width, yy - ft.Height * 0.5));
                if (k > 0) ctx.DrawLine(grid, new Point(x0, yy), new Point(x1, yy));
            }
        }
        else
        {
            var hi = Text(Fmt(s.Hi), Font, T.Sub);
            var lo = Text(Fmt(s.Lo), Font, T.Sub);
            ctx.DrawText(hi, new Point(x0 - 6 - hi.Width, y0));
            ctx.DrawText(lo, new Point(x0 - 6 - lo.Width, y1 - lo.Height));
        }
        string label = s.Label.Length > 3 && s.Label[1] == 'P' && s.Label[2] == ' '
                       ? s.Label[3..] : s.Label;
        var name = Text(label, Font + 1, T.Fg);
        ctx.DrawText(name, new Point(x0, solo ? y0 - SoloLabelGap - name.Height
                                                : y0 + lh - name.Baseline));
    }

    private void DrawEvents(DrawingContext ctx, TimelineViewModel vm, TrendPalette T,
                            Func<double, double> X, double padT, double plotH)
    {
        foreach (var row in vm.Events)
        {
            if (!row.IsOn) continue;
            var c = T.SideColor(row.Side);
            var faint = new Pen(T.SideFaint(row.Side), 1);
            double lane = padT + (row.Side == 2 ? Marker * 2.5 : 1), m = Marker;
            foreach (var t in row.Event.Times)
            {
                double px = Math.Round(X(t)) + 0.5;
                ctx.DrawLine(faint, new Point(px, padT), new Point(px, padT + plotH));
                ctx.DrawGeometry(c, null, Glyph(row.Glyph, px, lane, m));
            }
        }
    }

    private static StreamGeometry Glyph(string glyph, double px, double lane, double m)
    {
        var geo = new StreamGeometry();
        using var g = geo.Open();
        switch (glyph)
        {
            case "hit":
                g.BeginFigure(new Point(px - m, lane), true);
                g.LineTo(new Point(px + m, lane));
                g.LineTo(new Point(px, lane + m * 1.5));
                break;
            case "rev":
                g.BeginFigure(new Point(px - m, lane + m * 1.5), true);
                g.LineTo(new Point(px + m, lane + m * 1.5));
                g.LineTo(new Point(px, lane));
                break;
            case "card":
                g.BeginFigure(new Point(px - m * 0.75, lane), true);
                g.LineTo(new Point(px + m * 0.75, lane));
                g.LineTo(new Point(px + m * 0.75, lane + m * 1.5));
                g.LineTo(new Point(px - m * 0.75, lane + m * 1.5));
                break;
            case "boss":
                g.BeginFigure(new Point(px, lane), true);
                g.LineTo(new Point(px + m, lane + m * 0.75));
                g.LineTo(new Point(px, lane + m * 1.5));
                g.LineTo(new Point(px - m, lane + m * 0.75));
                break;
            default:
                double r = m * 0.6, cy = lane + m * 0.75;
                g.BeginFigure(new Point(px - r, cy), true);
                g.ArcTo(new Point(px + r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
                g.ArcTo(new Point(px - r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
                break;
        }
        g.EndFigure(true);
        return geo;
    }

    private void DrawBands(DrawingContext ctx, TimelineViewModel vm, TrendPalette T,
                           Func<double, double> X, double x0, double x1, double top)
    {
        double by = top, bh = BandHeight;
        foreach (var row in vm.Bands)
        {
            if (!row.IsOn) continue;
            var name = Text(row.Name, Font, T.Sub);
            ctx.DrawText(name, new Point(x0 - 6 - name.Width, by + bh * 0.5 - name.Height * 0.5));
            foreach (var run in row.Band.Runs)
            {
                var fill = TrendPalette.BandFill(row.Side, 0.45 + 0.18 * run.Value);
                double a = X(run.Start), z = X(run.End);
                ctx.FillRectangle(fill, new Rect(a, by, Math.Max(1, z - a), bh));
            }
            ctx.DrawRectangle(null, new Pen(T.Line, 1),
                              new Rect(x0 + 0.5, by + 0.5, Math.Max(1, x1 - x0 - 1), bh - 1));
            by += bh + 4;
        }
    }

    public static string Fmt(double v)
    {
        double a = Math.Abs(v);
        if (a >= 1e6) return (v / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        if (a >= 1e4) return (v / 1e3).ToString("0", CultureInfo.InvariantCulture) + "k";
        return (Math.Floor(v * 10 + 0.5) / 10).ToString("0.#", CultureInfo.InvariantCulture);
    }

    public static string AxisTime(double sec)
    {
        long t = (long)Math.Floor(sec + 0.5);
        long m = t / 60, r = t - m * 60;
        return m.ToString(CultureInfo.InvariantCulture) + ":"
               + r.ToString("00", CultureInfo.InvariantCulture);
    }

    private static FormattedText Text(string s, double size, IBrush brush) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
}
