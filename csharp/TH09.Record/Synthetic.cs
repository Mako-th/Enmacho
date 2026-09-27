using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public static class Synthetic
{
    public static List<(string Table, int Rows)> Build(string path)
    {
        using var db = Schema.CreateFrom(path);
        var conn = db.Connection;
        using var tx = conn.BeginTransaction();
        var counts = new List<(string, int)>();

        Ins(conn, tx, "INSERT INTO sessions(session_id,started_at,ended_at,status,logger_version,notes)"
                    + " VALUES(@a,@b,@c,@d,@e,@f)",
            [1L, "2026-01-01T00:00:00+09:00", "2026-01-01T00:10:00+09:00", "completed", "synthetic-v1", null]);
        Ins(conn, tx, "INSERT INTO sessions(session_id,started_at,ended_at,status,logger_version,notes)"
                    + " VALUES(@a,@b,@c,@d,@e,@f)",
            [2L, "2026-01-01T01:00:00+09:00", null, "completed", "synthetic-v1", new byte[] { 0x00, 0x7F, 0xFF, 0x10 }]);
        Ins(conn, tx, "INSERT INTO sessions(session_id,started_at,ended_at,status,logger_version,notes)"
                    + " VALUES(@a,@b,@c,@d,@e,@f)",
            [3L, "2026-01-01T02:00:00+09:00", null, "running", "synthetic-v1", "この行は外れる"]);
        Ins(conn, tx, "INSERT INTO sessions(session_id,started_at,ended_at,status,logger_version,notes)"
                    + " VALUES(@a,@b,@c,@d,@e,@f)",
            [4L, "2026-01-01T03:00:00+09:00", null, "completed", "synthetic-v1", "コーパス外"]);
        counts.Add(("sessions", 4));

        Ins(conn, tx, "INSERT INTO session_metadata(session_id,game_mode,difficulty,p1_character,"
                    + "p2_character,p1_control,p2_control,p1_cpu_level,p2_cpu_level,initial_lives,"
                    + "initial_life_raw,initial_lives_option_modified,field_id,battle_bgm_id,execution_type)"
                    + " VALUES(@a,@b,@c,@d,@e,@f,@g,@h,@i,@j,@k,@l,@m,@n,@o)",
            [1L, 1L, 2L, 3L, 4L, 0L, 1L, null, 5L, 2.5, 5L, 0L, 8L, 12L, ""]);
        Ins(conn, tx, "INSERT INTO session_metadata(session_id,game_mode,difficulty,p1_character,"
                    + "p2_character,p1_control,p2_control,p1_cpu_level,p2_cpu_level,initial_lives,"
                    + "initial_life_raw,initial_lives_option_modified,field_id,battle_bgm_id,execution_type)"
                    + " VALUES(@a,@b,@c,@d,@e,@f,@g,@h,@i,@j,@k,@l,@m,@n,@o)",
            [2L, 1L, 2L, 3L, 4L, 0L, 1L, -1L, 9007199254740993L, -0.0,
             5L, 0L, 8L, 12L, "a\tb\r\nc\\d\U00020BB7"]);
        counts.Add(("session_metadata", 2));

        Ins(conn, tx, "INSERT INTO session_net_play(session_id,recorded_at,local_name,remote_name,"
                    + "seat,module_base,read_status) VALUES(@a,@b,@c,@d,@e,@f,@g)",
            [1L, "2026-01-01T00:00:01+09:00", "自分", "†", 1L, 123456789L, "ok"]);
        counts.Add(("session_net_play", 1));

        Ins(conn, tx, "INSERT INTO clear_bonuses(clear_bonus_id,session_id,wall_time,mode,stage_index,"
                    + "winner_side,life_raw,maximum_combo,spell_attack_count,boss_attack_count,"
                    + "boss_reversal_count,remaining_players,life_bonus,maximum_combo_bonus,"
                    + "spell_attack_bonus,boss_attack_bonus,boss_reversal_bonus,remaining_players_bonus,"
                    + "total_bonus,score_before_bonus,score_after_bonus)"
                    + " VALUES(@a,@b,@c,@d,@e,@f,@g,@h,@i,@j,@k,@l,@m,@n,@o,@p,@q,@r,@s,@t,@u)",
            [10L, 1L, "2026-01-01T00:05:00+09:00", 1L, 0L, 1L, 4L, 12L, 3L, 2L, 1L,
             double.PositiveInfinity, 1000L, 2000L, 3000L, 4000L, 5000L, 6000L, 21000L,
             9007199254740993L, -1L]);
        counts.Add(("clear_bonuses", 1));

        Ins(conn, tx, "INSERT INTO stages(stage_record_id,session_id,stage_number,started_at,ended_at,"
                    + "status,opponent_character,field_id,battle_bgm_id,score_at_start,score_at_end,"
                    + "lives_at_start,lives_at_end,winner_side,clear_bonus_id,rng_seed)"
                    + " VALUES(@a,@b,@c,@d,@e,@f,@g,@h,@i,@j,@k,@l,@m,@n,@o,@p)",
            [20L, 1L, 1L, "2026-01-01T00:00:10+09:00", "2026-01-01T00:05:00+09:00", "completed",
             5L, 8L, 12L, 0L, 12345L, 2.5, 2.0, 1L, 10L, null]);
        Ins(conn, tx, "INSERT INTO stages(stage_record_id,session_id,stage_number,started_at,ended_at,"
                    + "status,opponent_character,field_id,battle_bgm_id,score_at_start,score_at_end,"
                    + "lives_at_start,lives_at_end,winner_side,clear_bonus_id,rng_seed)"
                    + " VALUES(@a,@b,@c,@d,@e,@f,@g,@h,@i,@j,@k,@l,@m,@n,@o,@p)",
            [21L, 2L, 1L, "2026-01-01T01:00:10+09:00", null, "aborted",
             null, null, null, 0L, 0L, double.Epsilon, -0.0, null, null, 65535L]);
        counts.Add(("stages", 2));

        Ins(conn, tx, "INSERT INTO rounds(round_record_id,session_id,stage_record_id,round_number,"
                    + "started_at,ended_at,status,duration_frames,winner_side,score_1_at_start,"
                    + "score_2_at_start,score_1_at_end,score_2_at_end,life_1_raw_at_start,"
                    + "life_2_raw_at_start,life_1_raw_at_end,life_2_raw_at_end,lives_1_at_start,"
                    + "lives_1_at_end,pause_used) VALUES(@a,@b,@c,@d,@e,@f,@g,@h,@i,@j,@k,@l,@m,@n,@o,@p,@q,@r,@s,@t)",
            [30L, 1L, 20L, 1L, "2026-01-01T00:00:10+09:00", "2026-01-01T00:02:00+09:00", "completed",
             6000L, 1L, 0L, 0L, 12345L, 999L, 8L, 8L, 8L, 0L, 2.5, 2.0, 1L]);
        counts.Add(("rounds", 1));

        var rm = new List<object?> { 30L };
        for (var i = 0; i < 36; i++) rm.Add(null);
        rm[9] = 0.1 + 0.2;
        rm[10] = -0.0;
        rm[11] = double.MaxValue;
        rm[12] = double.Epsilon;
        rm[13] = 1.0 / 3.0;
        rm[14] = double.PositiveInfinity;
        rm[35] = 240L;
        rm[36] = 3L;
        var rmCols = "round_record_id,p1_spell_cap_ticks,p2_spell_cap_ticks,p1_spell_points_max,"
                   + "p2_spell_points_max,p1_spell_points_max_frame,p2_spell_points_max_frame,"
                   + "p1_spell_points_total,p2_spell_points_total,p1_gauge_avg,p2_gauge_avg,"
                   + "p1_gauge_max,p2_gauge_max,p1_score_gain_rate,p2_score_gain_rate,"
                   + "p1_hits_with_boss_candidate,p2_hits_with_boss_candidate,p1_hits_with_ex_candidate,"
                   + "p2_hits_with_ex_candidate,p1_hits_ambiguous,p2_hits_ambiguous,"
                   + "p1_boss_present_ticks,p2_boss_present_ticks,p1_cpu_quick_timer_max,"
                   + "p2_cpu_quick_timer_max,p1_cpu_stand_timer_max,p2_cpu_stand_timer_max,"
                   + "p1_cpu_timer_frozen_ticks,p2_cpu_timer_frozen_ticks,p1_no_hit_gap_max_ticks,"
                   + "p2_no_hit_gap_max_ticks,p1_boss_reversal_clean_count,p2_boss_reversal_clean_count,"
                   + "p1_kurai_c2_count,p2_kurai_c2_count,total_ticks,analysis_version";
        Ins(conn, tx, $"INSERT INTO round_metrics({rmCols}) VALUES("
                      + string.Join(",", Enumerable.Range(0, 37).Select(i => "@v" + i)) + ")",
            rm, "@v");
        counts.Add(("round_metrics", 1));

        var payloads = new (long Id, long Session, string Json)[]
        {
            (40, 1, """{"delta":1,"side":2}"""),
            (41, 1, "{oops"),
            (42, 1, """{"nan":NaN,"inf":Infinity,"ninf":-Infinity}"""),
            (43, 1, """{"big":123456789012345678901234567890}"""),
            (44, 1, """{"a":1,"a":2}"""),
            (45, 1, "{\"\u3042\":1,\"\U00020BB7\":2,\"\ue000\":3,\"z\":4}"),
            (46, 1, "123"),
            (47, 1, "\"\\u0000\\u007f\\ud800\""),
            (48, 1, "null"),
            (49, 1, "[]"),
            (50, 1, "{}"),
            (51, 1, """{"e":[1.0,1e400,-0.0,5e-324,0.1]}"""),
            (52, 1, "  \t\r\n {\"pad\":1} \n"),
            (53, 2, "{\"tab\":\"a\\tb\",\"nl\":\"a\\nb\"}"),
        };
        foreach (var (id, session, json) in payloads)
        {
            Ins(conn, tx, "INSERT INTO events(event_id,session_id,wall_time,event_type,game_frame,"
                        + "side,payload_json) VALUES(@a,@b,@c,@d,@e,@f,@g)",
                [id, session, "2026-01-01T00:00:20+09:00", "SYNTH", 1234L, 1L, json]);
        }
        Ins(conn, tx, "INSERT INTO events(event_id,session_id,wall_time,event_type,game_frame,"
                    + "side,payload_json) VALUES(@a,@b,@c,@d,@e,@f,@g)",
            [54L, 1L, "2026-01-01T00:00:21+09:00", "SYNTH_BLOB", null, null,
             System.Text.Encoding.UTF8.GetBytes("""{"blob":true}""")]);
        counts.Add(("events", payloads.Length + 1));

        var corpus = ParitySpec.CorpusReplaySha256;
        Ins(conn, tx, ReplaySql,
            [60L, corpus[0], 1234L, "2026-01-01T00:00:00.123456+09:00", "x", "y", "Game",
             1L, 2L, "名前\t改行\n", "2026/01/01", 3L, 4L, "", "\U00020BB7", 1L, 2L, "decoded",
             """{"ok":true,"f":0.30000000000000004}""", null]);
        Ins(conn, tx, ReplaySql,
            [61L, corpus[1], 0L, null, "x", "y", "Adonis",
             null, null, null, null, null, null, null, null, null, null, "decode_failed", null, 1L]);
        Ins(conn, tx, ReplaySql,
            [62L, corpus[2], 2L, "2026-01-01T00:00:00+09:00", "x", "y", "Adonis",
             1L, 2L, "p", "d", 3L, 4L, "a", "b", 0L, 1L, "decoded", "こわれた JSON", 0L]);
        Ins(conn, tx, ReplaySql,
            [63L, corpus[3], 3L, "2026-01-01T00:00:00.5", "x", "y", "Game",
             1L, 2L, "p", "d", 3L, 4L, "a", "b", 1L, 1L, "decoded", "[1,2,3]", null]);
        Ins(conn, tx, ReplaySql,
            [64L, "0000000000000000000000000000000000000000000000000000000000000000", 4L,
             "2026-01-01T00:00:00+09:00", "x", "y", "Game",
             1L, 2L, "外", "d", 3L, 4L, "a", "b", 1L, 1L, "decoded", "{}", null]);
        for (var i = 4; i < corpus.Length; i++)
        {
            Ins(conn, tx, ReplaySql,
                [65L + i - 4, corpus[i], (long)i, "2026-01-01T00:00:00+09:00", "x", "y", "Game",
                 1L, 2L, "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                 "d", 3L, 4L, "a", "b", 1L, 1L, "decoded", "{}", null]);
        }
        counts.Add(("replays", 5 + corpus.Length - 4));

        var paths = new (long Id, long Replay, string Full, string Name, string? Hint)[]
        {
            (70, 60, @"C:\a\b\th9_01.rpy", "th9_01.rpy", "replay"),
            (71, 60, "/mnt/c/a/b/th9_02.rpy", "th9_02.rpy", null),
            (72, 61, "th9_03.rpy", "th9_03.rpy", ""),
            (73, 64, @"C:\out\th9_99.rpy", "th9_99.rpy", "out"),
        };
        foreach (var (id, replay, full, name, hint) in paths)
        {
            Ins(conn, tx, "INSERT INTO replay_paths(replay_path_id,replay_id,full_path,filename,"
                        + "parent_hint,first_seen_at,last_seen_at,is_current) VALUES(@a,@b,@c,@d,@e,@f,@g,@h)",
                [id, replay, full, name, hint, "x", "y", 1L]);
        }
        counts.Add(("replay_paths", paths.Length));

        foreach (var (session, replay) in new (long, long)[] { (1, 60), (1, 61), (2, 62), (2, 63), (4, 64) })
        {
            Ins(conn, tx, "INSERT INTO session_replays(session_id,replay_id,link_confidence,link_method)"
                        + " VALUES(@a,@b,@c,@d)", [session, replay, 1.0, "replay-scan"]);
        }
        counts.Add(("session_replays", 5));

        Ins(conn, tx, "INSERT INTO snapshots(snapshot_id,session_id,wall_time,payload_json)"
                    + " VALUES(@a,@b,@c,@d)", [80L, 1L, "x", "{}"]);
        Ins(conn, tx, "INSERT INTO deleted_sessions(session_id,deleted_at,payload_json)"
                    + " VALUES(@a,@b,@c)", [999L, "x", "{}"]);
        Ins(conn, tx, "INSERT INTO replay_scan_jobs(job_id,started_at,ended_at,status,speed_setting,note)"
                    + " VALUES(@a,@b,@c,@d,@e,@f)", [90L, "x", "y", "done", 4L, null]);
        Ins(conn, tx, "INSERT INTO replay_scan_items(item_id,job_id,replay_id,started_at,status)"
                    + " VALUES(@a,@b,@c,@d,@e)", [91L, 90L, 60L, "x", "done"]);
        counts.Add(("snapshots", 1));
        counts.Add(("deleted_sessions", 1));
        counts.Add(("replay_scan_jobs", 1));
        counts.Add(("replay_scan_items", 1));

        Ins(conn, tx, "INSERT INTO db_meta(key,value) VALUES(@a,@b)", ["session_id_high_water", "4"]);
        Ins(conn, tx, "INSERT INTO db_meta(key,value) VALUES(@a,@b)", ["synthetic_extra", "12"]);
        counts.Add(("db_meta", 2));

        Ins(conn, tx, "INSERT INTO players(player_id,display_name,is_self,notes,created_at)"
                    + " VALUES(@a,@b,@c,@d,@e)", [100L, "自分", 1L, null, "x"]);
        Ins(conn, tx, "INSERT INTO players(player_id,display_name,is_self,notes,created_at)"
                    + " VALUES(@a,@b,@c,@d,@e)", [101L, "\U00020BB7", 0L, "非 BMP の表示名", "y"]);
        counts.Add(("players", 2));

        Ins(conn, tx, "INSERT INTO player_aliases(name,player_id,first_seen_at,source)"
                    + " VALUES(@a,@b,@c,@d)", ["†", 100L, "x", "replay"]);
        Ins(conn, tx, "INSERT INTO player_aliases(name,player_id,first_seen_at,source)"
                    + " VALUES(@a,@b,@c,@d)", ["", 101L, null, null]);
        counts.Add(("player_aliases", 2));

        tx.Commit();
        return counts;
    }

    private const string ReplaySql =
        "INSERT INTO replays(replay_id,sha256,file_size,mtime,first_seen_at,last_seen_at,source,"
        + "mode,difficulty,player_name,replay_date,p1_char,p2_char,p1_name,p2_name,is_own,owner_side,"
        + "decode_status,decoded_json,own_override)"
        + " VALUES(@a,@b,@c,@d,@e,@f,@g,@h,@i,@j,@k,@l,@m,@n,@o,@p,@q,@r,@s,@t)";

    private static void Ins(SqliteConnection conn, SqliteTransaction tx, string sql,
                            IReadOnlyList<object?> values, string prefix = "@")
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        for (var i = 0; i < values.Count; i++)
        {
            var name = prefix == "@" ? "@" + (char)('a' + i) : prefix + i;
            cmd.Parameters.AddWithValue(name, values[i] ?? DBNull.Value);
        }
        cmd.ExecuteNonQuery();
    }
}
