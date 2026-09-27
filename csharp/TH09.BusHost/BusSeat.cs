using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using TH09.TickBus;

[assembly: SupportedOSPlatform("windows")]

namespace TH09.BusHost;

public enum SeatOutcome
{
    Initialized,

    ForcedReset,

    Joined,

    JoinedArmed,
}

public sealed class BusSeat : IDisposable
{
    private MemoryMappedFile? _section;

    internal BusSeat(string name, long size, SeatOutcome outcome, MemoryMappedFile section)
    {
        Name = name;
        Size = size;
        Outcome = outcome;
        _section = section;
    }

    public string Name { get; }

    public long Size { get; }

    public SeatOutcome Outcome { get; }

    public bool Initialized => Outcome is SeatOutcome.Initialized or SeatOutcome.ForcedReset;

    public bool IsOpen => _section is not null;

    public SharedMemoryView OpenReadOnlyView()
    {
        ObjectDisposedException.ThrowIf(_section is null, this);
        return SharedMemoryView.OpenReadOnly(Name);
    }

    public void Dispose()
    {
        _section?.Dispose();
        _section = null;
    }
}
