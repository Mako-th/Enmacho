using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

namespace TH09.Record;

public static class SessionRestore
{
    public static IReadOnlyDictionary<string, long> Restore(
        SqliteConnection c, long sessionId, string? payloadJson = null,
        string source = "tombstone", string sessionWall = "exact",
        bool note = true, string? at = null, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var say = log ?? (_ => { });
        payloadJson ??= Repository.TombstoneJson(c, sessionId)
            ?? throw new InvalidOperationException(
                $"restore_session: session={sessionId} の控え（tombstone）がありません");

        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("tables", out var tablesEl) || tablesEl.ValueKind != JsonValueKind.Object
            || !tablesEl.TryGetProperty("sessions", out var sessionsRows)
            || sessionsRows.ValueKind != JsonValueKind.Array || sessionsRows.GetArrayLength() == 0)
            throw new InvalidOperationException(
                $"restore_session: session={sessionId} の控えに sessions の行がありません");

        using (var check = c.CreateCommand())
        {
            check.CommandText = "SELECT 1 FROM sessions WHERE session_id=$0";
            check.Parameters.AddWithValue("$0", sessionId);
            if (check.ExecuteScalar() is not null)
                throw new InvalidOperationException(
                    $"restore_session: session={sessionId} は既に本体DBにあります"
                    + "（別のセッションが同じ id を使っている可能性があるので上書きしません）");
        }

        var wrote = new Dictionary<string, long>();
        RecordDb.Exec(c, "PRAGMA foreign_keys=OFF");
        using var tx = c.BeginTransaction();
        try
        {
            if (tablesEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var tableProp in tablesEl.EnumerateObject())
                {
                    var table = tableProp.Name;
                    var rows = tableProp.Value;
                    if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0) continue;
                    if (!TableExists(c, table)) continue;
                    var have = TableColumns(c, table);
                    long n = 0;
                    foreach (var rowEl in rows.EnumerateArray())
                    {
                        InsertRow(c, tx, table, rowEl, have, sessionId, table == "sessions" && note, source, sessionWall, at);
                        n++;
                    }
                    wrote[table] = wrote.GetValueOrDefault(table) + n;
                }
            }

            if (root.TryGetProperty("refs", out var refsEl) && refsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var (table, col) in RecordLabels.SessionRefNullable)
                {
                    if (!refsEl.TryGetProperty(table, out var idsEl) || idsEl.ValueKind != JsonValueKind.Array
                        || idsEl.GetArrayLength() == 0 || !TableExists(c, table))
                        continue;
                    long changed = 0;
                    foreach (var idEl in idsEl.EnumerateArray())
                    {
                        using var upd = c.CreateCommand();
                        upd.Transaction = tx;
                        upd.CommandText = $"UPDATE \"{table}\" SET \"{col}\"=$0 WHERE rowid=$1 AND \"{col}\" IS NULL";
                        upd.Parameters.AddWithValue("$0", sessionId);
                        upd.Parameters.AddWithValue("$1", idEl.GetInt64());
                        changed += upd.ExecuteNonQuery();
                    }
                    if (changed > 0) wrote[table] = wrote.GetValueOrDefault(table) + changed;
                }
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        say($"[DB] session={sessionId} を復元しました（"
            + string.Join(", ", wrote.Select(kv => kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture)))
            + "）");
        return wrote;
    }

    private static void InsertRow(SqliteConnection c, SqliteTransaction tx, string table, JsonElement rowEl,
                                  HashSet<string> have, long sessionId, bool stampNote,
                                  string source, string sessionWall, string? at)
    {
        var cols = new List<string>();
        var values = new List<object>();
        foreach (var cell in rowEl.EnumerateObject())
        {
            var col = cell.Name;
            if (!have.Contains(col)) continue;
            object value;
            if (string.Equals(col, "session_id", StringComparison.Ordinal))
                value = sessionId;
            else if (table == "sessions" && stampNote && string.Equals(col, "notes", StringComparison.Ordinal))
                value = RestoredNote(cell.Value.ValueKind == JsonValueKind.Null ? null : cell.Value.GetString(),
                                    source, sessionWall, at) is { } s ? s : (object)DBNull.Value;
            else
                value = JsonElementToSqliteValue(cell.Value);
            cols.Add(col);
            values.Add(value);
        }
        using var ins = c.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = $"INSERT OR REPLACE INTO \"{table}\"({string.Join(",", cols.Select(x => "\"" + x + "\""))})"
                         + $" VALUES({string.Join(",", cols.Select((_, i) => "$" + i))})";
        for (var i = 0; i < cols.Count; i++) ins.Parameters.AddWithValue("$" + i, values[i]);
        ins.ExecuteNonQuery();
    }

    private static object JsonElementToSqliteValue(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => DBNull.Value,
        JsonValueKind.True => 1L,
        JsonValueKind.False => 0L,
        JsonValueKind.Number => e.TryGetInt64(out var n) ? n : e.GetDouble(),
        JsonValueKind.String => e.GetString()!,
        _ => throw new InvalidDataException(
            $"控えの値に配列／オブジェクトが出てきた（{e.ValueKind}）。TOMBSTONE_TABLES の行に"
            + "そんな列は無いはずなので、扱いを決めてから戻すこと。"),
    };

    internal static string RestoredNote(string? original, string source, string sessionWall, string? at = null)
    {
        var mark = $"[{RecordLabels.RestoredNoteMark} {at ?? LiveTickSource.NowIso()} src={source} "
                 + $"session_wall={sessionWall} child_wall=synthetic]";
        return string.IsNullOrEmpty(original) ? mark : $"{original} {mark}";
    }

    private static bool TableExists(SqliteConnection c, string name)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$0";
            cmd.Parameters.AddWithValue("$0", name);
            return cmd.ExecuteScalar() is not null;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static HashSet<string> TableColumns(SqliteConnection c, string table)
    {
        var have = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var r = cmd.ExecuteReader();
        while (r.Read()) have.Add(r.GetString(1));
        return have;
    }
}
