namespace TH09.Shell.Data;

internal static class DumpFlags
{
    public const string NarrowFlag = "--narrow";

    public const string BlocksFlag = "--blocks";

    public const string HideFlag = "--hide";

    public static bool TryRead(IReadOnlyList<string> args, out bool narrow, out bool blocks,
                               out IReadOnlyList<string> hidden)
    {
        narrow = false;
        blocks = false;
        hidden = [];
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case NarrowFlag:
                    narrow = true;
                    break;
                case BlocksFlag:
                    blocks = true;
                    break;
                case HideFlag:
                    if (i + 1 >= args.Count) return false;
                    hidden = SplitKinds(args[++i]);
                    break;
                default:
                    return false;
            }
        }
        if (narrow) blocks = true;
        return true;
    }

    public static IReadOnlyList<string> SplitKinds(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries
                                  | StringSplitOptions.TrimEntries)];
}
