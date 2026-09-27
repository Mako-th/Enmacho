using System.Runtime.CompilerServices;
using TH09.Generated;

namespace TH09.Analysis;

public enum HitTypeKind
{
    Bullet = 0,
    ExCircle = 1,
    Laser = 2,
    Contact = 3,
}

public enum HitConfidence
{
    Resolved,
    Narrowed,
    Guessed,
    Unavailable,
}

public enum LaserHow
{
    Pivot,
    PivotAngle,
    PivotGeometry,
    GeometryOnly,
}

public static class HitCandidates
{
    private const uint HitValid = AnalysisTables.HitValid;
    private const int HitTypeShift = AnalysisTables.HitTypeShift;
    private const uint HitTypeMask = AnalysisTables.HitTypeMask;

    private const long BulletArrayBase = AnalysisTables.BulletArrayBase;
    private const long BulletStride = AnalysisTables.BulletArrayStride;

    private static readonly int[] NearTicks = [0, 1, -1, 2, -2];

    private const double PivotTol = AnalysisTables.HitXyTol;

    private const double AngleTol = AnalysisTables.HitAngleTol;

    public const double DefaultMargin = 6.0;

    public const int DefaultSpan = 1;


    public const string WhyResolved = "resolved";
    public const string WhyNoExSlots = "no_ex_slots";
    public const string WhyEnemyNotFound = "enemy_not_found";
    public const string WhyNoPlayerSide = "no_player_side";
    public const string WhyExNotFound = "ex_not_found";
    public const string WhyLaserNotFound = "laser_not_found";
    public const string WhyBulletNoPtr = "bullet_no_ptr";
    public const string WhyUnknownType = "unknown_type";
    public const string WhyExNotPorted = "ex_not_ported";


    public const string HowLaserFan = "laser_fan";
    public const string HowLaserGeometry = "laser_geometry";
    public const string HowEnemyHitXy = "hit座標";
    public const string HowEnemy = "enemy";
    public const string HowEnemyRanged = "enemy_ranged";

    public const string HowExHit = "hit座標";
    public const string HowExHitNextTick = "hit座標+1tick";
    public const string HowExHitNextTickExtrapolated = "hit座標+1tick(外挿)";
    public const string HowExHitTrail = "hit座標(トレイル)";

    private const char Tab = (char)9;

    private static readonly Dictionary<string, string> WhyJaMap = LoadJa(AnalysisTables.CandidateWhyJaPacked);
    private static readonly Dictionary<string, string> EnemyHowJaMap = LoadJa(AnalysisTables.EnemyHowJaPacked);
    private static readonly Dictionary<string, string> EnemyIdentifyHowJaMap =
        LoadJa(AnalysisTables.EnemyIdentifyHowJaPacked);
    private static readonly Dictionary<string, string> ExHowJaMap = LoadJa(AnalysisTables.ExHowJaPacked);
    private static readonly Dictionary<string, string> HitTypeJaMap = LoadJa(AnalysisTables.HitTypeJaPacked);

    public static int WhyJaCount => WhyJaMap.Count;
    public static int EnemyHowJaCount => EnemyHowJaMap.Count;
    public static int EnemyIdentifyHowJaCount => EnemyIdentifyHowJaMap.Count;
    public static int ExHowJaCount => ExHowJaMap.Count;
    public static int HitTypeJaCount => HitTypeJaMap.Count;

