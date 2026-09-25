using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;

namespace TH09.Shell.Data;

internal static class PlaybackTrace
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet =
        System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string name);

    public const string RelativePath = "perf/playback.tsv";

    public const int Capacity = 8000;

    private record struct Beat(double AtMs, byte Driver, short Steps, int Tick, double Speed,
                               double UiMs, int Gc0, int Gc1, int Gc2);

    private static readonly Beat[] _ring = new Beat[Capacity];
    private static int _n;
    private static int _last = -1;
    private static readonly Stopwatch _clock = Stopwatch.StartNew();

    public const byte DriverRaf = 0, DriverTimer = 1, DriverPost = 2;

    private static string DriverName(byte d) => d switch
    {
        DriverRaf => "raf",
        DriverPost => "post",
        _ => "timer",
    };

    public static void Beat1(byte driver, int steps, int tick, double speed)
    {
        int at = _n % Capacity;
        _ring[at] = new Beat(_clock.Elapsed.TotalMilliseconds, driver,
                             (short)Math.Clamp(steps, short.MinValue, short.MaxValue), tick, speed,
                             double.NaN,
                             GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        _last = at;
        _n++;
    }

    public static void NoteUi(double ms)
    {
        if (_last >= 0) _ring[_last].UiMs = ms;
    }

    private static readonly double[] _bg = new double[6000];
    private static int _bgN;
    private static System.Threading.Timer? _bgTimer;

    public const int BackgroundMs = 4;

    public static string ClockKind { get; set; } = "まだ再生していない";

    private static uint Milliseconds1 => TimerResolution.Milliseconds;

    private static void StartBackground()
    {
        _bgN = 0;
        _bgTimer?.Dispose();
        _bgTimer = new System.Threading.Timer(_ =>
        {
            int at = _bgN;
            if (at >= _bg.Length) return;
            _bg[at] = _clock.Elapsed.TotalMilliseconds;
            _bgN = at + 1;
        }, null, 0, BackgroundMs);
    }

    private static void StopBackground()
    {
        _bgTimer?.Dispose();
        _bgTimer = null;
    }

    public static void Reset()
    {
        StartBackground();
        _n = 0;
        _last = -1;
        _clock.Restart();
    }

    public static string Note { get; set; } = "";

    private static string GraphicsName()
    {
        try
        {
            string[] want =
            [
                "av_libGLESv2.dll", "av_libEGL.dll", "libGLESv2.dll", "libEGL.dll",
                "opengl32.dll", "d3d11.dll", "d3d9.dll", "vulkan-1.dll", "libSkiaSharp.dll",
            ];
            var hit = new List<string>();
            foreach (var w in want)
                if (GetModuleHandleW(w) != IntPtr.Zero) hit.Add(w);
            return hit.Count == 0
                   ? "★GPU の DLL が 1 つも入っていない"
                   : string.Join(", ", hit);
        }
        catch { return "分からない（引けなかった）"; }
    }

    public static string Suffix { get; set; } =
        Environment.GetEnvironmentVariable("TH09_TRACE_SUFFIX")?.Trim() is { Length: > 0 } v
        && v.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 ? v : "";

    public static int Dump()
    {
        if (_n == 0) return 0;
        try
        {
            var root = TH09.Record.Paths.Default.DataRoot;
            if (string.IsNullOrEmpty(root)) return 0;
            var name = Suffix.Length == 0 ? RelativePath
                                          : RelativePath.Replace(".tsv", "_" + Suffix + ".tsv");
            var path = Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            int count = Math.Min(_n, Capacity);
            int from = _n <= Capacity ? 0 : _n % Capacity;

            var sb = new StringBuilder();
            sb.AppendLine("# 被弾窓の再生の進め方（★いちばん新しい 1 回ぶん。上書き）");
            if (Note.Length > 0) sb.Append("# ").AppendLine(Note);
            sb.Append("# 描く経路: ").Append(GraphicsName())
              .Append("  / 頼んだ経路: ").AppendLine(Program.RenderModeAsked);
            sb.Append("# 細かいタイマ: 頼む=").Append(TimerResolution.Wanted)
              .Append(" / OS の返事=").Append(TimerResolution.LastResult == 0 ? "受け入れた" : "断られた(" + TimerResolution.LastResult + ")")
              .Append(" / 頼んだ間隔=").Append(Milliseconds1).Append(" ms")
              .Append(" / 電力スロットリングの除外=").Append(TimerResolution.ThrottleNote)
              .Append(" / 起こす側=").AppendLine(ClockKind);
            sb.AppendLine("# driver=post なら専用スレッドから起こした（既定） / timer なら WM_TIMER / raf なら描く回");
            sb.AppendLine("# gap_ms は前の回からの実時間。★16.7 が並べば毎フレーム、"
                          + "33 が並べば 1 フレームおき、まだらなら取りこぼし");
            sb.AppendLine("i\tat_ms\tgap_ms\tui_ms\tdriver\tsteps\ttick\tspeed"
                          + "\tgc0\tgc1\tgc2");
            double prev = double.NaN;
            for (var k = 0; k < count; k++)
            {
                var b = _ring[(from + k) % Capacity];
                double gap = double.IsNaN(prev) ? 0 : b.AtMs - prev;
                prev = b.AtMs;
                sb.Append(k.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(b.AtMs.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(gap.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(double.IsNaN(b.UiMs) ? ""
                          : b.UiMs.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(DriverName(b.Driver)).Append('\t')
                  .Append(b.Steps.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(b.Tick.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(b.Speed.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(b.Gc0.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(b.Gc1.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(b.Gc2.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            DumpBackground(path);
            return count;
        }
        catch { return 0; }
    }

    private static void DumpBackground(string mainPath)
    {
        StopBackground();
        try
        {
            int n = Math.Min(_bgN, _bg.Length);
            if (n < 2) return;
            var sb = new StringBuilder();
            sb.AppendLine("# ★UI とは別のスレッドで鼓動させた時計（" + BackgroundMs + " ms 間隔を頼んだ）");
            sb.AppendLine("# ★こちらも飛んでいればプロセス/機械全体 / 規則正しければ UI スレッドだけが待ち");
            sb.AppendLine("k	at_ms	gap_ms");
            for (var k = 1; k < n; k++)
                sb.Append(k.ToString(CultureInfo.InvariantCulture)).Append('	')
                  .Append(_bg[k].ToString("F2", CultureInfo.InvariantCulture)).Append('	')
                  .Append((_bg[k] - _bg[k - 1]).ToString("F2", CultureInfo.InvariantCulture))
                  .AppendLine();
            File.WriteAllText(mainPath.Replace(".tsv", "_bg.tsv"), sb.ToString(),
                              new UTF8Encoding(false));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
