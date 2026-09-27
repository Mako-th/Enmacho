using Microsoft.Data.Sqlite;
using TH09.Layer0.Generated;

namespace TH09.Layer0;

public enum Layer0OpenMode
{
    Create,

    Append,
}

public sealed class Layer0Writer : IDisposable
{
    public const string StampTable = Layer0Stamp.Table;

    public static string SourceKind => Layer0Schema.SourceKindTick;

    public sealed record FieldSlot(string Name, int WordIndex);

    public sealed record Options(
        string DbPath,
        Layer0OpenMode Mode,
        int RecordVersion,
        IReadOnlyList<FieldSlot> Fields,
        TickEncoder.SectionPolicy Policy,
        int MaxSegmentTicks,
        int PendingTickLimit = Layer0Schema.PendingTickLimit,
        int Level = Layer0Schema.DefaultLevel,
        bool Verify = Layer0Schema.VerifyDefault,
        string WriteEncoding = TickArchive.Encoding);

    public sealed record Stats(long Segments, long Ticks, long Bytes,
                               long Lost, long Torn, long VerifyChecked, long VerifyFailures);

    private readonly Options _o;
    private readonly TextWriter _log;
    private readonly SqliteConnection _conn;
    private readonly List<uint>[] _cols;
    private readonly string[] _names;

    private long? _sessionId;
    private long _segmentNo;
    private int _tickCount;
    private long _lostBase, _tornBase, _lost, _torn;
    private long _segments, _ticks, _bytes, _verifyChecked, _verifyFailures;
    private bool _disposed;


    private Layer0Writer(Options o, TextWriter log, SqliteConnection conn)
    {
        _o = o;
        _log = log;
        _conn = conn;
        _names = [.. o.Fields.Select(f => f.Name)];
        _cols = new List<uint>[o.Fields.Count];
        for (int i = 0; i < _cols.Length; i++) _cols[i] = [];
    }

