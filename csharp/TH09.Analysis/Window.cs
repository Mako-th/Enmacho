using System.Collections;
using TA = TH09.Layer0.TickArchive;

namespace TH09.Analysis;

public sealed class Window
{
    public WindowMeta Meta { get; }
    public WindowColumns Cols { get; }
    public MainColumns Main { get; }
    public MainColumns Pre { get; }

    public int TickCount => Meta.TickCount;

    private Dictionary<int, Dictionary<string, List<int>>>? _slotCache;
    private readonly Dictionary<string, double?[]?> _extCache = new(StringComparer.Ordinal);

    public Window(WindowMeta meta, WindowColumns cols, MainColumns main, MainColumns pre)
    {
        Meta = meta; Cols = cols; Main = main; Pre = pre;
    }

    public Window(WindowMeta meta, TA.TickColumns cols, MainColumns main, MainColumns pre)
        : this(meta, WindowColumns.Of(cols), main, pre) { }

    public List<WindowEvent> Events()
    {
        var outList = new List<WindowEvent>();
        foreach (var e in Meta.Hits)
        {
            int raw = (int)(e.Seq - Meta.FirstSeq);
            int i = e.Trigger == TriggerHitKind ? raw - 1 : raw;
            if (i >= 0 && i < TickCount)
                outList.Add(new WindowEvent(Math.Max(0, i), raw, e.Side, e.Trigger));
        }
        return outList;
    }

    public const string TriggerHitKind = "hit_kind";

    public static readonly string[] NonHitTriggers = ["quick"];

    public int? PrimaryEventIndex(int? side = null)
    {
        var ev = Events();
        if (ev.Count == 0) return null;
        var idx = ev.Select((x, k) => (x, k)).Where(t => side is null || t.x.Side == side.Value)
                    .Select(t => t.k).ToList();
        if (idx.Count == 0) idx = Enumerable.Range(0, ev.Count).ToList();
        var real = idx.Where(k => !NonHitTriggers.Contains(ev[k].Trigger, StringComparer.Ordinal)).ToList();
        return (real.Count > 0 ? real : idx)[0];
    }

    public uint? Raw(string? name, int i)
    {
        if (name is null || !Cols.Has(name)) return null;
        var col = Cols[name];
        return (uint)i < (uint)col.Length ? col[i] : null;
    }

    public double? Float(string? name, int i)
    {
        var v = Raw(name, i);
        return v is null ? null : CoordRing.UnpackF32(v.Value);
    }

    public double? MainAt(string name, int i)
    {
        if (!Main.Has(name)) return null;
        var col = Main[name];
        if ((uint)i >= (uint)col.Length) return null;
        return col[i].IsMissing ? null : col[i].AsDouble;
    }

    public double?[]? Series(string name)
    {
        if (!Main.Has(name)) return null;
        var col = Main[name];
        var outv = new double?[TickCount];
        for (int i = 0; i < outv.Length; i++)
            outv[i] = i < col.Length && !col[i].IsMissing ? col[i].AsDouble : null;
        return outv;
    }


    public int PreCount => Pre.TickCount;

    public int ExtIndex(int i) => PreCount + i;

    public double?[]? ExtSeries(string name)
    {
        if (_extCache.TryGetValue(name, out var got)) return got;
        if (!Main.Has(name)) { _extCache[name] = null; return null; }
        int pn = PreCount;
        var outv = new double?[pn + TickCount];
        if (pn > 0 && Pre.Has(name))
        {
            var pre = Pre[name];
            int from = Math.Max(0, pre.Length - pn);
            for (int k = from; k < pre.Length; k++)
                outv[pn - (pre.Length - k)] = pre[k].IsMissing ? null : pre[k].AsDouble;
        }
        var body = Main[name];
        for (int k = 0; k < TickCount; k++)
            outv[pn + k] = k < body.Length && !body[k].IsMissing ? body[k].AsDouble : null;
        _extCache[name] = outv;
        return outv;
    }

    public double? ExtAt(string name, int i)
    {
        var col = ExtSeries(name);
        if (col is null) return null;
        int x = ExtIndex(i);
        return (uint)x < (uint)col.Length ? col[x] : null;
    }

    public uint? CoordFlags(int i) => Raw(AnalysisTables.CoordFlagsColumn, i);

    public bool HasCoordFlags() => Cols.Has(AnalysisTables.CoordFlagsColumn);

    public Dictionary<string, List<int>> SlotsOf(int side)
    {
        if (_slotCache is null)
        {
            var cache = new Dictionary<int, Dictionary<string, List<int>>>
            {
                [1] = NewBuckets(),
                [2] = NewBuckets(),
            };
            if (Meta.SlotCount > CoordRing.SlotCount)
                throw new InvalidDataException(
                    $"窓の slot_count({Meta.SlotCount}) が枠の表({CoordRing.SlotCount})より多い"
                    + "（座標リングの版が違う窓を、今の表で読もうとしている）");
            for (int slot = 0; slot < Meta.SlotCount; slot++)
            {
                var b = CoordRing.BaseName(slot);
                if (b.Length < 4 || b[0] != 'p') continue;
                int sd = b[1] == '1' ? 1 : 2;
                var key = b[3] switch { 'b' => "bullet", 'e' => "enemy", 'l' => "laser", _ => null };
                if (key is not null) cache[sd][key].Add(slot);
            }
            _slotCache = cache;
        }
        return _slotCache[side];
    }

