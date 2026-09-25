namespace TH09.Drive;

public class MenuDriverError : Exception
{
    public MenuDriverError(string message) : base(message) { }
}

public sealed class UnexpectedScreen : MenuDriverError
{
    public UnexpectedScreen(string message) : base(message) { }
}

public sealed class MenuTimeout : MenuDriverError
{
    public MenuTimeout(string message) : base(message) { }
}

public sealed class MenuLost : MenuDriverError
{
    public MenuLost(string message) : base(message) { }
}

public sealed class EmptyCell : MenuDriverError
{
    public EmptyCell(string message) : base(message) { }
}

public sealed class SkipHoldFailed : MenuDriverError
{
    public SkipHoldFailed(string message) : base(message) { }
}

public sealed class PlaybackAbortFailed : MenuDriverError
{
    public PlaybackAbortFailed(string message) : base(message) { }
}
