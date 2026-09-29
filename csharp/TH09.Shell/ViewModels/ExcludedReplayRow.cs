using TH09.Record;

namespace TH09.Shell.ViewModels;

internal sealed record ExcludedReplayRow(ExcludedReplayEntry Entry)
{
    public string Text => Entry.FileName + "　（除外 " + Entry.ExcludedDay + "）";
}
