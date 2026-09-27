using System.Diagnostics;
using System.Globalization;
using TH09.Generated;
using TH09.TickBus;

namespace TH09.Record;

public sealed class SnapshotUnavailableException(string message) : Exception(message);

public sealed class TickHookLostException(string message) : Exception(message);

public sealed class LiveTickSource : IDisposable
{
    public const string Kind = "tick";

    public const string Label = "tick同期フック";

    public const double QuietNoticeSeconds = 10.0;

    public const uint RequiredFlags = TickReplay.RequiredFlags;

    public sealed record Options(
        int RingRewind = 0,
        int Margin = RingReader.DefaultMargin,
        int InvalidTicks = 30,
        double InvalidSeconds = 0.5,
        double StallSeconds = 3.0);

    private sealed record Sink(string Name, Action<IReadOnlyList<TickRecord>> Feed);

    private readonly TickBusReader _bus;
    private readonly Options _o;
    private readonly Action<string> _log;
    private readonly List<Sink> _sinks = [];
    private bool _disposed;

    private double _lastRecordAt, _quietWarnedAt, _rateAt;
    private long _rateCount;
    private long _invalidRun;
    private double? _invalidSince;

    public LiveTickSource(TickBusReader bus, Options? options = null, Action<string>? log = null)
    {
        _bus = bus;
        _o = options ?? new Options();
        _log = log ?? (static _ => { });
        Ring = new RingReader(bus, _o.Margin) { Log = m => _log("[tick] " + m) };
        var now = MonotonicSeconds();
        _lastRecordAt = now;
        _quietWarnedAt = now;
        _rateAt = now;
    }

    public RingReader Ring { get; }

    public bool AdonisLoaded { get; set; }

    public Func<bool>? GameAlive { get; set; }

    public double Rate { get; private set; }

    public IReadOnlyList<string> SinkNames => [.. _sinks.Select(s => s.Name)];

    public void AddSink(string name, Action<IReadOnlyList<TickRecord>> sink)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(sink);
        if (_sinks.Any(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
            throw new InvalidOperationException("同じ名前の行き先が既に挿さっています: " + name);
        _sinks.Add(new Sink(name, sink));
    }

    public void ClearSinks() => _sinks.Clear();

    public void Start()
    {
        Ring.StartAtHead(_o.RingRewind);
        if (Ring.Rewound > 0)
            _log($"[tick] リングを {Ring.Rewound} tick 巻き戻して読み始めます"
                 + "（読み手を作るまでに書かれたぶんの取り戻し）。");
    }

    public IReadOnlyList<Snapshot> Read()
    {
        var recs = Ring.Poll();
        var now = MonotonicSeconds();
        if (now - _rateAt >= 1.0)
        {
            Rate = _rateCount / (now - _rateAt);
            _rateCount = 0;
            _rateAt = now;
        }
        if (recs.Count == 0)
        {
            CheckAlive(now);
            return [];
        }
        _lastRecordAt = now;
        _rateCount += recs.Count;
        FanOut(recs);
        var wall = NowIso();
        var outList = new List<Snapshot>(recs.Count);
        foreach (var rec in recs)
        {
            if ((rec.At(TickWords.Record.FlagsOffset) & RequiredFlags) != RequiredFlags)
            {
                _invalidRun++;
                _invalidSince ??= now;
                continue;
            }
            _invalidRun = 0;
            _invalidSince = null;
            outList.Add(ToSnapshot(rec, wall, now));
        }
        if (outList.Count > 0) return outList;
        if (_invalidRun >= _o.InvalidTicks && _invalidSince is double since
            && now - since >= _o.InvalidSeconds)
        {
            throw new SnapshotUnavailableException(
                $"players/stats が無効なレコードが {_invalidRun} tick"
                + $"（{(now - since).ToString("F1", CultureInfo.InvariantCulture)} 秒）続いています");
        }
        return [];
    }

    public string Status() =>
        Label + "（" + Rate.ToString("F0", CultureInfo.InvariantCulture) + " tick/秒 / 累計"
        + Ring.ReadRecords.ToString(CultureInfo.InvariantCulture) + "件 / 取りこぼし"
        + Ring.LostRecords.ToString(CultureInfo.InvariantCulture) + "件・千切れ"
        + Ring.TornRecords.ToString(CultureInfo.InvariantCulture) + "件・再同期"
        + Ring.GapEvents.ToString(CultureInfo.InvariantCulture) + "回）";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearSinks();
        _bus.Dispose();
    }


