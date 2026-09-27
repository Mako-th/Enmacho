using System.Globalization;
using System.Runtime.CompilerServices;

namespace TH09.Analysis;

public sealed class SpiritSize
{
    private readonly double _v;

    internal SpiritSize(string shape, double v)
    {
        Shape = shape;
        Meaning = SpiritField.MeaningOf(shape);
        _v = v;
    }

    public string Shape { get; }

    public SpiritSizeMeaning Meaning { get; }

    public double Radius => Only(SpiritSizeMeaning.Radius);

    public double HalfWidth => Only(SpiritSizeMeaning.HalfWidth);

    public double FanAngleFull => Only(SpiritSizeMeaning.FanAngleFull);

    public double FanAngleHalf => FanAngleFull / 2.0;

    public double ConeWidthFactor => Only(SpiritSizeMeaning.ConeWidthFactor);

    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0}({1}={2})", Shape, Meaning, _v);

    private double Only(SpiritSizeMeaning want) =>
        Meaning == want ? _v
        : throw new InvalidOperationException(
            $"{Shape} の大きさは {Meaning} であって {want} ではない（形ごとに意味が違う）");
}

public enum SpiritSizeMeaning
{
    Radius,
    HalfWidth,
    FanAngleFull,
    ConeWidthFactor,
}

public sealed class SpiritShape
{
    private readonly Dictionary<string, double> _params;
    private readonly SpiritSize[] _ramp;

    internal SpiritShape(int charid, string name, string? aim, int delay, double? angleInitial,
                         Dictionary<string, double> pars, double[] ramp, IReadOnlyList<string> drawKeys)
    {
        Char = charid; Name = name; Aim = aim; Delay = delay; AngleInitial = angleInitial;
        _params = pars;
        _ramp = new SpiritSize[ramp.Length];
        for (int i = 0; i < ramp.Length; i++) _ramp[i] = new SpiritSize(name, ramp[i]);
        DrawKeys = drawKeys;
    }

    public int Char { get; }

    public string Name { get; }

    public string? Aim { get; }

    public int Delay { get; }

    public double? AngleInitial { get; }

    public int Steps => _ramp.Length - 1;

    public SpiritSize Final => _ramp[^1];

    public IReadOnlyList<string> DrawKeys { get; }

    public double Param(string key) =>
        _params.TryGetValue(key, out var v) ? v
        : throw new KeyNotFoundException(
            $"charid {Char}（{Name}）の吸霊範囲の表にその鍵はありません: {key}");

    internal SpiritSize AtStep(int step) => _ramp[step];
}

public sealed class SpiritTick
{
    public required SpiritShape Shape { get; init; }

    public required int Frame { get; init; }

    public required double X { get; init; }

    public required double Y { get; init; }

    public required SpiritSize Size { get; init; }

    public required double? Angle { get; init; }

    public required IReadOnlyList<double> Angles { get; init; }

    public required bool AngleKnown { get; init; }

    public required string Label { get; init; }
}

public static class SpiritField
{

    private static readonly Dictionary<string, string[]> DrawKeysByShape = BuildDrawKeys();
    private static readonly Dictionary<int, SpiritShape> ByChar = BuildByChar();
    private static readonly Dictionary<(int Char, int Sign), (double Target, double Rate)> AimVxTable =
        BuildAimVx();

