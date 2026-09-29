using System.Runtime.InteropServices;
using System.Text;

namespace TH09.Update;

internal static class Program
{
    private const string Title = "幻想閻魔帳の更新";

    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; }
        catch (IOException) { }

        if (args.Length == 2 && args[0] == "--check-url")
        {
            bool accepted = Installer.IsAcceptedUrl(args[1]);
            Console.WriteLine(accepted ? "accepted" : "refused");
            return accepted ? 0 : 3;
        }

        var err = Options.TryParse(args, out var o);
        if (err is not null)
        {
            Console.Error.WriteLine(err);
            Console.Error.WriteLine("使い方: --dest <フォルダ> (--url <https の URL> | --zip-file <zip>) [--sha256 <16進>] "
                                    + "(--relaunch <exe> | --shell-exe <ファイル名>)");
            return 2;
        }
        if (o.Help)
        {
            Console.WriteLine("幻想閻魔帳の更新（本体が自分を閉じたあと、新しい版へ入れ替えて開き直す）");
            Console.WriteLine("引数: --dest <フォルダ> (--url <https の URL> | --zip-file <zip>) [--sha256 <16進>] "
                              + "(--relaunch <exe> | --shell-exe <ファイル名>) "
                              + "[--lock-wait-sec <秒>] [--no-dialog]");
            return 0;
        }

        string dest = Path.GetFullPath(o.Dest).TrimEnd(Path.DirectorySeparatorChar);
        var ui = new Ui(o.NoDialog);

        var refuse = Installer.RefuseDestination(dest);
        if (refuse is not null) return Fail(o, ui, null, 3, refuse, relaunch: false, Console.WriteLine);
        if (!File.Exists(Path.Combine(dest, o.ShellExe)))
            return Fail(o, ui, null, 3, "入れ替え先に本体（" + o.ShellExe + "）がありません: " + dest, relaunch: false, Console.WriteLine);
        if (o.Url is not null && !Installer.IsAcceptedUrl(o.Url))
            return Fail(o, ui, dest, 3, "この場所からは落としません: " + o.Url, relaunch: true, Console.WriteLine);

        string logFile = Path.Combine(dest, Installer.LogFileName);
        void Log(string m)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + m;
            Console.WriteLine(line);
            try { File.AppendAllText(logFile, line + Environment.NewLine, Encoding.UTF8); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        string tmp = Path.Combine(Path.GetTempPath(), "EnmaCho_update_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            Log("更新を始めます: " + dest);

            string zip;
            if (o.ZipFile is not null)
            {
                zip = o.ZipFile;
                Log("ローカルの zip を使います: " + zip);
            }
            else
            {
                zip = Path.Combine(tmp, "download.zip");
                Log("落としています: " + o.Url);
                var dl = Downloader.Get(o.Url!, zip, Log);
                if (dl is not null) return Fail(o, ui, dest, 1, "落とせませんでした: " + dl, relaunch: true, Log);
            }

            if (o.Sha256 is not null)
            {
                string got = Installer.Sha256Of(zip);
                if (got != o.Sha256)
                    return Fail(o, ui, dest, 3, "zip の sha256 が合いません（期待 " + o.Sha256 + " ／ 実際 " + got + "）", relaunch: true, Log);
                Log("sha256 が合いました");
            }

            var staged = Installer.Extract(zip, Path.Combine(tmp, "stage"), o.ShellExe);
            if (staged.Error is not null) return Fail(o, ui, dest, 3, staged.Error, relaunch: true, Log);
            Log("展開して確かめました（" + staged.Files.Count + " ファイル）");

            while (true)
            {
                var locked = WaitUnlocked(dest, staged.Files, o.LockWaitSeconds);
                if (locked.Count == 0) break;
                string names = string.Join("、", locked.Take(5)) + (locked.Count > 5 ? " ほか" : "");
                Log("書き込めないファイルがあります: " + names);
                if (!ui.RetryCancel(Title, "花映塚を終了してから［再試行］を押してください。\n\n使用中のファイル: " + names))
                {
                    bool shellRunning = locked.Any(f => string.Equals(f, o.ShellExe, StringComparison.OrdinalIgnoreCase));
                    return Fail(o, ui, dest, 1, "使用中のファイルがあるので、更新を中止しました（何も書き換えていません）",
                                relaunch: !shellRunning, Log);
                }
            }

            var r = Installer.Apply(dest, staged, Log);
            if (!r.Ok)
            {
                string msg = r.RollbackFailed
                    ? "入れ替えに失敗し、元へ戻し切れませんでした。" + Installer.BackupDirName + " の中に、入れ替え前のファイルが残っています。（" + r.Error + "）"
                    : "入れ替えに失敗したので、元に戻しました。（" + r.Error + "）";
                return Fail(o, ui, dest, r.RollbackFailed ? 4 : 1, msg, relaunch: !r.RollbackFailed, Log);
            }

            Log("入れ替えました（" + r.Written + " ファイル）");
            Relaunch(o, ui, Log);
            return 0;
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static int Fail(Options o, Ui ui, string? dest, int code, string why, bool relaunch, Action<string> log)
    {
        log("★" + why);
        ui.Error(Title, why);
        if (relaunch && dest is not null) Relaunch(o, ui, log);
        return code;
    }

    private static void Relaunch(Options o, Ui ui, Action<string> log)
    {
        if (o.Relaunch is null) return;
        try
        {
            TH09.Launch.DriveLauncher.Relaunch(o.Relaunch);
            log("開き直しました: " + o.Relaunch);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or ArgumentException or IOException)
        {
            log("★開き直せませんでした: " + ex.Message);
            ui.Error(Title, "開き直せませんでした。手で起動してください。\n" + o.Relaunch);
        }
    }

    private static List<string> WaitUnlocked(string dest, List<string> files, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            var locked = Installer.Locked(dest, files);
            if (locked.Count == 0 || DateTime.UtcNow >= until) return locked;
            Thread.Sleep(500);
        }
    }
}

