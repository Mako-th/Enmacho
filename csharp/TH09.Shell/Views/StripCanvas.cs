using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TH09.Analysis;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;


internal enum StripCanvasRole
{
    Ticks,
    Strips,
}

internal sealed class StripCanvas : Control
{
    public const double StripHeight = 78;
    public const double LabelHeight = 13;
    public const double MinGap = 9;
    public const double Thumb = 8;

    public const double SepWidth = 1.5;


    public const double AxisY = 6.5;
    public const double BandY = 3, BandH = 7;
    public const double RowTop = 12, RowH = 9, RowGap = 12;
    public const double RowFontSize = 9;
    public const int RowMax = 0;
    public const double LabelDx = 6;
    public const double CardDotR = 3.5;
    public const double EventLineAlpha = 85.0 / 255.0;
    public const double DecidedAlpha = 0.55;
    public const double MarkHalfW = 4, MarkTop = 1, MarkBottom = 10;
    public const double TicksEmptyHeight = 14;

    public const double RecoveryLineY = AxisY - 4.5;
    public const double RecoveryLineWidth = 2;
    public const double RecoveryLineAlpha = 0.75;
    public const double RecoveryMarkTop = AxisY - 7.5, RecoveryMarkBottom = AxisY - 1.5;
    public static readonly double[] RecoveryDash = [3, 3];

    public const int MergeMin = 3, MergeDiv = 60;

    public static readonly string[] TickLabelTiers = ["hit", "card", "time_stop", "bonus"];

    public const double TickLabelChromaStep = 0.26;

    public static readonly Dictionary<string, string> TickLabelTierBySrc =
        new(StringComparer.Ordinal)
        {
            ["hit"] = "hit",
            ["gauge"] = "card",
            ["quick"] = "card",
            ["spell"] = "bonus",
            ["time_stop"] = "time_stop",
            ["boss"] = "bonus",
            ["reversal"] = "bonus",
        };

    public static readonly string[] FreezePointKinds =
        [FreezeSpans.KindRoundStart, FreezeSpans.KindRoundOver, FreezeSpans.KindRoundEnd];

    public const string HitMarkJa = "被弾";

    public static readonly (byte R, byte G, byte B, double A) FreezeBandDark = (110, 165, 215, 0.45);
    public static readonly (byte R, byte G, byte B, double A) FreezeBandLight = (60, 110, 170, 0.35);

    public static readonly StyledProperty<HitWindowViewModel?> ModelProperty =
        AvaloniaProperty.Register<StripCanvas, HitWindowViewModel?>(nameof(Model));

    public static readonly StyledProperty<int> TickProperty =
        AvaloniaProperty.Register<StripCanvas, int>(nameof(Tick));

    public static readonly StyledProperty<string> ThemeNameProperty =
        AvaloniaProperty.Register<StripCanvas, string>(nameof(ThemeName), "dark");

    public static readonly StyledProperty<StripCanvasRole> RoleProperty =
        AvaloniaProperty.Register<StripCanvas, StripCanvasRole>(nameof(Role), StripCanvasRole.Strips);

    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<StripCanvas, int>(nameof(Revision));

    static StripCanvas()
    {
        AffectsRender<StripCanvas>(ModelProperty, TickProperty, ThemeNameProperty, RoleProperty,
                                   RevisionProperty);
        AffectsMeasure<StripCanvas>(ModelProperty, ThemeNameProperty, RoleProperty, RevisionProperty);
    }

    public StripCanvas()
    {
        ClipToBounds = true;
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

    public StripCanvasRole Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var vm = Model;
        double w = double.IsInfinity(availableSize.Width) ? 520 : availableSize.Width;
        double h;
        if (Role == StripCanvasRole.Ticks)
        {
            h = vm?.Window is null ? TicksEmptyHeight
                                   : TickRowFor(vm, BoardPalette.Of(ThemeName), w).Height;
            _measuredHeight = h;
        }
        else
        {
            h = StripHeight * (vm?.Strips.Count ?? 0) + 4;
        }
        return new Size(w, h);
    }

    private double _measuredHeight = TicksEmptyHeight;

    private bool _remeasureAsked;

    public static double StripX(int n, int total, double width) =>
        Thumb + (total <= 1 ? 0 : (double)n / (total - 1)) * Math.Max(1, width - 2 * Thumb);

