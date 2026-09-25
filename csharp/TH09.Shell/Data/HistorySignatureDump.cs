using System.Globalization;
using System.Text;

namespace TH09.Shell.Data;

internal static class HistorySignatureDump
{
    public const string Flag = "--dump-history-signature";

    public static int Run(string dbPath)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(),
                                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            TrackerDb.MainDbPath = dbPath;
            if (!TrackerDb.MainDbExists)
            {
                Console.Error.WriteLine("本体 DB が見つかりません: " + dbPath);
                return 2;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var snap = HistoryQuery.Read(dbPath);
            sw.Stop();

            stdout.WriteLine("rows\t" + snap.Rows.Count.ToString(CultureInfo.InvariantCulture));
            stdout.WriteLine("protected\t" + snap.Protected.Count.ToString(CultureInfo.InvariantCulture));
            stdout.WriteLine("sig\t" + snap.Signature);
            stdout.WriteLine("ms\t" + sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            stdout.Flush();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }
}
