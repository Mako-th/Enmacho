namespace TH09.Record;

public static class SessionWriterDump
{
    public const string Flag = "--session-writer";

    public static int Run(TextWriter w, string dbPath, string writerLock)
    {
        ArgumentNullException.ThrowIfNull(w);
        if (SessionWriter.IsRealLockName(writerLock))
        {
            Console.Error.WriteLine(
                "★検査用の名前しか受け付けない（本物のロックを握ると、"
                + "動いている監視・走査を実際に止めてしまう）: " + writerLock);
            return 1;
        }

        var logs = new List<string>();
        using var writer = SessionWriter.OpenWithSpareLock(dbPath, writerLock, logs.Add);
        foreach (var line in logs) Row(w, "log", line);
        if (writer is null) return 3;

        Row(w, "opened", "1");
        Row(w, "tables", writer.Connection.Database);
        return 0;
    }

    private static void Row(TextWriter w, params string[] cells) =>
        w.Write(string.Join("\t", cells) + "\n");
}
