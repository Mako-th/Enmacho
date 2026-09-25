using System.Globalization;
using Microsoft.Data.Sqlite;
using TH09.Generated;

using ScanJobs = TH09.Generated.DbColumns.ReplayScanJobs;
using ScanItems = TH09.Generated.DbColumns.ReplayScanItems;

namespace TH09.Record;

public sealed record ScanItemResult(
    string Status,
    long? SessionId = null,
    string? VerifyStatus = null,
    long? GapCount = null,
    string? Error = null,
    IReadOnlyDictionary<string, long?>? Health = null);

public sealed class ScanLedger : IDisposable
{
    public const string StatusRunning = "running";

    private static readonly string[] LedgerOwnColumns =
    [
        ScanItems.ItemId, ScanItems.JobId, ScanItems.ReplayId, ScanItems.StartedAt,
        ScanItems.EndedAt, ScanItems.Status, ScanItems.SessionId, ScanItems.VerifyStatus,
        ScanItems.GapCount, ScanItems.Attempt, ScanItems.Error,
    ];

    public static readonly IReadOnlyList<string> HealthColumns =
        ScanItems.All.Where(c => !LedgerOwnColumns.Contains(c, StringComparer.Ordinal)).ToArray();

    private static readonly object Gate = new();
    private static string? _heldWriterName;
    private static string? _heldScanName;
    private static Mutex? _heldWriter;
    private static Mutex? _heldScan;
    private static int _heldUsers;

    private readonly RecordDb _db;
    private bool _closed;

    private ScanLedger(RecordDb db) => _db = db;

    public SqliteConnection Connection => _db.Connection;

    public static int ItemColumnCount => LedgerOwnColumns.Length + HealthColumns.Count;


    public static ScanLedger? Open(string dbPath, Action<string>? log = null) =>
        OpenWithLocks(dbPath, ExclusiveLocks.SessionWriter, ExclusiveLocks.ScanActive, log);

    public static bool IsRealLockName(string name) =>
        ExclusiveLocks.All.Contains(name, StringComparer.Ordinal);

    public static ScanLedger? OpenWithSpareLocks(string dbPath, string writerName, string scanName,
                                                 Action<string>? log = null)
    {
        foreach (var name in new[] { writerName, scanName })
        {
            if (!IsRealLockName(name)) continue;
            throw new ArgumentException(
                "★検査用の名前しか受け付けない（本物のロックを握ると、"
                + "動いている監視・走査・watcher を実際に止めてしまう）: " + name);
        }
        return OpenWithLocks(dbPath, writerName, scanName, log);
    }

    internal static ScanLedger? OpenWithLocks(string dbPath, string writerName, string scanName,
                                              Action<string>? log)
    {
        lock (Gate)
        {
            if (_heldWriterName is null)
            {
                var writer = TakeLock(writerName);
                if (writer is null)
                {
                    log?.Invoke("別のプロセスが同じ DB へセッションを書いています"
                                + "（監視を OFF にするか、走査を止めてから実行してください）");
                    return null;
                }
                var scan = TakeLock(scanName);
                if (scan is null)
                    log?.Invoke("警告: 走査中の印を立てられません。"
                                + "watcher がスロットを登録するかもしれません。");
                _heldWriterName = writerName;
                _heldScanName = scanName;
                _heldWriter = writer;
                _heldScan = scan;
            }
            else if (!string.Equals(_heldWriterName, writerName, StringComparison.Ordinal)
                     || !string.Equals(_heldScanName, scanName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"このプロセスは既に別の名前でロックを持っている（{_heldWriterName} / {_heldScanName}）");
            }
            _heldUsers++;
        }

        try
        {
            var db = RecordDb.OpenReadWrite(dbPath);
            Schema.EnsureShape(db);
            return new ScanLedger(db);
        }
        catch (Exception)
        {
            ReleaseOne();
            throw;
        }
    }

    private static Mutex? TakeLock(string name)
    {
        Mutex? m = null;
        try
        {
            m = new Mutex(true, name, out var createdNew);
            if (createdNew) return m;
            m.Dispose();
            return null;
        }
        catch (Exception)
        {
            m?.Dispose();
            return null;
        }
    }


    public static bool ScanIsActive() => IsSignalled(ExclusiveLocks.ScanActive);

    internal static bool IsSignalled(string name)
    {
        try
        {
            if (!Mutex.TryOpenExisting(name, out var found)) return false;
            found.Dispose();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ReleaseOne()
    {
        lock (Gate)
        {
            if (_heldWriterName is null) return;
            if (--_heldUsers > 0) return;
            GiveBack(_heldWriter);
            GiveBack(_heldScan);
            _heldWriter = null;
            _heldScan = null;
            _heldWriterName = null;
            _heldScanName = null;
        }
    }

    private static void GiveBack(Mutex? m)
    {
        if (m is null) return;
        try
        {
            m.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            m.Dispose();
        }
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _db.Dispose();
        ReleaseOne();
    }


    public static string NowIso() =>
        DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    public long StartJob(string? speedSetting, string? note) =>
        InsertReturningId(
            $"INSERT INTO {ScanJobs.Table}"
            + $"({ScanJobs.StartedAt},{ScanJobs.Status},{ScanJobs.SpeedSetting},{ScanJobs.Note})"
            + " VALUES($0,$1,$2,$3)",
            [NowIso(), StatusRunning, speedSetting, note]);

    public void EndJob(long jobId, string status) =>
        Run($"UPDATE {ScanJobs.Table} SET {ScanJobs.EndedAt}=$0,{ScanJobs.Status}=$1"
            + $" WHERE {ScanJobs.JobId}=$2",
            [NowIso(), status, jobId]);

    public long StartItem(long jobId, long replayId, int attempt) =>
        InsertReturningId(
            $"INSERT INTO {ScanItems.Table}"
            + $"({ScanItems.JobId},{ScanItems.ReplayId},{ScanItems.StartedAt},"
            + $"{ScanItems.Status},{ScanItems.Attempt})"
            + " VALUES($0,$1,$2,$3,$4)",
            [jobId, replayId, NowIso(), StatusRunning, (long)attempt]);

    public void EndItem(long itemId, ScanItemResult r)
    {
        var columns = new List<string>
        {
            ScanItems.EndedAt, ScanItems.Status, ScanItems.SessionId,
            ScanItems.VerifyStatus, ScanItems.GapCount, ScanItems.Error,
        };
        var values = new List<object?>
        {
            NowIso(), r.Status, r.SessionId, r.VerifyStatus, r.GapCount, r.Error,
        };
        foreach (var name in HealthColumns)
        {
            columns.Add(name);
            values.Add(r.Health is not null && r.Health.TryGetValue(name, out var v) ? v : null);
        }
        var sets = string.Join(",", columns.Select((c, i) => c + "=$" + i.ToString(CultureInfo.InvariantCulture)));
        values.Add(itemId);
        Run($"UPDATE {ScanItems.Table} SET {sets}"
            + $" WHERE {ScanItems.ItemId}=${values.Count - 1}", values);
    }


    private void Run(string sql, IReadOnlyList<object?> values)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, values);
        cmd.ExecuteNonQuery();
    }

    private long InsertReturningId(string sql, IReadOnlyList<object?> values)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = sql + "; SELECT last_insert_rowid();";
        Bind(cmd, values);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Bind(SqliteCommand cmd, IReadOnlyList<object?> values)
    {
        for (var i = 0; i < values.Count; i++)
            cmd.Parameters.AddWithValue("$" + i.ToString(CultureInfo.InvariantCulture),
                                        values[i] ?? DBNull.Value);
    }
}
