using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TH09.Layer0;
using TH09.TickBus;

using ScanItems = TH09.Generated.DbColumns.ReplayScanItems;
using SessionCols = TH09.Generated.DbColumns.Sessions;
using StageCols = TH09.Generated.DbColumns.Stages;
using RoundCols = TH09.Generated.DbColumns.Rounds;

namespace TH09.Record;

public static class CaptureLoopScript
{
    public const string FormatVersion = "th09-record-captureloop-v1";

    public const string AliveUnknown = "unknown";

    private sealed class Setup
    {
        public int Margin = RingReader.DefaultMargin;
        public int Rewind;
        public int InvalidTicks = 30;
        public double InvalidSeconds = 0.5;
        public double StallSeconds = 3.0;
        public bool Adonis;
        public string GameAlive = AliveUnknown;

        public double Timeout = 1.0;
        public double IdleClose = 2.0;
        public double PollSleep = CaptureLoop.PollSleepSeconds;
        public bool Announce;
        public string? DecodedJson;
        public int StopAfter = -1;
        public int CancelAfter = -1;

        public bool Layer0 = true;
        public int SegmentTicks = 100000;
        public int RecordVersion = (int)TH09.Generated.TickWords.Version;

        public string? SkipReason;

        public int AtMenuFrom = -1;

        public readonly Dictionary<int, uint> MenuSeek = [];

        public readonly List<(string Prev, string Now)> SamePlayCases = [];

        public bool HitWin;
        public int? Before;
        public int? After;
        public List<string>? Triggers;

        public readonly Dictionary<int, uint> IdleSeek = [];
        public uint? IdleSeekAlways;

        public bool RunLoop;
        public int Stages = 1;

        public readonly List<(string Raw, int Stages)> HealthCases = [];

        public readonly List<(long Difficulty, long P1Character, string Json)> EarlyCases = [];
    }

