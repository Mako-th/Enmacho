#nullable enable

namespace TH09.Generated;

internal static class DbColumns
{
    public const int TableCount = 18;
    public const int ColumnCount = 212;

    public static class ClearBonuses
    {
        public const string Table = "clear_bonuses";
        public const string ClearBonusId = "clear_bonus_id";
        public const string SessionId = "session_id";
        public const string WallTime = "wall_time";
        public const string Mode = "mode";
        public const string StageIndex = "stage_index";
        public const string WinnerSide = "winner_side";
        public const string LifeRaw = "life_raw";
        public const string MaximumCombo = "maximum_combo";
        public const string SpellAttackCount = "spell_attack_count";
        public const string BossAttackCount = "boss_attack_count";
        public const string BossReversalCount = "boss_reversal_count";
        public const string RemainingPlayers = "remaining_players";
        public const string LifeBonus = "life_bonus";
        public const string MaximumComboBonus = "maximum_combo_bonus";
        public const string SpellAttackBonus = "spell_attack_bonus";
        public const string BossAttackBonus = "boss_attack_bonus";
        public const string BossReversalBonus = "boss_reversal_bonus";
        public const string RemainingPlayersBonus = "remaining_players_bonus";
        public const string TotalBonus = "total_bonus";
        public const string ScoreBeforeBonus = "score_before_bonus";
        public const string ScoreAfterBonus = "score_after_bonus";

        public static readonly string[] All = [ClearBonusId, SessionId, WallTime, Mode, StageIndex, WinnerSide, LifeRaw, MaximumCombo, SpellAttackCount, BossAttackCount, BossReversalCount, RemainingPlayers, LifeBonus, MaximumComboBonus, SpellAttackBonus, BossAttackBonus, BossReversalBonus, RemainingPlayersBonus, TotalBonus, ScoreBeforeBonus, ScoreAfterBonus];
    }

    public static class DbMeta
    {
        public const string Table = "db_meta";
        public const string Key = "key";
        public const string Value = "value";

        public static readonly string[] All = [Key, Value];
    }

    public static class DeletedSessions
    {
        public const string Table = "deleted_sessions";
        public const string SessionId = "session_id";
        public const string DeletedAt = "deleted_at";
        public const string PayloadJson = "payload_json";

        public static readonly string[] All = [SessionId, DeletedAt, PayloadJson];
    }

    public static class Events
    {
        public const string Table = "events";
        public const string EventId = "event_id";
        public const string SessionId = "session_id";
        public const string WallTime = "wall_time";
        public const string EventType = "event_type";
        public const string GameFrame = "game_frame";
        public const string Side = "side";
        public const string PayloadJson = "payload_json";

        public static readonly string[] All = [EventId, SessionId, WallTime, EventType, GameFrame, Side, PayloadJson];
    }

    public static class PlayerAliases
    {
        public const string Table = "player_aliases";
        public const string Name = "name";
        public const string PlayerId = "player_id";
        public const string FirstSeenAt = "first_seen_at";
        public const string Source = "source";

        public static readonly string[] All = [Name, PlayerId, FirstSeenAt, Source];
    }

    public static class Players
    {
        public const string Table = "players";
        public const string PlayerId = "player_id";
        public const string DisplayName = "display_name";
        public const string IsSelf = "is_self";
        public const string Notes = "notes";
        public const string CreatedAt = "created_at";

        public static readonly string[] All = [PlayerId, DisplayName, IsSelf, Notes, CreatedAt];
    }

    public static class ReplayPaths
    {
        public const string Table = "replay_paths";
        public const string ReplayPathId = "replay_path_id";
        public const string ReplayId = "replay_id";
        public const string FullPath = "full_path";
        public const string Filename = "filename";
        public const string ParentHint = "parent_hint";
        public const string FirstSeenAt = "first_seen_at";
        public const string LastSeenAt = "last_seen_at";
        public const string IsCurrent = "is_current";

        public static readonly string[] All = [ReplayPathId, ReplayId, FullPath, Filename, ParentHint, FirstSeenAt, LastSeenAt, IsCurrent];
    }

