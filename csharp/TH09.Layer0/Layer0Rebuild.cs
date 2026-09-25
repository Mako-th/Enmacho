using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using TH09.Layer0.Generated;

namespace TH09.Layer0;

public static class Layer0Rebuild
{
    public const string StampTable = "rebuild_stamp";

    private static readonly string[] ReplacedAlways = ["blob", "encoding", "compressed_bytes"];

    private static readonly string[] ReplacedFieldOrder = ["field_order", "field_order_encoding"];

    private static readonly (string Table, string KeyA, string KeyB, bool FieldOrder)[] BlobTables =
    [
        ("session_ticks", "session_id", "segment_no", false),
        ("session_hit_windows", "session_id", "window_no", true),
    ];

    private static string[] ReplacedIn(bool fieldOrder) =>
        fieldOrder ? [.. ReplacedAlways, .. ReplacedFieldOrder] : ReplacedAlways;

    private static readonly string[] PlainTables = ["hit_window_features"];

    public sealed record Options(
        string SourcePath, string DestPath, int Threads, bool Resume, int Limit, bool Deep);


    public static int Run(Options o, TextWriter log)
    {
        if (!File.Exists(o.SourcePath))
        {
            log.WriteLine("元の DB が見つかりません: " + o.SourcePath);
            return 2;
        }
        if (string.Equals(Path.GetFullPath(o.SourcePath), Path.GetFullPath(o.DestPath),
                          StringComparison.OrdinalIgnoreCase))
        {
            log.WriteLine("★元と行き先が同じファイルです。この道具は原本を書きません。");
            return 2;
        }

        var sw = Stopwatch.StartNew();
        using var src = OpenSource(o.SourcePath);
        var fingerprint = Fingerprint(src, o.SourcePath);
        log.WriteLine($"元 : {o.SourcePath}");
        log.WriteLine($"     {fingerprint}");

        using var dst = OpenDest(o, fingerprint, log);
        if (dst is null) return 2;
        log.WriteLine($"行き先: {o.DestPath}");
        log.WriteLine($"本数: {o.Threads}（論理 {Environment.ProcessorCount}）"
                      + (o.Limit > 0 ? $" / ★上限 {o.Limit:N0} 行（試し）" : ""));
        log.WriteLine();

        long done = 0, kept = 0, oldBytes = 0, newBytes = 0;

        foreach (var t in PlainTables)
        {
            long n = CopyPlain(src, dst, t);
            log.WriteLine($"{t}: {n:N0} 行をそのまま運んだ");
        }

        foreach (var (table, keyA, keyB, fo) in BlobTables)
        {
            var r = Convert(o, table, keyA, keyB, fo, src, dst, log);
            done += r.Converted;
            kept += r.Kept;
            oldBytes += r.OldBytes;
            newBytes += r.NewBytes;
        }

        log.WriteLine();
        log.WriteLine($"変換: {done:N0} 行 / ★現行のまま残した行 {kept:N0} 行"
                      + $"（新形式の方が大きい行。★印は encoding 列そのもの）");
        if (oldBytes > 0)
            log.WriteLine($"blob: {oldBytes:N0} B → {newBytes:N0} B / 比 {(double)newBytes / oldBytes:F3}");

        bool ok = VerifyTables(o, src, dst, log);
        if (ok && o.Deep) ok = VerifyDeep(o, src, dst, log);

        if (ok && o.Limit <= 0)
        {
            Stamp(dst, "completed", fingerprint);
            log.WriteLine("★完了の印を入れた（★このファイルは、もう行き先として開けない）");
        }
        else if (o.Limit > 0)
        {
            log.WriteLine("★上限つきで走らせたので、完了の印は入れない（★続きは --resume）");
        }

        Checkpoint(dst);
        sw.Stop();
        long size = new FileInfo(o.DestPath).Length;
        log.WriteLine();
        log.WriteLine($"行き先のファイル: {size:N0} B（元は {new FileInfo(o.SourcePath).Length:N0} B"
                      + $" / 比 {(double)size / new FileInfo(o.SourcePath).Length:F3}）");
        log.WriteLine($"かかった時間: {sw.Elapsed.TotalMinutes:F1} 分");
        return ok ? 0 : 1;
    }


