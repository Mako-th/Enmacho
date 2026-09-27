using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public static class SamePlayRule
{
    public static string? Broken(Snapshot? prev, Snapshot now)
    {
        ArgumentNullException.ThrowIfNull(now);
        if (prev is null) return null;
        if (now.Mode != prev.Mode)
            return "mode " + Num(prev.Mode) + " → " + Num(now.Mode);
        if (now.Difficulty != prev.Difficulty)
            return "難易度 " + Num(prev.Difficulty) + " → " + Num(now.Difficulty);
        if (now.P1Character != prev.P1Character)
            return "1P キャラ " + Num(prev.P1Character) + " → " + Num(now.P1Character);
        return null;
    }

    private static string Num(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class StateMachine
{
    private readonly SqliteConnection _c;
    private readonly long _sid;
    private readonly Action<string>? _log;

    private Snapshot? _prev;
    private long _lastGoodFrames;
    private long? _stageId;
    private long? _roundId;
    private Payload? _pendingResult;
    private readonly List<PendingHit> _pendingHits = [];

    public Snapshot? Prev => _prev;

    public long EventCount { get; private set; }

    public StateMachine(SqliteConnection conn, long sessionId, Action<string>? log = null)
    {
        _c = conn;
        _sid = sessionId;
        _log = log;
    }


    private sealed class PendingHit
    {
        public required int Side { get; init; }
        public required long RoundFrames { get; init; }
        public required long StageIndex { get; init; }
        public required long CompletedRounds { get; init; }
        public required long LifeRawFrom { get; init; }
        public long LifeRawTo { get; set; }
        public required long GaugeAtHitRaw { get; init; }
        public long? GaugeBeforeBonusRaw { get; set; }
        public long? GaugeAfterBonusRaw { get; set; }
        public required int GaugeBonusFrames { get; init; }
        public bool KuraiC2 { get; set; }
        public required int OppCpuDodgeMode { get; init; }
        public required int OppCpuQuickDisableTimer { get; init; }
        public required int OppCpuStandstillTimer { get; init; }
        public required List<string> DetectedBy { get; init; }

        public int Age { get; set; }
        public required int AfterAt { get; init; }
        public required int BeforeAt { get; init; }
        public required long Attacks0 { get; init; }

        public Payload ToPayload(string status)
        {
            var p = new Payload()
                .Add("side", (long)Side)
                .Add("round_frames", RoundFrames)
                .Add("stage_index", StageIndex)
                .Add("completed_rounds", CompletedRounds)
                .Add("life_raw_from", LifeRawFrom)
                .Add("life_raw_to", LifeRawTo)
                .Add("gauge_at_hit_raw", GaugeAtHitRaw)
                .Add("gauge_before_bonus_raw", GaugeBeforeBonusRaw)
                .Add("gauge_after_bonus_raw", GaugeAfterBonusRaw)
                .Add("gauge_bonus_frames", (long)GaugeBonusFrames)
                .Add("kurai_c2", KuraiC2)
                .Add("opp_cpu_dodge_mode", (long)OppCpuDodgeMode)
                .Add("opp_cpu_quick_disable_timer", (long)OppCpuQuickDisableTimer)
                .Add("opp_cpu_standstill_timer", (long)OppCpuStandstillTimer)
                .AddStrings("detected_by", DetectedBy)
                .AddEmptyArray("candidates");
            p.Add("gauge_sample_status", status);
            return p;
        }
    }


    private int Exec(string sql, params object?[] args)
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = sql;
        for (var i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue("$" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                        args[i] ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    private long LastRowId()
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private void Emit(string eventType, Snapshot s, Payload payload, long? gameFrame = null)
    {
        var text = payload.ToJson();
        var frame = gameFrame ?? s.RoundFrames;
        Exec("INSERT INTO events(session_id,wall_time,event_type,game_frame,side,payload_json)"
             + " VALUES($0,$1,$2,$3,$4,$5)",
             _sid, s.WallTime, eventType, frame, payload.Side, text);
        EventCount++;
        if (_log is not null)
        {
            var stamp = s.WallTime.Length >= 23 ? s.WallTime[11..23] : s.WallTime;
            _log($"[{stamp}] {eventType}: {text}");
        }
    }

    private long SaveClearBonus(Snapshot s, long? winnerSide, long scoreBefore, long scoreAfter)
    {
        var side = winnerSide is 1 or 2 ? winnerSide!.Value : 1;
        var lifeRaw = side == 1 ? s.P1LifeRaw : s.P2LifeRaw;
        var maxCombo = side == 1 ? s.P1MaxCombo : s.P2MaxCombo;
        var spell = side == 1 ? s.P1SpellAttacks : s.P2SpellAttacks;
        var boss = side == 1 ? s.P1BossAttacks : s.P2BossAttacks;
        var reversal = side == 1 ? s.P1BossReversals : s.P2BossReversals;
        var lives = side == 1 ? s.P1Lives : s.P2Lives;
        Exec("INSERT INTO clear_bonuses("
             + "session_id,wall_time,mode,stage_index,winner_side,life_raw,maximum_combo,"
             + "spell_attack_count,boss_attack_count,boss_reversal_count,remaining_players,"
             + "life_bonus,maximum_combo_bonus,spell_attack_bonus,boss_attack_bonus,"
             + "boss_reversal_bonus,remaining_players_bonus,total_bonus,score_before_bonus,score_after_bonus)"
             + " VALUES($0,$1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19)",
             _sid, s.WallTime, (long)s.Mode, (long)s.StageIndex, side, lifeRaw, maxCombo,
             spell, boss, reversal, lives,
             s.ClearLifeBonus, s.ClearMaxComboBonus, s.ClearSpellBonus, s.ClearBossBonus,
             s.ClearReversalBonus, s.ClearLivesBonus, s.ClearTotal, scoreBefore, scoreAfter);
        return LastRowId();
    }


    private bool CloseStaleRound(Snapshot s, string status = "aborted", string reason = "")
    {
        if (_roundId is null) return false;
        var re = _prev ?? s;
        var frames = re.RoundFrames > 0 ? re.RoundFrames : _lastGoodFrames;
        var n = Exec(
            "UPDATE rounds SET ended_at=COALESCE(ended_at,$0),status=$1,"
            + "duration_frames=COALESCE(duration_frames,$2),"
            + "score_1_at_end=COALESCE(score_1_at_end,$3),score_2_at_end=COALESCE(score_2_at_end,$4),"
            + "life_1_raw_at_end=COALESCE(life_1_raw_at_end,$5),life_2_raw_at_end=COALESCE(life_2_raw_at_end,$6),"
            + "lives_1_at_end=COALESCE(lives_1_at_end,$7),pause_used=$8"
            + " WHERE round_record_id=$9 AND status='running'",
            s.WallTime, status, frames, re.P1Score, re.P2Score, re.P1LifeRaw, re.P2LifeRaw,
            re.P1Lives, re.PauseUsed != 0 ? 1L : 0L, _roundId);
        if (n == 0) return false;
        Emit("ROUND_ABANDONED", s, new Payload()
            .Add("round_record_id", _roundId)
            .Add("status", status)
            .Add("reason", reason)
            .Add("duration_frames", frames)
            .Add("duration", MonitorRules.FmtFrames(frames)));
        return true;
    }

    private bool CloseStaleStage(Snapshot s, string status = "aborted", string reason = "")
    {
        if (_stageId is null) return false;
        var re = _prev ?? s;
        var n = Exec(
            "UPDATE stages SET ended_at=COALESCE(ended_at,$0),status=$1,"
            + "score_at_end=COALESCE(score_at_end,$2),lives_at_end=COALESCE(lives_at_end,$3)"
            + " WHERE stage_record_id=$4 AND status='running'",
            s.WallTime, status, re.P1Score, re.P1Lives, _stageId);
        if (n == 0) return false;
        Emit("STAGE_ABANDONED", s, new Payload()
            .Add("stage_record_id", _stageId)
            .Add("status", status)
            .Add("reason", reason)
            .Add("stage", re.Mode is 0 or 1 ? re.StageIndex + 1 : null));
        return true;
    }

    private void OpenStage(Snapshot s)
    {
        FlushPendingHits(s, "stage_changed");
        CloseStaleRound(s, reason: "次のステージが始まりました");
        CloseStaleStage(s, reason: "次のステージが始まりました");
        long? stageNumber = s.Mode is 0 or 1 ? s.StageIndex + 1 : null;
        Exec("INSERT INTO stages(session_id,stage_number,started_at,status,"
             + "opponent_character,field_id,battle_bgm_id,score_at_start,lives_at_start)"
             + " VALUES($0,$1,$2,$3,$4,$5,$6,$7,$8)",
             _sid, stageNumber, s.WallTime, "running", (long)s.P2Character, (long)s.FieldId,
             MonitorRules.ValidBgmId(s.BattleBgmId) ? s.BattleBgmId : null, s.P1Score, s.P1Lives);
        _stageId = LastRowId();
    }

    private void OpenRound(Snapshot s, long roundNumber)
    {
        FlushPendingHits(s, "round_started");
        CloseStaleRound(s, reason: "次のラウンドが始まりました");
        Exec("INSERT INTO rounds(session_id,stage_record_id,round_number,started_at,status,"
             + "score_1_at_start,score_2_at_start,life_1_raw_at_start,life_2_raw_at_start,lives_1_at_start)"
             + " VALUES($0,$1,$2,$3,$4,$5,$6,$7,$8,$9)",
             _sid, _stageId, roundNumber, s.WallTime, "running", s.P1Score, s.P2Score,
             s.P1LifeRaw, s.P2LifeRaw, s.P1Lives);
        _roundId = LastRowId();
    }

    private void FinishRound(Snapshot s, long? winner)
    {
        if (_roundId is null) return;
        Exec("UPDATE rounds SET ended_at=$0,status='completed',duration_frames=$1,winner_side=$2,"
             + "score_1_at_end=$3,score_2_at_end=$4,life_1_raw_at_end=$5,life_2_raw_at_end=$6,"
             + "lives_1_at_end=$7,pause_used=$8 WHERE round_record_id=$9",
             s.WallTime, _lastGoodFrames, winner, s.P1Score, s.P2Score, s.P1LifeRaw, s.P2LifeRaw,
             s.P1Lives, s.PauseUsed != 0 ? 1L : 0L, _roundId);
    }

    public void Close(Snapshot? s, string status = "aborted", string wallFallback = "")
    {
        FlushPendingHits(s, "session_closed");
        var wall = s?.WallTime ?? wallFallback;
        if (_pendingResult is not null && _stageId is not null)
        {
            var pr = _pendingResult;
            Exec("UPDATE stages SET ended_at=COALESCE(ended_at,$0),status='completed',"
                 + "score_at_end=COALESCE(score_at_end,$1),lives_at_end=COALESCE(lives_at_end,$2),"
                 + "winner_side=COALESCE(winner_side,$3),clear_bonus_id=COALESCE(clear_bonus_id,$4)"
                 + " WHERE stage_record_id=$5",
                 wall, _pendingExpectedAfter, _pendingLives1, _pendingWinnerSide, _pendingClearBonusId, _stageId);
            if (s is not null)
            {
                var settled = pr.Clone();
                settled.Set("settled_by", "session_close");
                Emit("STAGE_COMPLETED", s, settled);
            }
            _pendingResult = null;
        }
        if (_roundId is not null)
        {
            if (s is null)
            {
                Exec("UPDATE rounds SET ended_at=COALESCE(ended_at,$0),"
                     + "status=CASE WHEN status='running' THEN $1 ELSE status END"
                     + " WHERE round_record_id=$2", wall, status, _roundId);
            }
            else
            {
                Exec("UPDATE rounds SET ended_at=COALESCE(ended_at,$0),"
                     + "status=CASE WHEN status='running' THEN $1 ELSE status END,"
                     + "duration_frames=COALESCE(duration_frames,$2),"
                     + "score_1_at_end=COALESCE(score_1_at_end,$3),score_2_at_end=COALESCE(score_2_at_end,$4),"
                     + "life_1_raw_at_end=COALESCE(life_1_raw_at_end,$5),life_2_raw_at_end=COALESCE(life_2_raw_at_end,$6),"
                     + "lives_1_at_end=COALESCE(lives_1_at_end,$7),pause_used=$8"
                     + " WHERE round_record_id=$9",
                     wall, status, s.RoundFrames, s.P1Score, s.P2Score, s.P1LifeRaw, s.P2LifeRaw,
                     s.P1Lives, s.PauseUsed != 0 ? 1L : 0L, _roundId);
            }
        }
        if (_stageId is not null)
        {
            Exec("UPDATE stages SET ended_at=COALESCE(ended_at,$0),"
                 + "status=CASE WHEN status='running' THEN $1 ELSE status END,"
                 + "score_at_end=COALESCE(score_at_end,$2),lives_at_end=COALESCE(lives_at_end,$3)"
                 + " WHERE stage_record_id=$4",
                 wall, status, s is null ? null : (object)s.P1Score, s is null ? null : (object)s.P1Lives, _stageId);
        }
    }


    private static bool RoundIsLive(Snapshot s, Snapshot p) =>
        p.ResultState == 0 && s.RoundFrames >= p.RoundFrames
        && s.CompletedRounds >= p.CompletedRounds
        && s.StageIndex == p.StageIndex;

    private static List<string> HitSignals(Snapshot s, Snapshot p, int side)
    {
        var sig = new List<string>();
        if (s.LifeRaw(side) < p.LifeRaw(side)) sig.Add("life_raw");
        if (p.SpellPoints(side) > 0 && s.SpellPoints(side) == 0) sig.Add("spell_points_reset");
        if (MonitorRules.NormalizeComboGauge(p.ComboGaugeRaw(side)) > 0
            && MonitorRules.NormalizeComboGauge(s.ComboGaugeRaw(side)) == 0) sig.Add("combo_gauge_reset");
        return sig;
    }

    private void OpenHit(Snapshot s, Snapshot p, int side, List<string> signals)
    {
        var lifeTo = s.LifeRaw(side);
        var after = lifeTo == 0 ? RecordLabels.HitGaugeBonusFramesDeath : RecordLabels.HitGaugeBonusFrames;
        var opp = side == 1 ? 2 : 1;
        _pendingHits.Add(new PendingHit
        {
            Side = side,
            RoundFrames = s.RoundFrames,
            StageIndex = s.StageIndex,
            CompletedRounds = p.CompletedRounds,
            LifeRawFrom = p.LifeRaw(side),
            LifeRawTo = lifeTo,
            GaugeAtHitRaw = MonitorRules.GaugeBits(s.Gauge(side)),
            GaugeBeforeBonusRaw = null,
            GaugeAfterBonusRaw = null,
            GaugeBonusFrames = after,
            KuraiC2 = false,
            OppCpuDodgeMode = p.CpuDodgeMode(opp),
            OppCpuQuickDisableTimer = p.CpuQuickDisableTimer(opp),
            OppCpuStandstillTimer = p.CpuStandstillTimer(opp),
            DetectedBy = [.. signals],
            Age = 0,
            AfterAt = after,
            BeforeAt = after - 1,
            Attacks0 = s.AttackTotal(side),
        });
    }

    private void AdvancePendingHits(Snapshot s)
    {
        if (_pendingHits.Count == 0) return;
        List<PendingHit>? done = null;
        foreach (var h in _pendingHits)
        {
            h.Age += 1;
            var side = h.Side;
            if (h.Age <= h.BeforeAt && s.AttackTotal(side) > h.Attacks0) h.KuraiC2 = true;
            if (h.GaugeBeforeBonusRaw is null && h.Age >= h.BeforeAt)
                h.GaugeBeforeBonusRaw = MonitorRules.GaugeBits(s.Gauge(side));
            if (h.Age >= h.AfterAt)
            {
                h.GaugeAfterBonusRaw = MonitorRules.GaugeBits(s.Gauge(side));
                (done ??= []).Add(h);
            }
        }
        if (done is null) return;
        foreach (var h in done)
        {
            _pendingHits.Remove(h);
            EmitHit(s, h, "complete");
        }
    }

    private void FlushPendingHits(Snapshot? s, string reason)
    {
        if (_pendingHits.Count == 0) return;
        var re = s ?? _prev;
        var pending = new List<PendingHit>(_pendingHits);
        _pendingHits.Clear();
        if (re is null) return;
        foreach (var h in pending) EmitHit(re, h, reason);
    }

    private void EmitHit(Snapshot s, PendingHit h, string status) =>
        Emit("HIT", s, h.ToPayload(status), gameFrame: h.RoundFrames);

    private void DetectHits(Snapshot s, Snapshot p)
    {
        if (!RoundIsLive(s, p)) return;
        for (var side = 1; side <= 2; side++)
        {
            var sig = HitSignals(s, p, side);
            if (sig.Count == 0 || sig[0] != "life_raw") continue;
            PendingHit? recent = null;
            foreach (var h in _pendingHits)
                if (h.Side == side && h.Age < RecordLabels.HitClusterTicks) recent = h;
            if (recent is not null)
            {
                recent.LifeRawTo = s.LifeRaw(side);
                foreach (var nm in sig)
                    if (!recent.DetectedBy.Contains(nm)) recent.DetectedBy.Add(nm);
                continue;
            }
            OpenHit(s, p, side, sig);
        }
    }


    private long? _pendingExpectedAfter;
    private double? _pendingLives1;
    private long? _pendingWinnerSide;
    private long? _pendingClearBonusId;

    public void Process(Snapshot s)
    {
        if (s.RoundFrames > 0) _lastGoodFrames = s.RoundFrames;
        var bgmValue = MonitorRules.ValidBgmId(s.BattleBgmId) ? s.BattleBgmId : (long?)null;
        var bgmText = bgmValue is not null
            ? MonitorRules.Name(RecordLabels.Bgms, bgmValue.Value)
            : "Loading / Unset";

        if (_prev is null)
        {
            var initialLives = s.P1Lives;
            Exec("INSERT OR REPLACE INTO session_metadata(session_id,game_mode,difficulty,"
                 + "p1_character,p2_character,p1_control,p2_control,p1_cpu_level,p2_cpu_level,"
                 + "initial_lives,initial_life_raw,initial_lives_option_modified,field_id,battle_bgm_id,execution_type)"
                 + " VALUES($0,$1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)",
                 _sid, (long)s.Mode, (long)s.Difficulty, (long)s.P1Character, (long)s.P2Character,
                 (long)s.P1Control, (long)s.P2Control, (long)s.P1CpuLevel, (long)s.P2CpuLevel,
                 initialLives, s.P1LifeRaw, null, (long)s.FieldId, bgmValue, s.ExecutionType);
            OpenStage(s);
            OpenRound(s, 1);
            Emit("SESSION_STARTED", s, new Payload()
                .Add("mode", MonitorRules.Name(RecordLabels.Modes, s.Mode))
                .Add("difficulty", MonitorRules.Name(RecordLabels.Difficulties, s.Difficulty))
                .Add("execution_type", s.ExecutionType)
                .Add("stage", s.Mode is 0 or 1 ? s.StageIndex + 1 : null)
                .Add("field", MonitorRules.Name(RecordLabels.Fields, s.FieldId))
                .Add("battle_bgm", bgmText)
                .Add("p1", MonitorRules.Char(s.P1Character))
                .Add("p2", MonitorRules.Char(s.P2Character))
                .AddFloat("initial_lives", initialLives)
                .Add("initial_life_raw", s.P1LifeRaw));
            _prev = s;
            return;
        }

        var p = _prev;
        AdvancePendingHits(s);

        if (p.ResultState == 0 && s.ResultState == 1)
        {
            Emit("ROUND_END_STATE", s, new Payload()
                .Add("duration_frames", _lastGoodFrames)
                .Add("duration", MonitorRules.FmtFrames(_lastGoodFrames)));
        }

        if (s.CompletedRounds > p.CompletedRounds)
        {
            long? winner = s.P1Wins > p.P1Wins ? 1 : s.P2Wins > p.P2Wins ? 2 : null;
            FinishRound(s, winner);
            Emit("ROUND_COMPLETED", s, new Payload()
                .Add("side", winner)
                .Add("round_number", s.CompletedRounds)
                .Add("duration_frames", _lastGoodFrames)
                .Add("score_1_before_result", s.P1Score)
                .Add("score_2_before_result", s.P2Score)
                .Add("pause_used", s.PauseUsed != 0));
        }

        if (p.ResultState != 2 && s.ResultState == 2)
        {
            var eventName = s.Mode == 2 ? "MATCH_FINAL" : "STAGE_RESULT_READY";
            long? winnerSide = s.ResultWinner is 0 or 1 ? s.ResultWinner + 1 : null;
            var scoreBefore = winnerSide != 2 ? s.P1Score : s.P2Score;
            var expectedAfter = scoreBefore + s.ClearTotal;
            var payload = new Payload()
                .Add("winner_side", winnerSide)
                .Add("field_id", (long)s.FieldId)
                .Add("battle_bgm_id", bgmValue)
                .AddFloat("lives_1", s.P1Lives)
                .Add("life_bonus", s.ClearLifeBonus)
                .Add("maximum_combo_bonus", s.ClearMaxComboBonus)
                .Add("spell_attack_bonus", s.ClearSpellBonus)
                .Add("boss_attack_bonus", s.ClearBossBonus)
                .Add("boss_reversal_bonus", s.ClearReversalBonus)
                .Add("remaining_players_bonus", s.ClearLivesBonus)
                .Add("clear_bonus_total", s.ClearTotal)
                .Add("score_before_bonus", scoreBefore)
                .Add("expected_score_after_bonus", expectedAfter);
            Emit(eventName, s, payload);
            var bonusId = SaveClearBonus(s, winnerSide, scoreBefore, expectedAfter);
            _pendingResult = payload.Clone();
            _pendingResult.Add("clear_bonus_id", bonusId);
            _pendingExpectedAfter = expectedAfter;
            _pendingLives1 = s.P1Lives;
            _pendingWinnerSide = winnerSide;
            _pendingClearBonusId = bonusId;
            if (s.Mode == 2)
            {
                Exec("UPDATE stages SET ended_at=$0,status='completed',score_at_end=$1,lives_at_end=$2,"
                     + "winner_side=$3,clear_bonus_id=$4 WHERE stage_record_id=$5",
                     s.WallTime, expectedAfter, s.P1Lives, winnerSide, bonusId, _stageId);
            }
        }

        if (p.ResultState == 1 && s.ResultState == 0 && s.RoundFrames < p.RoundFrames
            && s.StageIndex == p.StageIndex
            && MonitorRules.MoreRoundsToPlay(s.Mode, s.P1Wins, s.P2Wins, s.RoundsRequired))
        {
            OpenRound(s, s.CompletedRounds + 1);
            Emit("ROUND_STARTED", s, new Payload()
                .Add("round_number", s.CompletedRounds + 1)
                .Add("stage", s.Mode is 0 or 1 ? s.StageIndex + 1 : null)
                .AddFloat("lives_1", s.P1Lives));
        }

        if (s.Mode is 0 or 1 && s.StageIndex != p.StageIndex && s.StageIndex is >= 0 and <= 8)
        {
            if (_pendingResult is not null)
            {
                var settled = _pendingResult.Clone();
                settled.Set("completed_stage", p.StageIndex + 1);
                settled.Set("observed_score_after_bonus", s.P1Score);
                Emit("STAGE_COMPLETED", s, settled);
                Exec("UPDATE stages SET ended_at=$0,status='completed',score_at_end=$1,lives_at_end=$2,"
                     + "winner_side=$3,clear_bonus_id=$4 WHERE stage_record_id=$5",
                     s.WallTime, s.P1Score, p.P1Lives, _pendingWinnerSide, _pendingClearBonusId, _stageId);
                _pendingResult = null;
            }
            OpenStage(s);
            OpenRound(s, 1);
            Emit("STAGE_CHANGED", s, new Payload()
                .Add("stage", s.StageIndex + 1)
                .Add("field", MonitorRules.Name(RecordLabels.Fields, s.FieldId))
                .Add("battle_bgm", bgmText));
        }

        if (_stageId is not null && s.P2Character != p.P2Character
            && s.P2Character >= 0 && s.P2Character < RecordLabels.Characters.Length)
        {
            Exec("UPDATE stages SET opponent_character=$0 WHERE stage_record_id=$1",
                 (long)s.P2Character, _stageId);
        }

        if (MonitorRules.ValidBgmId(s.BattleBgmId) && s.BattleBgmId != p.BattleBgmId)
        {
            Exec("UPDATE stages SET battle_bgm_id=$0 WHERE stage_record_id=$1",
                 (long)s.BattleBgmId, _stageId);
            Emit("BATTLE_BGM_SELECTED", s, new Payload()
                .Add("battle_bgm_id", (long)s.BattleBgmId)
                .Add("battle_bgm", MonitorRules.Name(RecordLabels.Bgms, s.BattleBgmId))
                .Add("stage", s.Mode is 0 or 1 ? s.StageIndex + 1 : null));
        }

        foreach (var (nowValue, prevValue, etype, side) in CounterEvents(s, p))
        {
            var d = nowValue - prevValue;
            if (d > 0)
            {
                Emit(etype, s, new Payload()
                    .Add("side", (long)side)
                    .Add("delta", d)
                    .Add("value", nowValue));
            }
        }

        if (s.Mode is 0 or 1 && s.P1Lives > p.P1Lives)
        {
            Emit("EXTEND", s, new Payload()
                .Add("side", 1L)
                .AddFloat("from", p.P1Lives)
                .AddFloat("to", s.P1Lives));
        }
        if (s.Mode is 0 or 1 && s.P1Lives >= 0 && s.P1Lives < p.P1Lives)
        {
            Emit("LIVES_LOST", s, new Payload()
                .Add("side", 1L)
                .AddFloat("from", p.P1Lives)
                .AddFloat("to", s.P1Lives)
                .Add("stage", s.StageIndex + 1));
        }

        DetectHits(s, p);
        _prev = s;
    }

    private static IEnumerable<(long Now, long Prev, string Type, int Side)> CounterEvents(Snapshot s, Snapshot p)
    {
        yield return (s.P1SpellAttacks, p.P1SpellAttacks, "SPELL_ATTACK", 1);
        yield return (s.P1BossAttacks, p.P1BossAttacks, "BOSS_ATTACK", 1);
        yield return (s.P1BossReversals, p.P1BossReversals, "BOSS_REVERSAL", 1);
        yield return (s.P2SpellAttacks, p.P2SpellAttacks, "SPELL_ATTACK", 2);
        yield return (s.P2BossAttacks, p.P2BossAttacks, "BOSS_ATTACK", 2);
        yield return (s.P2BossReversals, p.P2BossReversals, "BOSS_REVERSAL", 2);
    }
}
