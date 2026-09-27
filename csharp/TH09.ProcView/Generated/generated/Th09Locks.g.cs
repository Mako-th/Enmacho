#nullable enable

namespace TH09.Generated;

internal static class ExclusiveLocks
{
    public const int Count = 2;

    public const string SessionWriter = @"Local\TH09SessionWriter";

    public const string ScanActive = @"Local\TH09ScanActive";

    public static readonly string[] All = [SessionWriter, ScanActive];
}
