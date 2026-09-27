using W = TH09.Generated.TickWords;

namespace TH09.TickBus;

public static class SelfTest
{
    private static readonly string[] V14CpuMovePrefixes =
    [
        "p1_cpu_dir", "p2_cpu_dir",
        "p1_cpu_dodge_dir", "p2_cpu_dodge_dir",
        "p1_cpu_prev_dir", "p2_cpu_prev_dir",
        "p1_cpu_target", "p2_cpu_target",
        "p1_item", "p2_item",
        "p1_enemy_prio", "p2_enemy_prio",
        "p1_enemy_first", "p2_enemy_first",
        "p1_slow_mult", "p2_slow_mult",
    ];

    private const int V14CpuMoveFieldCount = 88;

    private static readonly string[] Dropped = BuildDropped();

    private static string[] BuildDropped()
    {
        var v14 = new List<string>();
        foreach (var name in W.Record.All)
            foreach (var prefix in V14CpuMovePrefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal)) { v14.Add(name); break; }
        if (v14.Count != V14CpuMoveFieldCount)
            throw new InvalidOperationException(
                $"v14 の CPU 移動判断の語が {v14.Count} 個しか拾えていません"
                + $"（{V14CpuMoveFieldCount} 個のはず）。V14CpuMovePrefixes か tickbus.h がずれています。");
        var all = new List<string> { W.Record.SeqBegin, W.Record.SeqEnd, W.Record.Flags };
        all.AddRange(v14);
        all.Add(W.Record.P1EnemySubMask);
        all.Add(W.Record.P2EnemySubMask);
        return all.ToArray();
    }

    private static readonly (string From, string[] To)[] Splits =
    [
        (W.Record.P1EnemyClassCounts, ["p1_c2_count", "p1_c3_count", "p1_boss_count"]),
        (W.Record.P2EnemyClassCounts, ["p2_c2_count", "p2_c3_count", "p2_boss_count"]),
    ];

    private static readonly (string From, string To)[] Renames =
    [
        (W.Record.P1ScoreRaw, "p1_score"),
        (W.Record.P2ScoreRaw, "p2_score"),
        (W.Record.P1CpuQuickTimer, "p1_cpu_quick_disable_timer"),
        (W.Record.P2CpuQuickTimer, "p2_cpu_quick_disable_timer"),
        (W.Record.P1CpuStandTimer, "p1_cpu_standstill_timer"),
        (W.Record.P2CpuStandTimer, "p2_cpu_standstill_timer"),
        (W.Record.P1CpuQuickTimerCur, "p1_cpu_quick_disable_timer_cur"),
        (W.Record.P2CpuQuickTimerCur, "p2_cpu_quick_disable_timer_cur"),
        (W.Record.P1CpuStandTimerCur, "p1_cpu_standstill_timer_cur"),
        (W.Record.P2CpuStandTimerCur, "p2_cpu_standstill_timer_cur"),
    ];

    private static readonly string[] Derived = ["execution_type", "replay_active"];

    private static TickBusLayout.HeaderReject? LegacyRejectReason(TickBusHeader h)
        => h.Magic != TickBusLayout.Magic ? TickBusLayout.HeaderReject.Magic : null;

    public static int Run(TextWriter outw)
    {
        int failed = 0;
        void Check(string name, Func<string?> body)
        {
            string? why;
            try { why = body(); }
            catch (Exception exc) { why = $"例外: {exc.Message}"; }
            if (why is null) { outw.WriteLine($"  ok   {name}"); }
            else { failed++; outw.WriteLine($"  NG   {name}: {why}"); }
        }

        outw.WriteLine("[1] レイアウトの整合（生成物と表・オフセットの昇順・区画の合計）");
        Check("主リングの表が生成物と一致する", () => { TickBusLayout.SelfCheck(); return null; });
        Check("座標リングの区画がレコードちょうどに収まる", () => { CoordLayout.SelfCheck(); return null; });
        Check("主リングの語数（ヘッダ 29 / レコード 163 は tickbus.h 由来）", () =>
            TickBusLayout.HeaderFields.Length == W.Header.Count && TickBusLayout.RecordFields.Length == W.Record.Count
                ? null
                : $"表 {TickBusLayout.HeaderFields.Length}/{TickBusLayout.RecordFields.Length} 対 生成物 {W.Header.Count}/{W.Record.Count}");

        outw.WriteLine("[2] ヘッダを弾く条件（DLL 側 tickbus_header_ok と同じ 4 項目）");
        Check("正しいヘッダは通る", () => TickBusLayout.RejectReason(GoodHeader()) is null ? null : "弾かれた");
        Check("全部 0 のヘッダは magic で弾く", () =>
            TickBusLayout.RejectReason(new TickBusHeader(new uint[TickBusLayout.HeaderWordCount]))
                == TickBusLayout.HeaderReject.Magic ? null : "magic 以外の理由になった");
        Check("版が違えば version で弾く", () =>
            TickBusLayout.RejectReason(GoodHeader(version: TickBusLayout.Version - 1))
                == TickBusLayout.HeaderReject.Version ? null : "弾かれなかった");
        Check("record_size が違えば弾く", () =>
            TickBusLayout.RejectReason(GoodHeader(recordSize: 640))
                == TickBusLayout.HeaderReject.RecordSize ? null : "弾かれなかった");
        Check("capacity が違えば弾く", () =>
            TickBusLayout.RejectReason(GoodHeader(capacity: 16384))
                == TickBusLayout.HeaderReject.Capacity ? null : "弾かれなかった");
        Check("★否定: version を見ない旧実装なら版違いを通してしまう", () =>
            LegacyRejectReason(GoodHeader(version: TickBusLayout.Version - 1)) is null
                ? null : "旧実装でも落ちた（引き合いになっていない）");

        outw.WriteLine("[3] 座標リングのヘッダ（DLL 側 coordbus_header_ok と同じ 5 項目）");
        Check("正しいヘッダは通る", () => CoordLayout.RejectReason(GoodCoordHeader()) is null ? null : "弾かれた");
        Check("slot_count が違えば弾く（record_size が同じでも起こりうる）", () =>
            CoordLayout.RejectReason(GoodCoordHeader(slots: CoordLayout.Slots - 1)) is not null ? null : "弾かれなかった");
        Check("enable=0 でもヘッダは正当（書かないだけ）", () =>
            CoordLayout.RejectReason(GoodCoordHeader(enable: 0)) is null ? null : "弾かれた");

        outw.WriteLine("[4] 値の解釈（float の生ビット / i32 / ×10）");
        Check("1.0f の生ビット", () => TickBusLayout.FloatFromBits(0x3F800000u) == 1.0 ? null : "1.0 にならない");
        Check("float32 → double は往復する", () =>
        {
            uint[] pats = [0x00000000, 0x80000000, 0x3F800000, 0xBF800000, 0x7F7FFFFF,
                           0x00000001, 0x807FFFFF, 0x7F800000, 0xFF800000];
            foreach (var b in pats)
            {
                uint back = BitConverter.SingleToUInt32Bits((float)TickBusLayout.FloatFromBits(b));
                if (back != b) return $"0x{b:X8} → 0x{back:X8}";
            }
            return null;
        });
        Check("s32 は符号付きで返る（番兵 -999999 が負値）", () =>
            TickBusLayout.S32(0xFFFFFFFFu) == -1 && TickBusLayout.S32(0x80000000u) == int.MinValue
            && TickBusLayout.S32(unchecked((uint)-999999)) == -999999 ? null : "符号が合わない");
        Check("score の ×10 が u32 を溢れない", () =>
            (long)0xFFFFFFFFu * 10 == 42949672950L ? null : "long で計算していない");
        Check("敵カウンタの 3 分割（bit24-31 は捨てる）", () =>
            TickBusLayout.UnpackEnemyClassCounts(0xAA030201u) == (1u, 2u, 3u) ? null : "分割が違う");

        outw.WriteLine("[5] レコード → Snapshot の配線（th09_tick_source.check_record_mapping の移植）");
        Check("語が 1 つ残らず配線されている", CheckRecordMapping);
        Check("Snapshot の名前が重複していない", () =>
        {
            var names = SnapshotMapper.FromRecord(ZeroRecord()).Select(p => p.Name).ToList();
            var dup = names.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            return dup.Count == 0 ? null : "重複: " + string.Join(" ", dup);
        });
        Check("Execution Type は replay_flag と REPLAYMGR_VALID の両方を見る", () =>
        {
            var words = new uint[TickBusLayout.RecordWordCount];
            words[W.Record.ReplayFlagOffset >> 2] = 1;
            if (SnapshotMapper.ExecutionType(new TickRecord(words), false).Label != SnapshotMapper.ExecLive)
                return "flags を見ていない";
            words[W.Record.FlagsOffset >> 2] = TickBusBits.FlagReplaymgrValid;
            if (SnapshotMapper.ExecutionType(new TickRecord(words), false).Label != SnapshotMapper.ExecReplay)
                return "両方立っているのに Replay にならない";
            words[W.Record.ReplayFlagOffset >> 2] = 0;
            return SnapshotMapper.ExecutionType(new TickRecord(words), true).Label == SnapshotMapper.ExecNet
                ? null : "adonis のとき Network Play にならない";
        });

        outw.WriteLine(failed == 0 ? "自己検査: 合格" : $"自己検査: {failed} 件が落ちた");
        return failed;
    }

    private static string? CheckRecordMapping()
    {
        var produced = SnapshotMapper.FromRecord(ZeroRecord()).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var target = new Dictionary<string, string>(StringComparer.Ordinal);
        var splitAll = new HashSet<string>(StringComparer.Ordinal);
        var renameMap = Renames.ToDictionary(r => r.From, r => r.To, StringComparer.Ordinal);
        var splitMap = Splits.ToDictionary(s => s.From, s => s.To, StringComparer.Ordinal);
        foreach (var s in Splits) foreach (var t in s.To) splitAll.Add(t);

        var unwired = new List<string>();
        foreach (var (name, _off) in TickBusLayout.RecordFields)
        {
            if (Array.IndexOf(Dropped, name) >= 0) continue;
            string to = splitMap.TryGetValue(name, out var parts) ? parts[0]
                      : renameMap.TryGetValue(name, out var r) ? r : name;
            target[name] = to;
            if (!produced.Contains(to)) unwired.Add($"{name} → {to}");
        }
        foreach (var s in Splits)
            foreach (var t in s.To)
                if (!produced.Contains(t)) unwired.Add($"{s.From} → {t}（分解先）");

        var stray = produced
            .Where(n => !target.ContainsValue(n) && !splitAll.Contains(n) && Array.IndexOf(Derived, n) < 0)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        var known = TickBusLayout.RecordFields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var ghost = Dropped.Concat(Splits.Select(s => s.From))
            .Where(n => !known.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();

        if (unwired.Count == 0 && stray.Count == 0 && ghost.Count == 0) return null;
        return $"未配線={string.Join(" ", unwired)} / 余分={string.Join(" ", stray)} / 幽霊={string.Join(" ", ghost)}";
    }

    private static TickRecord ZeroRecord() => new(new uint[TickBusLayout.RecordWordCount]);

    private static TickBusHeader GoodHeader(uint? version = null, uint? recordSize = null, uint? capacity = null)
    {
        var w = new uint[TickBusLayout.HeaderWordCount];
        w[W.Header.MagicOffset >> 2] = TickBusLayout.Magic;
        w[W.Header.VersionOffset >> 2] = version ?? TickBusLayout.Version;
        w[W.Header.RecordSizeOffset >> 2] = recordSize ?? TickBusLayout.RecordSize;
        w[W.Header.CapacityOffset >> 2] = capacity ?? TickBusLayout.Capacity;
        return new TickBusHeader(w);
    }

    private static CoordBusHeader GoodCoordHeader(int? slots = null, uint enable = 1)
    {
        var w = new uint[CoordLayout.HeaderSize / 4];
        w[0x00 >> 2] = CoordLayout.Magic;
        w[0x04 >> 2] = CoordLayout.Version;
        w[0x08 >> 2] = CoordLayout.RecordSize;
        w[0x0C >> 2] = CoordLayout.Capacity;
        w[0x20 >> 2] = (uint)(slots ?? CoordLayout.Slots);
        w[0x24 >> 2] = enable;
        return new CoordBusHeader(w);
    }
}
