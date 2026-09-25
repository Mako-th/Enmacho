using TH09.Record;

namespace TH09.Shell.Data;

internal static class StreamBlockSource
{
    public static IReadOnlyList<PlayInsertBlock> Load(string dbPath, long? sid,
        IReadOnlyList<StreamTargetEntry> targets, out StreamPanelView? panel,
        IReadOnlyList<string>? hidden = null, bool narrow = false)
    {
        panel = null;
        if (!OperatingSystem.IsWindows()) return [];
        try
        {
            using var record = RecordDb.OpenReadOnly(dbPath);
            if (record is null) return [];
            panel = StreamBests.BuildSessionPanel(record.Connection, sid, targets);
            return panel is null ? [] : StreamCompareFormat.ComposeBlocks(panel, hidden, narrow);
        }
        catch (Exception)
        {
            panel = null;
            return [];
        }
    }
}
