using System.Globalization;
using System.Text;
using TH09.Record.Generated;

namespace TH09.Record;

public static class SyntheticTicks
{
    public const string FormatVersion = "th09-record-syntick-v1";

    public sealed record Case(string Name, string Why, List<uint[]> Rows);

    public sealed class Tick
    {
        private readonly Dictionary<string, uint> _w;

        public Tick()
        {
            _w = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (var name in TickReplay.NeededWords) _w[name] = 0;
            _w["flags"] = RecordLabels.RequiredFlags;
        }

        private Tick(Dictionary<string, uint> w) => _w = new Dictionary<string, uint>(w, StringComparer.Ordinal);

        public Tick Next() => new(_w);

        public Tick Set(string word, long value)
        {
            if (!_w.ContainsKey(word))
                throw new KeyNotFoundException($"合成に無い語です: {word}"
                    + "（TickReplay.NeededWords に足すか、綴りを見直すこと）");
            _w[word] = unchecked((uint)value);
            return this;
        }

        public Tick SetFloat(string word, float value) =>
            Set(word, BitConverter.SingleToUInt32Bits(value));

        public Tick SetBits(string word, uint bits) => Set(word, bits);

        public long Get(string word) => _w[word];

        public uint[] Row() => [.. TickReplay.NeededWords.Select(n => _w[n])];
    }

    private static Tick Base(long mode)
    {
        var t = new Tick()
            .Set("mode", mode)
            .Set("difficulty", 1)
            .Set("stage_index", mode == 2 ? 9 : 0)
            .Set("field_id", 3)
            .Set("battle_bgm_id", 4)
            .Set("p1_character", 0)
            .Set("p2_character", 1)
            .Set("p1_control", 0)
            .Set("p2_control", 1)
            .Set("p1_cpu_level", 2)
            .Set("p2_cpu_level", 3)
            .Set("rounds_required", mode == 2 ? 2 : 1)
            .Set("p1_life_raw", 10)
            .Set("p2_life_raw", 10)
            .Set("p1_spell_points", 5)
            .Set("p2_spell_points", 5)
            .Set("p1_combo_gauge_raw", 20)
            .Set("p2_combo_gauge_raw", 20)
            .SetFloat("p1_gauge", 100.0f)
            .SetFloat("p2_gauge", 120.0f)
            .SetFloat("p1_lives", 3.0f)
            .SetFloat("p2_lives", 3.0f)
            .Set("p1_score_raw", 1000)
            .Set("p2_score_raw", 2000);
        return t;
    }

