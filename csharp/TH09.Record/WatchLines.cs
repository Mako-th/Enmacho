namespace TH09.Record;

public static class WatchLines
{
    public const string RegisteredPrefix = "登録: ";

    public static bool IsRegistered(string? line) =>
        !string.IsNullOrEmpty(line) && line.StartsWith(RegisteredPrefix, StringComparison.Ordinal);
}
