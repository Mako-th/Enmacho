using System.Runtime.Versioning;
using Avalonia.Input;
using TH09.Record;

namespace TH09.Shell.Data;

[SupportedOSPlatform("windows")]
internal static class MonitorToggleKeyReading
{
    public const string Known = ConfigStore.RecordReplayToggleKeyDefault;

    public const Key KnownKey = Key.F9;

    public static Key? Read(string? text, out string reason)
    {
        if (string.Equals(text, Known, StringComparison.Ordinal))
        {
            reason = "";
            return KnownKey;
        }
        reason = "監視を切り替えるキーは " + Known + " だけです（"
                 + (string.IsNullOrWhiteSpace(text)
                     ? "空欄なので"
                     : text + " は読めないので")
                 + "、キーでは切り替えません）。";
        return null;
    }
}
