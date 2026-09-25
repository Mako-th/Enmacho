using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace TH09.Shell.Views;

internal static class FolderOpener
{
    public static async Task RevealAsync(TopLevel? top, string? directory)
    {
        if (top is null || string.IsNullOrWhiteSpace(directory)) return;
        try
        {
            var dir = new DirectoryInfo(directory);
            if (!dir.Exists)
            {
                Data.LogSource.Warn("リプレイ", "フォルダがありません: " + directory);
                return;
            }
            var ok = await top.Launcher.LaunchDirectoryInfoAsync(dir);
            if (!ok) Data.LogSource.Warn("リプレイ", "フォルダを開けませんでした: " + directory);
        }
        catch (Exception ex)
        {
            Data.LogSource.Error("リプレイ", "フォルダを開けませんでした: " + ex.Message);
        }
    }
}
