using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TH09.Analysis;
using TH09.Shell.ViewModels;

namespace TH09.Shell.Views;


internal sealed class BoardCanvas : Control
{
    public static readonly StyledProperty<HitWindowViewModel?> ModelProperty =
        AvaloniaProperty.Register<BoardCanvas, HitWindowViewModel?>(nameof(Model));

    public static readonly StyledProperty<int> TickProperty =
        AvaloniaProperty.Register<BoardCanvas, int>(nameof(Tick));

    public static readonly StyledProperty<string> ThemeNameProperty =
        AvaloniaProperty.Register<BoardCanvas, string>(nameof(ThemeName), "dark");

    public static readonly StyledProperty<string> ColorByProperty =
        AvaloniaProperty.Register<BoardCanvas, string>(nameof(ColorBy), "origin");

    public static readonly StyledProperty<string> ExTrailProperty =
        AvaloniaProperty.Register<BoardCanvas, string>(nameof(ExTrail), "hit");

    public static readonly StyledProperty<int> ColorSpreadProperty =
        AvaloniaProperty.Register<BoardCanvas, int>(nameof(ColorSpread));

    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<BoardCanvas, int>(nameof(Revision));

    static BoardCanvas()
    {
        AffectsRender<BoardCanvas>(ModelProperty, TickProperty, ThemeNameProperty,
                                   ColorByProperty, ExTrailProperty, ColorSpreadProperty,
                                   RevisionProperty);
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

    public string ColorBy
    {
        get => GetValue(ColorByProperty);
        set => SetValue(ColorByProperty, value);
    }

    public string ExTrail
    {
        get => GetValue(ExTrailProperty);
        set => SetValue(ExTrailProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public int ColorSpread
    {
        get => GetValue(ColorSpreadProperty);
        set => SetValue(ColorSpreadProperty, value);
    }

    public int LastDrawnBullets { get; private set; }
    public int LastDrawnEnemies { get; private set; }
    public int LastDrawnLasers { get; private set; }

    public int LastDrawnRings { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var vm = Model;
        return vm is null ? new Size(0, 0) : new Size(vm.BoardWidth, vm.BoardHeight);
    }

    public override void Render(DrawingContext ctx)
    {
        var vm = Model;
        var win = vm?.Window;
        if (vm is null || win is null) return;
        var T = BoardPalette.Of(ThemeName);
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double fw = HitWindowViewModel.FieldX1 - HitWindowViewModel.FieldX0;
        double fh = HitWindowViewModel.FieldY1 - HitWindowViewModel.FieldY0;
        double ss = Math.Min((w - HitWindowViewModel.Pad * 2) / fw,
                             (h - HitWindowViewModel.Pad * 2) / fh);
        if (ss <= 0) return;
        double ox = (w - fw * ss) / 2.0, oy = (h - fh * ss) / 2.0;
        double PX(double x) => ox + (x - HitWindowViewModel.FieldX0) * ss;
        double PY(double y) => oy + (y - HitWindowViewModel.FieldY0) * ss;

        int i = Tick;
        LastDrawnRings = 0;
        ctx.FillRectangle(T.BoardBg, new Rect(0, 0, w, h));


        var freeze = FreezeSpans.Of(win);
        var freezeSpan = freeze.Readable ? FreezeSpans.At(freeze, i) : null;
        if (freezeSpan is not null && vm.Emph != "frame")
            ctx.FillRectangle(T.FreezeTint, new Rect(0, 0, w, h));

        bool hit = vm.Events.Any(e => e.Index == i && e.Side == vm.Side);
        if (hit && vm.Emph != "frame")
        {
            ctx.FillRectangle(vm.Side == 2 ? T.HitTint2 : T.HitTint1, new Rect(0, 0, w, h));
        }

        var frame = new Pen(T.Frame, 1.5);
        ctx.DrawRectangle(null, frame,
            new Rect(PX(HitWindowViewModel.FieldX0) + 0.5, PY(HitWindowViewModel.FieldY0) + 0.5,
                     PX(HitWindowViewModel.FieldX1) - PX(HitWindowViewModel.FieldX0),
                     PY(HitWindowViewModel.FieldY1) - PY(HitWindowViewModel.FieldY0)));
        if (vm.Emph == "frame" && hit)
        {
            ctx.DrawRectangle(null, new Pen(T.Side(vm.Side), 3),
                new Rect(PX(HitWindowViewModel.FieldX0) + 1.5, PY(HitWindowViewModel.FieldY0) + 1.5,
                         PX(HitWindowViewModel.FieldX1) - PX(HitWindowViewModel.FieldX0) - 2,
                         PY(HitWindowViewModel.FieldY1) - PY(HitWindowViewModel.FieldY0) - 2));
        }

        var board = vm.CurrentBoard;
        var self = vm.SelfAt(i);
        double selfSide = vm.SelfSide(i);

        var clipRect = new Rect(PX(HitWindowViewModel.FieldX0), PY(HitWindowViewModel.FieldY0),
                                PX(HitWindowViewModel.FieldX1) - PX(HitWindowViewModel.FieldX0),
                                PY(HitWindowViewModel.FieldY1) - PY(HitWindowViewModel.FieldY0));
        using var clip = ctx.PushClip(clipRect);


        var spirit = SpiritField.At(win, i, vm.Side);
        if (spirit is not null)
        {
            var geo = new StreamGeometry();
            using (var gc = geo.Open())
            {
                foreach (var th in spirit.Angles.Count > 0 ? spirit.Angles : [0.0])
                    SpiritPath(gc, spirit, th, PX, PY, ss);
            }
            var sideBrush = T.Side(vm.Side);
            ctx.DrawGeometry(T.Fade(sideBrush, BoardPalette.SpiritFillA), null, geo);
            var pen = new Pen(T.Fade(sideBrush, BoardPalette.SpiritLineA), BoardPalette.SpiritW);
            if (!spirit.AngleKnown) pen = new Pen(pen.Brush, BoardPalette.SpiritW)
            { DashStyle = new DashStyle(BoardPalette.ClearRingDash, 0) };
            ctx.DrawGeometry(null, pen, geo);
        }

        bool inert = RingInert(freeze, vm.Side, i);
        foreach (var r in ClearRings.At(win, i, vm.Side))
        {
            var b = T.Side(vm.Side);
            double rr = r.R * ss;
            var c = new Point(PX(r.X), PY(r.Y));
            if (!inert)
                ctx.DrawEllipse(T.Fade(b, BoardPalette.ClearRingFillA), null, c, rr, rr);
            var rpen = new Pen(T.Fade(b, BoardPalette.ClearRingLineA), BoardPalette.ClearRingW);
            if (!r.Certain)
                rpen = new Pen(rpen.Brush, BoardPalette.ClearRingW)
                { DashStyle = new DashStyle(BoardPalette.ClearRingDash, 0) };
            ctx.DrawEllipse(null, rpen, c, rr, rr);
        }

        foreach (var bl in EnemyBlasts.At(win, i, vm.Side))
        {
            var b = T.WhiteBullet;
            double rr = bl.R * ss;
            var c = new Point(PX(bl.X), PY(bl.Y));
            ctx.DrawEllipse(T.Fade(b, BoardPalette.BlastFillA), null, c, rr, rr);
            ctx.DrawEllipse(null, new Pen(T.Fade(b, BoardPalette.BlastLineA), BoardPalette.BlastW), c, rr, rr);
        }

        var hitEx = vm.HitEx;
        var hazardExList = vm.HitEx is null ? vm.HazardCulpritEx : Array.Empty<(int, (int, int)?)>();
        foreach (var ex in ExItems.At(win, i, vm.Side))
        {
            var effect = ExItems.ColorOf(ex.Name);
            bool isHitEx = (hitEx is { } he && ex.Slot == he.Slot
                            && (he.Span is not { } sp4 || (i >= sp4.Start && i <= sp4.End)))
                          || HazardExAt(hazardExList, ex.Slot, i);
            int foeCharId = (int?)vm.MainAt("p" + (3 - vm.Side) + "_character", i) ?? 0;
            int? cardLevel = ExItems.CardLevelOf(ex.ExType);
            IBrush brush = T.ArmedExColor(vm.Side, isHit: isHitEx, ex.FromBoss, cardLevel, foeCharId);
            var dash = effect is not null
                ? new DashStyle(BoardPalette.ClearRingDash, 0) : null;
            double fillA = effect is not null ? BoardPalette.ExEffectFillA : BoardPalette.ExArmFillA;
            double lineA = isHitEx ? BoardPalette.ExHitLineA : BoardPalette.ExArmLineA;
            var pen = new Pen(T.Fade(brush, lineA), BoardPalette.TrailW);
            if (dash is not null) pen = new Pen(pen.Brush, BoardPalette.TrailW) { DashStyle = dash };

            bool isCross = ex.IsCross;
            if (ex.Shape?.Circles is { Count: > 0 } circles)
            {
                foreach (var (cx, cy, cr) in circles)
                {
                    var c = new Point(PX(cx), PY(cy));
                    double rr = cr * ss;
                    ctx.DrawEllipse(T.Fade(brush, fillA), null, c, rr, rr);
                    ctx.DrawEllipse(null, pen, c, rr, rr);
                }
            }
            else if (ex.Shape?.Rect is { } rect)
            {
                double hw = rect.W * 0.5 * ss, hh = rect.H * 0.5 * ss;
                var r2 = new Rect(PX(rect.X) - hw, PY(rect.Y) - hh, hw * 2, hh * 2);
                ctx.DrawRectangle(T.Fade(brush, fillA), null, r2);
                ctx.DrawRectangle(null, pen, r2);
            }
            else
            {
                double px = PX(ex.X), py = PY(ex.Y), k = BoardPalette.ExCrossPx;
                double crossA = BoardPalette.ExCrossAlpha(ex.HasHitbox);
                var gp = new Pen(T.Fade(T.Dead, crossA), BoardPalette.TrailW);
                ctx.DrawLine(gp, new Point(px - k, py), new Point(px + k, py));
                ctx.DrawLine(gp, new Point(px, py - k), new Point(px, py + k));
            }
            if (isHitEx && !isCross)
            {
                double hr = 0, vr = 0;
                if (ex.Shape?.Circles is { Count: > 0 } rc)
                {
                    double maxR = 0;
                    foreach (var (_, _, cr2) in rc) maxR = Math.Max(maxR, cr2);
                    hr = vr = maxR * ss;
                }
                else if (ex.Shape?.Rect is { } rr2)
                {
                    hr = rr2.W * 0.5 * ss; vr = rr2.H * 0.5 * ss;
                }
                MarkRing(ctx, T, vm.Side, PX(ex.X), PY(ex.Y), hr, vr);
            }
        }
        if (self is not null)
        {
            double mhx = selfSide * 0.5 * ss + BoardPalette.SelfMatPx;
            double mhy = selfSide * 0.5 * ss + BoardPalette.SelfMatPx;
            double x0 = Math.Max(PX(HitWindowViewModel.FieldX0) + 1, PX(self.Value.X) - mhx);
            double y0 = Math.Max(PY(HitWindowViewModel.FieldY0) + 1, PY(self.Value.Y) - mhy);
            double x1 = Math.Min(PX(HitWindowViewModel.FieldX1) - 1, PX(self.Value.X) + mhx);
            double y1 = Math.Min(PY(HitWindowViewModel.FieldY1) - 1, PY(self.Value.Y) + mhy);
            if (x1 > x0 && y1 > y0) ctx.FillRectangle(T.Bg, new Rect(x0, y0, x1 - x0, y1 - y0));
        }


        {
            int selfChar = (int?)vm.MainAt("p" + vm.Side + "_character", i) ?? -1;
            static int ShotKey(bool? c1) => c1 is null ? 2 : (c1.Value ? 1 : 0);
            static bool? ShotC1(int key) => key == 2 ? null : key == 1;
            var buckets = new Dictionary<int, StreamGeometry>();
            var open = new Dictionary<int, StreamGeometryContext>();
            foreach (var s in PlayerShots.At(win, i, vm.Side))
            {
                if (s.Box is not { } box) continue;
                var key = ShotKey(PlayerShots.IsC1(selfChar < 0 ? null : selfChar, s.Parts.Entry));
                if (!buckets.TryGetValue(key, out var geo))
                {
                    geo = new StreamGeometry();
                    buckets[key] = geo;
                    open[key] = geo.Open();
                }
                double hx = box.HalfW * ss, hy = box.HalfH * ss;
                Box(open[key], PX(s.X) - hx, PY(s.Y) - hy, PX(s.X) + hx, PY(s.Y) + hy);
            }
            foreach (var (key, gc) in open) gc.Dispose();
            foreach (var (key, geo) in buckets)
            {
                var brush = T.ShotBrush(vm.Side, ShotC1(key));
                double a = key == 1 ? BoardPalette.ShotAC1 : BoardPalette.ShotA;
                ctx.DrawGeometry(T.Fade(brush, a), null, geo);
            }
        }

        int spread = ColorSpread;
        bool byOrigin = vm.ColorBy == "origin";
        TrailPoint?[]? culpritPts = null;
        if (vm.CulpritBulletSlot is int culpritSlotForSkip)
            foreach (var t in vm.HitTrails)
                if (t.Slot == culpritSlotForSkip) { culpritPts = t.Points; break; }
        Dictionary<int, TrailPoint?[]>? hazardBulletPts = null;
        foreach (var (hs, hk) in vm.HazardCulpritTrailSlots)
        {
            if (hk != HitCandidates.HazardMatchKind.Bullet) continue;
            foreach (var t in vm.HitTrails)
                if (t.Slot == hs) { (hazardBulletPts ??= [])[hs] = t.Points; break; }
        }
        foreach (var b in board.Bullets)
        {
            if (b.X is null || b.Y is null) continue;
            if (culpritPts is not null && b.Slot == vm.CulpritBulletSlot
                && i < culpritPts.Length && culpritPts[i] is not null)
                continue;
            if (hazardBulletPts is not null && hazardBulletPts.TryGetValue(b.Slot, out var hbp)
                && i < hbp.Length && hbp[i] is not null)
                continue;
            uint kind = b.Kind ?? 0;
            int sprite = (int)(kind & 0xFFFF);
            var (hx, hy) = vm.BulletHalf(kind);
            IBrush brush;
            if (spread > 0)
            {
                brush = T.SpreadBrush(sprite, spread);
            }
            else if (byOrigin)
            {
                var o = BulletOrigin.AtBirth(win, b.Slot, i);
                brush = T.OriginBrush(o?.Origin, o?.EnemyClass, o?.CardLevel,
                                      o?.BossSub is null ? null : (int)o.BossSub.Value);
            }
            else
            {
                brush = T.BulletSpriteBrush(sprite);
            }
            if (sprite == 0) brush = T.Erasable(brush);
            double rx = hx * ss, ry = hy * ss;
            ctx.FillRectangle(brush, new Rect(PX(b.X.Value) - rx, PY(b.Y.Value) - ry, rx * 2, ry * 2));
        }
        LastDrawnBullets = board.Bullets.Count;

        var hitFoe = vm.HitEnemy;
        foreach (var e in board.Enemies)
        {
            if (e.X is null || e.Y is null) continue;
            bool foeHit = hitFoe is { } hf && e.Slot == hf.Slot
                          && (hf.Span is not { } sp2 || (i >= sp2.Start && i <= sp2.End));
            bool hazardFoeHit = !foeHit
                                && HazardTrailHas(vm.HazardCulpritTrailSlots, e.Slot, HitCandidates.HazardMatchKind.Enemy);
            bool ghostActivated = e.EnemyClass == "ghost" && CoordRing.EnemyGhostActivated(e.Kind ?? 0);
            var brush = foeHit || hazardFoeHit ? T.Side(vm.Side)
                      : ghostActivated ? T.GhostActivatedBrush
                      : T.EnemyBrush(e.EnemyClass);
            var box = EnemySize.Of(win, e.Slot, i, e.EnemyClass);
            bool none = box is not null && box.None;
            double hx = BoardPalette.EnemyMarkPx, hy = BoardPalette.EnemyMarkPx;
            if (box is not null && !none) { hx = box.HalfW * ss; hy = box.HalfH * ss; }
            var rect = new Rect(PX(e.X.Value) - hx, PY(e.Y.Value) - hy, hx * 2, hy * 2);
            if (none)
            {
                ctx.FillRectangle(new SolidColorBrush(
                    ((ISolidColorBrush)brush).Color, BoardPalette.EnemyNoHitFillA).ToImmutable(), rect);
                ctx.DrawRectangle(null, new Pen(brush, 1.4)
                { DashStyle = new DashStyle(BoardPalette.ClearRingDash, 0) }, rect);
            }
            else
            {
                ctx.DrawRectangle(null, new Pen(brush, 1.4), rect);
                if (box is not null && box.Ranged)
                {
                    double hx2 = box.HalfWMax * ss, hy2 = box.HalfHMax * ss;
                    ctx.DrawRectangle(null, new Pen(brush, 1.4)
                    { DashStyle = new DashStyle(BoardPalette.ClearRingDash, 0) },
                        new Rect(PX(e.X.Value) - hx2, PY(e.Y.Value) - hy2, hx2 * 2, hy2 * 2));
                }
            }
            if (foeHit)
            {
                double rhx = box is not null && !none ? box.HalfWMax * ss : BoardPalette.EnemyMarkPx;
                double rhy = box is not null && !none ? box.HalfHMax * ss : BoardPalette.EnemyMarkPx;
                MarkRing(ctx, T, vm.Side, PX(e.X.Value), PY(e.Y.Value), rhx, rhy);
            }
        }
        LastDrawnEnemies = board.Enemies.Count;

        int laserFoeCharId = (int?)vm.MainAt("p" + (3 - vm.Side) + "_character", i) ?? 0;
        var deadLaserBrush = T.DeadLaserColor(laserFoeCharId);
        foreach (var l in board.Lasers)
        {
            var info = l.Laser;
            if (info is null || l.X is null || l.Y is null) continue;
            var (a, b, half) = LaserSegment(l.X.Value, l.Y.Value, info.Value);
            IPen pen;
            if (info.Value.Lethal)
            {
                bool isCulprit = vm.HitLaser is { } hl && l.Slot == hl.Slot
                                 && (hl.Span is not { } sp3 || (i >= sp3.Start && i <= sp3.End));
                bool hazardIsCulprit = !isCulprit
                                       && HazardTrailHas(vm.HazardCulpritTrailSlots, l.Slot, HitCandidates.HazardMatchKind.Laser);
                bool isHit = isCulprit || hazardIsCulprit;
                var laserOrigin = LaserOrigin.Parts(l.State);
                var laserKind = LaserOrigin.Classify(laserFoeCharId, laserOrigin.Sub);
                int? attackSub = LaserOrigin.AttackSub(win, vm.Side, l.Slot, laserFoeCharId, laserOrigin, i);
                var laserBrush = T.LaserOriginColor(vm.Side, isHit, laserKind, attackSub, laserFoeCharId);
                double laserA = isHit ? BoardPalette.LaserHitA : BoardPalette.LaserFoeA;
                pen = new Pen(T.Fade(laserBrush, laserA), half * 2 * ss) { LineCap = PenLineCap.Flat };
            }
            else
            {
                pen = new Pen(deadLaserBrush, BoardPalette.DeadLaserPx)
                { DashStyle = new DashStyle([3, 3], 0) };
            }
            ctx.DrawLine(pen, new Point(PX(a.X), PY(a.Y)), new Point(PX(b.X), PY(b.Y)));
        }
        LastDrawnLasers = board.Lasers.Count;


        var ev = vm.Events.FirstOrDefault(e => e.Index == i);
        (double X, double Y, double HalfW, double HalfH)? ObjAt(int slot)
        {
            foreach (var bl in board.Bullets)
                if (bl.Slot == slot && bl.X is not null && bl.Y is not null)
                {
                    var (bhx, bhy) = vm.BulletHalf(bl.Kind ?? 0);
                    return (PX(bl.X.Value), PY(bl.Y.Value),
                            Math.Max(1.2, bhx * ss), Math.Max(1.2, bhy * ss));
                }
            foreach (var l in board.Lasers)
                if (l.Slot == slot && l.Laser is { } linfo && l.X is not null && l.Y is not null)
                {
                    var (la, lb, lhalf) = LaserSegment(l.X.Value, l.Y.Value, linfo);
                    double mx = (PX(la.X) + PX(lb.X)) / 2.0, my = (PY(la.Y) + PY(lb.Y)) / 2.0;
                    double lh = lhalf * ss;
                    return (mx, my, lh, lh);
                }
            foreach (var en in board.Enemies)
                if (en.Slot == slot && en.X is not null && en.Y is not null)
                {
                    var ebox = EnemySize.Of(win, en.Slot, i, en.EnemyClass);
                    double ehx = BoardPalette.EnemyMarkPx, ehy = BoardPalette.EnemyMarkPx;
                    if (ebox is not null && !ebox.None) { ehx = ebox.HalfWMax * ss; ehy = ebox.HalfHMax * ss; }
                    return (PX(en.X.Value), PY(en.Y.Value), ehx, ehy);
                }
            return null;
        }

        void DrawTrailGeo(TrailPoint?[] pts, IPen pen)
        {
            bool open = false;
            var geo = new StreamGeometry();
            using (var gc = geo.Open())
            {
                int last = Math.Min(i, pts.Length - 1);
                for (int k = 0; k <= last; k++)
                {
                    var pt = pts[k];
                    if (pt is null) { if (open) { gc.EndFigure(false); open = false; } continue; }
                    if (pt.Value.X is null || pt.Value.Y is null) continue;
                    var p2 = new Point(PX(pt.Value.X.Value), PY(pt.Value.Y.Value));
                    if (!open) { gc.BeginFigure(p2, false); open = true; }
                    else gc.LineTo(p2);
                }
                if (open) gc.EndFigure(false);
            }
            ctx.DrawGeometry(null, pen, geo);
        }

        if (ExTrailOn("rest"))
        {
            var exPen = new Pen(T.Fade(T.Side(vm.Side), BoardPalette.TrailACand), BoardPalette.TrailW)
            { DashStyle = new DashStyle(BoardPalette.TrailDot, 0) };
            foreach (var pts in vm.ExTrails.Values) DrawTrailGeo(pts, exPen);
        }

        if (vm.HazardCulpritExTrail.Count > 0)
        {
            var hazardExPen = new Pen(T.Fade(T.Side(vm.Side), BoardPalette.TrailAFixed), BoardPalette.TrailW);
            foreach (var (_, pts) in vm.HazardCulpritExTrail) DrawTrailGeo(pts, hazardExPen);
        }

        if (vm.HitTrails.Count > 0)
        {
            {
                foreach (var (slot, pts, fixedOne, est) in vm.HitTrails)
                {
                    double alpha = fixedOne ? BoardPalette.TrailAFixed
                                 : est ? BoardPalette.TrailAEst : BoardPalette.TrailACand;
                    var brush = T.Fade(T.Side(vm.Side), alpha);
                    var pen = fixedOne
                        ? new Pen(brush, BoardPalette.TrailW)
                        : new Pen(brush, BoardPalette.TrailW)
                          { DashStyle = new DashStyle(BoardPalette.TrailDot, 0) };
                    DrawTrailGeo(pts, pen);

                    if (!fixedOne)
                    {
                        if (ObjAt(slot) is { } cand)
                            MarkRing(ctx, T, vm.Side, cand.X, cand.Y, cand.HalfW, cand.HalfH, alpha);
                    }
                    else if (vm.CulpritBulletSlot == slot)
                    {
                        int lastIdx = Math.Min(i, pts.Length - 1);
                        var cp = lastIdx >= 0 ? pts[lastIdx] : null;
                        if (cp is { X: not null, Y: not null } tp)
                        {
                            double cx = 0, cy = 0;
                            if (vm.CulpritHalf is { } half)
                            {
                                cx = half.HalfW * ss; cy = half.HalfH * ss;
                                ctx.FillRectangle(T.Side(vm.Side),
                                    new Rect(PX(tp.X.Value) - cx, PY(tp.Y.Value) - cy, cx * 2, cy * 2));
                            }
                            MarkRing(ctx, T, vm.Side, PX(tp.X.Value), PY(tp.Y.Value), cx, cy);
                        }
                    }
                    else if (HazardTrailKindOf(vm.HazardCulpritTrailSlots, slot) is { } hazardKind)
                    {
                        if (ObjAt(slot) is { } hzCand)
                        {
                            if (hazardKind == HitCandidates.HazardMatchKind.Bullet)
                                ctx.FillRectangle(T.Side(vm.Side),
                                    new Rect(hzCand.X - hzCand.HalfW, hzCand.Y - hzCand.HalfH,
                                              hzCand.HalfW * 2, hzCand.HalfH * 2));
                            MarkRing(ctx, T, vm.Side, hzCand.X, hzCand.Y, hzCand.HalfW, hzCand.HalfH, alpha);
                        }
                    }
                }

            }
        }

        if (hit && vm.HitHasNoCandidate)
        {
            var hp = vm.HitPoint;
            if (hp is { } p)
            {
                double px = PX(p.X), py = PY(p.Y), k2 = BoardPalette.HitXPx;
                var xp = new Pen(T.Fade(T.Side(vm.Side), BoardPalette.TrailAEst), BoardPalette.TrailW);
                ctx.DrawLine(xp, new Point(px - k2, py - k2), new Point(px + k2, py + k2));
                ctx.DrawLine(xp, new Point(px - k2, py + k2), new Point(px + k2, py - k2));
                MarkRing(ctx, T, vm.Side, px, py, 0, 0, BoardPalette.TrailAEst);
            }
        }

        double textX = PX(HitWindowViewModel.FieldX0) + BoardPalette.BoardTextPad;
        double textRoom = w - textX - BoardPalette.BoardTextPad;
        if (hit || freezeSpan is not null)
        {
            double y0 = PY(HitWindowViewModel.FieldY0) + BoardPalette.BoardTextPad;
            if (hit)
            {
                FitText(ctx, "★被弾", textX, y0, textRoom, T.Side(vm.Side));
                y0 += BoardPalette.BoardTextLineH;
            }
            if (freezeSpan is { } fz)
            {
                FitText(ctx, fz.Label, textX, y0, textRoom, T.Sub);
                var role = FreezeSpans.Role(fz, vm.Side);
                var note = FreezeSpans.RoleNote(role);
                if (note.Length > 0)
                    FitText(ctx, note, textX, y0 + BoardPalette.BoardTextLineH, textRoom, T.Sub);
            }
        }
        if (vm.CandidateWhyText is { } why)
            FitText(ctx, "★候補なし: " + why, textX, h - BoardPalette.BoardTextPad, textRoom, T.Sub, bottom: true);

        if (self is not null)
        {
            bool inv = vm.InvAt(i) == true;
            var ringPen = new Pen(T.Fade(T.Side(vm.Side), BoardPalette.SelfRingA),
                                  BoardPalette.SelfRingW);
            var at = new Point(PX(self.Value.X), PY(self.Value.Y));
            ctx.DrawEllipse(null, ringPen, at, BoardPalette.SelfRingPx, BoardPalette.SelfRingPx);
            if (inv)
                ctx.DrawEllipse(null, ringPen, at,
                                BoardPalette.SelfRingPx + BoardPalette.InvRingGap,
                                BoardPalette.SelfRingPx + BoardPalette.InvRingGap);

            double hs = selfSide * 0.5 * ss;
            ctx.FillRectangle(inv ? T.Fade(T.Side(vm.Side), BoardPalette.SelfInvA) : T.Side(vm.Side),
                new Rect(PX(self.Value.X) - hs, PY(self.Value.Y) - hs, hs * 2, hs * 2));
        }
    }

    private bool ExTrailOn(string which) =>
        ExTrail == "all" || (ExTrail == "hit" && which == "hit");

    private static bool RingInert(FreezeResult freeze, int boardSide, int i)
    {
        foreach (var sp in freeze.Spans)
        {
            if (i < sp.Index || i >= sp.Index + sp.Frames) continue;
            if (!string.Equals(sp.Kind, FreezeSpans.KindTimeStop, StringComparison.Ordinal)) continue;
            if (string.Equals(FreezeSpans.Role(sp, boardSide), FreezeSpans.RoleField,
                              StringComparison.Ordinal)) return true;
        }
        return false;
    }


    private static void SpiritPath(StreamGeometryContext gc, SpiritTick t, double th,
                                   Func<double, double> PX, Func<double, double> PY, double ss)
    {
        var s = t.Shape;
        double cx = PX(t.X), cy = PY(t.Y);
        double x0 = PX(HitWindowViewModel.FieldX0), x1 = PX(HitWindowViewModel.FieldX1);
        double y0 = PY(HitWindowViewModel.FieldY0), y1 = PY(HitWindowViewModel.FieldY1);

        switch (s.Name)
        {
            case var n when n == SpiritShapeNames.Circle || n == SpiritShapeNames.CircleUp:
                Circle(gc, cx, cy, t.Size.Radius * ss);
                break;
            case var n when n == SpiritShapeNames.BandV:
                Box(gc, cx - t.Size.HalfWidth * ss, y0, cx + t.Size.HalfWidth * ss, y1);
                break;
            case var n when n == SpiritShapeNames.BandH:
                Box(gc, x0, cy - t.Size.HalfWidth * ss, x1, cy + t.Size.HalfWidth * ss);
                break;
            case var n when n == SpiritShapeNames.Cross:
                Box(gc, cx - t.Size.HalfWidth * ss, y0, cx + t.Size.HalfWidth * ss, y1);
                Box(gc, x0, cy - t.Size.HalfWidth * ss, x1, cy + t.Size.HalfWidth * ss);
                break;
            case var n when n == SpiritShapeNames.Fan:
                Fan(gc, cx, cy, s.Param("radius") * ss, th, t.Size.FanAngleHalf);
                break;
            case var n when n == SpiritShapeNames.Lens:
                Lens(gc, cx, cy, t.Size.Radius * ss, s.Param("k"), th);
                break;
            case var n when n == SpiritShapeNames.Flower:
                Circle(gc, cx, cy, t.Size.Radius * ss);
                Petals(gc, cx, cy, t.Size.Radius * ss, s.Param("petal_r") * ss,
                       (int)s.Param("petals"), th);
                break;
            case var n when n == SpiritShapeNames.Star:
                Star(gc, cx, cy, t.Size.Radius * ss, (int)s.Param("points"), s.Param("inner"), th);
                break;
            case var n when n == SpiritShapeNames.ConeUp:
                ConeUp(gc, cx, cy, t.Size.ConeWidthFactor, s.Param("span") * ss,
                       s.Param("min_half") * ss, s.Param("y_gate") * ss, y0);
                break;
            default:
                break;
        }
    }

    private static void Circle(StreamGeometryContext gc, double cx, double cy, double r)
    {
        if (r <= 0) return;
        gc.BeginFigure(new Point(cx + r, cy), true);
        gc.ArcTo(new Point(cx - r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
        gc.ArcTo(new Point(cx + r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
        gc.EndFigure(true);
    }

    private static void Box(StreamGeometryContext gc, double ax, double ay, double bx, double by)
    {
        if (bx <= ax || by <= ay) return;
        gc.BeginFigure(new Point(ax, ay), true);
        gc.LineTo(new Point(bx, ay));
        gc.LineTo(new Point(bx, by));
        gc.LineTo(new Point(ax, by));
        gc.EndFigure(true);
    }

    private static void Fan(StreamGeometryContext gc, double cx, double cy, double r,
                            double th, double half)
    {
        if (r <= 0 || half <= 0) return;
        gc.BeginFigure(new Point(cx, cy), true);
        int n = Math.Max(2, (int)(half * 2 / 0.1));
        for (int k = 0; k <= n; k++)
        {
            double a = th - half + half * 2 * k / n;
            gc.LineTo(new Point(cx + Math.Cos(a) * r, cy + Math.Sin(a) * r));
        }
        gc.EndFigure(true);
    }

    private static void Lens(StreamGeometryContext gc, double cx, double cy, double r,
                             double k, double th)
    {
        if (r <= 0) return;
        double ox = Math.Cos(th + Math.PI / 2) * k * r, oy = Math.Sin(th + Math.PI / 2) * k * r;
        double d = Math.Sqrt(ox * ox + oy * oy);
        if (d >= r) return;
        const int N = 48;
        var up = new List<Point>(N + 1);
        var dn = new List<Point>(N + 1);
        double ux = ox / (d == 0 ? 1 : d), uy = oy / (d == 0 ? 1 : d);
        double vx = -uy, vy = ux;
        double halfLen = Math.Sqrt(r * r - d * d);
        for (int j = 0; j <= N; j++)
        {
            double s = -halfLen + halfLen * 2 * j / N;
            double h1 = Math.Sqrt(Math.Max(0, r * r - (s) * (s))) - d;
            if (h1 <= 0) { h1 = 0; }
            up.Add(new Point(cx + vx * s + ux * h1, cy + vy * s + uy * h1));
            dn.Add(new Point(cx + vx * s - ux * h1, cy + vy * s - uy * h1));
        }
        gc.BeginFigure(up[0], true);
        for (int j = 1; j < up.Count; j++) gc.LineTo(up[j]);
        for (int j = dn.Count - 1; j >= 0; j--) gc.LineTo(dn[j]);
        gc.EndFigure(true);
    }

    private static void Petals(StreamGeometryContext gc, double cx, double cy, double r,
                               double petalR, int petals, double th)
    {
        if (petals <= 0 || petalR <= 0) return;
        for (int k = 0; k < petals; k++)
        {
            double a = th + Math.PI * 2 * k / petals;
            double px = cx + Math.Cos(a) * (r + petalR), py = cy + Math.Sin(a) * (r + petalR);
            Circle(gc, px, py, petalR);
        }
    }

    private static void Star(StreamGeometryContext gc, double cx, double cy, double r,
                             int points, double inner, double th)
    {
        if (points <= 0 || r <= 0 || inner == 0) return;
        double ri = r / (inner * inner);
        gc.BeginFigure(new Point(cx + Math.Cos(th) * r, cy + Math.Sin(th) * r), true);
        for (int k = 0; k < points * 2; k++)
        {
            double a = th + Math.PI * k / points;
            double rad = (k % 2 == 0) ? r : ri;
            gc.LineTo(new Point(cx + Math.Cos(a) * rad, cy + Math.Sin(a) * rad));
        }
        gc.EndFigure(true);
    }

    private static void ConeUp(StreamGeometryContext gc, double cx, double cy,
                               double wFactor, double span, double minHalf, double yGate, double top)
    {
        double bottom = cy + yGate;
        if (bottom <= top) return;
        int n = BoardPalette.SpiritConeSteps;
        var left = new List<Point>(n + 1);
        var right = new List<Point>(n + 1);
        for (int k = 0; k <= n; k++)
        {
            double y = bottom + (top - bottom) * k / n;
            double Y = y - cy + span;
            double inside = span * span - Y * Y;
            double half = Math.Max(minHalf, wFactor * Math.Sqrt(Math.Max(0, inside)));
            left.Add(new Point(cx - half, y));
            right.Add(new Point(cx + half, y));
        }
        gc.BeginFigure(left[0], true);
        for (int k = 1; k < left.Count; k++) gc.LineTo(left[k]);
        for (int k = right.Count - 1; k >= 0; k--) gc.LineTo(right[k]);
        gc.EndFigure(true);
    }
    internal static ((double X, double Y) A, (double X, double Y) B, double Half)
        LaserSegment(double x, double y, LaserInfo l)
    {
        double a = l.Angle ?? 0.0, tail = l.Tail ?? 0.0, head = l.Head ?? 0.0;
        double dx = Math.Cos(a), dy = Math.Sin(a);
        return ((x + dx * tail, y + dy * tail), (x + dx * head, y + dy * head),
                (l.Width ?? 0.0) * BoardPalette.LaserHalfFactor);
    }

    private void MarkRing(DrawingContext ctx, BoardPalette theme, int side,
                          double px, double py, double hx, double hy,
                          double alpha = BoardPalette.TrailAFixed)
    {
        LastDrawnRings++;
        double r = Math.Max(BoardPalette.MarkRingMinR,
                            Math.Sqrt(hx * hx + hy * hy) + BoardPalette.MarkRingPad);
        var pen = new Pen(theme.Fade(theme.Side(side), alpha), BoardPalette.MarkRingW)
        { DashStyle = new DashStyle(BoardPalette.MarkRingDash, 0) };
        ctx.DrawEllipse(null, pen, new Point(px, py), r, r);
    }

    private static bool HazardExAt(
        IReadOnlyList<(int Slot, (int Start, int End)? Span)> hazardEx, int slot, int tick)
    {
        foreach (var (s, span) in hazardEx)
            if (s == slot && (span is not { } sp || (tick >= sp.Start && tick <= sp.End)))
                return true;
        return false;
    }

    private static bool HazardTrailHas(
        IReadOnlyList<(int Slot, HitCandidates.HazardMatchKind Kind)> hazardTrail,
        int slot, HitCandidates.HazardMatchKind kind)
    {
        foreach (var (s, k) in hazardTrail)
            if (s == slot && k == kind) return true;
        return false;
    }

    private static HitCandidates.HazardMatchKind? HazardTrailKindOf(
        IReadOnlyList<(int Slot, HitCandidates.HazardMatchKind Kind)> hazardTrail, int slot)
    {
        foreach (var (s, k) in hazardTrail)
            if (s == slot) return k;
        return null;
    }

    private static void FitText(DrawingContext ctx, string s, double x, double y, double maxWidth,
                                IBrush brush, bool bottom = false)
    {
        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
        FormattedText ft = new(s, System.Globalization.CultureInfo.InvariantCulture,
                               FlowDirection.LeftToRight, typeface, BoardPalette.BoardFontPx, brush);
        for (double px = BoardPalette.BoardFontPx; px >= BoardPalette.BoardFontMinPx; px -= 1)
        {
            ft = new FormattedText(s, System.Globalization.CultureInfo.InvariantCulture,
                                   FlowDirection.LeftToRight, typeface, px, brush);
            if (ft.Width <= maxWidth) break;
        }
        ctx.DrawText(ft, new Point(x, bottom ? y - ft.Height : y));
    }
}


internal sealed class BoardPalette
{
    public const double SelfRingPx = 11;
    public const double SelfRingW = 1.4;
    public const double SelfRingA = 0.55;

    public const double SelfInvA = 0.35;
    public const double InvRingGap = 4;

    public const double SelfMatPx = 3;
    public const double EnemyMarkPx = 4;
    public const double DeadLaserPx = 1;
    public const double LaserHitA = 0.95, LaserFoeA = 0.5;

    public const double ErasableAlpha = 0.45;

    public const double EnemyNoHitFillA = 0.13;

    public static readonly double[] ClearRingDash = [5, 4];

    public static readonly double[] MarkRingDash = [2, 3];
    public const double MarkRingW = 1;
    public const double MarkRingMinR = 7, MarkRingPad = 3;

    public static double LaserHalfFactor => TH09.Analysis.BoardGeometry.LaserHalfFactor;


    public const double ClearRingW = 2.6, ClearRingFillA = 0.05, ClearRingLineA = 0.85;

    public const double BlastW = 1.2, BlastFillA = 0.08, BlastLineA = 0.7;

    public const double ShotAC1 = 0.30, ShotA = 0.15;

    public const double SpiritW = 1.0, SpiritFillA = 0.035, SpiritLineA = 0.5;
    public const int SpiritConeSteps = 24;

    public const double ExEffectFillA = 0.11, ExArmFillA = 0.28;
    public const double ExArmLineA = 0.9, ExHitLineA = 0.95;
    public const double ExCrossPx = 5;
    public const double ExCrossDarkA = 0.9, ExCrossFaintA = 0.35;
    public static double ExCrossAlpha(bool hasHitbox) => hasHitbox ? ExCrossFaintA : ExCrossDarkA;

    public const double TrailW = 1.4;
    public static readonly double[] TrailDot = [1, 3];
    public const double TrailAFixed = 0.85, TrailAEst = 0.68, TrailACand = 0.5;
    public const double HitXPx = 5;

    public const double BoardFontPx = 12, BoardFontMinPx = 9;
    public const double BoardTextPad = 4;
    public const double BoardTextLineH = 15;

    public static readonly string[] TierOrder =
        ["boss", "ex", "card", "lily", "other", "white_origin"];

    public const double TierChromaBase = 0.19, TierChromaStep = 0.03;
    public const double LightLDrop = 0.17;

    public static readonly Dictionary<string, (double H, double? C, double LDark, double? LLight)> PaletteRows =
        new(StringComparer.Ordinal)
        {
            ["boss"] = (350.0, null, 0.72, null),
            ["ex"] = (311.0, null, 0.74, null),
            ["card"] = (158.0, null, 0.78, null),
            ["lily"] = (86.0, null, 0.88, null),
            ["other"] = (263.0, 0.03, 0.765, null),
            ["white_origin"] = (275.0, 0.006, 0.889, 0.424),
            ["fairy"] = (133.5, 0.18, 0.795, null),
            ["enemy_other"] = (267.4, 0.038, 0.607, null),
            ["ghost_penalty"] = (25.0, 0.17, 0.58, null),
        };

    public static readonly string[] CharColors =
    [
        "#ff8a8a", "#ffd166", "#8ab4ff", "#a0e8b0", "#f2a3ff", "#7fe3ff",
        "#ffb37a", "#c792ea", "#b8d47a", "#8fe36b", "#5f9fcc", "#6fe0c4",
        "#ff9ec4", "#e3a3ff", "#d4b483", "#9ad6c8",
    ];

    public const string SharedBulletColor = "#8b93a6";
    public const string UnknownBulletColor = "#5a6172";

    public static double? PaletteChroma(string key)
    {
        if (!PaletteRows.TryGetValue(key, out var row)) return null;
        if (row.C is not null) return row.C;
        return TierChromaBase - TierChromaStep * Array.IndexOf(TierOrder, key);
    }

    public static string? PaletteColor(string key, double dl = 0.0, bool light = false)
    {
        if (!PaletteRows.TryGetValue(key, out var row)) return null;
        double c = PaletteChroma(key)!.Value;
        double baseL = !light ? row.LDark : (row.LLight ?? row.LDark - LightLDrop);
        return OklchHex(baseL + dl, c, row.H);
    }

    public static string OklchHex(double L, double C, double h)
    {
        double a = C * Math.Cos(h * Math.PI / 180.0);
        double b = C * Math.Sin(h * Math.PI / 180.0);
        double l1 = L + 0.3963377774 * a + 0.2158037573 * b;
        double m1 = L - 0.1055613458 * a - 0.0638541728 * b;
        double s1 = L - 0.0894841775 * a - 1.2914855480 * b;
        double l = l1 * l1 * l1, m = m1 * m1 * m1, s = s1 * s1 * s1;
        double[] lin =
        [
            4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s,
        ];
        var outv = new int[3];
        for (int k = 0; k < 3; k++)
        {
            double v = LinearToSrgb(lin[k]) * 255.0;
            outv[k] = Math.Max(0, Math.Min(255, (int)Math.Round(v, MidpointRounding.AwayFromZero)));
        }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                             "#{0:x2}{1:x2}{2:x2}", outv[0], outv[1], outv[2]);
    }

    private static double LinearToSrgb(double c) =>
        c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    private static double SrgbToLinear(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    public static (double L, double C, double H)? OklchOf(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length != 6) return null;
        double[] v = new double[3];
        for (int k = 0; k < 3; k++)
        {
            if (!int.TryParse(s.AsSpan(k * 2, 2), System.Globalization.NumberStyles.HexNumber,
                              System.Globalization.CultureInfo.InvariantCulture, out var b)) return null;
            v[k] = SrgbToLinear(b / 255.0);
        }
        double l = 0.4122214708 * v[0] + 0.5363325363 * v[1] + 0.0514459929 * v[2];
        double m = 0.2119034982 * v[0] + 0.6806995451 * v[1] + 0.1073969566 * v[2];
        double sN = 0.0883024619 * v[0] + 0.2817188376 * v[1] + 0.6299787005 * v[2];
        double l1 = l > 0 ? Math.Cbrt(l) : 0.0, m1 = m > 0 ? Math.Cbrt(m) : 0.0,
               s1 = sN > 0 ? Math.Cbrt(sN) : 0.0;
        double L = 0.2104542553 * l1 + 0.7936177850 * m1 - 0.0040720468 * s1;
        double a = 1.9779984951 * l1 - 2.4285922050 * m1 + 0.4505937099 * s1;
        double bb = 0.0259040371 * l1 + 0.7827717662 * m1 - 0.8086757660 * s1;
        double h = Math.Atan2(bb, a) * 180.0 / Math.PI;
        return (L, Math.Sqrt(a * a + bb * bb), (h % 360.0 + 360.0) % 360.0);
    }


    public const double UnknownChromaScale = 0.5;

    public const double BossSubLStep = 0.05;
    public const int BossSubLo = 3, BossSubHi = 7;

    public const double CardLevelLStep = 0.07;

    public const double GhostActivatedLDelta = -0.12;

    public static string FadeUnknown(string col)
    {
        var lch = OklchOf(col);
        return lch is null ? col : OklchHex(lch.Value.L, lch.Value.C * UnknownChromaScale, lch.Value.H);
    }

    public static string BossSubColor(int? sub, bool light) =>
        sub is null || sub.Value < BossSubLo || sub.Value > BossSubHi
            ? PaletteColor("boss", 0.0, light)!
            : PaletteColor("boss", BossSubLStep * (sub.Value - 5), light)!;

    public static string CardLevelColor(int? level, bool light) =>
        level is not (2 or 3)
            ? PaletteColor("card", 0.0, light)!
            : PaletteColor("card", -CardLevelLStep * (level!.Value - 2), light)!;

    public static string OriginColor(string? origin, string? cls, int? level, int? sub, bool light)
    {
        var tier = BulletOrigin.Tier(origin, cls);
        if (tier is null) return UnknownBulletColor;
        if (tier == BulletOrigin.TierCard)
            return level is not null ? CardLevelColor(level, light)
                                     : FadeUnknown(PaletteColor("card", 0.0, light)!);
        if (tier == BulletOrigin.TierBoss)
            return sub is not null ? BossSubColor(sub, light)
                                   : FadeUnknown(PaletteColor("boss", 0.0, light)!);
        return PaletteColor(tier, 0.0, light)!;
    }

    public IBrush BulletSpriteBrush(int sprite)
    {
        if (_spriteBrush.TryGetValue(sprite, out var got)) return got;
        var p = BulletOrigin.SpriteParts(sprite);
        IBrush b = p.Name is null ? _unknown
                 : sprite == 0 ? WhiteBullet
                 : p.Shared ? _shared
                 : CharColor(p.Owner!.Value);
        _spriteBrush[sprite] = b;
        return b;
    }

    public IBrush OriginBrush(string? origin, string? cls, int? level, int? sub)
    {
        var key = (origin, cls, level, sub);
        if (_originBrush.TryGetValue(key, out var got)) return got;
        var b = Solid(OriginColor(origin, cls, level, sub, Light));
        _originBrush[key] = b;
        return b;
    }

    public static Dictionary<string, string> EnemyColors(bool light) =>
        new(StringComparer.Ordinal)
        {
            ["fairy"] = PaletteColor("fairy", 0.0, light)!,
            ["ghost"] = PaletteColor("ex", 0.0, light)!,
            ["lily"] = PaletteColor("lily", 0.0, light)!,
            ["boss"] = PaletteColor("boss", 0.0, light)!,
            ["c2c3"] = PaletteColor("card", 0.0, light)!,
            ["other"] = PaletteColor("enemy_other", 0.0, light)!,
        };


    private static readonly Dictionary<string, BoardPalette> Cache = new(StringComparer.Ordinal);

    public static BoardPalette Of(string themeName)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(themeName, out var p))
            {
                p = new BoardPalette(themeName == "light");
                Cache[themeName] = p;
            }
            return p;
        }
    }

    public bool Light { get; }
    public IBrush Bg { get; }
    public IBrush BoardBg { get; }
    public IBrush Frame { get; }
    public IBrush Dead { get; }
    public IBrush Sub { get; }
    public IBrush WhiteBullet { get; }
    public IBrush HitTint1 { get; }
    public IBrush HitTint2 { get; }
    public IBrush FreezeTint { get; }

    private readonly string _boardBgHex;
    private readonly IBrush _p1, _p2;
    private readonly Dictionary<string, IBrush> _enemy = new(StringComparer.Ordinal);
    private readonly IBrush[] _chars;
    private readonly IBrush _shared, _unknown;
    private readonly Dictionary<int, IBrush> _spriteBrush = [];
    private readonly Dictionary<(string?, string?, int?, int?), IBrush> _originBrush = [];
    private readonly Dictionary<IBrush, IBrush> _erasable = [];

    private BoardPalette(bool light)
    {
        Light = light;
        Bg = Solid(light ? "#eceef2" : "#0f1117");
        _boardBgHex = light ? "#f6f7fa" : "#0a0c12";
        BoardBg = Solid(_boardBgHex);
        Frame = Solid(light ? "#8a93a6" : "#c9cfdd");
        Dead = Solid(light ? "#c9ced8" : "#3a4152");
        Sub = Solid(light ? "#5c6474" : "#828a9e");
        WhiteBullet = Solid(light ? "#a8b6cc" : "#e8eef8");
        _p1 = Solid(light ? "#0a6fae" : "#5bc8ff");
        _p2 = Solid(light ? "#b8501c" : "#ff8a5b");
        HitTint1 = new SolidColorBrush(Color.FromArgb((byte)Math.Round(0.13 * 255), 91, 200, 255)).ToImmutable();
        HitTint2 = new SolidColorBrush(Color.FromArgb((byte)Math.Round(0.13 * 255), 255, 138, 91)).ToImmutable();
        FreezeTint = light
            ? new SolidColorBrush(Color.FromArgb((byte)Math.Round(0.14 * 255), 70, 110, 165)).ToImmutable()
            : new SolidColorBrush(Color.FromArgb((byte)Math.Round(0.14 * 255), 120, 165, 215)).ToImmutable();
        foreach (var kv in EnemyColors(light)) _enemy[kv.Key] = Solid(kv.Value);
        _chars = CharColors.Select(Solid).ToArray();
        _shared = Solid(SharedBulletColor);
        _unknown = Solid(UnknownBulletColor);
    }

    private static IBrush Solid(string hex) => new SolidColorBrush(Color.Parse(hex)).ToImmutable();

    public IBrush ShotBrush(int side, bool? c1)
    {
        var key = (side, c1);
        if (_shot.TryGetValue(key, out var got)) return got;
        var c0 = ((ISolidColorBrush)Side(side)).Color;
        var col = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                                $"#{c0.R:x2}{c0.G:x2}{c0.B:x2}");
        string hex;
        if (c1 is null)
        {
            hex = FadeUnknown(col);
        }
        else
        {
            var lch = OklchOf(col);
            hex = lch is null ? col
                : OklchHex(lch.Value.L - (c1.Value ? TH09.Analysis.BoardGeometry.ShotC1LDrop : 0.0), lch.Value.C, lch.Value.H);
        }
        var made = Solid(hex);
        _shot[key] = made;
        return made;
    }

    private readonly Dictionary<(int, bool?), IBrush> _shot = [];

    public IBrush Side(int side) => side == 2 ? _p2 : _p1;

    public IBrush CharColor(int charId) =>
        _chars[((charId % _chars.Length) + _chars.Length) % _chars.Length];

    private string CharHex(int charId) =>
        CharColors[((charId % CharColors.Length) + CharColors.Length) % CharColors.Length];

    public IBrush ArmedExColor(int side, bool isHit, bool? fromBoss, int? cardLevel, int foeCharId)
    {
        if (isHit) return Side(side);
        if (fromBoss == true) return Hex(PaletteColor("boss", 0.0, Light)!);
        if (cardLevel is not null) return Hex(CardLevelColor(cardLevel, Light));
        return Hex(PaletteColor("ex", 0.0, Light)!);
    }

    public IBrush LaserOriginColor(int side, bool isHit, LaserOriginKind kind, int? attackSub, int foeCharId)
    {
        if (isHit) return Side(side);
        if (kind is LaserOriginKind.BossChild or LaserOriginKind.BossDirect) return Hex(BossSubColor(attackSub, Light));
        if (kind == LaserOriginKind.ExChild)
            return ArmedExColor(side, isHit: false, fromBoss: false, cardLevel: null, foeCharId);
        return Hex(FadeUnknown(CharHex(foeCharId)));
    }

    private readonly Dictionary<int, IBrush> _deadLaser = [];

    public IBrush DeadLaserColor(int foeCharId)
    {
        int key = ((foeCharId % _chars.Length) + _chars.Length) % _chars.Length;
        if (_deadLaser.TryGetValue(key, out var got)) return got;
        var made = Hex(MidHex(_boardBgHex, CharHex(foeCharId)));
        _deadLaser[key] = made;
        return made;
    }

    public static string MidHex(string a, string b)
    {
        var (ar, ag, ab) = Rgb(a);
        var (br, bg, bb) = Rgb(b);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                             "#{0:x2}{1:x2}{2:x2}",
                             (ar + br) / 2, (ag + bg) / 2, (ab + bb) / 2);
    }

    private static (int R, int G, int B) Rgb(string hex)
    {
        var s = hex.TrimStart('#');
        return (Convert.ToInt32(s.Substring(0, 2), 16),
                Convert.ToInt32(s.Substring(2, 2), 16),
                Convert.ToInt32(s.Substring(4, 2), 16));
    }

    public IBrush EnemyBrush(string? cls) =>
        cls is not null && _enemy.TryGetValue(cls, out var b) ? b : _enemy["other"];

    private IBrush? _ghostActivated;

    public IBrush GhostActivatedBrush =>
        _ghostActivated ??= Solid(PaletteColor("ex", GhostActivatedLDelta, Light)!);

    public IBrush SpreadBrush(int sprite, int spread) =>
        _chars[(((sprite % spread) + spread) % spread) % _chars.Length];

    public IBrush Erasable(IBrush b)
    {
        if (_erasable.TryGetValue(b, out var got)) return got;
        var c = ((ISolidColorBrush)b).Color;
        var faded = new SolidColorBrush(
            Color.FromArgb((byte)Math.Round(c.A * ErasableAlpha), c.R, c.G, c.B)).ToImmutable();
        _erasable[b] = faded;
        return faded;
    }

    public IBrush Fade(IBrush b, double alpha)
    {
        var key = (b, alpha);
        if (_faded.TryGetValue(key, out var got)) return got;
        var c = ((ISolidColorBrush)b).Color;
        var made = new SolidColorBrush(
            Color.FromArgb((byte)Math.Round(c.A * alpha), c.R, c.G, c.B)).ToImmutable();
        _faded[key] = made;
        return made;
    }

    public IBrush Hex(string hex)
    {
        if (_hex.TryGetValue(hex, out var got)) return got;
        var made = new SolidColorBrush(Color.Parse(hex)).ToImmutable();
        _hex[hex] = made;
        return made;
    }

    private readonly Dictionary<(IBrush, double), IBrush> _faded = [];
    private readonly Dictionary<string, IBrush> _hex = new(StringComparer.Ordinal);
}
