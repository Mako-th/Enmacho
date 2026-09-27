using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.Record.Generated;

namespace TH09.Record;

public static class SelfTest
{
    private static bool _ok = true;
    private static int _checks;

    private static void Chk(string label, bool cond)
    {
        _checks++;
        Console.WriteLine($"  {(cond ? "ok  " : "NG  ")}{label}");
        _ok = _ok && cond;
    }

    public static int Run()
    {
        _ok = true;
        _checks = 0;
        var work = Path.Combine(Path.GetTempPath(), "th09_record_selftest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            PathsTests(work);
            SchemaTests(work);
            TokenTests();
            SyntheticDumpTests(work);
            EmptySegmentTests(work);
            PlayerIdentityTests(work);
            LegacyTests();
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }

        Console.WriteLine();
        if (_checks == 0)
        {
            Console.WriteLine("★1 つも検査していません。これは『全部通った』ではありません。");
            return 2;
        }
        Console.WriteLine(_ok ? $"すべて ok（{_checks} 件）" : $"★失敗あり（{_checks} 件中）");
        Console.WriteLine("★これで Python と一致したのではない ——突き合わせは parity/run_parity.py。");
        return _ok ? 0 : 1;
    }


    private static void PathsTests(string work)
    {
        Console.WriteLine("=== 置き場所（配布時のパス規約）===");

        var portable = Path.Combine(work, "portable");
        Directory.CreateDirectory(portable);
        var p1 = Paths.Resolve(portable);
        Chk("目印が無ければポータブル（exe の隣）", p1.Mode == PathMode.Portable);
        Chk("置き場所は exe の隣", string.Equals(p1.DataRoot, portable, StringComparison.OrdinalIgnoreCase));
        Chk("本体 DB は exe の隣",
            string.Equals(p1.MainDb, Path.Combine(portable, Paths.MainDbDefault), StringComparison.OrdinalIgnoreCase));
        Chk("Layer 0 も同じ置き場所の下（★分けない）",
            p1.Layer0Db.StartsWith(portable + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Chk("設定は exe の隣の config.json",
            string.Equals(p1.ConfigPath, Path.Combine(portable, "config.json"), StringComparison.OrdinalIgnoreCase));
        Chk("★既定の置き場所は相対（絶対の既定＝%LOCALAPPDATA% 等を持たない）",
            !Path.IsPathRooted(Paths.MainDbDefault) && !Path.IsPathRooted(Paths.Layer0DbDefault));

        var gitOnly = Path.Combine(work, "gitonly");
        Directory.CreateDirectory(Path.Combine(gitOnly, ".git"));
        Directory.CreateDirectory(Path.Combine(gitOnly, "app"));
        Chk("★`.git` があるだけならポータブルのまま（配布物が git の下でも巻き込まれない）",
            Paths.Resolve(Path.Combine(gitOnly, "app")).Mode == PathMode.Portable);

        var repo = Path.Combine(work, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "python"));
        Directory.CreateDirectory(Path.Combine(repo, "project_material_documents"));
        var exeDir = Path.Combine(repo, "csharp", "TH09.Record", "bin");
        Directory.CreateDirectory(exeDir);
        var p2 = Paths.Resolve(exeDir);
        Chk("目印 2 つでリポジトリ扱い", p2.Mode == PathMode.Repository);
        Chk("置き場所はリポジトリ直下", string.Equals(p2.DataRoot, repo, StringComparison.OrdinalIgnoreCase));
        Chk("設定は Python 側と同じ config.json",
            string.Equals(p2.ConfigPath, Path.Combine(repo, "python", "config.json"),
                          StringComparison.OrdinalIgnoreCase));

        var elsewhere = Path.Combine(work, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        var otherDb = Path.Combine(elsewhere, "other.sqlite3");
        File.WriteAllText(Path.Combine(repo, "python", "config.json"),
            "{\"database\": " + Quote(otherDb) + ", \"layer0_db_path\": \"data/l0.db\"}",
            new UTF8Encoding(false));
        var p3 = Paths.Resolve(exeDir);
        Chk("★config.json の絶対パスで本体 DB を差し替えられる",
            string.Equals(p3.MainDb, otherDb, StringComparison.OrdinalIgnoreCase) && p3.MainDbFromConfig);
        Chk("相対パスは置き場所を基準にする",
            string.Equals(p3.Layer0Db, Path.Combine(repo, "data", "l0.db"), StringComparison.OrdinalIgnoreCase));
        var report = p3.Diagnose(probe: false);
        Chk("★置き場所の外を指したら、黙って通さずに言う（分けないのが既定）",
            report.Severity == PathSeverity.Warn
            && report.Lines.Any(l => l.Contains("分けない", StringComparison.Ordinal)));

        File.WriteAllText(Path.Combine(repo, "python", "config.json"), "{ これは JSON ではない",
                          new UTF8Encoding(false));
        var p4 = Paths.Resolve(exeDir);
        Chk("壊れた config.json は既定値で動く（落ちない）",
            !p4.ConfigLoaded
            && string.Equals(p4.MainDb, Path.Combine(repo, Paths.MainDbDefault), StringComparison.OrdinalIgnoreCase));

        var blocker = Path.Combine(work, "blocker");
        File.WriteAllBytes(blocker, [0x54]);
        var probe = Paths.Probe(Path.Combine(blocker, "inside"));
        Chk("★書けない場所を、書いてみて見つける", probe.Writable == false && probe.Reason is not null);
        var p5 = Paths.Resolve(Path.Combine(blocker, "inside"));
        var rep5 = p5.Diagnose();
        Chk("★書けないなら Fatal", rep5.Severity == PathSeverity.Fatal);
        Chk("★『別の場所へ黙って書かない』とはっきり言う",
            rep5.Lines.Any(l => l.Contains("別の場所へ黙って書く", StringComparison.Ordinal)));
        Chk("★直し方（移す / config.json に絶対パス）を出す",
            rep5.Lines.Any(l => l.Contains("絶対パス", StringComparison.Ordinal)));
        var threw = false;
        try { p5.EnsureWritable(); } catch (IOException) { threw = true; }
        Chk("★書けない場所では EnsureWritable が止める", threw);

        var notYet = Path.Combine(work, "notyet", "deep");
        var probe2 = Paths.Probe(notYet);
        Chk("★まだ無いフォルダは、上へ辿って実在する所で試す",
            probe2.Writable == true
            && string.Equals(probe2.Probed, work, StringComparison.OrdinalIgnoreCase));
        Chk("★★診断はフォルダを作らない（作ると『見ただけ』で物が増える）",
            !Directory.Exists(Path.Combine(work, "notyet")));
        Chk("★途中に同じ名前のファイルがあれば、そう言う",
            probe.Reason is not null && probe.Reason.Contains("同じ名前のファイル", StringComparison.Ordinal));

        Chk("書ける場所は Ok", Paths.Resolve(portable).Diagnose().Severity == PathSeverity.Ok);

        var fakeStore = Path.Combine(work, "fakeVirtualStore");
        var target = Path.Combine(work, "portable");
        var root = Path.GetPathRoot(Path.GetFullPath(target))!;
        var shadow = Path.Combine(fakeStore, Path.GetFullPath(target)[root.Length..]);
        Directory.CreateDirectory(shadow);
        Chk("★VirtualStore に影があれば見つける",
            Paths.FindVirtualStoreShadow(target, fakeStore) is not null);
        Chk("影が無ければ null（『無い』を作らない）",
            Paths.FindVirtualStoreShadow(Path.Combine(work, "repo"), fakeStore) is null);
        var rep8 = Paths.Resolve(target).Diagnose(probe: true, virtualStoreRoot: fakeStore);
        Chk("★影が見つかったら Fatal にして、その場所を出す",
            rep8.Severity == PathSeverity.Fatal
            && rep8.Lines.Any(l => l.Contains("別の場所に居ます", StringComparison.Ordinal)));
        Directory.Delete(shadow, recursive: true);

        Chk("守られたフォルダの判定が効く（Program Files）",
            Paths.IsUnderProtectedRoot(Path.Combine(
                Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "TH09")));
        Chk("★普通の場所は守られたフォルダではない", !Paths.IsUnderProtectedRoot(work));
    }

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\") + "\"";


    private static void SchemaTests(string work)
    {
        Console.WriteLine("=== スキーマの原本（定義元から作った形と生成物のフィンガープリントを突き合わせる）===");
        Chk($"フィンガープリントが生成物と一致（{RecordSchema.Sha256[..12]}…）",
            string.Equals(Schema.Fingerprint(), RecordSchema.Sha256, StringComparison.Ordinal));

        using var refDb = Schema.CreateFrom(Path.Combine(work, "reference.sqlite3"));
        var want = Schema.Shapes(refDb);
        Chk($"定義元だけから {RecordSchema.TableCount} 表を組める（実測 {want.Count} 表）",
            want.Count == RecordSchema.TableCount);
        var cols = want.Sum(kv => kv.Value.Count);
        Chk($"列は {cols} 本（0 なら何も見ていない）", cols > 0);
        Chk("同じ形どうしなら差は 0 件（誤検出しない）",
            Schema.Compare(want, want, Schema.PendingSet()).Fatal.Count == 0);

        foreach (var (label, mutate) in Mutations())
        {
            var got = Clone(want);
            var changed = mutate(got);
            Chk($"素材を壊せた（{label}）——壊せないなら次の行は何も見ていない", changed);
            if (!changed) continue;
            var diff = Schema.Compare(want, got, Schema.PendingSet());
            Chk($"★{label} → 落ちる（差 {diff.Fatal.Count} 件）", diff.Fatal.Count > 0);
        }

        var pendingCol = RecordSchema.PendingColumns[0];
        var older = Clone(want);
        older[pendingCol.Table].RemoveAll(c => c.Name == pendingCol.Column);
        var d1 = Schema.Compare(want, older, Schema.PendingSet());
        Chk($"`ALTER` で足す列（{pendingCol.Table}.{pendingCol.Column}）が無いだけなら移行待ち",
            d1.Fatal.Count == 0 && d1.Pending.Count == 1);
        var d2 = Schema.Compare(want, older, []);
        Chk("★仕掛けが無い扱いにすると、同じ欠けが致命的になる（分岐が効いている）", d2.Fatal.Count > 0);

        var oldPath = Path.Combine(work, "old.sqlite3");
        using (var old = Schema.CreateFrom(oldPath))
        {
            foreach (var (table, column, _) in RecordSchema.PendingColumns)
                RecordDb.Exec(old.Connection, $"ALTER TABLE \"{table}\" DROP COLUMN \"{column}\"");
            var before = Schema.Compare(want, Schema.Shapes(old), Schema.PendingSet());
            Chk($"★{RecordSchema.PendingColumns.Length} 列を落とした DB は「移行待ち」として見える"
                + $"（致命 {before.Fatal.Count} / 移行待ち {before.Pending.Count}）",
                before.Fatal.Count == 0 && before.Pending.Count == RecordSchema.PendingColumns.Length);
            var added = Schema.EnsureShape(old);
            Chk($"★EnsureShape が {RecordSchema.PendingColumns.Length} 列を足す（実測 {added.Count} 列）",
                added.Count == RecordSchema.PendingColumns.Length);
            var after = Schema.Compare(want, Schema.Shapes(old), Schema.PendingSet());
            Chk("★揃えた後、欠けている列は 0 件（移行待ち 0）", after.Pending.Count == 0);
            Chk("★残る差は『列の並び』だけ（ALTER は末尾にしか足せない）",
                after.Fatal.Count > 0
                && after.Fatal.All(m => m.Contains("列の並びが違う", StringComparison.Ordinal)));
            Chk("★もう一度 EnsureShape しても何も起きない（冪等）", Schema.EnsureShape(old).Count == 0);
        }

        var extra = Clone(want);
        extra["th09_stray"] = [new ColumnShape("x", "INTEGER", 0, null, 0)];
        Chk("★定義元に無い表が実 DB にあったら致命的",
            Schema.Compare(want, extra, Schema.PendingSet()).Fatal.Count > 0);
    }

    private static Dictionary<string, List<ColumnShape>> Clone(Dictionary<string, List<ColumnShape>> src) =>
        src.ToDictionary(kv => kv.Key, kv => new List<ColumnShape>(kv.Value), StringComparer.Ordinal);

    private static List<(string, Func<Dictionary<string, List<ColumnShape>>, bool>)> Mutations() =>
    [
        ("列を 1 本落とす", m => m["sessions"].RemoveAll(c => c.Name == "notes") > 0),
        ("型を変える（TEXT → INTEGER）", m => Replace(m, "events", "wall_time", c => c with { Type = "INTEGER" })),
        ("NOT NULL を外す", m => Replace(m, "clear_bonuses", "total_bonus", c => c with { NotNull = 0 })),
        ("既定値を変える", m => Replace(m, "replay_paths", "is_current", c => c with { Default = "0" })),
        ("主キーを外す", m => Replace(m, "db_meta", "key", c => c with { Pk = 0 })),
        ("表を 1 つ増やす", m => m.TryAdd("th09_stray", [new ColumnShape("x", "INTEGER", 0, null, 0)])),
        ("列の並びを入れ替える", m => Swap(m, "sessions", 1, 2)),
    ];

    private static bool Replace(Dictionary<string, List<ColumnShape>> m, string table, string column,
                                Func<ColumnShape, ColumnShape> f)
    {
        var i = m[table].FindIndex(c => c.Name == column);
        if (i < 0) return false;
        var before = m[table][i];
        m[table][i] = f(before);
        return m[table][i] != before;
    }

    private static bool Swap(Dictionary<string, List<ColumnShape>> m, string table, int a, int b)
    {
        if (m[table].Count <= Math.Max(a, b)) return false;
        (m[table][a], m[table][b]) = (m[table][b], m[table][a]);
        return true;
    }


    private static void TokenTests()
    {
        Console.WriteLine("=== 値の書き方（parity_spec.py の表と同じ規則か）===");
        Chk("0.0 → f:0000000000000000", ParityValue.FloatToken(0.0) == "f:0000000000000000");
        Chk("★-0.0 は 0.0 と別（f:8000000000000000）",
            ParityValue.FloatToken(-0.0) == "f:8000000000000000");
        Chk("+∞ → f:7ff0000000000000", ParityValue.FloatToken(double.PositiveInfinity) == "f:7ff0000000000000");
        Chk("最小の非正規化数 → f:0000000000000001", ParityValue.FloatToken(double.Epsilon) == "f:0000000000000001");
        Chk("0.1+0.2 は 0.3 と別のビット",
            ParityValue.FloatToken(0.1 + 0.2) != ParityValue.FloatToken(0.3));

        Chk(@"TAB と改行と円記号だけを退避する",
            ParityValue.EscapeText("a\tb\r\nc\\d") == @"a\tb\r\nc\\d");
        Chk("★退避の順（円記号を先にしないと \\t が \\\\t になる）",
            ParityValue.EscapeText("\\t") == @"\\t");
        Chk(@"パスは \ でも / でも末尾だけ",
            ParityValue.BasenameToken(@"C:\a\b.rpy") == "s:b.rpy"
            && ParityValue.BasenameToken("/x/y/b.rpy") == "s:b.rpy"
            && ParityValue.BasenameToken("b.rpy") == "s:b.rpy");
        Chk("パスが NULL なら ~", ParityValue.BasenameToken(null) == "~");
        Chk("時刻は秒までに落とす（時間帯は残す）",
            ParityValue.TruncateSecondsToken("2025-10-30T20:38:15.133879+09:00")
            == "s:2025-10-30T20:38:15+09:00");
        Chk("★小数が末尾でも落とせる（時間帯なし）",
            ParityValue.TruncateSecondsToken("2025-10-30T20:38:15.5") == "s:2025-10-30T20:38:15");
        Chk("小数点が無ければそのまま",
            ParityValue.TruncateSecondsToken("2025-10-30T20:38:15+09:00") == "s:2025-10-30T20:38:15+09:00");
        Chk("BLOB は 16 進小文字", ParityValue.BlobToken([0x00, 0xFF, 0x10]) == "b:00ff10");

        Console.WriteLine("=== JSON の正規形（★実データに無い経路）===");
        Chk("素直な形（鍵は UTF-8 のバイト順）",
            ParityValue.CanonJsonToken("""{"b":1,"a":2}""") == """j:{"a":2,"b":1}""");
        Chk("★NaN / Infinity を読める（System.Text.Json は落とす）",
            ParityValue.CanonJsonToken("""{"n":NaN,"i":Infinity,"m":-Infinity}""")
            == "j:{\"i\":f:7ff0000000000000,\"m\":f:fff0000000000000,\"n\":f:7ff8000000000000}");
        Chk("★64bit を超える整数をそのまま持つ",
            ParityValue.CanonJsonToken("""{"b":123456789012345678901234567890}""")
            == """j:{"b":123456789012345678901234567890}""");
        Chk("★-0（整数）は 0 に畳む", ParityValue.CanonJsonToken("[-0]") == "j:[0]");
        Chk("★-0.0（小数）はビットで残る",
            ParityValue.CanonJsonToken("[-0.0]") == "j:[f:8000000000000000]");
        Chk("★1e400 は +∞", ParityValue.CanonJsonToken("[1e400]") == "j:[f:7ff0000000000000]");
        Chk("★鍵が重なったら後勝ち", ParityValue.CanonJsonToken("""{"a":1,"a":2}""") == """j:{"a":2}""");
        Chk("★非 ASCII は \\uXXXX（小文字）。★退避でさらに二重になる",
            ParityValue.CanonJsonToken("""{"\u3042":1}""")
            == """j:{"\\u3042":1}""");
        Chk("★非 BMP はサロゲート 2 つに分けて書く",
            ParityValue.CanonJsonToken("[\"\U00020BB7\"]")
            == """j:["\\ud842\\udfb7"]""");
        Chk("★壊れた JSON は j!: で生のまま（TAB は退避される）",
            ParityValue.CanonJsonToken("{oops\there") == @"j!:{oops\there");
        Chk("★根がオブジェクトでなくてもよい",
            ParityValue.CanonJsonToken("123") == "j:123" && ParityValue.CanonJsonToken("null") == "j:null");
        Chk("★値の後ろにごみがあれば読めない扱い", ParityValue.CanonJsonToken("{} x") == @"j!:{} x");
        Chk("★01 は読めない（Python の NUMBER_RE と同じ）", ParityValue.CanonJsonToken("[01]").StartsWith("j!:", StringComparison.Ordinal));
        Chk("★+1 / .5 / 1. も読めない",
            ParityValue.CanonJsonToken("[+1]").StartsWith("j!:", StringComparison.Ordinal)
            && ParityValue.CanonJsonToken("[.5]").StartsWith("j!:", StringComparison.Ordinal)
            && ParityValue.CanonJsonToken("[1.]").StartsWith("j!:", StringComparison.Ordinal));
        Chk("★文字列の中の生の制御文字は読めない（strict=True と同じ）",
            ParityValue.CanonJsonToken("[\"a\u0001b\"]").StartsWith("j!:", StringComparison.Ordinal));
        Chk("★空白は 4 種だけ飛ばす", ParityValue.CanonJsonToken(" \t\r\n{} \n") == "j:{}");
        Chk("★垂直タブは空白ではない",
            ParityValue.CanonJsonToken("\u000b{}").StartsWith("j!:", StringComparison.Ordinal));
        Chk("NULL は ~", ParityValue.CanonJsonToken(null) == "~");
    }


    private static void SyntheticDumpTests(string work)
    {
        Console.WriteLine("=== 合成 DB の吐き出し（★実データに無い経路を通す）===");
        var dbPath = Path.Combine(work, "synthetic.sqlite3");
        var counts = Synthetic.Build(dbPath);
        Chk($"合成 DB に {counts.Sum(c => c.Rows)} 行入れた（{counts.Count} 表）", counts.Sum(c => c.Rows) > 0);

        var text = DumpToString(dbPath, out var stats);
        var data = DataLines(text);
        Chk($"★値の行が {data.Count} 行出た（0 なら測れていない）", data.Count > 0);
        Chk("★running のセッションを外し、ヘッダに出す",
            stats.ExcludedSessions.Count == 1 && stats.ExcludedSessions[0] == 3
            && text.Contains("# excluded-sessions 1 [3]", StringComparison.Ordinal));
        Chk("★コーパス外のリプレイは出てこない",
            !text.Contains("th9_99.rpy", StringComparison.Ordinal)
            && !text.Contains("コーパス外", StringComparison.Ordinal));
        Chk("★表ごと除外の 4 表は 1 行も出さない（行はあるのに）",
            !data.Any(l => l.StartsWith("snapshots\t", StringComparison.Ordinal))
            && !data.Any(l => l.StartsWith("deleted_sessions\t", StringComparison.Ordinal))
            && !data.Any(l => l.StartsWith("replay_scan_jobs\t", StringComparison.Ordinal))
            && !data.Any(l => l.StartsWith("replay_scan_items\t", StringComparison.Ordinal)));
        Chk("★表ごと除外でも「DB には何行あるか」は出す",
            text.Contains("# table snapshots rows-in-db=1 emitted=0", StringComparison.Ordinal));

        Chk("★BLOB の値が出ている（実データ 0 件）", data.Any(l => l.Contains("\tb:", StringComparison.Ordinal)));
        Chk("★壊れた JSON が出ている（実データ 0 件）", data.Any(l => l.Contains("\tj!:", StringComparison.Ordinal)));
        Chk("★NaN が出ている（実データ 0 件）", text.Contains("f:7ff8000000000000", StringComparison.Ordinal));
        Chk("★-0.0 が出ている（実データ 0 件）", text.Contains("f:8000000000000000", StringComparison.Ordinal));
        Chk("★64bit 超の整数が出ている（実データ 0 件）",
            text.Contains("123456789012345678901234567890", StringComparison.Ordinal));
        Chk("★round_metrics / players / player_aliases / session_net_play の行が出ている（実データのコーパスでは 0 行）",
            stats.RowsPerTable.Where(x => x.Table is "round_metrics" or "players"
                                                  or "player_aliases" or "session_net_play")
                 .All(x => x.Rows > 0));

        var mutated = Path.Combine(work, "synthetic_mutated.sqlite3");
        File.Copy(dbPath, mutated, overwrite: true);
        using (var w = RecordDb.OpenReadWrite(mutated))
            RecordDb.Exec(w.Connection, "UPDATE session_metadata SET field_id=99 WHERE session_id=1");
        Chk("★比べる列を 1 つ変えたら、吐き出しが変わる",
            DataLines(DumpToString(mutated, out _)).SequenceEqual(data) == false);

        var clock = Path.Combine(work, "synthetic_clock.sqlite3");
        File.Copy(dbPath, clock, overwrite: true);
        using (var w = RecordDb.OpenReadWrite(clock))
        {
            RecordDb.Exec(w.Connection, "UPDATE sessions SET started_at='9999-01-01T00:00:00+09:00'");
            RecordDb.Exec(w.Connection, "UPDATE sessions SET logger_version='まったく別の実装'");
            RecordDb.Exec(w.Connection, "UPDATE events SET wall_time='9999-01-01T00:00:00+09:00'");
        }
        Chk("★除外した列（時刻・記録した側の素性）を変えても、吐き出しは 1 行も変わらない",
            DataLines(DumpToString(clock, out _)).SequenceEqual(data));

        var renum = Path.Combine(work, "synthetic_renum.sqlite3");
        File.Copy(dbPath, renum, overwrite: true);
        using (var w = RecordDb.OpenReadWrite(renum))
        {
            RecordDb.Exec(w.Connection, "PRAGMA foreign_keys=OFF");
            RecordDb.Exec(w.Connection, "UPDATE events SET event_id=event_id+1000");
        }
        Chk("★自動採番の id が全部ずれても、順位が同じなら一致する",
            DataLines(DumpToString(renum, out _)).SequenceEqual(data));

        var reorder = Path.Combine(work, "synthetic_reorder.sqlite3");
        File.Copy(dbPath, reorder, overwrite: true);
        using (var w = RecordDb.OpenReadWrite(reorder))
        {
            RecordDb.Exec(w.Connection, "PRAGMA foreign_keys=OFF");
            RecordDb.Exec(w.Connection, "UPDATE events SET event_id=9999 WHERE event_id=40");
        }
        Chk("★順位の並びが変われば差が出る（順位で見るのは『何でも一致する』ではない）",
            !DataLines(DumpToString(reorder, out _)).SequenceEqual(data));

        Chk("★同じ DB からは必ず同じバイト列（決定的）",
            string.Equals(DumpToString(dbPath, out _), text, StringComparison.Ordinal));

        var wrongType = Path.Combine(work, "synthetic_wrongtype.sqlite3");
        File.Copy(dbPath, wrongType, overwrite: true);
        using (var w = RecordDb.OpenReadWrite(wrongType))
        {
            RecordDb.Exec(w.Connection, "PRAGMA foreign_keys=OFF");
            RecordDb.Exec(w.Connection, "UPDATE replay_paths SET full_path=12345 WHERE replay_path_id=70");
        }
        var coerced = DumpToString(wrongType, out _);
        Chk("★TEXT の列に整数を入れても text に直る（＝『型が違う』は起きない）",
            coerced.Contains("	s:12345", StringComparison.Ordinal));

        using (var db = RecordDb.OpenReadOnly(dbPath)!)
        {
            var (missSpec, missDb, tables, columns) = ParityDump.CheckAgainstDb(db);
            var pendingNames = Schema.PendingSet().Select(x => $"{x.Item1}.{x.Item2}")
                                     .ToHashSet(StringComparer.Ordinal);
            Chk($"★{tables} 表 {columns} 列のうち、分類漏れは『後から足す列』だけ"
                + $"（漏れ {missSpec.Count} 件: {string.Join(" ", missSpec)} / DB に無い列 {missDb.Count} 件）",
                missDb.Count == 0 && missSpec.All(pendingNames.Contains));
        }
    }

    private static string DumpToString(string dbPath, out ParityStats stats)
    {
        using var db = RecordDb.OpenReadOnly(dbPath)
                       ?? throw new FileNotFoundException("合成 DB を開けない", dbPath);
        var body = new StringWriter { NewLine = "\n" };
        stats = ParityDump.Dump(db, body);
        return string.Join("\n", ParityDump.HeaderLines(stats, db)) + "\n" + body;
    }

    private static List<string> DataLines(string text) =>
        text.Split('\n').Where(l => !l.StartsWith('#') && l.Length > 0).ToList();



    private static void EmptySegmentTests(string work)
    {
        Console.WriteLine("=== 0 tick のセグメント（★実データに実在。★流す tick が 1 つも無い）===");
        var l0Path = Path.Combine(work, "empty_seg_layer0.sqlite3");
        BuildLayer0WithEmptySegment(l0Path);

        using var db = ScratchRun.Create(Path.Combine(work, "empty_seg_scratch.sqlite3"), l0Path);
        using var layer0 = TickReplay.OpenLayer0(l0Path);
        List<ScratchRun.Line> lines;
        try
        {
            lines = ScratchRun.ReplayLayer0(db, layer0, [7L]);
        }
        catch (Exception ex)
        {
            Chk($"★0 tick のセグメントで落ちた（{ex.GetType().Name}: {ex.Message}）", false);
            return;
        }
        Chk("★0 tick のセグメントを含むセッションを流せる", lines.Count == 1);
        Chk($"★★中身のあるセグメントは読めている（読んだ tick {(lines.Count == 1 ? lines[0].TotalTicks : -1)} ／ 期待 4）",
            lines.Count == 1 && lines[0].TotalTicks == 4);
        Chk($"★★セグメントは 2 本とも数えている（{(lines.Count == 1 ? lines[0].Note : "")}）",
            lines.Count == 1 && lines[0].Note.Contains("seg=2", StringComparison.Ordinal));
    }

    private static void BuildLayer0WithEmptySegment(string path)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = RecordDb.BusyTimeoutMs / 1000,
        };
        using var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TABLE session_ticks (session_id INTEGER NOT NULL, segment_no INTEGER NOT NULL,"
                + " record_version INTEGER NOT NULL, source_kind TEXT NOT NULL,"
                + " lost_records INTEGER NOT NULL, torn_records INTEGER NOT NULL,"
                + " tick_count INTEGER NOT NULL, field_order TEXT NOT NULL,"
                + " encoding TEXT NOT NULL DEFAULT 'colzlib-v1', uncompressed_bytes INTEGER,"
                + " compressed_bytes INTEGER, blob BLOB NOT NULL,"
                + " PRIMARY KEY (session_id, segment_no));";
            cmd.ExecuteNonQuery();
        }

        Insert(conn, 0, 0, "{\"format\":\"colzlib-v1\",\"record_version\":13,\"tick_count\":0,"
                           + "\"fields\":[],\"sections\":[]}", []);

        var columns = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        foreach (var name in TickReplay.NeededWords)
            columns[name] = [0u, 0u, 0u, 0u];
        var enc = TickEncoder.EncodeColumns(columns, [.. columns.Keys],
                                            TickEncoder.SectionPolicy.Empty, recordVersion: 13);
        Insert(conn, 1, 4, enc.FieldOrder.ToCompactJson(escapeNonAscii: false), enc.Blob);
    }

