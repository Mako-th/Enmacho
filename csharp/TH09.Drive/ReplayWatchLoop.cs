using System.Runtime.Versioning;
using TH09.Record;
using TH09.Replay;

namespace TH09.Drive;

public sealed record ReplayWatchCycle(bool Scanning, int Seen, int Registered, int Held,
                                      int SkippedSlots, int Missing, int Swept);

[SupportedOSPlatform("windows")]
public sealed class ReplayWatchLoop
{
    public const string RegisteredPrefix = "登録: ";

    public const string HeldPrefix = "保留: ";

    public const string ReplaySuffix = ".rpy";

    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly ReplayRegistrar _registrar;
    private readonly IReadOnlyList<string> _roots;
    private readonly IReadOnlyList<string> _ownNames;
    private readonly bool _ignoreCase;
    private readonly bool _partial;
    private readonly Func<bool> _scanIsActive;
    private readonly Action<string> _log;

    private Dictionary<string, FileSig> _known = new(StringComparer.Ordinal);

    public ReplayWatchLoop(ReplayRegistrar registrar, IReadOnlyList<string> roots,
                           IReadOnlyList<string>? ownNames, bool ignoreCase, bool partial,
                           Func<bool>? scanIsActive = null, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(roots);
        _registrar = registrar;
        _roots = roots;
        _ownNames = ownNames ?? [];
        _ignoreCase = ignoreCase;
        _partial = partial;
        _scanIsActive = scanIsActive ?? ScanLedger.ScanIsActive;
        _log = log ?? (line => Console.WriteLine(line));
    }

    public int KnownCount => _known.Count;


    public int Baseline()
    {
        var state = new Dictionary<string, FileSig>(StringComparer.Ordinal);
        var count = 0;
        foreach (var root in _roots)
        {
            if (!DirectoryExists(root)) continue;
            foreach (var path in Rglob(root))
            {
                var sig = Signature(path);
                if (sig is null) continue;
                state[path] = sig.Value;
                count++;
            }
        }
        _known = state;
        return count;
    }

    public int ImportExisting()
    {
        var scanning = _scanIsActive();
        var n = 0;
        foreach (var root in _roots)
        {
            if (!DirectoryExists(root)) continue;
            foreach (var path in Rglob(root))
            {
                if (scanning && ReplaySlots.IsSlotFile(path, _roots)) continue;
                if (TryRegister(path)) n++;
            }
        }
        return n;
    }

    public int SweepMoved()
    {
        var existingRoots = _roots.Where(DirectoryExists).ToList();
        if (existingRoots.Count == 0) return 0;
        var scanning = _scanIsActive();
        var stale = new List<string>();
        foreach (var path in _registrar.CurrentPaths())
        {
            if (!UnderAny(path, existingRoots)) continue;
            if (scanning && ReplaySlots.IsSlotFile(path, _roots)) continue;
            if (StillExists(path)) continue;
            stale.Add(path);
        }
        return stale.Count > 0 ? _registrar.SweepMissing(stale) : 0;
    }

    private static bool StillExists(string path)
    {
        try { return File.Exists(path) || Directory.Exists(path); }
        catch (Exception) { return false; }
    }