    private void CheckAlive(double now)
    {
        if (now - _lastRecordAt < _o.StallSeconds) return;
        if (GameAlive is { } alive && !alive())
        {
            throw new SnapshotUnavailableException("th09.exe が終了しました");
        }
        var hdr = _bus.ReadHeader();
        if (hdr.HookState == TickWords.HookStates.Running && hdr.LastError == 0)
        {
            if (now - _quietWarnedAt >= QuietNoticeSeconds)
            {
                _quietWarnedAt = now;
                _log($"[tick] {(now - _lastRecordAt).ToString("F0", CultureInfo.InvariantCulture)}"
                     + " 秒 tick が来ていません（フックは RUNNING）。ゲームが停止中とみなして待機します。");
            }
            return;
        }
        _lastRecordAt = now;
        throw new TickHookLostException(
            $"{_o.StallSeconds.ToString("F0", CultureInfo.InvariantCulture)} 秒 write_index が進みません"
            + $"（hook_state={TickWords.HookStates.Text(hdr.HookState)}"
            + $" / last_error={hdr.LastError.ToString(CultureInfo.InvariantCulture)}）。");
    }


    private void FanOut(IReadOnlyList<TickRecord> recs)
    {
        foreach (var sink in _sinks)
        {
            try { sink.Feed(recs); }
            catch (Exception exc)
            {
                _log($"[tick] 行き先 {sink.Name} への書き出しに失敗しました: {exc.Message}");
            }
        }
    }


    private static readonly Dictionary<string, int> _slotOf = BuildSlots();

    private static Dictionary<string, int> BuildSlots()
    {
        var probe = SnapshotMapper.FromRecord(new TickRecord(new uint[TickBusLayout.RecordWordCount]));
        var map = new Dictionary<string, int>(probe.Count, StringComparer.Ordinal);
        for (var i = 0; i < probe.Count; i++) map[probe[i].Name] = i;
        return map;
    }

