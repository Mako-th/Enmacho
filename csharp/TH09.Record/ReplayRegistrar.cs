using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using TH09.Generated;
using TH09.Record.Generated;

using Replays = TH09.Generated.DbColumns.Replays;
using PathRows = TH09.Generated.DbColumns.ReplayPaths;
using Links = TH09.Generated.DbColumns.SessionReplays;
using Sessions = TH09.Generated.DbColumns.Sessions;

namespace TH09.Record;

public sealed record ReplayFacts(
    string Status,
    bool Decoded,
    string Source,
    int? OwnerSideAuto,
    long? Mode,
    long? Difficulty,
    string? PlayerName,
    string? ReplayDate,
    long? P1Char,
    long? P2Char,
    string? P1Name,
    string? P2Name,
    string DecodedJson);

public sealed record ReplayLinkResult(long SessionId, double Confidence, double Seconds, bool Written);

public sealed record ReplayRegistration(
    long ReplayId,
    bool ReplayInserted,
    bool PathInserted,
    string Source,
    int? OwnerSide,
    long? IsOwn,
    ReplayLinkResult? Link);

public sealed class ReplayRegistrar : IDisposable
{

    public const double LinkWindowSeconds = 600.0;

    public const double LinkNearSeconds = 30.0;

    public const double LinkMidSeconds = 120.0;

    public const double LinkConfidenceNear = 0.95;

    public const double LinkConfidenceMid = 0.85;

    public const double LinkConfidenceFar = 0.65;

    public static string MtimeLinkMethod => RecordLabels.MtimeLinkMethod;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly RecordDb _db;
    private readonly Func<string> _now;
    private SqliteTransaction? _tx;
    private bool _closed;

    private ReplayRegistrar(RecordDb db, Func<string> now)
    {
        _db = db;
        _now = now;
    }

    public static ReplayRegistrar Open(string dbPath, Func<string>? now = null)
    {
        var db = RecordDb.OpenReadWrite(dbPath);
        try
        {
            Schema.EnsureShape(db);
        }
        catch (Exception)
        {
            db.Dispose();
            throw;
        }
        return new ReplayRegistrar(db, now ?? ScanLedger.NowIso);
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _tx?.Dispose();
        _tx = null;
        _db.Dispose();
    }


    public ReplayRegistration Register(string fullPath, ReplayFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var info = new FileInfo(fullPath);
        var size = info.Length;
        var mtime = new DateTimeOffset(info.LastWriteTime);
        var sha = Sha256Hex(fullPath);
        var t = _now();

        _tx = _db.Connection.BeginTransaction();
        try
        {
            var inserted = Exec(
                $"INSERT OR IGNORE INTO {Replays.Table}"
                + $"({Replays.Sha256},{Replays.FileSize},{Replays.Mtime},"
                + $"{Replays.FirstSeenAt},{Replays.LastSeenAt},{Replays.Source})"
                + " VALUES($0,$1,$2,$3,$4,$5)",
                [sha, size, IsoText(mtime), t, t, facts.Source]) > 0;
            var replayId = ScalarLong(
                $"SELECT {Replays.ReplayId} FROM {Replays.Table} WHERE {Replays.Sha256}=$0", [sha]);

            var source = EffectiveSource(replayId, facts.Source);
            Exec($"UPDATE {Replays.Table} SET {Replays.LastSeenAt}=$0,{Replays.Source}=$1"
                 + $" WHERE {Replays.ReplayId}=$2",
                 [t, source, replayId]);

            var pathId = ScalarNullableLong(
                $"SELECT {PathRows.ReplayPathId} FROM {PathRows.Table} WHERE {PathRows.FullPath}=$0",
                [fullPath]);
            var pathInserted = pathId is null;
            if (pathId is not null)
            {
                Exec($"UPDATE {PathRows.Table} SET {PathRows.ReplayId}=$0,{PathRows.LastSeenAt}=$1,"
                     + $"{PathRows.IsCurrent}=1 WHERE {PathRows.ReplayPathId}=$2",
                     [replayId, t, pathId.Value]);
            }
            else
            {
                Exec($"INSERT INTO {PathRows.Table}"
                     + $"({PathRows.ReplayId},{PathRows.FullPath},{PathRows.Filename},"
                     + $"{PathRows.ParentHint},{PathRows.FirstSeenAt},{PathRows.LastSeenAt},"
                     + $"{PathRows.IsCurrent})"
                     + " VALUES($0,$1,$2,$3,$4,$5,1)",
                     [replayId, fullPath, FileName(fullPath), ParentName(fullPath), t, t]);
            }

            var side = OwnSideFor(replayId, facts);
            long? isOwn = side is null ? null : (side.Value != 0 ? 1L : 0L);
            Exec($"UPDATE {Replays.Table} SET {Replays.Mode}=$0,{Replays.Difficulty}=$1,"
                 + $"{Replays.PlayerName}=$2,{Replays.ReplayDate}=$3,{Replays.P1Char}=$4,"
                 + $"{Replays.P2Char}=$5,{Replays.P1Name}=$6,{Replays.P2Name}=$7,"
                 + $"{Replays.IsOwn}=$8,{Replays.OwnerSide}=$9,{Replays.DecodeStatus}=$10,"
                 + $"{Replays.DecodedJson}=$11 WHERE {Replays.ReplayId}=$12",
                 [facts.Mode, facts.Difficulty, facts.PlayerName, facts.ReplayDate,
                  facts.P1Char, facts.P2Char, facts.P1Name, facts.P2Name,
                  isOwn, side is null ? null : (long)side.Value, facts.Status, facts.DecodedJson,
                  replayId]);

            var link = Link(replayId, mtime);
            _tx.Commit();
            return new ReplayRegistration(replayId, inserted, pathInserted, source, side, isOwn, link);
        }
        catch (Exception)
        {
            try { _tx.Rollback(); } catch (Exception) { }
            throw;
        }
        finally
        {
            _tx.Dispose();
            _tx = null;
        }
    }