    private static bool UnderAny(string path, IReadOnlyList<string> dirs)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception) { return false; }
        foreach (var dir in dirs)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }


    public ReplayWatchCycle RunCycle()
    {
        var current = new Dictionary<string, FileSig>(StringComparer.Ordinal);
        var scanning = _scanIsActive();
        int seen = 0, registered = 0, held = 0, skipped = 0;
        foreach (var root in _roots)
        {
            if (!DirectoryExists(root)) continue;
            foreach (var path in Rglob(root))
            {
                var sig = Signature(path);
                if (sig is null) continue;
                current[path] = sig.Value;
                seen++;
                if (scanning && ReplaySlots.IsSlotFile(path, _roots))
                {
                    skipped++;
                    continue;
                }
                if (_known.TryGetValue(path, out var old) && old == sig.Value) continue;
                if (TryRegister(path)) registered++;
                else held++;
            }
        }
        var missing = new List<string>();
        foreach (var path in _known.Keys)
        {
            if (!current.ContainsKey(path)) missing.Add(path);
        }
        var swept = missing.Count > 0 ? _registrar.SweepMissing(missing) : 0;
        _known = current;
        return new ReplayWatchCycle(scanning, seen, registered, held, skipped,
                                    missing.Count, swept);
    }

    public int Run(Func<bool> waitForNext, Action<ReplayWatchCycle>? afterCycle = null)
    {
        ArgumentNullException.ThrowIfNull(waitForNext);
        var cycles = 0;
        while (true)
        {
            var done = RunCycle();
            cycles++;
            afterCycle?.Invoke(done);
            if (!waitForNext()) break;
        }
        return cycles;
    }

    public int Run(TimeSpan interval, CancellationToken token) =>
        Run(() => !token.WaitHandle.WaitOne(interval));


    private bool TryRegister(string path)
    {
        try
        {
            var status = Register(path);
            _log(RegisteredPrefix + path + " [" + status + "]");
            return true;
        }
        catch (Exception exc)
        {
            _log(HeldPrefix + path + " " + exc.GetType().Name + ": " + exc.Message);
            return false;
        }
    }

    private string Register(string path)
    {
        var decoded = ReplayDecode.DecodeReplay(path);
        var source = ReplayOwner.ReplaySource(path, decoded);
        var side = decoded.Decoded
            ? ReplayOwner.OwnerSide(decoded, path, _ownNames, _ignoreCase, _partial)
            : null;
        var facts = new ReplayFacts(
            Status: decoded.Status,
            Decoded: decoded.Decoded,
            Source: source,
            OwnerSideAuto: side,
            Mode: decoded.Mode,
            Difficulty: decoded.Difficulty,
            PlayerName: decoded.Name,
            ReplayDate: decoded.Date,
            P1Char: decoded.P1Char,
            P2Char: decoded.P2Char,
            P1Name: decoded.P1Name,
            P2Name: decoded.P2Name,
            DecodedJson: ReplayFactsJson.DecodedJson(decoded));
        _registrar.Register(path, facts);
        return decoded.Status;
    }


    private readonly record struct FileSig(long Size, long Stamp);

    private static FileSig? Signature(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.Directory) != 0)
            {
                return new FileSig(0, new DirectoryInfo(path).LastWriteTimeUtc.Ticks);
            }
            return new FileSig(info.Length, info.LastWriteTimeUtc.Ticks);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IEnumerable<string> Rglob(string root)
    {
        foreach (var path in MatchesIn(root)) yield return path;
        var stack = new List<string> { root };
        while (stack.Count > 0)
        {
            var dir = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            foreach (var child in ChildDirectories(dir))
            {
                foreach (var path in MatchesIn(child)) yield return path;
                stack.Add(child);
            }
        }
    }

    private static IReadOnlyList<string> MatchesIn(string dir)
    {
        var hits = new List<string>();
        foreach (var entry in Entries(dir))
        {
            if (entry.Name.EndsWith(ReplaySuffix, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(entry.FullName);
            }
        }
        return hits;
    }

    private static IReadOnlyList<string> ChildDirectories(string dir)
    {
        var hits = new List<string>();
        foreach (var entry in Entries(dir))
        {
            var attrs = entry.Attributes;
            if ((attrs & FileAttributes.Directory) != 0 && (attrs & FileAttributes.ReparsePoint) == 0)
            {
                hits.Add(entry.FullName);
            }
        }
        return hits;
    }

    private static IReadOnlyList<FileSystemInfo> Entries(string dir)
    {
        try
        {
            return [.. new DirectoryInfo(dir).EnumerateFileSystemInfos()];
        }
        catch (Exception)
        {
            return [];
        }
    }
}
