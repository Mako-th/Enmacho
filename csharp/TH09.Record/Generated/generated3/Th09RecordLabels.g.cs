#nullable enable

namespace TH09.Record.Generated;

using System.Collections.Frozen;

internal static class RecordLabels
{
    public static readonly System.Collections.Frozen.FrozenDictionary<int, string> Modes =
        new Dictionary<int, string>
        {
            [0] = "Story",
            [1] = "Extra",
            [2] = "Match",
        }.ToFrozenDictionary();

    public static readonly System.Collections.Frozen.FrozenDictionary<int, string> Difficulties =
        new Dictionary<int, string>
        {
            [0] = "Easy",
            [1] = "Normal",
            [2] = "Hard",
            [3] = "Lunatic",
            [4] = "Extra",
        }.ToFrozenDictionary();

    public static readonly System.Collections.Frozen.FrozenDictionary<int, string> Fields =
        new Dictionary<int, string>
        {
            [0] = "迷いの竹林",
            [1] = "幻草原",
            [2] = "白玉楼階段",
            [3] = "永遠亭",
            [4] = "霧の湖",
            [5] = "幽明結界",
            [6] = "妖怪獣道",
            [7] = "迷いの竹林",
            [8] = "太陽の畑",
            [9] = "大蝦蟇の池",
            [10] = "無名の丘",
            [11] = "再思の道",
            [12] = "無縁塚",
            [13] = "太陽の畑",
            [14] = "無名の丘",
            [15] = "無縁塚",
        }.ToFrozenDictionary();

    public static readonly System.Collections.Frozen.FrozenDictionary<int, string> Bgms =
        new Dictionary<int, string>
        {
            [0] = "春色小径 ～ Colorful Path",
            [1] = "オリエンタルダークフライト",
            [2] = "フラワリングナイト",
            [3] = "東方妖々夢 ～ Ancient Temple",
            [4] = "狂気の瞳 ～ Invisible Full Moon",
            [5] = "おてんば恋娘の冒険",
            [6] = "幽霊楽団 ～ Phantom Ensemble",
            [7] = "もう歌しか聞こえない ～ Flower Mix",
            [8] = "お宇佐さまの素い幡",
            [9] = "今昔幻想郷 ～ Flower Land",
            [10] = "風神少女 (Short Version)",
            [11] = "ポイズンボディ ～ Forsaken Doll",
            [12] = "彼岸帰航 ～ Riverside View",
            [13] = "六十年目の東方裁判 ～ Fate of Sixty Years",
        }.ToFrozenDictionary();

    public static readonly string[] Characters =
    [
        "霊夢",
        "魔理沙",
        "咲夜",
        "妖夢",
        "鈴仙",
        "チルノ",
        "リリカ",
        "ミスティア",
        "てゐ",
        "幽香",
        "文",
        "メディスン",
        "小町",
        "映姫",
        "メルラン",
        "ルナサ",
    ];

    public const int HitGaugeBonusFrames = 61;
    public const int HitGaugeBonusFramesDeath = 71;
    public const int HitClusterTicks = 4;

    public const int ComboGaugeSentinelMax = -900000;

    public const uint RequiredFlags = 5u;
    public const uint FlagReplayMgrValid = 8u;

    public const string ExecReplay = "Replay Playback";
    public const string ExecNet = "Network Play (Adonis)";
    public const string ExecLive = "Live Play";

    public const string SessionIdHighWater = "session_id_high_water";
    public const int TombstoneFormat = 1;
    public static readonly string[] TombstoneTables = ["sessions", "session_metadata", "session_replays", "session_net_play"];
    public static readonly (string Table, string Column)[] SessionRefNullable =
    [
        ("replay_scan_items", "session_id"),
    ];

    public const string UnknownStartedAt = "1970-01-01T00:00:00+00:00";
    public const string UnknownStatus = "restored";
    public const string RestoredNoteMark = "restored";
    public static readonly System.Collections.Frozen.FrozenDictionary<string, string> SessionCloseReason =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["completed"] = "aborted",
            ["disconnected"] = "disconnected",
            ["stopped"] = "stopped",
            ["timeout"] = "timeout",
        }.ToFrozenDictionary(StringComparer.Ordinal);


    public static readonly string[] SessionChildTables =
    [
        "session_replays",
        "rounds",
        "stages",
        "clear_bonuses",
        "events",
        "snapshots",
        "session_metadata",
        "session_net_play",
    ];

    public static readonly string[] RoundChildTables =
    [
        "round_metrics",
    ];

    public const int MinStageTimeFrames = 300;
    public const int OpponentKeyedMaxStage = 6;

    public const string NetSource = "Adonis";
    public const string NetExec = "Network Play (Adonis)";
    public const int ControlCpu = 1;
    public const string MatchRoundStatus = "completed";
    public const int NetSeatLocalIsP1 = 1;
    public const int NetSeatLocalIsP2 = 2;
    public const string BucketStory = "story";
    public const string BucketNet = "net";
    public const string BucketCpu = "cpu";
    public const string BucketLocal = "local";

    public const string ScanLinkMethod = "replay-scan";
    public const string MtimeLinkMethod = "mtime-nearest-session-end";

    public const int RoundMetricsAnalysisVersion = 1;
    public const long SpellPointsCap = 999990L;
    public const int TimerSentinel = -999999;
    public const int ZeroHitFreezeFrames = 360;
    public static readonly string[] RoundMetricsColumns = ["p1_spell_cap_ticks", "p2_spell_cap_ticks", "p1_spell_points_max", "p2_spell_points_max", "p1_spell_points_max_frame", "p2_spell_points_max_frame", "p1_spell_points_total", "p2_spell_points_total", "p1_gauge_avg", "p2_gauge_avg", "p1_gauge_max", "p2_gauge_max", "p1_score_gain_rate", "p2_score_gain_rate", "p1_hits_with_boss_candidate", "p2_hits_with_boss_candidate", "p1_hits_with_ex_candidate", "p2_hits_with_ex_candidate", "p1_hits_ambiguous", "p2_hits_ambiguous", "p1_boss_present_ticks", "p2_boss_present_ticks", "p1_cpu_quick_timer_max", "p2_cpu_quick_timer_max", "p1_cpu_stand_timer_max", "p2_cpu_stand_timer_max", "p1_cpu_timer_frozen_ticks", "p2_cpu_timer_frozen_ticks", "p1_no_hit_gap_max_ticks", "p2_no_hit_gap_max_ticks", "p1_boss_reversal_clean_count", "p2_boss_reversal_clean_count", "p1_kurai_c2_count", "p2_kurai_c2_count", "total_ticks", "analysis_version"];
    public static readonly string[] RoundMetricsUncomputedColumns = ["p1_hits_ambiguous", "p1_hits_with_boss_candidate", "p1_hits_with_ex_candidate", "p2_hits_ambiguous", "p2_hits_with_boss_candidate", "p2_hits_with_ex_candidate"];
}
