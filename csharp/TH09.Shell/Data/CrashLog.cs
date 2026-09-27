using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class CrashLog
{
    public const string FileName = "crash.log";

    public const long MaxBytes = 1024 * 1024;

    private static string? _path;

    public static string? Path_
    {
        get
        {
            if (_path is not null) return _path;
            try
            {
                var root = TH09.Record.Paths.Default.DataRoot;
                if (string.IsNullOrEmpty(root)) return null;
                _path = System.IO.Path.Combine(root, FileName);
                return _path;
            }
            catch { return null; }
        }
    }

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Write("Task", e.Exception);
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            Write("UIThread", e.Exception);
    }

    public static void Write(string where, Exception? ex)
    {
        try
        {
            var path = Path_;
            if (path is null) return;
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes) File.Delete(path);
            }
            catch { }

            var sb = new StringBuilder();
            sb.Append("==== ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",
                                                            CultureInfo.InvariantCulture))
              .Append("  [").Append(where).Append("] ====").AppendLine();
            if (!string.IsNullOrEmpty(Doing)) sb.Append("いま何をしていたか: ").AppendLine(Doing);
            sb.AppendLine(ex?.ToString() ?? "（例外そのものが取れなかった）");
            sb.AppendLine();
            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
        catch { }
    }

    public static string Doing { get; set; } = "";
}
