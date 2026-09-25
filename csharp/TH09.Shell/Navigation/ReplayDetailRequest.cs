namespace TH09.Shell.Navigation;

internal readonly record struct ReplayDetailRequest
{
    private ReplayDetailRequest(long? sessionId, long? replayId)
    {
        SessionId = sessionId;
        ReplayId = replayId;
    }

    public long? SessionId { get; }

    public long? ReplayId { get; }

    public static ReplayDetailRequest FromSession(long sessionId, long? replayId = null)
        => new(sessionId, replayId);

    public static ReplayDetailRequest FromReplay(long replayId, long? sessionId = null)
        => new(sessionId, replayId);

    public override string ToString()
        => (SessionId is long s ? "session " + s : "session なし")
           + " / " + (ReplayId is long r ? "replay " + r : "replay なし");
}