    private static Dictionary<string, string[]> BuildDrawKeys()
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var line in Packed.Lines(AnalysisTables.SpiritDrawKeysPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2)
                throw new InvalidDataException($"SPIRIT_DRAW_KEYS の行が 2 列でない: {line}");
            map[f[0]] = f[1].Length == 0 ? [] : f[1].Split(',');
        }
        return map;
    }

    private static Dictionary<int, SpiritShape> BuildByChar()
    {
        var ramps = new Dictionary<int, double[]>();
        foreach (var line in Packed.Lines(AnalysisTables.SpiritRampPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2)
                throw new InvalidDataException($"吸霊の育ちの行が 2 列でない: {line}");
            var vs = f[1].Split(' ');
            var ramp = new double[vs.Length];
            for (int i = 0; i < vs.Length; i++) ramp[i] = D(vs[i]);
            ramps[I(f[0])] = ramp;
        }

        var pars = new Dictionary<int, Dictionary<string, double>>();
        foreach (var line in Packed.Lines(AnalysisTables.SpiritParamsPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 3)
                throw new InvalidDataException($"吸霊の補助値の行が 3 列でない: {line}");
            int c = I(f[0]);
            if (!pars.TryGetValue(c, out var box)) pars[c] = box = new Dictionary<string, double>(StringComparer.Ordinal);
            box[f[1]] = D(f[2]);
        }

        var map = new Dictionary<int, SpiritShape>();
        foreach (var line in Packed.Lines(AnalysisTables.SpiritFieldPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 5)
                throw new InvalidDataException($"SPIRIT_FIELD の行が 5 列でない: {line}");
            int c = I(f[0]);
            var shape = f[1];
            if (!ramps.TryGetValue(c, out var ramp))
                throw new InvalidDataException($"charid {c} の育ちの並びが表に無い（生成物が食い違っている）");
            if (!pars.TryGetValue(c, out var box))
                throw new InvalidDataException($"charid {c} の補助値が表に無い（生成物が食い違っている）");
            if (!DrawKeysByShape.TryGetValue(shape, out var keys))
                throw new InvalidDataException($"描き方の分からない形: {shape}");
            map[c] = new SpiritShape(c, shape, f[2].Length == 0 ? null : f[2], I(f[3]),
                                     f[4].Length == 0 ? null : D(f[4]), box, ramp, keys);
        }
        return map;
    }

    private static Dictionary<(int, int), (double, double)> BuildAimVx()
    {
        var map = new Dictionary<(int, int), (double, double)>();
        foreach (var line in Packed.Lines(AnalysisTables.SpiritAimVxPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 4)
                throw new InvalidDataException($"aim_vx の行が 4 列でない: {line}");
            map[(I(f[0]), I(f[1]))] = (D(f[2]), D(f[3]));
        }
        return map;
    }

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    public static IReadOnlyCollection<int> Chars => ByChar.Keys;

    public static string FieldJa => AnalysisTables.SpiritFieldJa;


    public static SpiritShape? Of(int? charid) =>
        charid is not null && ByChar.TryGetValue(charid.Value, out var sh) ? sh : null;

    internal static SpiritSizeMeaning MeaningOf(string shape) => shape switch
    {
        AnalysisTables.SpiritShapeCircle or AnalysisTables.SpiritShapeCircleUp
            or AnalysisTables.SpiritShapeLens or AnalysisTables.SpiritShapeFlower
            or AnalysisTables.SpiritShapeStar => SpiritSizeMeaning.Radius,
        AnalysisTables.SpiritShapeBandV or AnalysisTables.SpiritShapeBandH
            or AnalysisTables.SpiritShapeCross => SpiritSizeMeaning.HalfWidth,
        AnalysisTables.SpiritShapeFan => SpiritSizeMeaning.FanAngleFull,
        AnalysisTables.SpiritShapeConeUp => SpiritSizeMeaning.ConeWidthFactor,
        _ => throw new InvalidDataException(
            $"この形の大きさが何を指すか決めていない: {shape}（形を足したらここも足す）"),
    };


    public static SpiritSize? Size(int? charid, int? frame)
    {
        var sh = Of(charid);
        if (sh is null || frame is null) return null;
        int n = frame.Value;
        if (n < 1) return null;
        int k = n - sh.Delay;
        if (k < 0) k = 0;
        else if (k > sh.Steps) k = sh.Steps;
        return sh.AtStep(k);
    }


    public static (double X, double Y)? Center(int? charid, double? x, double? y, SpiritSize? size)
    {
        var sh = Of(charid);
        if (sh is null || x is null || y is null) return null;
        if (size is not null && !string.Equals(size.Shape, sh.Name, StringComparison.Ordinal))
            throw new ArgumentException($"{sh.Name} に {size.Shape} の大きさを渡している");
        if (!string.Equals(sh.Name, AnalysisTables.SpiritShapeCircleUp, StringComparison.Ordinal))
            return (x.Value, y.Value);
        if (size is null) return null;
        return (x.Value, y.Value - sh.Param("offset") * size.Radius);
    }


    public static double? AngleInit(int? charid, double? moveAngle)
    {
        var sh = Of(charid);
        if (sh?.Aim is null) return null;
        if (string.Equals(sh.Aim, AnalysisTables.SpiritAimMoveBack, StringComparison.Ordinal)
            && sh.AngleInitial is null)
            return moveAngle is null ? null : NormAngle(moveAngle.Value + Math.PI);
        return sh.AngleInitial;
    }

    public static double? AngleStep(int? charid, double? angle, double? moveAngle, int? vxSign)
    {
        var sh = Of(charid);
        if (sh?.Aim is null) return null;
        if (string.Equals(sh.Aim, AnalysisTables.SpiritAimFixed, StringComparison.Ordinal))
            return sh.AngleInitial;
        if (angle is null) return null;
        if (string.Equals(sh.Aim, AnalysisTables.SpiritAimSpin, StringComparison.Ordinal))
            return NormAngle(angle.Value + sh.Param("spin"));
        if (string.Equals(sh.Aim, AnalysisTables.SpiritAimMoveBack, StringComparison.Ordinal))
        {
            if (moveAngle is null) return null;
            double target = NormAngle(moveAngle.Value + Math.PI);
            double d = target - angle.Value;
            if (target > angle.Value)
            {
                if (d > Math.PI) d -= 2 * Math.PI;
            }
            else if (d < -Math.PI) d += 2 * Math.PI;
            return NormAngle(angle.Value + sh.Param("lerp") * d);
        }
        if (string.Equals(sh.Aim, AnalysisTables.SpiritAimVx, StringComparison.Ordinal))
        {
            if (vxSign is null) return null;
            if (!AimVxTable.TryGetValue((sh.Char, vxSign.Value), out var t)) return null;
            return angle.Value + t.Rate * (t.Target - angle.Value);
        }
        return null;
    }

    public static double[] SweepOptions()
    {
        double lo = AnalysisTables.MoveAngleHitLo, hi = AnalysisTables.MoveAngleHitHi;
        int n = AnalysisTables.SpiritSweepSteps;
        var outv = new double[n];
        for (int j = 0; j < n; j++) outv[j] = lo + (hi - lo) * j / n;
        return outv;
    }

    public static string? Label(int? charid, double? angle)
    {
        var sh = Of(charid);
        if (sh is null) return null;
        return sh.Aim is not null && angle is null
            ? AnalysisTables.SpiritFieldJa + AnalysisTables.SpiritAngleUnknownSuffix
            : AnalysisTables.SpiritFieldJa;
    }

    private static double NormAngle(double a)
    {
        for (int k = 0; k < 17 && a > Math.PI; k++) a -= 2 * Math.PI;
        for (int k = 0; k < 17 && a < -Math.PI; k++) a += 2 * Math.PI;
        return a;
    }


    private const uint SlowBit = AnalysisTables.InputReplaySlow;

    private const int StateDown = AnalysisTables.PlayerStateDown;

    private const int RoundHead = AnalysisTables.RoundHeadFrames;

    private static readonly double[] NoOptions = [];

    private static readonly HashSet<int> SkipStates = BuildSkipStates();

    private static readonly Dictionary<int, double> DirAngle = BuildIntMap(AnalysisTables.MoveDirAnglePacked);

    private static readonly Dictionary<int, int> DirVxSign = BuildVxSign();

    private static readonly double[] AngleOptions = BuildAngleOptions();

    private static readonly double[] SweepShared = SweepOptions();

    private static readonly double[] EdgeOptions = [.. AngleOptions, .. SweepShared];

    private static HashSet<int> BuildSkipStates()
    {
        var set = new HashSet<int>();
        foreach (var line in Packed.Lines(AnalysisTables.MoveSkipStatesPacked)) set.Add(I(line));
        return set;
    }

    private static Dictionary<int, double> BuildIntMap(string packed)
    {
        var map = new Dictionary<int, double>();
        foreach (var line in Packed.Lines(packed))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException($"方向の表の行が 2 列でない: {line}");
            map[I(f[0])] = D(f[1]);
        }
        return map;
    }

    private static Dictionary<int, int> BuildVxSign()
    {
        var map = new Dictionary<int, int>();
        foreach (var line in Packed.Lines(AnalysisTables.MoveDirVxSignPacked))
        {
            var f = line.Split('\t');
            if (f.Length != 2) throw new InvalidDataException($"x 速度の符号の行が 2 列でない: {line}");
            map[I(f[0])] = I(f[1]);
        }
        return map;
    }

    private static double[] BuildAngleOptions()
    {
        var lines = Packed.Lines(AnalysisTables.MoveAngleOptionsPacked);
        var outv = new double[lines.Length];
        for (int i = 0; i < lines.Length; i++) outv[i] = D(lines[i]);
        return outv;
    }

    private static uint Bits(double v) => unchecked((uint)(long)v);

    internal static bool SlowHeld(double? inputReplay) =>
        inputReplay is not null && (Bits(inputReplay.Value) & SlowBit) != 0;

    internal static int? MoveDirCode(double? inputReplay)
    {
        if (inputReplay is null) return null;
        uint v = Bits(inputReplay.Value);
        const uint up = AnalysisTables.InputReplayUp, down = AnalysisTables.InputReplayDown;
        const uint left = AnalysisTables.InputReplayLeft, right = AnalysisTables.InputReplayRight;
        if ((v & (up | left)) == (up | left)) return 5;
        if ((v & (down | left)) == (down | left)) return 7;
        if ((v & (up | right)) == (up | right)) return 6;
        if ((v & (down | right)) == (down | right)) return 8;
        if ((v & down) != 0) return 2;
        if ((v & up) != 0) return 1;
        if ((v & left) != 0) return 3;
        if ((v & right) != 0) return 4;
        return 0;
    }

    internal static double? MoveDirAngleOf(int? code) =>
        code is not null && DirAngle.TryGetValue(code.Value, out var a) ? a : null;

    internal static int? MoveVxSign(int? code) =>
        code is not null && DirVxSign.TryGetValue(code.Value, out var s) ? s : null;

    internal static (double?[] Angles, double[][] Options) RestoreMoveAngles(
        double?[] inputReplay, double?[] playerState, bool roundHead)
    {
        int n = inputReplay.Length;
        var angles = new double?[n];
        var opts = new double[n][];
        double? angle = roundHead ? AnalysisTables.MoveAngleInit : null;
        double[] cur = roundHead ? NoOptions : AngleOptions;
        for (int k = 0; k < n; k++)
        {
            var state = Get(playerState, k);
            if (state is not null && (int)state.Value == StateDown)
            {
                angle = null; cur = SweepShared;
            }
            else if (state is not null && !SkipStates.Contains((int)state.Value))
            {
                var a = MoveDirAngleOf(MoveDirCode(Get(inputReplay, k)));
                if (a is not null) { angle = a; cur = NoOptions; }
            }
            angles[k] = angle; opts[k] = cur;
        }
        return (angles, opts);
    }


    internal static SpiritTick?[] TrackCore(double?[] inputReplay, double?[] playerState, double?[] character,
                                            double?[] posX, double?[] posY,
                                            double?[] moveAngles, double[][] options,
                                            double?[]? p1GameFlags = null, double?[]? globalState = null)
    {
        int n = inputReplay.Length;
        var outv = new SpiritTick?[n];
        bool alive = false;
        int frame = 0;
        double? angle = null;
        double?[]? cand = null;
        double[] sweep = NoOptions;
        for (int k = 0; k < n; k++)
        {
            var state = Get(playerState, k);
            bool born = false;
            if (state is not null && (int)state.Value == StateDown) alive = false;
            else if (state is not null && !SkipStates.Contains((int)state.Value))
            {
                if (inputReplay[k] is null) alive = false;
                else if (!SlowHeld(inputReplay[k])) alive = false;
                else if (!alive) { alive = true; born = true; }
            }
            if (!alive)
            {
                outv[k] = null; frame = 0; angle = null; cand = null; sweep = NoOptions;
                continue;
            }
            bool frozenTick = FrozenTick(GetOrNull(p1GameFlags, k), GetOrNull(globalState, k));
            if (!frozenTick) frame++;
            var sh = Of(ToInt(Get(character, k)));
            if (sh is null) { outv[k] = null; continue; }
            double? here = Get(moveAngles, k);
            int? vx = MoveVxSign(MoveDirCode(inputReplay[k]));
            if (born)
            {
                double? prev = k > 0 ? Get(moveAngles, k - 1) : null;
                angle = AngleInit(sh.Char, prev);
                var o = k < options.Length ? options[k] : NoOptions;
                sweep = o.Length > 0 ? o
                      : (k > 0 && k - 1 < options.Length ? options[k - 1] : EdgeOptions);
                if (string.Equals(sh.Aim, AnalysisTables.SpiritAimMoveBack, StringComparison.Ordinal)
                    && sweep.Length > 0)
                {
                    cand = new double?[sweep.Length];
                    for (int j = 0; j < sweep.Length; j++) cand[j] = AngleInit(sh.Char, prev ?? sweep[j]);
                }
                else cand = null;
            }
            if (!frozenTick)
            {
                angle = AngleStep(sh.Char, angle, here, vx);
                if (cand is not null)
                    for (int j = 0; j < cand.Length; j++)
                        cand[j] = AngleStep(sh.Char, cand[j], here ?? sweep[j], vx);
            }

            var size = Size(sh.Char, frame);
            var xy = Center(sh.Char, Get(posX, k), Get(posY, k), size);
            if (size is null || xy is null) { outv[k] = null; continue; }
            var angles = new List<double>();
            if (angle is not null) angles.Add(angle.Value);
            else if (cand is not null)
                foreach (var a in cand) if (a is not null) angles.Add(a.Value);
            outv[k] = new SpiritTick
            {
                Shape = sh,
                Frame = frame,
                X = xy.Value.X,
                Y = xy.Value.Y,
                Size = size,
                Angle = angle,
                Angles = angles,
                AngleKnown = angle is not null || sh.Aim is null,
                Label = Label(sh.Char, angle)!,
            };
        }
        return outv;
    }


    private static readonly string[] WordSuffixes =
        ["input_replay", "player_state", "character", "pos_x", "pos_y"];

    private static string Word(int side, string suffix) => "p" + side + "_" + suffix;

    public static bool HasWords(Window w)
    {
        foreach (int side in new[] { 1, 2 })
            foreach (var s in WordSuffixes)
                if (!w.Main.Has(Word(side, s))) return false;
        return true;
    }

    public static bool HasRecordedMoveAngle(Window w) =>
        w.Main.Has(Word(1, "move_dir_angle")) && w.Main.Has(Word(2, "move_dir_angle"));

    public static SpiritTick?[]? Track(Window w, int side) => Scan.For(w).Runs(side);

    public static SpiritTick? At(Window w, int i, int side)
    {
        if (i < 0 || i >= w.TickCount) return null;
        var runs = Track(w, side);
        if (runs is null) return null;
        int k = w.ExtIndex(i);
        return (uint)k < (uint)runs.Length ? runs[k] : null;
    }

    private interface IExtSource
    {
        double?[]? ExtSeries(string name);
    }

    private sealed class WindowExt : IExtSource
    {
        private readonly Window _w;
        public WindowExt(Window w) => _w = w;
        public double?[]? ExtSeries(string name) => _w.ExtSeries(name);
    }

    private sealed class FakeExt : IExtSource
    {
        private readonly Dictionary<string, double?[]> _cols = new(StringComparer.Ordinal);
        public double?[]? ExtSeries(string name) => _cols.TryGetValue(name, out var v) ? v : null;
        public FakeExt With(string name, double?[] col) { _cols[name] = col; return this; }
    }

    private sealed class Scan
    {
        private static readonly ConditionalWeakTable<Window, Scan> Cached = new();

        public static Scan For(Window w) => Cached.GetValue(w, static x => new Scan(new WindowExt(x)));

        private readonly IExtSource _src;
        private readonly Dictionary<int, SpiritTick?[]?> _runs = [];
        private readonly Dictionary<int, (double?[] Angles, double[][] Options)?> _move = [];
        private (double?[]? P1GameFlags, double?[]? GlobalState)? _freezeWords;

        internal Scan(IExtSource src) => _src = src;

        public SpiritTick?[]? Runs(int side)
        {
            if (_runs.TryGetValue(side, out var cached)) return cached;
            SpiritTick?[]? outv = null;
            var ir = _src.ExtSeries(Word(side, "input_replay"));
            var st = _src.ExtSeries(Word(side, "player_state"));
            var ch = _src.ExtSeries(Word(side, "character"));
            var px = _src.ExtSeries(Word(side, "pos_x"));
            var py = _src.ExtSeries(Word(side, "pos_y"));
            var mv = MoveAngles(side);
            var (p1gf, gs) = FreezeWords();
            if (ir is not null && st is not null && ch is not null
                && px is not null && py is not null && mv is not null)
                outv = TrackCore(ir, st, ch, px, py, mv.Value.Angles, mv.Value.Options, p1gf, gs);
            _runs[side] = outv;
            return outv;
        }

        private (double?[]? P1GameFlags, double?[]? GlobalState) FreezeWords()
        {
            if (_freezeWords is not null) return _freezeWords.Value;
            var got = (_src.ExtSeries(Word(1, "game_flags")), _src.ExtSeries("global_state"));
            _freezeWords = got;
            return got;
        }

        private (double?[] Angles, double[][] Options)? MoveAngles(int side)
        {
            if (_move.TryGetValue(side, out var cached)) return cached;
            (double?[] Angles, double[][] Options)? got;
            var rec = _src.ExtSeries(Word(side, "move_dir_angle"));
            if (rec is not null)
            {
                var opts = new double[rec.Length][];
                Array.Fill(opts, NoOptions);
                got = (rec, opts);
            }
            else
            {
                var ir = _src.ExtSeries(Word(side, "input_replay"));
                var st = _src.ExtSeries(Word(side, "player_state"));
                if (ir is null || st is null) got = null;
                else
                {
                    var rf = _src.ExtSeries("round_frames");
                    var first = rf is null || rf.Length == 0 ? null : rf[0];
                    got = RestoreMoveAngles(ir, st, first is not null && (int)first.Value <= RoundHead);
                }
            }
            _move[side] = got;
            return got;
        }
    }

    private static double? Get(double?[] col, int k) => (uint)k < (uint)col.Length ? col[k] : null;

    private static double? GetOrNull(double?[]? col, int k) =>
        col is not null && (uint)k < (uint)col.Length ? col[k] : null;

    private static int? ToInt(double? v) => v is null ? null : (int)v.Value;

    private static bool FrozenTick(double? p1GameFlags, double? globalState)
    {
        if (globalState is not null
            && (Bits(globalState.Value) & (AnalysisTables.GsFreezeP1 | AnalysisTables.GsFreezeP2)) != 0)
            return true;
        return p1GameFlags is not null && (Bits(p1GameFlags.Value) & AnalysisTables.GfTimeStop) != 0;
    }


    public static IEnumerable<(string Label, Func<string?> Body)> SelfTestCases()
    {
        yield return ("吸霊の表が空でない（母数）", () =>
            ByChar.Count > 0 && DrawKeysByShape.Count > 0 && AimVxTable.Count > 0
                ? null
                : $"キャラ {ByChar.Count} / 形 {DrawKeysByShape.Count} / aim_vx {AimVxTable.Count}");

        yield return ("全キャラで形・育ち・補助値がそろっている", () =>
        {
            foreach (var c in ByChar.Keys.OrderBy(k => k))
            {
                var sh = Of(c);
                if (sh is null) return $"charid {c} が引けない";
                if (sh.Steps < 0) return $"charid {c} の育ちが空";
                foreach (var k in new[] { "r0", "cap", "dr" })
                    try { _ = sh.Param(k); }
                    catch (KeyNotFoundException) { return $"charid {c} に {k} が無い"; }
            }
            return null;
        });

        yield return ("★描くのに要る鍵が、その形のキャラ全員にある", () =>
        {
            int checkedKeys = 0;
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
                foreach (var k in sh.DrawKeys)
                {
                    try { _ = sh.Param(k); checkedKeys++; }
                    catch (KeyNotFoundException) { return $"charid {sh.Char}（{sh.Name}）に {k} が無い"; }
                }
            return checkedKeys > 0 ? null : "引いた鍵が 0 個（この検査は何も見ていない）";
        });

        yield return ("★どの形も『大きさが何を指すか』が決まっている", () =>
        {
            foreach (var shape in DrawKeysByShape.Keys.OrderBy(s => s, StringComparer.Ordinal))
                try { _ = MeaningOf(shape); }
                catch (InvalidDataException) { return $"意味の決まっていない形: {shape}"; }
            var kinds = DrawKeysByShape.Keys.Select(MeaningOf).Distinct().Count();
            if (kinds <= 1) return $"意味が {kinds} 通りしか無い";
            foreach (var (shape, keys) in DrawKeysByShape)
                if (keys.Contains("radius", StringComparer.Ordinal)
                    && MeaningOf(shape) == SpiritSizeMeaning.Radius)
                    return $"{shape} は半径を別に持っているのに、育つ語まで半径と読んでいる";
            return null;
        });

        yield return ("★表に無いキャラは null（0 に倒さない）", () =>
        {
            int alien = Enumerable.Range(0, 4096).First(k => !ByChar.ContainsKey(k));
            if (Of(alien) is not null) return $"charid {alien} に形を返した";
            if (Size(alien, 1) is not null) return $"charid {alien} に大きさを返した";
            if (Label(alien, 0.0) is not null) return $"charid {alien} に名前を返した";
            if (Center(alien, 0, 0, null) is not null) return $"charid {alien} に中心を返した";
            if (Of(null) is not null || Size(null, 1) is not null || Label(null, 0.0) is not null)
                return "charid が null のときに答えを返した";
            return null;
        });

        yield return ("★frame < 1 は null。★0 は答えとして正しい（縮む形がある）", () =>
        {
            var c = ByChar.Keys.OrderBy(k => k).First();
            if (Size(c, 0) is not null || Size(c, -5) is not null || Size(c, null) is not null)
                return "frame < 1 に答えを返した";
            var shrink = ByChar.Values.Where(s => Math.Abs(Value(s.Final)) < 1e-12).ToList();
            if (shrink.Count == 0) return "0 まで縮むキャラが 1 人も居ない（この検査は何も見ていない）";
            foreach (var sh in shrink)
                if (Size(sh.Char, sh.Steps + sh.Delay) is null) return $"charid {sh.Char} の 0 が null になった";
            return null;
        });

        yield return ("★大きさは意味を取り違えると落ちる（0 を返さない）", () =>
        {
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                var size = Size(sh.Char, 1 + sh.Delay);
                if (size is null) return $"charid {sh.Char} の大きさが引けない";
                foreach (SpiritSizeMeaning m in Enum.GetValues<SpiritSizeMeaning>())
                {
                    if (m == size.Meaning) continue;
                    try { _ = Read(size, m); return $"charid {sh.Char} が {m} として読めてしまった"; }
                    catch (InvalidOperationException) { }
                }
            }
            return null;
        });

        yield return ("★位相: 立った最初のレコードで 1 回ぶん育っている", () =>
        {
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                if (sh.Steps < 1) continue;
                var first = Size(sh.Char, 1 + sh.Delay);
                if (first is null) return $"charid {sh.Char} の 1 枚目が引けない";
                if (!Near(Value(first), Value(sh.AtStep(1))))
                    return $"charid {sh.Char} の 1 枚目が 0 段目のまま（位相が 1 つ遅い）";
                if (sh.Delay > 0 && !Near(Value(Size(sh.Char, sh.Delay)!), Value(sh.AtStep(0))))
                    return $"charid {sh.Char} が待機中に育っている";
            }
            return null;
        });

        yield return ("★育ちは表の並びそのまま（全段）。伸び切った後は同じ値が続く", () =>
        {
            int compared = 0;
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                for (int k = 0; k <= sh.Steps; k++)
                {
                    var got = Size(sh.Char, k + sh.Delay < 1 ? 1 : k + sh.Delay);
                    if (got is null) return $"charid {sh.Char} の {k} 段目が引けない";
                    if (k >= 1 && !Near(Value(got), Value(sh.AtStep(k))))
                        return $"charid {sh.Char} の {k} 段目が表と違う: {Value(got)} ≠ {Value(sh.AtStep(k))}";
                    compared++;
                }
                foreach (int extra in new[] { 1, 60, 6000 })
                {
                    var late = Size(sh.Char, sh.Steps + sh.Delay + extra);
                    if (late is null) return $"charid {sh.Char} が {extra} F 後に消えた（寿命を作っている）";
                    if (!Near(Value(late), Value(sh.Final)))
                        return $"charid {sh.Char} が伸び切った後も動いた";
                }
            }
            return compared > 0 ? null : "1 段も比べていない";
        });

        yield return ("★否定: 旧（押した瞬間を 0 とする位相）なら伸び切りが 1 F 遅れる", () =>
        {
            int differ = 0, sized = 0;
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                if (sh.Steps < 1) continue;
                sized++;
                int last = sh.Steps + sh.Delay;
                int k = Math.Clamp(last - sh.Delay - 1, 0, sh.Steps);
                if (!Near(Value(sh.AtStep(k)), Value(Size(sh.Char, last)!))) differ++;
            }
            if (sized == 0) return "育つキャラが 1 人も居ない（この検査は何も見ていない）";
            return differ == sized ? null
                : $"旧と同じ答えになるキャラが {sized - differ} 人（位相の違いが見えていない）";
        });

        yield return ("★★否定: 閉じた式（r0 + dr*n を上限で止める）は表と食い違う", () =>
        {
            var differ = new List<int>();
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                double r0 = sh.Param("r0"), dr = sh.Param("dr"), cap = sh.Param("cap");
                for (int n = 1; n <= sh.Steps; n++)
                {
                    double legacy = dr > 0 ? Math.Min(cap, r0 + dr * n) : Math.Max(cap, r0 + dr * n);
                    if (!Near(legacy, Value(Size(sh.Char, n + sh.Delay)!))) { differ.Add(sh.Char); break; }
                }
            }
            if (differ.Count == 0)
                return "閉じた式が全キャラで一致した（否定になっていない ——表か写しがおかしい）";
            var over = ByChar.Values.Where(s => !Near(Value(s.Final), s.Param("cap")))
                                    .Select(s => s.Char).OrderBy(k => k).ToList();
            if (over.Count == 0) return "伸び切りが上限と一致しないキャラが居ない（表がおかしい）";
            return over.All(differ.Contains) ? null
                : $"上限をまたぐ {Show(over)} のうち、閉じた式で食い違わないものがある（{Show(differ)}）";
        });

        yield return ("★★否定: float64 で積むと、伸び切るまでの F がずれる", () =>
        {
            var differ = new List<int>();
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                double r0 = sh.Param("r0"), dr = sh.Param("dr"), cap = sh.Param("cap");
                double v = r0;
                int n = 0;
                while (n < 4096 && ((dr > 0 && v < cap) || (dr < 0 && v > cap))) { v += dr; n++; }
                if (n != sh.Steps) differ.Add(sh.Char);
            }
            return differ.Count > 0 ? null
                : "float64 で積んでも段数が全キャラで一致した（否定になっていない）";
        });

        yield return ("★中心は自機座標そのもの（上へずれる形だけが例外）", () =>
        {
            var moved = new List<int>();
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                var size = Size(sh.Char, 1 + sh.Delay);
                var got = Center(sh.Char, 10.0, 100.0, size);
                if (got is null) return $"charid {sh.Char} の中心が引けない";
                if (!Near(got.Value.X, 10.0)) return $"charid {sh.Char} の x がずれた";
                if (!Near(got.Value.Y, 100.0)) moved.Add(sh.Char);
            }
            if (moved.Count == 0) return "中心がずれるキャラが 1 人も居ない（この検査は何も見ていない）";
            foreach (var c in moved)
                if (!string.Equals(Of(c)!.Name, AnalysisTables.SpiritShapeCircleUp, StringComparison.Ordinal))
                    return $"charid {c}（{Of(c)!.Name}）の中心がずれた";
            foreach (var c in moved)
                if (Center(c, 10.0, 100.0, Size(c, 1 + Of(c)!.Delay))!.Value.Y >= 100.0)
                    return $"charid {c} が下へずれている（y の向きを取り違えている）";
            return null;
        });

        yield return ("★座標が読めなければ中心も null。★別の形の大きさは受け取らない", () =>
        {
            var up = ByChar.Values.First(s =>
                string.Equals(s.Name, AnalysisTables.SpiritShapeCircleUp, StringComparison.Ordinal));
            if (Center(up.Char, null, 100.0, Size(up.Char, 1)) is not null) return "x が null でも答えた";
            if (Center(up.Char, 10.0, null, Size(up.Char, 1)) is not null) return "y が null でも答えた";
            if (Center(up.Char, 10.0, 100.0, null) is not null) return "大きさが無いのに中心を答えた";
            var other = ByChar.Values.First(s => !string.Equals(s.Name, up.Name, StringComparison.Ordinal));
            try
            {
                Center(up.Char, 10.0, 100.0, Size(other.Char, 1 + other.Delay));
                return "別の形の大きさを黙って受け取った";
            }
            catch (ArgumentException) { return null; }
        });

        yield return ("★向きを持たない形は、生成時も 1 フレーム後も null", () =>
        {
            var plain = ByChar.Values.Where(s => s.Aim is null).OrderBy(s => s.Char).ToList();
            if (plain.Count == 0) return "向きを持たない形が 1 つも無い（この検査は何も見ていない）";
            foreach (var sh in plain)
            {
                if (AngleInit(sh.Char, 0.0) is not null) return $"charid {sh.Char} が生成時の角を返した";
                if (AngleStep(sh.Char, 0.0, 0.0, 1) is not null) return $"charid {sh.Char} が角を進めた";
                if (Label(sh.Char, null) != AnalysisTables.SpiritFieldJa)
                    return $"charid {sh.Char} に『向き不明』の接尾が付いた";
            }
            return null;
        });

        yield return ("★fixed は前の値を見ない（毎フレーム書き直す）", () =>
        {
            var fixedOnes = Aimed(AnalysisTables.SpiritAimFixed);
            if (fixedOnes.Count == 0) return "fixed のキャラが居ない（この検査は何も見ていない）";
            foreach (var sh in fixedOnes)
            {
                if (sh.AngleInitial is null) return $"charid {sh.Char} に生成時の角が無い";
                var got = AngleStep(sh.Char, 999.0, null, null);
                if (got is null || !Near(got.Value, sh.AngleInitial.Value))
                    return $"charid {sh.Char} が前の値に引きずられた: {Show(got)}";
            }
            return null;
        });

        yield return ("★move_back: 移動方向が分からなければ null（初期値で埋めない）", () =>
        {
            var back = Aimed(AnalysisTables.SpiritAimMoveBack);
            if (back.Count == 0) return "move_back のキャラが居ない（この検査は何も見ていない）";
            int fromMove = 0;
            foreach (var sh in back)
            {
                if (AngleStep(sh.Char, 0.0, null, null) is not null)
                    return $"charid {sh.Char} が移動方向なしで角を進めた";
                if (sh.AngleInitial is null)
                {
                    fromMove++;
                    if (AngleInit(sh.Char, null) is not null)
                        return $"charid {sh.Char} が移動方向なしで生成時の角を答えた";
                    var got = AngleInit(sh.Char, 0.0);
                    if (got is null || !Near(got.Value, Math.PI))
                        return $"charid {sh.Char} の生成時の角が逆向きでない: {Show(got)}";
                }
                else if (AngleInit(sh.Char, null) is null)
                    return $"charid {sh.Char} は生成時の角が表にあるのに null を返した";
            }
            return fromMove > 0 ? null : "移動方向から生成時の角を作るキャラが居ない（この検査は薄い）";
        });

        yield return ("★move_back は目標へ寄っていき、行き過ぎない", () =>
        {
            foreach (var sh in Aimed(AnalysisTables.SpiritAimMoveBack))
            {
                double move = 0.0, target = Math.PI;
                double? a = AngleInit(sh.Char, Math.PI);
                if (a is null) a = sh.AngleInitial;
                if (a is null) return $"charid {sh.Char} の初期の角が出ない";
                double prev = Math.Abs(NormAngle(target - a.Value));
                var one = AngleStep(sh.Char, a, move, null);
                if (one is null) return $"charid {sh.Char} が 1 フレームも進まない";
                if (!Near(Math.Abs(NormAngle(target - one.Value)), prev * (1.0 - sh.Param("lerp"))))
                    return $"charid {sh.Char} の寄せ率が表と違う";
                for (int k = 0; k < 200; k++)
                {
                    a = AngleStep(sh.Char, a, move, null);
                    if (a is null) return $"charid {sh.Char} が途中で null になった";
                    double now = Math.Abs(NormAngle(target - a.Value));
                    if (now > prev + 1e-9) return $"charid {sh.Char} が目標から遠ざかった";
                    prev = now;
                }
                if (prev > 1e-3) return $"charid {sh.Char} が 200 F でも目標に寄り切らない（残り {prev}）";
            }
            return null;
        });

        yield return ("★spin は毎フレーム同じだけ回り、畳まれる", () =>
        {
            var spin = Aimed(AnalysisTables.SpiritAimSpin);
            if (spin.Count == 0) return "spin のキャラが居ない（この検査は何も見ていない）";
            foreach (var sh in spin)
            {
                double? a = sh.AngleInitial;
                if (a is null) return $"charid {sh.Char} に生成時の角が無い";
                double step = sh.Param("spin");
                for (int k = 0; k < 1000; k++)
                {
                    var next = AngleStep(sh.Char, a, null, null);
                    if (next is null) return $"charid {sh.Char} が null になった";
                    if (next.Value <= -Math.PI - 1e-9 || next.Value > Math.PI + 1e-9)
                        return $"charid {sh.Char} の角が畳まれていない: {next.Value}";
                    if (!Near(NormAngle(next.Value - a!.Value), NormAngle(step)))
                        return $"charid {sh.Char} の回り方が変わった";
                    a = next;
                }
                return null;
            }
            return null;
        });

        yield return ("★★否定: vx は畳まない（旧のように畳むと星の向きが変わる）", () =>
        {
            var vx = Aimed(AnalysisTables.SpiritAimVx);
            if (vx.Count == 0) return "vx のキャラが居ない（この検査は何も見ていない）";
            foreach (var sh in vx)
            {
                if (AngleStep(sh.Char, 0.0, 0.0, null) is not null)
                    return $"charid {sh.Char} が x 速度の符号なしで角を進めた";
                var got = AngleStep(sh.Char, 10.0, null, 0);
                if (got is null) return $"charid {sh.Char} が答えない";
                if (got.Value <= Math.PI)
                    return $"charid {sh.Char} が畳まれている（{got.Value}）";
                if (Near(NormAngle(got.Value), got.Value)) return "旧と新が同じ答えになった";
                foreach (int sign in new[] { -1, 0, 1 })
                    if (AngleStep(sh.Char, 0.0, null, sign) is null)
                        return $"charid {sh.Char} が符号 {sign} に答えない";
            }
            return null;
        });

        yield return ("★向きを使う形だけ『向き不明』の接尾が付く", () =>
        {
            if (string.IsNullOrEmpty(AnalysisTables.SpiritFieldJa)
                || string.IsNullOrEmpty(AnalysisTables.SpiritAngleUnknownSuffix))
                return "名前か接尾が空（凡例に出せない）";
            int aimed = 0;
            foreach (var sh in ByChar.Values.OrderBy(s => s.Char))
            {
                bool suffixed = Label(sh.Char, null) != AnalysisTables.SpiritFieldJa;
                if (suffixed != (sh.Aim is not null))
                    return $"charid {sh.Char}（aim={sh.Aim ?? "なし"}）の接尾が合わない";
                if (Label(sh.Char, 0.0) != AnalysisTables.SpiritFieldJa)
                    return $"charid {sh.Char} は向きが分かっているのに接尾が付いた";
                if (sh.Aim is not null) aimed++;
            }
            return aimed > 0 ? null : "向きを使うキャラが居ない（この検査は何も見ていない）";
        });

        yield return ("★掃きは表の本数ぶん・[lo, hi) の半開", () =>
        {
            var opts = SweepOptions();
            if (opts.Length != AnalysisTables.SpiritSweepSteps)
                return $"{opts.Length} 本（表は {AnalysisTables.SpiritSweepSteps} 本）";
            if (opts.Length == 0) return "0 本（合併が描けない）";
            if (!Near(opts[0], AnalysisTables.MoveAngleHitLo)) return "始点が範囲の下端でない";
            if (opts[^1] >= AnalysisTables.MoveAngleHitHi) return "上端を含んでいる（半開でない）";
            for (int k = 1; k < opts.Length; k++)
                if (opts[k] <= opts[k - 1]) return "並びが単調でない";
            return null;
        });

        yield return ("入力語と移動方向の表が空でない（母数）", () =>
        {
            if (DirAngle.Count == 0 || DirVxSign.Count == 0 || AngleOptions.Length == 0
                || SkipStates.Count == 0)
                return $"方向 {DirAngle.Count} / 符号 {DirVxSign.Count} /"
                       + $" 候補 {AngleOptions.Length} / 飛ばす状態 {SkipStates.Count}";
            if (DirAngle.ContainsKey(0)) return "角の表にコード 0 が在る（押していないと右が同じになる）";
            if (!DirVxSign.ContainsKey(0)) return "符号の表にコード 0 が無い（vx が 0 と言えない）";
            if (DirVxSign.Count != DirAngle.Count + 1)
                return $"符号 {DirVxSign.Count} と 角 {DirAngle.Count} の差が 1 でない"
                       + "（コード 0 のぶん以外に食い違いがある）";
            if (AngleOptions.Length != DirAngle.Count)
                return $"候補 {AngleOptions.Length} が方向の本数と違う";
            if (EdgeOptions.Length != AngleOptions.Length + SweepShared.Length)
                return "窓の外の候補が「両方の和」になっていない";
            return null;
        });

        yield return ("★★方向コードと角・符号が、押したビットの幾何と合う", () =>
        {
            var bits = new (uint Bit, int Dx, int Dy)[]
            {
                (AnalysisTables.InputReplayUp, 0, -1),
                (AnalysisTables.InputReplayDown, 0, 1),
                (AnalysisTables.InputReplayLeft, -1, 0),
                (AnalysisTables.InputReplayRight, 1, 0),
            };
            int seen = 0;
            var codes = new HashSet<int>();
            for (int mask = 1; mask < 16; mask++)
            {
                uint word = 0; int dx = 0, dy = 0, pressed = 0;
                for (int b = 0; b < bits.Length; b++)
                    if ((mask & (1 << b)) != 0)
                    { word |= bits[b].Bit; dx += bits[b].Dx; dy += bits[b].Dy; pressed++; }
                if (dx == 0 && dy == 0) continue;
                if (pressed > 2) continue;
                var code = MoveDirCode(word);
                if (code is null || code == 0) return $"ビット {word:X} でコードが出ない";
                codes.Add(code.Value);
                var a = MoveDirAngleOf(code);
                if (a is null) return $"コード {code} の角が表に無い";
                if (!Near(NormAngle(a.Value - Math.Atan2(dy, dx)), 0.0))
                    return $"コード {code} の角が幾何と合わない（{a.Value} vs {Math.Atan2(dy, dx)}）";
                var sign = MoveVxSign(code);
                if (sign is null || sign.Value != Math.Sign(dx))
                    return $"コード {code} の x 速度の符号が {Show(sign is null ? null : (double)sign.Value)}";
                seen++;
            }
            if (codes.Count != DirAngle.Count)
                return $"作れた方向が {codes.Count} 通り（表は {DirAngle.Count} 通り）";
            if (MoveVxSign(0) != 0) return "何も押していないのに vx が 0 でない";
            if (MoveDirCode(0.0) != 0) return "何も押していないのにコードが 0 でない";
            if (MoveDirCode(null) is not null) return "読めない語にコードを返した";
            return seen > 0 ? null : "1 通りも見ていない";
        });

        yield return ("★★否定: コード 0 に「右」を割り当てる旧なら、押していないフレームで向きが化ける", () =>
        {
            static double LegacyAngle(int code) =>
                DirAngle.TryGetValue(code, out var a) ? a : 0.0;

            int rightCode = MoveDirCode((double)AnalysisTables.InputReplayRight)!.Value;
            int leftCode = MoveDirCode((double)AnalysisTables.InputReplayLeft)!.Value;
            if (!Near(LegacyAngle(0), DirAngle[rightCode]))
                return "旧の写しが「右」になっていない（否定テストの意味が無い）";

            var ir = new double?[] { AnalysisTables.InputReplayLeft, 0.0, 0.0 };
            var st = new double?[] { 0.0, 0.0, 0.0 };
            var (angles, opts) = RestoreMoveAngles(ir, st, roundHead: false);
            for (int k = 0; k < angles.Length; k++)
            {
                if (angles[k] is null) return $"{k} で角が出ない";
                if (opts[k].Length != 0) return $"{k} で候補が残っている（分かっているのに）";
                if (!Near(angles[k]!.Value, DirAngle[leftCode]))
                    return $"{k} が左のままでない（{angles[k]}）";
            }
            double legacy = DirAngle[leftCode]; int flipped = 0;
            foreach (var v in ir)
            {
                legacy = LegacyAngle(MoveDirCode(v)!.Value);
                if (Near(legacy, DirAngle[rightCode])) flipped++;
            }
            return flipped > 0 ? null : "旧でも化けなかった（否定になっていない）";
        });

        yield return ("★復元の 3 つの規則（押した／押していない／向きが書かれない状態）", () =>
        {
            int upCode = MoveDirCode((double)AnalysisTables.InputReplayUp)!.Value;
            int skip = SkipStates.First(s => s != StateDown);
            var head = RestoreMoveAngles([0.0], [0.0], roundHead: true);
            if (head.Angles[0] is null || !Near(head.Angles[0]!.Value, AnalysisTables.MoveAngleInit))
                return "ラウンドの頭で初期値から始まっていない";
            if (head.Options[0].Length != 0) return "頭なのに候補が残っている";
            var cold = RestoreMoveAngles([0.0], [0.0], roundHead: false);
            if (cold.Angles[0] is not null) return "頭でないのに角を名乗った（-pi/2 で埋めている）";
            if (cold.Options[0].Length != AngleOptions.Length)
                return $"未見の候補が {cold.Options[0].Length} 本（表は {AngleOptions.Length} 本）";
            var got = RestoreMoveAngles(
                [AnalysisTables.InputReplayUp, AnalysisTables.InputReplayUp],
                [skip, 0.0], roundHead: false);
            if (got.Angles[0] is not null) return "飛ばす状態で角が書かれた";
            if (got.Angles[1] is null || !Near(got.Angles[1]!.Value, DirAngle[upCode]))
                return "状態が戻った次のフレームで書かれていない";
            var hit = RestoreMoveAngles(
                [AnalysisTables.InputReplayUp, AnalysisTables.InputReplayUp],
                [0.0, StateDown], roundHead: false);
            if (hit.Angles[1] is not null) return "被弾フレームで角を名乗った（復元できないのに）";
            if (hit.Options[1].Length != SweepShared.Length) return "被弾の候補が掃きになっていない";
            return null;
        });

        yield return ("★低速を押している間だけ出て、枚数は押し始めから数え直す", () =>
        {
            var sh = ByChar.Values.OrderBy(s => s.Char).First();
            double?[] ir = [Slow(), Slow(), Slow(), 0.0, Slow(), Slow()];
            var track = Synth(sh.Char, ir);
            int?[] want = [1, 2, 3, null, 1, 2];
            for (int k = 0; k < ir.Length; k++)
            {
                if (want[k] is null) { if (track[k] is not null) return $"{k} で消えていない"; continue; }
                if (track[k] is null) return $"{k} で出ていない";
                if (track[k]!.Frame != want[k]!.Value)
                    return $"{k} の枚数が {track[k]!.Frame}（{want[k]} のはず）";
            }
            var one = track[2]!;
            if (!Near(Value(one.Size), Value(Size(sh.Char, 3)!))) return "大きさが育ちと合わない";
            if (one.Label.Length == 0) return "名前が空";
            if (!ReferenceEquals(one.Shape, sh)) return "形が別のキャラのものになっている";
            return null;
        });

        yield return ("★★被弾したフレームで消える（低速を押しっぱなしでも）", () =>
        {
            var sh = ByChar.Values.OrderBy(s => s.Char).First();
            double?[] ir = [Slow(), Slow(), Slow(), Slow()];
            double?[] st = [0.0, 0.0, StateDown, 0.0];
            var track = TrackCore(ir, st, Fill(ir.Length, sh.Char), Fill(ir.Length, 10.0),
                                  Fill(ir.Length, 100.0), new double?[ir.Length],
                                  Options(ir.Length));
            if (track[1] is null) return "被弾の前から出ていない";
            if (track[2] is not null) return "被弾したフレームで消えていない";
            if (track[3] is null) return "次のフレームで生まれ直していない";
            if (track[3]!.Frame != 1) return $"生まれ直した枚数が {track[3]!.Frame}（1 のはず）";
            return null;
        });

        yield return ("★★否定: 低速ビットを見ない旧なら、離しても出っぱなしになる", () =>
        {
            var sh = ByChar.Values.OrderBy(s => s.Char).First();
            double?[] ir = [Slow(), 0.0, 0.0];
            static bool LegacySlow(double? v) => v is not null;
            if (!LegacySlow(0.0)) return "旧の写しが落ちている（否定テストの意味が無い）";
            int legacyAlive = ir.Count(LegacySlow);
            var track = Synth(sh.Char, ir);
            int alive = track.Count(t => t is not null);
            if (legacyAlive != ir.Length) return "旧が出っぱなしになっていない";
            return alive == 1 ? null : $"新も {alive} フレーム出している（ビットを見ていない）";
        });

        yield return ("★表に無いキャラ・座標が読めない tick は描かない（0 を作らない）", () =>
        {
            int alien = Enumerable.Range(0, 4096).First(k => !ByChar.ContainsKey(k));
            double?[] ir = [Slow(), Slow()];
            var track = TrackCore(ir, Fill(ir.Length, 0.0), Fill(ir.Length, (double)alien),
                                  Fill(ir.Length, 10.0), Fill(ir.Length, 100.0),
                                  new double?[ir.Length], Options(ir.Length));
            if (track.Any(t => t is not null)) return "表に無いキャラを描いた";
            var sh = ByChar.Values.OrderBy(s => s.Char).First();
            var noPos = TrackCore(ir, Fill(ir.Length, 0.0), Fill(ir.Length, sh.Char),
                                  new double?[ir.Length], Fill(ir.Length, 100.0),
                                  new double?[ir.Length], Options(ir.Length));
            if (noPos.Any(t => t is not null)) return "自機の座標が読めないのに描いた";
            var noWord = TrackCore(new double?[2], Fill(2, 0.0), Fill(2, sh.Char),
                                   Fill(2, 10.0), Fill(2, 100.0), new double?[2], Options(2));
            return noWord.All(t => t is null) ? null : "入力語が読めない tick で「出ている」と言った";
        });

        yield return ("★★向きが分からない回は、候補を全部育てて名前に接尾が付く", () =>
        {
            var back = Aimed(AnalysisTables.SpiritAimMoveBack)
                       .Where(s => s.AngleInitial is null).ToList();
            if (back.Count == 0) return "移動方向から向きを作るキャラが居ない（この検査は薄い）";
            foreach (var sh in back)
            {
                double?[] ir = [Slow(), Slow(), Slow()];
                var opts = new double[ir.Length][];
                Array.Fill(opts, AngleOptions);
                var track = TrackCore(ir, Fill(ir.Length, 0.0), Fill(ir.Length, sh.Char),
                                      Fill(ir.Length, 10.0), Fill(ir.Length, 100.0),
                                      new double?[ir.Length], opts);
                foreach (var t in track)
                {
                    if (t is null) return $"charid {sh.Char} が出ていない";
                    if (t.Angle is not null) return "分からないのに 1 値へ倒した";
                    if (t.AngleKnown) return "分からないのに「分かっている」と言った";
                    if (t.Angles.Count != AngleOptions.Length)
                        return $"候補が {t.Angles.Count} 本（{AngleOptions.Length} 本のはず）";
                    if (!t.Label.EndsWith(AnalysisTables.SpiritAngleUnknownSuffix, StringComparison.Ordinal))
                        return "名前に『向き不明』が付いていない";
                }
                double?[] late = [0.0, Slow(), Slow(), Slow()];
                var known = TrackCore(late, Fill(late.Length, 0.0), Fill(late.Length, sh.Char),
                                      Fill(late.Length, 10.0), Fill(late.Length, 100.0),
                                      Fill(late.Length, 0.0), Options(late.Length));
                if (known[0] is not null) return "低速を押していないのに出ている";
                for (int k = 1; k < known.Length; k++)
                {
                    var t = known[k];
                    if (t is null || t.Angle is null) return "分かっているのに角が出ない";
                    if (!t.AngleKnown || t.Angles.Count != 1) return "候補が 1 本に決まっていない";
                    if (t.Label.EndsWith(AnalysisTables.SpiritAngleUnknownSuffix, StringComparison.Ordinal))
                        return "分かっているのに接尾が付いた";
                }
                var edge = TrackCore([Slow()], Fill(1, 0.0), Fill(1, sh.Char), Fill(1, 10.0),
                                     Fill(1, 100.0), Fill(1, 0.0), Options(1));
                if (edge[0] is null) return "先頭で生まれた回が出ていない";
                if (edge[0]!.AngleKnown) return "1 つ前の角が無いのに「分かっている」と言った";
                if (edge[0]!.Angles.Count != EdgeOptions.Length)
                    return $"窓の外の候補が {edge[0]!.Angles.Count} 本（{EdgeOptions.Length} 本のはず）";
            }
            return null;
        });

        yield return ("★向きを使わない形は、分からなくても『分かっている』（候補は空）", () =>
        {
            var plain = ByChar.Values.Where(s => s.Aim is null).OrderBy(s => s.Char).ToList();
            if (plain.Count == 0) return "向きを持たない形が 1 つも無い";
            foreach (var sh in plain)
            {
                var t = Synth(sh.Char, [Slow()])[0];
                if (t is null) return $"charid {sh.Char} が出ていない";
                if (!t.AngleKnown) return "向きを持たないのに「分からない」と言った";
                if (t.Angles.Count != 0) return "回す候補が無いのに候補が入っている";
                if (t.Label.EndsWith(AnalysisTables.SpiritAngleUnknownSuffix, StringComparison.Ordinal))
                    return "向きを持たないのに接尾が付いた";
            }
            return null;
        });

        yield return ("★読む語の綴りが RECORD_FIELDS に在る（人の目で守らない）", () =>
        {
            var known = new HashSet<string>(Packed.Lines(AnalysisTables.RecordFieldsPacked),
                                            StringComparer.Ordinal);
            if (known.Count == 0) return "語の表が空（引けていない）";
            var want = new List<string> { "round_frames", "global_state", Word(1, "game_flags") };
            foreach (int side in new[] { 1, 2 })
            {
                foreach (var s in WordSuffixes) want.Add(Word(side, s));
                want.Add(Word(side, "move_dir_angle"));
            }
            var missing = want.Where(x => !known.Contains(x)).ToList();
            return missing.Count == 0 ? null : "RECORD_FIELDS に無い語: " + string.Join(",", missing);
        });

        yield return ("★★凍結中は frame が進まない", () =>
        {
            var sh = ByChar.Values.OrderBy(s => s.Char).First();
            double?[] ir = [Slow(), Slow(), Slow(), Slow()];
            double?[] gf1 = [0.0, (double)AnalysisTables.GfTimeStop, (double)AnalysisTables.GfTimeStop, 0.0];
            var track = SynthFrozen(sh.Char, ir, gf1, null);
            int?[] want = [1, 1, 1, 2];
            for (int k = 0; k < ir.Length; k++)
            {
                if (track[k] is null) return $"{k} で出ていない";
                if (track[k]!.Frame != want[k]) return $"{k} の枚数が {track[k]!.Frame}（{want[k]} のはず）";
            }
            return null;
        });

        yield return ("★★凍結が明けたら続きから育つ（リセットされない）", () =>
        {
            var sh = ByChar.Values.OrderBy(s => s.Char).FirstOrDefault(s => s.Steps >= 2);
            if (sh is null) return "育ちの段数が 2 以上のキャラが居ない（この検査は薄い）";
            double?[] ir = [Slow(), Slow(), Slow(), Slow(), Slow()];
            double?[] gf1 = [0.0, (double)AnalysisTables.GfTimeStop, (double)AnalysisTables.GfTimeStop, 0.0, 0.0];
            var track = SynthFrozen(sh.Char, ir, gf1, null);
            int?[] want = [1, 1, 1, 2, 3];
            for (int k = 0; k < ir.Length; k++)
            {
                if (track[k] is null) return $"{k} で出ていない";
                if (track[k]!.Frame != want[k]) return $"{k} の枚数が {track[k]!.Frame}（{want[k]} のはず）";
            }
            return null;
        });

        yield return ("★★凍結中は向きが変わらない（AngleStep をスキップする）", () =>
        {
            var sh = Aimed(AnalysisTables.SpiritAimSpin).FirstOrDefault();
            if (sh is null) return "spin のキャラが居ない（この検査は薄い）";
            double?[] ir = [Slow(), Slow(), Slow()];
            double?[] gs = [0.0, (double)AnalysisTables.GsFreezeP1, 0.0];
            var track = SynthFrozen(sh.Char, ir, null, gs);
            if (track[0]?.Angle is null || track[1]?.Angle is null || track[2]?.Angle is null)
                return "角が出ていない";
            if (!Near(track[0]!.Angle!.Value, track[1]!.Angle!.Value))
                return "凍結中に角が変わった";
            if (Near(track[1]!.Angle!.Value, track[2]!.Angle!.Value))
                return "明けても角が変わらない（凍結が解けていない）";
            return null;
        });

        yield return ("★★否定: 語が無い窓では今までどおり育つ（補正が効いていないことを見る）", () =>
        {
            var sh = ByChar.Values.OrderBy(s => s.Char).First();
            double?[] ir = [Slow(), Slow(), Slow(), Slow()];
            var track = Synth(sh.Char, ir);
            int?[] want = [1, 2, 3, 4];
            for (int k = 0; k < ir.Length; k++)
            {
                if (track[k] is null) return $"{k} で出ていない";
                if (track[k]!.Frame != want[k])
                    return $"{k} の枚数が {track[k]!.Frame}（{want[k]} のはず ——語が無いのに補正が効いた）";
            }
            return null;
        });

        yield return ("★★凍結ゲートは常に 1P の語（2P だけ立っていても凍結扱いにしない）", () =>
        {
            var sh = ByChar.Values.OrderBy(s => s.Char).First();
            const int n = 3;
            FakeExt Build() => new FakeExt()
                .With(Word(1, "input_replay"), Fill(n, Slow()))
                .With(Word(2, "input_replay"), Fill(n, Slow()))
                .With(Word(1, "player_state"), Fill(n, 0.0))
                .With(Word(2, "player_state"), Fill(n, 0.0))
                .With(Word(1, "character"), Fill(n, sh.Char))
                .With(Word(2, "character"), Fill(n, sh.Char))
                .With(Word(1, "pos_x"), Fill(n, 10.0))
                .With(Word(2, "pos_x"), Fill(n, 10.0))
                .With(Word(1, "pos_y"), Fill(n, 100.0))
                .With(Word(2, "pos_y"), Fill(n, 100.0))
                .With(Word(1, "move_dir_angle"), Fill(n, 0.0))
                .With(Word(2, "move_dir_angle"), Fill(n, 0.0))
                .With(Word(1, "game_flags"), Fill(n, 0.0))
                .With(Word(2, "game_flags"), [0.0, (double)AnalysisTables.GfTimeStop, 0.0]);
            var p1 = new Scan(Build()).Runs(1);
            var p2 = new Scan(Build()).Runs(2);
            if (p1 is null || p2 is null) return "組み立てられない（この検査の合成が壊れている）";
            if (p1[2]?.Frame != 3) return $"1P の枚数が {p1[2]?.Frame}（3 のはず ——1P 以外は見ない）";
            if (p2[2]?.Frame != 3)
                return $"2P の枚数が {p2[2]?.Frame}（3 のはず ——2P の語だけでは凍らない。"
                       + "『1P∨2P』か『side なりの語』を採っている）";
            return null;
        });
    }


    private static double Slow() => AnalysisTables.InputReplaySlow;

    private static double?[] Fill(int n, double v)
    {
        var outv = new double?[n];
        Array.Fill(outv, v);
        return outv;
    }

    private static double[][] Options(int n)
    {
        var outv = new double[n][];
        Array.Fill(outv, NoOptions);
        return outv;
    }

    private static SpiritTick?[] Synth(int charid, double?[] inputReplay) =>
        TrackCore(inputReplay, Fill(inputReplay.Length, 0.0), Fill(inputReplay.Length, charid),
                  Fill(inputReplay.Length, 10.0), Fill(inputReplay.Length, 100.0),
                  Fill(inputReplay.Length, 0.0), Options(inputReplay.Length));

    private static SpiritTick?[] SynthFrozen(
        int charid, double?[] inputReplay, double?[]? p1GameFlags, double?[]? globalState) =>
        TrackCore(inputReplay, Fill(inputReplay.Length, 0.0), Fill(inputReplay.Length, charid),
                  Fill(inputReplay.Length, 10.0), Fill(inputReplay.Length, 100.0),
                  Fill(inputReplay.Length, 0.0), Options(inputReplay.Length),
                  p1GameFlags, globalState);

    private static List<SpiritShape> Aimed(string aim) =>
        ByChar.Values.Where(s => string.Equals(s.Aim, aim, StringComparison.Ordinal))
                     .OrderBy(s => s.Char).ToList();

    private static double Value(SpiritSize s) => Read(s, s.Meaning);

    private static double Read(SpiritSize s, SpiritSizeMeaning m) => m switch
    {
        SpiritSizeMeaning.Radius => s.Radius,
        SpiritSizeMeaning.HalfWidth => s.HalfWidth,
        SpiritSizeMeaning.FanAngleFull => s.FanAngleFull,
        SpiritSizeMeaning.ConeWidthFactor => s.ConeWidthFactor,
        _ => throw new InvalidDataException($"知らない意味: {m}"),
    };

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

    private static string Show(double? x) =>
        x is null ? "null" : x.Value.ToString(CultureInfo.InvariantCulture);

    private static string Show(IEnumerable<int> xs) => "{" + string.Join(",", xs) + "}";
}
