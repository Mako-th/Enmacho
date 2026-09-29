using TH09.Launch;

namespace TH09.Shell.Data;

internal static class UpdateLauncher
{
    public static bool CanOffer(UpdateNotice.Offer offer) => WhyNotOffer(offer) is null;

    public static string? WhyNotOffer(UpdateNotice.Offer offer)
    {
        if (BuildInfo.IsDev) return "開発者版なので確認を出さない";
        if (offer.ZipUrl is null) return "Releases に配る zip が見つからない";
        if (!DriveLauncher.UpdaterExists) return "入れ替え役が本体の隣に無い";
        return null;
    }

    public static IReadOnlyList<string> BuildArgs(UpdateNotice.Offer offer, string destDir, string shellExePath)
        => DriveLauncher.PreviewUpdaterArguments(Request(offer, destDir, shellExePath));

    private static UpdaterRequest Request(UpdateNotice.Offer offer, string destDir, string shellExePath)
        => new(destDir, offer.ZipUrl!, offer.Sha256, shellExePath);

    public static bool TryStart(UpdateNotice.Offer offer)
    {
        try
        {
            string? me = Environment.ProcessPath;
            if (me is null)
            {
                LogSource.Warn("更新", "入れ替え役を起こせませんでした: 自分の exe の場所が分からない");
                return false;
            }
            string runDir = DriveLauncher.StartUpdater(
                Request(offer, AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), me));
            LogSource.Info("更新", "入れ替え役を起こしました: " + runDir);
            return true;
        }
        catch (Exception ex)
        {
            LogSource.Warn("更新", "入れ替え役を起こせませんでした: " + LogSource.Describe(ex));
            return false;
        }
    }

    public static void CleanOldCopies()
        => Clean(Path.GetTempPath(), TimeSpan.FromMinutes(10));

    public static int Clean(string tempRoot, TimeSpan minAge)
    {
        int n = UpdaterRuns.Clean(tempRoot, minAge, m => LogSource.Info("更新", m));
        if (n > 0) LogSource.Info("更新", "入れ替え役の古い写しを " + n + " 件消しました");
        return n;
    }
}
