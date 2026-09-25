namespace TH09.Shell.Data;

internal enum PlayRole
{
    Body,

    Label,

    Value,

    Separator,

    Title,

    Gain,

    Loss,
}

internal readonly record struct PlaySpan(string Text, PlayRole Role)
{
    public static PlaySpan Body(string text) => new(text, PlayRole.Body);
    public static PlaySpan Label(string text) => new(text, PlayRole.Label);
    public static PlaySpan Value(string text) => new(text, PlayRole.Value);
    public static PlaySpan Separator(string text) => new(text, PlayRole.Separator);
    public static PlaySpan Title(string text) => new(text, PlayRole.Title);

    public static PlaySpan Signed(string text) => new(text,
        text.StartsWith('+') ? PlayRole.Gain
        : text.StartsWith('-') ? PlayRole.Loss
        : PlayRole.Value);
}

internal sealed class PlayTextLine
{
    public PlayTextLine(params PlaySpan[] spans) : this((IReadOnlyList<PlaySpan>)spans) { }

    public PlayTextLine(IReadOnlyList<PlaySpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        Spans = spans;
        Text = string.Concat(spans.Select(s => s.Text));
    }

    public IReadOnlyList<PlaySpan> Spans { get; }

    public string Text { get; }

    public PlayTextLine Indent(string pad)
        => pad.Length == 0 ? this : new PlayTextLine([PlaySpan.Body(pad), .. Spans]);
}
