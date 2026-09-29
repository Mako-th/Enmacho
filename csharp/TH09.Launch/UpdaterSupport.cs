namespace TH09.Launch;

internal static partial class UpdaterExecutable
{
    internal static string FileName => GeneratedFileName;

    internal static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, FileName);
}

internal static partial class ShellExecutable
{
    internal static string FileName => GeneratedFileName;
}

public sealed record UpdaterRequest(string DestinationDirectory, string ZipUrl, string? Sha256, string ShellExePath);

public static class UpdaterRuns
{
    public const string RunDirPrefix = "EnmaCho_update_run_";

    public static int Clean(string tempRoot, TimeSpan minAge, Action<string> log)
    {
        int removed = 0;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(tempRoot, RunDirPrefix + "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(dir) < minAge) continue;
                    Directory.Delete(dir, recursive: true);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log("入れ替え役の古い写しを消せませんでした: " + dir + "（" + ex.Message + "）");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log("入れ替え役の古い写しを探せませんでした: " + ex.Message);
        }
        return removed;
    }
}
