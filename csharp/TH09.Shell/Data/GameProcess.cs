global using AdonisState = TH09.ProcView.ModuleState;

using System.Runtime.Versioning;
using TH09.ProcView;

namespace TH09.Shell.Data;

internal readonly record struct GameProbe(int Pid, AdonisState Adonis)
{
    public bool Running => Pid > 0;

    public bool Missing => Pid == 0;
}

[SupportedOSPlatform("windows")]
internal sealed class GameProcess
{
    public const string ExeName = "th09.exe";

    public const string AdonisModule = "adonis2.dll";

    public const int IntervalMs = 1000;

    public const int NotProbed = ProcessLookup.NotProbed;

    private long _at;
    private bool _probed;
    private GameProbe _last;

    public GameProbe Poll()
    {
        var now = Environment.TickCount64;
        if (_probed && now - _at < IntervalMs) return _last;
        _at = now;
        _probed = true;
        _last = ProbeOnce();
        return _last;
    }

    public static GameProbe ProbeOnce()
    {
        try
        {
            var pid = ProcessLookup.FindPid(ExeName);
            if (pid <= 0) return new GameProbe(pid, AdonisState.Unknown);
            return new GameProbe(pid, ProcessLookup.FindModule(pid, AdonisModule));
        }
        catch (Exception)
        {
            return new GameProbe(NotProbed, AdonisState.Unknown);
        }
    }
}
