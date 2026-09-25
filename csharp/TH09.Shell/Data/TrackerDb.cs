using Microsoft.Data.Sqlite;

namespace TH09.Shell.Data;

internal static class TrackerDb
{
    public static string? MainDbPath { get; set; }

    public static bool MainDbExists
        => !string.IsNullOrEmpty(MainDbPath) && File.Exists(MainDbPath);

    public static string? Layer0DbPath { get; set; }

    public static TH09.Analysis.AnalysisDb OpenMainDb()
    {
        if (string.IsNullOrEmpty(MainDbPath))
            throw new InvalidOperationException(
                "本体 DB の場所が未設定（TrackerDb.MainDbPath）。TH09.Record.Paths.Default.MainDb を入れる");
        return new TH09.Analysis.AnalysisDb(MainDbPath);
    }

    public static void ForEachRow(TH09.Analysis.AnalysisDb db, string sql, Action<SqliteDataReader> onRow)
    {
        using var cmd = db.Command(sql);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) onRow(reader);
    }

    public static long? Int64OrNull(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);

    public static int? Int32OrNull(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);

    public static double? DoubleOrNull(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDouble(i);

    public static string? StringOrNull(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
