using Microsoft.Data.Sqlite;

namespace TH09.Analysis;

public sealed class AnalysisDb : IDisposable
{
    private readonly SqliteConnection _conn;

    public string Path { get; }

    public AnalysisDb(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("DB が見つかりません: " + path, path);
        Path = path;
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = TH09.Layer0.Layer0Reader.BusyTimeoutMs / 1000,
        };
        _conn = new SqliteConnection(csb.ToString());
        _conn.Open();
        using var pragma = _conn.CreateCommand();
        pragma.CommandText = "PRAGMA query_only=1;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();

    public SqliteCommand Command(string sql, params object[] args)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        for (int i = 0; i < args.Length; i++) cmd.Parameters.AddWithValue("$" + i, args[i]);
        return cmd;
    }

    public bool HasTable(string name)
    {
        using var cmd = Command("SELECT 1 FROM sqlite_master WHERE type='table' AND name=$0", name);
        using var r = cmd.ExecuteReader();
        return r.Read();
    }
}
