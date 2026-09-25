using System.Globalization;
using System.Runtime.CompilerServices;

namespace TH09.Analysis;

public sealed class ExHitbox
{
    public required string Kind { get; init; }
    public double? R { get; init; }
    public double? W { get; init; }
    public double? H { get; init; }
    public string? Formula { get; init; }
    public int? GateLo { get; init; }
    public int? GateHi { get; init; }
    public bool EffectOnly { get; init; }
    public bool Still { get; init; }
    public bool NotMovingX { get; init; }
    public string? Center { get; init; }
    public IReadOnlyList<int>? Trail { get; init; }
    public IReadOnlyDictionary<int, double>? RByVariant { get; init; }
}

public sealed class ExKindParts
{
    public required uint Raw { get; init; }
    public int? ExType { get; init; }
    public string? ExName { get; init; }
    public ExHitbox? Hitbox { get; init; }
    public double? Radius { get; init; }
    public int? Variant { get; init; }
}

public sealed class ExShape
{
    public IReadOnlyList<(double X, double Y, double R)>? Circles { get; init; }
    public (double X, double Y, double W, double H)? Rect { get; init; }
}

public sealed class ExItem
{
    public required int Slot { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public double? Radius { get; init; }
    public string? Name { get; init; }
    public int? ExType { get; init; }
    public uint? Timer { get; init; }
    public required int ExSide { get; init; }
    public int? Variant { get; init; }
    public required bool HasHitbox { get; init; }
    public ExHitbox? Hitbox { get; init; }
    public ExShape? Shape { get; init; }
    public bool IsCross => Shape?.Circles is not { Count: > 0 } && Shape?.Rect is null;
    public required bool? FromBoss { get; init; }
}

public static class ExItems
{

    private static readonly Dictionary<uint, (int ExType, string? Name)> UpdateFp = BuildUpdateFp();
    private static readonly Dictionary<uint, ExHitbox?> Hitboxes = BuildHitboxes();
    private static readonly Dictionary<string, string> NamesJa = BuildPairs(AnalysisTables.ExNameJaPacked);
    private static readonly Dictionary<string, string> Colors = BuildPairs(AnalysisTables.ExColorsPacked);
    private static readonly Dictionary<string, string> Notes = BuildPairs(AnalysisTables.ExNotePacked);
    private static readonly Dictionary<int, int> HitboxArmTable = BuildHitboxArm();
    private static readonly Dictionary<long, double> ReisenRadiusTable = BuildReisenRadius();
    private static readonly HashSet<int> ParentTypes = BuildParentTypes();

    private static readonly Dictionary<int, (double X, double Y)> ScreenOrigin = BuildScreenOrigin();

    private static readonly HashSet<int> ScreenXyExTypes = BuildTypeSet(AnalysisTables.ScreenXyExTypesPacked);

    private static readonly HashSet<int> BoardXyExTypes = BuildTypeSet(AnalysisTables.BoardXyExTypesPacked);

    private static readonly Dictionary<int, int> ExFlightFrames = BuildFlightFrames();

    private static readonly Dictionary<uint, int?> BossBirthTimer = BuildBossBirthTimer();

    private static readonly Dictionary<int, int> CardLevelTable = BuildCardLevel();

    private static readonly string[] ExSuffixes = Packed.Lines(AnalysisTables.ExSuffixesPacked);

    public static int TypeCount => UpdateFp.Count;
    public static int NameCount => NamesJa.Count;
    public static int CardLevelTypeCount => CardLevelTable.Count;

    private static Dictionary<uint, (int, string?)> BuildUpdateFp()
    {
        var map = new Dictionary<uint, (int, string?)>();
        foreach (var line in Packed.Lines(AnalysisTables.ExUpdateFpPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 3) throw new InvalidDataException("ExUpdateFpPacked の列数が 3 でない: " + line);
            map[Hex(f[0])] = ((int)Hex(f[1]), f[2].Length == 0 ? null : f[2]);
        }
        return map;
    }

    private static Dictionary<uint, ExHitbox?> BuildHitboxes()
    {
        var map = new Dictionary<uint, ExHitbox?>();
        foreach (var line in Packed.Lines(AnalysisTables.ExHitboxPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 12) throw new InvalidDataException("ExHitboxPacked の列数が 12 でない: " + line);
            var fp = Hex(f[0]);
            if (f[1].Length == 0) { map[fp] = null; continue; }
            var flags = f[8].Length == 0 ? [] : f[8].Split(',');
            map[fp] = new ExHitbox
            {
                Kind = f[1],
                R = Num(f[2]), W = Num(f[3]), H = Num(f[4]),
                Formula = Str(f[5]),
                GateLo = Int(f[6]), GateHi = Int(f[7]),
                EffectOnly = flags.Contains("effect_only"),
                Still = flags.Contains("still"),
                NotMovingX = flags.Contains("not_moving_x"),
                Center = Str(f[9]),
                Trail = Trail(f[10]),
                RByVariant = ByVariant(f[11]),
            };
        }
        return map;

        static IReadOnlyList<int>? Trail(string s) =>
            s.Length == 0 ? null
                          : s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                             .Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();

        static IReadOnlyDictionary<int, double>? ByVariant(string s)
        {
            if (s.Length == 0) return null;
            var d = new Dictionary<int, double>();
            foreach (var pair in s.Split(','))
            {
                var kv = pair.Split(':');
                if (kv.Length != 2) throw new InvalidDataException("r_by_variant の形が違う: " + s);
                d[int.Parse(kv[0], CultureInfo.InvariantCulture)] =
                    double.Parse(kv[1], CultureInfo.InvariantCulture);
            }
            return d;
        }
    }

