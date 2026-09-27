namespace TH09.Shell.Data;

internal sealed class AutoMonitorRestart
{
    public const int MaxAttempts = 3;

    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan StableAfter = TimeSpan.FromSeconds(60);

    private int attempts;
    private DateTime? startedAt;

    public int Attempts => attempts;

    public void Started(DateTime now) => startedAt = now;

    public void Reset()
    {
        attempts = 0;
        startedAt = null;
    }

    public TimeSpan? OnUnintendedExit(DateTime now)
    {
        if (startedAt is DateTime started && now - started >= StableAfter) attempts = 0;
        startedAt = null;
        if (attempts >= MaxAttempts) return null;
        attempts++;
        return Delay;
    }
}