    private static void Insert(SqliteConnection conn, long segmentNo, int tickCount,
                               string fieldOrder, byte[] blob)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO session_ticks(session_id,segment_no,record_version,source_kind,"
            + "lost_records,torn_records,tick_count,field_order,encoding,"
            + "uncompressed_bytes,compressed_bytes,blob)"
            + " VALUES(7,$g,13,'polling',0,0,$t,$f,'colzlib-v1',0,$c,$b)";
        cmd.Parameters.AddWithValue("$g", segmentNo);
        cmd.Parameters.AddWithValue("$t", tickCount);
        cmd.Parameters.AddWithValue("$f", fieldOrder);
        cmd.Parameters.AddWithValue("$c", blob.Length);
        cmd.Parameters.AddWithValue("$b", blob);
        cmd.ExecuteNonQuery();
    }


    private static void PlayerIdentityTests(string work)
    {
        Console.WriteLine("=== プレイヤーの同一視（players / player_aliases）===");
        var dbPath = Path.Combine(work, "player_identity.sqlite3");
        using var db = Schema.CreateFrom(dbPath);
        var conn = db.Connection;

        Chk("空の DB では AliasMap が空 dict（例外にならない）", PlayerIdentity.AliasMap(conn).Count == 0);
        Chk("空の DB では Players が空リスト", PlayerIdentity.Players(conn).Count == 0);
        Chk("空の DB では SelfPlayerIds が空集合", PlayerIdentity.SelfPlayerIds(conn).Count == 0);
        Chk("★空の DB では SelfNames も空集合（『自分のプレイが無い』ではない。呼ぶ側が読み替える）",
            PlayerIdentity.SelfNames(conn).Count == 0);
        Chk("登録の無い名前は PlayerIdForName が null", PlayerIdentity.PlayerIdForName(conn, "誰か") is null);
        Chk("空文字・null は PlayerIdForName が null（`isinstance(name,str) and name` の移植）",
            PlayerIdentity.PlayerIdForName(conn, "") is null && PlayerIdentity.PlayerIdForName(conn, null) is null);

        ExecSql(conn, "INSERT INTO replays(replay_id,sha256,file_size,first_seen_at,last_seen_at,"
                    + "p1_name,p2_name,replay_date,decode_status) VALUES($0,$1,1,$2,$2,$3,$4,$5,'decoded')",
                1L, "sha1", "2026-01-01", "Mako", "PlayerB", "25/01/02");
        ExecSql(conn, "INSERT INTO replays(replay_id,sha256,file_size,first_seen_at,last_seen_at,"
                    + "p1_name,p2_name,replay_date,decode_status) VALUES($0,$1,1,$2,$2,$3,$4,$5,'decoded')",
                2L, "sha2", "2026-01-01", "PlayerB", "Mako", "25/01/04");
        ExecSql(conn, "INSERT INTO replays(replay_id,sha256,file_size,first_seen_at,last_seen_at,"
                    + "p1_name,p2_name,replay_date,decode_status) VALUES($0,$1,1,$2,$2,$3,$4,$5,'decoded')",
                3L, "sha3", "2026-01-01", "mako", "PlayerC", "24/05/01");
        ExecSql(conn, "INSERT INTO replays(replay_id,sha256,file_size,first_seen_at,last_seen_at,"
                    + "p1_name,p2_name,replay_date,decode_status) VALUES($0,$1,1,$2,$2,$3,$4,$5,'decoded')",
                4L, "sha4", "2026-01-01", "Mako@Reimu", "PlayerB", "25/08/02");
        ExecSql(conn, "INSERT INTO session_net_play(session_id,recorded_at,local_name,remote_name,seat)"
                    + " VALUES($0,$1,$2,$3,2)",
                InsertBareSession(conn, 900L), "2026-08-09T10:00:00+09:00", "まこっちゃん", "PlayerD");

        var data = PlayerAliasCandidates.Collect(conn);
        var want = new[] { "Mako", "PlayerB", "mako", "PlayerC", "Mako@Reimu", "まこっちゃん", "PlayerD" };
        Chk($"★Collect が replays と session_net_play の両方の名前を拾う（{string.Join(",", data.Keys.OrderBy(x => x, StringComparer.Ordinal))}）",
            data.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(want));
        Chk("Mako の対戦数は 2（大小まで一致する行だけ。session_net_play には出ない）", data["Mako"].N == 2);
        Chk("★AsYmd が ISO 時刻も YY/MM/DD に揃える（まこっちゃんの初出 26/08/09）",
            data["まこっちゃん"].First == "26/08/09" && data["まこっちゃん"].Last == "26/08/09");
        Chk("Mako の初出・最終は replay_date の最小・最大（25/01/02 〜 25/01/04）",
            data["Mako"].First == "25/01/02" && data["Mako"].Last == "25/01/04");
        Chk("Mako の相手には PlayerB が 2 回数えられている", data["Mako"].Opponents.GetValueOrDefault("PlayerB") == 2);
        Chk("AsYmd: 読めない値は空文字（推測で埋めない）", PlayerAliasCandidates.AsYmd("junk") == ""
            && PlayerAliasCandidates.AsYmd(null) == "" && PlayerAliasCandidates.AsYmd("") == "");
        Chk("AsYmd: すでに YY/MM/DD ならそのまま", PlayerAliasCandidates.AsYmd("25/01/02") == "25/01/02");

        var beforeMerge = PlayerIdentity.AliasMap(conn);
        var candBefore = PlayerAliasCandidates.Hints(data.Keys, beforeMerge);
        bool Has(List<(string A, string B, string Why)> cand, string a, string b) =>
            cand.Any(x => (x.A == a && x.B == b) || (x.A == b && x.B == a));
        Chk("★Mako ↔ mako を候補に出す（大小・空白を落とすと同じ）", Has(candBefore, "Mako", "mako"));
        Chk("★Mako ↔ Mako@Reimu を候補に出す（片方がもう片方で始まる）", Has(candBefore, "Mako", "Mako@Reimu"));
        Chk("似ていない組（Mako ↔ PlayerB）は候補に出さない", !Has(candBefore, "Mako", "PlayerB"));
        Chk("★Norm は NFKC ＋ 大小・空白を落とすだけ（数字までは落とさない。ab と ab 2 は別のまま）",
            PlayerAliasCandidates.Norm("Mako") == PlayerAliasCandidates.Norm("mako")
            && PlayerAliasCandidates.Norm("ab") != PlayerAliasCandidates.Norm("ab 2"));

        long Seed()
        {
            var alias = PlayerIdentity.AliasMap(conn);
            var added = 0;
            foreach (var name in data.Keys.Where(k => !alias.ContainsKey(k)))
            {
                var rec = data[name];
                var pid = PlayerIdentity.AddPlayer(conn, name);
                PlayerIdentity.SetPlayerAlias(conn, name, pid,
                    source: string.Join('+', rec.Sources.OrderBy(x => x, StringComparer.Ordinal)),
                    firstSeenAt: rec.First.Length == 0 ? null : rec.First);
                added++;
            }
            return added;
        }
        var seeded = Seed();
        Chk($"★seed で名前の数ぶん登録される（{seeded} / {want.Length}）", seeded == want.Length);
        var afterSeed = PlayerIdentity.AliasMap(conn);
        Chk("★1 名前＝1 人（この時点では全員が別人）", afterSeed.Values.ToHashSet().Count == want.Length);
        Chk("★Mako と mako は勝手に同一視しない（別の player_id）", afterSeed["Mako"] != afterSeed["mako"]);
        var reseeded = Seed();
        Chk("★2 回目の seed は 0 件（冪等。既存の紐づけを触らない）", reseeded == 0);
        Chk("2 回目のあとも紐づけは変わらない", PlayerIdentity.AliasMap(conn).SequenceEqual(afterSeed));

        var makoId = afterSeed["Mako"];
        foreach (var alias in new[] { "mako", "Mako@Reimu", "まこっちゃん" })
        {
            var (b, a) = PlayerIdentity.MergeAlias(conn, alias, makoId);
            Chk($"★merge_alias({alias}) は (移す前, 移した後) を返す（{b} → {a}）", a == makoId);
        }
        var boundNow = PlayerIdentity.AliasMap(conn).Where(kv => kv.Value == makoId).Select(kv => kv.Key)
                                     .ToHashSet(StringComparer.Ordinal);
        Chk("★束ねた 4 つが同じ player_id で引ける",
            boundNow.SetEquals(new[] { "Mako", "mako", "Mako@Reimu", "まこっちゃん" }));
        Chk("巻き込まれていない名前がある（PlayerB は別人のまま）",
            PlayerIdentity.PlayerIdForName(conn, "PlayerB") != makoId);
        var orphanIds = new[] { "mako", "Mako@Reimu", "まこっちゃん" }
            .Select(n => afterSeed[n]).Where(id => id != makoId).ToHashSet();
        Chk("★別名が 0 本になった人の行は消えない（display_name / notes は戻せないため）",
            orphanIds.All(id => PlayerIdentity.Players(conn).Any(p => p.PlayerId == id)));

        Chk("SetSelf は未知の player_id では false（原本 UPDATE の rowcount=0）",
            !PlayerIdentity.SetSelf(conn, 999999, true));
        Chk("★既知の player_id では true", PlayerIdentity.SetSelf(conn, makoId, true));
        Chk("★self_names が自分の別名 4 つを返す（部分一致を捨てられる）",
            PlayerIdentity.SelfNames(conn).SetEquals(boundNow));
        Chk("『ab』のような短い名前で暴発しない（PlayerB は self_names に無い）",
            !PlayerIdentity.SelfNames(conn).Contains("PlayerB"));
        Chk("self_player_ids が makoId だけ", PlayerIdentity.SelfPlayerIds(conn).SetEquals(new[] { makoId }));
        Chk("★他の人の is_self には触らない（PlayerB は false のまま）",
            !PlayerIdentity.Players(conn).Single(p => p.PlayerId == PlayerIdentity.PlayerIdForName(conn, "PlayerB")).IsSelf);
        Chk("降ろせる（取り違えても戻せる）", PlayerIdentity.SetSelf(conn, makoId, false)
            && PlayerIdentity.SelfNames(conn).Count == 0);
        PlayerIdentity.SetSelf(conn, makoId, true);

        var candAfter = PlayerAliasCandidates.Hints(data.Keys, PlayerIdentity.AliasMap(conn));
        Chk("★束ねた Mako ↔ mako はもう候補に出ない（済んだ判断を蒸し返さない）",
            !Has(candAfter, "Mako", "mako"));
        Chk("★束ねた Mako ↔ Mako@Reimu ももう候補に出ない", !Has(candAfter, "Mako", "Mako@Reimu"));
        Chk($"候補は減った（束ねる前 {candBefore.Count} 件 → 後 {candAfter.Count} 件）",
            candAfter.Count < candBefore.Count);

        List<(string A, string B, string Why)> LegacyHintsNoSkip(IEnumerable<string> names)
        {
            var ordered = names.OrderBy(x => x, StringComparer.Ordinal).ToList();
            var outp = new List<(string, string, string)>();
            for (var i = 0; i < ordered.Count; i++)
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    var a = ordered[i]; var b = ordered[j];
                    var na = PlayerAliasCandidates.Norm(a); var nb = PlayerAliasCandidates.Norm(b);
                    if (na == nb) outp.Add((a, b, "同じ"));
                    else if (na.Length >= 2 && nb.Length >= 2
                             && (na.StartsWith(nb, StringComparison.Ordinal) || nb.StartsWith(na, StringComparison.Ordinal)))
                        outp.Add((a, b, "接頭辞"));
                }
            return outp;
        }
        var legacy = LegacyHintsNoSkip(data.Keys);
        Chk("★旧実装（除外なし）は Mako ↔ mako を出し続ける（束ねた後も）", Has(legacy, "Mako", "mako"));
        Chk("★新実装はそこを除外している（旧と新で件数が違う）", legacy.Count > candAfter.Count);