    private static Dictionary<string, string> BuildPairs(string packed)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(packed))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException("2 列の表の列数が違う: " + line);
            map[f[0]] = f[1];
        }
        return map;
    }

    private static Dictionary<int, int> BuildHitboxArm()
    {
        var map = new Dictionary<int, int>();
        foreach (var line in Packed.Lines(AnalysisTables.ExHitboxArmPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException("ExHitboxArmPacked の列数が 2 でない: " + line);
            map[(int)Hex(f[0])] = int.Parse(f[1], CultureInfo.InvariantCulture);
        }
        return map;
    }

    private static Dictionary<int, int> BuildCardLevel()
    {
        var map = new Dictionary<int, int>();
        foreach (var line in Packed.Lines(AnalysisTables.ExCardLevelPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException("ExCardLevelPacked の列数が 2 でない: " + line);
            map[(int)Hex(f[0])] = int.Parse(f[1], CultureInfo.InvariantCulture);
        }
        return map;
    }

    private static Dictionary<long, double> BuildReisenRadius()
    {
        var map = new Dictionary<long, double>();
        foreach (var line in Packed.Lines(AnalysisTables.ExReisenRadiusPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException("ExReisenRadiusPacked の列数が 2 でない: " + line);
            map[long.Parse(f[0], CultureInfo.InvariantCulture)] =
                double.Parse(f[1], CultureInfo.InvariantCulture);
        }
        return map;
    }

    private static HashSet<int> BuildParentTypes() => BuildTypeSet(AnalysisTables.ExParentTypesPacked);

    private static HashSet<int> BuildTypeSet(string packed)
    {
        var set = new HashSet<int>();
        foreach (var line in Packed.Lines(packed)) set.Add((int)Hex(line));
        return set;
    }

    private static Dictionary<int, (double X, double Y)> BuildScreenOrigin()
    {
        var map = new Dictionary<int, (double X, double Y)>();
        foreach (var line in Packed.Lines(AnalysisTables.ScreenOriginPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 3) throw new InvalidDataException("ScreenOriginPacked の列数が 3 でない: " + line);
            map[int.Parse(f[0], CultureInfo.InvariantCulture)] =
                (double.Parse(f[1], CultureInfo.InvariantCulture),
                 double.Parse(f[2], CultureInfo.InvariantCulture));
        }
        return map;
    }

    private static Dictionary<int, int> BuildFlightFrames()
    {
        var map = new Dictionary<int, int>();
        foreach (var line in Packed.Lines(AnalysisTables.ExFlightFramesPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException("ExFlightFramesPacked の列数が 2 でない: " + line);
            map[(int)Hex(f[0])] = int.Parse(f[1], CultureInfo.InvariantCulture);
        }
        return map;
    }

    private static Dictionary<uint, int?> BuildBossBirthTimer()
    {
        var map = new Dictionary<uint, int?>();
        foreach (var line in Packed.Lines(AnalysisTables.ExBossBirthTimerPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException("ExBossBirthTimerPacked の列数が 2 でない: " + line);
            map[Hex(f[0])] = Int(f[1]);
        }
        return map;
    }

    private static uint Hex(string s) =>
        uint.Parse(s.StartsWith("0x", StringComparison.Ordinal) ? s[2..] : s,
                   NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static double? Num(string s) =>
        s.Length == 0 ? null : double.Parse(s, CultureInfo.InvariantCulture);

    private static int? Int(string s) =>
        s.Length == 0 ? null : int.Parse(s, CultureInfo.InvariantCulture);

    private static string? Str(string s) => s.Length == 0 ? null : s;


    public static ExKindParts KindParts(uint kind, uint? variantWord = null)
    {
        int? exType = null;
        string? name = null;
        if (UpdateFp.TryGetValue(kind, out var ent)) { exType = ent.ExType; name = ent.Name; }
        Hitboxes.TryGetValue(kind, out var box);
        int? variant = VariantOf(kind, variantWord);
        double? r = null;
        if (box is not null && box.Kind == "circle")
        {
            r = box.R;
            if (box.RByVariant is not null)
                r = variant is not null && box.RByVariant.TryGetValue(variant.Value, out var rv) ? rv : null;
        }
        return new ExKindParts
        {
            Raw = kind, ExType = exType, ExName = name,
            Hitbox = box, Radius = r, Variant = variant,
        };
    }

    public static int? VariantOf(uint kind, uint? variantWord)
    {
        if (variantWord is null) return null;
        if (!Hitboxes.TryGetValue(kind, out var box) || box?.RByVariant is null) return null;
        return (int)(variantWord.Value & AnalysisTables.ExVariantMask);
    }

    public static long? TimerInner(uint? exTimer) =>
        exTimer is null ? null : (long)exTimer.Value + AnalysisTables.ExTimerInnerDelta;

    public static int? HitboxArm(int? exType) =>
        exType is not null && HitboxArmTable.TryGetValue(exType.Value, out var v) ? v : null;

    public static double? ReisenRadius(long? t) =>
        t is not null && ReisenRadiusTable.TryGetValue(t.Value, out var r) ? r : null;

    public static string? NameJa(string? name) =>
        name is not null && NamesJa.TryGetValue(name, out var ja) ? ja : name;

    public static string? ColorOf(string? name) =>
        name is not null && Colors.TryGetValue(name, out var c) ? c : null;

    public static string? NoteOf(string? name) =>
        name is not null && Notes.TryGetValue(name, out var n) ? n : null;

    public static IReadOnlyList<(string Name, string Ja, string Note)> EffectNotes()
    {
        var outList = new List<(string, string, string)>();
        foreach (var name in Colors.Keys)
        {
            var note = NoteOf(name);
            if (note is null) continue;
            outList.Add((name, NameJa(name) ?? name, note));
        }
        return outList;
    }

    public static bool IsParentType(int? exType) => exType is not null && ParentTypes.Contains(exType.Value);

    public static int? CardLevelOf(int? exType) =>
        exType is not null && CardLevelTable.TryGetValue(exType.Value, out var lv) ? lv : null;

    public static bool? BornFromBoss(uint? kind, uint? birthTimer, double? bossType)
    {
        if (kind is null) return null;
        if (!UpdateFp.ContainsKey(kind.Value)) return null;
        if (!BossBirthTimer.TryGetValue(kind.Value, out var preset)) return false;
        if (preset is null) return null;
        if (birthTimer is null || bossType is null) return null;
        return birthTimer.Value == (uint)preset.Value
               && (int)bossType.Value == AnalysisTables.BossTypeBoss;
    }

    public static bool? ReisenBossGroup(IReadOnlyList<(uint X, uint Y)> groupRawXy)
    {
        int n = groupRawXy.Count;
        if (n == 1) return false;
        if (n == 3 && groupRawXy[0] == groupRawXy[1] && groupRawXy[1] == groupRawXy[2])
            return true;
        return null;
    }

    private static List<(uint X, uint Y)> ReisenBossSiblings(Window w, int side, int birthTick)
    {
        int want = 2 - side;
        var outList = new List<(uint, uint)>();
        foreach (var slot in Slots(w))
        {
            var kind = w.Raw(CoordRing.ColKind(slot), birthTick);
            if (kind is null || kind.Value != AnalysisTables.ExReisenUpdateFp) continue;
            var seg = SlotSegments.At(w, slot, birthTick);
            if (seg is null || seg.Value.Start != birthTick) continue;
            var sd = w.Raw(ColSide(slot), birthTick);
            if (sd is null || (int)sd.Value != want) continue;
            var x = w.Raw(CoordRing.ColX(slot), birthTick);
            var y = w.Raw(CoordRing.ColY(slot), birthTick);
            if (x is null || y is null) continue;
            outList.Add((x.Value, y.Value));
        }
        return outList;
    }

    public static bool? FromBoss(Window w, int slot, int i, int side)
    {
        var seg = SlotSegments.At(w, slot, i);
        if (seg is null || seg.Value.Start <= 0) return null;
        int head = seg.Value.Start;
        var cache = CacheOf(w);
        lock (cache)
        {
            var key = (slot, head, side);
            if (cache.Boss.TryGetValue(key, out var got)) return got;
            var kind = w.Raw(CoordRing.ColKind(slot), head);
            bool? made = kind is not null && kind.Value == AnalysisTables.ExReisenUpdateFp
                ? ReisenBossGroup(ReisenBossSiblings(w, side, head))
                : BornFromBoss(kind, w.Raw(ColTimer(slot), head), w.MainAt($"p{side}_boss_type", head));
            cache.Boss[key] = made;
            return made;
        }
    }


    public static bool HasExSlots(Window w) => CoordRing.SlotCountHasEx(w.Meta.SlotCount);

    private const int SuffixTimerIdx = 0, SuffixSideIdx = 1, SuffixVariantIdx = 2;

    public static string ColTimer(int slot) => ExCol(slot, SuffixTimerIdx);
    public static string ColSide(int slot) => ExCol(slot, SuffixSideIdx);
    public static string ColVariant(int slot) => ExCol(slot, SuffixVariantIdx);

    private static string ExCol(int slot, int which) => ExBase(slot) + ExSuffixes[which];

    private static string ExBase(int slot) =>
        CoordRing.IsExSlot(slot) ? CoordRing.BaseName(slot)
                                 : throw new ArgumentException($"slot {slot} は Ex 枠ではありません");

    public static IReadOnlyList<int> Slots(Window w)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            if (cache.Slots is not null) return cache.Slots;
            var outList = new List<int>();
            int end = Math.Min(w.Meta.SlotCount, AnalysisTables.CoordBaseEx + AnalysisTables.CoordExSlots);
            for (int slot = AnalysisTables.CoordBaseEx; slot < end; slot++)
                if (CoordRing.IsExSlot(slot)) outList.Add(slot);
            cache.Slots = outList;
            return outList;
        }
    }


    public static bool?[] ScreenFlags(Window w, int slot)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            if (cache.Screen.TryGetValue(slot, out var got)) return got;
            var made = ComputeScreenFlags(
                w.TickCount, slot,
                Column(w, CoordRing.ColState(slot)), Column(w, CoordRing.ColKind(slot)),
                Column(w, ColTimer(slot)), Column(w, ColSide(slot)), Column(w, CoordRing.ColX(slot)));
            cache.Screen[slot] = made;
            return made;
        }
    }

    public static bool?[] ComputeScreenFlags(int n, int slot, uint[]? st, uint[]? kd,
                                             uint[]? tm, uint[]? sd, uint[]? xs)
    {
        var outv = new bool?[n];
        if (kd is null || tm is null || sd is null) return outv;
        var life = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            bool ok = i < n;
            if (ok)
            {
                var s = At(st, i);
                ok = st is null || (s is not null && CoordRing.SlotIsAlive(slot, s.Value));
            }
            if (ok) ok = At(kd, i) is not null && At(tm, i) is not null && At(sd, i) is not null;
            if (ok && life.Count > 0 && (At(kd, i) != At(kd, life[0]) || At(sd, i) != At(sd, life[0])))
            { JudgeLife(life); life.Clear(); }
            if (ok) life.Add(i);
            else if (life.Count > 0) { JudgeLife(life); life.Clear(); }
        }
        return outv;

        void JudgeLife(List<int> ln)
        {
            int exSide = (int)sd![ln[0]];
            int? exType = KindParts(kd![ln[0]]).ExType;
            if (exType is not null && ScreenXyExTypes.Contains(exType.Value)) { Fill(ln, true); return; }
            if (exType is not null && BoardXyExTypes.Contains(exType.Value)) { Fill(ln, false); return; }
            int frames = exType is not null && ExFlightFrames.TryGetValue(exType.Value, out var fr)
                         ? fr : AnalysisTables.ExFlightFramesDefault;
            double ox = ScreenOrigin.TryGetValue(2 - exSide, out var org) ? org.X : 0.0;
            var live = ln.Where(k => tm![k] != 0).ToList();
            for (int pos = 1; pos < ln.Count; pos++)
            {
                int k = ln[pos], p = ln[pos - 1];
                long t = tm![k], pt = tm[p];
                if (!(t < pt && pt == frames)) continue;
                var x = Float(xs, k); var px = Float(xs, p);
                if (x is null || px is null || Math.Abs((x.Value - px.Value) + ox) > AnalysisTables.ExJumpTol) continue;
                Fill(live.Where(j => j < k), true);
                Fill(live.Where(j => j >= k), false);
                return;
            }
            if (live.Any(k => tm![k] > frames)) { Fill(live, false); return; }
            foreach (var k in live)
            {
                var x = Float(xs, k);
                if (x is not null && Math.Abs(x.Value) > AnalysisTables.ExBoardXLimit) { Fill(live, true); return; }
            }
        }

        void Fill(IEnumerable<int> idx, bool v) { foreach (var k in idx) outv[k] = v; }
    }

    public static (double? X, double? Y) BoardXy(Window w, int slot, int i, double? x, double? y)
    {
        if (x is null || y is null || !CoordRing.IsExSlot(slot)) return (x, y);
        return BoardXyCore(ScreenFlags(w, slot), Column(w, ColSide(slot)), i, x, y);
    }

    public static (double? X, double? Y) BoardXyCore(bool?[] screen, uint[]? sideCol,
                                                     int i, double? x, double? y)
    {
        if (x is null || y is null) return (x, y);
        if ((uint)i >= (uint)screen.Length || screen[i] != true) return (x, y);
        var v = At(sideCol, i);
        if (v is null) return (x, y);
        if (!ScreenOrigin.TryGetValue(2 - (int)v.Value, out var org)) return (x, y);
        return (x - org.X, y - org.Y);
    }

    public static bool InFlight(Window w, int slot, int i)
    {
        var flags = ScreenFlags(w, slot);
        return (uint)i < (uint)flags.Length && flags[i] == true;
    }


    public const int TickFrozen = 0, TickStep = 1, TickBreak = 2;

    public static ExFrozen Frozen(Window w, int slot)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            if (cache.Frozen.TryGetValue(slot, out var got)) return got;
            var made = ComputeFrozen(w.TickCount, slot,
                                     Column(w, CoordRing.ColState(slot)), Column(w, ColTimer(slot)));
            cache.Frozen[slot] = made;
            return made;
        }
    }

    public static ExFrozen ComputeFrozen(int n, int slot, uint[]? st, uint[]? tm)
    {
        var marks = new int[n];
        var steps = new int[n];
        var lifeHead = new int[n];
        var pushAt = new List<int> { 0 };
        for (int i = 0; i < n; i++) marks[i] = TickBreak;
        for (int i = 1; i < n; i++)
        {
            var t = At(tm, i); var pt = At(tm, i - 1);
            var a = At(st, i); var b = At(st, i - 1);
            bool alive = st is null || (a is not null && a.Value != 0 && b is not null && b.Value != 0);
            int m;
            if (t is null || pt is null || !alive) m = TickBreak;
            else
            {
                long d = (long)t.Value - pt.Value;
                m = d == 0 ? TickFrozen : d == 1 ? TickStep : TickBreak;
            }
            marks[i] = m;
            steps[i] = steps[i - 1] + (m == TickFrozen ? 0 : 1);
            if (steps[i] != steps[i - 1]) pushAt.Add(i);
            lifeHead[i] = m == TickBreak ? i : lifeHead[i - 1];
        }
        return new ExFrozen(marks, steps, pushAt.ToArray(), lifeHead);
    }

    public static int? TrailTickCore(ExFrozen fz, int i, int back)
    {
        if ((uint)i >= (uint)fz.Steps.Length) return null;
        if (back <= 0) return i;
        int head = fz.LifeHead[i];
        int want = fz.Steps[i] - back;
        return want >= fz.Steps[head] ? fz.PushAt[want] : null;
    }

    public static int? TrailTick(Window w, int slot, int i, int back) =>
        (uint)i >= (uint)w.TickCount ? null
        : TrailTickWith(Frozen(w, slot), slot, Column(w, CoordRing.ColState(slot)),
                        Column(w, ColTimer(slot)), i, back);

    public static int? TrailTickWith(ExFrozen fz, int slot, uint[]? st, uint[]? tm, int i, int back)
    {
        if ((uint)i >= (uint)fz.Steps.Length) return null;
        return TrailTickCore(fz, i, back) ?? SpawnTickCore(slot, st, tm, fz.LifeHead[i]);
    }

    public static int? SpawnTick(Window w, int slot, int head) =>
        SpawnTickCore(slot, Column(w, CoordRing.ColState(slot)), Column(w, ColTimer(slot)), head);

    public static int? SpawnTickCore(int slot, uint[]? st, uint[]? tm, int head)
    {
        if (head <= 0) return null;
        var prev = At(st, head - 1);
        if (prev is null || CoordRing.SlotIsAlive(slot, prev.Value)) return null;
        var t = At(tm, head);
        if (t is not null && t.Value == 0)
        {
            int nxt = head + 1;
            var s = At(st, nxt);
            if (s is null || !CoordRing.SlotIsAlive(slot, s.Value)) return null;
            return nxt;
        }
        return head;
    }

    public static bool[] Still(Window w, int slot, bool xy)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            var key = (slot, xy);
            if (cache.Still.TryGetValue(key, out var got)) return got;
            var made = ComputeStill(w.TickCount, Frozen(w, slot).Marks,
                                    Column(w, CoordRing.ColX(slot)), Column(w, CoordRing.ColY(slot)), xy);
            cache.Still[key] = made;
            return made;
        }
    }

    public static bool[] ComputeStill(int n, int[] marks, uint[]? xs, uint[]? ys, bool xy)
    {
        var outv = new bool[n];
        bool prev = false;
        for (int i = 1; i < n; i++)
        {
            int m = i < marks.Length ? marks[i] : TickBreak;
            if (m == TickFrozen) { outv[i] = prev; continue; }
            if (m != TickStep) { prev = false; continue; }
            var px = Float(xs, i - 1); var cx = Float(xs, i);
            bool ok = px is not null && cx is not null && px.Value == cx.Value;
            if (ok && xy)
            {
                var py = Float(ys, i - 1); var cy = Float(ys, i);
                ok = py is not null && cy is not null && py.Value == cy.Value;
            }
            outv[i] = prev = ok;
        }
        return outv;
    }

    public static bool LandedXCore(bool inFlight, double x) =>
        !inFlight && x >= BoardGeometry.FieldX0 && x <= BoardGeometry.FieldX1;


    public const int BurstNone = 0, BurstFirst = 1, BurstLater = 2, BurstUnknown = 3;

    public static int[] Burst(Window w, int slot)
    {
        var cache = CacheOf(w);
        lock (cache)
        {
            if (cache.Burst.TryGetValue(slot, out var got)) return got;
            var made = ComputeBurst(w.TickCount, slot, Column(w, CoordRing.ColState(slot)),
                                    Still(w, slot, true),
                                    Column(w, CoordRing.ColX(slot)), Column(w, CoordRing.ColY(slot)));
            cache.Burst[slot] = made;
            return made;
        }
    }

    public static int[] ComputeBurst(int n, int slot, uint[]? st, bool[] still, uint[]? xs, uint[]? ys)
    {
        var alive = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var s = At(st, i);
            alive[i] = st is null || (s is not null && s.Value != 0 && CoordRing.SlotIsAlive(slot, s.Value));
        }
        var moved = new bool[n];
        for (int i = 1; i < n; i++)
        {
            var px = Float(xs, i - 1); var cx = Float(xs, i);
            var py = Float(ys, i - 1); var cy = Float(ys, i);
            if (px is null || cx is null || py is null || cy is null) continue;
            moved[i] = px.Value != cx.Value || py.Value != cy.Value;
        }
        var outv = new int[n];
        int a0 = 0;
        while (a0 < n)
        {
            if (!alive[a0]) { a0++; continue; }
            int j = a0;
            while (j < n && alive[j]) j++;
            var runs = new List<(int A, int B, bool MovedBefore)>();
            bool seenMove = false;
            int k = a0;
            while (k < j)
            {
                if (!(k < still.Length && still[k]))
                {
                    seenMove = seenMove || (k > a0 && moved[k]);
                    k++;
                    continue;
                }
                int m = k;
                while (m < j && m < still.Length && still[m]) m++;
                runs.Add((k, m, seenMove));
                k = m;
            }
            for (int nth = 0; nth < runs.Count; nth++)
            {
                var (a, b, movedBefore) = runs[nth];
                int v = nth > 0 ? BurstLater
                      : runs.Count > 1 ? BurstFirst
                      : movedBefore ? BurstFirst
                      : (b == j && j < n) ? BurstLater
                      : BurstUnknown;
                for (int t = a; t < b; t++) outv[t] = v;
            }
            a0 = j;
        }
        return outv;
    }


    public static (double X, double Y)? NextTickCenter(Window w, int slot, int i)
    {
        var seg = SlotSegments.At(w, slot, i);
        if (seg is null) return null;
        (double X, double Y)? PosAt(int j)
        {
            if (j < seg.Value.Start || j > seg.Value.End || j < 0 || j >= w.TickCount) return null;
            var (qx, qy) = BoardXy(w, slot, j,
                                   w.Float(CoordRing.ColX(slot), j), w.Float(CoordRing.ColY(slot), j));
            return qx is null || qy is null ? null : (qx.Value, qy.Value);
        }
        var next = PosAt(i + 1);
        if (next is not null) return next;
        var p0 = PosAt(i); var p1 = PosAt(i - 1); var p2 = PosAt(i - 2);
        if (p0 is null || p1 is null || p2 is null) return null;
        return ExtrapolateNext(p0.Value, p1.Value, p2.Value);
    }

    public static (double X, double Y) ExtrapolateNext(
        (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2)
        => (3.0 * p0.X - 3.0 * p1.X + p2.X, 3.0 * p0.Y - 3.0 * p1.Y + p2.Y);

    public static ExShape? ShapeCore(ExHitbox? box, int? exType, double x, double y, uint? timer,
                                     double? radius, bool inFlight, bool still, bool xStill,
                                     int burstPhase, Func<int, (double X, double Y)?>? trailAt,
                                     Func<(double X, double Y)?>? centerAt = null)
    {
        if (box is null) return null;
        if (inFlight) return null;
        var inner = TimerInner(timer);
        var arm = HitboxArm(exType);
        if (arm is not null && (inner is null || inner < arm.Value)) return null;
        if ((box.GateLo is not null || box.GateHi is not null) && timer is not null)
        {
            if ((box.GateLo is not null && timer.Value < box.GateLo.Value)
                || (box.GateHi is not null && timer.Value > box.GateHi.Value)) return null;
        }
        if (box.NotMovingX && !xStill) return null;
        if (box.Still)
        {
            if (!still) return null;
            if (burstPhase != BurstFirst) return null;
        }
        if (box.Center is not null)
        {
            if (box.Center != "next_tick")
                throw new InvalidOperationException($"知らない center の指定です: {box.Center}");
            var c = centerAt?.Invoke();
            if (c is null) return null;
            (x, y) = (c.Value.X, c.Value.Y);
        }
        if (box.Kind == "aabb")
        {
            if (box.W is null || box.H is null) return null;
            return new ExShape { Rect = (x, y, box.W.Value, box.H.Value) };
        }
        double? r = radius ?? box.R;
        if (box.Formula == "reisen") r = ReisenRadius(inner);
        if (r is null) return null;
        if (box.Trail is null || box.Trail.Count == 0)
            return new ExShape { Circles = [(x, y, r.Value)] };
        if (trailAt is null) return null;
        var circles = new List<(double, double, double)>();
        foreach (var back in box.Trail)
        {
            var p = trailAt(back);
            if (p is not null) circles.Add((p.Value.X, p.Value.Y, r.Value));
        }
        return circles.Count > 0 ? new ExShape { Circles = circles } : null;
    }


    public static List<ExItem> At(Window w, int i, int side)
    {
        int want = 2 - side;
        var outList = new List<ExItem>();
        foreach (var slot in Slots(w))
        {
            var st = w.Raw(CoordRing.ColState(slot), i);
            if (st is null || !CoordRing.SlotIsAlive(slot, st.Value)) continue;
            var parts = KindParts(w.Raw(CoordRing.ColKind(slot), i) ?? 0u, w.Raw(ColVariant(slot), i));
            int exSide = (int)(w.Raw(ColSide(slot), i) ?? 0u);
            if (exSide != want) continue;
            var (x, y) = BoardXy(w, slot, i,
                                 w.Float(CoordRing.ColX(slot), i), w.Float(CoordRing.ColY(slot), i));
            if (x is null || y is null) continue;
            var timer = w.Raw(ColTimer(slot), i);
            var box = parts.Hitbox;
            bool inFlight = InFlight(w, slot, i);
            var shape = ShapeCore(box, parts.ExType, x.Value, y.Value, timer, parts.Radius,
                                  inFlight, StillAt(w, slot, i, timer, true),
                                  LandedXCore(inFlight, x.Value), BurstAt(w, slot, i),
                                  back =>
                                  {
                                      var j = TrailTick(w, slot, i, back);
                                      if (j is null) return null;
                                      var s = w.Raw(CoordRing.ColState(slot), j.Value);
                                      if (s is null || !CoordRing.SlotIsAlive(slot, s.Value)) return null;
                                      var (px, py) = BoardXy(w, slot, j.Value,
                                                             w.Float(CoordRing.ColX(slot), j.Value),
                                                             w.Float(CoordRing.ColY(slot), j.Value));
                                      return px is null || py is null ? null : (px.Value, py.Value);
                                  },
                                  () => NextTickCenter(w, slot, i));
            outList.Add(new ExItem
            {
                Slot = slot, X = x.Value, Y = y.Value,
                Radius = parts.Radius, Name = parts.ExName, ExType = parts.ExType,
                Timer = timer, ExSide = want, Variant = parts.Variant,
                HasHitbox = box is not null, Hitbox = box, Shape = shape,
                FromBoss = FromBoss(w, slot, i, side),
            });
        }
        return outList;
    }

    public static TrailPoint?[] Trail(Window w, int slot, int upto, int? at = null)
    {
        if (!CoordRing.IsExSlot(slot))
            throw new ArgumentException($"slot {slot} は Ex 枠ではありません");
        return HitCandidates.TrailCore(w.TickCount, upto, at,
            i => SlotSegments.At(w, slot, i),
            i =>
            {
                var (x, y) = BoardXy(w, slot, i,
                                     w.Float(CoordRing.ColX(slot), i), w.Float(CoordRing.ColY(slot), i));
                return new TrailPoint(x, y);
            });
    }

    public static Dictionary<int, TrailPoint?[]> TrailsFor(Window w, int side, int upto, int? at = null)
    {
        var outDict = new Dictionary<int, TrailPoint?[]>();
        int end = Math.Min(upto, w.TickCount - 1);
        int want = 2 - side;
        foreach (var slot in Slots(w))
        {
            bool matched = false;
            for (int i = 0; i <= end; i++)
            {
                var st = w.Raw(CoordRing.ColState(slot), i);
                if (st is null || !CoordRing.SlotIsAlive(slot, st.Value)) continue;
                var sd = w.Raw(ColSide(slot), i);
                if (sd is null || (int)sd.Value != want) continue;
                matched = true;
                break;
            }
            if (matched) outDict[slot] = Trail(w, slot, end, at);
        }
        return outDict;
    }

    public static bool StillAt(Window w, int slot, int i, uint? timer, bool xy)
    {
        if (i <= 0 || timer is null) return false;
        var flags = Still(w, slot, xy);
        return (uint)i < (uint)flags.Length && flags[i];
    }

    public static int BurstAt(Window w, int slot, int i)
    {
        var flags = Burst(w, slot);
        return (uint)i < (uint)flags.Length ? flags[i] : BurstNone;
    }


    private static uint[]? Column(Window w, string name) => w.Cols.Has(name) ? w.Cols[name] : null;

    private static uint? At(uint[]? col, int i) =>
        col is not null && (uint)i < (uint)col.Length ? col[i] : null;

    private static double? Float(uint[]? col, int i)
    {
        var v = At(col, i);
        return v is null ? null : CoordRing.UnpackF32(v.Value);
    }


    private sealed class Cache
    {
        public List<int>? Slots;
        public readonly Dictionary<int, bool?[]> Screen = new();
        public readonly Dictionary<int, ExFrozen> Frozen = new();
        public readonly Dictionary<(int Slot, bool Xy), bool[]> Still = new();
        public readonly Dictionary<int, int[]> Burst = new();
        public readonly Dictionary<(int Slot, int Head, int Side), bool?> Boss = new();
    }

    private static readonly ConditionalWeakTable<Window, Cache> Caches = new();

    private static Cache CacheOf(Window w) => Caches.GetValue(w, static _ => new Cache());


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("Ex の update fp の表が空でない（母数）", () =>
            UpdateFp.Count > 0 ? null : "0 件。生成物を引けていない");
        yield return ("★判定の形の表は update fp と同じ母数（全件ぶん出ている）", () =>
            Hitboxes.Count == UpdateFp.Count
                ? null : $"形 {Hitboxes.Count} 件 ≠ fp {UpdateFp.Count} 件");
        yield return ("★判定を持つ fp が 1 つ以上ある（母数）", () =>
            Hitboxes.Values.Count(b => b is not null) > 0 ? null : "0 件。12 列のほどき方が壊れている");
        yield return ("正式名称・色・説明・ガード・鈴仙の半径・親の表が空でない（母数）", () =>
        {
            var empty = new List<string>();
            if (NamesJa.Count == 0) empty.Add("EX_NAME_JA");
            if (Colors.Count == 0) empty.Add("EX_COLORS");
            if (Notes.Count == 0) empty.Add("EX_NOTE");
            if (HitboxArmTable.Count == 0) empty.Add("EX_HITBOX_ARM");
            if (ReisenRadiusTable.Count == 0) empty.Add("ex_reisen_radius");
            if (ParentTypes.Count == 0) empty.Add("EX_PARENT_TYPES");
            return empty.Count == 0 ? null : "0 件の表: " + string.Join(",", empty);
        });

        yield return ("★表に無い fp は ex_type も形も null（未知の type）", () =>
        {
            var p = KindParts(0xDEADBEEFu);
            if (p.ExType is not null) return "知らない fp に type を付けている";
            if (p.Hitbox is not null) return "知らない fp に形を付けている";
            return p.Raw == 0xDEADBEEFu ? null : "生値を残していない";
        });
        yield return ("★『表に無い』と『判定を持たない』が形として分かれる", () =>
        {
            var noBox = UpdateFp.Keys.FirstOrDefault(fp => Hitboxes[fp] is null);
            if (noBox == 0) return "判定を持たない fp が 1 つも無い（表のほどき方が壊れている）";
            bool known = Hitboxes.TryGetValue(noBox, out var b) && b is null;
            bool unknown = !Hitboxes.TryGetValue(0xDEADBEEFu, out _);
            return known && unknown ? null : "2 つが同じ答えになっている";
        });
        yield return ("★ガードの外の鈴仙の半径は null（0 を返さない）", () =>
        {
            var lo = ReisenRadiusTable.Keys.Min();
            if (ReisenRadius(lo - 1) is not null) return "ガードの下の外で半径が出た";
            if (ReisenRadius(ReisenRadiusTable.Keys.Max() + 1) is not null) return "ガードの上の外で半径が出た";
            return ReisenRadius(lo) is not null ? null : "ガードの中で半径が出ない";
        });
        yield return ("★ガードの無い type にはガードを掛けない（0 に倒さない）", () =>
            HitboxArm(null) is null && HitboxArm(0x7F) is null ? null : "知らない type にガードが付いた");
        yield return ("★ex_timer_inner は負にもなる（0 で止めない）", () =>
            TimerInner(0) == -1 && TimerInner(null) is null ? null : "0 で止めている");

        yield return ("★飛行中は判定が無い（十字になる）", () =>
        {
            var box = FirstBox(b => b.Kind == "circle" && b.R is not null && b.Trail is null
                                    && !b.Still && !b.NotMovingX && b.GateLo is null);
            if (box is null) return "素直な円の type が表に無い";
            return ShapeCore(box.Value.Box, box.Value.Type, 0, 0, 200, null,
                             inFlight: true, still: false, xStill: false,
                             burstPhase: BurstNone, trailAt: null) is null
                   ? null : "飛行中に判定が出た";
        });
        yield return ("★ガードより前は判定が無い / ガードから先は出る", () =>
        {
            var t = ArmedType();
            if (t is null) return "ガードを持つ type が表に無い";
            var (fp, exType, arm) = t.Value;
            var box = Hitboxes[fp];
            var before = ShapeCore(box, exType, 0, 0, (uint)arm, box!.R, false, false, false, BurstNone, null);
            var after = ShapeCore(box, exType, 0, 0, (uint)(arm + 1), box.R, false, false, false, BurstNone, null);
            if (before is not null) return "記録 t=arm（inner=arm-1）で判定が出た（位相が 1 ずれている）";
            return after is not null ? null : "記録 t=arm+1 でも判定が出ない";
        });
        yield return ("★★鈴仙の円は inner の半径で出る（記録の値では出ない）", () =>
        {
            var box = FirstBox(b => b.Formula == "reisen");
            if (box is null) return "鈴仙が表に無い";
            const uint rec = 10;
            var sh = ShapeCore(box.Value.Box, box.Value.Type, 0, 0, rec, null,
                               inFlight: false, still: true, xStill: false,
                               burstPhase: BurstFirst, trailAt: null);
            if (sh?.Circles is null || sh.Circles.Count != 1) return "円が 1 つ出ない";
            double got = sh.Circles[0].R;
            if (got != ReisenRadius(TimerInner(rec))) return $"半径 {got} が inner の値と違う";
            return got != ReisenRadius(rec) ? null : "★記録の値の半径になっている（1 段大きい）";
        });
        yield return ("★★止まっている区間が 2 本目（後始末）なら判定は無い / 判別不能も描かない", () =>
        {
            var box = FirstBox(b => b.Formula == "reisen");
            if (box is null) return "鈴仙が表に無い";
            const uint rec = 10;
            ExShape? Try(int phase) => ShapeCore(box.Value.Box, box.Value.Type, 0, 0, rec, null,
                                                 inFlight: false, still: true, xStill: false,
                                                 burstPhase: phase, trailAt: null);
            if (Try(BurstFirst) is null) return "炸裂（FIRST）で判定が出ない";
            if (Try(BurstLater) is not null) return "★後始末（state 3）に判定が出た（成長を 2 回描く）";
            return Try(BurstUnknown) is null ? null : "判別できない区間を描いた（推測している）";
        });
        yield return ("★AABB は (中心, 中心, 全幅, 全高) で返る", () =>
        {
            var box = FirstBox(b => b.Kind == "aabb");
            if (box is null) return "AABB の type が表に無い";
            var sh = ShapeCore(box.Value.Box, null, 3.0, 4.0, 100, null,
                               false, false, xStill: true, BurstNone, null);
            if (sh?.Rect is null) return "矩形が出ない";
            if (ShapeCore(box.Value.Box, null, 3.0, 4.0, 100, null,
                          false, false, xStill: false, BurstNone, null) is not null)
                return "★x が動いている間（弧）に矩形が出た";
            var r = sh.Rect.Value;
            return r.X == 3.0 && r.Y == 4.0 && r.W == box.Value.Box.W && r.H == box.Value.Box.H
                   ? null : $"矩形が {r}";
        });
        yield return ("★★LandedXCore: 飛んでいなければ盤内の x で真、盤の外・飛行中は偽", () =>
        {
            double inX = (BoardGeometry.FieldX0 + BoardGeometry.FieldX1) / 2.0;
            if (!LandedXCore(inFlight: false, x: inX)) return "盤内の x なのに false";
            if (LandedXCore(inFlight: true, x: inX)) return "飛行中なのに true（先に弾いていない）";
            if (LandedXCore(inFlight: false, x: BoardGeometry.FieldX1 + 1.0))
                return "盤の外の x なのに true（y は見ないはずだが x は見るはず）";
            if (!LandedXCore(inFlight: false, x: BoardGeometry.FieldX0))
                return "FieldX0 ちょうどが false（境界を含んでいない）";
            return LandedXCore(inFlight: false, x: BoardGeometry.FieldX1) ? null : "FieldX1 ちょうどが false";
        });
        yield return ("★★LandedXCore: y は見ない（★実測で y を足すと正しいものまで弾く）", () =>
        {
            var m = typeof(ExItems).GetMethod(nameof(LandedXCore));
            return m!.GetParameters().Length == 2 ? null : $"引数が {m.GetParameters().Length} 個（y が増えている）";
        });
        yield return ("★否定: 旧（＝前後差の ComputeStill）だと窓の頭（tick 0）が必ず「動いている」に倒れる", () =>
        {
            var marks = new[] { TickStep, TickStep, TickStep };
            var xs = new[] { F(0f), F(0f), F(0f) };
            var legacy = ComputeStill(3, marks, xs, xs, xy: false);
            if (legacy[0]) return "旧実装でも tick 0 が true になった（否定テストが効いていない）";
            return LandedXCore(inFlight: false, x: 0.0) ? null : "新でも窓の頭（tick 0 相当）で判定が出ない";
        });
        yield return ("★咲夜の子は段ぶんの円が出る / 1 つも遡れなければ null", () =>
        {
            var box = FirstBox(b => b.Trail is not null && b.Trail.Count > 0);
            if (box is null) return "トレイルを持つ type が表に無い";
            int arm = HitboxArm(box.Value.Type) ?? 0;
            uint t = (uint)(arm + 1);
            var all = ShapeCore(box.Value.Box, box.Value.Type, 0, 0, t, box.Value.Box.R,
                                false, false, false, BurstNone, back => (back * 1.0, 0.0));
            if (all?.Circles is null || all.Circles.Count != box.Value.Box.Trail!.Count)
                return $"円が {all?.Circles?.Count} 個（段は {box.Value.Box.Trail!.Count} 個）";
            var none = ShapeCore(box.Value.Box, box.Value.Type, 0, 0, t, box.Value.Box.R,
                                 false, false, false, BurstNone, _ => null);
            return none is null ? null : "1 つも遡れないのに形が出た";
        });

        yield return ("★★Center=next_tick の枠は、渡した中心ではなく centerAt の位置に円を置く", () =>
        {
            var box = FirstBox(b => b.Center is not null);
            if (box is null) return "Center を持つ type が表に無い";
            if (box.Value.Box.Center != "next_tick") return $"知らない Center: {box.Value.Box.Center}";
            uint t = (uint)((HitboxArm(box.Value.Type) ?? 0) + 1);
            var got = ShapeCore(box.Value.Box, box.Value.Type, 7.0, 9.0, t, box.Value.Box.R,
                                false, false, false, BurstNone, null, () => (41.0, 43.0));
            if (got?.Circles is null || got.Circles.Count != 1) return $"円が {got?.Circles?.Count} 個";
            var (cx, cy, _) = got.Circles[0];
            if (Math.Abs(cx - 41.0) > 1e-9 || Math.Abs(cy - 43.0) > 1e-9)
                return $"中心が ({cx}, {cy})（(41, 43) のはず。記録位置は (7, 9)）";
            var none = ShapeCore(box.Value.Box, box.Value.Type, 7.0, 9.0, t, box.Value.Box.R,
                                 false, false, false, BurstNone, null, () => null);
            return none is null ? null : "中心を出せないのに形が出た";
        });
        yield return ("★Center を持たない枠は、渡した中心のまま", () =>
        {
            var box = FirstBox(b => b.Center is null && b.Kind == "circle"
                                    && b.R is not null && b.Trail is null && !b.Still);
            if (box is null) return "Center 無しの単純な円の type が表に無い";
            uint t = (uint)((HitboxArm(box.Value.Type) ?? 0) + 1);
            var got = ShapeCore(box.Value.Box, box.Value.Type, 7.0, 9.0, t, box.Value.Box.R,
                                false, false, false, BurstNone, null, () => (41.0, 43.0));
            if (got?.Circles is null || got.Circles.Count != 1) return $"円が {got?.Circles?.Count} 個";
            var (cx, cy, _) = got.Circles[0];
            return Math.Abs(cx - 7.0) < 1e-9 && Math.Abs(cy - 9.0) < 1e-9
                ? null : $"中心が ({cx}, {cy})（(7, 9) のはず）";
        });

        yield return ("合成: 凍結した tick では段が進まない", () =>
        {
            var fz = ComputeFrozen(5, AnalysisTables.CoordBaseEx, [1, 1, 1, 1, 1], [0, 1, 1, 2, 3]);
            if (fz.Marks[2] != TickFrozen) return $"tick2 が {fz.Marks[2]}（FROZEN のはず）";
            if (fz.Steps[4] != 3) return $"段が {fz.Steps[4]}（3 のはず。凍結ぶんが詰まっていない）";
            return fz.LifeHead[4] == 0 ? null : $"一生の頭が {fz.LifeHead[4]}";
        });
        yield return ("合成: ex_timer が飛んだら BREAK（一生を数え直す）", () =>
        {
            var fz = ComputeFrozen(4, AnalysisTables.CoordBaseEx, [1, 1, 1, 1], [1, 2, 0, 1]);
            if (fz.Marks[2] != TickBreak) return $"tick2 が {fz.Marks[2]}（BREAK のはず）";
            return fz.LifeHead[3] == 2 ? null : $"一生の頭が {fz.LifeHead[3]}（2 のはず）";
        });
        yield return ("★遡り切れなければ null（窓の頭に張り付けない）", () =>
        {
            var fz = ComputeFrozen(4, AnalysisTables.CoordBaseEx, [1, 1, 1, 1], [1, 2, 3, 4]);
            if (TrailTickCore(fz, 3, 0) != 3) return "0 段前がその tick でない";
            if (TrailTickCore(fz, 3, 2) != 1) return "2 段前が合わない";
            return TrailTickCore(fz, 3, 99) is null ? null : "遡り切れないのに tick を返した";
        });

        yield return ("★凍結中は直前の判定を持ち越す（消さない）", () =>
        {
            int[] marks = [TickBreak, TickStep, TickFrozen, TickStep];
            uint[] xs = [F(1f), F(1f), F(1f), F(1f)];
            uint[] ys = [F(2f), F(2f), F(2f), F(2f)];
            var still = ComputeStill(4, marks, xs, ys, xy: true);
            if (!still[1]) return "止まっているのに false";
            return still[2] ? null : "★凍結の tick で判定が消えた（持ち越していない）";
        });
        yield return ("合成: 止まった区間が 2 本あれば 1 本目が炸裂・2 本目が後始末", () =>
        {
            bool[] still = [false, true, true, false, true, true];
            uint[] xs = [F(0f), F(0f), F(0f), F(9f), F(9f), F(9f)];
            uint[] ys = [F(0f), F(0f), F(0f), F(0f), F(0f), F(0f)];
            var b = ComputeBurst(6, AnalysisTables.CoordBaseEx, [1, 1, 1, 1, 1, 1], still, xs, ys);
            if (b[1] != BurstFirst) return $"1 本目が {b[1]}（FIRST のはず）";
            return b[4] == BurstLater ? null : $"2 本目が {b[4]}（LATER のはず）";
        });
        yield return ("★両端が切れている区間は UNKNOWN（描かない）", () =>
        {
            bool[] still = [true, true, true];
            uint[] xs = [F(0f), F(0f), F(0f)];
            var b = ComputeBurst(3, AnalysisTables.CoordBaseEx, [1, 1, 1], still, xs, xs);
            return b[0] == BurstUnknown ? null : $"{b[0]}（UNKNOWN のはず）";
        });

        yield return ("★kind / ex_timer / ex_side のどれかが無い窓は全部『不明』（無補正）", () =>
        {
            var f = ComputeScreenFlags(3, AnalysisTables.CoordBaseEx, [1, 1, 1], null, [1, 2, 3], [0, 0, 0], null);
            return f.All(v => v is null) ? null : "列が無いのに座標系を決めた";
        });
        yield return ("★一生ずっと画面座標の type は全部『画面』（魔理沙・咲夜の親）", () =>
        {
            var fp = UpdateFp.FirstOrDefault(kv => kv.Value.Name == "marisa").Key;
            if (fp == 0) return "marisa が表に無い";
            var f = ComputeScreenFlags(3, AnalysisTables.CoordBaseEx, [1, 1, 1],
                                       [fp, fp, fp], [1, 2, 3], [0, 0, 0], null);
            return f.All(v => v == true) ? null : "画面座標に倒れていない";
        });

        yield return ("★否定①: 生の ex_timer を使う旧なら鈴仙の円が 1 段大きい", () =>
        {
            const uint rec = 10;
            var legacy = ReisenRadius(rec);
            var now = ReisenRadius(TimerInner(rec));
            if (legacy is null || now is null) return "どちらかが引けない（標本の選び方が悪い）";
            return legacy.Value > now.Value ? null
                 : $"旧 {legacy} が新 {now} より大きくない（否定テストの意味が無い）";
        });
        yield return ("★否定②: ガードを掛けない旧なら飛行中の鈴仙に判定が出る", () =>
        {
            var box = FirstBox(b => b.Formula == "reisen");
            if (box is null) return "鈴仙が表に無い";
            uint t = (uint)(box.Value.Box.GateLo!.Value + 5);
            bool legacyDraws = t >= box.Value.Box.GateLo!.Value && t <= box.Value.Box.GateHi!.Value
                               && ReisenRadius(TimerInner(t)) is not null;
            if (!legacyDraws) return "旧が落ちない（写しが間違っている。否定テストの意味が無い）";
            var now = ShapeCore(box.Value.Box, box.Value.Type, 0, 0, t, null,
                                inFlight: false, still: false, xStill: false,
                                burstPhase: BurstFirst, trailAt: null);
            return now is null ? null : "新でも飛行中（止まっていない）に判定が出た";
        });
        yield return ("★否定③: type を見ずに ex_variant を読む旧なら幽香以外に変種が付く", () =>
        {
            var yuuka = UpdateFp.FirstOrDefault(kv => Hitboxes[kv.Key]?.RByVariant is not null).Key;
            var other = UpdateFp.FirstOrDefault(kv => Hitboxes[kv.Key]?.RByVariant is null
                                                     && Hitboxes[kv.Key]?.R is not null).Key;
            if (yuuka == 0 || other == 0) return "標本になる type が表に無い";
            const uint word = 3;
            int legacy = (int)(word & AnalysisTables.ExVariantMask);
            if (legacy != 3) return "旧が落ちない（写しが間違っている）";
            if (VariantOf(other, word) is not null) return "★幽香以外に変種が付いた";
            return VariantOf(yuuka, word) == 3 ? null : "幽香で変種が取れない";
        });
        yield return ("★否定④: 生の tick で遡る旧なら凍結を跨いだ円が違う位置を掴む", () =>
        {
            var fz = ComputeFrozen(5, AnalysisTables.CoordBaseEx, [1, 1, 1, 1, 1], [1, 2, 2, 2, 3]);
            int legacy = 4 - 2;
            var now = TrailTickCore(fz, 4, 2);
            if (now is null) return "段で遡れない（合成が悪い）";
            return now.Value != legacy ? null
                 : $"旧と新が同じ tick {legacy}（凍結を跨いでいない。否定テストの意味が無い）";
        });

        yield return ("★否定⑤: 画面座標の type を盤内座標のまま出す旧なら x=632 が盤に出る", () =>
        {
            var fps = UpdateFp.Where(kv => ScreenXyExTypes.Contains(kv.Value.ExType))
                              .Select(kv => kv.Key).ToList();
            if (fps.Count == 0) return "画面座標の type が表に無い";
            const float sx = 632f;
            foreach (var fp in fps)
            {
                uint[] st = [1, 1, 1], kd = [fp, fp, fp], tm = [1, 2, 3], sd = [0, 0, 0];
                uint[] xs = [F(sx), F(sx), F(sx)], ys = [F(20f), F(20f), F(20f)];
                var screen = ComputeScreenFlags(3, AnalysisTables.CoordBaseEx, st, kd, tm, sd, xs);
                double legacy = CoordRing.UnpackF32(xs[1]);
                if (Math.Abs(legacy) <= AnalysisTables.ExBoardXLimit)
                    return "旧が落ちない（標本の x が盤に収まっている。否定テストの意味が無い）";
                if (screen[1] != true) return $"fp 0x{fp:X8} を画面座標と見ていない";
                var (bx, _) = BoardXyCore(screen, sd, 1, legacy, 20.0);
                if (bx is null) return "補正が位置を捨てた";
                if (Math.Abs(bx.Value) > AnalysisTables.ExBoardXLimit)
                    return $"★fp 0x{fp:X8} が x={bx} のまま盤に出た（2026-08-10 の不具合）";
            }
            return null;
        });
        yield return ("★否定⑥: boss preset の空を『ボス由来でない』に倒す旧なら鈴仙を取り違える", () =>
        {
            var blank = BossBirthTimer.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList();
            if (blank.Count == 0) return "preset が空の行が表に無い（鈴仙の行が消えている）";
            uint fp = blank[0];
            bool LegacyFromBoss(uint kind, uint birthTimer, int bossType)
            {
                int preset = BossBirthTimer.TryGetValue(kind, out var v) ? v ?? 0 : 0;
                return birthTimer == (uint)preset && bossType == AnalysisTables.BossTypeBoss;
            }
            if (LegacyFromBoss(fp, 1, AnalysisTables.BossTypeBoss) != false)
                return "旧が落ちない（写しが間違っている。否定テストの意味が無い）";
            var now = BornFromBoss(fp, 1, AnalysisTables.BossTypeBoss);
            return now is null ? null : $"新も {now} と答えた（割れないものを言い切っている）";
        });

        yield return ("★座標系の表の母数（原点 / 画面 type / 盤内 type / 飛行の長さ）", () =>
        {
            var zero = new List<string>();
            if (ScreenOrigin.Count == 0) zero.Add("SCREEN_ORIGIN");
            if (ScreenXyExTypes.Count == 0) zero.Add("SCREEN_XY_EX_TYPES");
            if (BoardXyExTypes.Count == 0) zero.Add("BOARD_XY_EX_TYPES");
            if (ExFlightFrames.Count == 0) zero.Add("EX_FLIGHT_FRAMES");
            if (zero.Count > 0) return "0 件の表: " + string.Join(",", zero);
            if (!ScreenOrigin.ContainsKey(1) || !ScreenOrigin.ContainsKey(2))
                return "盤の側 1 / 2 の原点が揃っていない: " + string.Join(",", ScreenOrigin.Keys);
            return AnalysisTables.ExFlightFramesDefault > 0 ? null : "飛行の既定の長さが 0";
        });
        yield return ("★ボスの誕生 preset の母数と、★preset が空の行が在ること", () =>
        {
            if (BossBirthTimer.Count == 0) return "0 件。生成物を引けていない";
            int blank = BossBirthTimer.Count(kv => kv.Value is null);
            if (blank == 0) return "★preset が空の行が 1 つも無い（鈴仙の行を埋めていないか）";
            return BossBirthTimer.Count - blank > 0 ? null : "preset が全部空（突き合わせられない）";
        });
        yield return ("★★Ex の 3 列が正しい列名になる（★並びを取り違えると値が黙って入れ替わる）", () =>
        {
            if (ExSuffixes.Length <= SuffixVariantIdx)
                return $"接尾辞が {ExSuffixes.Length} 本（timer / side / variant が要る）";
            int slot = AnalysisTables.CoordBaseEx;
            var b = CoordRing.BaseName(slot);
            if (ColTimer(slot) != b + "_timer") return $"timer の列名が {ColTimer(slot)}";
            if (ColSide(slot) != b + "_side") return $"side の列名が {ColSide(slot)}";
            if (ColVariant(slot) != b + "_variant") return $"variant の列名が {ColVariant(slot)}";
            return new HashSet<string> { ColTimer(slot), ColSide(slot), ColVariant(slot) }.Count == 3
                ? null : "3 列が別の名前になっていない";
        });

        yield return ("★★ボス由来は 3 値（表に無い fp は null / 表に無いキャラは false）", () =>
        {
            if (BornFromBoss(0xDEADBEEFu, 91, AnalysisTables.BossTypeBoss) is not null)
                return "未知の fp に答えを出した";
            var notBoss = UpdateFp.Keys.FirstOrDefault(fp => !BossBirthTimer.ContainsKey(fp));
            if (notBoss == 0) return "ボスが Ex を置かない fp が表に無い";
            if (BornFromBoss(notBoss, 91, AnalysisTables.BossTypeBoss) != false)
                return "誕生タイマーの表に無い fp を false と言えていない";
            var withPreset = BossBirthTimer.First(kv => kv.Value is not null);
            uint p = (uint)withPreset.Value!.Value;
            if (BornFromBoss(withPreset.Key, p, AnalysisTables.BossTypeBoss) != true)
                return "preset ちょうど ＋ boss_type 3 を true と言えていない";
            if (BornFromBoss(withPreset.Key, p + 1, AnalysisTables.BossTypeBoss) != false)
                return "★preset より大きい誕生 timer を拾った（`>=` になっている）";
            if (BornFromBoss(withPreset.Key, p, AnalysisTables.BossTypeBoss + 1) != false)
                return "boss_type を見ていない";
            return BornFromBoss(withPreset.Key, null, 3) is null
                && BornFromBoss(withPreset.Key, p, null) is null
                ? null : "読めない語を false に倒している";
        });

        yield return ("★★★合成: 鈴仙は preset の道では割れない（BornFromBoss はいまも null）", () =>
        {
            return BornFromBoss(AnalysisTables.ExReisenUpdateFp, 1, AnalysisTables.BossTypeBoss) is null
                ? null : "preset の道が鈴仙を言い切ってしまっている";
        });
        yield return ("★★★合成: ちょうど1個 ＝ キャラの Ex（false）", () =>
        {
            var one = new List<(uint X, uint Y)> { (100u, 200u) };
            return ReisenBossGroup(one) == false ? null : "1 個を false と言えていない";
        });
        yield return ("★★★合成: ちょうど3個・座標が完全一致 ＝ ボスの3way（true）", () =>
        {
            var three = new List<(uint X, uint Y)> { (100u, 200u), (100u, 200u), (100u, 200u) };
            return ReisenBossGroup(three) == true ? null : "3 個・一致を true と言えていない";
        });
        yield return ("★★★合成: 2個だけ・4個・3個だが座標が1つずれる ＝ null（押し切らない）", () =>
        {
            var two = new List<(uint X, uint Y)> { (100u, 200u), (100u, 200u) };
            if (ReisenBossGroup(two) is not null) return "2 個を言い切ってしまっている";
            var four = new List<(uint X, uint Y)>
                { (100u, 200u), (100u, 200u), (100u, 200u), (100u, 200u) };
            if (ReisenBossGroup(four) is not null) return "4 個を言い切ってしまっている";
            var mismatched = new List<(uint X, uint Y)> { (100u, 200u), (100u, 200u), (100u, 201u) };
            return ReisenBossGroup(mismatched) is null
                ? null : "座標が 1 つずれても true と言えてしまっている";
        });
        yield return ("★否定: 個数を見ず座標一致だけで判定する旧なら、2個一致を true と言ってしまう", () =>
        {
            bool? LegacyGroup(IReadOnlyList<(uint X, uint Y)> g) =>
                g.Count >= 2 && g.All(p => p == g[0]);
            var two = new List<(uint X, uint Y)> { (100u, 200u), (100u, 200u) };
            if (LegacyGroup(two) != true) return "旧が落ちない（写しが間違っている。否定テストの意味が無い）";
            return ReisenBossGroup(two) is null
                ? null : "新も 2 個一致を言い切ってしまっている（個数を見ていない）";
        });

        yield return ($"★★★母数: Ex 枠自体が C2/C3 の設置物と分かる type の表が 0 行でない（{CardLevelTypeCount} type）", () =>
            CardLevelTypeCount > 0 ? null : "0 件。生成表を引けていない");
        yield return ("★★★合成: type 7 は C2（レベル 2）、type 8・9 は C3（レベル 3）", () =>
        {
            var c2Types = CardLevelTable.Where(kv => kv.Value == 2).Select(kv => kv.Key).ToList();
            var c3Types = CardLevelTable.Where(kv => kv.Value == 3).Select(kv => kv.Key).ToList();
            if (c2Types.Count != 1) return $"C2（レベル 2）の type が {c2Types.Count} 件（1 件の想定）";
            if (c3Types.Count != 2) return $"C3（レベル 3）の type が {c3Types.Count} 件（2 件の想定。2 個 spawn するので）";
            if (CardLevelOf(c2Types[0]) != 2) return "CardLevelOf が C2 を 2 と言わない";
            return c3Types.All(t => CardLevelOf(t) == 3) ? null : "CardLevelOf が C3 を 3 と言わない";
        });
        yield return ("★★★合成: ボスの設置物（type 0x0B/0x0C）はここに乗らない（BornFromBoss が別に見分ける）", () =>
        {
            var bossTypes = BossBirthTimer.Keys
                .Where(fp => UpdateFp.ContainsKey(fp))
                .Select(fp => UpdateFp[fp].ExType).ToList();
            if (bossTypes.Count == 0) return "ボス由来の type が表に無い（母数が足りない）";
            foreach (var t in bossTypes)
                if (CardLevelOf(t) is not null) return $"ボスの type {t:X} が CardLevelOf に乗った（役割が混ざった）";
            return null;
        });
        yield return ("★★★否定: 表に無い type（未知）は CardLevelOf が null（推測で付けない）", () =>
        {
            int unknown = 0;
            while (CardLevelTable.ContainsKey(unknown)) unknown++;
            return CardLevelOf(unknown) is null && CardLevelOf(null) is null
                ? null : "未知の type / null にレベルが付いた";
        });
    }

    private static (ExHitbox Box, int? Type)? FirstBox(Func<ExHitbox, bool> pred)
    {
        foreach (var (fp, box) in Hitboxes)
            if (box is not null && pred(box))
                return (box, UpdateFp.TryGetValue(fp, out var e) ? e.ExType : null);
        return null;
    }

    private static (uint Fp, int ExType, int Arm)? ArmedType()
    {
        foreach (var (fp, ent) in UpdateFp)
        {
            if (Hitboxes[fp] is not { } box || box.Kind != "circle" || box.R is null) continue;
            if (box.Still || box.NotMovingX || box.Trail is not null || box.GateLo is not null) continue;
            if (HitboxArmTable.TryGetValue(ent.ExType, out var arm)) return (fp, ent.ExType, arm);
        }
        return null;
    }

    private static uint F(float v) => BitConverter.SingleToUInt32Bits(v);
}

public readonly record struct ExFrozen(int[] Marks, int[] Steps, int[] PushAt, int[] LifeHead);
