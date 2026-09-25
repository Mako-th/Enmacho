namespace TH09.Launch;

public sealed record LaunchOptions
{
    public static LaunchOptions Default { get; } = new();

    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public ScanSettings? Scan { get; init; }

    public Layer1Settings? Layer1 { get; init; }

    internal void Validate(LaunchKind kind)
    {
        if (StopTimeout < TimeSpan.Zero || StopTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(StopTimeout));

        if (kind == LaunchKind.Scan)
        {
            if (Scan is null) throw new ArgumentException("走査の設定がありません。");
            Scan.Validate();
        }
        else if (Scan is not null)
        {
            throw new ArgumentException("走査の設定は走査にだけ渡せます: " + kind);
        }

        if (kind == LaunchKind.BuildLayer1)
        {
            if (Layer1 is null) throw new ArgumentException("Layer 1 の設定がありません。");
            Layer1.Validate();
        }
        else if (Layer1 is not null)
        {
            throw new ArgumentException("Layer 1 の設定は Layer 1 の作り直しにだけ渡せます: " + kind);
        }
    }
}
