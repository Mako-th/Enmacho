using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TH09.Drive;

public sealed record LeftoverEvidence(string SlotFile, string Why);

public readonly record struct LeftoverSweep(int FromBackup, int FromMarker,
                                            int SkippedAlive, int SkippedUnknown)
{
    public int Restored => FromBackup + FromMarker;
}

public sealed class SlotMarker
{
    public bool HadBackupIsFalse { get; init; }

    public long? Pid { get; init; }

    public bool PidIsSelf { get; init; }

    public string PidText { get; init; } = "None";
}

public static class ReplaySlots
{
    public const string BackupSuffix = ".tickscan.bak";

    public const string OwnedSuffix = ".tickscan.owned";

    public const string TempSuffix = ".tickscan.tmp";

    public const int SlotMin = 1;

    public const int SlotMax = 25;

    private static readonly List<LeftoverEvidence> Evidence = [];

    private static readonly string[] Suffixes = [BackupSuffix, OwnedSuffix];

    public static void DefaultLog(string message) =>
        Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message);

    public static readonly Action<string> Quiet = _ => { };

    public static string NowIso() =>
        DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    public static bool IsSlotFile(string path, IEnumerable<string> dirs)
    {
        string name = Path.GetFileName(path);
        if (name.Length != 10
            || !name.StartsWith("th9_", StringComparison.OrdinalIgnoreCase)
            || !char.IsAsciiDigit(name[4]) || !char.IsAsciiDigit(name[5])
            || !name.EndsWith(".rpy", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string parent = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        return dirs.Any(d => SameDir(parent, d));
    }

    public static bool SameDir(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static string SlotPath(string replayDir, int slot) =>
        Path.Combine(replayDir, "th9_" + slot.ToString("00", CultureInfo.InvariantCulture) + ".rpy");

    public static int? SlotOf(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        if (!name.StartsWith("th9_", StringComparison.Ordinal)
            || !name.EndsWith(".rpy", StringComparison.Ordinal))
        {
            return null;
        }
        string body = name[4..^4];
        if (body.Length == 0 || !body.All(char.IsAsciiDigit)) return null;
        if (!int.TryParse(body, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            return null;
        }
        return value >= SlotMin && value <= SlotMax ? value : null;
    }

    public static bool PidAlive(long? pid)
    {
        if (pid is null || pid <= 0 || pid > int.MaxValue) return false;
        try
        {
            using var proc = Process.GetProcessById((int)pid.Value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    public static SlotMarker? ReadMarker(string owned)
    {
        try
        {
            byte[] raw = File.ReadAllBytes(owned);
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var root = doc.RootElement;

            bool hadBackupIsFalse = root.TryGetProperty("had_backup", out var hb)
                                    && hb.ValueKind == JsonValueKind.False;

            long? pid = null;
            bool pidIsSelf = false;
            string pidText = "None";
            if (root.TryGetProperty("pid", out var pe))
            {
                switch (pe.ValueKind)
                {
                    case JsonValueKind.Number:
                        if (pe.TryGetInt64(out long n))
                        {
                            pid = n;
                            pidIsSelf = n == Environment.ProcessId;
                        }
                        pidText = pe.GetRawText();
                        break;
                    case JsonValueKind.String:
                        string s = pe.GetString() ?? "";
                        if (long.TryParse(s.Trim(), NumberStyles.AllowLeadingSign,
                                          CultureInfo.InvariantCulture, out long m))
                        {
                            pid = m;
                        }
                        pidText = s;
                        break;
                    case JsonValueKind.True: pidText = "True"; break;
                    case JsonValueKind.False: pidText = "False"; break;
                    case JsonValueKind.Null: pidText = "None"; break;
                    default: pidText = pe.GetRawText(); break;
                }
            }
            return new SlotMarker
            {
                HadBackupIsFalse = hadBackupIsFalse,
                Pid = pid,
                PidIsSelf = pidIsSelf,
                PidText = pidText,
            };
        }
        catch (Exception exc) when (exc is IOException or UnauthorizedAccessException
                                        or JsonException or ArgumentException)
        {
            return null;
        }
    }

    public static void NoteLeftoverEvidence(string target, string why)
    {
        lock (Evidence) Evidence.Add(new LeftoverEvidence(Path.GetFileName(target), why));
    }

    public static IReadOnlyList<LeftoverEvidence> TakeLeftoverEvidence()
    {
        lock (Evidence)
        {
            var found = Evidence.ToArray();
            Evidence.Clear();
            return found;
        }
    }

    public static string? LeftoverPlaybackReason(IReadOnlyList<LeftoverEvidence> evidence,
                                                 bool playing, string? replayPath = null,
                                                 bool? demo = null)
    {
        if (evidence is null || evidence.Count == 0) return null;
        if (!playing) return null;
        if (demo is true) return null;
        var names = evidence.Select(e => e.SlotFile.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(replayPath))
        {
            string basename = replayPath.Replace('\\', '/');
            int cut = basename.LastIndexOf('/');
            basename = (cut >= 0 ? basename[(cut + 1)..] : basename).ToLowerInvariant();
            if (basename.Length > 0 && !names.Contains(basename))
            {
                return null;
            }
        }
        string where = string.IsNullOrEmpty(replayPath)
            ? "再生中のパスは読めません"
            : "再生中のパス " + replayPath + " も一致";
        return "前回の走査の残骸が残っています（" + string.Join(" / ", evidence.Select(e => e.Why))
               + "）。退避を戻さずに終わった＝最後まで行かなかった証拠なので、"
               + "いま再生中のリプレイは走査のものと判断します（" + where + "）";
    }

    public static string? RecoverLeftoverPlayback(MenuDriver drv, IMenuView view,
                                                  IMenuMemory? mem, Action<string>? log = null)
    {
        var evidence = TakeLeftoverEvidence();
        if (evidence.Count == 0) return null;
        var say = log ?? DefaultLog;

        bool playing = view.ReplayPlaying();
        bool? demo = view.TitleDemo();
        string? path = MenuMap.ReadReplayPath(mem);
        string? why = LeftoverPlaybackReason(evidence, playing: playing, replayPath: path, demo: demo);
        if (why is null)
        {
            say("前回の走査の残骸は回収しましたが、いま止める理由はありません（再生中="
                + Bool(playing) + " / パス=" + (path ?? "None") + " / デモ=" + Tri(demo)
                + " / 残骸=" + string.Join(" / ", evidence.Select(e => e.Why))
                + "）。従来どおり待ちます。");
            return null;
        }
        drv.EndReplayPlayback(why);
        return why;
    }

    private static string Bool(bool value) => value ? "True" : "False";

    private static string Tri(bool? value) => value is null ? "None" : Bool(value.Value);

    public static LeftoverSweep RestoreLeftoverBackups(string replayDir, Action<string>? log = null)
    {
        var say = log ?? DefaultLog;
        int fromBackup = 0, fromMarker = 0, skippedAlive = 0, skippedUnknown = 0;

        var targets = new SortedSet<string>(StringComparer.Ordinal);
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string entry in SafeList(replayDir))
        {
            string name = Path.GetFileName(entry);
            foreach (string suffix in Suffixes)
            {
                if (name.Length > suffix.Length
                    && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    string target = name[..^suffix.Length];
                    if (targets.Add(target)) byName[target] = Path.Combine(replayDir, target);
                }
            }
        }

        foreach (string name in targets)
        {
            string target = byName[name];
            string bak = target + BackupSuffix;
            string owned = target + OwnedSuffix;
            var marker = File.Exists(owned) ? ReadMarker(owned) : null;

            if (File.Exists(owned) && marker is null)
            {
                skippedUnknown++;
                NoteLeftoverEvidence(target, Path.GetFileName(owned) + " が壊れた形で残っている");
                say("警告: " + Path.GetFileName(owned) + " が読めません（JSON が壊れています）。安全のため触りません。");
                say("      対処: " + name + " の中身を確認し、問題なければ "
                    + Path.GetFileName(owned) + " を手で消してください。");
                continue;
            }

            if (marker is not null && !marker.PidIsSelf && PidAlive(marker.Pid))
            {
                skippedAlive++;
                say("別プロセス（pid=" + marker.PidText + "）がスロット " + name + " を使用中です。触りません。");
                continue;
            }

            try
            {
                if (File.Exists(bak))
                {
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(bak, target, overwrite: true);
                    if (File.Exists(owned)) File.Delete(owned);
                    fromBackup++;
                    NoteLeftoverEvidence(target, Path.GetFileName(bak)
                        + " が残っていた（前回の走査がスロットを退避したまま終わっている）");
                    say("前回の残骸を復元しました: " + name);
                    continue;
                }

                if (marker is null) continue;
                if (marker.HadBackupIsFalse)
                {
                    if (File.Exists(target)) File.Delete(target);
                    File.Delete(owned);
                    fromMarker++;
                    NoteLeftoverEvidence(target, Path.GetFileName(owned)
                        + " が残っていた（元が空のスロットを占有したまま終わっている）");
                    say("前回の残骸を削除しました: " + name + "（元は空のスロット）");
                }
                else
                {
                    skippedUnknown++;
                    NoteLeftoverEvidence(target, Path.GetFileName(owned)
                        + " が残っていた（退避ありと言っているのに" + Path.GetFileName(bak) + " が無い）");
                    say("警告: " + Path.GetFileName(owned) + " は退避ありと言っていますが "
                        + Path.GetFileName(bak) + " がありません。触りません。");
                    say("      対処: " + name + " が元から入っていたリプレイか確かめ、違えば消して "
                        + Path.GetFileName(owned) + " も消してください。");
                }
            }
            catch (Exception exc) when (exc is IOException or UnauthorizedAccessException)
            {
                skippedUnknown++;
                say("警告: " + name + " を回収できません: " + exc.Message);
            }
        }

        var sweep = new LeftoverSweep(fromBackup, fromMarker, skippedAlive, skippedUnknown);
        if (sweep.Restored != 0 || skippedAlive != 0 || skippedUnknown != 0)
        {
            say("残骸チェック: 退避から復元 " + Num(fromBackup) + " 件 / マーカーから削除 "
                + Num(fromMarker) + " 件 / 使用中で見送り " + Num(skippedAlive)
                + " 件 / 判断不能 " + Num(skippedUnknown) + " 件");
        }
        return sweep;
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<string> SafeList(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir);
        }
        catch (Exception exc) when (exc is IOException or UnauthorizedAccessException
                                        or ArgumentException)
        {
            return [];
        }
    }

    internal static string MarkerJson(int slot, string source, bool hadBackup,
                                      string startedAt, int pid, long? jobId)
    {
        var sb = new StringBuilder();
        sb.Append("{\"slot\": ").Append(slot.ToString(CultureInfo.InvariantCulture));
        sb.Append(", \"source\": ").Append(JsonString(source));
        sb.Append(", \"had_backup\": ").Append(hadBackup ? "true" : "false");
        sb.Append(", \"started_at\": ").Append(JsonString(startedAt));
        sb.Append(", \"pid\": ").Append(pid.ToString(CultureInfo.InvariantCulture));
        sb.Append(", \"job_id\": ").Append(jobId is null
            ? "null" : jobId.Value.ToString(CultureInfo.InvariantCulture));
        sb.Append('}');
        return sb.ToString();
    }

    private static string JsonString(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in value)
        {
            switch (c)
            {
                case Quote: sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.Append(Quote).ToString();
    }

    private const char Quote = (char)0x22;
}

public sealed class SlotOccupationError : Exception
{
    public SlotOccupationError(string message) : base(message) { }
}

public sealed class SlotOccupation : IDisposable
{
    private readonly Action<string> _log;
    private readonly object _gate = new();
    private EventHandler? _atExit;
    private bool _copied;

    public SlotOccupation(string replayDir, string source, int? slot,
                          long? jobId = null, Action<string>? log = null)
    {
        ReplayDir = replayDir;
        Source = source;
        JobId = jobId;
        _log = log ?? ReplaySlots.DefaultLog;
        string? parent = Path.GetDirectoryName(Path.GetFullPath(source));
        int? existing = parent is not null && ReplaySlots.SameDir(parent, replayDir)
            ? ReplaySlots.SlotOf(source) : null;
        if (existing is not null)
        {
            Slot = existing.Value;
            Target = Path.GetFullPath(source);
        }
        else
        {
            Slot = slot ?? throw new ArgumentException(
                "スロット番号が要ります（リプレイがスロットに入っていません）: " + source);
            Target = ReplaySlots.SlotPath(replayDir, Slot);
        }
    }

    public string ReplayDir { get; }

    public string Source { get; }

    public int Slot { get; }

    public long? JobId { get; }

    public string Target { get; }

    public string? Backup { get; private set; }

    public string? Marker { get; private set; }

    public SlotOccupation Enter()
    {
        if (ReplaySlots.SameDir(Target, Source))
        {
            _log("リプレイは既にスロット " + Two(Slot) + " に入っています（ファイル操作なし）。");
            return this;
        }
        bool hadBackup = File.Exists(Target);
        if (hadBackup)
        {
            Backup = Target + ReplaySlots.BackupSuffix;
            if (File.Exists(Backup))
            {
                throw new SlotOccupationError(
                    "エラー: 退避先が既にあります: " + Backup + "\n"
                    + "       --restore-slots で先に復元してください。");
            }
            File.Move(Target, Backup);
            _log("スロット " + Two(Slot) + " の中身を退避しました: " + Path.GetFileName(Backup));
        }
        Marker = Target + ReplaySlots.OwnedSuffix;
        File.WriteAllText(Marker, ReplaySlots.MarkerJson(
            Slot, Path.GetFullPath(Source), hadBackup, ReplaySlots.NowIso(),
            Environment.ProcessId, JobId), new UTF8Encoding(false));
        _atExit = (_, _) => Restore();
        AppDomain.CurrentDomain.ProcessExit += _atExit;
        _copied = true;
        string tmp = Target + ReplaySlots.TempSuffix;
        File.Copy(Source, tmp, overwrite: true);
        File.SetLastWriteTimeUtc(tmp, File.GetLastWriteTimeUtc(Source));
        File.Move(tmp, Target, overwrite: true);
        _log("スロット " + Two(Slot) + " へリプレイを置きました: " + Path.GetFileName(Source));
        return this;
    }

    public void Restore()
    {
        lock (_gate)
        {
            if (!_copied) return;
            _copied = false;
            if (_atExit is not null)
            {
                AppDomain.CurrentDomain.ProcessExit -= _atExit;
                _atExit = null;
            }
            try
            {
                if (Backup is not null && File.Exists(Backup))
                {
                    if (File.Exists(Target)) File.Delete(Target);
                    File.Move(Backup, Target, overwrite: true);
                    _log("スロット " + Two(Slot) + " を元に戻しました。");
                }
                else if (File.Exists(Target))
                {
                    File.Delete(Target);
                    _log("スロット " + Two(Slot) + " に置いたリプレイを削除しました（元は空）。");
                }
                if (Marker is not null && File.Exists(Marker)) File.Delete(Marker);
            }
            catch (Exception exc) when (exc is IOException or UnauthorizedAccessException)
            {
                _log("警告: スロット " + Two(Slot) + " を復元できません: " + exc.Message);
            }
        }
    }

    public void Dispose() => Restore();

    private static string Two(int value) => value.ToString("00", CultureInfo.InvariantCulture);
}