    private static Tick Run(List<uint[]> rows, Tick t, int n)
    {
        for (var i = 0; i < n; i++)
        {
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1);
            rows.Add(t.Row());
        }
        return t;
    }

    public static List<Case> All()
    {
        var cases = new List<Case>();

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 5);
            t = t.Next().Set("pause_used", 1).Set("round_frames", t.Get("round_frames") + 1);
            rows.Add(t.Row());
            t = Run(rows, t, 5);
            t = t.Next().Set("result_state", 1);
            rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 1).Set("p1_wins", 1);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            cases.Add(new Case("pause_used", "ポーズを挟んだ回（★rounds.pause_used=1 は実データ 0 件）", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(2);
            rows.Add(t.Row());
            t = Run(rows, t, 4);
            t = t.Next().Set("result_state", 1); rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 1).Set("p2_wins", 1); rows.Add(t.Row());
            t = t.Next().Set("result_state", 0).Set("round_frames", 0); rows.Add(t.Row());
            t = Run(rows, t, 4);
            t = t.Next().Set("result_state", 1); rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 2).Set("p1_wins", 1); rows.Add(t.Row());
            t = t.Next().Set("result_state", 0).Set("round_frames", 0); rows.Add(t.Row());
            t = Run(rows, t, 4);
            t = t.Next().Set("result_state", 1); rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 3).Set("p1_wins", 2); rows.Add(t.Row());
            t = t.Next().Set("result_state", 2).Set("result_winner", 0)
                 .Set("clear_life_bonus", 1000).Set("clear_max_combo_bonus", 2000)
                 .Set("clear_spell_bonus", 300).Set("clear_boss_bonus", 400)
                 .Set("clear_reversal_bonus", 500).Set("clear_lives_bonus", 600)
                 .Set("clear_total", 4800);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            cases.Add(new Case("match_三本勝負", "Match の 2 本先取と MATCH_FINAL", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            t = t.Next().Set("result_state", 1); rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 1).Set("p2_wins", 1); rows.Add(t.Row());
            t = t.Next().Set("result_state", 0).Set("round_frames", 0)
                 .SetFloat("p1_lives", 2.0f);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            t = t.Next().Set("result_state", 1); rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 2).Set("p1_wins", 1); rows.Add(t.Row());
            t = Run(rows, t, 2);
            cases.Add(new Case("story_2P勝ち", "Story で 2P が取った回（R2 が開くか）", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1)
                 .Set("p1_life_raw", 4)
                 .Set("p1_spell_points", 0)
                 .Set("p1_combo_gauge_raw", unchecked((uint)-999999));
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("p1_spell_attacks", 1).Set("round_frames", t.Get("round_frames") + 1);
            rows.Add(t.Row());
            for (var i = 0; i < 70; i++)
            {
                t = t.Next().Set("round_frames", t.Get("round_frames") + 1)
                     .SetFloat("p1_gauge", 100.0f + i);
                rows.Add(t.Row());
            }
            cases.Add(new Case("被弾_生存", "生存被弾（+61）＋喰らい C2 ＋番兵のコンボゲージ", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1)
                 .Set("p1_life_raw", 0).Set("completed_rounds", 1).Set("p2_wins", 1)
                 .Set("result_state", 1);
            rows.Add(t.Row());
            for (var i = 0; i < 75; i++)
            {
                var rf = t.Get("round_frames");
                t = t.Next().Set("round_frames", i < 55 ? rf + 1 : rf).SetFloat("p1_gauge", 200.0f + i);
                rows.Add(t.Row());
            }
            cases.Add(new Case("被弾_致命", "致命被弾（+71）と round_frames の凍り", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1).Set("p1_life_raw", 6);
            rows.Add(t.Row());
            t = Run(rows, t, 5);
            t = t.Next().Set("stage_index", 1).Set("round_frames", 0).Set("p2_character", 4);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            cases.Add(new Case("被弾_跳ね前に面が変わる", "flush（stage_changed）と ROUND/STAGE_ABANDONED", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1).Set("p1_life_raw", 8);
            rows.Add(t.Row());
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1).Set("p1_life_raw", 6)
                 .Set("p1_spell_points", 0);
            rows.Add(t.Row());
            t = Run(rows, t, 75);
            cases.Add(new Case("被弾_畳み込み", "HIT_CLUSTER_TICKS 以内の追加信号", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1)
                 .Set("p1_spell_points", 0).Set("p1_combo_gauge_raw", 0)
                 .Set("p2_spell_points", 0).Set("p2_combo_gauge_raw", unchecked((uint)-999999));
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            cases.Add(new Case("被弾でない信号", "spell_points / combo_gauge のリセット単独", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0).Set("mode", 7).Set("field_id", 99).Set("p1_character", 99)
                           .Set("p2_character", 98).Set("difficulty", 9).Set("battle_bgm_id", 99);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("battle_bgm_id", 13).Set("round_frames", t.Get("round_frames") + 1);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            cases.Add(new Case("知らない id", "Unknown(値) の綴りと BGM の後追い", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0)
                .Set("p1_score_raw", 0xFFFFFFFFL)
                .Set("p2_score_raw", 0x80000000L)
                .Set("p1_max_combo", 0xFFFFFFFFL)
                .Set("round_frames", 0x7FFFFFFFL)
                .Set("p1_life_raw", 0x80000001L);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            cases.Add(new Case("u32_上半分", "2^31 以上の u32（符号の反転が起きないか）", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0)
                .Set("p2_cpu_dodge_mode", -3)
                .Set("p2_cpu_quick_timer", -999999)
                .Set("p2_cpu_stand_timer", -1);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1).Set("p1_life_raw", 2);
            rows.Add(t.Row());
            t = Run(rows, t, 75);
            cases.Add(new Case("i32_負値", "CPU の内部値が負のまま payload へ入るか", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0)
                .SetBits("p1_gauge", 0x80000000u)
                .SetBits("p2_gauge", 0x00000001u)
                .SetFloat("p1_lives", 2.5f)
                .SetFloat("p2_lives", 1e30f);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1).Set("p1_life_raw", 2);
            rows.Add(t.Row());
            t = Run(rows, t, 75);
            cases.Add(new Case("float_端", "-0.0 / 非正規化数 / 巨大な値の生ビット", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            t = t.Next().Set("result_state", 1); rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 1).Set("p1_wins", 1); rows.Add(t.Row());
            t = t.Next().Set("result_state", 2).Set("result_winner", 0)
                 .Set("clear_life_bonus", 11).Set("clear_max_combo_bonus", 22)
                 .Set("clear_spell_bonus", 33).Set("clear_boss_bonus", 44)
                 .Set("clear_reversal_bonus", 55).Set("clear_lives_bonus", 66)
                 .Set("clear_total", 231);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("stage_index", 1).Set("result_state", 0).Set("round_frames", 0)
                 .Set("p1_score_raw", 5000).Set("completed_rounds", 0).Set("p1_wins", 0)
                 .Set("p2_character", 5).Set("battle_bgm_id", 6).Set("field_id", 1);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            cases.Add(new Case("面の切り替わり", "STAGE_RESULT_READY → STAGE_COMPLETED → STAGE_CHANGED", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0).Set("stage_index", 8);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            t = t.Next().Set("result_state", 1); rows.Add(t.Row());
            t = t.Next().Set("completed_rounds", 1).Set("p1_wins", 1); rows.Add(t.Row());
            t = t.Next().Set("result_state", 2).Set("result_winner", 0).Set("clear_total", 777);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            cases.Add(new Case("最終面", "close() が pending_result を確定させる枝", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(2);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().Set("result_state", 2).Set("result_winner", 7).Set("clear_total", 5);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            cases.Add(new Case("勝者不明", "result_winner が 0/1 でないとき", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            t = t.Next().SetFloat("p1_lives", 4.0f).Set("round_frames", t.Get("round_frames") + 1);
            rows.Add(t.Row());
            t = t.Next().SetFloat("p1_lives", 3.0f).Set("round_frames", t.Get("round_frames") + 1);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            cases.Add(new Case("残機の増減", "EXTEND と LIVES_LOST", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(2);
            rows.Add(t.Row());
            t = Run(rows, t, 1);
            t = t.Next().Set("round_frames", t.Get("round_frames") + 1)
                 .Set("p1_spell_attacks", 3).Set("p1_boss_attacks", 2).Set("p1_boss_reversals", 1)
                 .Set("p2_spell_attacks", 5).Set("p2_boss_attacks", 4).Set("p2_boss_reversals", 6);
            rows.Add(t.Row());
            t = Run(rows, t, 2);
            cases.Add(new Case("カウンタ一斉", "SPELL/BOSS/REVERSAL 6 件が同じ tick に出る順", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0);
            rows.Add(t.Row());
            for (var i = 0; i < 6; i++)
            {
                t = t.Next().Set("round_frames", t.Get("round_frames") + 1)
                     .Set("flags", i % 2 == 0 ? 0 : RecordLabels.RequiredFlags);
                rows.Add(t.Row());
            }
            t = t.Next().Set("flags", RecordLabels.RequiredFlags)
                 .Set("round_frames", t.Get("round_frames") + 1);
            rows.Add(t.Row());
            cases.Add(new Case("フラグ落ち混在", "採らない tick が挟まる（母数と monotonic）", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(0).Set("flags", 0);
            for (var i = 0; i < 5; i++) { t = t.Next(); rows.Add(t.Row()); }
            cases.Add(new Case("有効 tick なし", "close(None) ——行が 1 つも開かない", rows));
        }

        {
            var rows = new List<uint[]>();
            var t = Base(2)
                .Set("flags", RecordLabels.RequiredFlags | RecordLabels.FlagReplayMgrValid)
                .Set("replay_flag", 1);
            rows.Add(t.Row());
            t = Run(rows, t, 3);
            cases.Add(new Case("再生中", "execution_type = Replay Playback", rows));
        }

        return cases;
    }

    public static (int Cases, int Ticks) Write(string path)
    {
        var cases = All();
        var sb = new StringBuilder();
        sb.Append("# ").Append(FormatVersion).Append('\n');
        sb.Append("# ★実データに 1 件も無い経路を通すための合成。原本は csharp/TH09.Record/SyntheticTicks.cs\n");
        sb.Append("@words\t").Append(string.Join("\t", TickReplay.NeededWords)).Append('\n');
        var ticks = 0;
        foreach (var c in cases)
        {
            sb.Append("@case\t").Append(c.Name).Append('\t')
              .Append(c.Rows.Count.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(c.Why).Append('\n');
            foreach (var row in c.Rows)
            {
                sb.Append(string.Join("\t", row.Select(v => v.ToString(CultureInfo.InvariantCulture))))
                  .Append('\n');
                ticks++;
            }
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return (cases.Count, ticks);
    }


    public static List<Case> Read(string path)
    {
        var cases = new List<Case>();
        string[]? words = null;
        Case? cur = null;
        foreach (var raw in File.ReadLines(path, System.Text.Encoding.UTF8))
        {
            var line = raw.TrimEnd('\r', '\n');
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("@words	", StringComparison.Ordinal))
            {
                words = line.Split('	')[1..];
                if (!words.SequenceEqual(TickReplay.NeededWords))
                    throw new InvalidDataException(
                        $"合成ファイルの語の並びが違う（ファイル {words.Length} 語 / いまの定数 "
                        + $"{TickReplay.NeededWords.Length} 語）。作り直すこと");
                continue;
            }
            if (line.StartsWith("@case	", StringComparison.Ordinal))
            {
                var f = line.Split('	');
                cur = new Case(f[1], f.Length > 3 ? f[3] : "", []);
                cases.Add(cur);
                continue;
            }
            if (words is null) throw new InvalidDataException("@words 行より前に tick の行が来た");
            if (cur is null) throw new InvalidDataException("@case 行より前に tick の行が来た");
            var cells = line.Split('	');
            if (cells.Length != words.Length)
                throw new InvalidDataException($"語の数が合わない（{cells.Length} 個 / 期待 {words.Length} 個）");
            cur.Rows.Add([.. cells.Select(x => uint.Parse(x, CultureInfo.InvariantCulture))]);
        }
        if (cases.Count == 0) throw new InvalidDataException("合成が 1 件も読めなかった（0 件は『測れていない』）");
        return cases;
    }

    public static IEnumerable<Snapshot> Snapshots(Case c)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < TickReplay.NeededWords.Length; i++) index[TickReplay.NeededWords[i]] = i;
        long kept = 0;
        var wall = "";
        foreach (var row in c.Rows)
        {
            if ((row[index["flags"]] & RecordLabels.RequiredFlags) != RecordLabels.RequiredFlags) continue;
            if (kept % 60 == 0) wall = TickReplay.EpochWall(kept / 60);
            yield return FromRow(row, index, kept, wall);
            kept++;
        }
    }

    private static Snapshot FromRow(uint[] row, Dictionary<string, int> ix, long kept, string wall)
    {
        long U(string n) => row[ix[n]];
        int S(string n) => unchecked((int)row[ix[n]]);
        double F(string n) => BitConverter.UInt32BitsToSingle(row[ix[n]]);
        var replayActive = (row[ix["flags"]] & RecordLabels.FlagReplayMgrValid) != 0 && row[ix["replay_flag"]] == 1;
        return new Snapshot
        {
            WallTime = wall,
            Monotonic = kept / 60.0,
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
            P1Score = U("p1_score_raw") * 10,
            P2Score = U("p2_score_raw") * 10,
            P1MaxCombo = U("p1_max_combo"),
            P2MaxCombo = U("p2_max_combo"),
            P1SpellPoints = U("p1_spell_points"),
            P2SpellPoints = U("p2_spell_points"),
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
            P1ComboGaugeRaw = S("p1_combo_gauge_raw"),
            P2ComboGaugeRaw = S("p2_combo_gauge_raw"),
            P1CpuDodgeMode = S("p1_cpu_dodge_mode"),
            P2CpuDodgeMode = S("p2_cpu_dodge_mode"),
            P1CpuQuickDisableTimer = S("p1_cpu_quick_timer"),
            P2CpuQuickDisableTimer = S("p2_cpu_quick_timer"),
            P1CpuStandstillTimer = S("p1_cpu_stand_timer"),
            P2CpuStandstillTimer = S("p2_cpu_stand_timer"),
            ExecutionType = replayActive ? RecordLabels.ExecReplay : RecordLabels.ExecLive,
        };
    }
}
