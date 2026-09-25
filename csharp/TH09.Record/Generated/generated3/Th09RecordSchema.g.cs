#nullable enable

namespace TH09.Record.Generated;

internal static class RecordSchema
{
    public const int TableCount = 18;
    public const int IndexCount = 10;

    public const string Sha256 = "992cb3e34756443a742db8d982704e36044988d64fb8b8f4f7b1c997484c7b75";

    public static readonly string[] Statements =
    [
        "CREATE TABLE db_meta(key TEXT PRIMARY KEY, value TEXT)",
        "CREATE TABLE deleted_sessions(session_id INTEGER PRIMARY KEY, deleted_at TEXT NOT NULL, payload_json TEXT NOT NULL)",
        "CREATE TABLE sessions (\n session_id INTEGER PRIMARY KEY, started_at TEXT NOT NULL, ended_at TEXT,\n status TEXT NOT NULL, logger_version TEXT, notes TEXT\n)",
        "CREATE TABLE events (\n event_id INTEGER PRIMARY KEY, session_id INTEGER NOT NULL REFERENCES sessions(session_id),\n wall_time TEXT NOT NULL, event_type TEXT NOT NULL, game_frame INTEGER,\n side INTEGER, payload_json TEXT NOT NULL\n)",
        "CREATE INDEX idx_events_session_time ON events(session_id, event_id)",
        "CREATE INDEX idx_events_session_type ON events(session_id, event_type)",
        "CREATE TABLE snapshots (\n snapshot_id INTEGER PRIMARY KEY, session_id INTEGER NOT NULL REFERENCES sessions(session_id),\n wall_time TEXT NOT NULL, payload_json TEXT NOT NULL\n)",
        "CREATE INDEX idx_snapshots_session_time ON snapshots(session_id, snapshot_id)",
        "CREATE TABLE clear_bonuses (\n clear_bonus_id INTEGER PRIMARY KEY,\n session_id INTEGER NOT NULL REFERENCES sessions(session_id),\n wall_time TEXT NOT NULL,\n mode INTEGER NOT NULL,\n stage_index INTEGER,\n winner_side INTEGER,\n life_raw INTEGER,\n maximum_combo INTEGER,\n spell_attack_count INTEGER,\n boss_attack_count INTEGER,\n boss_reversal_count INTEGER,\n remaining_players REAL,\n life_bonus INTEGER NOT NULL,\n maximum_combo_bonus INTEGER NOT NULL,\n spell_attack_bonus INTEGER NOT NULL,\n boss_attack_bonus INTEGER NOT NULL,\n boss_reversal_bonus INTEGER NOT NULL,\n remaining_players_bonus INTEGER NOT NULL,\n total_bonus INTEGER NOT NULL,\n score_before_bonus INTEGER,\n score_after_bonus INTEGER\n)",
        "CREATE INDEX idx_clear_bonuses_session ON clear_bonuses(session_id, clear_bonus_id)",
        "CREATE TABLE session_metadata (\n session_id INTEGER PRIMARY KEY REFERENCES sessions(session_id),\n game_mode INTEGER NOT NULL,\n difficulty INTEGER NOT NULL,\n p1_character INTEGER NOT NULL,\n p2_character INTEGER NOT NULL,\n p1_control INTEGER NOT NULL,\n p2_control INTEGER NOT NULL,\n p1_cpu_level INTEGER,\n p2_cpu_level INTEGER,\n initial_lives REAL,\n initial_life_raw INTEGER,\n initial_lives_option_modified INTEGER,\n field_id INTEGER,\n battle_bgm_id INTEGER,\n execution_type TEXT\n)",
        "CREATE TABLE session_net_play (\n session_id INTEGER PRIMARY KEY REFERENCES sessions(session_id),\n recorded_at TEXT NOT NULL,\n local_name TEXT,\n remote_name TEXT,\n seat INTEGER,\n module_base INTEGER,\n read_status TEXT\n)",
        "CREATE TABLE players (\n player_id INTEGER PRIMARY KEY,\n display_name TEXT NOT NULL,\n is_self INTEGER NOT NULL DEFAULT 0,\n notes TEXT,\n created_at TEXT\n)",
        "CREATE TABLE player_aliases (\n name TEXT PRIMARY KEY,\n player_id INTEGER NOT NULL REFERENCES players(player_id),\n first_seen_at TEXT,\n source TEXT\n)",
        "CREATE TABLE stages (\n stage_record_id INTEGER PRIMARY KEY,\n session_id INTEGER NOT NULL REFERENCES sessions(session_id),\n stage_number INTEGER,\n started_at TEXT NOT NULL,\n ended_at TEXT,\n status TEXT NOT NULL,\n opponent_character INTEGER,\n field_id INTEGER,\n battle_bgm_id INTEGER,\n score_at_start INTEGER,\n score_at_end INTEGER,\n lives_at_start REAL,\n lives_at_end REAL,\n winner_side INTEGER,\n clear_bonus_id INTEGER REFERENCES clear_bonuses(clear_bonus_id),\n -- ステージ開始時の RNG state(u16)。**採取点は未確定で、いまライブ側は書いていない。**\n -- 由来と注意（1024ステップのずれ）は th09_repository.py の STAGE_COLUMNS に集約してある。\n -- 既存DBへは th09_repository.migrate() が同じ列を足す（片方だけ直さないこと）。\n rng_seed INTEGER\n)",
        "CREATE INDEX idx_stages_session ON stages(session_id, stage_record_id)",
        "CREATE TABLE rounds (\n round_record_id INTEGER PRIMARY KEY,\n session_id INTEGER NOT NULL REFERENCES sessions(session_id),\n stage_record_id INTEGER REFERENCES stages(stage_record_id),\n round_number INTEGER NOT NULL,\n started_at TEXT NOT NULL,\n ended_at TEXT,\n status TEXT NOT NULL,\n duration_frames INTEGER,\n winner_side INTEGER,\n score_1_at_start INTEGER,\n score_2_at_start INTEGER,\n score_1_at_end INTEGER,\n score_2_at_end INTEGER,\n life_1_raw_at_start INTEGER,\n life_2_raw_at_start INTEGER,\n life_1_raw_at_end INTEGER,\n life_2_raw_at_end INTEGER,\n lives_1_at_start REAL,\n lives_1_at_end REAL,\n pause_used INTEGER NOT NULL DEFAULT 0\n)",
        "CREATE INDEX idx_rounds_session ON rounds(session_id, round_record_id)",
        "CREATE INDEX idx_rounds_stage ON rounds(stage_record_id)",
        "CREATE TABLE round_metrics (\n round_record_id INTEGER PRIMARY KEY REFERENCES rounds(round_record_id),\n p1_spell_cap_ticks INTEGER, p2_spell_cap_ticks INTEGER,\n p1_spell_points_max INTEGER, p2_spell_points_max INTEGER,\n p1_spell_points_max_frame INTEGER, p2_spell_points_max_frame INTEGER,\n p1_spell_points_total INTEGER, p2_spell_points_total INTEGER,\n p1_gauge_avg REAL, p2_gauge_avg REAL,\n p1_gauge_max REAL, p2_gauge_max REAL,\n p1_score_gain_rate REAL, p2_score_gain_rate REAL,\n p1_hits_with_boss_candidate INTEGER, p2_hits_with_boss_candidate INTEGER,\n p1_hits_with_ex_candidate INTEGER, p2_hits_with_ex_candidate INTEGER,\n p1_hits_ambiguous INTEGER, p2_hits_ambiguous INTEGER,\n p1_boss_present_ticks INTEGER, p2_boss_present_ticks INTEGER,\n p1_cpu_quick_timer_max INTEGER, p2_cpu_quick_timer_max INTEGER,\n p1_cpu_stand_timer_max INTEGER, p2_cpu_stand_timer_max INTEGER,\n p1_cpu_timer_frozen_ticks INTEGER, p2_cpu_timer_frozen_ticks INTEGER,\n p1_no_hit_gap_max_ticks INTEGER, p2_no_hit_gap_max_ticks INTEGER,\n p1_boss_reversal_clean_count INTEGER, p2_boss_reversal_clean_count INTEGER,\n p1_kurai_c2_count INTEGER, p2_kurai_c2_count INTEGER,\n total_ticks INTEGER,\n analysis_version INTEGER\n)",
        "CREATE TABLE replays(replay_id INTEGER PRIMARY KEY,sha256 TEXT NOT NULL UNIQUE,file_size INTEGER NOT NULL,mtime TEXT,first_seen_at TEXT NOT NULL,last_seen_at TEXT NOT NULL,source TEXT,mode INTEGER,difficulty INTEGER,player_name TEXT,replay_date TEXT,p1_char INTEGER,p2_char INTEGER,p1_name TEXT,p2_name TEXT,is_own INTEGER,owner_side INTEGER,decode_status TEXT,decoded_json TEXT,own_override INTEGER)",
        "CREATE TABLE replay_paths(replay_path_id INTEGER PRIMARY KEY,replay_id INTEGER NOT NULL REFERENCES replays(replay_id),full_path TEXT NOT NULL UNIQUE,filename TEXT NOT NULL,parent_hint TEXT,first_seen_at TEXT NOT NULL,last_seen_at TEXT NOT NULL,is_current INTEGER NOT NULL DEFAULT 1)",
        "CREATE INDEX idx_replay_paths_replay ON replay_paths(replay_id, is_current, replay_path_id)",
        "CREATE TABLE session_replays(session_id INTEGER NOT NULL REFERENCES sessions(session_id),replay_id INTEGER NOT NULL REFERENCES replays(replay_id),link_confidence REAL,link_method TEXT,PRIMARY KEY(session_id,replay_id))",
        "CREATE TABLE replay_scan_jobs (\n job_id INTEGER PRIMARY KEY,\n started_at TEXT NOT NULL,\n ended_at TEXT,\n status TEXT NOT NULL,\n speed_setting TEXT,\n note TEXT\n)",
        "CREATE TABLE replay_scan_items (\n item_id INTEGER PRIMARY KEY,\n job_id INTEGER NOT NULL REFERENCES replay_scan_jobs(job_id),\n replay_id INTEGER NOT NULL REFERENCES replays(replay_id),\n started_at TEXT,\n ended_at TEXT,\n status TEXT NOT NULL,\n session_id INTEGER REFERENCES sessions(session_id),\n verify_status TEXT,\n gap_count INTEGER,\n attempt INTEGER NOT NULL DEFAULT 1,\n error TEXT\n, hit_windows INTEGER, hit_window_ticks INTEGER, hit_window_bytes INTEGER, hit_window_lost_ticks INTEGER, hit_window_min_slack INTEGER, hits_seen INTEGER, quicks_seen INTEGER, coord_ticks INTEGER, hit_window_verify_failures INTEGER, layer0_segments INTEGER, layer0_ticks INTEGER, layer0_bytes INTEGER, layer0_lost INTEGER, layer0_torn INTEGER, layer0_verify_failures INTEGER, hit_window_skipped_hits INTEGER)",
        "CREATE INDEX idx_scan_items_job ON replay_scan_items(job_id, item_id)",
        "CREATE INDEX idx_scan_items_replay ON replay_scan_items(replay_id, item_id)",
    ];

