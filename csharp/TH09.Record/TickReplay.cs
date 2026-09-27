using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.Record.Generated;

namespace TH09.Record;

public static class TickReplay
{
    public const uint RequiredFlags = RecordLabels.RequiredFlags;

    public static readonly string[] NeededWords =
    [
        "flags", "replay_flag",
        "mode", "difficulty", "stage_index", "field_id", "battle_bgm_id",
        "p1_character", "p2_character", "p1_control", "p2_control", "p1_cpu_level", "p2_cpu_level",
        "round_frames", "completed_rounds", "rounds_required", "p1_wins", "p2_wins",
        "result_state", "result_winner", "pause_used",
        "p1_life_raw", "p2_life_raw", "p1_gauge", "p2_gauge", "p1_lives", "p2_lives",
        "p1_score_raw", "p2_score_raw", "p1_max_combo", "p2_max_combo",
        "p1_spell_points", "p2_spell_points",
        "p1_spell_attacks", "p2_spell_attacks", "p1_boss_attacks", "p2_boss_attacks",
        "p1_boss_reversals", "p2_boss_reversals",
        "clear_life_bonus", "clear_max_combo_bonus", "clear_spell_bonus", "clear_boss_bonus",
        "clear_reversal_bonus", "clear_lives_bonus", "clear_total",
        "p1_combo_gauge_raw", "p2_combo_gauge_raw",
        "p1_cpu_dodge_mode", "p2_cpu_dodge_mode",
        "p1_cpu_quick_timer", "p2_cpu_quick_timer",
        "p1_cpu_stand_timer", "p2_cpu_stand_timer",
    ];

    public sealed record Result(long SessionId, long SourceSessionId, int Segments,
                                long TotalTicks, long KeptTicks, long Events,
                                IReadOnlyList<int> RecordVersions);