    private static SqliteConnection OpenSource(string path)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        };
        var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        Exec(conn, "PRAGMA query_only=1;");
        return conn;
    }

    private static SqliteConnection? OpenDest(Options o, string fingerprint, TextWriter log)
    {
        bool exists = File.Exists(o.DestPath);
        if (!o.Resume && exists)
        {
            log.WriteLine("★行き先が既にあります。新規に建てるときは、無いファイルを指してください");
            log.WriteLine("  （続きからやるなら --resume。★この道具は既にあるファイルを上書きしません）");
            return null;
        }
        if (o.Resume && !exists)
        {
            log.WriteLine("★--resume ですが、行き先がありません: " + o.DestPath);
            return null;
        }

        if (o.Resume)
        {
            using (var probe = OpenSource(o.DestPath))
            {
                if (!HasTable(probe, StampTable))
                {
                    log.WriteLine($"★行き先に印（{StampTable}）がありません。"
                                  + "★これは、この道具が建てたファイルではありません");
                    return null;
                }
                var (state, stampedFingerprint) = LastStamp(probe);
                if (state == "completed")
                {
                    log.WriteLine("★この行き先は建て終わっています（完了の印が入っています）。続きはありません");
                    return null;
                }
                if (stampedFingerprint != fingerprint)
                {
                    log.WriteLine("★元の DB が、前回と違います（印と食い違う）。");
                    log.WriteLine("  印: " + stampedFingerprint);
                    log.WriteLine("  いま: " + fingerprint);
                    return null;
                }
            }
            log.WriteLine("★続きから（前回の印と元が一致）");
        }

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = o.DestPath,
            Mode = o.Resume ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        };
        var conn = new SqliteConnection(csb.ToString());
        conn.Open();

        Exec(conn, "PRAGMA journal_mode=WAL;");
        Exec(conn, "PRAGMA synchronous=NORMAL;");

        if (!o.Resume)
        {
            CopySchema(conn, o.SourcePath);
            Exec(conn, $"CREATE TABLE IF NOT EXISTS {StampTable} ("
                       + "seq INTEGER PRIMARY KEY AUTOINCREMENT,"
                       + "state TEXT NOT NULL,"
                       + "source TEXT NOT NULL,"
                       + "tool TEXT NOT NULL,"
                       + "at TEXT NOT NULL);");
            Stamp(conn, "building", fingerprint);
        }
        return conn;
    }

    private static void CopySchema(SqliteConnection dst, string sourcePath)
    {
        using var src = OpenSource(sourcePath);
        var ddl = new List<string>();
        using (var cmd = src.CreateCommand())
        {
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE sql IS NOT NULL ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read()) ddl.Add(r.GetString(0));
        }
        if (ddl.Count == 0) throw new InvalidDataException("元の DB に DDL が 1 本もありません（読めていない）");
        foreach (var sql in ddl) Exec(dst, sql);
    }

    private static void Stamp(SqliteConnection conn, string state, string fingerprint)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {StampTable}(state,source,tool,at) VALUES($s,$f,$t,$a)";
        cmd.Parameters.AddWithValue("$s", state);
        cmd.Parameters.AddWithValue("$f", fingerprint);
        cmd.Parameters.AddWithValue("$t", $"th09_layer0 rebuild / {SolidBrotli.EncodingV2}"
                                          + $" q{SolidBrotli.Quality} w{SolidBrotli.Window}");
        cmd.Parameters.AddWithValue("$a", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    private static (string State, string Source) LastStamp(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT state,source FROM {StampTable} ORDER BY seq DESC LIMIT 1";
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetString(1)) : ("", "");
    }

    private static string Fingerprint(SqliteConnection src, string path)
    {
        long size = new FileInfo(path).Length;
        long pages = ScalarLong(src, "PRAGMA page_count;");
        var parts = new List<string> { $"{size} B", $"{pages} 頁" };
        foreach (var (t, _, _, _) in BlobTables)
            parts.Add($"{t}={ScalarLong(src, $"SELECT COUNT(*) FROM {t}")}");
        foreach (var t in PlainTables)
            parts.Add($"{t}={ScalarLong(src, $"SELECT COUNT(*) FROM {t}")}");
        return string.Join(" / ", parts);
    }


    private static long CopyPlain(SqliteConnection src, SqliteConnection dst, string table)
    {
        var cols = ColumnsOf(src, table);
        long already = ScalarLong(dst, $"SELECT COUNT(*) FROM {table}");
        long want = ScalarLong(src, $"SELECT COUNT(*) FROM {table}");
        if (already == want) return already;
        if (already > 0)
            throw new InvalidDataException(
                $"{table} が途中まで入っています（{already:N0} / {want:N0}）。"
                + "★この表は途中再開に対応していません（行き先を作り直してください）");

        using var tx = dst.BeginTransaction();
        using var ins = dst.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = InsertSql(table, cols);
        var ps = new SqliteParameter[cols.Count];
        for (int i = 0; i < cols.Count; i++) ps[i] = ins.Parameters.Add(new SqliteParameter("$" + i, DBNull.Value));

        long n = 0;
        using (var cmd = src.CreateCommand())
        {
            cmd.CommandText = $"SELECT {string.Join(",", cols)} FROM {table}";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                for (int i = 0; i < cols.Count; i++) ps[i].Value = r.GetValue(i);
                ins.ExecuteNonQuery();
                n++;
            }
        }
        tx.Commit();
        return n;
    }

    private sealed record Job(object[] Values, long OldLength, long NewLength, bool Kept);

    private sealed record TableResult(long Converted, long Kept, long OldBytes, long NewBytes);

    private static TableResult Convert(Options o, string table, string keyA, string keyB,
                                       bool packFieldOrder,
                                       SqliteConnection src, SqliteConnection dst, TextWriter log)
    {
        var cols = ColumnsOf(src, table);
        foreach (var name in ReplacedIn(packFieldOrder))
        {
            if (!cols.Contains(name, StringComparer.Ordinal))
                throw new InvalidDataException($"{table} に {name} 列がありません（列の並びが変わっている）");
        }
        int iBlob = cols.IndexOf("blob");
        int iEnc = cols.IndexOf("encoding");
        int iComp = cols.IndexOf("compressed_bytes");
        int iFo = packFieldOrder ? cols.IndexOf("field_order") : -1;
        int iFoEnc = packFieldOrder ? cols.IndexOf("field_order_encoding") : -1;
        int iKeyA = cols.IndexOf(keyA);
        int iKeyB = cols.IndexOf(keyB);

        var already = new HashSet<(long, long)>();
        using (var cmd = dst.CreateCommand())
        {
            cmd.CommandText = $"SELECT {keyA},{keyB} FROM {table}";
            using var r = cmd.ExecuteReader();
            while (r.Read()) already.Add((r.GetInt64(0), r.GetInt64(1)));
        }

        var todo = new List<(long A, long B)>();
        using (var cmd = src.CreateCommand())
        {
            cmd.CommandText = $"SELECT {keyA},{keyB} FROM {table} ORDER BY LENGTH(blob) DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var key = (r.GetInt64(0), r.GetInt64(1));
                if (already.Contains(key)) continue;
                todo.Add(key);
                if (o.Limit > 0 && todo.Count >= o.Limit) break;
            }
        }
        long total = ScalarLong(src, $"SELECT COUNT(*) FROM {table}");
        log.WriteLine($"{table}: 全 {total:N0} 行 / 済み {already.Count:N0} 行 / これから {todo.Count:N0} 行");
        if (todo.Count == 0) return new TableResult(0, 0, 0, 0);

        var queue = new ConcurrentQueue<(long, long)>(todo);
        var outbox = new BlockingCollection<Job>(Math.Max(4, o.Threads * 2));
        var errors = new ConcurrentBag<string>();
        string select = $"SELECT {string.Join(",", cols)} FROM {table} WHERE {keyA}=$a AND {keyB}=$b";

        var workers = new Thread[o.Threads];
        for (int i = 0; i < o.Threads; i++)
        {
            workers[i] = new Thread(() =>
            {
                using var conn = OpenSource(o.SourcePath);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = select;
                var pa = cmd.Parameters.Add("$a", SqliteType.Integer);
                var pb = cmd.Parameters.Add("$b", SqliteType.Integer);
                while (queue.TryDequeue(out var key))
                {
                    try
                    {
                        pa.Value = key.Item1;
                        pb.Value = key.Item2;
                        var values = new object[cols.Count];
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read()) throw new InvalidDataException("行が消えました");
                            r.GetValues(values);
                        }
                        var oldBlob = (byte[])values[iBlob];
                        string encoding = (string)values[iEnc];
                        if (encoding != TickArchive.Encoding)
                            throw new InvalidDataException($"元が現行形式ではありません: {encoding}");

                        var (packed, _) = SolidBrotli.PackVerifiedV2(oldBlob);
                        bool kept = packed.Length >= oldBlob.Length;
                        if (!kept)
                        {
                            values[iBlob] = packed;
                            values[iEnc] = SolidBrotli.EncodingV2;
                            values[iComp] = (long)packed.Length;
                        }
                        if (packFieldOrder) PackFieldOrder(values, iFo, iFoEnc);
                        outbox.Add(new Job(values, oldBlob.Length, kept ? oldBlob.Length : packed.Length, kept));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{table} {key.Item1}/{key.Item2}: {ex.Message}");
                    }
                }
            }, 1 << 20)
            { IsBackground = true };
            workers[i].Start();
        }

        long converted = 0, kept = 0, ob = 0, nb = 0;
        void RunWriter()
        {
            using var ins = dst.CreateCommand();
            ins.CommandText = InsertSql(table, cols);
            var ps = new SqliteParameter[cols.Count];
            for (int i = 0; i < cols.Count; i++) ps[i] = ins.Parameters.Add(new SqliteParameter("$" + i, DBNull.Value));

            var tx = dst.BeginTransaction();
            ins.Transaction = tx;
            long sinceCommit = 0;
            var sw = Stopwatch.StartNew();
            foreach (var job in outbox.GetConsumingEnumerable())
            {
                for (int i = 0; i < cols.Count; i++) ps[i].Value = job.Values[i] ?? DBNull.Value;
                ins.ExecuteNonQuery();
                converted++;
                if (job.Kept) kept++;
                ob += job.OldLength;
                nb += job.NewLength;
                sinceCommit += job.NewLength;
                if (sinceCommit >= 256L * 1024 * 1024)
                {
                    tx.Commit();
                    tx.Dispose();
                    tx = dst.BeginTransaction();
                    ins.Transaction = tx;
                    sinceCommit = 0;
                    log.WriteLine($"  … {converted:N0} / {todo.Count:N0} 行"
                                  + $"（{sw.Elapsed.TotalMinutes:F1} 分 / {converted / sw.Elapsed.TotalSeconds:F1} 行/秒）");
                    log.Flush();
                }
            }
            tx.Commit();
            tx.Dispose();
        }

        Exception? writerFailure = null;
        var writer = new Thread(() =>
        {
            try { RunWriter(); }
            catch (Exception ex) { writerFailure = ex; outbox.CompleteAdding(); }
        }, 1 << 20)
        { IsBackground = true };
        writer.Start();

        foreach (var w in workers) w.Join();
        if (!outbox.IsAddingCompleted) outbox.CompleteAdding();
        writer.Join();
        if (writerFailure is not null)
            throw new InvalidDataException($"{table}: 書き込みで落ちた ——{writerFailure.Message}", writerFailure);

        if (!errors.IsEmpty)
        {
            foreach (var e in errors.Take(10)) log.WriteLine("  ★変換できなかった行: " + e);
            throw new InvalidDataException(
                $"{table}: 変換できなかった行が {errors.Count:N0} 本あります（これから {todo.Count:N0} 本のうち）");
        }
        if (converted != todo.Count)
            throw new InvalidDataException($"{table}: 書けた行が足りません（{converted:N0} / {todo.Count:N0}）");
        return new TableResult(converted, kept, ob, nb);
    }

    private static void PackFieldOrder(object[] values, int iFo, int iFoEnc)
    {
        var stored = (byte[])values[iFo];
        string enc = (string)values[iFoEnc];
        bool nofields = enc.EndsWith(TickArchive.FieldOrderNofieldsSuffix, StringComparison.Ordinal);
        string basis = nofields ? enc[..^TickArchive.FieldOrderNofieldsSuffix.Length] : enc;

        byte[] raw;
        if (basis == TickArchive.FieldOrderJsonZlib) raw = TickArchive.ZlibDecompress(stored, 0, stored.Length, 0);
        else if (basis == TickArchive.FieldOrderJson) raw = stored;
        else if (basis == TickArchive.FieldOrderJsonBrotli) return;
        else throw new InvalidDataException("未知の field_order 形式です: " + enc);

        var packed = TickArchive.PackJsonBrotli(raw);
        if (packed.Length >= stored.Length) return;

        var back = TickArchive.UnpackJsonBrotli(packed);
        if (!back.AsSpan().SequenceEqual(raw))
            throw new InvalidDataException(
                $"field_order の往復が一致しません（{raw.Length} B → {back.Length} B）");

        values[iFo] = packed;
        values[iFoEnc] = TickArchive.FieldOrderJsonBrotli
                         + (nofields ? TickArchive.FieldOrderNofieldsSuffix : "");
    }


    private static bool VerifyTables(Options o, SqliteConnection src, SqliteConnection dst, TextWriter log)
    {
        log.WriteLine();
        log.WriteLine("照合（表ごと）:");
        bool ok = true;
        foreach (var (table, keyA, keyB, fo) in BlobTables)
        {
            var replaced = ReplacedIn(fo);
            var cols = ColumnsOf(src, table);
            var carried = cols.Where(c => !replaced.Contains(c, StringComparer.Ordinal)).ToList();
            long ns = ScalarLong(src, $"SELECT COUNT(*) FROM {table}");
            long nd = ScalarLong(dst, $"SELECT COUNT(*) FROM {table}");
            if (o.Limit > 0)
            {
                log.WriteLine($"  {table}: 元 {ns:N0} 行 / 行き先 {nd:N0} 行（★上限つきなので一致は見ない）");
            }
            else if (ns != nd)
            {
                log.WriteLine($"  ★NG {table}: 行数が違う（元 {ns:N0} / 行き先 {nd:N0}）");
                ok = false;
                continue;
            }

            long ticksS = ScalarLong(src, $"SELECT SUM(tick_count) FROM {table}");
            long ticksD = ScalarLong(dst, $"SELECT SUM(tick_count) FROM {table}");
            long compared = 0, bad = 0;
            using (var cs = src.CreateCommand())
            using (var cd = dst.CreateCommand())
            {
                string order = $" ORDER BY {keyA},{keyB}";
                cs.CommandText = $"SELECT {string.Join(",", carried)} FROM {table}{order}";
                cd.CommandText = $"SELECT {string.Join(",", carried)} FROM {table}{order}";
                using var rs = cs.ExecuteReader();
                using var rd = cd.ExecuteReader();
                var vs = new object[carried.Count];
                var vd = new object[carried.Count];
                while (rd.Read())
                {
                    if (!rs.Read()) { bad++; break; }
                    rs.GetValues(vs);
                    rd.GetValues(vd);
                    while (o.Limit > 0 && !SameKey(vs, vd, carried, keyA, keyB) && rs.Read()) rs.GetValues(vs);
                    for (int i = 0; i < carried.Count; i++)
                    {
                        if (!SameValue(vs[i], vd[i]))
                        {
                            if (bad < 5) log.WriteLine($"  ★NG {table}: {carried[i]} が違う");
                            bad++;
                            break;
                        }
                    }
                    compared++;
                }
            }
            if (compared == 0) { log.WriteLine($"  ★NG {table}: 1 行も比べていない"); ok = false; continue; }
            if (bad > 0) { log.WriteLine($"  ★NG {table}: {bad:N0} 行が食い違う"); ok = false; continue; }
            log.WriteLine($"  ok {table}: {compared:N0} 行の「{string.Join(" / ", carried)}」が一致"
                          + (o.Limit > 0 ? "" : $" / tick 合計 {ticksS:N0} = {ticksD:N0}")
                          + $"（差し替えたのは {string.Join(" / ", replaced)} の {replaced.Length} 本だけ）");
            if (o.Limit <= 0 && ticksS != ticksD) { log.WriteLine("  ★NG tick 合計が違う"); ok = false; }
        }

        foreach (var t in PlainTables)
        {
            long ns = ScalarLong(src, $"SELECT COUNT(*) FROM {t}");
            long nd = ScalarLong(dst, $"SELECT COUNT(*) FROM {t}");
            if (ns != nd) { log.WriteLine($"  ★NG {t}: 行数が違う（元 {ns:N0} / 行き先 {nd:N0}）"); ok = false; }
            else log.WriteLine($"  ok {t}: {nd:N0} 行");
        }
        return ok;
    }

    private static bool VerifyDeep(Options o, SqliteConnection src, SqliteConnection dst, TextWriter log)
    {
        log.WriteLine();
        log.WriteLine("照合（列まで組み上げて。★行き先を読み直す）:");
        bool ok = true;
        foreach (var (table, keyA, keyB, _) in BlobTables)
        {
            bool isWindow = table == "session_hit_windows";
            var keys = new List<(long, long)>();
            using (var cmd = dst.CreateCommand())
            {
                cmd.CommandText = $"SELECT {keyA},{keyB} FROM {table}";
                using var r = cmd.ExecuteReader();
                while (r.Read()) keys.Add((r.GetInt64(0), r.GetInt64(1)));
            }
            var queue = new ConcurrentQueue<(long, long)>(keys);
            var bad = new ConcurrentBag<string>();
            long checkedRows = 0, words = 0, newFormat = 0;

            var threads = new Thread[o.Threads];
            for (int i = 0; i < o.Threads; i++)
            {
                threads[i] = new Thread(() =>
                {
                    using var cs = OpenSource(o.SourcePath);
                    using var cd = OpenSource(o.DestPath);
                    while (queue.TryDequeue(out var key))
                    {
                        try
                        {
                            var a = ReadOne(cs, table, keyA, keyB, key, isWindow);
                            var b = ReadOne(cd, table, keyA, keyB, key, isWindow);
                            string da = Parity.ColumnDigest(a.Columns);
                            string db = Parity.ColumnDigest(b.Columns);
                            if (!string.Equals(da, db, StringComparison.Ordinal))
                                bad.Add($"{table} {key.Item1}/{key.Item2}: 列のフィンガープリントが違う");
                            if (!string.Equals(a.FieldOrderDigest, b.FieldOrderDigest, StringComparison.Ordinal))
                                bad.Add($"{table} {key.Item1}/{key.Item2}: field_order の中身が違う");
                            Interlocked.Increment(ref checkedRows);
                            Interlocked.Add(ref words, a.Columns.WordCount);
                            if (b.Encoding == SolidBrotli.EncodingV2) Interlocked.Increment(ref newFormat);
                        }
                        catch (Exception ex)
                        {
                            bad.Add($"{table} {key.Item1}/{key.Item2}: {ex.Message}");
                        }
                    }
                }, 1 << 20)
                { IsBackground = true };
                threads[i].Start();
            }
            foreach (var t in threads) t.Join();

            if (checkedRows == 0) { log.WriteLine($"  ★NG {table}: 1 行も読み直していない"); ok = false; continue; }
            if (!bad.IsEmpty)
            {
                foreach (var m in bad.Take(5)) log.WriteLine("  ★NG " + m);
                log.WriteLine($"  ★NG {table}: {bad.Count:N0} 行が食い違う（{checkedRows:N0} 行のうち）");
                ok = false;
                continue;
            }
            log.WriteLine($"  ok {table}: {checkedRows:N0} 行 / {words:N0} 語が一致"
                          + $"（うち新形式で読み直したのは {newFormat:N0} 行）");
            if (newFormat == 0)
            {
                log.WriteLine($"  ★NG {table}: 新形式の行が 1 つも無い ⇒ この照合は何も確かめていない");
                ok = false;
            }
        }
        return ok;
    }

    private static (TickArchive.TickColumns Columns, string Encoding, string FieldOrderDigest) ReadOne(
        SqliteConnection conn, string table, string keyA, string keyB, (long, long) key, bool isWindow)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = isWindow
            ? $"SELECT field_order,field_order_encoding,encoding,blob FROM {table} WHERE {keyA}=$a AND {keyB}=$b"
            : $"SELECT field_order,encoding,blob FROM {table} WHERE {keyA}=$a AND {keyB}=$b";
        cmd.Parameters.AddWithValue("$a", key.Item1);
        cmd.Parameters.AddWithValue("$b", key.Item2);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new InvalidDataException("行が見つかりません");
        if (isWindow)
        {
            var order = TickArchive.UnpackFieldOrder((byte[])r.GetValue(0), r.GetString(1));
            string enc = r.GetString(2);
            return (TickArchive.Decode((byte[])r.GetValue(3), enc, order), enc,
                    Parity.FieldOrderDigest(order.Raw));
        }
        else
        {
            var order = TickArchive.ParseFieldOrder(r.GetString(0));
            string enc = r.GetString(1);
            return (TickArchive.Decode((byte[])r.GetValue(2), enc, order), enc,
                    Parity.FieldOrderDigest(order.Raw));
        }
    }


    private static List<string> ColumnsOf(SqliteConnection conn, string table)
    {
        var cols = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var r = cmd.ExecuteReader();
        while (r.Read()) cols.Add(r.GetString(1));
        if (cols.Count == 0) throw new InvalidDataException($"{table} の列が読めません（表がありません）");
        return cols;
    }

    private static string InsertSql(string table, List<string> cols) =>
        $"INSERT INTO {table}({string.Join(",", cols)})"
        + $" VALUES({string.Join(",", Enumerable.Range(0, cols.Count).Select(i => "$" + i))})";

    private static bool SameKey(object[] a, object[] b, List<string> cols, string keyA, string keyB)
    {
        int ia = cols.IndexOf(keyA), ib = cols.IndexOf(keyB);
        return SameValue(a[ia], b[ia]) && SameValue(a[ib], b[ib]);
    }

    private static bool SameValue(object a, object b)
    {
        if (a is DBNull && b is DBNull) return true;
        if (a is DBNull || b is DBNull) return false;
        if (a is byte[] xa && b is byte[] xb) return xa.AsSpan().SequenceEqual(xb);
        return Equals(a, b);
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static long ScalarLong(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? 0 : System.Convert.ToInt64(v);
    }

    private static bool HasTable(SqliteConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n";
        cmd.Parameters.AddWithValue("$n", name);
        return cmd.ExecuteScalar() is not null;
    }

    private static void Checkpoint(SqliteConnection conn) => Exec(conn, "PRAGMA wal_checkpoint(TRUNCATE);");
}
