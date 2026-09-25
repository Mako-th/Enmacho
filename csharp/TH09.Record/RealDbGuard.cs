namespace TH09.Record;

public static class RealDbGuard
{
    public static bool InMainDbDir(string path) => SameDir(path, DirOf(Paths.Default.MainDb));

    public static bool InLayer0Dir(string path) => SameDir(path, DirOf(Paths.Default.Layer0Db));

    public static bool InConfigDir(string path) => SameDir(path, DirOf(Paths.Default.ConfigPath));

    private static string DirOf(string path) => Path.GetDirectoryName(Full(path)) ?? "";

    private static bool SameDir(string path, string dir) =>
        dir.Length > 0
        && string.Equals(Path.GetDirectoryName(Full(path)) ?? "", dir,
                         StringComparison.OrdinalIgnoreCase);

    private static string Full(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