    public static class ReplayScanItems
    {
        public const string Table = "replay_scan_items";
        public const string ItemId = "item_id";
        public const string JobId = "job_id";
        public const string ReplayId = "replay_id";
        public const string StartedAt = "started_at";
        public const string EndedAt = "ended_at";
        public const string Status = "status";
        public const string SessionId = "session_id";
        public const string VerifyStatus = "verify_status";
        public const string GapCount = "gap_count";
        public const string Attempt = "attempt";
        public const string Error = "error";
        public const string HitWindows = "hit_windows";
        public const string HitWindowTicks = "hit_window_ticks";
        public const string HitWindowBytes = "hit_window_bytes";
        public const string HitWindowLostTicks = "hit_window_lost_ticks";
        public const string HitWindowMinSlack = "hit_window_min_slack";
        public const string HitsSeen = "hits_seen";
        public const string QuicksSeen = "quicks_seen";
        public const string CoordTicks = "coord_ticks";
        public const string HitWindowVerifyFailures = "hit_window_verify_failures";
        public const string Layer0Segments = "layer0_segments";
        public const string Layer0Ticks = "layer0_ticks";
        public const string Layer0Bytes = "layer0_bytes";
        public const string Layer0Lost = "layer0_lost";
        public const string Layer0Torn = "layer0_torn";
        public const string Layer0VerifyFailures = "layer0_verify_failures";
        public const string HitWindowSkippedHits = "hit_window_skipped_hits";

        public static readonly string[] All = [ItemId, JobId, ReplayId, StartedAt, EndedAt, Status, SessionId, VerifyStatus, GapCount, Attempt, Error, HitWindows, HitWindowTicks, HitWindowBytes, HitWindowLostTicks, HitWindowMinSlack, HitsSeen, QuicksSeen, CoordTicks, HitWindowVerifyFailures, Layer0Segments, Layer0Ticks, Layer0Bytes, Layer0Lost, Layer0Torn, Layer0VerifyFailures, HitWindowSkippedHits];
    }

    public static class ReplayScanJobs
    {
        public const string Table = "replay_scan_jobs";
        public const string JobId = "job_id";
        public const string StartedAt = "started_at";
        public const string EndedAt = "ended_at";
        public const string Status = "status";
        public const string SpeedSetting = "speed_setting";
        public const string Note = "note";

        public static readonly string[] All = [JobId, StartedAt, EndedAt, Status, SpeedSetting, Note];
    }

    public static class Replays
    {
        public const string Table = "replays";
        public const string ReplayId = "replay_id";
        public const string Sha256 = "sha256";
        public const string FileSize = "file_size";
        public const string Mtime = "mtime";
        public const string FirstSeenAt = "first_seen_at";
        public const string LastSeenAt = "last_seen_at";
        public const string Source = "source";
        public const string Mode = "mode";
        public const string Difficulty = "difficulty";
        public const string PlayerName = "player_name";
        public const string ReplayDate = "replay_date";
        public const string P1Char = "p1_char";
        public const string P2Char = "p2_char";
        public const string P1Name = "p1_name";
        public const string P2Name = "p2_name";
        public const string IsOwn = "is_own";
        public const string OwnerSide = "owner_side";
        public const string DecodeStatus = "decode_status";
        public const string DecodedJson = "decoded_json";
        public const string OwnOverride = "own_override";

        public static readonly string[] All = [ReplayId, Sha256, FileSize, Mtime, FirstSeenAt, LastSeenAt, Source, Mode, Difficulty, PlayerName, ReplayDate, P1Char, P2Char, P1Name, P2Name, IsOwn, OwnerSide, DecodeStatus, DecodedJson, OwnOverride];
    }