    public int SweepMissing(IEnumerable<string> fullPaths)
    {
        ArgumentNullException.ThrowIfNull(fullPaths);
        var n = 0;
        _tx = _db.Connection.BeginTransaction();
        try
        {
            foreach (var path in fullPaths)
            {
                n += Exec($"UPDATE {PathRows.Table} SET {PathRows.IsCurrent}=0,"
                          + $"{PathRows.LastSeenAt}=$0 WHERE {PathRows.FullPath}=$1",
                          [_now(), path]);
            }
            _tx.Commit();
        }
        catch (Exception)
        {
            try { _tx.Rollback(); } catch (Exception) { }
            throw;
        }
        finally
        {
            _tx.Dispose();
            _tx = null;
        }
        return n;
    }

    public IReadOnlyList<string> CurrentPaths()
    {
        var list = new List<string>();
        using var cmd = Command(
            $"SELECT {PathRows.FullPath} FROM {PathRows.Table} WHERE {PathRows.IsCurrent}=1"
            + $" ORDER BY {PathRows.ReplayPathId}", []);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }


    private string EffectiveSource(long replayId, string source)
    {
        var current = ScalarString(
            $"SELECT {Replays.Source} FROM {Replays.Table} WHERE {Replays.ReplayId}=$0", [replayId]);
        return string.Equals(current, HitWindowConst.ReplaySourceAdonis, StringComparison.Ordinal)
            ? HitWindowConst.ReplaySourceAdonis : source;
    }

    private int? OwnSideFor(long replayId, ReplayFacts facts)
    {
        var side = facts.Decoded ? facts.OwnerSideAuto : null;
        long? over;
        try
        {
            over = ScalarNullableLong(
                $"SELECT {Replays.OwnOverride} FROM {Replays.Table} WHERE {Replays.ReplayId}=$0",
                [replayId]);
        }
        catch (Exception)
        {
            over = null;
        }
        return over is not null ? (int)over.Value : side;
    }