    public static string StripNum(double v)
    {
        if (Math.Abs(v) >= 10000)
        {
            return v % 10000 == 0
                ? (v / 10000).ToString("0.##", CultureInfo.InvariantCulture) + "万"
                : v.ToString("#,0.##", CultureInfo.InvariantCulture);
        }
        return v % 1 == 0 ? v.ToString("0", CultureInfo.InvariantCulture)
                          : v.ToString("0.0", CultureInfo.InvariantCulture);
    }

    public override void Render(DrawingContext ctx)
    {
        var vm = Model;
        if (vm?.Window is null) return;
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var T = BoardPalette.Of(ThemeName);
        if (Role == StripCanvasRole.Ticks) RenderTicks(ctx, vm, T, w, h);
        else RenderStrips(ctx, vm, T, w);
    }

    private void RenderTicks(DrawingContext ctx, HitWindowViewModel vm, BoardPalette T, double w, double h)
    {
        var row = TickRowFor(vm, T, w);
        if (Math.Abs(row.Height - _measuredHeight) > 0.5 && !_remeasureAsked)
        {
            _remeasureAsked = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _remeasureAsked = false;
                InvalidateMeasure();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        int n = row.N;
        double X(int m) => StripX(m, n, w);
        double sep = row.Height - 0.5;

        var band = FreezeBandBrush(T.Light);
        foreach (var b in row.Bands) ctx.FillRectangle(band, new Rect(b.X, BandY, b.W, BandH));

        ctx.DrawLine(new Pen(T.Frame, 1), new Point(X(0), AxisY), new Point(X(n - 1), AxisY));

        foreach (var d in row.Dots)
        {
            var c = new Point(d.X, AxisY);
            if (d.Fill) ctx.DrawEllipse(d.Brush, null, c, CardDotR, CardDotR);
            else ctx.DrawEllipse(null, new Pen(d.Brush, 1.4), c, CardDotR, CardDotR);
        }

        foreach (var r in row.Recoveries)
        {
            var pen = new Pen(T.Fade(r.Brush, RecoveryLineAlpha), RecoveryLineWidth,
                              r.Dashed ? new DashStyle(RecoveryDash, 0) : null);
            ctx.DrawLine(pen, new Point(r.X0, RecoveryLineY), new Point(r.X1, RecoveryLineY));
            if (r.MarkEnd)
                ctx.DrawLine(new Pen(r.Brush, 1),
                             new Point(r.X1, RecoveryMarkTop), new Point(r.X1, RecoveryMarkBottom));
        }

        if (row.Decided is double dx)
            ctx.DrawLine(new Pen(T.Fade(T.Frame, DecidedAlpha), 1),
                         new Point(dx, 0), new Point(dx, RowTop));

        ctx.DrawLine(new Pen(T.Dead, 1), new Point(X(0), sep), new Point(X(n - 1), sep));

        foreach (var e in row.EventLines)
            ctx.DrawLine(new Pen(e.Brush, 1), new Point(e.X, 0), new Point(e.X, RowTop));

        foreach (var m in row.Marks)
        {
            ctx.DrawLine(new Pen(m.Brush, 1),
                         new Point(Math.Round(m.X) + 0.5, 0), new Point(Math.Round(m.X) + 0.5, RowTop));
            var g = new StreamGeometry();
            using (var gc = g.Open())
            {
                gc.BeginFigure(new Point(m.X - MarkHalfW, MarkTop), true);
                gc.LineTo(new Point(m.X + MarkHalfW, MarkTop));
                gc.LineTo(new Point(m.X, MarkBottom));
                gc.EndFigure(true);
            }
            ctx.DrawGeometry(m.Brush, null, g);
        }

        double cx = Math.Round(X(vm.TickIndex)) + 0.5;
        ctx.DrawLine(new Pen(T.Frame, 1), new Point(cx, 0), new Point(cx, row.Height));

        if (row.Note is not null)
            ctx.DrawText(Text(row.Note, RowFontSize, T.Dead), new Point(X(0), RowTop));

        foreach (var L in row.Labels)
        {
            double ly = RowY(L.Row);
            ctx.FillRectangle(T.Bg, new Rect(L.X - 1, ly - 1, L.Text.Width + 2, RowH + 2));
            ctx.DrawText(L.Text, new Point(L.X, ly));
        }
    }

    public static double RowY(int r) => RowTop + r * RowGap;

    private void RenderStrips(DrawingContext ctx, HitWindowViewModel vm, BoardPalette T, double w)
    {
        int n = vm.TickCount;
        var axis = new Pen(T.Frame, 1);
        var rule = new Pen(T.Dead, 1);
        var linePen = new Pen(T.Side(vm.Side), 1.3);
        var cursor = new Pen(T.Frame, 1);
        var sub = T.Dead;

        var sep = new Pen(T.Frame, SepWidth);
        for (int k = 0; k <= vm.Strips.Count; k++)
        {
            double sy = Math.Round(k * StripHeight) + 0.5;
            ctx.DrawLine(sep, new Point(0, sy), new Point(w, sy));
        }

        for (int k = 0; k < vm.Strips.Count; k++)
        {
            var s = vm.Strips[k];
            double y0 = k * StripHeight + 6 + LabelHeight;
            double y1 = y0 + StripHeight - 14 - LabelHeight;
            double X(int m) => StripX(m, n, w);
            double Y(double v) => y1 - (v - s.Lo) / Math.Max(1e-9, s.Hi - s.Lo) * (y1 - y0);

            ctx.DrawLine(axis, new Point(X(0), y1 + 0.5), new Point(X(n - 1), y1 + 0.5));

            var ms = s.Marks.Where(m => m >= s.Lo && m <= s.Hi).OrderByDescending(m => m).ToArray();
            foreach (var m in ms)
            {
                double my = Math.Round(Y(m)) + 0.5;
                ctx.DrawLine(rule, new Point(X(0), my), new Point(X(n - 1), my));
            }
            var prio = ms.Length > 1
                ? new[] { ms[0], ms[^1] }.Concat(ms).ToArray()
                : ms;
            var taken = new List<double>();
            foreach (var m in prio)
            {
                double ty = Math.Round(Y(m)) + 1.5;
                if (taken.Any(t => Math.Abs(t - ty) < MinGap)) continue;
                taken.Add(ty);
                var ft = Text(StripNum(m), 9, sub);
                ctx.DrawText(ft, new Point(X(n - 1) - 2 - ft.Width, ty));
            }

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                bool pen = false;
                for (int m = 0; m < s.Values.Length && m < n; m++)
                {
                    var v = s.Values[m];
                    if (v is null) { if (pen) g.EndFigure(false); pen = false; continue; }
                    var p = new Point(X(m), Y(v.Value));
                    if (!pen) { g.BeginFigure(p, false); pen = true; }
                    else g.LineTo(p);
                }
                if (pen) g.EndFigure(false);
            }
            ctx.DrawGeometry(null, linePen, geo);

            ctx.DrawText(Text(s.Label, 10, sub), new Point(StripX(0, n, w), y0 - 13));

            if (s.Note is not null)
            {
                ctx.DrawText(Text(s.Note, 9, sub), new Point(StripX(0, n, w), (y0 + y1) / 2 - 6));
            }

            double cx = Math.Round(X(vm.TickIndex)) + 0.5;
            ctx.DrawLine(cursor, new Point(cx, y0), new Point(cx, y1));
        }
    }

