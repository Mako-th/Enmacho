using System.Globalization;
using Microsoft.Data.Sqlite;

using PlayerCols = TH09.Generated.DbColumns.Players;
using AliasCols = TH09.Generated.DbColumns.PlayerAliases;

namespace TH09.Record;

public sealed record PlayerRow(long PlayerId, string DisplayName, bool IsSelf, string? Notes, string? CreatedAt);

public static class PlayerIdentity
{

    public static Dictionary<string, long> AliasMap(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {AliasCols.Name},{AliasCols.PlayerId} FROM {AliasCols.Table}";
            using var r = cmd.ExecuteReader();
            while (r.Read()) map[r.GetString(0)] = r.GetInt64(1);
        }
        catch (SqliteException)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }
        return map;
    }

    public static long? PlayerIdForName(SqliteConnection c, string? name)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (string.IsNullOrEmpty(name)) return null;
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {AliasCols.PlayerId} FROM {AliasCols.Table} WHERE {AliasCols.Name}=$n";
            cmd.Parameters.AddWithValue("$n", name);
            var v = cmd.ExecuteScalar();
            return v is null or DBNull ? null : Convert.ToInt64(v, CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    public static List<PlayerRow> Players(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var rows = new List<PlayerRow>();
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {PlayerCols.PlayerId},{PlayerCols.DisplayName},{PlayerCols.IsSelf},"
                             + $"{PlayerCols.Notes},{PlayerCols.CreatedAt} FROM {PlayerCols.Table}"
                             + $" ORDER BY {PlayerCols.PlayerId}";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                rows.Add(new PlayerRow(r.GetInt64(0), r.GetString(1), r.GetInt64(2) != 0,
                    r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4)));
            }
        }
        catch (SqliteException)
        {
            return new List<PlayerRow>();
        }
        return rows;
    }

    public static HashSet<long> SelfPlayerIds(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var ids = new HashSet<long>();
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {PlayerCols.PlayerId} FROM {PlayerCols.Table}"
                             + $" WHERE {PlayerCols.IsSelf}=1";
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt64(0));
        }
        catch (SqliteException)
        {
            return new HashSet<long>();
        }
        return ids;
    }

    public static HashSet<string> SelfNames(SqliteConnection c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var names = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT a.{AliasCols.Name} FROM {AliasCols.Table} a"
                             + $" JOIN {PlayerCols.Table} p USING({PlayerCols.PlayerId})"
                             + $" WHERE p.{PlayerCols.IsSelf}=1";
            using var r = cmd.ExecuteReader();
            while (r.Read()) names.Add(r.GetString(0));
        }
        catch (SqliteException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        return names;
    }


    public static long AddPlayer(SqliteConnection c, string displayName, bool isSelf = false,
                                 string? notes = null, string? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(displayName);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"INSERT INTO {PlayerCols.Table}({PlayerCols.DisplayName},{PlayerCols.IsSelf},"
                         + $"{PlayerCols.Notes},{PlayerCols.CreatedAt}) VALUES($n,$s,$notes,$at);"
                         + " SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", displayName);
        cmd.Parameters.AddWithValue("$s", isSelf ? 1L : 0L);
        cmd.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$at", createdAt ?? LiveTickSource.NowIso());
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static void SetPlayerAlias(SqliteConnection c, string name, long playerId,
                                      string? source = null, string? firstSeenAt = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(name);
        var keepAt = firstSeenAt;
        var keepSrc = source;
        using (var sel = c.CreateCommand())
        {
            sel.CommandText = $"SELECT {AliasCols.FirstSeenAt},{AliasCols.Source} FROM {AliasCols.Table}"
                             + $" WHERE {AliasCols.Name}=$n";
            sel.Parameters.AddWithValue("$n", name);
            using var r = sel.ExecuteReader();
            if (r.Read())
            {
                if (!r.IsDBNull(0) && r.GetString(0).Length > 0) keepAt = r.GetString(0);
                if (!r.IsDBNull(1) && r.GetString(1).Length > 0) keepSrc = r.GetString(1);
            }
        }
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"INSERT OR REPLACE INTO {AliasCols.Table}({AliasCols.Name},{AliasCols.PlayerId},"
                         + $"{AliasCols.FirstSeenAt},{AliasCols.Source}) VALUES($n,$p,$at,$src)";
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$at", (object?)keepAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$src", (object?)keepSrc ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static (long Before, long After) MergeAlias(SqliteConnection c, string name, long intoPlayerId)
    {
        ArgumentNullException.ThrowIfNull(c);
        var before = PlayerIdForName(c, name);
        if (before is null)
            throw new InvalidOperationException($"merge_alias: 別名 {name} は登録されていません");
        if (!PlayerExists(c, intoPlayerId))
            throw new InvalidOperationException($"merge_alias: player_id={intoPlayerId} は登録されていません");
        SetPlayerAlias(c, name, intoPlayerId);
        return (before.Value, intoPlayerId);
    }

    public static bool SetSelf(SqliteConnection c, long playerId, bool isSelf = true)
    {
        ArgumentNullException.ThrowIfNull(c);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"UPDATE {PlayerCols.Table} SET {PlayerCols.IsSelf}=$v"
                         + $" WHERE {PlayerCols.PlayerId}=$id";
        cmd.Parameters.AddWithValue("$v", isSelf ? 1L : 0L);
        cmd.Parameters.AddWithValue("$id", playerId);
        return cmd.ExecuteNonQuery() > 0;
    }

    private static bool PlayerExists(SqliteConnection c, long playerId)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM {PlayerCols.Table} WHERE {PlayerCols.PlayerId}=$id";
        cmd.Parameters.AddWithValue("$id", playerId);
        return cmd.ExecuteScalar() is not null;
    }
}