        Chk("時期が重なる（Mako 25/01/02〜04 と PlayerB 25/01/02〜08/02 は重なる）",
            PlayerAliasCandidates.OverlapText(data["Mako"], data["PlayerB"]) == "時期が重なる");
        Chk("★時期が重ならなくても手がかりは手がかりのまま（mako は 24/05/01 の 1 本だけ）",
            PlayerAliasCandidates.OverlapText(data["Mako"], data["mako"]) == "時期が重ならない"
            && Has(candBefore, "Mako", "mako"));
        var noDates = new NameStat();
        Chk("初出・最終が無ければ『時期不明』", PlayerAliasCandidates.OverlapText(data["Mako"], noDates) == "時期不明");

        var raisedMissingAlias = false;
        try { PlayerIdentity.MergeAlias(conn, "存在しない名前", makoId); }
        catch (InvalidOperationException) { raisedMissingAlias = true; }
        Chk("★★未登録の別名を merge しようとすると例外", raisedMissingAlias);
        var raisedMissingPlayer = false;
        try { PlayerIdentity.MergeAlias(conn, "PlayerB", 424242); }
        catch (InvalidOperationException) { raisedMissingPlayer = true; }
        Chk("★★未知の player_id へ merge しようとすると例外", raisedMissingPlayer);