    private static SnapValue Take(List<(string Name, SnapValue Value)> vals, string name, SnapValue.K kind)
    {
        if (!_slotOf.TryGetValue(name, out var i) || i >= vals.Count
            || !string.Equals(vals[i].Name, name, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException(
                "SnapshotMapper の出力に語がありません（並びが変わった？）: " + name);
        }
        if (vals[i].Value.Kind != kind)
            throw new InvalidDataException($"語 {name} の型が違います（{vals[i].Value.Kind} / 期待 {kind}）");
        return vals[i].Value;
    }

    public static Snapshot FromRecord(TickRecord rec, string wall, double monotonic, bool adonisLoaded = false)
    {
        var vals = SnapshotMapper.FromRecord(rec, adonisLoaded);
        long U(string n) => Take(vals, n, SnapValue.K.Int).IntValue;
        int S(string n) => checked((int)Take(vals, n, SnapValue.K.Int).IntValue);
        double F(string n) => Take(vals, n, SnapValue.K.Float).FloatValue;
        return new Snapshot
        {
            WallTime = wall,
            Monotonic = monotonic,
            Mode = U("mode"),
            Difficulty = U("difficulty"),
            StageIndex = U("stage_index"),
            FieldId = U("field_id"),
            BattleBgmId = U("battle_bgm_id"),
            P1Character = U("p1_character"),
            P2Character = U("p2_character"),
            P1Control = U("p1_control"),
            P2Control = U("p2_control"),
            P1CpuLevel = U("p1_cpu_level"),
            P2CpuLevel = U("p2_cpu_level"),
            RoundFrames = U("round_frames"),
            CompletedRounds = U("completed_rounds"),
            RoundsRequired = U("rounds_required"),
            P1Wins = U("p1_wins"),
            P2Wins = U("p2_wins"),
            ResultState = U("result_state"),
            ResultWinner = U("result_winner"),
            PauseUsed = U("pause_used"),
            P1LifeRaw = U("p1_life_raw"),
            P2LifeRaw = U("p2_life_raw"),
            P1Gauge = F("p1_gauge"),
            P2Gauge = F("p2_gauge"),
            P1Lives = F("p1_lives"),
            P2Lives = F("p2_lives"),
            P1Score = U("p1_score"),
            P2Score = U("p2_score"),
            P1MaxCombo = U("p1_max_combo"),
            P2MaxCombo = U("p2_max_combo"),
            P1SpellPoints = U("p1_spell_points"),
            P2SpellPoints = U("p2_spell_points"),
            P1ComboGaugeRaw = S("p1_combo_gauge_raw"),
            P2ComboGaugeRaw = S("p2_combo_gauge_raw"),
            P1SpellAttacks = U("p1_spell_attacks"),
            P2SpellAttacks = U("p2_spell_attacks"),
            P1BossAttacks = U("p1_boss_attacks"),
            P2BossAttacks = U("p2_boss_attacks"),
            P1BossReversals = U("p1_boss_reversals"),
            P2BossReversals = U("p2_boss_reversals"),
            ClearLifeBonus = U("clear_life_bonus"),
            ClearMaxComboBonus = U("clear_max_combo_bonus"),
            ClearSpellBonus = U("clear_spell_bonus"),
            ClearBossBonus = U("clear_boss_bonus"),
            ClearReversalBonus = U("clear_reversal_bonus"),
            ClearLivesBonus = U("clear_lives_bonus"),
            ClearTotal = U("clear_total"),
            P1CpuDodgeMode = S("p1_cpu_dodge_mode"),
            P2CpuDodgeMode = S("p2_cpu_dodge_mode"),
            P1CpuQuickDisableTimer = S("p1_cpu_quick_disable_timer"),
            P2CpuQuickDisableTimer = S("p2_cpu_quick_disable_timer"),
            P1CpuStandstillTimer = S("p1_cpu_standstill_timer"),
            P2CpuStandstillTimer = S("p2_cpu_standstill_timer"),
            ExecutionType = Take(vals, "execution_type", SnapValue.K.Str).StrValue!,
        };
    }

    private Snapshot ToSnapshot(TickRecord rec, string wall, double monotonic) =>
        FromRecord(rec, wall, monotonic, AdonisLoaded);


    public static readonly (string Name, Func<Snapshot, string> Token)[] Fields =
    [
        ("wall_time", s => ParityValue.StringToken(s.WallTime)),
        ("monotonic", s => ParityValue.FloatToken(s.Monotonic)),
        ("mode", s => ParityValue.IntToken(s.Mode)),
        ("difficulty", s => ParityValue.IntToken(s.Difficulty)),
        ("stage_index", s => ParityValue.IntToken(s.StageIndex)),
        ("field_id", s => ParityValue.IntToken(s.FieldId)),
        ("battle_bgm_id", s => ParityValue.IntToken(s.BattleBgmId)),
        ("p1_character", s => ParityValue.IntToken(s.P1Character)),
        ("p2_character", s => ParityValue.IntToken(s.P2Character)),
        ("p1_control", s => ParityValue.IntToken(s.P1Control)),
        ("p2_control", s => ParityValue.IntToken(s.P2Control)),
        ("p1_cpu_level", s => ParityValue.IntToken(s.P1CpuLevel)),
        ("p2_cpu_level", s => ParityValue.IntToken(s.P2CpuLevel)),
        ("round_frames", s => ParityValue.IntToken(s.RoundFrames)),
        ("completed_rounds", s => ParityValue.IntToken(s.CompletedRounds)),
        ("rounds_required", s => ParityValue.IntToken(s.RoundsRequired)),
        ("p1_wins", s => ParityValue.IntToken(s.P1Wins)),
        ("p2_wins", s => ParityValue.IntToken(s.P2Wins)),
        ("result_state", s => ParityValue.IntToken(s.ResultState)),
        ("result_winner", s => ParityValue.IntToken(s.ResultWinner)),
        ("pause_used", s => ParityValue.IntToken(s.PauseUsed)),
        ("p1_life_raw", s => ParityValue.IntToken(s.P1LifeRaw)),
        ("p2_life_raw", s => ParityValue.IntToken(s.P2LifeRaw)),
        ("p1_gauge", s => ParityValue.FloatToken(s.P1Gauge)),
        ("p2_gauge", s => ParityValue.FloatToken(s.P2Gauge)),
        ("p1_lives", s => ParityValue.FloatToken(s.P1Lives)),
        ("p2_lives", s => ParityValue.FloatToken(s.P2Lives)),
        ("p1_score", s => ParityValue.IntToken(s.P1Score)),
        ("p2_score", s => ParityValue.IntToken(s.P2Score)),
        ("p1_max_combo", s => ParityValue.IntToken(s.P1MaxCombo)),
        ("p2_max_combo", s => ParityValue.IntToken(s.P2MaxCombo)),
        ("p1_spell_points", s => ParityValue.IntToken(s.P1SpellPoints)),
        ("p2_spell_points", s => ParityValue.IntToken(s.P2SpellPoints)),
        ("p1_combo_gauge_raw", s => ParityValue.IntToken(s.P1ComboGaugeRaw)),
        ("p2_combo_gauge_raw", s => ParityValue.IntToken(s.P2ComboGaugeRaw)),
        ("p1_spell_attacks", s => ParityValue.IntToken(s.P1SpellAttacks)),
        ("p2_spell_attacks", s => ParityValue.IntToken(s.P2SpellAttacks)),
        ("p1_boss_attacks", s => ParityValue.IntToken(s.P1BossAttacks)),
        ("p2_boss_attacks", s => ParityValue.IntToken(s.P2BossAttacks)),
        ("p1_boss_reversals", s => ParityValue.IntToken(s.P1BossReversals)),
        ("p2_boss_reversals", s => ParityValue.IntToken(s.P2BossReversals)),
        ("clear_life_bonus", s => ParityValue.IntToken(s.ClearLifeBonus)),
        ("clear_max_combo_bonus", s => ParityValue.IntToken(s.ClearMaxComboBonus)),
        ("clear_spell_bonus", s => ParityValue.IntToken(s.ClearSpellBonus)),
        ("clear_boss_bonus", s => ParityValue.IntToken(s.ClearBossBonus)),
        ("clear_reversal_bonus", s => ParityValue.IntToken(s.ClearReversalBonus)),
        ("clear_lives_bonus", s => ParityValue.IntToken(s.ClearLivesBonus)),
        ("clear_total", s => ParityValue.IntToken(s.ClearTotal)),
        ("p1_cpu_dodge_mode", s => ParityValue.IntToken(s.P1CpuDodgeMode)),
        ("p2_cpu_dodge_mode", s => ParityValue.IntToken(s.P2CpuDodgeMode)),
        ("p1_cpu_quick_disable_timer", s => ParityValue.IntToken(s.P1CpuQuickDisableTimer)),
        ("p2_cpu_quick_disable_timer", s => ParityValue.IntToken(s.P2CpuQuickDisableTimer)),
        ("p1_cpu_standstill_timer", s => ParityValue.IntToken(s.P1CpuStandstillTimer)),
        ("p2_cpu_standstill_timer", s => ParityValue.IntToken(s.P2CpuStandstillTimer)),
        ("execution_type", s => ParityValue.StringToken(s.ExecutionType)),
    ];

    private static readonly Dictionary<string, Func<Snapshot, string>> _tokenOf =
        Fields.ToDictionary(f => f.Name, f => f.Token, StringComparer.Ordinal);

    public static string Token(Snapshot s, string name) => _tokenOf[name](s);

    public static IReadOnlyList<(string Name, string FromSnapshot, string FromMapper)>
        CheckAgainstMapper(TickRecord rec, bool adonisLoaded = false)
    {
        var snap = FromRecord(rec, "", 0.0, adonisLoaded);
        var vals = SnapshotMapper.FromRecord(rec, adonisLoaded);
        var byName = new Dictionary<string, SnapValue>(vals.Count, StringComparer.Ordinal);
        foreach (var (name, value) in vals) byName[name] = value;

        var bad = new List<(string, string, string)>();
        foreach (var (name, token) in Fields)
        {
            if (!byName.TryGetValue(name, out var v)) continue;
            var mapper = v.Kind switch
            {
                SnapValue.K.Int => ParityValue.IntToken(v.IntValue),
                SnapValue.K.Float => ParityValue.FloatToken(v.FloatValue),
                _ => ParityValue.StringToken(v.StrValue ?? ""),
            };
            var mine = token(snap);
            if (!string.Equals(mine, mapper, StringComparison.Ordinal))
                bad.Add((name, mine, mapper));
        }
        return bad;
    }

    public static int MapperBackedFieldCount =>
        Fields.Count(f => _slotOf.ContainsKey(f.Name));


    public static string NowIso() =>
        DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    private static double MonotonicSeconds() =>
        (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
}