    public static readonly string[] Tables = ["db_meta", "deleted_sessions", "sessions", "events", "snapshots", "clear_bonuses", "session_metadata", "session_net_play", "players", "player_aliases", "stages", "rounds", "round_metrics", "replays", "replay_paths", "session_replays", "replay_scan_jobs", "replay_scan_items"];

    public static readonly (string Table, string Column, string Decl)[] PendingColumns =
    [
        ("replay_scan_items", "coord_ticks", "INTEGER"),
        ("replay_scan_items", "hit_window_bytes", "INTEGER"),
        ("replay_scan_items", "hit_window_lost_ticks", "INTEGER"),
        ("replay_scan_items", "hit_window_min_slack", "INTEGER"),
        ("replay_scan_items", "hit_window_skipped_hits", "INTEGER"),
        ("replay_scan_items", "hit_window_ticks", "INTEGER"),
        ("replay_scan_items", "hit_window_verify_failures", "INTEGER"),
        ("replay_scan_items", "hit_windows", "INTEGER"),
        ("replay_scan_items", "hits_seen", "INTEGER"),
        ("replay_scan_items", "layer0_bytes", "INTEGER"),
        ("replay_scan_items", "layer0_lost", "INTEGER"),
        ("replay_scan_items", "layer0_segments", "INTEGER"),
        ("replay_scan_items", "layer0_ticks", "INTEGER"),
        ("replay_scan_items", "layer0_torn", "INTEGER"),
        ("replay_scan_items", "layer0_verify_failures", "INTEGER"),
        ("replay_scan_items", "quicks_seen", "INTEGER"),
        ("replays", "decode_status", "TEXT"),
        ("replays", "decoded_json", "TEXT"),
        ("replays", "difficulty", "INTEGER"),
        ("replays", "is_own", "INTEGER"),
        ("replays", "mode", "INTEGER"),
        ("replays", "own_override", "INTEGER"),
        ("replays", "owner_side", "INTEGER"),
        ("replays", "p1_char", "INTEGER"),
        ("replays", "p1_name", "TEXT"),
        ("replays", "p2_char", "INTEGER"),
        ("replays", "p2_name", "TEXT"),
        ("replays", "player_name", "TEXT"),
        ("replays", "replay_date", "TEXT"),
        ("session_metadata", "execution_type", "TEXT"),
        ("stages", "rng_seed", "INTEGER"),
    ];
}