    private ReplayLinkResult? Link(long replayId, DateTimeOffset mtime)
    {
        var found = false;
        long bestSession = 0;
        var bestSeconds = 0.0;
        using (var cmd = Command(
            $"SELECT {Sessions.SessionId},{Sessions.EndedAt} FROM {Sessions.Table}"
            + $" WHERE {Sessions.EndedAt} IS NOT NULL", []))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetValue(1) is not string text) continue;
                if (!TryParseAware(text, out var ended)) continue;
                var seconds = Math.Abs((mtime - ended).TotalSeconds);
                if (seconds > LinkWindowSeconds) continue;
                if (found && seconds >= bestSeconds) continue;
                found = true;
                bestSeconds = seconds;
                bestSession = reader.GetInt64(0);
            }
        }
        if (!found) return null;

        var confidence = bestSeconds <= LinkNearSeconds ? LinkConfidenceNear
                       : bestSeconds <= LinkMidSeconds ? LinkConfidenceMid
                       : LinkConfidenceFar;

        var exists = false;
        double? currentConfidence = null;
        string? currentMethod = null;
        using (var cmd = Command(
            $"SELECT {Links.LinkConfidence},{Links.LinkMethod} FROM {Links.Table}"
            + $" WHERE {Links.SessionId}=$0 AND {Links.ReplayId}=$1",
            [bestSession, replayId]))
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                exists = true;
                currentConfidence = reader.IsDBNull(0) ? null : reader.GetDouble(0);
                currentMethod = reader.GetValue(1) as string;
            }
        }

        var written = false;
        if (!exists)
        {
            Exec($"INSERT INTO {Links.Table}"
                 + $"({Links.SessionId},{Links.ReplayId},{Links.LinkConfidence},{Links.LinkMethod})"
                 + " VALUES($0,$1,$2,$3)",
                 [bestSession, replayId, confidence, MtimeLinkMethod]);
            written = true;
        }
        else if (!string.Equals(currentMethod, ScanLink.Method, StringComparison.Ordinal)
                 && !(currentConfidence is not null && currentConfidence.Value >= ScanLink.Confidence))
        {
            Exec($"UPDATE {Links.Table} SET {Links.LinkConfidence}=$0,{Links.LinkMethod}=$1"
                 + $" WHERE {Links.SessionId}=$2 AND {Links.ReplayId}=$3",
                 [confidence, MtimeLinkMethod, bestSession, replayId]);
            written = true;
        }
        return new ReplayLinkResult(bestSession, confidence, bestSeconds, written);
    }


    public static string Sha256Hex(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                          1 << 20, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        return Convert.ToHexStringLower(sha.ComputeHash(stream));
    }

    public static string IsoText(DateTimeOffset value)
    {
        var micro = value.Ticks % TimeSpan.TicksPerSecond / 10;
        return micro == 0
            ? value.ToString("yyyy-MM-ddTHH:mm:sszzz", Inv)
            : value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz", Inv);
    }

    private static bool TryParseAware(string text, out DateTimeOffset value)
    {
        value = default;
        if (!HasOffset(text)) return false;
        return DateTimeOffset.TryParse(text, Inv, DateTimeStyles.RoundtripKind, out value);
    }

    private static bool HasOffset(string text)
    {
        if (text.EndsWith('Z') || text.EndsWith('z')) return true;
        var sep = text.IndexOfAny(['T', 't', ' ']);
        if (sep < 0) return false;
        return text.AsSpan(sep + 1).LastIndexOfAny('+', '-') >= 0;
    }

    private static string FileName(string fullPath) => Path.GetFileName(fullPath);

    private static string ParentName(string fullPath)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(dir)) return "";
        var info = new DirectoryInfo(dir);
        return info.Parent is null ? "" : info.Name;
    }


    private SqliteCommand Command(string sql, IReadOnlyList<object?> values)
    {
        var cmd = _db.Connection.CreateCommand();
        cmd.Transaction = _tx;
        cmd.CommandText = sql;
        for (var i = 0; i < values.Count; i++)
            cmd.Parameters.AddWithValue("$" + i.ToString(Inv), values[i] ?? DBNull.Value);
        return cmd;
    }

    private int Exec(string sql, IReadOnlyList<object?> values)
    {
        using var cmd = Command(sql, values);
        return cmd.ExecuteNonQuery();
    }

    private long ScalarLong(string sql, IReadOnlyList<object?> values)
    {
        using var cmd = Command(sql, values);
        return Convert.ToInt64(cmd.ExecuteScalar(), Inv);
    }

    private long? ScalarNullableLong(string sql, IReadOnlyList<object?> values)
    {
        using var cmd = Command(sql, values);
        var raw = cmd.ExecuteScalar();
        return raw is null || raw is DBNull ? null : Convert.ToInt64(raw, Inv);
    }

    private string? ScalarString(string sql, IReadOnlyList<object?> values)
    {
        using var cmd = Command(sql, values);
        var raw = cmd.ExecuteScalar();
        return raw is null || raw is DBNull ? null : Convert.ToString(raw, Inv);
    }
}
