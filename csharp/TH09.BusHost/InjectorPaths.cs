namespace TH09.BusHost;

public static class InjectorPaths
{
    public const string InjectorExeName = "th09_inject.exe";

    public const string HookDllName = "th09_tickhook.dll";

    private static readonly string[] InjectorDirParts = ["th09_inject", "build"];

    private static readonly string[][] HookDllRelativeParts =
    [
        ["..", "..", "th09_tickhook", "build", HookDllName],
        [HookDllName],
    ];

    public static string InjectorDir(string nativeDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeDir);
        return Path.GetFullPath(Path.Combine(nativeDir, Path.Combine(InjectorDirParts)));
    }

    public static string InjectorExe(string nativeDir)
        => Path.Combine(InjectorDir(nativeDir), InjectorExeName);

    public static string? ExistingInjectorExe(string nativeDir)
    {
        var path = InjectorExe(nativeDir);
        return File.Exists(path) ? path : null;
    }

    public static IReadOnlyList<string> HookDllCandidates(string nativeDir)
    {
        var dir = InjectorDir(nativeDir);
        var list = new List<string>(HookDllRelativeParts.Length);
        foreach (var parts in HookDllRelativeParts)
            list.Add(Path.GetFullPath(Path.Combine(dir, Path.Combine(parts))));
        return list;
    }

    public static string HookDll(string nativeDir)
    {
        var candidates = HookDllCandidates(nativeDir);
        foreach (var path in candidates)
            if (File.Exists(path)) return path;
        return candidates[0];
    }
}
