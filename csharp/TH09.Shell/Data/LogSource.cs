using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal enum LogLevel
{
    Info = 0,

    Warning = 1,

    Error = 2,
}

internal sealed record LogEntry(long Seq, DateTime At, LogLevel Level, string Category, string Message)
{
    public string TimeText => At.ToString("yy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

    public string LevelText => Level switch
    {
        LogLevel.Error => "エラー",
        LogLevel.Warning => "警告",
        _ => "情報",
    };

    public bool IsError => Level == LogLevel.Error;

    public bool IsWarning => Level == LogLevel.Warning;
}

internal sealed record LogFilter(LogLevel Minimum = LogLevel.Info, string? Text = null);

internal static class LogSource
{
    public const int Capacity = 2000;

    private static readonly object Gate = new();
    private static readonly Queue<LogEntry> Entries = new();
    private static long _seq;
    private static int _dropped;
    private static bool _started;

    internal static bool SuppressFileWrites { get; set; }

    public static event Action<LogEntry>? Added;

    public static int Dropped { get { lock (Gate) return _dropped; } }

    public static int Count { get { lock (Gate) return Entries.Count; } }

    public static LogEntry Add(LogLevel level, string category, string message)
        => Add(level, category, message, DateTime.Now);

    public static LogEntry Add(LogLevel level, string category, string message, DateTime at)
    {
        LogEntry entry;
        lock (Gate)
        {
            entry = new LogEntry(++_seq, at, level, category, message);
            Entries.Enqueue(entry);
            while (Entries.Count > Capacity)
            {
                Entries.Dequeue();
                _dropped++;
            }
        }
        Added?.Invoke(entry);
        AppendToDefaultFile(entry);
        return entry;
    }

    public static LogEntry Info(string category, string message) => Add(LogLevel.Info, category, message);

    public static LogEntry Warn(string category, string message) => Add(LogLevel.Warning, category, message);

    public static LogEntry Error(string category, string message) => Add(LogLevel.Error, category, message);

    public static LogEntry[] Snapshot() { lock (Gate) return [.. Entries]; }

    public static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
            _dropped = 0;
        }
    }

    public static List<LogEntry> Filter(IEnumerable<LogEntry> entries, LogFilter filter)
    {
        var text = string.IsNullOrWhiteSpace(filter.Text) ? null : filter.Text;
        var outRows = new List<LogEntry>();
        foreach (var e in entries)
        {
            if (e.Level < filter.Minimum) continue;
            if (text is not null
                && !e.Message.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !e.Category.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            outRows.Add(e);
        }
        return outRows;
    }

    public static void EnsureStarted()
    {
        lock (Gate)
        {
            if (_started) return;
            _started = true;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Error("例外", "未処理: " + Describe(e.ExceptionObject as Exception));
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Error("例外", "拾われなかった Task: " + Describe(e.Exception));

        if (!OperatingSystem.IsWindows())
        {
            Warn("起動", "置き場所は Windows でしか決まらない（TH09.Record.Paths）");
            return;
        }

        try
        {
            var paths = TH09.Record.Paths.Default;
            Info("起動", "置き場所 " + paths.Mode + " / exe " + paths.ExeDir);
            Info("起動", "設定 " + paths.ConfigPath + (paths.ConfigLoaded ? "（読めた）" : "（読めていない）"));
            Info("起動", "本体 DB " + paths.MainDb + Exists(paths.MainDb));
            Info("起動", "Layer 0 " + paths.Layer0Db + Exists(paths.Layer0Db));
        }
        catch (Exception ex)
        {
            Error("起動", "置き場所を決められませんでした: " + Describe(ex));
        }
    }

    private static string Exists(string path) => File.Exists(path) ? "（有り）" : "（無し）";

    public static string Describe(Exception? ex)
        => ex is null ? "（例外が無い）" : ex.GetType().Name + ": " + ex.Message;


    public const char Separator = '\t';

    public static readonly string[] Columns = ["seq", "time", "level", "category", "message"];

    public static string Row(LogEntry e)
        => string.Join(Separator,
                       e.Seq.ToString(CultureInfo.InvariantCulture),
                       e.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                       e.LevelText,
                       Escape(e.Category),
                       Escape(e.Message));

    public static string Escape(string s)
        => s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");


    public const string FilePrefix = "th09_log_";

    public const string FileStampFormat = "yyyyMMdd";

    public const string FileExtension = ".tsv";

    public const int FileRetentionCount = 10;

    private static readonly object FileGate = new();
    private static string? _fileDay;

    public static bool? LastFileWriteOk { get; private set; }

    public static string FileNameFor(DateTime at)
        => FilePrefix + at.ToString(FileStampFormat, CultureInfo.InvariantCulture) + FileExtension;

    public static bool IsOwnLogFile(string directory, string path)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path)) return false;
        var full = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(full);
        var wantParent = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        if (parent is null
            || !string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar), wantParent,
                              StringComparison.OrdinalIgnoreCase)) return false;
        var name = Path.GetFileName(full);
        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal)
            || !name.EndsWith(FileExtension, StringComparison.Ordinal)) return false;
        var stamp = name[FilePrefix.Length..^FileExtension.Length];
        return DateTime.TryParseExact(stamp, FileStampFormat, CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out _);
    }

    public static (int Kept, int Removed) PruneOldFiles(string directory)
    {
        List<string> own;
        try
        {
            own = new List<string>(Directory.EnumerateFiles(directory));
            own.RemoveAll(f => !IsOwnLogFile(directory, f));
            own.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)));
        }
        catch { return (0, 0); }

        var removed = 0;
        for (var i = FileRetentionCount; i < own.Count; i++)
        {
            try { File.Delete(own[i]); removed++; }
            catch { }
        }
        return (own.Count - removed, removed);
    }

    public static bool WriteEntryToFile(string directory, LogEntry entry)
    {
        try
        {
            lock (FileGate)
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, FileNameFor(entry.At));
                var isNew = !File.Exists(path);
                using var writer = new StreamWriter(path, append: true, new UTF8Encoding(false));
                if (isNew) writer.Write(string.Join('\t', Columns) + "\n");
                writer.Write(Row(entry) + "\n");
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AppendToDefaultFile(LogEntry entry)
    {
        if (SuppressFileWrites) return;
        string? dir;
        try
        {
            dir = OperatingSystem.IsWindows() ? TH09.Record.Paths.Default.LogsDir : null;
        }
        catch { dir = null; }
        if (string.IsNullOrEmpty(dir)) { LastFileWriteOk = false; return; }

        var ok = WriteEntryToFile(dir, entry);
        LastFileWriteOk = ok;
        if (!ok) return;

        var day = entry.At.ToString(FileStampFormat, CultureInfo.InvariantCulture);
        bool isNewDay;
        lock (FileGate) { isNewDay = day != _fileDay; if (isNewDay) _fileDay = day; }
        if (isNewDay) PruneOldFiles(dir);
    }
}