internal sealed class Ui(bool noDialog)
{
    private const uint MbOk = 0x0, MbRetryCancel = 0x5, MbIconError = 0x10, MbIconWarning = 0x30, MbTopmost = 0x40000;
    private const int IdRetry = 4;

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public void Error(string title, string text)
    {
        if (noDialog) return;
        MessageBox(IntPtr.Zero, text, title, MbOk | MbIconError | MbTopmost);
    }

    public bool RetryCancel(string title, string text)
    {
        if (noDialog) return false;
        return MessageBox(IntPtr.Zero, text, title, MbRetryCancel | MbIconWarning | MbTopmost) == IdRetry;
    }
}

internal static class Downloader
{
    private const long MaxBytes = 500L * 1024 * 1024;

    public static string? Get(string url, string to, Action<string> log)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Enmacho-Update");
            using var res = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode) return "HTTP " + (int)res.StatusCode;
            long? len = res.Content.Headers.ContentLength;
            if (len > MaxBytes) return "大きすぎます（" + len + " バイト）";
            using var src = res.Content.ReadAsStream();
            using var dst = File.Create(to);
            var buf = new byte[81920];
            long total = 0;
            int lastPct = -1;
            int n;
            while ((n = src.Read(buf, 0, buf.Length)) > 0)
            {
                total += n;
                if (total > MaxBytes) return "大きすぎます（" + total + " バイト以上）";
                dst.Write(buf, 0, n);
                if (len is > 0)
                {
                    int pct = (int)(total * 100 / len.Value) / 10 * 10;
                    if (pct != lastPct) { lastPct = pct; log("  " + pct + "%（" + total / 1024 + " KB）"); }
                }
            }
            log("落としました（" + total / 1024 + " KB）");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException
                                       or UnauthorizedAccessException or InvalidOperationException)
        {
            return ex.Message;
        }
    }
}
