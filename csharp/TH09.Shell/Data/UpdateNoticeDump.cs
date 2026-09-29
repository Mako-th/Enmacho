using System.Text;

namespace TH09.Shell.Data;

internal static class UpdateNoticeDump
{
    public const string Flag = "--dump-update-notice";

    public const string CleanFlag = "--dump-update-clean";

    public static int RunClean(string root)
    {
        string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(full, Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
                          StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full))
        {
            Console.Error.WriteLine("本物の一時フォルダ・無いフォルダは断ります: " + full);
            return 3;
        }
        int n = UpdateLauncher.Clean(full, TimeSpan.Zero);
        Console.WriteLine("removed\t" + n);
        foreach (var e in Directory.EnumerateFileSystemEntries(full).OrderBy(x => x, StringComparer.Ordinal))
            Console.WriteLine("left\t" + Path.GetFileName(e));
        return 0;
    }

    public static int Run(string releaseJsonPath, string currentVersion)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        string json;
        try
        {
            json = File.ReadAllText(releaseJsonPath);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine("JSON を読めません: " + releaseJsonPath + "（" + ex.Message + "）");
            return 2;
        }

        var offer = UpdateNotice.Parse(json, currentVersion);
        bool notify = offer is not null;
        stdout.WriteLine("notify\t" + (notify ? "1" : "0"));
        stdout.WriteLine("message\t" + (notify ? UpdateNotice.Message : ""));
        stdout.WriteLine("zip_url\t" + (offer?.ZipUrl ?? ""));
        stdout.WriteLine("sha256\t" + (offer?.Sha256 ?? ""));
        stdout.WriteLine("launch_args\t" + (offer is null || offer.ZipUrl is null ? ""
            : string.Join(" ", UpdateLauncher.BuildArgs(offer, "DEST", "SHELL.exe"))));
        stdout.WriteLine("prompt\t" + (notify ? UpdateNotice.PromptMessage.Replace("\n", "|") : ""));
        stdout.Flush();
        return 0;
    }
}