    private static Dictionary<string, string> LoadJa(string packed)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(packed))
        {
            int t = line.IndexOf(Tab);
            if (t < 0) throw new InvalidDataException($"語の表の行に区切りが無い: {line}");
            map[line[..t]] = line[(t + 1)..];
        }
        return map;
    }

    public static string WhyJa(string? key) =>
        key is not null && WhyJaMap.TryGetValue(key, out var ja) ? ja : (key ?? "");

    public static string HowJa(string? key)
    {
        if (key is null) return "";
        if (EnemyHowJaMap.TryGetValue(key, out var a)) return a;
        return EnemyIdentifyHowJaMap.TryGetValue(key, out var b) ? b : key;
    }

    public static string? TypeJa(HitTypeKind? kind)
    {
        if (kind is null) return null;
        var key = ((int)kind.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return HitTypeJaMap.TryGetValue(key, out var ja) ? ja : key;
    }

    public static string ExHowJa(string? key) =>
        key is not null && ExHowJaMap.TryGetValue(key, out var ja) ? ja : (key ?? "");


    private static string HitKindName(int side) => side == 1 ? TickWords.Record.P1HitKind : TickWords.Record.P2HitKind;
    private static string HitObjPtrName(int side) => side == 1 ? TickWords.Record.P1HitObjPtr : TickWords.Record.P2HitObjPtr;
    private static string BulletMgrName(int side) => side == 1 ? TickWords.Record.P1BulletMgr : TickWords.Record.P2BulletMgr;
    private static string HitXName(int side) => side == 1 ? TickWords.Record.P1HitX : TickWords.Record.P2HitX;
    private static string HitYName(int side) => side == 1 ? TickWords.Record.P1HitY : TickWords.Record.P2HitY;
    private static string HitLaserXName(int side) => side == 1 ? TickWords.Record.P1HitLaserX : TickWords.Record.P2HitLaserX;
    private static string HitLaserYName(int side) => side == 1 ? TickWords.Record.P1HitLaserY : TickWords.Record.P2HitLaserY;
    private static string HitLaserAngleName(int side) => side == 1 ? TickWords.Record.P1HitLaserAngle : TickWords.Record.P2HitLaserAngle;
    private static string PosXName(int side) => side == 1 ? TickWords.Record.P1PosX : TickWords.Record.P2PosX;
    private static string PosYName(int side) => side == 1 ? TickWords.Record.P1PosY : TickWords.Record.P2PosY;

    private static uint? Word(IHitSource src, string name, int i)
    {
        var v = src.MainAt(name, i);
        return v is null ? null : (uint)Math.Round(v.Value);
    }


    public static HitTypeKind? TypeOf(uint hitKind) =>
        (hitKind & HitValid) == 0 ? null : (HitTypeKind)((hitKind >> HitTypeShift) & HitTypeMask);

    public static HitTypeKind? HitType(IHitSource src, int? side = null, int? eventIndex = null)
    {
        int? k = PickEventIndex(src, side, eventIndex);
        if (k is null) return null;
        var e = src.Events[k.Value];
        foreach (var d in NearTicks)
        {
            var got = TypeOf(Word(src, HitKindName(e.Side), e.Raw + d) ?? 0u);
            if (got is not null) return got;
        }
        return null;
    }

    public static HitTypeKind? HitType(Window w, int? side = null, int? eventIndex = null) =>
        HitType(WindowHits.For(w), side, eventIndex);


    public static HitPointAt? HitPointOf(IHitSource src, int? side = null, int? eventIndex = null)
    {
        int? k = PickEventIndex(src, side, eventIndex);
        if (k is null) return null;
        var e = src.Events[k.Value];
        if (Window.NonHitTriggers.Contains(e.Trigger, StringComparer.Ordinal)) return null;
        var ht = HitType(src, side, eventIndex);
        if (ht is null || ht == HitTypeKind.Laser) return null;
        foreach (var d in NearTicks)
        {
            if (((Word(src, HitKindName(e.Side), e.Raw + d) ?? 0u) & HitValid) == 0) continue;
            int at = e.Raw + d;
            var hx = src.MainAt(HitXName(e.Side), at);
            var hy = src.MainAt(HitYName(e.Side), at);
            if (hx is null || hy is null) return null;
            return new HitPointAt(hx.Value, hy.Value, at);
        }
        return null;
    }

    public static HitPointAt? HitPointOf(Window w, int? side = null, int? eventIndex = null) =>
        HitPointOf(WindowHits.For(w), side, eventIndex);


    public static int? BulletCulprit(IHitSource src, int? side = null, int? eventIndex = null)
    {
        foreach (var e in EventFan(src, side, eventIndex))
        {
            var mgr = Word(src, BulletMgrName(e.Side), e.Raw);
            foreach (var d in NearTicks)
            {
                var n = BulletSlotOfPtr(Word(src, HitObjPtrName(e.Side), e.Raw + d), mgr);
                if (n is not null) return GlobalBulletSlot(e.Side, n.Value, src.SlotCount);
            }
        }
        return null;
    }

    public static int? BulletCulprit(Window w, int? side = null, int? eventIndex = null) =>
        BulletCulprit(WindowHits.For(w), side, eventIndex);

    public static int? BulletSlotOfPtr(uint? ptr, uint? mgr)
    {
        if (ptr is null or 0 || mgr is null or 0) return null;
        long off = (long)ptr.Value - mgr.Value - BulletArrayBase;
        if (off < 0 || off % BulletStride != 0) return null;
        long slot = off / BulletStride;
        return slot < AnalysisTables.CoordBulletSlots ? (int)slot : null;
    }

    public static int? GlobalBulletSlot(int side, int n, int slotCount)
    {
        if (n < 0 || n >= AnalysisTables.CoordBulletSlots) return null;
        int b = side switch
        {
            1 => AnalysisTables.CoordBaseP1Bullet,
            2 => AnalysisTables.CoordBaseP2Bullet,
            _ => -1,
        };
        if (b < 0) return null;
        int gs = b + n;
        return gs < slotCount && gs < CoordRing.SlotCount ? gs : null;
    }


    public static LaserIdentity? LaserIdentify(IHitSource src, int? side = null, int? eventIndex = null,
                                               double margin = DefaultMargin)
    {
        var fanEvents = EventFan(src, side, eventIndex);
        if (fanEvents.Count == 0) return null;
        foreach (var e in fanEvents)
        {
            int sd = e.Side;
            foreach (var d in NearTicks)
            {
                var lx = src.MainAt(HitLaserXName(sd), e.Raw + d);
                var ly = src.MainAt(HitLaserYName(sd), e.Raw + d);
                if (lx is null || ly is null) continue;
                if (lx.Value == 0.0 && ly.Value == 0.0) continue;
                foreach (var j in new[] { e.Index + d, e.Raw + d, e.Index + d - 1, e.Index + d + 1 })
                {
                    var fan = LaserSlotsAt(src, sd, lx.Value, ly.Value, j);
                    if (fan.Count == 1)
                        return new LaserIdentity(fan[0], LaserHow.Pivot, 1, fan, null);
                    if (fan.Count <= 1) continue;
                    var ang = src.MainAt(HitLaserAngleName(sd), e.Raw + d);
                    if (ang is not null)
                    {
                        var exact = ByAngle(src, sd, fan, ang.Value, j);
                        if (exact is not null)
                            return new LaserIdentity(exact, LaserHow.PivotAngle, fan.Count, fan, null);
                    }
                    var pick = NearestLaser(src, sd, e.Index + d, fan, margin);
                    return new LaserIdentity(pick?.Slot, LaserHow.PivotGeometry, fan.Count, fan, pick?.Distance);
                }
            }
        }
        if (HitType(src, eventIndex is null ? side : null, eventIndex) != HitTypeKind.Laser) return null;
        var c = LaserCandidatesOf(src, fanEvents[0].Side, margin);
        if (c.Count == 0) return null;
        return new LaserIdentity(c[0].Slot, LaserHow.GeometryOnly, c.Count,
                                 c.Select(r => r.Slot).ToList(), c[0].Distance);
    }

    public static LaserIdentity? LaserIdentify(Window w, int? side = null, int? eventIndex = null,
                                               double margin = DefaultMargin) =>
        LaserIdentify(WindowHits.For(w), side, eventIndex, margin);

    public static List<int> LaserSlotsAt(IHitSource src, int side, double lx, double ly, int i)
    {
        var found = new List<int>();
        foreach (var it in src.BoardAt(i, side).Lasers)
            if (it.X is not null && it.Y is not null
                && Math.Abs(it.X.Value - lx) <= PivotTol && Math.Abs(it.Y.Value - ly) <= PivotTol)
                found.Add(it.Slot);
        return found;
    }

    private static int? ByAngle(IHitSource src, int side, List<int> slots, double angle, int i)
    {
        int? one = null;
        int n = 0;
        foreach (var it in src.BoardAt(i, side).Lasers)
        {
            if (!slots.Contains(it.Slot)) continue;
            var a = it.Laser?.Angle;
            if (a is null || Math.Abs(a.Value - angle) > AngleTol) continue;
            one = it.Slot;
            n++;
        }
        return n == 1 ? one : null;
    }

    private static LaserCandidate? NearestLaser(IHitSource src, int side, int i, List<int> slots, double margin)
    {
        var px = src.MainAt(PosXName(side), i);
        var py = src.MainAt(PosYName(side), i);
        if (px is null || py is null) return null;
        LaserCandidate? best = null;
        foreach (var it in src.BoardAt(i, side).Lasers)
        {
            if (!slots.Contains(it.Slot) || it.Laser?.Lethal != true) continue;
            var seg = LaserSegmentOf(it);
            double dd = PointSegmentDistance(px.Value, py.Value, seg.X1, seg.Y1, seg.X2, seg.Y2);
            if (dd <= seg.Half + margin && (best is null || dd < best.Value.Distance))
                best = new LaserCandidate(it.Slot, Math.Round(dd, 1), Math.Round(seg.Half, 1));
        }
        return best;
    }

    public static List<LaserCandidate> LaserCandidatesOf(IHitSource src, int? side = null,
                                                         double margin = DefaultMargin)
    {
        var outv = new List<LaserCandidate>();
        var cands = src.Events.Where(x => side is null || x.Side == side.Value).ToList();
        if (cands.Count == 0) return outv;
        var e = cands[0];
        var px = src.MainAt(PosXName(e.Side), e.Index);
        var py = src.MainAt(PosYName(e.Side), e.Index);
        if (px is null || py is null) return outv;
        foreach (var it in src.BoardAt(e.Index, e.Side).Lasers)
        {
            if (it.Laser?.Lethal != true) continue;
            var seg = LaserSegmentOf(it);
            double d = PointSegmentDistance(px.Value, py.Value, seg.X1, seg.Y1, seg.X2, seg.Y2);
            if (d <= seg.Half + margin)
                outv.Add(new LaserCandidate(it.Slot, Math.Round(d, 1), Math.Round(seg.Half, 1)));
        }
        outv.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return outv;
    }

    public static List<LaserCandidate> LaserCandidatesOf(Window w, int? side = null,
                                                         double margin = DefaultMargin) =>
        LaserCandidatesOf(WindowHits.For(w), side, margin);

    public static LaserSeg LaserSegmentOf(BoardItem it)
    {
        double x = it.X ?? 0.0, y = it.Y ?? 0.0;
        var L = it.Laser;
        double a = L?.Angle ?? 0.0, tail = L?.Tail ?? 0.0, head = L?.Head ?? 0.0, width = L?.Width ?? 0.0;
        double dx = Math.Cos(a), dy = Math.Sin(a);
        return new LaserSeg(x + dx * tail, y + dy * tail, x + dx * head, y + dy * head,
                            width * AnalysisTables.LaserHalfFactor);
    }

    public static double PointSegmentDistance(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double l2 = dx * dx + dy * dy;
        if (l2 <= 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        double t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / l2, 0.0, 1.0);
        double qx = x1 + t * dx, qy = y1 + t * dy;
        return Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
    }


    public enum HazardMatchKind { Bullet, Enemy, Laser, Ex }

    public readonly record struct HazardBoardCandidate(HazardMatchKind Kind, int Slot, double X, double Y, double? Angle);

    public readonly record struct HazardOverlap(int ListIndex, HazardElement Element, HazardBoardCandidate? Match);

    public sealed record HazardCulprit(
        int TickIndex, int ListIndex, HazardElement Element, int OverlapCount, HazardBoardCandidate? Match,
        IReadOnlyList<HazardOverlap> All);

    private const uint StateNormal = 0;
    private const uint StateInvincible = 3;

    public static HazardCulprit? HazardListCulprit(Window w, int? side = null, int? eventIndex = null)
    {
        var src = WindowHits.For(w);
        int? k = PickEventIndex(src, side, eventIndex);
        if (k is null) return null;
        var e = src.Events[k.Value];
        bool isQuick = Window.NonHitTriggers.Contains(e.Trigger, StringComparer.Ordinal);
        return HazardListCulprit(w, e.Side, e.Raw, isQuick);
    }

    public static HazardCulprit? HazardListCulprit(Window w, int side, int raw, bool isQuick)
    {
        var src = WindowHits.For(w);
        int? bas = isQuick ? FindQuickTick(src, side, raw) : FindHitKindTick(src, side, raw);
        if (bas is null) return null;
        var px = src.MainAt(PosXName(side), bas.Value);
        var py = src.MainAt(PosYName(side), bas.Value);
        if (px is null || py is null) return null;
        var chId = Word(src, side == 1 ? TickWords.Record.P1Character : TickWords.Record.P2Character, 0);
        if (chId is null) return null;
        var hitRadius = HazardList.CharHitRadius((int)chId.Value);
        if (hitRadius is null) return null;
        var elems = HazardList.RealElementsOf(w, side, bas.Value);
        if (elems is null || elems.Count == 0) return null;
        var overlaps = HazardList.Overlaps(elems, px.Value, py.Value, hitRadius.Value);
        if (overlaps.Count == 0) return null;
        var candidates = BoardCandidatesOf(src, w, side, bas.Value);
        HazardBoardCandidate? MatchOf(HazardElement el)
        {
            var m = MatchOnBoard(el, candidates);
            if (m is null && el.Kind != HazardKind.Obb)
                m = MatchExHazard(w, side, bas.Value, el.X, el.Y);
            return m;
        }
        var all = new List<HazardOverlap>(overlaps.Count);
        foreach (var idx in overlaps)
            all.Add(new HazardOverlap(idx, elems[idx], MatchOf(elems[idx])));
        var head = all[0];
        return new HazardCulprit(bas.Value, head.ListIndex, head.Element, overlaps.Count, head.Match, all);
    }

    private static int? FindHitKindTick(IHitSource src, int side, int raw)
    {
        foreach (var d in NearTicks)
        {
            int j = raw + d;
            if (((Word(src, HitKindName(side), j) ?? 0u) & HitValid) != 0) return j;
        }
        return null;
    }

    private static int? FindQuickTick(IHitSource src, int side, int raw)
    {
        string name = side == 1 ? TickWords.Record.P1PlayerState : TickWords.Record.P2PlayerState;
        foreach (var d in NearTicks)
        {
            int j = raw + d;
            if (j < 1) continue;
            var cur = Word(src, name, j);
            var prev = Word(src, name, j - 1);
            if (cur == StateInvincible && prev == StateNormal) return j;
        }
        return null;
    }

    private static List<HazardBoardCandidate> BoardCandidatesOf(IHitSource src, Window? w, int side, int index)
    {
        var outv = new List<HazardBoardCandidate>();
        var board = src.BoardAt(index, side);
        if (w is not null)
            outv.AddRange(RawBulletCandidatesOf(w, side, index));
        else
            foreach (var it in board.Bullets)
                if (it.X is double bx && it.Y is double by)
                    outv.Add(new HazardBoardCandidate(HazardMatchKind.Bullet, it.Slot, bx, by, null));
        foreach (var it in board.Enemies)
            if (it.X is double ex && it.Y is double ey)
                outv.Add(new HazardBoardCandidate(HazardMatchKind.Enemy, it.Slot, ex, ey, null));
        foreach (var it in board.Lasers)
            if (it.X is double lx && it.Y is double ly)
                outv.Add(new HazardBoardCandidate(HazardMatchKind.Laser, it.Slot, lx, ly, it.Laser?.Angle));
        return outv;
    }

    private static IEnumerable<HazardBoardCandidate> RawBulletCandidatesOf(Window w, int side, int index)
    {
        int baseSlot = side == 1 ? AnalysisTables.CoordBaseP1Bullet : AnalysisTables.CoordBaseP2Bullet;
        for (int s = 0; s < AnalysisTables.CoordBulletSlots; s++)
        {
            int slot = baseSlot + s;
            var st = w.Raw(CoordRing.ColState(slot), index);
            if (st is null || !CoordRing.SlotIsAlive(slot, st.Value)) continue;
            var x = w.Float(CoordRing.ColX(slot), index);
            var y = w.Float(CoordRing.ColY(slot), index);
            if (x is null || y is null) continue;
            yield return new HazardBoardCandidate(HazardMatchKind.Bullet, slot, x.Value, y.Value, null);
        }
    }

    private static HazardBoardCandidate? MatchExHazard(Window w, int side, int bas, double x, double y)
    {
        if (!HasExSlots(w)) return null;
        var memo = new Dictionary<int, Dictionary<int, ExItem>>();
        Dictionary<int, ExItem> AtTick(int j)
        {
            if (!memo.TryGetValue(j, out var got))
            {
                got = new Dictionary<int, ExItem>();
                foreach (var o in ExItems.At(w, j, side)) got[o.Slot] = o;
                memo[j] = got;
            }
            return got;
        }
        return MatchExHazardCore(bas, x, y, AtTick,
                                 (slot, bb, back) => ExItems.TrailTick(w, slot, bb, back),
                                 (slot, i) => SlotSegments.At(w, slot, i));
    }

    public static HazardBoardCandidate? MatchExHazardCore(int bas, double x, double y,
        Func<int, IReadOnlyDictionary<int, ExItem>> at,
        Func<int, int, int, int?> trailTickOf,
        Func<int, int, (int Start, int End)?> segmentAt)
    {
        int? slot = ExIdentifyCore(bas, x, y, at, trailTickOf, segmentAt)?.Slot
                    ?? ExIdentifyCore(bas - 1, x, y, at, trailTickOf, segmentAt)?.Slot;
        return slot is int s ? new HazardBoardCandidate(HazardMatchKind.Ex, s, x, y, null) : null;
    }

    private static HazardBoardCandidate? MatchOnBoard(HazardElement e, IEnumerable<HazardBoardCandidate> candidates)
    {
        if (e.Kind == HazardKind.Obb)
        {
            foreach (var c in candidates)
            {
                if (c.Kind != HazardMatchKind.Laser) continue;
                if (Math.Abs(c.X - e.PivotX) > PivotTol || Math.Abs(c.Y - e.PivotY) > PivotTol) continue;
                if (c.Angle is double a && Math.Abs(a - e.Angle) <= AngleTol) return c;
            }
            return null;
        }
        foreach (var c in candidates)
        {
            if (c.Kind is HazardMatchKind.Laser or HazardMatchKind.Ex) continue;
            if (Math.Abs(c.X - e.X) <= PivotTol && Math.Abs(c.Y - e.Y) <= PivotTol) return c;
        }
        return null;
    }


    public static EnemyIdentity? EnemyIdentify(IHitSource src, int? side = null, int? eventIndex = null)
    {
        if (src.Events.Count == 0) return null;
        if (HitType(src, side, eventIndex) != HitTypeKind.Contact) return null;
        int? k = PickEventIndex(src, side, eventIndex);
        if (k is null) return null;
        var e = src.Events[k.Value];
        int sd = e.Side;
        int? bas = null;
        double? hx = null, hy = null;
        foreach (var d in NearTicks)
        {
            if ((( Word(src, HitKindName(sd), e.Raw + d) ?? 0u) & HitValid) == 0) continue;
            bas = e.Raw + d;
            hx = src.MainAt(HitXName(sd), bas.Value);
            hy = src.MainAt(HitYName(sd), bas.Value);
            break;
        }
        if (bas is null || hx is null || hy is null || bas.Value < 0 || bas.Value >= src.TickCount) return null;
        var found = src.BoardAt(bas.Value, sd).Enemies
            .Where(o => o.X is not null && o.Y is not null
                        && Math.Abs(o.X.Value - hx.Value) <= PivotTol
                        && Math.Abs(o.Y.Value - hy.Value) <= PivotTol)
            .ToList();
        var slots = found.Select(o => o.Slot).Distinct().OrderBy(x => x).ToList();
        return slots.Count == 1
            ? new EnemyIdentity(found[0].Slot, slots.Count, slots, found[0].EnemyClass)
            : new EnemyIdentity(null, slots.Count, slots, null);
    }

    public static EnemyIdentity? EnemyIdentify(Window w, int? side = null, int? eventIndex = null) =>
        EnemyIdentify(WindowHits.For(w), side, eventIndex);

    public static EnemyCandidateSet EnemyCandidatesOf(IHitSource src, WindowEvent e, int side,
                                                      double playerSide, int span = DefaultSpan)
    {
        var found = new List<int>();
        var sure = new List<int>();
        for (int k = e.Index - span; k <= e.Index + span; k++)
        {
            if (k < 0 || k >= src.TickCount) continue;
            var px = src.MainAt(PosXName(side), k);
            var py = src.MainAt(PosYName(side), k);
            if (px is null || py is null) continue;
            foreach (var o in src.BoardAt(k, side).Enemies)
            {
                var box = EnemySize.Half(o.EnemyClass);
                if (box is null || box.None || o.X is null || o.Y is null) continue;
                if (!CoversRect(px.Value, py.Value, o.X.Value, o.Y.Value,
                                box.HalfWMax, box.HalfHMax, playerSide)) continue;
                if (!found.Contains(o.Slot)) found.Add(o.Slot);
                if (!sure.Contains(o.Slot)
                    && CoversRect(px.Value, py.Value, o.X.Value, o.Y.Value,
                                  box.HalfW, box.HalfH, playerSide))
                    sure.Add(o.Slot);
            }
        }
        found.Sort();
        sure.Sort();
        return new EnemyCandidateSet(found, sure, !found.SequenceEqual(sure));
    }

    public static EnemyCandidateSet EnemyCandidatesOf(Window w, WindowEvent e, int side,
                                                      double playerSide, int span = DefaultSpan) =>
        EnemyCandidatesOf(WindowHits.For(w), e, side, playerSide, span);

    public static bool CoversRect(double px, double py, double cx, double cy,
                                  double halfW, double halfH, double playerSide) =>
        Math.Abs(px - cx) <= halfW + playerSide * 0.5
        && Math.Abs(py - cy) <= halfH + playerSide * 0.5;


    public static ExIdentity? ExIdentify(Window w, int? side = null, int? eventIndex = null)
    {
        var src = WindowHits.For(w);
        if (src.Events.Count == 0) return null;
        if (!HasExSlots(w)) return null;
        var ht = HitType(src, side, eventIndex);
        if (ht != HitTypeKind.ExCircle && ht != HitTypeKind.Contact) return null;
        int? k = PickEventIndex(src, side, eventIndex);
        if (k is null) return null;
        var e = src.Events[k.Value];
        int sd = e.Side;
        int? bas = null; double? hx = null, hy = null;
        foreach (var d in NearTicks)
        {
            if (((Word(src, HitKindName(sd), e.Raw + d) ?? 0u) & HitValid) == 0) continue;
            bas = e.Raw + d;
            hx = src.MainAt(HitXName(sd), bas.Value);
            hy = src.MainAt(HitYName(sd), bas.Value);
            break;
        }
        if (bas is null || hx is null || hy is null || bas.Value < 0 || bas.Value >= w.TickCount) return null;
        int b = bas.Value;

        var memo = new Dictionary<int, Dictionary<int, ExItem>>();
        Dictionary<int, ExItem> AtTick(int j)
        {
            if (!memo.TryGetValue(j, out var got))
            {
                got = new Dictionary<int, ExItem>();
                foreach (var o in ExItems.At(w, j, sd)) got[o.Slot] = o;
                memo[j] = got;
            }
            return got;
        }
        return ExIdentifyCore(b, hx.Value, hy.Value, AtTick,
                              (slot, bb, back) => ExItems.TrailTick(w, slot, bb, back),
                              (slot, i) => SlotSegments.At(w, slot, i));
    }

    public static ExIdentity? ExIdentifyCore(int bas, double hx, double hy,
        Func<int, IReadOnlyDictionary<int, ExItem>> at,
        Func<int, int, int, int?> trailTickOf,
        Func<int, int, (int Start, int End)?> segmentAt)
    {
        double tol = PivotTol;
        var found = new List<(ExItem Item, string How)>();

        foreach (var o in at(bas + 1).Values)
        {
            var how = ExHitHow(o.Hitbox, 1);
            if (how is null) continue;
            if (Math.Abs(o.X - hx) <= tol && Math.Abs(o.Y - hy) <= tol) found.Add((o, how));
        }
        var baseItems = at(bas);
        foreach (var o in baseItems.Values)
        {
            var how = ExHitHow(o.Hitbox, 0);
            if (how is null) continue;
            if (Math.Abs(o.X - hx) <= tol && Math.Abs(o.Y - hy) <= tol) found.Add((o, how));
        }
        foreach (var (slot, o0) in baseItems)
        {
            var trail = o0.Hitbox?.Trail;
            if (trail is null) continue;
            foreach (var back in trail)
            {
                if (back <= 0) continue;
                var j = trailTickOf(slot, bas, back);
                if (j is null) continue;
                if (!at(j.Value).TryGetValue(slot, out var o)) continue;
                var how = ExHitHow(o.Hitbox, -back);
                if (how is null) continue;
                if (Math.Abs(o.X - hx) <= tol && Math.Abs(o.Y - hy) <= tol) found.Add((o, how));
            }
        }
        if (found.Count == 0)
            found = ExHitNextTickExtrapolated(bas, hx, hy, at, segmentAt);

        var slots = found.Select(f => f.Item.Slot).Distinct().OrderBy(x => x).ToList();
        if (slots.Count != 1)
            return new ExIdentity(null, HowExHit, slots.Count, slots, null, null);
        var (item, how1) = found[0];
        return new ExIdentity(item.Slot, how1, 1, slots, item.ExType, item.Name);
    }

    private static List<(ExItem Item, string How)> ExHitNextTickExtrapolated(
        int bas, double hx, double hy,
        Func<int, IReadOnlyDictionary<int, ExItem>> at,
        Func<int, int, (int Start, int End)?> segmentAt)
    {
        var outList = new List<(ExItem, string)>();
        double tol = PivotTol;
        foreach (var o in at(bas).Values)
        {
            if (ExHitHow(o.Hitbox, 1) != HowExHitNextTick) continue;
            var p = ExExtrapolatedXy(o.Slot, bas, at, segmentAt);
            if (p is null) continue;
            if (Math.Abs(p.Value.X - hx) <= tol && Math.Abs(p.Value.Y - hy) <= tol)
                outList.Add((o, HowExHitNextTickExtrapolated));
        }
        return outList;
    }

    private static (double X, double Y)? ExExtrapolatedXy(int slot, int bas,
        Func<int, IReadOnlyDictionary<int, ExItem>> at,
        Func<int, int, (int Start, int End)?> segmentAt)
    {
        if (bas - 2 < 0) return null;
        var seg = segmentAt(slot, bas);
        if (seg is null || seg.Value.Start > bas - 2) return null;
        var pts = new (double X, double Y)[3];
        int idx = 0;
        foreach (var j in new[] { bas, bas - 1, bas - 2 })
        {
            if (!at(j).TryGetValue(slot, out var o)) return null;
            pts[idx++] = (o.X, o.Y);
        }
        return ExItems.ExtrapolateNext(pts[0], pts[1], pts[2]);
    }

    private static string? ExHitHow(ExHitbox? box, int off)
    {
        if (box is null || box.EffectOnly) return null;
        if (box.Center == "next_tick") return off == 1 ? HowExHitNextTick : null;
        if (box.Trail is { Count: > 0 } trail)
        {
            if (!trail.Contains(-off)) return null;
            return off == 0 ? HowExHit : HowExHitTrail;
        }
        return off == 0 ? HowExHit : null;
    }


    public static bool HasExSlots(IHitSource src) => CoordRing.SlotCountHasEx(src.SlotCount);

    public static bool HasExSlots(Window w) => CoordRing.SlotCountHasEx(w.Meta.SlotCount);

    public static HitCandidateSet? Resolve(IHitSource src, int? side = null, int? eventIndex = null,
                                           double? playerSide = null, double margin = DefaultMargin,
                                           int span = DefaultSpan) =>
        ResolveCore(src, side, eventIndex, playerSide, margin, span, exIdentify: null);

    public static HitCandidateSet? Resolve(Window w, int? side = null, int? eventIndex = null,
                                           double? playerSide = null, double margin = DefaultMargin,
                                           int span = DefaultSpan) =>
        ResolveCore(WindowHits.For(w), side, eventIndex, playerSide, margin, span,
                   exIdentify: () => ExIdentify(w, side, eventIndex));

    private static HitCandidateSet? ResolveCore(IHitSource src, int? side, int? eventIndex,
                                                double? playerSide, double margin, int span,
                                                Func<ExIdentity?>? exIdentify)
    {
        int? k = PickEventIndex(src, side, eventIndex);
        if (k is null) return null;
        var e = src.Events[k.Value];
        int sd = e.Side;
        var ht = HitType(src, side, eventIndex);
        bool hasEx = HasExSlots(src);

        HitCandidateSet Out(HitConfidence conf, int? resolved, IReadOnlyList<int> slots, string how,
                            string? why, bool ranged = false, IReadOnlyList<int>? sure = null,
                            bool exChecked = true, bool howFromEx = false) =>
            new()
            {
                Type = ht,
                Side = sd,
                EventIndex = k.Value,
                Confidence = conf,
                ResolvedSlot = resolved,
                Slots = slots,
                SureSlots = sure ?? [],
                SizeRanged = ranged,
                HowKey = how,
                HowFromEx = howFromEx,
                WhyKey = why,
                ExPathChecked = exChecked,
            };

        if (ht == HitTypeKind.Laser)
        {
            var got = LaserIdentify(src, side, eventIndex, margin);
            if (got is not null && (got.How == LaserHow.Pivot || got.How == LaserHow.PivotAngle))
                return Out(HitConfidence.Resolved, got.Slot, [], "", WhyResolved);
            if (got is not null && got.FanSlots.Count > 0)
                return got.How == LaserHow.PivotGeometry
                    ? Out(HitConfidence.Narrowed, null, got.FanSlots, HowLaserFan, null)
                    : Out(HitConfidence.Guessed, null, got.FanSlots, HowLaserGeometry, null);
            var c = LaserCandidatesOf(src, sd, margin);
            var slots = c.Select(r => r.Slot).ToList();
            return c.Count > 0
                ? Out(HitConfidence.Guessed, null, slots, HowLaserGeometry, null)
                : Out(HitConfidence.Unavailable, null, [], "", WhyLaserNotFound);
        }

        if (ht is HitTypeKind.ExCircle or HitTypeKind.Contact)
        {
            bool wantRect = ht == HitTypeKind.Contact;
            if (!hasEx && !wantRect)
                return Out(HitConfidence.Unavailable, null, [], "", WhyNoExSlots);

            ExIdentity? exGot = null;
            bool exChecked = false;
            if (hasEx && exIdentify is not null)
            {
                exGot = exIdentify();
                exChecked = true;
                if (exGot is not null && exGot.Slot is not null)
                    return Out(HitConfidence.Resolved, exGot.Slot, [], "", WhyResolved,
                               exChecked: true, howFromEx: true);
                if (exGot is not null && exGot.MatchedSlots.Count > 0)
                    return Out(HitConfidence.Narrowed, null, exGot.MatchedSlots, exGot.How, null,
                               exChecked: true, howFromEx: true);
            }

            if (wantRect)
            {
                var foe = EnemyIdentify(src, side, eventIndex);
                if (foe is not null && foe.Slot is not null)
                    return Out(HitConfidence.Resolved, foe.Slot, [], "", WhyResolved, exChecked: exChecked);
                if (foe is not null && foe.MatchedSlots.Count > 0)
                    return Out(HitConfidence.Narrowed, null, foe.MatchedSlots, HowEnemyHitXy, null,
                               exChecked: exChecked);
            }
            if (playerSide is null)
                return Out(HitConfidence.Unavailable, null, [], "", WhyNoPlayerSide, exChecked: exChecked);
            if (!wantRect)
                return Out(HitConfidence.Unavailable, null, [], "", WhyExNotPorted, exChecked: exChecked);
            var enemy = EnemyCandidatesOf(src, e, sd, playerSide.Value, span);
            if (enemy.Slots.Count > 0)
                return Out(HitConfidence.Guessed, null, enemy.Slots,
                           enemy.Ranged ? HowEnemyRanged : HowEnemy, null,
                           ranged: enemy.Ranged, sure: enemy.SureSlots, exChecked: exChecked);
            return Out(HitConfidence.Unavailable, null, [], "",
                       hasEx ? WhyEnemyNotFound : WhyNoExSlots, exChecked: exChecked);
        }

        if (ht == HitTypeKind.Bullet)
        {
            var got = BulletCulprit(src, side, eventIndex);
            return got is not null
                ? Out(HitConfidence.Resolved, got, [], "", WhyResolved)
                : Out(HitConfidence.Unavailable, null, [], "", WhyBulletNoPtr);
        }
        return Out(HitConfidence.Unavailable, null, [], "", WhyUnknownType);
    }


    public static TrailPoint?[] Trail(Window w, int slot, int upto, int? at = null)
    {
        if (CoordRing.IsExSlot(slot))
            throw new NotSupportedException(
                $"slot {slot} は Ex 枠。Ex の座標補正（_ex_board_xy）を移すまで軌跡は出せない");
        return TrailCore(w.TickCount, upto, at,
                         i => SlotSegments.At(w, slot, i),
                         i => new TrailPoint(w.Float(CoordRing.ColX(slot), i),
                                             w.Float(CoordRing.ColY(slot), i)));
    }

    public static TrailPoint?[] TrailCore(int tickCount, int upto, int? at,
                                          Func<int, (int Start, int End)?> segmentAt,
                                          Func<int, TrailPoint> pointAt)
    {
        int end = Math.Min(upto, tickCount - 1);
        var keep = at is null ? null : segmentAt(at.Value);
        var outv = new TrailPoint?[Math.Max(0, end + 1)];
        for (int i = 0; i <= end; i++)
        {
            var seg = segmentAt(i);
            if (seg is null || (keep is not null && seg.Value != keep.Value)) { outv[i] = null; continue; }
            if (keep is null && i == seg.Value.Start && segmentAt(i - 1) is not null)
            {
                outv[i] = null;
                continue;
            }
            outv[i] = pointAt(i);
        }
        return outv;
    }


    private static int? PickEventIndex(IHitSource src, int? side, int? eventIndex)
    {
        if (eventIndex is int k && k >= 0 && k < src.Events.Count) return k;
        return src.PrimaryEventIndex(side);
    }

    private static List<WindowEvent> EventFan(IHitSource src, int? side, int? eventIndex)
    {
        var ev = src.Events;
        if (eventIndex is int k && k >= 0 && k < ev.Count) return [ev[k]];
        var byside = ev.Where(x => side is null || x.Side == side.Value).ToList();
        return byside.Count > 0 ? byside : ev.ToList();
    }


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ($"母数: 語の表が 0 行でない（種別 {HitTypeJaCount} / 理由 {WhyJaCount}"
                      + $" / 敵の幾何 {EnemyHowJaCount} / 敵の確定 {EnemyIdentifyHowJaCount}"
                      + $" / Ex {ExHowJaCount}）", () =>
        {
            if (HitTypeJaCount == 0) return "種別の語が 0 行";
            if (WhyJaCount == 0) return "理由の文が 0 行";
            if (EnemyHowJaCount == 0) return "敵の幾何の文が 0 行";
            if (EnemyIdentifyHowJaCount == 0) return "敵の確定の文が 0 行";
            return ExHowJaCount == 0 ? "Ex の確定の文が 0 行（★Ex の道は未移植だが、表は在るはず）" : null;
        });

        yield return ("母数: 理由の表に出る鍵が、C# 側の名前とちょうど一致する（★片方が古くならないように）", () =>
        {
            var here = new HashSet<string>(
                new[] { WhyResolved, WhyNoExSlots, WhyEnemyNotFound, WhyNoPlayerSide,
                        WhyExNotFound, WhyLaserNotFound, WhyBulletNoPtr, WhyUnknownType },
                StringComparer.Ordinal);
            var fromTable = new HashSet<string>(WhyJaMap.Keys, StringComparer.Ordinal);
            var missing = fromTable.Except(here).ToList();
            var extra = here.Except(fromTable).ToList();
            if (missing.Count > 0) return "表にあって C# に無い鍵: " + string.Join(",", missing);
            if (extra.Count > 0) return "C# にあって表に無い鍵: " + string.Join(",", extra);
            return fromTable.Contains(WhyExNotPorted)
                ? $"{WhyExNotPorted} が表に入った（Python 側にこの鍵は無いはず）" : null;
        });

        yield return ("母数: 候補の出し方の鍵が表から引ける（★敵の 3 つ。鍵のまま返っていない）", () =>
        {
            foreach (var key in new[] { HowEnemy, HowEnemyRanged, HowEnemyHitXy })
                if (HowJa(key) == key) return $"鍵 {key} の文が表から引けない";
            return HowJa("no_such_how") == "no_such_how" ? null : "表に無い鍵を鍵のまま返していない";
        });

        yield return ("★衝突の見張り: EX_HOW と ENEMY_IDENTIFY_HOW が★同じ鍵で違う文を持つ", () =>
        {
            if (!ExHowJaMap.TryGetValue(HowEnemyHitXy, out var ex)) return null;
            if (!EnemyIdentifyHowJaMap.TryGetValue(HowEnemyHitXy, out var foe))
                return $"敵の確定の表に鍵 {HowEnemyHitXy} が無い（C# の鍵が表とずれた）";
            if (ex == foe) return "2 つの表の文が同じになった（見分けが付かない）";
            return HowJa(HowEnemyHitXy) == foe ? null : "HowJa() が Ex の表を引いている";
        });

        yield return ("合成: 種別の語が 4 通りとも引ける（★読めない種別は null）", () =>
        {
            var got = new HashSet<string>(StringComparer.Ordinal);
            foreach (HitTypeKind k in Enum.GetValues<HitTypeKind>())
            {
                var ja = TypeJa(k);
                if (ja is null) return $"種別 {k} の語が null";
                if (ja == ((int)k).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    return $"種別 {k} が番号のまま返った（表に無い）";
                got.Add(ja);
            }
            if (got.Count != 4) return $"語が {got.Count} 通り（4 通りのはず）";
            return TypeJa(null) is null ? null : "読めない種別に語が付いた";
        });

        yield return ("★否定: 旧（＝表に無い鍵を空文字で返す）なら、resolved と見分けが付かない", () =>
        {
            string Legacy(string key) => WhyJaMap.TryGetValue(key, out var ja) ? ja : "";
            if (Legacy(WhyResolved) != "")
                return "旧の写しで resolved が空にならない ——否定テストが効いていない";
            if (Legacy(WhyExNotPorted) != "")
                return "旧の写しで表に無い鍵が空にならない ——否定テストが効いていない";
            if (Legacy(WhyResolved) != Legacy(WhyExNotPorted)) return "旧で見分けが付いてしまう";
            if (WhyJa(WhyResolved) != "") return $"resolved が空文字でない（{WhyJa(WhyResolved)}）";
            return WhyJa(WhyExNotPorted) == WhyExNotPorted
                ? null : $"表に無い鍵が {WhyJa(WhyExNotPorted)} になった";
        });

        yield return ("母数: 被弾の種別が 4 通りすべて出る（★どれかへ潰れていない）", () =>
        {
            var got = new HashSet<HitTypeKind>();
            for (uint t = 0; t <= 3; t++)
            {
                var one = TypeOf(HitValid | (t << HitTypeShift));
                if (one is null) return $"種別 {t} が null になった";
                got.Add(one.Value);
            }
            return got.Count == 4 ? null : $"種別 {got.Count} 通り（4 通りのはず）";
        });

        yield return ("★VALID が立っていなければ種別は null（★0 を「弾」に倒さない）", () =>
        {
            if (TypeOf(0u) is HitTypeKind a) return $"語 0 が種別 {a} になった";
            return TypeOf(3u << HitTypeShift) is HitTypeKind b ? $"VALID 無しが種別 {b} になった" : null;
        });

        yield return ("合成: hit_obj_ptr → 弾配列の番号（★端数と 0 は null）", () =>
        {
            const uint mgr = 0x0A000000u;
            uint Ptr(long n) => (uint)(mgr + BulletArrayBase + n * BulletStride);
            if (BulletSlotOfPtr(Ptr(0), mgr) != 0) return "枠 0 が解けない";
            if (BulletSlotOfPtr(Ptr(12), mgr) != 12) return "枠 12 が解けない";
            if (BulletSlotOfPtr(Ptr(0) + 4, mgr) is int bad) return $"端数のポインタが枠 {bad} になった";
            if (BulletSlotOfPtr(mgr, mgr) is int neg) return $"配列より手前が枠 {neg} になった";
            if (BulletSlotOfPtr(0u, mgr) is not null || BulletSlotOfPtr(Ptr(3), 0u) is not null)
                return "0 のポインタ／mgr に枠が付いた";
            int over = AnalysisTables.CoordBulletSlots;
            return BulletSlotOfPtr(Ptr(over), mgr) is int o ? $"枠数を越えた {o} が返った" : null;
        });

        yield return ("合成: 弾の通し番号が、列名の索引と同じ答えになる（★別の道から）", () =>
        {
            int n = 0;
            foreach (var side in new[] { 1, 2 })
            {
                var gs = GlobalBulletSlot(side, 5, CoordRing.SlotCount);
                if (gs is null) return $"側 {side} の弾 5 が引けない";
                var want = $"p{side}_b5";
                if (CoordRing.BaseName(gs.Value) != want)
                    return $"通し番号 {gs} の名前 {CoordRing.BaseName(gs.Value)} ≠ {want}";
                n++;
            }
            if (GlobalBulletSlot(1, AnalysisTables.CoordBulletSlots, CoordRing.SlotCount) is not null)
                return "弾の枠数を越えた番号が引けてしまう";
            return GlobalBulletSlot(2, 5, 1) is null ? (n == 2 ? null : $"母数 {n} ≠ 2") : "窓の外の枠が引けた";
        });

        yield return ("合成: 矩形の判定は自機の一辺が「半分」効く（★そのままではない）", () =>
        {
            if (!CoversRect(11.0, 0.0, 0.0, 0.0, 10.0, 10.0, 2.0)) return "半分が効いていない（届かない）";
            if (CoversRect(11.1, 0.0, 0.0, 0.0, 10.0, 10.0, 2.0)) return "半分より広く効いている";
            return CoversRect(0.0, 0.0, 0.0, 0.0, 10.0, 10.0, 0.0) ? null : "中心が当たらない";
        });

        yield return ("★否定: 旧（＝内接楕円で判定する）なら、AABB の角にある被弾を落とす", () =>
        {
            const double hw = 12.0, hh = 12.0, ps = 2.0;
            double cx = hw + ps * 0.5, cy = hh + ps * 0.5;
            if (!LegacyEllipseCovers(0.0, 0.0, 0.0, 0.0, hw, hh, ps))
                return "旧の写しが中心すら当たらない（否定テストが効いていない）";
            if (LegacyEllipseCovers(cx, cy, 0.0, 0.0, hw, hh, ps))
                return "旧の写し（楕円）が角でも当たった ——否定テストが効いていない";
            return CoversRect(cx, cy, 0.0, 0.0, hw, hh, ps) ? null : "新（AABB）が角で当たらない";
        });

        yield return ("合成: 点と線分の距離（★端の外は端点までの距離）", () =>
        {
            if (Math.Abs(PointSegmentDistance(5.0, 3.0, 0.0, 0.0, 10.0, 0.0) - 3.0) > 1e-9)
                return "線分の真横で 3.0 にならない";
            if (Math.Abs(PointSegmentDistance(-4.0, 3.0, 0.0, 0.0, 10.0, 0.0) - 5.0) > 1e-9)
                return "端の外で端点までの距離にならない";
            return Math.Abs(PointSegmentDistance(4.0, 5.0, 1.0, 1.0, 1.0, 1.0) - 5.0) <= 1e-9
                ? null : "長さ 0 の線分で端点までの距離にならない";
        });

        yield return ("合成: レーザーの線分は pivot が始点（★半幅は生成表の係数）", () =>
        {
            var it = FakeLaser(0, x: 10.0, y: 20.0, angle: 0.0, tail: 0.0, head: 100.0,
                               width: 8.0, lethal: true);
            var seg = LaserSegmentOf(it);
            if (Math.Abs(seg.X1 - 10.0) > 1e-9 || Math.Abs(seg.Y1 - 20.0) > 1e-9)
                return $"始点 ({seg.X1},{seg.Y1}) が pivot ではない";
            if (Math.Abs(seg.X2 - 110.0) > 1e-9) return $"終点 {seg.X2} ≠ 110";
            return Math.Abs(seg.Half - 8.0 * AnalysisTables.LaserHalfFactor) < 1e-9
                ? null : $"半幅 {seg.Half} が係数を通っていない";
        });

        yield return ("合成: レーザーの候補は★致死のものだけ（生存 ≠ 致死）", () =>
        {
            var src = new FakeSource { TickCount = 4 };
            src.Ev.Add(new WindowEvent(1, 1, 1, "life_raw"));
            src.Main(PosXName(1), 1, 0.0);
            src.Main(PosYName(1), 1, 0.0);
            var board = new Board();
            board.Lasers.Add(FakeLaser(700, 0.0, 0.0, 0.0, 0.0, 100.0, 8.0, lethal: true));
            board.Lasers.Add(FakeLaser(701, 0.0, 1.0, 0.0, 0.0, 100.0, 8.0, lethal: false));
            src.Boards[(1, 1)] = board;
            var got = LaserCandidatesOf(src, 1);
            if (got.Count != 1) return $"候補 {got.Count} 本（1 本のはず。致死でないものを拾っている）";
            return got[0].Slot == 700 ? null : $"候補が枠 {got[0].Slot}";
        });

        yield return ("合成: 弾の被弾は hit_obj_ptr で確定する（★候補は空のまま）", () =>
        {
            var src = BulletHit(mgr: 0x0A000000u, n: 7);
            var got = Resolve(src, playerSide: 2.0);
            if (got is null) return "事象があるのに null";
            if (got.Type != HitTypeKind.Bullet) return $"種別 {got.Type}";
            if (got.Confidence != HitConfidence.Resolved) return $"確度 {got.Confidence}";
            if (got.Slots.Count != 0) return $"確定したのに候補が {got.Slots.Count} 件";
            var want = GlobalBulletSlot(1, 7, CoordRing.SlotCount);
            return got.ResolvedSlot == want ? null : $"確定した枠 {got.ResolvedSlot} ≠ {want}";
        });

        yield return ("合成: ポインタが取れない弾は★『決まった』に倒さず理由を返す", () =>
        {
            var src = BulletHit(mgr: 0x0A000000u, n: 7);
            src.Mains.Remove((HitObjPtrName(1), 5));
            var got = Resolve(src, playerSide: 2.0);
            if (got is null) return "事象があるのに null";
            if (got.Confidence != HitConfidence.Unavailable) return $"確度 {got.Confidence}";
            return got.WhyKey == WhyBulletNoPtr ? null : $"理由 {got.WhyKey} ≠ {WhyBulletNoPtr}";
        });

        yield return ("合成: 体当たりは hit_x,hit_y の一致で 1 体に確定する", () =>
        {
            var src = ContactHit(enemyX: 30.0, enemyY: 40.0, hitX: 30.0, hitY: 40.0);
            var got = Resolve(src, playerSide: 2.0);
            if (got is null) return "事象があるのに null";
            if (got.Type != HitTypeKind.Contact) return $"種別 {got.Type}";
            if (got.Confidence != HitConfidence.Resolved) return $"確度 {got.Confidence}";
            if (got.ResolvedSlot != ContactEnemySlot) return $"確定した枠 {got.ResolvedSlot}";
            return got.ExPathChecked ? "Ex の道を見ていないのに ExPathChecked が真" : null;
        });

        yield return ("合成: 一致しなければ幾何の候補へ落ちる（★確度は『候補』）", () =>
        {
            var src = ContactHit(enemyX: 0.0, enemyY: 0.0, hitX: 999.0, hitY: 999.0);
            var got = Resolve(src, playerSide: 2.0);
            if (got is null) return "事象があるのに null";
            if (got.Confidence != HitConfidence.Guessed) return $"確度 {got.Confidence}";
            if (got.Slots.Count != 1 || got.Slots[0] != ContactEnemySlot)
                return $"候補 [{string.Join(",", got.Slots)}]";
            return got.HowKey is HowEnemy or HowEnemyRanged ? null : $"出し方 {got.HowKey}";
        });

        yield return ("合成: 自機の一辺を渡さなければ幾何の道は止まる（★0 件にしない）", () =>
        {
            var src = ContactHit(enemyX: 0.0, enemyY: 0.0, hitX: 999.0, hitY: 999.0);
            var got = Resolve(src, playerSide: null);
            if (got is null) return "事象があるのに null";
            return got.WhyKey == WhyNoPlayerSide ? null : $"理由 {got.WhyKey} ≠ {WhyNoPlayerSide}";
        });

        yield return ("合成: Ex サークルは★『探していない』を『探したが無い』と混ぜない（Window 抜きの口）", () =>
        {
            var src = ContactHit(enemyX: 0.0, enemyY: 0.0, hitX: 999.0, hitY: 999.0);
            src.Main(HitKindName(1), 5, HitValid | ((uint)HitTypeKind.ExCircle << HitTypeShift));
            var got = Resolve(src, playerSide: 2.0);
            if (got is null) return "事象があるのに null";
            if (got.Type != HitTypeKind.ExCircle) return $"種別 {got.Type}";
            if (got.WhyKey != WhyExNotPorted) return $"理由 {got.WhyKey} ≠ {WhyExNotPorted}";
            return got.ExPathChecked ? "Ex を見ていないのに ExPathChecked が真" : null;
        });


        yield return ("合成: ExIdentifyCore は ring(base) の一致で 1 枠に確定する（off=0）", () =>
        {
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [1] = FakeEx(1, 5.0, 5.0, FakeExBox()) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = ExIdentifyCore(10, 5.0, 5.0, At, (_, _, _) => null, (_, _) => null);
            if (got is null) return "一致するのに null";
            if (got.Slot != 1) return $"確定した枠 {got.Slot}";
            return got.How == HowExHit ? null : $"根拠 {got.How}";
        });

        yield return ("合成: 咲夜のトレイルは★枠自身の Hitbox.Trail だけを見て段前の位置と合わせる", () =>
        {
            var box = FakeExBox(trail: [0, 7]);
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [1] = FakeEx(1, 999.0, 999.0, box) },
                [3] = new() { [1] = FakeEx(1, 1.0, 2.0, box) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            int? TrailTickOf(int slot, int bas, int back) => slot == 1 && bas == 10 && back == 7 ? 3 : null;
            var got = ExIdentifyCore(10, 1.0, 2.0, At, TrailTickOf, (_, _) => null);
            if (got is null) return "一致するのに null";
            if (got.Slot != 1) return $"確定した枠 {got.Slot}";
            return got.How == HowExHitTrail ? null : $"根拠 {got.How}（トレイルのはず）";
        });

        yield return ("合成: 文（next_tick）は ring(base+1) の一致で「+1tick」に確定する", () =>
        {
            var box = FakeExBox(center: "next_tick");
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [11] = new() { [2] = FakeEx(2, 7.0, 8.0, box) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = ExIdentifyCore(10, 7.0, 8.0, At, (_, _, _) => null, (_, _) => null);
            if (got is null) return "一致するのに null";
            if (got.Slot != 2) return $"確定した枠 {got.Slot}";
            return got.How == HowExHitNextTick ? null : $"根拠 {got.How}";
        });


        yield return ("合成: MatchExHazardCore はまず bas（同 tick）で探す", () =>
        {
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [1] = FakeEx(1, 5.0, 5.0, FakeExBox()) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = MatchExHazardCore(10, 5.0, 5.0, At, (_, _, _) => null, (_, _) => null);
            return got is { Kind: HazardMatchKind.Ex, Slot: 1 } ? null : $"対応づけが違う（{got}）";
        });

        yield return ("合成: bas に無ければ bas-1（実測: 霊夢の陰陽玉）へ落ちて探す", () =>
        {
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [9] = new() { [3] = FakeEx(3, -36.3, 380.8, FakeExBox()) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = MatchExHazardCore(10, -36.3, 380.8, At, (_, _, _) => null, (_, _) => null);
            return got is { Kind: HazardMatchKind.Ex, Slot: 3 } ? null : $"対応づけが違う（{got}）";
        });

        yield return ("★否定: bas だけしか見ないと、bas-1 にしか居ない Ex を見落とす", () =>
        {
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [9] = new() { [3] = FakeEx(3, -36.3, 380.8, FakeExBox()) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var legacy = ExIdentifyCore(10, -36.3, 380.8, At, (_, _, _) => null, (_, _) => null);
            return legacy?.Slot is null ? null : "旧の呼び方でも見つかってしまい、否定テストとして効いていない";
        });

        yield return ("合成: bas と bas-1 の両方に居れば bas（主）を優先する", () =>
        {
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [1] = FakeEx(1, 5.0, 5.0, FakeExBox()) },
                [9] = new() { [2] = FakeEx(2, 5.0, 5.0, FakeExBox()) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = MatchExHazardCore(10, 5.0, 5.0, At, (_, _, _) => null, (_, _) => null);
            return got is { Slot: 1 } ? null : $"bas-1（副）を優先してしまった（{got}）";
        });

        yield return ("合成: どちらにも無ければ null（嘘の対応づけを返さない）", () =>
        {
            Dictionary<int, ExItem> At(int j) => [];
            return MatchExHazardCore(10, 5.0, 5.0, At, (_, _, _) => null, (_, _) => null) is null
                ? null : "何も無いのに対応づいた";
        });

        yield return ("★厳密一致が在れば外挿は走らない（確定済みの枠選択を 1 件も変えない）", () =>
        {
            var plain = FakeExBox();
            var next = FakeExBox(center: "next_tick");
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [1] = FakeEx(1, 5.0, 5.0, plain), [2] = FakeEx(2, 5.0, 5.0, next) },
                [9] = new() { [2] = FakeEx(2, 5.0, 5.0, next) },
                [8] = new() { [2] = FakeEx(2, 5.0, 5.0, next) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = ExIdentifyCore(10, 5.0, 5.0, At, (_, _, _) => null, (slot, i) => (0, 10));
            if (got is null) return "一致するのに null";
            return got.Slot == 1 ? null : $"確定した枠 {got.Slot}（外挿が紛れ込んで Narrowed 側に倒れた）";
        });

        yield return ("合成: 外挿は厳密一致が無いときだけ 2 次で 1 枠に確定する", () =>
        {
            var next = FakeExBox(center: "next_tick");
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [2] = FakeEx(2, 3.0, 0.0, next) },
                [9] = new() { [2] = FakeEx(2, 1.0, 0.0, next) },
                [8] = new() { [2] = FakeEx(2, 0.0, 0.0, next) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = ExIdentifyCore(10, 6.0, 0.0, At, (_, _, _) => null, (slot, i) => (0, 10));
            if (got is null) return "外挿で決まるはずが null";
            if (got.Slot != 2) return $"確定した枠 {got.Slot}";
            return got.How == HowExHitNextTickExtrapolated ? null : $"根拠 {got.How}（外挿のはず）";
        });

        yield return ("★外挿は 3 tick とも★同じ枠でなければしない（枠の使い回しを跨がない）", () =>
        {
            var next = FakeExBox(center: "next_tick");
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [2] = FakeEx(2, 3.0, 0.0, next) },
                [9] = new() { [2] = FakeEx(2, 1.0, 0.0, next) },
                [8] = new() { [2] = FakeEx(2, 0.0, 0.0, next) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = ExIdentifyCore(10, 6.0, 0.0, At, (_, _, _) => null, (slot, i) => (10, 10));
            return got?.Slot is null ? null : $"確定した枠 {got.Slot}（区間を跨いで外挿した）";
        });

        yield return ("合成: EffectOnly・判定を持たない枠は★位置が一致していても候補にならない", () =>
        {
            var effectOnly = FakeExBox(effectOnly: true);
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new()
                {
                    [1] = FakeEx(1, 5.0, 5.0, effectOnly),
                    [2] = FakeEx(2, 5.0, 5.0, box: null),
                },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = ExIdentifyCore(10, 5.0, 5.0, At, (_, _, _) => null, (_, _) => null);
            return got is { Slot: null, Matched: 0 } ? null
                 : $"候補になった（slot={got?.Slot}, matched={got?.Matched}）——判定を持たない枠を拾っている";
        });

        yield return ("合成: 同じ座標に重なった 2 枠は★『決まった』に倒さず Narrowed（matched_slots）で返す", () =>
        {
            var box = FakeExBox();
            var atMap = new Dictionary<int, Dictionary<int, ExItem>>
            {
                [10] = new() { [1] = FakeEx(1, 5.0, 5.0, box), [2] = FakeEx(2, 5.0, 5.0, box) },
            };
            Dictionary<int, ExItem> At(int j) => atMap.TryGetValue(j, out var d) ? d : [];
            var got = ExIdentifyCore(10, 5.0, 5.0, At, (_, _, _) => null, (_, _) => null);
            if (got is null) return "一致するのに null";
            if (got.Slot is not null) return $"1 枠に確定した（{got.Slot}）——原理的に見分けられないはず";
            if (!got.MatchedSlots.OrderBy(x => x).SequenceEqual(new[] { 1, 2 }))
                return $"一致した枠 [{string.Join(",", got.MatchedSlots)}]";
            return got.How == HowExHit ? null : $"根拠 {got.How}（固定のはず）";
        });

        yield return ("母数: Ex の 4 つの根拠すべてが表から引ける（★鍵のまま返っていない）", () =>
        {
            foreach (var key in new[] { HowExHit, HowExHitNextTick, HowExHitNextTickExtrapolated, HowExHitTrail })
                if (ExHowJa(key) == key) return $"鍵 {key} の文が Ex の表から引けない";
            return ExHowJa("no_such_how") == "no_such_how" ? null : "表に無い鍵を鍵のまま返していない";
        });


        yield return ("合成: Resolve は Ex を敵より先に見る（1 枠に決まれば Resolved）", () =>
        {
            var src = ContactHit(enemyX: 0.0, enemyY: 0.0, hitX: 999.0, hitY: 999.0);
            var exId = new ExIdentity(777, HowExHit, 1, [777], 5, "hato");
            var got = ResolveCore(src, null, null, 2.0, DefaultMargin, DefaultSpan, () => exId);
            if (got is null) return "事象があるのに null";
            if (got.Confidence != HitConfidence.Resolved) return $"確度 {got.Confidence}";
            if (got.ResolvedSlot != 777) return $"確定した枠 {got.ResolvedSlot}（Ex の答えを使っていない）";
            return got.ExPathChecked ? null : "Ex を見たのに ExPathChecked が偽";
        });

        yield return ("合成: 両方決まるなら★Ex が先（Python の順そのまま。敵を先に見ていない）", () =>
        {
            var src = ContactHit(enemyX: 30.0, enemyY: 40.0, hitX: 30.0, hitY: 40.0);
            var exId = new ExIdentity(777, HowExHit, 1, [777], null, null);
            var got = ResolveCore(src, null, null, 2.0, DefaultMargin, DefaultSpan, () => exId);
            if (got is null) return "事象があるのに null";
            return got.ResolvedSlot == 777 ? null : $"確定した枠 {got.ResolvedSlot}（敵を先に見ている）";
        });

        yield return ("合成: Ex が絞れただけなら Narrowed を返し★HowFromEx を立てる", () =>
        {
            var src = ContactHit(enemyX: 0.0, enemyY: 0.0, hitX: 999.0, hitY: 999.0);
            var exId = new ExIdentity(null, HowExHit, 2, [11, 12], null, null);
            var got = ResolveCore(src, null, null, 2.0, DefaultMargin, DefaultSpan, () => exId);
            if (got is null) return "事象があるのに null";
            if (got.Confidence != HitConfidence.Narrowed) return $"確度 {got.Confidence}";
            if (!got.Slots.SequenceEqual(new[] { 11, 12 })) return $"候補 [{string.Join(",", got.Slots)}]";
            return got.HowFromEx ? null : "Ex 由来なのに HowFromEx が立っていない（表の選択を誤る）";
        });

        yield return ("合成: Ex が 0 件なら敵の確定へ落ちる（Ex → 敵の順で、敵も試す）", () =>
        {
            var src = ContactHit(enemyX: 30.0, enemyY: 40.0, hitX: 30.0, hitY: 40.0);
            var got = ResolveCore(src, null, null, 2.0, DefaultMargin, DefaultSpan, () => null);
            if (got is null) return "事象があるのに null";
            if (got.Confidence != HitConfidence.Resolved) return $"確度 {got.Confidence}";
            if (got.ResolvedSlot != ContactEnemySlot) return $"確定した枠 {got.ResolvedSlot}";
            return got.ExPathChecked ? null : "Ex を見た（0 件）のに ExPathChecked が偽";
        });

        yield return ("合成: 敵で確定したときは★HowFromEx を立てない（表の選択を誤らせない）", () =>
        {
            var src = ContactHit(enemyX: 30.0, enemyY: 40.0, hitX: 30.0, hitY: 40.0);
            var got = ResolveCore(src, null, null, 2.0, DefaultMargin, DefaultSpan, () => null);
            if (got is null) return "事象があるのに null";
            return !got.HowFromEx ? null : "敵の確定なのに HowFromEx が立った";
        });

        yield return ("合成: 種別が読めない窓（詰みクイックなど）は unknown_type", () =>
        {
            var src = new FakeSource { TickCount = 8 };
            src.Ev.Add(new WindowEvent(3, 3, 1, "quick"));
            var got = Resolve(src, playerSide: 2.0);
            if (got is null) return "事象があるのに null";
            if (got.Type is not null) return $"種別 {got.Type} が付いた";
            return got.WhyKey == WhyUnknownType ? null : $"理由 {got.WhyKey}";
        });

        yield return ("合成: 事象が 1 つも無い窓は null（★『候補 0 件』にしない）", () =>
            Resolve(new FakeSource { TickCount = 8 }, playerSide: 2.0) is null
                ? null : "事象が無いのに答えが返った");

        yield return ("合成: 被弾の位置は hit_kind の VALID が立つ tick（raw+1）から採る", () =>
        {
            var src = new FakeSource { TickCount = 8 };
            src.Ev.Add(new WindowEvent(4, 5, 1, Window.TriggerHitKind));
            src.Main(HitXName(1), 5, 11.0);
            src.Main(HitYName(1), 5, 12.0);
            src.Main(HitKindName(1), 6, HitValid | ((uint)HitTypeKind.Bullet << HitTypeShift));
            src.Main(HitXName(1), 6, 21.0);
            src.Main(HitYName(1), 6, 22.0);
            var got = HitPointOf(src, side: 1);
            if (got is null) return "出せるのに null";
            return got.Value.At == 6 && got.Value.X == 21.0 && got.Value.Y == 22.0
                ? null : $"raw の座標を拾った（at={got.Value.At} x={got.Value.X}）";
        });

        yield return ("合成: クイックの起点・種別不明・レーザーでは位置を出さない", () =>
        {
            var quick = new FakeSource { TickCount = 8 };
            quick.Ev.Add(new WindowEvent(3, 3, 1, "quick"));
            quick.Main(HitXName(1), 3, 1.0);
            quick.Main(HitYName(1), 3, 1.0);
            if (HitPointOf(quick, side: 1) is not null) return "クイックの起点で出した";
            var unknown = new FakeSource { TickCount = 8 };
            unknown.Ev.Add(new WindowEvent(3, 3, 1, Window.TriggerHitKind));
            unknown.Main(HitXName(1), 3, 1.0);
            unknown.Main(HitYName(1), 3, 1.0);
            if (HitPointOf(unknown, side: 1) is not null) return "種別が読めないのに出した";
            var laser = new FakeSource { TickCount = 8 };
            laser.Ev.Add(new WindowEvent(3, 3, 1, Window.TriggerHitKind));
            laser.Main(HitKindName(1), 3, HitValid | ((uint)HitTypeKind.Laser << HitTypeShift));
            laser.Main(HitXName(1), 3, 1.0);
            laser.Main(HitYName(1), 3, 1.0);
            if (HitPointOf(laser, side: 1) is not null) return "レーザーで出した（位置として信用できない）";
            static bool Legacy(FakeSource s) =>
                s.MainAt(HitXName(s.Ev[0].Side), s.Ev[0].Raw) is not null
                && s.MainAt(HitYName(s.Ev[0].Side), s.Ev[0].Raw) is not null;
            return Legacy(quick) && Legacy(unknown) && Legacy(laser)
                ? null : "旧の写しが 3 つとも『出す』になっていない ——否定テストが効いていない";
        });

        yield return ("★否定: 旧（＝hit_* の 1 tick 遅れを見ない）なら、1 フレームずれた盤面から拾う", () =>
        {
            var src = new FakeSource { TickCount = 8 };
            var e = new WindowEvent(4, 5, 1, Window.TriggerHitKind);
            src.Ev.Add(e);
            foreach (var t in new[] { 3, 4, 5 })
            {
                src.Main(PosXName(1), t, 0.0);
                src.Main(PosYName(1), t, 0.0);
            }
            src.Boards[(4, 1)] = OneEnemy(ContactEnemySlot, 0.0, 0.0);
            src.Boards[(5, 1)] = OneEnemy(ContactEnemySlot + 1, 0.0, 0.0);
            var now = EnemyCandidatesOf(src, e, 1, 2.0, span: 0);
            var legacy = LegacyRawTickCandidates(src, e, 1, 2.0, span: 0);
            if (legacy.Count != 1 || legacy[0] != ContactEnemySlot + 1)
                return $"旧の写しが raw の盤面を拾っていない（[{string.Join(",", legacy)}]）"
                       + " ——否定テストが効いていない";
            if (now.Slots.Count != 1) return $"新の候補 {now.Slots.Count} 件（1 件のはず）";
            return now.Slots[0] == ContactEnemySlot
                ? null : $"新が枠 {now.Slots[0]} を拾った（{ContactEnemySlot} のはず）";
        });

        yield return ("合成: 妖精の幅で答えが変わるときは★ranged と sure を分けて返す", () =>
        {
            var box = EnemySize.Half(AnalysisTables.EnemyClassFairy);
            if (box is null || box.None) return "妖精の判定が引けない（母数が足りない）";
            if (!box.Ranged) return "妖精の判定に幅が無い（この検査の前提が崩れている）";
            var src = new FakeSource { TickCount = 4 };
            var e = new WindowEvent(1, 1, 1, "life_raw");
            src.Ev.Add(e);
            src.Main(PosXName(1), 1, 0.0);
            src.Main(PosYName(1), 1, 0.0);
            double x = (box.HalfW + box.HalfWMax) * 0.5;
            src.Boards[(1, 1)] = OneEnemy(ContactEnemySlot, x, 0.0);
            var got = EnemyCandidatesOf(src, e, 1, 0.0, span: 0);
            if (got.Slots.Count != 1) return $"最大でも候補が {got.Slots.Count} 件";
            if (got.SureSlots.Count != 0) return "最小でも当たったことになっている";
            return got.Ranged ? null : "ranged が立っていない（幅で答えが変わっているのに）";
        });

        yield return ("合成: 軌跡は★その tick に居たものの区間だけを繋ぐ（他は null）", () =>
        {
            var got = TrailCore(6, 5, at: 4, TwoSegments, TickAsPoint);
            for (int i = 0; i <= 2; i++) if (got[i] is not null) return $"tick {i} の別のものが繋がった";
            for (int i = 3; i <= 5; i++) if (got[i] is null) return $"tick {i} が落ちた";
            return got[4]?.X == 4.0 ? null : $"tick 4 の位置 {got[4]?.X}";
        });

        yield return ("★否定: 旧（＝at を見ずに全部繋ぐ）なら、別のものの位置が 1 本に入る", () =>
        {
            var legacy = TrailCore(6, 5, at: null, TwoSegments, TickAsPoint);
            if (legacy[0] is null || legacy[1] is null)
                return "旧の写しが 1 つ目の区間を描いていない ——否定テストが効いていない";
            if (legacy[3] is not null) return "地続きの継ぎ目で線が切れていない";
            var now = TrailCore(6, 5, at: 4, TwoSegments, TickAsPoint);
            return now[0] is null && now[1] is null ? null : "新でも別のものが繋がった";
        });

        yield return ("合成: at に何も居なければ全区間を返す（★黙って空にしない）", () =>
        {
            var got = TrailCore(6, 5, at: 9, TwoSegments, TickAsPoint);
            int n = got.Count(x => x is not null);
            return n > 0 ? null : "居ない tick を渡したら軌跡が空になった";
        });

        yield return ("合成: 軌跡は窓の外へはみ出さない／長さ 0 の窓でも落ちない", () =>
        {
            if (TrailCore(6, 99, null, TwoSegments, TickAsPoint).Length != 6) return "窓の外まで伸びた";
            return TrailCore(0, 5, null, TwoSegments, TickAsPoint).Length == 0 ? null : "長さ 0 の窓で点が出た";
        });

        yield return ("合成: ★『居なかった』と『居たが座標が読めない』を混ぜない", () =>
        {
            var got = TrailCore(6, 5, at: 4, TwoSegments, _ => new TrailPoint(null, null));
            if (got[0] is not null) return "居なかった tick が点になった";
            if (got[4] is null) return "居た tick が『居なかった』になった";
            return got[4]!.Value.X is null ? null : "読めなかった座標が値を持った";
        });

        yield return ("★母数: 敵の候補は『判定が無い』枠を拾わない（★知らないとは別）", () =>
        {
            var src = new FakeSource { TickCount = 4 };
            var e = new WindowEvent(1, 1, 1, "life_raw");
            src.Ev.Add(e);
            src.Main(PosXName(1), 1, 0.0);
            src.Main(PosYName(1), 1, 0.0);
            var board = new Board();
            string[] all = [AnalysisTables.EnemyClassFairy, AnalysisTables.EnemyClassGhost,
                            AnalysisTables.EnemyClassLily, AnalysisTables.EnemyClassBoss,
                            AnalysisTables.EnemyClassC2C3, AnalysisTables.EnemyClassOther];
            var none = all.FirstOrDefault(c => EnemySize.Half(c)?.None == true);
            if (none is null) return "判定を持たないと確定している分類が 1 つも無い（母数が足りない）";
            board.Enemies.Add(FakeEnemy(ContactEnemySlot, 0.0, 0.0, none));
            board.Enemies.Add(FakeEnemy(ContactEnemySlot + 1, 0.0, 0.0, null));
            src.Boards[(1, 1)] = board;
            var got = EnemyCandidatesOf(src, e, 1, 2.0, span: 0);
            return got.Slots.Count == 0 ? null
                : $"判定なし／分類不明を拾った（[{string.Join(",", got.Slots)}]）";
        });


        yield return ("合成: FindHitKindTick は hit_kind の VALID が立つ記録を見つける（起点の 1 tick 後）", () =>
        {
            var src = new FakeSource { TickCount = 8 };
            src.Main(HitKindName(1), 6, HitValid | ((uint)HitTypeKind.Bullet << HitTypeShift));
            return FindHitKindTick(src, 1, 5) == 6 ? null : "見つからない／位置が違う";
        });
        yield return ("★否定: VALID が 1 つも無ければ FindHitKindTick は null", () =>
        {
            var src = new FakeSource { TickCount = 8 };
            src.Main(HitKindName(1), 6, (uint)HitTypeKind.Bullet << HitTypeShift);
            return FindHitKindTick(src, 1, 5) is null ? null : "VALID 無しで見つかった";
        });

        yield return ("合成: FindQuickTick は player_state が 0→3 に変わる記録を見つける", () =>
        {
            var src = new FakeSource { TickCount = 8 };
            src.Main(TickWords.Record.P1PlayerState, 3, 0u);
            src.Main(TickWords.Record.P1PlayerState, 4, 3u);
            return FindQuickTick(src, 1, 5) == 4 ? null : "見つからない／位置が違う";
        });
        yield return ("★否定: 0→3 の遷移が無ければ FindQuickTick は null（3 のまま／0 のままは拾わない）", () =>
        {
            var src = new FakeSource { TickCount = 8 };
            src.Main(TickWords.Record.P1PlayerState, 3, 3u);
            src.Main(TickWords.Record.P1PlayerState, 4, 3u);
            return FindQuickTick(src, 1, 5) is null ? null : "遷移が無いのに見つかった";
        });

        yield return ("合成: MatchOnBoard は軸平行矩形／円を中心の位置で盤面の候補に対応づける", () =>
        {
            var e = new HazardElement(HazardKind.Aabb, 10.0, 20.0, 3, 4, 0, 0, 0, 0);
            var cands = new[]
            {
                new HazardBoardCandidate(HazardMatchKind.Bullet, 5, 999.0, 999.0, null),
                new HazardBoardCandidate(HazardMatchKind.Enemy, 7, 10.0, 20.0, null),
            };
            var got = MatchOnBoard(e, cands);
            return got is { Kind: HazardMatchKind.Enemy, Slot: 7 } ? null : $"対応づけが違う（{got}）";
        });
        yield return ("合成: MatchOnBoard はレーザー（回転矩形）を pivot＋角度で対応づける", () =>
        {
            var e = new HazardElement(HazardKind.Obb, 0, 10, 2, 5, 0, 1.0, 3.0, 4.0);
            var cands = new[]
            {
                new HazardBoardCandidate(HazardMatchKind.Enemy, 1, 3.0, 4.0, 1.0),
                new HazardBoardCandidate(HazardMatchKind.Laser, 9, 3.0, 4.0, 1.0),
            };
            var got = MatchOnBoard(e, cands);
            return got is { Kind: HazardMatchKind.Laser, Slot: 9 } ? null : $"対応づけが違う（{got}）";
        });
        yield return ("★否定: 位置が離れていれば対応づけない（嘘の対応づけを返さない）", () =>
            MatchOnBoard(new HazardElement(HazardKind.Aabb, 10, 20, 1, 1, 0, 0, 0, 0),
                         [new HazardBoardCandidate(HazardMatchKind.Bullet, 5, 999.0, 999.0, null)])
                is null ? null : "離れているのに対応づいた");

        yield return ("合成: HazardListCulprit（被弾）はゲームと同じ記録から要素と重なりを読む", () =>
        {
            var w = HazardCulpritWindow(
                tickCount: 8, side: 1, charId: 0 , bas: 5,
                trigger: Window.TriggerHitKind, triggerRaw: 6, posX: 0.0, posY: 0.0,
                elems: new Dictionary<int, HazardElement>
                {
                    [0] = new HazardElement(HazardKind.Aabb, 0.0, 0.0, 1.0, 1.0, 0, 0, 0, 0),
                });
            var got = HazardListCulprit(w, 1);
            if (got is null) return "答えが出ない";
            if (got.TickIndex != 5) return $"記録の位置 {got.TickIndex}（5 のはず）";
            if (got.ListIndex != 0) return $"リストの添字 {got.ListIndex}（0 のはず）";
            return got.OverlapCount == 1 ? null : $"重なりの総数 {got.OverlapCount}（1 のはず）";
        });
        yield return ("合成: HazardCulprit.All は重なった全部を持ち、先頭は他の 4 フィールドと一致する", () =>
        {
            var w = HazardCulpritWindow(
                tickCount: 8, side: 1, charId: 0 , bas: 5,
                trigger: Window.TriggerHitKind, triggerRaw: 6, posX: 0.0, posY: 0.0,
                elems: new Dictionary<int, HazardElement>
                {
                    [0] = new HazardElement(HazardKind.Aabb, 0.0, 0.0, 1.0, 1.0, 0, 0, 0, 0),
                    [1] = new HazardElement(HazardKind.Aabb, 1.5, 0.0, 1.0, 1.0, 0, 0, 0, 0),
                },
                bullet0: (State: 1u, X: 1.5, Y: 0.0));
            var got = HazardListCulprit(w, 1);
            if (got is null) return "答えが出ない";
            if (got.OverlapCount != 2) return $"重なりの総数 {got.OverlapCount}（2 のはず）";
            if (got.All.Count != 2) return $"All の件数 {got.All.Count}（2 のはず）";
            if (got.All[0].ListIndex != 0 || got.All[1].ListIndex != 1)
                return $"All の順が違う（{got.All[0].ListIndex}, {got.All[1].ListIndex}）";
            if (got.All[0].Match is not null) return "要素 0（弾から離れている）が対応づいてしまった";
            if (got.All[1].Match is not { Kind: HazardMatchKind.Bullet }) return "要素 1 が弾に対応づかない";
            if (got.ListIndex != got.All[0].ListIndex) return "ListIndex が All[0] と食い違う";
            if (!got.Element.Equals(got.All[0].Element)) return "Element が All[0] と食い違う";
            if (got.Match != got.All[0].Match) return "Match が All[0] と食い違う";
            return null;
        });
        yield return ("★否定: 重なりが 1 件だけなら All も 1 件（水増ししない）", () =>
        {
            var w = HazardCulpritWindow(
                tickCount: 8, side: 1, charId: 0, bas: 5,
                trigger: Window.TriggerHitKind, triggerRaw: 6, posX: 0.0, posY: 0.0,
                elems: new Dictionary<int, HazardElement>
                {
                    [0] = new HazardElement(HazardKind.Aabb, 0.0, 0.0, 1.0, 1.0, 0, 0, 0, 0),
                });
            var got = HazardListCulprit(w, 1);
            return got?.All.Count == 1 ? null : $"All の件数 {got?.All.Count}（1 のはず）";
        });
        yield return ("合成: HazardListCulprit（クイック）は player_state 0→3 の記録から読む", () =>
        {
            var w = HazardCulpritWindow(
                tickCount: 8, side: 1, charId: 0, bas: 4,
                trigger: "quick", triggerRaw: 5, posX: 1.0, posY: 1.0,
                elems: new Dictionary<int, HazardElement>
                {
                    [0] = new HazardElement(HazardKind.Circle, 1.0, 1.0, 0, 0, 5.0, 0, 0, 0),
                },
                quickStateAt: 4);
            var got = HazardListCulprit(w, 1);
            return got is { TickIndex: 4, ListIndex: 0, OverlapCount: 1 } ? null : $"答えが違う（{got}）";
        });
        yield return ("★否定: 重なりが 0 件なら HazardListCulprit は null（嘘の答えを出さない）", () =>
        {
            var w = HazardCulpritWindow(
                tickCount: 8, side: 1, charId: 0, bas: 5,
                trigger: Window.TriggerHitKind, triggerRaw: 6, posX: 0.0, posY: 0.0,
                elems: new Dictionary<int, HazardElement>
                {
                    [0] = new HazardElement(HazardKind.Aabb, 500.0, 500.0, 1.0, 1.0, 0, 0, 0, 0),
                });
            return HazardListCulprit(w, 1) is null ? null : "重ならないのに答えが出た";
        });

        yield return ("合成: 消滅演出中（state 5 系）の弾でも盤面の物に対応づく", () =>
        {
            var w = HazardCulpritWindow(
                tickCount: 8, side: 1, charId: 0, bas: 5,
                trigger: Window.TriggerHitKind, triggerRaw: 6, posX: 30.0, posY: 40.0,
                elems: new Dictionary<int, HazardElement>
                {
                    [0] = new HazardElement(HazardKind.Aabb, 30.0, 40.0, 1.0, 1.0, 0, 0, 0, 0),
                },
                bullet0: (State: AnalysisTables.CoordBulletStateVanish, X: 30.0, Y: 40.0));
            var got = HazardListCulprit(w, 1);
            int wantSlot = AnalysisTables.CoordBaseP1Bullet;
            return got?.Match is { Kind: HazardMatchKind.Bullet, Slot: int s } && s == wantSlot
                ? null : $"対応づけが違う（{got?.Match}）";
        });

        yield return ("★否定: Window.BoardAt（消滅演出中を返さない）だけで探すと、この弾を見落とす", () =>
        {
            var w = HazardCulpritWindow(
                tickCount: 8, side: 1, charId: 0, bas: 5,
                trigger: Window.TriggerHitKind, triggerRaw: 6, posX: 0.0, posY: 0.0,
                elems: new Dictionary<int, HazardElement>
                {
                    [0] = new HazardElement(HazardKind.Aabb, 30.0, 40.0, 1.0, 1.0, 0, 0, 0, 0),
                },
                bullet0: (State: AnalysisTables.CoordBulletStateVanish, X: 30.0, Y: 40.0));
            var src = WindowHits.For(w);
            bool legacyFound = src.BoardAt(5, 1).Bullets.Any(b => b.Slot == AnalysisTables.CoordBaseP1Bullet);
            return !legacyFound ? null : "合成が狂っている（旧でも見えてしまう＝消滅演出中になっていない）";
        });
    }

    private static Window HazardCulpritWindow(
        int tickCount, int side, int charId, int bas, string trigger, int triggerRaw,
        double posX, double posY, IReadOnlyDictionary<int, HazardElement> elems, int? quickStateAt = null,
        (uint State, double X, double Y)? bullet0 = null)
    {
        var meta = new WindowMeta
        {
            SessionId = 1, WindowNo = 1, FirstSeq = 1000, FirstFrame = null,
            TickCount = tickCount, SlotCount = 0, Quant = "synth",
            Hits = [new HitEvent(1000 + triggerRaw, side, trigger)], LostTicks = 0,
        };
        var main = new Dictionary<string, TickValue[]>(StringComparer.Ordinal);
        TickValue[] Missing() { var c = new TickValue[tickCount]; Array.Fill(c, TickValue.Missing); return c; }
        var chCol = Missing(); chCol[0] = TickValue.Int(charId);
        main[side == 1 ? "p1_character" : "p2_character"] = chCol;
        var posXCol = Missing(); posXCol[bas] = TickValue.Float(posX);
        main[side == 1 ? "p1_pos_x" : "p2_pos_x"] = posXCol;
        var posYCol = Missing(); posYCol[bas] = TickValue.Float(posY);
        main[side == 1 ? "p1_pos_y" : "p2_pos_y"] = posYCol;
        if (trigger == Window.TriggerHitKind)
        {
            var hkCol = Missing();
            hkCol[bas] = TickValue.Int(HitValid | ((uint)HitTypeKind.Bullet << HitTypeShift));
            main[side == 1 ? "p1_hit_kind" : "p2_hit_kind"] = hkCol;
        }
        if (quickStateAt is int qa)
        {
            var stCol = Missing();
            stCol[qa - 1] = TickValue.Int(StateNormal);
            stCol[qa] = TickValue.Int(StateInvincible);
            main[side == 1 ? "p1_player_state" : "p2_player_state"] = stCol;
        }
        var hlCol = Missing();
        hlCol[bas] = TickValue.Int(AnalysisTables.HitlistValid | (uint)elems.Count);
        main[side == 1 ? "p1_hit_list_count" : "p2_hit_list_count"] = hlCol;

        var raw = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        void Put(int i, string suffix, double v)
        {
            var arr = new uint[tickCount];
            arr[bas] = BitConverter.SingleToUInt32Bits((float)v);
            raw["p" + side + "_h" + i + suffix] = arr;
        }
        foreach (var (i, e) in elems)
        {
            Put(i, "_x", e.X); Put(i, "_y", e.Y);
            Put(i, "_half_x", e.HalfX); Put(i, "_half_y", e.HalfY);
            Put(i, "_radius", e.Radius); Put(i, "_angle", e.Angle);
            Put(i, "_pivot_x", e.PivotX); Put(i, "_pivot_y", e.PivotY);
        }
        if (bullet0 is { } b0)
        {
            int slot0 = side == 1 ? AnalysisTables.CoordBaseP1Bullet : AnalysisTables.CoordBaseP2Bullet;
            var stArr = new uint[tickCount]; stArr[bas] = b0.State;
            raw[CoordRing.ColState(slot0)] = stArr;
            var bxArr = new uint[tickCount]; bxArr[bas] = BitConverter.SingleToUInt32Bits((float)b0.X);
            raw[CoordRing.ColX(slot0)] = bxArr;
            var byArr = new uint[tickCount]; byArr[bas] = BitConverter.SingleToUInt32Bits((float)b0.Y);
            raw[CoordRing.ColY(slot0)] = byArr;
        }
        return new Window(meta, new WindowColumns(raw), new MainColumns(main), MainColumns.Empty);
    }


    private static int ContactEnemySlot => AnalysisTables.CoordBaseP1Enemy + 2;

    private static bool LegacyEllipseCovers(double px, double py, double cx, double cy,
                                            double halfW, double halfH, double playerSide)
    {
        double rw = halfW + playerSide * 0.5, rh = halfH + playerSide * 0.5;
        if (rw <= 0.0 || rh <= 0.0) return false;
        double dx = (px - cx) / rw, dy = (py - cy) / rh;
        return dx * dx + dy * dy <= 1.0;
    }

    private static List<int> LegacyRawTickCandidates(IHitSource src, WindowEvent e, int side,
                                                     double playerSide, int span)
    {
        var found = new List<int>();
        for (int k = e.Raw - span; k <= e.Raw + span; k++)
        {
            if (k < 0 || k >= src.TickCount) continue;
            var px = src.MainAt(PosXName(side), k);
            var py = src.MainAt(PosYName(side), k);
            if (px is null || py is null) continue;
            foreach (var o in src.BoardAt(k, side).Enemies)
            {
                var box = EnemySize.Half(o.EnemyClass);
                if (box is null || box.None || o.X is null || o.Y is null) continue;
                if (CoversRect(px.Value, py.Value, o.X.Value, o.Y.Value,
                               box.HalfWMax, box.HalfHMax, playerSide) && !found.Contains(o.Slot))
                    found.Add(o.Slot);
            }
        }
        found.Sort();
        return found;
    }

    private static (int Start, int End)? TwoSegments(int i) =>
        i is >= 0 and <= 2 ? (0, 2) : i is >= 3 and <= 5 ? (3, 5) : null;

    private static TrailPoint TickAsPoint(int i) => new(i, i);

    private static BoardItem FakeLaser(int slot, double x, double y, double angle,
                                       double tail, double head, double width, bool lethal) =>
        new()
        {
            Slot = slot,
            X = x,
            Y = y,
            Kind = 0u,
            State = 1u,
            Laser = new LaserInfo(angle, tail, head, width, 0u, 0u, lethal),
        };

    private static BoardItem FakeEnemy(int slot, double x, double y, string? cls) =>
        new() { Slot = slot, X = x, Y = y, Kind = 0u, State = 1u, EnemyClass = cls };

    private static Board OneEnemy(int slot, double x, double y)
    {
        var board = new Board();
        board.Enemies.Add(FakeEnemy(slot, x, y, AnalysisTables.EnemyClassFairy));
        return board;
    }

    private static FakeSource BulletHit(uint mgr, int n)
    {
        var src = new FakeSource { TickCount = 8 };
        src.Ev.Add(new WindowEvent(4, 5, 1, Window.TriggerHitKind));
        src.Main(HitKindName(1), 5, HitValid | ((uint)HitTypeKind.Bullet << HitTypeShift));
        src.Main(BulletMgrName(1), 5, mgr);
        src.Main(HitObjPtrName(1), 5, (uint)(mgr + BulletArrayBase + n * BulletStride));
        return src;
    }

    private static FakeSource ContactHit(double enemyX, double enemyY, double hitX, double hitY)
    {
        var src = new FakeSource { TickCount = 8 };
        src.Ev.Add(new WindowEvent(4, 5, 1, Window.TriggerHitKind));
        src.Main(HitKindName(1), 5, HitValid | ((uint)HitTypeKind.Contact << HitTypeShift));
        src.Main(HitXName(1), 5, hitX);
        src.Main(HitYName(1), 5, hitY);
        foreach (var t in new[] { 3, 4, 5 })
        {
            src.Main(PosXName(1), t, 0.0);
            src.Main(PosYName(1), t, 0.0);
            src.Boards[(t, 1)] = OneEnemy(ContactEnemySlot, enemyX, enemyY);
        }
        return src;
    }

    private static ExHitbox FakeExBox(string? center = null, IReadOnlyList<int>? trail = null,
                                      bool effectOnly = false) =>
        new() { Kind = "circle", R = 8.0, Center = center, Trail = trail, EffectOnly = effectOnly };

    private static ExItem FakeEx(int slot, double x, double y, ExHitbox? box,
                                 int? exType = null, string? name = null) =>
        new()
        {
            Slot = slot, X = x, Y = y, ExSide = 0, HasHitbox = box is not null, Hitbox = box,
            ExType = exType, Name = name, FromBoss = null,
        };

    private sealed class FakeSource : IHitSource
    {
        public int TickCount { get; init; }
        public int SlotCount { get; init; } = CoordRing.SlotCount;
        public List<WindowEvent> Ev { get; } = [];
        public IReadOnlyList<WindowEvent> Events => Ev;
        public Dictionary<(string Name, int Tick), double> Mains { get; } = new();
        public Dictionary<(int Tick, int Side), Board> Boards { get; } = new();

        public void Main(string name, int tick, double v) => Mains[(name, tick)] = v;

        public double? MainAt(string name, int i) => Mains.TryGetValue((name, i), out var v) ? v : null;

        public Board BoardAt(int i, int side) => Boards.TryGetValue((i, side), out var b) ? b : new Board();

        public int? PrimaryEventIndex(int? side)
        {
            if (Ev.Count == 0) return null;
            var idx = Enumerable.Range(0, Ev.Count).Where(k => side is null || Ev[k].Side == side.Value).ToList();
            if (idx.Count == 0) idx = [.. Enumerable.Range(0, Ev.Count)];
            var real = idx.Where(k => !Window.NonHitTriggers.Contains(Ev[k].Trigger, StringComparer.Ordinal)).ToList();
            return (real.Count > 0 ? real : idx)[0];
        }
    }
}

public readonly record struct HitPointAt(double X, double Y, int At);

public interface IHitSource
{
    int TickCount { get; }
    int SlotCount { get; }
    IReadOnlyList<WindowEvent> Events { get; }
    double? MainAt(string name, int i);
    Board BoardAt(int i, int side);
    int? PrimaryEventIndex(int? side);
}

public sealed class WindowHits : IHitSource
{
    private readonly Window _w;
    private readonly object _gate = new();
    private List<WindowEvent>? _events;

    private WindowHits(Window w) => _w = w;

    private static readonly ConditionalWeakTable<Window, WindowHits> Cached = new();

    public static WindowHits For(Window w) => Cached.GetValue(w, static x => new WindowHits(x));

    public int TickCount => _w.TickCount;
    public int SlotCount => _w.Meta.SlotCount;

    public IReadOnlyList<WindowEvent> Events
    {
        get { lock (_gate) { return _events ??= _w.Events(); } }
    }

    public double? MainAt(string name, int i) => _w.MainAt(name, i);
    public Board BoardAt(int i, int side) => _w.BoardAt(i, side);
    public int? PrimaryEventIndex(int? side) => _w.PrimaryEventIndex(side);
}

public readonly record struct LaserSeg(double X1, double Y1, double X2, double Y2, double Half);

public readonly record struct LaserCandidate(int Slot, double Distance, double HalfWidth);

public sealed record LaserIdentity(int? Slot, LaserHow How, int Fan,
                                   IReadOnlyList<int> FanSlots, double? Distance);

public sealed record EnemyIdentity(int? Slot, int Matched, IReadOnlyList<int> MatchedSlots, string? EnemyClass);

public sealed record ExIdentity(int? Slot, string How, int Matched, IReadOnlyList<int> MatchedSlots,
                                int? ExType, string? Name);

public readonly record struct EnemyCandidateSet(IReadOnlyList<int> Slots, IReadOnlyList<int> SureSlots, bool Ranged);

public readonly record struct TrailPoint(double? X, double? Y);

public sealed class HitCandidateSet
{
    public required HitTypeKind? Type { get; init; }
    public required int Side { get; init; }
    public required int EventIndex { get; init; }
    public required HitConfidence Confidence { get; init; }
    public required int? ResolvedSlot { get; init; }
    public required IReadOnlyList<int> Slots { get; init; }
    public required IReadOnlyList<int> SureSlots { get; init; }
    public required bool SizeRanged { get; init; }
    public required string HowKey { get; init; }
    public required bool HowFromEx { get; init; }
    public required string? WhyKey { get; init; }
    public required bool ExPathChecked { get; init; }
}
