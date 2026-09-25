using Microsoft.Data.Sqlite;
using TH09.Generated;

namespace TH09.Record;

public sealed class SessionWriter : IDisposable
{
    private readonly RecordDb _db;
    private readonly Mutex _heldWriter;
    private bool _closed;

    private SessionWriter(RecordDb db, Mutex heldWriter)
    {
        _db = db;
        _heldWriter = heldWriter;
    }

    public SqliteConnection Connection => _db.Connection;

    public static SessionWriter? Open(string dbPath, Action<string>? log = null) =>
        OpenWithLock(dbPath, ExclusiveLocks.SessionWriter, log);

    public static bool IsRealLockName(string name) =>
        ExclusiveLocks.All.Contains(name, StringComparer.Ordinal);

    public static SessionWriter? OpenWithSpareLock(string dbPath, string writerName,
                                                   Action<string>? log = null)
    {
        if (IsRealLockName(writerName))
            throw new ArgumentException(
                "★検査用の名前しか受け付けない（本物のロックを握ると、"
                + "動いている監視・走査を実際に止めてしまう）: " + writerName);
        return OpenWithLock(dbPath, writerName, log);
    }

    private static SessionWriter? OpenWithLock(string dbPath, string writerName, Action<string>? log)
    {
        var heldWriter = TakeLock(writerName);
        if (heldWriter is null)
        {
            log?.Invoke("別のプロセスが同じ DB へセッションを書いています");
            return null;
        }

        try
        {
            var db = RecordDb.OpenReadWrite(dbPath);
            Schema.EnsureShape(db);
            return new SessionWriter(db, heldWriter);
        }
        catch
        {
            GiveBack(heldWriter);
            throw;
        }
    }

    private static Mutex? TakeLock(string name)
    {
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(true, name, out var createdNew);
            if (createdNew) return mutex;
            mutex.Dispose();
            return null;
        }
        catch
        {
            mutex?.Dispose();
            return null;
        }
    }

    private static void GiveBack(Mutex mutex)
    {
        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            mutex.Dispose();
        }
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _db.Dispose();
        GiveBack(_heldWriter);
    }
}