    public static SqliteConnection OpenLayer0(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Layer 0 の DB がありません: " + path, path);
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = RecordDb.BusyTimeoutMs / 1000,
        }.ToString();
        var conn = new SqliteConnection(cs);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA query_only=1";
        pragma.ExecuteNonQuery();
        return conn;
    }

    public static List<long> Sessions(SqliteConnection layer0)
    {
        var ids = new List<long>();
        using var cmd = layer0.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT session_id FROM session_ticks ORDER BY session_id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) ids.Add(r.GetInt64(0));
        return ids;
    }

    public static string EpochWall(long sec) =>
        string.Format(CultureInfo.InvariantCulture, "1970-01-01T{0:00}:{1:00}:{2:00}.000+00:00",
                      sec / 3600, sec / 60 % 60, sec % 60);

    public static Result ReplayInto(SqliteConnection conn, long sessionId,
                                    SqliteConnection layer0, long sourceSessionId,
                                    string closeStatus = "aborted", Action<string>? log = null)
    {
        var sm = new StateMachine(conn, sessionId, log);
        var only = new HashSet<string>(NeededWords, StringComparer.Ordinal);
        long total = 0, kept = 0;
        Snapshot? last = null;
        var wall = "";
        var versions = new SortedSet<int>();

        var segments = SegmentReader.SegmentsOf(layer0, sourceSessionId);
        foreach (var seg in segments)
        {
            versions.Add(seg.RecordVersion);
            var order = TickArchive.ParseFieldOrder(seg.FieldOrderText);
            if (order.TickCount == 0) continue;
            var cols = TickArchive.Decode(seg.Blob, seg.Encoding, order, only);

            var w = new Words(cols);
            for (var i = 0; i < order.TickCount; i++)
            {
                total++;
                if ((w.Flags[i] & RequiredFlags) != RequiredFlags) continue;
                if (kept % 60 == 0) wall = EpochWall(kept / 60);
                var snap = w.Make(i, kept, wall);
                kept++;
                last = snap;
                sm.Process(snap);
            }
        }
        sm.Close(last, closeStatus, wallFallback: EpochWall(0));
        return new Result(sessionId, sourceSessionId, segments.Count, total, kept,
                          sm.EventCount, [.. versions]);
    }

    private sealed class Words
    {
        private readonly uint[] _mode, _difficulty, _stageIndex, _fieldId, _battleBgmId;
        private readonly uint[] _p1Character, _p2Character, _p1Control, _p2Control, _p1CpuLevel, _p2CpuLevel;
        private readonly uint[] _roundFrames, _completedRounds, _roundsRequired, _p1Wins, _p2Wins;
        private readonly uint[] _resultState, _resultWinner, _pauseUsed;
        private readonly uint[] _p1LifeRaw, _p2LifeRaw, _p1Gauge, _p2Gauge, _p1Lives, _p2Lives;
        private readonly uint[] _p1ScoreRaw, _p2ScoreRaw, _p1MaxCombo, _p2MaxCombo;
        private readonly uint[] _p1SpellPoints, _p2SpellPoints;
        private readonly uint[] _p1SpellAttacks, _p2SpellAttacks, _p1BossAttacks, _p2BossAttacks;
        private readonly uint[] _p1BossReversals, _p2BossReversals;
        private readonly uint[] _clearLife, _clearMaxCombo, _clearSpell, _clearBoss, _clearReversal;
        private readonly uint[] _clearLives, _clearTotal;
        private readonly uint[] _p1ComboGauge, _p2ComboGauge;
        private readonly uint[] _p1Dodge, _p2Dodge, _p1Quick, _p2Quick, _p1Stand, _p2Stand;
        private readonly uint[] _replayFlag;

        public uint[] Flags { get; }

        public Words(TickArchive.TickColumns c)
        {
            Flags = c["flags"]; _replayFlag = c["replay_flag"];
            _mode = c["mode"]; _difficulty = c["difficulty"]; _stageIndex = c["stage_index"];
            _fieldId = c["field_id"]; _battleBgmId = c["battle_bgm_id"];
            _p1Character = c["p1_character"]; _p2Character = c["p2_character"];
            _p1Control = c["p1_control"]; _p2Control = c["p2_control"];
            _p1CpuLevel = c["p1_cpu_level"]; _p2CpuLevel = c["p2_cpu_level"];
            _roundFrames = c["round_frames"]; _completedRounds = c["completed_rounds"];
            _roundsRequired = c["rounds_required"]; _p1Wins = c["p1_wins"]; _p2Wins = c["p2_wins"];
            _resultState = c["result_state"]; _resultWinner = c["result_winner"]; _pauseUsed = c["pause_used"];
            _p1LifeRaw = c["p1_life_raw"]; _p2LifeRaw = c["p2_life_raw"];
            _p1Gauge = c["p1_gauge"]; _p2Gauge = c["p2_gauge"];
            _p1Lives = c["p1_lives"]; _p2Lives = c["p2_lives"];
            _p1ScoreRaw = c["p1_score_raw"]; _p2ScoreRaw = c["p2_score_raw"];
            _p1MaxCombo = c["p1_max_combo"]; _p2MaxCombo = c["p2_max_combo"];
            _p1SpellPoints = c["p1_spell_points"]; _p2SpellPoints = c["p2_spell_points"];
            _p1SpellAttacks = c["p1_spell_attacks"]; _p2SpellAttacks = c["p2_spell_attacks"];
            _p1BossAttacks = c["p1_boss_attacks"]; _p2BossAttacks = c["p2_boss_attacks"];
            _p1BossReversals = c["p1_boss_reversals"]; _p2BossReversals = c["p2_boss_reversals"];
            _clearLife = c["clear_life_bonus"]; _clearMaxCombo = c["clear_max_combo_bonus"];
            _clearSpell = c["clear_spell_bonus"]; _clearBoss = c["clear_boss_bonus"];
            _clearReversal = c["clear_reversal_bonus"]; _clearLives = c["clear_lives_bonus"];
            _clearTotal = c["clear_total"];
            _p1ComboGauge = c["p1_combo_gauge_raw"]; _p2ComboGauge = c["p2_combo_gauge_raw"];
            _p1Dodge = c["p1_cpu_dodge_mode"]; _p2Dodge = c["p2_cpu_dodge_mode"];
            _p1Quick = c["p1_cpu_quick_timer"]; _p2Quick = c["p2_cpu_quick_timer"];
            _p1Stand = c["p1_cpu_stand_timer"]; _p2Stand = c["p2_cpu_stand_timer"];
        }

        private static long U(uint v) => v;

        private static int S(uint v) => unchecked((int)v);

        private static double F(uint v) => BitConverter.UInt32BitsToSingle(v);

        private static long X10(uint v) => (long)v * 10;

        public Snapshot Make(int i, long kept, string wall)
        {
            var replayActive = (Flags[i] & RecordLabels.FlagReplayMgrValid) != 0 && _replayFlag[i] == 1;
            return new Snapshot
            {
                WallTime = wall,
                Monotonic = kept / 60.0,
                Mode = U(_mode[i]),
                Difficulty = U(_difficulty[i]),
                StageIndex = U(_stageIndex[i]),
                FieldId = U(_fieldId[i]),
                BattleBgmId = U(_battleBgmId[i]),
                P1Character = U(_p1Character[i]),
                P2Character = U(_p2Character[i]),
                P1Control = U(_p1Control[i]),
                P2Control = U(_p2Control[i]),
                P1CpuLevel = U(_p1CpuLevel[i]),
                P2CpuLevel = U(_p2CpuLevel[i]),
                RoundFrames = U(_roundFrames[i]),
                CompletedRounds = U(_completedRounds[i]),
                RoundsRequired = U(_roundsRequired[i]),
                P1Wins = U(_p1Wins[i]),
                P2Wins = U(_p2Wins[i]),
                ResultState = U(_resultState[i]),
                ResultWinner = U(_resultWinner[i]),
                PauseUsed = U(_pauseUsed[i]),
                P1LifeRaw = U(_p1LifeRaw[i]),
                P2LifeRaw = U(_p2LifeRaw[i]),
                P1Gauge = F(_p1Gauge[i]),
                P2Gauge = F(_p2Gauge[i]),
                P1Lives = F(_p1Lives[i]),
                P2Lives = F(_p2Lives[i]),
                P1Score = X10(_p1ScoreRaw[i]),
                P2Score = X10(_p2ScoreRaw[i]),
                P1MaxCombo = U(_p1MaxCombo[i]),
                P2MaxCombo = U(_p2MaxCombo[i]),
                P1SpellPoints = U(_p1SpellPoints[i]),
                P2SpellPoints = U(_p2SpellPoints[i]),
                P1SpellAttacks = U(_p1SpellAttacks[i]),
                P2SpellAttacks = U(_p2SpellAttacks[i]),
                P1BossAttacks = U(_p1BossAttacks[i]),
                P2BossAttacks = U(_p2BossAttacks[i]),
                P1BossReversals = U(_p1BossReversals[i]),
                P2BossReversals = U(_p2BossReversals[i]),
                ClearLifeBonus = U(_clearLife[i]),
                ClearMaxComboBonus = U(_clearMaxCombo[i]),
                ClearSpellBonus = U(_clearSpell[i]),
                ClearBossBonus = U(_clearBoss[i]),
                ClearReversalBonus = U(_clearReversal[i]),
                ClearLivesBonus = U(_clearLives[i]),
                ClearTotal = U(_clearTotal[i]),
                P1ComboGaugeRaw = S(_p1ComboGauge[i]),
                P2ComboGaugeRaw = S(_p2ComboGauge[i]),
                P1CpuDodgeMode = S(_p1Dodge[i]),
                P2CpuDodgeMode = S(_p2Dodge[i]),
                P1CpuQuickDisableTimer = S(_p1Quick[i]),
                P2CpuQuickDisableTimer = S(_p2Quick[i]),
                P1CpuStandstillTimer = S(_p1Stand[i]),
                P2CpuStandstillTimer = S(_p2Stand[i]),
                ExecutionType = replayActive ? RecordLabels.ExecReplay : RecordLabels.ExecLive,
            };
        }
    }
}
