namespace TH09.Launch;

internal static partial class DriveExecutable
{
    internal static string FileName => GeneratedFileName;

    internal static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, FileName);
}
