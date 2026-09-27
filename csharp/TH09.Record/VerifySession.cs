using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TH09.Record.Generated;

using BonusCols = TH09.Generated.DbColumns.ClearBonuses;
using EventCols = TH09.Generated.DbColumns.Events;
using MetaCols = TH09.Generated.DbColumns.SessionMetadata;
using RoundCols = TH09.Generated.DbColumns.Rounds;
using StageCols = TH09.Generated.DbColumns.Stages;

namespace TH09.Record;

public sealed record VerifyResult(string Status, IReadOnlyList<string> Lines);

public static class VerifySession
{
    public const string StatusOk = "ok";

    public const string StatusMismatch = "mismatch";

    private const string MarkOk = "  OK   ";

    private const string MarkNg = "  NG   ";

    public static VerifyResult Run(SqliteConnection conn, long sessionId, CanonJson.Node decoded)
    {
        var meta = ReadMeta(conn, sessionId);
        if (meta is null)
            return new VerifyResult(StatusMismatch, ["session_metadata がありません"]);

        var checks = new List<(bool Ok, string Text)>();
        void Add(bool ok, string label, string got, string want) =>
            checks.Add((ok, $"{label}: 実測={got} / リプレイ={want}"));

        var wantDifficulty = Member(decoded, "difficulty");
        Add(SameNumber(meta.Difficulty, wantDifficulty), "難易度",
            DifficultyLabel(meta.Difficulty), DifficultyLabel(wantDifficulty));

        var wantChar = Member(decoded, "p1_char");
        Add(SameNumber(meta.P1Character, wantChar), "1Pキャラ",
            MonitorRules.Char(meta.P1Character ?? throw new InvalidDataException(
                "session_metadata.p1_character が NULL（原本は M.char(None) で TypeError）")),
            wantChar is null || wantChar.Kind == CanonJson.Kind.Null
                ? "?" : MonitorRules.Char(ReplayStages.AsPythonInt(wantChar, "p1_char")));

        var want = ReplayStages.P1Stages(decoded);
        var got = ReadStages(conn, sessionId);
        Add(got.Count == want.Count, "ステージ数",
            got.Count.ToString(CultureInfo.InvariantCulture),
            want.Count.ToString(CultureInfo.InvariantCulture));

        for (var i = 0; i < want.Count; i++)
        {
            var score = want[i].Score ?? throw new InvalidDataException(
                "リプレイの面に score が無い（原本は want['score'] で KeyError）");
            var label = $"Stage {i + 1} 開始スコア";
            if (i >= got.Count)
            {
                checks.Add((false, $"{label}: 実測なし / リプレイ={FormatScore(score)}"));
                continue;
            }
            Add(SameNumber(got[i], score), label,
                got[i] is { } v ? Grouped(v) : "None", FormatScore(score));
        }

        var status = checks.All(c => c.Ok) ? StatusOk : StatusMismatch;
        return new VerifyResult(status, checks.Select(c => (c.Ok ? MarkOk : MarkNg) + c.Text).ToList());
    }

    public static IReadOnlyList<string> CaptureSummary(SqliteConnection conn, long sessionId)
    {
        var lines = new List<string>();

        var rounds = Row(conn,
            $"SELECT COUNT(*), SUM({RoundCols.DurationFrames}),"
            + $" SUM(CASE WHEN {RoundCols.Status}='completed' THEN 1 ELSE 0 END)"
            + $" FROM {RoundCols.Table} WHERE {RoundCols.SessionId}=$0", sessionId);
        lines.Add($"ラウンド: {PyStr(rounds[0])} 件（completed {PyStr(rounds[2])} 件）"
                  + $"/ 合計 {PyStr(rounds[1])} フレーム = {MonitorRules.FmtFrames(OrZero(rounds[1]))}");

        var bonus = Row(conn,
            $"SELECT COUNT(*), MAX({BonusCols.MaximumCombo}), SUM({BonusCols.SpellAttackCount}),"
            + $" SUM({BonusCols.BossAttackCount}), SUM({BonusCols.BossReversalCount}),"
            + $" MAX({BonusCols.ScoreAfterBonus})"
            + $" FROM {BonusCols.Table} WHERE {BonusCols.SessionId}=$0", sessionId);
        lines.Add($"クリアボーナス: {PyStr(bonus[0])} 件 / 最大コンボ {PyStr(bonus[1])} /"
                  + $" カードアタック計 {PyStr(bonus[2])} / ボスカード計 {PyStr(bonus[3])} /"
                  + $" リバーサル計 {PyStr(bonus[4])}");

        var final = Row(conn,
            $"SELECT MAX({StageCols.ScoreAtEnd}) FROM {StageCols.Table}"
            + $" WHERE {StageCols.SessionId}=$0", sessionId)[0];
        lines.Add("最終通しスコア: " + (final is null ? "—" : Grouped(Convert.ToInt64(final, CultureInfo.InvariantCulture))));

        var kinds = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT {EventCols.EventType}, COUNT(*) FROM {EventCols.Table}"
                            + $" WHERE {EventCols.SessionId}=$0 GROUP BY {EventCols.EventType}"
                            + " ORDER BY 2 DESC";
            cmd.Parameters.AddWithValue("$0", sessionId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                kinds.Add(PyStr(r.IsDBNull(0) ? null : r.GetValue(0)) + "×"
                          + PyStr(r.GetValue(1)));
        }
        lines.Add("イベント: " + string.Join(", ", kinds));
        return lines;
    }


