using System.Runtime.Versioning;
using TH09.ProcView;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
public static class GamePid
{
    public const string ExeName = "th09.exe";

    public const string AdonisModule = "adonis2.dll";

    public const int NotProbed = ProcessLookup.NotProbed;

    public static int Find() => ProcessLookup.FindPid(ExeName);

    public static bool StillAlive(int pid)
    {
        if (pid <= 0) return false;
        var now = Find();
        return now == NotProbed || now == pid;
    }

    public static bool AdonisLoaded(int pid) =>
        ProcessLookup.FindModule(pid, AdonisModule) == ModuleState.Loaded;

    public static string? GameExePath(int pid) => ProcessLookup.GameExePath(pid);

    public static ProcessMemory? OpenMemory(int pid, out int error) =>
        ProcessMemory.TryOpen(pid, out error);
}
