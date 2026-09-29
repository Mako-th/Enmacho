using System.Globalization;

namespace TH09.Update;

internal sealed class Options
{
    public int LockWaitSeconds { get; private set; } = 30;

    public string Dest { get; private set; } = "";

    public string? Url { get; private set; }

    public string? ZipFile { get; private set; }

    public string? Sha256 { get; private set; }

    public string ShellExe { get; private set; } = "";

    public string? Relaunch { get; private set; }

    public bool NoDialog { get; private set; }

    public bool Help { get; private set; }

    public static string? TryParse(string[] args, out Options o)
    {
        o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (a)
            {
                case "--help" or "-h" or "/?":
                    o.Help = true;
                    break;
                case "--no-dialog":
                    o.NoDialog = true;
                    break;
                case "--lock-wait-sec":
                    if (!TryInt(Next(), 0, out var lw)) return "--lock-wait-sec には 0 以上の整数が要ります";
                    o.LockWaitSeconds = lw;
                    break;
                case "--dest":
                    o.Dest = Next() ?? "";
                    if (o.Dest.Length == 0) return "--dest にフォルダが要ります";
                    break;
                case "--url":
                    o.Url = Next();
                    if (string.IsNullOrEmpty(o.Url)) return "--url に URL が要ります";
                    break;
                case "--zip-file":
                    o.ZipFile = Next();
                    if (string.IsNullOrEmpty(o.ZipFile)) return "--zip-file にファイルが要ります";
                    break;
                case "--sha256":
                    var raw = Next() ?? "";
                    if (raw.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) raw = raw["sha256:".Length..];
                    raw = raw.Trim().ToLowerInvariant();
                    if (raw.Length != 64 || !raw.All(Uri.IsHexDigit)) return "--sha256 には 16 進 64 字が要ります";
                    o.Sha256 = raw;
                    break;
                case "--shell-exe":
                    o.ShellExe = Next() ?? "";
                    if (o.ShellExe.Length == 0) return "--shell-exe にファイル名が要ります";
                    break;
                case "--relaunch":
                    o.Relaunch = Next();
                    if (string.IsNullOrEmpty(o.Relaunch)) return "--relaunch に exe のパスが要ります";
                    break;
                default:
                    return "知らない引数です: " + a;
            }
        }

        if (o.Help) return null;
        if (o.Dest.Length == 0) return "--dest が要ります";
        if ((o.Url is null) == (o.ZipFile is null)) return "--url と --zip-file は、どちらか 1 つだけ指定してください";
        if (o.ShellExe.Length == 0 && o.Relaunch is not null) o.ShellExe = Path.GetFileName(o.Relaunch);
        if (o.ShellExe.Length == 0) return "--shell-exe（または --relaunch）が要ります";
        return null;
    }

    private static bool TryInt(string? s, int min, out int v)
        => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out v) && v >= min;
}