    private sealed record Meta(long? Difficulty, long? P1Character);

    private static Meta? ReadMeta(SqliteConnection conn, long sessionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {MetaCols.Difficulty},{MetaCols.P1Character}"
                        + $" FROM {MetaCols.Table} WHERE {MetaCols.SessionId}=$0";
        cmd.Parameters.AddWithValue("$0", sessionId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Meta(r.IsDBNull(0) ? null : r.GetInt64(0),
                        r.IsDBNull(1) ? null : r.GetInt64(1));
    }

    private static List<long?> ReadStages(SqliteConnection conn, long sessionId)
    {
        var list = new List<long?>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {StageCols.ScoreAtStart} FROM {StageCols.Table}"
                        + $" WHERE {StageCols.SessionId}=$0 ORDER BY {StageCols.StageRecordId}";
        cmd.Parameters.AddWithValue("$0", sessionId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.IsDBNull(0) ? null : r.GetInt64(0));
        return list;
    }

    private static object?[] Row(SqliteConnection conn, string sql, long sessionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$0", sessionId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return new object?[r.FieldCount];
        var row = new object?[r.FieldCount];
        for (var i = 0; i < row.Length; i++) row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
        return row;
    }


    private static string PyStr(object? v) => v switch
    {
        null => "None",
        long n => n.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "None",
    };

    private static long OrZero(object? v) =>
        v is null ? 0 : Convert.ToInt64(v, CultureInfo.InvariantCulture);

    private static string Grouped(long v) => v.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatScore(CanonJson.Node score)
    {
        if (score.Kind == CanonJson.Kind.Bool) return score.Bool ? "1" : "0";
        if (score.Kind != CanonJson.Kind.Int)
            throw new InvalidDataException(
                $"リプレイの score が整数でない（原本は書式でここが落ちる）: {CanonJson.Canon(score)}");
        return GroupDigits(score.Text);
    }

    private static string GroupDigits(string text)
    {
        var negative = text.StartsWith('-');
        var digits = negative ? text[1..] : text;
        var sb = new StringBuilder(digits.Length + digits.Length / 3 + 1);
        if (negative) sb.Append('-');
        var head = digits.Length % 3;
        if (head == 0) head = 3;
        sb.Append(digits, 0, head);
        for (var i = head; i < digits.Length; i += 3)
        {
            sb.Append(',');
            sb.Append(digits, i, 3);
        }
        return sb.ToString();
    }

    private static string DifficultyLabel(long? v) =>
        v is { } n && n <= int.MaxValue && n >= int.MinValue
                   && RecordLabels.Difficulties.TryGetValue((int)n, out var name)
            ? name
            : PyStr(v);

    private static string DifficultyLabel(CanonJson.Node? v)
    {
        if (v is null || v.Kind == CanonJson.Kind.Null) return "None";
        if (v.Kind == CanonJson.Kind.Int || v.Kind == CanonJson.Kind.Bool)
            return DifficultyLabel(ReplayStages.AsPythonInt(v, "difficulty"));
        if (v.Kind == CanonJson.Kind.String) return v.Text;
        throw new InvalidDataException(
            $"difficulty が数でも文字列でもない: {CanonJson.Canon(v)}");
    }

    private static CanonJson.Node? Member(CanonJson.Node decoded, string name) =>
        decoded.Members.TryGetValue(name, out var v) ? v : null;

    private static bool SameNumber(long? dbValue, CanonJson.Node? jsonValue)
    {
        var jsonIsNone = jsonValue is null || jsonValue.Kind == CanonJson.Kind.Null;
        if (dbValue is null || jsonIsNone) return dbValue is null && jsonIsNone;
        if (jsonValue!.Kind == CanonJson.Kind.Bool) return dbValue.Value == (jsonValue.Bool ? 1 : 0);
        if (jsonValue.Kind != CanonJson.Kind.Int) return false;
        return long.TryParse(jsonValue.Text, NumberStyles.AllowLeadingSign,
                             CultureInfo.InvariantCulture, out var n) && dbValue.Value == n;
    }
}
