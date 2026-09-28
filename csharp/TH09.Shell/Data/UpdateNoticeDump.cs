using System.Text;

namespace TH09.Shell.Data;

internal static class UpdateNoticeDump
{
    public const string Flag = "--dump-update-notice";

    public static int Run(string releaseJsonPath, string currentVersion)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        string json;
        try
        {
            json = File.ReadAllText(releaseJsonPath);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine("JSON を読めません: " + releaseJsonPath + "（" + ex.Message + "）");
            return 2;
        }

        bool notify = UpdateNotice.ShouldNotify(json, currentVersion);
        stdout.WriteLine("notify\t" + (notify ? "1" : "0"));
        stdout.WriteLine("message\t" + (notify ? UpdateNotice.Message : ""));
        stdout.Flush();
        return 0;
    }
}