    private static Setup Parse(string path)
    {
        var s = new Setup();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split('\t');
            switch (f[0])
            {
                case "margin": s.Margin = Int(f[1]); break;
                case "rewind": s.Rewind = Int(f[1]); break;
                case "invalid_ticks": s.InvalidTicks = Int(f[1]); break;
                case "invalid_seconds": s.InvalidSeconds = Dbl(f[1]); break;
                case "stall_seconds": s.StallSeconds = Dbl(f[1]); break;
                case "adonis": s.Adonis = f[1] == "1"; break;
                case "gamealive": s.GameAlive = f[1]; break;
                case "timeout": s.Timeout = Dbl(f[1]); break;
                case "idle_close": s.IdleClose = Dbl(f[1]); break;
                case "poll_sleep": s.PollSleep = Dbl(f[1]); break;
                case "announce": s.Announce = f[1] == "1"; break;
                case "decoded": s.DecodedJson = f[1]; break;
                case "stop_after": s.StopAfter = Int(f[1]); break;
                case "cancel_after": s.CancelAfter = Int(f[1]); break;
                case "skip": s.SkipReason = f[1]; break;
                case "at_menu":
                    s.AtMenuFrom = f[1] == "never" ? int.MaxValue : Int(f[1]);
                    break;
                case "menu_seek":
                    s.MenuSeek[Int(f[1])] = uint.Parse(f[2], CultureInfo.InvariantCulture);
                    break;
                case "layer0": s.Layer0 = f[1] == "1"; break;
                case "segment_ticks": s.SegmentTicks = Int(f[1]); break;
                case "record_version": s.RecordVersion = Int(f[1]); break;
                case "hitwin": s.HitWin = f[1] == "1"; break;
                case "before": s.Before = Int(f[1]); break;
                case "after": s.After = Int(f[1]); break;
                case "triggers":
                    s.Triggers = [.. f[1].Split(',', StringSplitOptions.RemoveEmptyEntries)];
                    break;
                case "stages": s.Stages = Int(f[1]); break;
                case "idle_seek":
                    if (f[1] == "*") s.IdleSeekAlways = uint.Parse(f[2], CultureInfo.InvariantCulture);
                    else s.IdleSeek[Int(f[1])] = uint.Parse(f[2], CultureInfo.InvariantCulture);
                    break;
                case "@health":
                    s.HealthCases.Add((f.Length > 1 ? f[1] : "", f.Length > 2 ? Int(f[2]) : 1));
                    break;
                case "@early":
                    s.EarlyCases.Add((long.Parse(f[1], CultureInfo.InvariantCulture),
                                      long.Parse(f[2], CultureInfo.InvariantCulture), f[3]));
                    break;
                case "@sameplay": s.SamePlayCases.Add((f[1], f[2])); break;
                case "@run": s.RunLoop = true; break;
                default:
                    throw new InvalidDataException("台本に知らない行があります: " + line);
            }
        }
        if (!s.RunLoop && s.HealthCases.Count == 0 && s.EarlyCases.Count == 0
            && s.SamePlayCases.Count == 0)
            throw new InvalidDataException("台本に手順が 1 つもありません（0 手は『測れていない』）");
        return s;
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static double Dbl(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    public static int Run(string? busName, string? coordName, string scriptPath,
                          string? dbPath, string? layer0Path, string outPath, Action<string> log)
    {
        var setup = Parse(scriptPath);
        var sb = new StringBuilder();
        sb.Append("# ").Append(FormatVersion).Append(" out\n");
        sb.Append("# ★原本は csharp/TH09.Record/CaptureLoopScript.cs（台本は Python 側が書く）\n");

        WriteConstants(sb);
        foreach (var (rawHealth, stages) in setup.HealthCases)
        {
            var result = new CaptureResult { Health = ParseHealth(rawHealth) };
            sb.Append("health\t").Append(rawHealth.Length == 0 ? "-" : rawHealth)
              .Append('\t').Append(stages.ToString(CultureInfo.InvariantCulture))
              .Append('\t').Append(CaptureHealth.Problem(result, stages) ?? "None").Append('\n');
        }
        foreach (var (difficulty, p1, json) in setup.EarlyCases)
        {
            sb.Append("early\t").Append(difficulty.ToString(CultureInfo.InvariantCulture))
              .Append('\t').Append(p1.ToString(CultureInfo.InvariantCulture))
              .Append('\t').Append(json).Append('\t')
              .Append(EarlyMismatchRule.Of(FakeSnapshot(difficulty, p1), ReplayStages.Parse(json))
                      ?? "None")
              .Append('\n');
        }
        foreach (var (prev, now) in setup.SamePlayCases)
        {
            sb.Append("sameplay\t").Append(prev).Append('\t').Append(now).Append('\t')
              .Append(SamePlayRule.Broken(PlaySnapshot(prev), PlaySnapshot(now)) ?? "None")
              .Append('\n');
        }

        var ran = 0;
        if (setup.RunLoop)
        {
            if (busName is null || dbPath is null || layer0Path is null)
            {
                Console.Error.WriteLine("@run には --name / --db / --layer0 が要ります。");
                return 1;
            }
            RunOnce(setup, busName, coordName, dbPath, layer0Path, sb, log);
            ran = 1;
        }

        sb.Append("end\tran=").Append(ran.ToString(CultureInfo.InvariantCulture))
          .Append("\thealth_cases=").Append(setup.HealthCases.Count.ToString(CultureInfo.InvariantCulture))
          .Append("\tearly_cases=").Append(setup.EarlyCases.Count.ToString(CultureInfo.InvariantCulture))
          .Append("\tsameplay_cases=")
          .Append(setup.SamePlayCases.Count.ToString(CultureInfo.InvariantCulture))
          .Append('\n');
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("書き出し: " + outPath);
        Console.WriteLine("  ★母数: 走行 " + ran.ToString(CultureInfo.InvariantCulture)
                          + " 回 / 健康値の標本 "
                          + setup.HealthCases.Count.ToString(CultureInfo.InvariantCulture)
                          + " 件 / 食い違いの標本 "
                          + setup.EarlyCases.Count.ToString(CultureInfo.InvariantCulture)
                          + " 件 / 続きかどうかの標本 "
                          + setup.SamePlayCases.Count.ToString(CultureInfo.InvariantCulture) + " 件");
        return ran > 0
               || setup.HealthCases.Count + setup.EarlyCases.Count + setup.SamePlayCases.Count > 0
            ? 0 : 1;
    }

    private static void WriteConstants(StringBuilder sb)
    {
        void Const(string name, string value) =>
            sb.Append("const\t").Append(name).Append('\t').Append(value).Append('\n');

        Const("not_run", CaptureResult.NotRun);
        Const("captured", CaptureStatus.Captured);
        Const("disconnected", CaptureStatus.Disconnected);
        Const("interrupted", CaptureStatus.Interrupted);
        Const("timeout", CaptureStatus.Timeout);
        Const("setup_failed", CaptureStatus.SetupFailed);
        Const("menu_failed", CaptureStatus.MenuFailed);
        Const("error", CaptureStatus.Error);
        Const("logger_version", CaptureLoop.LoggerVersion);
        Const("newgame_streak", CaptureLoop.NewGameStreak.ToString(CultureInfo.InvariantCulture));
        Const("min_ticks_per_stage",
              CaptureHealth.MinTicksPerStage.ToString(CultureInfo.InvariantCulture));
        Const("poll_sleep", CaptureLoop.PollSleepSeconds.ToString("R", CultureInfo.InvariantCulture));
        Const("unavailable_sleep",
              CaptureLoop.UnavailableSleepSeconds.ToString("R", CultureInfo.InvariantCulture));
        Const("absent_sleep", CaptureLoop.AbsentSleepSeconds.ToString("R", CultureInfo.InvariantCulture));
        Const("layer0_sink", CaptureLoop.Layer0SinkName);
        Const("hitwin_sink", HitWindows.SinkName);

        foreach (var key in CaptureStatus.SessionClose.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            var v = CaptureStatus.SessionClose[key];
            sb.Append("sessionclose\t").Append(key).Append('\t').Append(v.Reason)
              .Append('\t').Append(v.SessionStatus).Append('\n');
        }
        sb.Append("sessionclose_fallback\t").Append(CaptureStatus.Fallback.Reason)
          .Append('\t').Append(CaptureStatus.Fallback.SessionStatus).Append('\n');

        sb.Append("failure\tcancel\t")
          .Append(FailureStatus.Of(new OperationCanceledException())).Append('\n');
        sb.Append("failure\thooklost\t")
          .Append(FailureStatus.Of(new TickHookLostException("x"))).Append('\n');
        sb.Append("failure\tother\t")
          .Append(FailureStatus.Of(new InvalidOperationException("x"))).Append('\n');
    }

    private static Snapshot FakeSnapshot(long difficulty, long p1Character)
    {
        var words = new uint[TickBusLayout.RecordWordCount];
        words[TH09.Generated.TickWords.Record.DifficultyOffset >> 2] = (uint)difficulty;
        words[TH09.Generated.TickWords.Record.P1CharacterOffset >> 2] = (uint)p1Character;
        return LiveTickSource.FromRecord(new TickRecord(words), "", 0.0);
    }

    private static Snapshot PlaySnapshot(string text)
    {
        var f = text.Split(',');
        if (f.Length != 6)
        {
            throw new InvalidDataException(
                "@sameplay の 1 マスは mode,difficulty,p1_character,p2_character,stage_index,"
                + "p1_score_raw の 6 つです: " + text);
        }
        var words = new uint[TickBusLayout.RecordWordCount];
        words[TH09.Generated.TickWords.Record.ModeOffset >> 2] = Word(f[0]);
        words[TH09.Generated.TickWords.Record.DifficultyOffset >> 2] = Word(f[1]);
        words[TH09.Generated.TickWords.Record.P1CharacterOffset >> 2] = Word(f[2]);
        words[TH09.Generated.TickWords.Record.P2CharacterOffset >> 2] = Word(f[3]);
        words[TH09.Generated.TickWords.Record.StageIndexOffset >> 2] = Word(f[4]);
        words[TH09.Generated.TickWords.Record.P1ScoreRawOffset >> 2] = Word(f[5]);
        return LiveTickSource.FromRecord(new TickRecord(words), "", 0.0);
    }

    private static uint Word(string s) => uint.Parse(s, CultureInfo.InvariantCulture);

    private static Dictionary<string, long?> ParseHealth(string text)
    {
        var health = new Dictionary<string, long?>(StringComparer.Ordinal);
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=');
            if (kv.Length != 2) throw new InvalidDataException("健康値の書き方が読めません: " + part);
            if (!ScanItems.All.Contains(kv[0], StringComparer.Ordinal))
                throw new InvalidDataException("replay_scan_items に無い列名です: " + kv[0]);
            health[kv[0]] = kv[1] == "None" ? null : long.Parse(kv[1], CultureInfo.InvariantCulture);
        }
        return health;
    }

