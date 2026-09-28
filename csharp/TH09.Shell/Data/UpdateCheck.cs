using System.Net.Http;
using System.Threading;
using Avalonia.Threading;

namespace TH09.Shell.Data;

internal static class UpdateCheck
{
    private const string ReleasesLatestUrl = "https://api.github.com/repos/Mako-th/Enmacho/releases/latest";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static async Task RunAsync(string currentVersion, Action onNotify)
    {
        try
        {
            using var http = new HttpClient { Timeout = Timeout };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Enmacho-UpdateCheck");
            using var cts = new CancellationTokenSource(Timeout);
            var json = await http.GetStringAsync(ReleasesLatestUrl, cts.Token).ConfigureAwait(false);
            if (!UpdateNotice.ShouldNotify(json, currentVersion)) return;
            Dispatcher.UIThread.Post(onNotify);
        }
        catch (Exception ex)
        {
            LogSource.Info("更新の確認", "新しい版を確認できませんでした: " + LogSource.Describe(ex));
        }
    }
}