    private static Dictionary<string, List<int>> NewBuckets() =>
        new(StringComparer.Ordinal) { ["bullet"] = [], ["enemy"] = [], ["laser"] = [] };

    public Board BoardAt(int i, int side)
    {
        var board = new Board();
        foreach (var (kind, slots) in SlotsOf(side))
        {
            var list = kind switch
            {
                "bullet" => board.Bullets,
                "enemy" => board.Enemies,
                _ => board.Lasers,
            };
            foreach (var slot in slots)
            {
                var st = Raw(CoordRing.ColState(slot), i);
                if (st is null || !CoordRing.SlotIsAlive(slot, st.Value)) continue;
                if (CoordRing.SlotIsVanishing(slot, st.Value)) continue;
                var item = new BoardItem
                {
                    Slot = slot,
                    X = Float(CoordRing.ColX(slot), i),
                    Y = Float(CoordRing.ColY(slot), i),
                    Kind = Raw(CoordRing.ColKind(slot), i),
                    State = st.Value,
                };
                if (kind == "laser") item.Laser = LaserOf(slot, i);
                else if (kind == "enemy") item.EnemyClass = CoordRing.EnemyClass(item.Kind ?? 0);
                list.Add(item);
            }
        }
        return board;
    }

    public LaserInfo LaserOf(int slot, int i)
    {
        uint? Get(int which) => Raw(CoordRing.LaserCol(slot, which), i);
        var timer = Get(4); var gate0 = Get(5); var gate2 = Get(6); var phase = Get(7);
        bool lethal = timer is not null && gate0 is not null && gate2 is not null && phase is not null
                      && CoordRing.LaserIsLethal(phase.Value, timer.Value, gate0.Value, gate2.Value);
        return new LaserInfo(
            Float(CoordRing.LaserCol(slot, 0), i),
            Float(CoordRing.LaserCol(slot, 1), i),
            Float(CoordRing.LaserCol(slot, 2), i),
            Float(CoordRing.LaserCol(slot, 3), i),
            timer,
            phase is null ? null : CoordRing.LaserPhase(phase.Value),
            lethal);
    }
}

public readonly record struct WindowEvent(int Index, int Raw, int Side, string Trigger);

public sealed class Board
{
    public List<BoardItem> Bullets { get; } = [];
    public List<BoardItem> Enemies { get; } = [];
    public List<BoardItem> Lasers { get; } = [];
    public int Count => Bullets.Count + Enemies.Count + Lasers.Count;
}

public sealed class BoardItem
{
    public required int Slot { get; init; }
    public required double? X { get; init; }
    public required double? Y { get; init; }
    public required uint? Kind { get; init; }
    public required uint State { get; init; }
    public string? EnemyClass { get; set; }
    public LaserInfo? Laser { get; set; }
}

public readonly record struct LaserInfo(double? Angle, double? Tail, double? Head, double? Width,
                                        uint? Timer, uint? Phase, bool Lethal);

public sealed class WindowColumns : IEnumerable<KeyValuePair<string, uint[]>>
{
    private readonly Dictionary<string, uint[]> _cols;

    public WindowColumns(Dictionary<string, uint[]> cols) => _cols = cols;

    public static WindowColumns Of(TA.TickColumns cols)
    {
        var map = new Dictionary<string, uint[]>(cols.Count, StringComparer.Ordinal);
        foreach (var kv in cols) map[kv.Key] = kv.Value;
        return new WindowColumns(map);
    }

    public int Count => _cols.Count;

    public IReadOnlyCollection<string> Names => _cols.Keys;

    public long WordCount
    {
        get { long n = 0; foreach (var kv in _cols) n += kv.Value.LongLength; return n; }
    }

    public uint[] this[string word] =>
        _cols.TryGetValue(word, out var col) ? col
        : throw new KeyNotFoundException("その窓にその語の列がありません: " + word);

    public bool Has(string word) => _cols.ContainsKey(word);

    public WindowColumns Slice(int from)
    {
        if (from <= 0) return this;
        var map = new Dictionary<string, uint[]>(_cols.Count, StringComparer.Ordinal);
        foreach (var kv in _cols)
        {
            int keep = Math.Max(0, kv.Value.Length - from);
            var buf = new uint[keep];
            if (keep > 0) Array.Copy(kv.Value, from, buf, 0, keep);
            map[kv.Key] = buf;
        }
        return new WindowColumns(map);
    }

    public IEnumerator<KeyValuePair<string, uint[]>> GetEnumerator() => _cols.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