    public static class RoundMetrics
    {
        public const string Table = "round_metrics";
        public const string RoundRecordId = "round_record_id";
        public const string P1SpellCapTicks = "p1_spell_cap_ticks";
        public const string P2SpellCapTicks = "p2_spell_cap_ticks";
        public const string P1SpellPointsMax = "p1_spell_points_max";
        public const string P2SpellPointsMax = "p2_spell_points_max";
        public const string P1SpellPointsMaxFrame = "p1_spell_points_max_frame";
        public const string P2SpellPointsMaxFrame = "p2_spell_points_max_frame";
        public const string P1SpellPointsTotal = "p1_spell_points_total";
        public const string P2SpellPointsTotal = "p2_spell_points_total";
        public const string P1GaugeAvg = "p1_gauge_avg";
        public const string P2GaugeAvg = "p2_gauge_avg";
        public const string P1GaugeMax = "p1_gauge_max";
        public const string P2GaugeMax = "p2_gauge_max";
        public const string P1ScoreGainRate = "p1_score_gain_rate";
        public const string P2ScoreGainRate = "p2_score_gain_rate";
        public const string P1HitsWithBossCandidate = "p1_hits_with_boss_candidate";
        public const string P2HitsWithBossCandidate = "p2_hits_with_boss_candidate";
        public const string P1HitsWithExCandidate = "p1_hits_with_ex_candidate";
        public const string P2HitsWithExCandidate = "p2_hits_with_ex_candidate";
        public const string P1HitsAmbiguous = "p1_hits_ambiguous";
        public const string P2HitsAmbiguous = "p2_hits_ambiguous";
        public const string P1BossPresentTicks = "p1_boss_present_ticks";
        public const string P2BossPresentTicks = "p2_boss_present_ticks";
        public const string P1CpuQuickTimerMax = "p1_cpu_quick_timer_max";
        public const string P2CpuQuickTimerMax = "p2_cpu_quick_timer_max";
        public const string P1CpuStandTimerMax = "p1_cpu_stand_timer_max";
        public const string P2CpuStandTimerMax = "p2_cpu_stand_timer_max";
        public const string P1CpuTimerFrozenTicks = "p1_cpu_timer_frozen_ticks";
        public const string P2CpuTimerFrozenTicks = "p2_cpu_timer_frozen_ticks";
        public const string P1NoHitGapMaxTicks = "p1_no_hit_gap_max_ticks";
        public const string P2NoHitGapMaxTicks = "p2_no_hit_gap_max_ticks";
        public const string P1BossReversalCleanCount = "p1_boss_reversal_clean_count";
        public const string P2BossReversalCleanCount = "p2_boss_reversal_clean_count";
        public const string P1KuraiC2Count = "p1_kurai_c2_count";
        public const string P2KuraiC2Count = "p2_kurai_c2_count";
        public const string TotalTicks = "total_ticks";
        public const string AnalysisVersion = "analysis_version";

        public static readonly string[] All = [RoundRecordId, P1SpellCapTicks, P2SpellCapTicks, P1SpellPointsMax, P2SpellPointsMax, P1SpellPointsMaxFrame, P2SpellPointsMaxFrame, P1SpellPointsTotal, P2SpellPointsTotal, P1GaugeAvg, P2GaugeAvg, P1GaugeMax, P2GaugeMax, P1ScoreGainRate, P2ScoreGainRate, P1HitsWithBossCandidate, P2HitsWithBossCandidate, P1HitsWithExCandidate, P2HitsWithExCandidate, P1HitsAmbiguous, P2HitsAmbiguous, P1BossPresentTicks, P2BossPresentTicks, P1CpuQuickTimerMax, P2CpuQuickTimerMax, P1CpuStandTimerMax, P2CpuStandTimerMax, P1CpuTimerFrozenTicks, P2CpuTimerFrozenTicks, P1NoHitGapMaxTicks, P2NoHitGapMaxTicks, P1BossReversalCleanCount, P2BossReversalCleanCount, P1KuraiC2Count, P2KuraiC2Count, TotalTicks, AnalysisVersion];
    }

    public static class Rounds
    {
        public const string Table = "rounds";
        public const string RoundRecordId = "round_record_id";
        public const string SessionId = "session_id";
        public const string StageRecordId = "stage_record_id";
        public const string RoundNumber = "round_number";
        public const string StartedAt = "started_at";
        public const string EndedAt = "ended_at";
        public const string Status = "status";
        public const string DurationFrames = "duration_frames";
        public const string WinnerSide = "winner_side";
        public const string Score1AtStart = "score_1_at_start";
        public const string Score2AtStart = "score_2_at_start";
        public const string Score1AtEnd = "score_1_at_end";
        public const string Score2AtEnd = "score_2_at_end";
        public const string Life1RawAtStart = "life_1_raw_at_start";
        public const string Life2RawAtStart = "life_2_raw_at_start";
        public const string Life1RawAtEnd = "life_1_raw_at_end";
        public const string Life2RawAtEnd = "life_2_raw_at_end";
        public const string Lives1AtStart = "lives_1_at_start";
        public const string Lives1AtEnd = "lives_1_at_end";
        public const string PauseUsed = "pause_used";

        public static readonly string[] All = [RoundRecordId, SessionId, StageRecordId, RoundNumber, StartedAt, EndedAt, Status, DurationFrames, WinnerSide, Score1AtStart, Score2AtStart, Score1AtEnd, Score2AtEnd, Life1RawAtStart, Life2RawAtStart, Life1RawAtEnd, Life2RawAtEnd, Lives1AtStart, Lives1AtEnd, PauseUsed];
    }

