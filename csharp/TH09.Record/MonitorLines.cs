namespace TH09.Record;

public static class MonitorLines
{
    public const string GameGone = "th09.exe が終了しました。次の起動を待ちます。";

    public const string GameFoundPrefix = "th09.exe を見つけました（pid=";

    public const string GameFoundSuffix = "）。";

    public const string SessionOpenedPrefix = "再生を検出しました: session=";

    public const string SessionClosedPrefix = "session=";

    public const string SessionClosedSuffix = " を閉じました";

    public static string GameFound(int pid) =>
        GameFoundPrefix + pid.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + GameFoundSuffix;

    public static string SessionClosed(long sessionId) =>
        SessionClosedPrefix
        + sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + SessionClosedSuffix;

    public static bool? SessionMark(string? line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        if (line.StartsWith(SessionOpenedPrefix, StringComparison.Ordinal)) return true;
        if (line.StartsWith(SessionClosedPrefix, StringComparison.Ordinal)
            && line.EndsWith(SessionClosedSuffix, StringComparison.Ordinal)) return false;
        if (string.Equals(line, GameGone, StringComparison.Ordinal)) return false;
        return null;
    }

    public static bool IsNotice(string? line) =>
        !string.IsNullOrEmpty(line)
        && (SessionMark(line) is not null
            || line.StartsWith(GameFoundPrefix, StringComparison.Ordinal));
}