    private static void RunOnce(Setup setup, string busName, string? coordName,
                                string dbPath, string layer0Path, StringBuilder sb, Action<string> log)
    {
        using var db = Schema.CreateFrom(dbPath);

        HitWindows? windows = null;
        if (setup.HitWin)
        {
            if (coordName is null)
                throw new InvalidDataException("hitwin 1 には --coord-name が要ります。");
            windows = new HitWindows(CoordBusReader.OpenVerified(coordName), log,
                                     setup.Triggers, setup.Before, setup.After,
                                     scopes: null, margin: CoordRingReader.DefaultMargin)
            {
                AdonisLoaded = setup.Adonis,
            };
        }

        LiveTickSource? src = null;
        string? sinkNames = null;
        long makeSource = 0, idles = 0, aliveCalls = 0, stopCalls = 0, menuCalls = 0;
        using var cancel = new CancellationTokenSource();

        Func<bool>? gameAlive = setup.GameAlive == AliveUnknown ? null : () =>
        {
            aliveCalls++;
            Remember();
            return setup.GameAlive != "dead";
        };

        void Remember()
        {
            if (sinkNames is null && src is not null) sinkNames = string.Join(",", src.SinkNames);
        }

        LiveTickSource MakeSource()
        {
            makeSource++;
            var bus = TickBusReader.OpenVerified(busName);
            try
            {
                src = new LiveTickSource(
                    bus,
                    new LiveTickSource.Options(setup.Rewind, setup.Margin, setup.InvalidTicks,
                                               setup.InvalidSeconds, setup.StallSeconds),
                    log)
                {
                    AdonisLoaded = setup.Adonis,
                    GameAlive = gameAlive,
                };
                return src;
            }
            catch
            {
                bus.Dispose();
                throw;
            }
        }

        void Idle()
        {
            Remember();
            if (setup.IdleSeek.TryGetValue((int)idles, out var at)) src!.Ring.NextIndex = at;
            else if (setup.IdleSeekAlways is uint always) src!.Ring.NextIndex = always;
            idles++;
            if (setup.CancelAfter >= 0 && idles >= setup.CancelAfter) cancel.Cancel();
            var ms = (int)Math.Round(setup.PollSleep * 1000.0);
            if (ms > 0) Thread.Sleep(ms);
        }

        bool ShouldStop()
        {
            stopCalls++;
            return idles >= setup.StopAfter;
        }

        bool AtTitleOrMenu()
        {
            menuCalls++;
            if (setup.MenuSeek.TryGetValue((int)menuCalls, out var at)) src!.Ring.NextIndex = at;
            return menuCalls >= setup.AtMenuFrom;
        }

        Layer0Writer? OpenArchive() => Layer0Writer.Open(new Layer0Writer.Options(
            DbPath: layer0Path,
            Mode: Layer0OpenMode.Create,
            RecordVersion: setup.RecordVersion,
            Fields: [.. TickBusLayout.RecordFields.Select(
                x => new Layer0Writer.FieldSlot(x.Name, x.Offset >> 2))],
            Policy: TickEncoder.SectionPolicy.Empty,
            MaxSegmentTicks: setup.SegmentTicks));

        var options = new CaptureLoop.Options(
            Conn: db.Connection,
            MakeSource: MakeSource,
            Timeout: setup.Timeout,
            IdleClose: setup.IdleClose,
            OpenArchive: setup.Layer0 ? OpenArchive : null,
            Windows: windows,
            Decoded: setup.DecodedJson is null ? null : ReplayStages.Parse(setup.DecodedJson),
            ShouldStop: setup.StopAfter >= 0 ? ShouldStop : null,
            SkipSession: setup.SkipReason is null ? null : _ => setup.SkipReason,
            AtTitleOrMenu: setup.AtMenuFrom < 0 ? null : AtTitleOrMenu,
            Announce: setup.Announce,
            Cancel: cancel.Token,
            Log: log,
            Idle: Idle);

        var result = CaptureLoop.Run(options);
        windows?.Close();

        sb.Append("run\tstatus=").Append(result.Status)
          .Append("\tsession=").Append(result.SessionId?.ToString(CultureInfo.InvariantCulture) ?? "None")
          .Append("\tgap=").Append(result.GapCount.ToString(CultureInfo.InvariantCulture))
          .Append("\tsource=").Append(result.SourceKind ?? "None")
          .Append("\tearly=").Append(result.EarlyMismatch ?? "None")
          .Append("\terror=").Append(result.Error ?? "None")
          .Append("\telapsed=").Append(result.Elapsed.ToString("F2", CultureInfo.InvariantCulture))
          .Append('\n');
        sb.Append("sinks\t").Append(sinkNames ?? "?").Append('\n');
        sb.Append("problem\t")
          .Append(CaptureHealth.Problem(result, setup.Stages) ?? "None").Append('\n');

        foreach (var name in ScanItems.All.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!result.Health.TryGetValue(name, out var v)) continue;
            sb.Append("health_col\t").Append(name).Append('\t')
              .Append(v?.ToString(CultureInfo.InvariantCulture) ?? "None").Append('\n');
        }
        sb.Append("health_count\t")
          .Append(result.Health.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');

        DumpDb(db.Connection, sb);

        sb.Append("counters\tmakesource=").Append(makeSource.ToString(CultureInfo.InvariantCulture))
          .Append("\tidles=").Append(idles.ToString(CultureInfo.InvariantCulture))
          .Append("\talive=").Append(aliveCalls.ToString(CultureInfo.InvariantCulture))
          .Append("\tstop=").Append(stopCalls.ToString(CultureInfo.InvariantCulture))
          .Append("\tmenu=").Append(menuCalls.ToString(CultureInfo.InvariantCulture))
          .Append('\n');
    }

    private static void DumpDb(SqliteConnection conn, StringBuilder sb)
    {
        void Dump(string tag, string sql)
        {
            var seq = 0;
            var running = 0;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var status = r.IsDBNull(0) ? "None" : r.GetString(0);
                if (string.Equals(status, ScanLedger.StatusRunning, StringComparison.Ordinal)) running++;
                sb.Append("db_").Append(tag).Append('\t')
                  .Append(seq.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(status).Append('\n');
                seq++;
            }
            sb.Append("db_count\t").Append(tag).Append('\t')
              .Append(seq.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(running.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        Dump("session", $"SELECT {SessionCols.Status} FROM {SessionCols.Table}"
                        + $" ORDER BY {SessionCols.SessionId}");
        Dump("stage", $"SELECT {StageCols.Status} FROM {StageCols.Table}"
                      + $" ORDER BY {StageCols.StageRecordId}");
        Dump("round", $"SELECT {RoundCols.Status} FROM {RoundCols.Table}"
                      + $" ORDER BY {RoundCols.RoundRecordId}");
    }
}
