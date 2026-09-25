namespace TH09.Launch;

public sealed class LaunchOutputEventArgs(string line) : EventArgs
{
    public string Line { get; } = line;
}