    private static FormattedText Text(string s, double size, IBrush brush) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);


    internal readonly record struct TickLabel(FormattedText Text, string Raw, double X, int Row);

    internal readonly record struct RawLabel(double X, double? X2, string Text, IBrush Brush, double Prio);

    internal sealed class TickRow
    {
        public TH09.Analysis.Window Window = default!;
        public int Side, N;
        public double Width, Height;
        public string Theme = "";
        public List<(double X, double W)> Bands = [];
        public List<(double X, bool Fill, IBrush Brush)> Dots = [];
        public List<(double X, IBrush Brush)> EventLines = [];
        public List<(double X, IBrush Brush)> Marks = [];
        public List<(double X0, double X1, bool Dashed, bool MarkEnd, IBrush Brush)> Recoveries = [];
        public List<TickLabel> Labels = [];
        public double? Decided;
        public string? Note;
    }

    private TickRow? _row;

    private TickRow TickRowFor(HitWindowViewModel vm, BoardPalette T, double w)
    {
        var win = vm.Window!;
        if (_row is not null && ReferenceEquals(_row.Window, win) && _row.Side == vm.Side
            && _row.N == vm.TickCount && Math.Abs(_row.Width - w) < 0.01
            && string.Equals(_row.Theme, ThemeName, StringComparison.Ordinal))
            return _row;
        _row = BuildTickRow(vm, T, w);
        return _row;
    }

    private TickRow BuildTickRow(HitWindowViewModel vm, BoardPalette T, double w)
    {
        var win = vm.Window!;
        int n = vm.TickCount;
        var row = new TickRow
        {
            Window = win, Side = vm.Side, N = n, Width = w, Theme = ThemeName,
        };
        double X(int m) => StripX(m, n, w);
        var labs = new List<RawLabel>();

        var freeze = FreezeSpans.Of(win);
        if (!freeze.Readable) row.Note = HitWindowViewModel.NoFreezeWords;
        foreach (var sp in freeze.Spans)
        {
            int k0 = sp.Index, k1 = Math.Min(n, sp.Index + sp.Frames) - 1;
            if (k1 < k0) continue;
            row.Bands.Add((X(k0), Math.Max(2, X(k1) - X(k0))));
            if (sp.PointNamed) continue;
            var c = TickLabelBrush(T, sp.LabelSide, sp.LabelSrc);
            if (Array.IndexOf(FreezePointKinds, sp.Kind) >= 0)
            {
                labs.Add(new RawLabel(X(k0) + LabelDx, null, sp.Label, c, 0.9));
                continue;
            }
            labs.Add(new RawLabel(X(k0), X(k1), sp.Label, c, sp.Depth > 0 ? 0.5 : 0));
        }

        var cards = CardEvents.For(win);
        foreach (var e in cards.Cards())
        {
            if (e.Index < 0) continue;
            var c = TickLabelBrush(T, e.Side, CardEvents.SourceKey(e.Source));
            row.Dots.Add((X(e.Index), e.Source != CardSource.Spell, c));
            string[]? extra = GaugeRecovery.CancelsRecovery(win, e)
                ? [GaugeRecovery.CardNoteRecoveryLostJa] : null;
            labs.Add(new RawLabel(X(e.Index) + LabelDx, null,
                                  CardEvents.SideLabel(e.Side, cards.CardFullName(e, extra))!, c, 0.6));
        }

        foreach (var e in GaugeRecovery.HitPoints(win))
        {
            row.Dots.Add((X(e.Index), true, T.Side(e.Side)));
            labs.Add(new RawLabel(X(e.Index) + LabelDx, null,
                                  GaugeRecovery.RingPointLabel(win, e),
                                  TickLabelBrush(T, e.Side, "hit"), 0.7));
        }

        foreach (var e in GaugeRecovery.Events(win))
        {
            int a0 = Math.Max(0, e.Index), b0 = Math.Min(n - 1, e.Due);
            if (b0 < a0) continue;
            row.Recoveries.Add((X(a0), X(b0), e.Fired, !e.Outside, T.Side(e.Side)));
            if (GaugeRecovery.RecoveryPointLabel(win, e) is string rt)
                labs.Add(new RawLabel(X(a0) + LabelDx, null, rt, TickLabelBrush(T, e.Side, "hit"), 0.55));
        }

        if (WindowCut.RoundDecided(win) is int di)
        {
            row.Decided = Math.Round(X(di)) + 0.5;
            if (!freeze.Spans.Any(s => string.Equals(s.Kind, FreezeSpans.KindRoundOver,
                                                     StringComparison.Ordinal)))
                labs.Add(new RawLabel(row.Decided.Value + LabelDx, null,
                                      FreezeSpans.Label(FreezeSpans.KindRoundOver), T.Frame, 0.9));
        }

        foreach (var e in vm.Events)
            row.EventLines.Add((Math.Round(X(e.Index)) + 0.5,
                                T.Fade(T.Side(e.Side), EventLineAlpha)));
        foreach (var g in HitGroups(vm))
        {
            row.Marks.Add((X(g.Index), T.Side(g.Side)));
            if (!g.IsHit) continue;
            labs.Add(new RawLabel(X(g.Index) + LabelDx, null,
                                  CardEvents.SideLabel(g.Side, HitMarkJa)!,
                                  TickLabelBrush(T, g.Side, "hit"), 1.0));
        }

        row.Labels = PlaceRows(labs, RowMax, w);
        int rows = 1;
        foreach (var L in row.Labels) rows = Math.Max(rows, L.Row + 1);
        row.Height = RowY(rows - 1) + RowH + 3;
        return row;
    }

    private static List<(int Index, int Side, bool IsHit)> HitGroups(HitWindowViewModel vm)
    {
        int merge = Math.Max(MergeMin, (int)Math.Round(vm.TickCount / (double)MergeDiv,
                                                       MidpointRounding.AwayFromZero));
        var outv = new List<(int Index, int Side, bool IsHit)>();
        foreach (var e in vm.Events.OrderBy(x => x.Index))
        {
            bool hit = !TH09.Analysis.Window.NonHitTriggers.Contains(e.Trigger, StringComparer.Ordinal);
            if (outv.Count > 0)
            {
                var g = outv[^1];
                if (e.Index - g.Index <= merge && g.Side == e.Side)
                {
                    outv[^1] = (g.Index, g.Side, g.IsHit || hit);
                    continue;
                }
            }
            outv.Add((e.Index, e.Side, hit));
        }
        return outv;
    }

    internal static List<TickLabel> PlaceRows(List<RawLabel> labs, int rowMax, double width)
    {
        var cand = new List<(RawLabel L, FormattedText T, double X0, double X1, double Ord)>(labs.Count);
        foreach (var L in labs)
        {
            var ft = Text(L.Text, RowFontSize, L.Brush);
            double bw = ft.Width;
            double bas = L.X2 is double x2 ? Math.Max(0, (L.X + x2 - bw) / 2) : L.X;
            double x0 = width > 0 ? Math.Max(0, Math.Min(bas, width - bw - 2)) : bas;
            cand.Add((L, ft, x0, x0 + bw + 4, L.X));
        }
        static List<(RawLabel L, FormattedText T, double X0, double X1, double Ord)> ByX(
            IEnumerable<(RawLabel L, FormattedText T, double X0, double X1, double Ord)> xs) =>
            xs.OrderBy(c => c.Ord).ThenByDescending(c => c.L.Prio).ToList();

        static (int[] Rows, int Top) Level(
            List<(RawLabel L, FormattedText T, double X0, double X1, double Ord)> items)
        {
            var rows = new int[items.Count];
            int top = 0;
            for (int a = 0; a < items.Count; a++)
            {
                int r = 0;
                for (int b = 0; b < a; b++)
                    if (items[b].X1 > items[a].X0 && rows[b] + 1 > r) r = rows[b] + 1;
                rows[a] = r;
                if (r + 1 > top) top = r + 1;
            }
            return (rows, top);
        }

        var keep = ByX(cand);
        if (rowMax > 0)
        {
            keep = [];
            foreach (var c in cand.OrderByDescending(x => x.L.Prio).ThenBy(x => x.X0))
            {
                var t = ByX(keep.Append(c));
                if (Level(t).Top <= rowMax) keep = t;
            }
        }
        var lv = Level(keep);
        var outv = new List<TickLabel>(keep.Count);
        for (int k = 0; k < keep.Count; k++)
            outv.Add(new TickLabel(keep[k].T, keep[k].L.Text, keep[k].X0, lv.Rows[k]));
        return outv;
    }


    private static readonly Dictionary<(bool Light, int Side, string Tier), IBrush> TierBrushes = new();
    private static readonly Dictionary<bool, IBrush> BandBrushes = new();

    internal static IBrush FreezeBandBrush(bool light)
    {
        lock (BandBrushes)
        {
            if (BandBrushes.TryGetValue(light, out var got)) return got;
            var (r, g, b, a) = light ? FreezeBandLight : FreezeBandDark;
            var made = new SolidColorBrush(
                Color.FromArgb((byte)Math.Round(a * 255), r, g, b)).ToImmutable();
            BandBrushes[light] = made;
            return made;
        }
    }

    internal static IBrush TickLabelBrush(BoardPalette T, int? side, string? src)
    {
        if (side is null || src is null) return T.Frame;
        if (!TickLabelTierBySrc.TryGetValue(src, out var tier)) return T.Frame;
        int rank = Array.IndexOf(TickLabelTiers, tier);
        if (rank < 0) return T.Frame;
        if (rank == 0) return T.Side(side.Value);
        var key = (T.Light, side.Value, tier);
        lock (TierBrushes)
        {
            if (TierBrushes.TryGetValue(key, out var got)) return got;
            var c = ((ISolidColorBrush)T.Side(side.Value)).Color;
            var hex = string.Format(CultureInfo.InvariantCulture, "#{0:x2}{1:x2}{2:x2}",
                                    c.R, c.G, c.B);
            var lch = BoardPalette.OklchOf(hex);
            var made = new SolidColorBrush(Color.Parse(
                lch is null ? hex
                            : BoardPalette.OklchHex(lch.Value.L,
                                                    lch.Value.C * (1.0 - TickLabelChromaStep * rank),
                                                    lch.Value.H))).ToImmutable();
            TierBrushes[key] = made;
            return made;
        }
    }
}