    public static class SessionMetadata
    {
        public const string Table = "session_metadata";
        public const string SessionId = "session_id";
        public const string GameMode = "game_mode";
        public const string Difficulty = "difficulty";
        public const string P1Character = "p1_character";
        public const string P2Character = "p2_character";
        public const string P1Control = "p1_control";
        public const string P2Control = "p2_control";
        public const string P1CpuLevel = "p1_cpu_level";
        public const string P2CpuLevel = "p2_cpu_level";
        public const string InitialLives = "initial_lives";
        public const string InitialLifeRaw = "initial_life_raw";
        public const string InitialLivesOptionModified = "initial_lives_option_modified";
        public const string FieldId = "field_id";
        public const string BattleBgmId = "battle_bgm_id";
        public const string ExecutionType = "execution_type";

        public static readonly string[] All = [SessionId, GameMode, Difficulty, P1Character, P2Character, P1Control, P2Control, P1CpuLevel, P2CpuLevel, InitialLives, InitialLifeRaw, InitialLivesOptionModified, FieldId, BattleBgmId, ExecutionType];
    }

    public static class SessionNetPlay
    {
        public const string Table = "session_net_play";
        public const string SessionId = "session_id";
        public const string RecordedAt = "recorded_at";
        public const string LocalName = "local_name";
        public const string RemoteName = "remote_name";
        public const string Seat = "seat";
        public const string ModuleBase = "module_base";
        public const string ReadStatus = "read_status";

        public static readonly string[] All = [SessionId, RecordedAt, LocalName, RemoteName, Seat, ModuleBase, ReadStatus];
    }

    public static class SessionReplays
    {
        public const string Table = "session_replays";
        public const string SessionId = "session_id";
        public const string ReplayId = "replay_id";
        public const string LinkConfidence = "link_confidence";
        public const string LinkMethod = "link_method";

        public static readonly string[] All = [SessionId, ReplayId, LinkConfidence, LinkMethod];
    }

    public static class Sessions
    {
        public const string Table = "sessions";
        public const string SessionId = "session_id";
        public const string StartedAt = "started_at";
        public const string EndedAt = "ended_at";
        public const string Status = "status";
        public const string LoggerVersion = "logger_version";
        public const string Notes = "notes";

        public static readonly string[] All = [SessionId, StartedAt, EndedAt, Status, LoggerVersion, Notes];
    }

    public static class Snapshots
    {
        public const string Table = "snapshots";
        public const string SnapshotId = "snapshot_id";
        public const string SessionId = "session_id";
        public const string WallTime = "wall_time";
        public const string PayloadJson = "payload_json";

        public static readonly string[] All = [SnapshotId, SessionId, WallTime, PayloadJson];
    }

    public static class Stages
    {
        public const string Table = "stages";
        public const string StageRecordId = "stage_record_id";
        public const string SessionId = "session_id";
        public const string StageNumber = "stage_number";
        public const string StartedAt = "started_at";
        public const string EndedAt = "ended_at";
        public const string Status = "status";
        public const string OpponentCharacter = "opponent_character";
        public const string FieldId = "field_id";
        public const string BattleBgmId = "battle_bgm_id";
        public const string ScoreAtStart = "score_at_start";
        public const string ScoreAtEnd = "score_at_end";
        public const string LivesAtStart = "lives_at_start";
        public const string LivesAtEnd = "lives_at_end";
        public const string WinnerSide = "winner_side";
        public const string ClearBonusId = "clear_bonus_id";
        public const string RngSeed = "rng_seed";

        public static readonly string[] All = [StageRecordId, SessionId, StageNumber, StartedAt, EndedAt, Status, OpponentCharacter, FieldId, BattleBgmId, ScoreAtStart, ScoreAtEnd, LivesAtStart, LivesAtEnd, WinnerSide, ClearBonusId, RngSeed];
    }

    public static readonly string[] AllTables = [ClearBonuses.Table, DbMeta.Table, DeletedSessions.Table, Events.Table, PlayerAliases.Table, Players.Table, ReplayPaths.Table, ReplayScanItems.Table, ReplayScanJobs.Table, Replays.Table, RoundMetrics.Table, Rounds.Table, SessionMetadata.Table, SessionNetPlay.Table, SessionReplays.Table, Sessions.Table, Snapshots.Table, Stages.Table];
}