        var beforeAt = ReadAliasRow(conn, "Mako");
        PlayerIdentity.SetPlayerAlias(conn, "Mako", makoId, source: "override", firstSeenAt: "99/12/31");
        var afterAt = ReadAliasRow(conn, "Mako");
        Chk("★既にある first_seen_at / source は付け替えで動かない",
            afterAt.FirstSeenAt == beforeAt.FirstSeenAt && afterAt.Source == beforeAt.Source);

        var threwNullConn = false;
        try { PlayerIdentity.Players(null!); } catch (ArgumentNullException) { threwNullConn = true; }
        Chk("null の接続は ArgumentNullException", threwNullConn);
    }

    private static void ExecSql(SqliteConnection conn, string sql, params object?[] values)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        for (var i = 0; i < values.Length; i++)
            cmd.Parameters.AddWithValue("$" + i.ToString(CultureInfo.InvariantCulture), values[i] ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static long InsertBareSession(SqliteConnection conn, long sessionId)
    {
        ExecSql(conn, "INSERT INTO sessions(session_id,started_at,status,logger_version)"
                    + " VALUES($0,$1,'completed','selftest')", sessionId, "2026-08-09T10:00:00+09:00");
        return sessionId;
    }

    private static (string? FirstSeenAt, string? Source) ReadAliasRow(SqliteConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT first_seen_at, source FROM player_aliases WHERE name=$n";
        cmd.Parameters.AddWithValue("$n", name);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return (null, null);
        return (r.IsDBNull(0) ? null : r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1));
    }

    private static void LegacyTests()
    {
        Console.WriteLine("=== 旧実装の写しを置いて、旧なら落ちること（否定テスト）===");

        static string LegacyFloat(double v) => "f:" + v.ToString("R", CultureInfo.InvariantCulture);
        var pyNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000L);
        var netNaN = double.NaN;
        Chk("★旧: 10 進で書くと 2 つの NaN が同じ文字列に潰れる",
            LegacyFloat(pyNaN) == LegacyFloat(netNaN));
        Chk("新: ビットで書けば潰れない（★どちらの NaN かが見える）",
            ParityValue.FloatToken(pyNaN) != ParityValue.FloatToken(netNaN));

        var keys = new List<string> { "\ue000", "\U00020BB7" };
        var utf16 = new List<string>(keys);
        utf16.Sort(StringComparer.Ordinal);
        var utf8 = new List<string>(keys);
        utf8.Sort(CanonJson.CompareByUtf8Bytes);
        Chk("★旧: UTF-16 の順（.NET の既定）は UTF-8 の順と違う", !utf16.SequenceEqual(utf8));
        Chk("新: UTF-8 のバイト順で並ぶ（Python と同じ）",
            utf8[0] == "\ue000" && utf8[1] == "\U00020BB7");

        var legacyBroke = false;
        try { using var _ = System.Text.Json.JsonDocument.Parse("""{"n":NaN}"""); }
        catch (System.Text.Json.JsonException) { legacyBroke = true; }
        Chk("★旧: System.Text.Json は NaN を落とす（j!: に化ける）", legacyBroke);
        Chk("新: 読めて j: になる",
            ParityValue.CanonJsonToken("""{"n":NaN}""").StartsWith("j:", StringComparison.Ordinal));
    }
}