    public static Layer0Writer Open(Options o, TextWriter? log = null)
    {
        log ??= TextWriter.Null;
        Validate(o);

        bool exists = File.Exists(o.DbPath);
        if (o.Mode == Layer0OpenMode.Create && exists)
            throw new InvalidOperationException(
                "★行き先が既にあります: " + o.DbPath
                + "。★この道具は既にあるファイルへ新規に建てません"
                + "（書き足すなら追記の指定で開いてください）。");
        if (o.Mode == Layer0OpenMode.Append && !exists)
            throw new InvalidOperationException("★書き足す先がありません: " + o.DbPath);

        if (o.Mode == Layer0OpenMode.Append)
        {
            using var probe = OpenReadOnly(o.DbPath);
            if (!HasTable(probe, StampTable))
                throw new InvalidOperationException(
                    $"★行き先に印（{StampTable}）がありません: " + o.DbPath
                    + "。★これは、この道具が建てたファイルではありません。");
        }

        if (o.Mode == Layer0OpenMode.Create
            && Path.GetDirectoryName(Path.GetFullPath(o.DbPath)) is { Length: > 0 } parent)
        {
            Directory.CreateDirectory(parent);
        }

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = o.DbPath,
            Mode = o.Mode == Layer0OpenMode.Create
                ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        };
        var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        try
        {
            ApplyPragmas(conn);
            if (o.Mode == Layer0OpenMode.Create)
            {
                foreach (var sql in Layer0Schema.Statements) Exec(conn, sql);
                Layer0Stamp.Create(conn);
                Stamp(conn, Layer0Stamp.StateCreated, o.WriteEncoding);
            }
            else
            {
                Stamp(conn, Layer0Stamp.StateOpened, o.WriteEncoding);
            }
        }
        catch
        {
            conn.Dispose();
            throw;
        }
        return new Layer0Writer(o, log, conn);
    }

    private static void Validate(Options o)
    {
        if (!string.Equals(TickArchive.Encoding, Layer0Schema.Encoding, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"★形式名が食い違っています（手書き {TickArchive.Encoding} / Python {Layer0Schema.Encoding}）。");
        if (o.Fields.Count == 0)
            throw new InvalidOperationException("★列が 0 本です。★これは「空の記録」ではなく「渡し忘れ」。");
        if (o.MaxSegmentTicks < 1)
            throw new InvalidOperationException("★1 セグメントの上限 tick 数は 1 以上にしてください。");
        if (o.PendingTickLimit < 0)
            throw new InvalidOperationException("★抱えておく tick 数は 0 以上にしてください。");
        if (o.WriteEncoding != TickArchive.Encoding && o.WriteEncoding != SolidBrotli.Encoding
            && o.WriteEncoding != SolidBrotli.EncodingV2)
            throw new InvalidOperationException(
                $"★知らない WriteEncoding です: {o.WriteEncoding}"
                + $"（対応は {TickArchive.Encoding} / {SolidBrotli.Encoding} / {SolidBrotli.EncodingV2}）。");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in o.Fields)
        {
            if (string.IsNullOrEmpty(f.Name))
                throw new InvalidOperationException("★名前の無い列があります。");
            if (f.WordIndex < 0)
                throw new InvalidOperationException($"★列 {f.Name} の語の位置が負です。");
            if (!seen.Add(f.Name))
                throw new InvalidOperationException(
                    $"★列 {f.Name} が 2 本あります。★列は名前で引くので、重なると片方が黙って消えます。");
        }
    }


    public void BeginSession(long sessionId)
    {
        if (_sessionId is not null && sessionId != _sessionId)
        {
            Flush();
            _segmentNo = 0;
        }
        _sessionId = sessionId;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(segment_no) FROM session_ticks WHERE session_id=$s";
        cmd.Parameters.AddWithValue("$s", sessionId);
        var v = cmd.ExecuteScalar();
        if (v is not null && v is not DBNull) _segmentNo = Convert.ToInt64(v) + 1;
    }

    public void EndSession()
    {
        Flush();
        _sessionId = null;
        _segmentNo = 0;
    }


    public void Add(IReadOnlyList<uint[]> records, long? lostRecords = null, long? tornRecords = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (lostRecords is not null) _lost = lostRecords.Value - _lostBase;
        if (tornRecords is not null) _torn = tornRecords.Value - _tornBase;

        foreach (var words in records)
        {
            for (int i = 0; i < _cols.Length; i++)
            {
                int at = _o.Fields[i].WordIndex;
                if (at >= words.Length)
                    throw new InvalidDataException(
                        $"★レコードが短すぎます（{words.Length} 語）。"
                        + $"列 {_o.Fields[i].Name} は {at} 語目を見ます。");
                _cols[i].Add(words[at]);
            }
            _tickCount++;

            if (_sessionId is null)
            {
                if (_o.PendingTickLimit > 0 && _tickCount > _o.PendingTickLimit * 5 / 4)
                    DropOldest(_tickCount - _o.PendingTickLimit);
            }
            else if (_tickCount >= _o.MaxSegmentTicks)
            {
                Flush();
            }
        }
    }

    private void DropOldest(int count)
    {
        foreach (var col in _cols) col.RemoveRange(0, count);
        _tickCount -= count;
    }


    public void BreakSegment(string reason = "")
    {
        if (_tickCount > 0)
            _log.WriteLine("[layer0] セグメントを区切ります（"
                           + (string.IsNullOrEmpty(reason) ? "指定なし" : reason) + "）");
        Flush();
    }

    public bool Flush()
    {
        if (_tickCount == 0 || _sessionId is null) return false;

        var map = new Dictionary<string, uint[]>(_cols.Length, StringComparer.Ordinal);
        for (int i = 0; i < _cols.Length; i++) map[_names[i]] = [.. _cols[i]];
        var enc = TickEncoder.EncodeColumns(map, _names, _o.Policy, _o.RecordVersion, _o.Level);

        WriteSegment(enc.Blob, enc.FieldOrder.ToCompactJson(escapeNonAscii: false),
                     enc.UncompressedBytes, _tickCount);

        foreach (var col in _cols) col.Clear();
        _tickCount = 0;
        _lostBase += _lost;
        _tornBase += _torn;
        _lost = 0;
        _torn = 0;
        return true;
    }

    private static byte[] PackForWrite(byte[] blob, string encoding) => encoding switch
    {
        _ when encoding == SolidBrotli.EncodingV2 => SolidBrotli.PackV2(blob),
        _ when encoding == SolidBrotli.Encoding => SolidBrotli.Pack(blob),
        _ => blob,
    };

    private void WriteSegment(byte[] blob, string fieldOrder, long rawBytes, int tickCount)
    {
        string encoding = _o.WriteEncoding;
        byte[] outBlob = PackForWrite(blob, encoding);

        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO session_ticks(session_id,segment_no,record_version,source_kind,"
                + "lost_records,torn_records,tick_count,field_order,encoding,"
                + "uncompressed_bytes,compressed_bytes,blob)"
                + " VALUES($s,$g,$v,$k,$l,$t,$n,$f,$e,$u,$c,$b)";
            cmd.Parameters.AddWithValue("$s", _sessionId!.Value);
            cmd.Parameters.AddWithValue("$g", _segmentNo);
            cmd.Parameters.AddWithValue("$v", _o.RecordVersion);
            cmd.Parameters.AddWithValue("$k", SourceKind);
            cmd.Parameters.AddWithValue("$l", _lost);
            cmd.Parameters.AddWithValue("$t", _torn);
            cmd.Parameters.AddWithValue("$n", tickCount);
            cmd.Parameters.AddWithValue("$f", fieldOrder);
            cmd.Parameters.AddWithValue("$e", encoding);
            cmd.Parameters.AddWithValue("$u", rawBytes);
            cmd.Parameters.AddWithValue("$c", outBlob.Length);
            cmd.Parameters.AddWithValue("$b", outBlob);
            cmd.ExecuteNonQuery();
        }

        if (_o.Verify)
        {
            _verifyChecked++;
            var err = VerifySegment(_conn, _sessionId.Value, _segmentNo, tickCount, encoding);
            if (err is not null)
            {
                _verifyFailures++;
                _log.WriteLine($"[layer0] ★書いたセグメントを読み戻せません"
                               + $"（session={_sessionId} seg={_segmentNo}）: {err}。"
                               + "★この回は保険が成立していません");
            }
        }

        _segmentNo++;
        _segments++;
        _ticks += tickCount;
        _bytes += outBlob.Length;
    }

    public Stats GetStats() =>
        new(_segments, _ticks, _bytes, _lostBase + _lost, _tornBase + _torn,
            _verifyChecked, _verifyFailures);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Flush(); }
        finally
        {
            try { Stamp(_conn, Layer0Stamp.StateClosed, _o.WriteEncoding); }
            catch (SqliteException) { }
            _conn.Dispose();
        }
    }


    public static string? VerifySegment(SqliteConnection conn, long sessionId, long segmentNo,
                                        long tickCount, string? expectedEncoding = null)
    {
        try
        {
            string fieldOrderText;
            byte[] blob;
            string encoding;
            long gotTicks;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT field_order,blob,encoding,tick_count FROM session_ticks"
                    + " WHERE session_id=$s AND segment_no=$g";
                cmd.Parameters.AddWithValue("$s", sessionId);
                cmd.Parameters.AddWithValue("$g", segmentNo);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return "書いたはずのセグメントが読み出せません";
                fieldOrderText = r.GetString(0);
                blob = (byte[])r.GetValue(1);
                encoding = r.GetString(2);
                gotTicks = r.GetInt64(3);
            }
            if (expectedEncoding is not null
                && !string.Equals(encoding, expectedEncoding, StringComparison.Ordinal))
                return $"encoding が {encoding}（書いたのは {expectedEncoding}）";
            if (gotTicks != tickCount)
                return $"tick_count が {gotTicks}（書いたのは {tickCount}）";

            var order = TickArchive.ParseFieldOrder(fieldOrderText);
            var cols = TickArchive.Decode(blob, encoding, order);

            long want = Math.Min(Layer0Schema.VerifyRecords, tickCount);
            long n = 0;
            for (int i = 0; i < want; i++)
            {
                foreach (var name in order.Fields)
                {
                    var col = cols[name];
                    if (i >= col.Length)
                        return $"列 {name} が {col.Length} 語しかありません（{i} 語目を見ています）";
                    _ = col[i];
                }
                n++;
            }
            if (n != want) return $"読み戻せたレコードが {n} 件（期待 {want} 件）";
        }
        catch (Exception exc)
        {
            return $"{exc.GetType().Name}: {exc.Message}";
        }
        return null;
    }

    public static (long Checked, long Failed, long Declared) Recheck(string path, TextWriter log)
    {
        using var conn = OpenReadOnly(path);
        var rows = new List<(long Session, long Segment, long Ticks)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT session_id,segment_no,tick_count FROM session_ticks"
                              + " ORDER BY session_id, segment_no";
            using var r = cmd.ExecuteReader();
            while (r.Read()) rows.Add((r.GetInt64(0), r.GetInt64(1), r.GetInt64(2)));
        }

        long checkedCount = 0, failed = 0, declared = 0;
        foreach (var (session, segment, ticks) in rows)
        {
            if (ticks == 0) { declared++; continue; }
            checkedCount++;
            var err = VerifySegment(conn, session, segment, ticks);
            if (err is not null)
            {
                failed++;
                log.WriteLine($"  ★読み戻せません session={session} seg={segment}: {err}");
            }
        }
        return (checkedCount, failed, declared);
    }


    public sealed record HitWindowInfo(long SessionId, long WindowNo, long FirstSeq, int TickCount,
                                       int CompressedBytes, long UncompressedBytes,
                                       int FieldOrderBytes, long LostTicks,
                                       bool Verified, string? VerifyError);

    public long NextHitWindowNo(long sessionId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(window_no) FROM session_hit_windows WHERE session_id=$s";
        cmd.Parameters.AddWithValue("$s", sessionId);
        var v = cmd.ExecuteScalar();
        return v is null || v is DBNull ? 0 : Convert.ToInt64(v) + 1;
    }

    public HitWindowInfo WriteHitWindow(long sessionId, long windowNo, long firstSeq,
                                        IReadOnlyDictionary<string, uint[]> columns,
                                        IReadOnlyList<string> fields, string hitsJson,
                                        long? firstFrame, long lostTicks,
                                        int slotCount, string quant,
                                        IReadOnlyList<string>? verifyOnly = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var enc = TickEncoder.EncodeColumns(columns, fields, _o.Policy, _o.RecordVersion, _o.Level);
        var (foBlob, foEncoding) =
            TickArchive.PackFieldOrder(enc.FieldOrder, enc.Sections, compress: true, omitFields: true);
        int tickCount = (int)enc.FieldOrder.Int("tick_count", 0);
        string encoding = _o.WriteEncoding;
        byte[] outBlob = PackForWrite(enc.Blob, encoding);

        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO session_hit_windows(session_id,window_no,first_seq,first_frame,"
                + "tick_count,slot_count,quant,hits,lost_ticks,field_order,field_order_encoding,"
                + "encoding,uncompressed_bytes,compressed_bytes,blob)"
                + " VALUES($s,$w,$q,$r,$n,$c,$u,$h,$l,$f,$g,$e,$b,$z,$d)";
            cmd.Parameters.AddWithValue("$s", sessionId);
            cmd.Parameters.AddWithValue("$w", windowNo);
            cmd.Parameters.AddWithValue("$q", firstSeq);
            cmd.Parameters.AddWithValue("$r", firstFrame is null ? DBNull.Value : firstFrame.Value);
            cmd.Parameters.AddWithValue("$n", tickCount);
            cmd.Parameters.AddWithValue("$c", slotCount);
            cmd.Parameters.AddWithValue("$u", quant);
            cmd.Parameters.AddWithValue("$h", hitsJson);
            cmd.Parameters.AddWithValue("$l", lostTicks);
            cmd.Parameters.AddWithValue("$f", foBlob);
            cmd.Parameters.AddWithValue("$g", foEncoding);
            cmd.Parameters.AddWithValue("$e", encoding);
            cmd.Parameters.AddWithValue("$b", enc.UncompressedBytes);
            cmd.Parameters.AddWithValue("$z", outBlob.Length);
            cmd.Parameters.AddWithValue("$d", outBlob);
            cmd.ExecuteNonQuery();
        }

        string? err = _o.Verify
            ? VerifyHitWindow(_conn, sessionId, windowNo, tickCount, verifyOnly)
            : null;
        return new HitWindowInfo(sessionId, windowNo, firstSeq, tickCount, outBlob.Length,
                                 enc.UncompressedBytes, foBlob.Length, lostTicks, _o.Verify, err);
    }

    public static string? VerifyHitWindow(SqliteConnection conn, long sessionId, long windowNo,
                                          long tickCount, IReadOnlyList<string>? only)
    {
        try
        {
            byte[] foBlob, blob;
            string foEncoding, encoding;
            long gotTicks;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT tick_count,field_order,field_order_encoding,encoding,blob"
                    + " FROM session_hit_windows WHERE session_id=$s AND window_no=$w";
                cmd.Parameters.AddWithValue("$s", sessionId);
                cmd.Parameters.AddWithValue("$w", windowNo);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return "書いたはずの窓が読み出せません";
                gotTicks = r.GetInt64(0);
                foBlob = (byte[])r.GetValue(1);
                foEncoding = r.GetString(2);
                encoding = r.GetString(3);
                blob = (byte[])r.GetValue(4);
            }
            if (gotTicks != tickCount)
                return $"tick_count が {gotTicks}（書いたのは {tickCount}）";

            var order = TickArchive.UnpackFieldOrder(foBlob, foEncoding);
            if (order.Fields.Count == 0) return "field_order から列名を復元できません";

            var want = only is null ? null : new HashSet<string>(only, StringComparer.Ordinal);
            var cols = TickArchive.Decode(blob, encoding, order, want);
            if (only is not null)
            {
                var missing = only.Where(n => !cols.Has(n)).ToList();
                if (missing.Count > 0) return "列が復元できません: " + string.Join(",", missing);
                var bad = only.Where(n => cols[n].Length != tickCount).ToList();
                if (bad.Count > 0) return "列の長さが tick 数と違います: " + string.Join(",", bad);
            }
            else
            {
                foreach (var name in order.Fields)
                {
                    if (!cols.Has(name)) return "列が復元できません: " + name;
                    if (cols[name].Length != tickCount)
                        return $"列 {name} の長さが {cols[name].Length}（tick 数は {tickCount}）";
                }
            }
        }
        catch (Exception exc)
        {
            return $"{exc.GetType().Name}: {exc.Message}";
        }
        return null;
    }


    private static SqliteConnection OpenReadOnly(string path)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = Layer0Schema.BusyTimeoutMs / 1000,
        };
        var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        Exec(conn, "PRAGMA query_only=1;");
        return conn;
    }

    private static void ApplyPragmas(SqliteConnection conn)
    {
        Exec(conn, $"PRAGMA busy_timeout={Layer0Schema.BusyTimeoutMs};");
        Exec(conn, "PRAGMA journal_mode=WAL;");
        using var read = conn.CreateCommand();
        read.CommandText = "PRAGMA journal_mode;";
        var mode = read.ExecuteScalar() as string ?? "";
        if (string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            Exec(conn, $"PRAGMA synchronous={Layer0Schema.SynchronousWal};");
    }

    private static void Stamp(SqliteConnection conn, string state, string encoding) =>
        Layer0Stamp.Write(conn, state,
                          $"th09_layer0 write / {encoding}"
                          + $" / schema {Layer0Schema.Fingerprint}");

    private static bool HasTable(SqliteConnection conn, string name) =>
        Layer0Stamp.HasTable(conn, name);

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
