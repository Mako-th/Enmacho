using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using TH09.Generated;
using TH09.Record.Generated;

namespace TH09.Record;

public static class Program
{
    private const string Usage = """
        TH09 記録（段階 3）。Python 版 th09_paths.py / th09_repository.py と同じ形を出す。

          th09_record --paths [--no-probe]
              ★いまアプリのデータをどこへ置くつもりかと、そこへ書けるかを出す。
              ★書けない場所に居たら、はっきり言って終了コード 2 で止まる（別の場所へは逃げない）。
              --no-probe を付けると、実際に書いてみる確認だけを飛ばす。

          th09_record --schema-shape --out <shape.tsv>
              ★C# 側のスキーマの原本だけから空の DB を組み、その形（表・列・型・NOT NULL・
              既定値・主キー・並び）を吐く。Python 側の 4 つの定義元から組んだ形と突き合わせる用。

          th09_record --schema-check --main <tracker.sqlite3>
              ★その実 DB が「定義元から作れる形か」を見る。
              ★差は向きで意味が違う: 実 DB にあって定義元に無い＝致命的 /
              定義元にあって実 DB に無い＝次に開けば `ALTER` で足される（移行待ち）。

          th09_record --spec-check --main <tracker.sqlite3>
              ★その実 DB の全列が突き合わせの分類表に載っているかを見る（載っていない列は
              黙って比較から漏れる）。

          th09_record --dump --main <tracker.sqlite3> --out <csharp.txt> [--limit N]
              ★分類表に従って、行を正規化して吐く。Python 側 parity_spec.py --dump と同じ形式。
              --limit は動作確認用で、合否の判定には使えない。

          th09_record --make-synthetic --out <synthetic.sqlite3>
              ★実データに 1 件も無い経路を通すための合成 DB を作る（一時フォルダで使うこと）。

          th09_record --make-synthetic-ticks --out <synthetic_ticks.txt>
              ★合成の「生 tick」を 1 本のファイルに書く。★両側がこれを読んで同じ列を流す
              （★C# 側が原本。Python 側は数え直さず、このファイルを読む）。

          th09_record --replay --out-dir <dir> [--corpus <corpus.txt>] [--ticks <ticks.txt>]
              ★★書く側の突き合わせ。★<dir> に scratch DB を作り、生 tick を状態機械へ流して
              Layer 1 を書き、その行を分類表の形で吐く。★実 DB には 1 バイトも書かない。
              --corpus  Layer 0 のどのセッションを流すかを書いた表（★Python 側が作る）。
              --ticks   --make-synthetic-ticks が書いた合成の生 tick。
              ★どちらも与えれば 1 つの scratch DB へ続けて流す（Layer 0 → 合成の順）。
              出すもの（<dir> の中）: scratch.sqlite3 / rows.txt / runs.txt / tombstones.txt
              ★名前は Python 側（parity/write_side.py --out-dir）と同じなので、
              ★両側で別のフォルダを渡すこと。

          th09_record --capture --out-dir <dir> --from-seq N --until-seq M [--name <共有メモリ名>]
                      [--seconds S] [--margin N]
              ★★ゲームからの捕捉。★動いている Tick Bus を★読み取り専用で開き、
              ★seq が [from, until] の tick だけを拾って状態機械へ流し、scratch DB を書く。
              ★★共有メモリには 1 バイトも書かない（作る口を持たない）。
              ★★ゲームに入力を送らない／起動しない／終了しない／フォアグラウンドを触らない。
              ★★範囲は「操縦する側」（parity/live_capture.py）が決めて両側へ同じ値を渡す。
              ★時刻で揃えない ——状態機械は前の tick に依存するので、入り口が 1 tick ずれると
              行が丸ごと変わる。
              --seconds は待ちの上限（既定 30）。★範囲が揃わなければ終了コード 1 で落ちる
              （★「一致」ではなく「測れていない」）。
              出すもの（<dir> の中）: scratch.sqlite3 / rows.txt / runs.txt / tombstones.txt
                                     / capture_ticks.txt（★拾った生の全語）
              ★名前は Python 側（parity/capture_side.py）と同じなので、★両側で別のフォルダを渡すこと。

          th09_record --live-source --name <共有メモリ名> --script <台本.tsv> --out <live.txt>
              ★★tick を読み続ける口（LiveTickSource）を台本どおりに動かして、
              ★出てきた Snapshot と申告（取りこぼし・千切れ・再同期）を吐く。
              ★★共有メモリは開くだけ（読み取り専用。作らない・書かない）。
              ★★ゲームには一切触らない（起動しない／終了しない／入力を送らない／探しに行かない）。
              ★★本物の Tick Bus の名前は受け付けない ——合成のリングを指すこと
              （動いているゲームを読みたいなら --capture）。
              ★台本は Python 側（csharp/tools/test_live_source.py）が書き、両側が同じものを読む。

          th09_record --hit-window-collect --coord-name <共有メモリ名> --script <台本.tsv>
                      --db <新しい layer0.sqlite3> --out <hitwin.txt>
              ★★被弾窓を集めて書く口（HitWindowCollector）を台本どおりに動かす。
              ★★座標リングは開くだけ（読み取り専用。作らない・書かない）。
              ★★ゲームには一切触らない（起動しない／終了しない／入力を送らない／探しに行かない）。
              ★★本物の座標リング／Tick Bus の名前は受け付けない ——合成のリングを指すこと。
              ★--db は「まだ無いファイル」を渡すこと（書き手のロック①。本物の Layer 0 は開けない）。
              ★台本は Python 側（csharp/tools/test_hit_window_collect.py）が書き、両側が同じものを読む。

          th09_record --capture-loop --script <台本.tsv> --out <capture.txt>
                      [--name <共有メモリ名>] [--coord-name <共有メモリ名>]
                      [--db <新しい tracker.sqlite3>] [--layer0 <新しい layer0.sqlite3>]
              ★★セッションを 1 つ捕捉するループ（CaptureLoop）を台本どおりに動かす。
              ★出すもの: 状態の語と閉じ方の表 / 走行 1 回ぶんの結果と健康値 / そのあとの DB の姿。
              ★★共有メモリは開くだけ（読み取り専用。作らない・書かない）。
              ★★ゲームには一切触らない（起動しない／終了しない／入力を送らない／探しに行かない）。
              ★★本物の Tick Bus ／座標リングの名前は受け付けない ——合成のリングを指すこと。
              ★--db と --layer0 は「まだ無いファイル」を渡すこと（本体 DB と本物の Layer 0 を
              行き先にできないため）。@run の無い台本なら、そのどれも要らない（純関数だけを吐く）。
              ★台本は Python 側（csharp/tools/test_capture_loop.py）が書き、両側が同じものを読む。

          th09_record --count-paths --layer0 <th09_ticks.db> [--out <counts.tsv>]
              ★★合成（--make-synthetic-ticks）が「実データに 1 件も無い経路」だと言うための
              ★数え上げ。Layer 0 を 1 度なめて、合成 5 件ぶんの条件に当たる tick を数える。
              ★数えられるのは 20 件のうち 5 件だけ（★1 語で決まる条件だけ）。
              ★★数えていないものを「0 件だった」と書かないこと。

          th09_record --scan-ledger --db <tracker.sqlite3> --script <台本.tsv>
          th09_record --session-delete --db <tracker.sqlite3> --script <台本.tsv>
                （★セッションの削除の台本。★本物の DB は開かずに断る）
          th09_record --session-restore --db <tracker.sqlite3> --layer0 <ticks.db|-> --session <id>[,<id>...] [--dry-run] [--no-tombstone]
                （★開発者向け。消したセッションを控え（tombstone）＋ Layer 0 の生tickから戻す。
                ★GUI には配線しない。★本物の本体 DB・Layer 0 のフォルダは開かずに断る。終了コード 3。
                --dry-run は読み取り専用で計画だけ吐く。--no-tombstone は控えの無い孤児も生tickだけから戻す）
          th09_record --history-plan --db <tracker.sqlite3> --keep-aborted <N> --keep-completed <M>
                （★履歴の整理の計画と保護の分類を吐く。★読むだけなので本物も指せる）
          th09_record --history-prune --db <tracker.sqlite3> --layer0 <ticks.db|-> --keep-aborted <N> --keep-completed <M>
                （★計画どおりに消す。★本物の本体 DB・Layer 0 のフォルダは開かずに断る。終了コード 3）
                      --writer-lock <名前> --scan-lock <名前>
              ★★走査の作業履歴（replay_scan_jobs / replay_scan_items）を書く口を動かす。
              ★★検査用の口。★本物のロックの名前は受け付けない（動いている監視・走査・
              watcher を実際に止めてしまうため）。★ゲームには一切触らない。
              終了コード: 0 = 書けた / 1 = 引数が変 / 3 = 先に居る書き手が居て開けなかった。

          th09_record --session-writer --db <tracker.sqlite3> --writer-lock <名前>
              ★★監視が本体 DB へ書く入口を、合成 DB と検査用の別名ロックで動かす口。
              ★本物の書き手ロック名は受け付けない。走査中の印は取らない。
              終了コード: 0 = 開けた / 1 = 引数が変 / 3 = 先に書き手が居る。

          th09_record --replay-register --db <tracker.sqlite3> --script <台本.tsv>
              ★★リプレイ登録の書き口（ReplayRegistrar）を台本どおりに動かす。
              ★書くのは replays / replay_paths / session_replays の 3 表だけ（sessions は読むだけ）。
              ★★ロックを 1 本も取らない（設計判断。原本の watcher も取らない）。
              ★★走査中の合図は「読む」だけで立てない。本物のロックの名前は受け付けない。
              ★★本物の本体 DB のフォルダは開かずに断る（合成の DB を渡すこと）。
              ★ゲームには一切触らない。
              終了コード: 0 = 流れた / 1 = 引数か台本が変 / 3 = 本物を指していた。

          th09_record --replay-stages --db <tracker.sqlite3> [--script <台本.tsv>]
          th09_record --replay-stages --json <台本.tsv>
              ★★面の数え方（ReplayStages）と突き合わせ（VerifySession）を動かす口。
              ★--db だけなら replays を全件なめて、1 本ごとに raw / p1 / 空面ありかを吐く。
              ★台本を渡すと、その行だけを流す（@case = decoded_json を直に食わせる /
              @verify = 合成 DB のセッションと突き合わせる。@verify には --db が要る）。
              ★★本体 DB は必ず読み取り専用で開く（1 バイトも書かない）。ゲームには一切触らない。

          th09_record --dump-settings <config.json>
              ★★設定画面が扱う鍵を型付きで読んで、値と★倒れ方（既定へ倒した・丸めた）を吐く。
              ★窓を開かない・DB を開かない・ゲームには一切触らない。
              ★★本物の config.json のフォルダは受け付けない（合成の設定を渡すこと）。
              終了コード: 0 = 読んだ / 3 = 本物を指していた。

          th09_record --write-settings <config.json> <鍵>=<値> …
              ★★検査用の書き口。★型付きで差し替えて、★扱う鍵だけを書き戻す
              （★他の鍵と並び順は 1 つも失わない。★読めない JSON なら書かずに断る）。
              ★遊び方のガードは hit_window_scopes.net=false のように 1 語ずつ。
              ★stats_hidden_items は | 区切り（空の字なら 0 件）。
              ★★上下限で丸めた値で書き戻す（★丸めた事実は note 行に出る）。
              ★★本物の config.json のフォルダは受け付けない。
              終了コード: 0 = 書けた / 1 = 引数が変 / 3 = 本物を指していた / 4 = 書かずに断った。

          th09_record --dump-self-bests --db <tracker.sqlite3> [--own-only 0|1] [--skip-scanned 0|1]
              ★★自己ベスト（未走査リプレイの区間スコア / 最終スコアの概算 / 区間ベストの表）を吐く。
              ★読むだけなので本物の本体 DB も指せる。ゲームには一切触らない。
              ★★既定（どちらも 1）だと実 DB では区間スコアが 0 行になる ——
              ★全部が走査済みだから。★中身を見たいときは --skip-scanned 0 を付ける。

          th09_record --dump-live-progress --db <tracker.sqlite3> [--own-only 0|1] [--sids 12,34]
              ★★ベスト比較（対戦の最長ラウンド／進行中のプレイと自己ベストの突き合わせ）を吐く。
              ★読むだけなので本物の本体 DB も指せる。ゲームには一切触らない。
              ★--sids を省くと「いちばん新しいセッション 1 本」＝ 原本の sid=None。

          th09_record --dump-stream-panel --db <tracker.sqlite3> [--config <config.json>]
                      [--own-only 0|1] [--sids 12,34]
              ★★配信パネルの塊（SB・PB・Target・WR の 4 数と差分）を吐く。
              ★読むだけなので本物の本体 DB も config.json も指せる。ゲームには一切触らない。
              ★--config を省くと Target / WR は 0 件で読む（母数の count 行に出る）。
              ★--sids を省くと「いちばん新しいセッション 1 本」＝ 原本の sid=None。

          th09_record --player-aliases-list --db <tracker.sqlite3>
              ★★プレイヤーの同一視（players / player_aliases）の候補を出す（原本
              tools/gen_player_aliases.py --list の移植）。★1 行も書き換えない。
              ★読むだけなので本物の本体 DB も指せる。ゲームには一切触らない。

          th09_record --player-aliases-write --db <tracker.sqlite3> --script <台本.tsv>
              ★★プレイヤーの同一視の書き口（seed / merge / self）を台本どおりに動かす
              （原本 gen_player_aliases.py の cmd_seed / cmd_merge / cmd_self の移植）。
              ★★本物の本体 DB のフォルダは受け付けない（合成の DB を渡してください）。
              ★誰と誰が同じ人かを決めるのは使う人 ——ここは道具だけ。ゲームには一切触らない。
              終了コード: 0 = 最後まで流れた / 1 = 引数か台本が変 / 3 = 本物を指していた。

          th09_record --dump-story-records --db <tracker.sqlite3>
              ★★Story / Extra のキャラ別記録（段階 7 E-17 のうち画面に依存しない材料）を吐く。
              ★プレイごとの「ミス数 / 最高残機 / 到達面 / 最終スコア」と、(Mode×キャラ) へ畳んだもの。
              ★Layer 0 はなめない ——ミス数・エクステンドは本体 DB の events 表を数えるだけ。
              ★★対 CPU の 16×16 制覇表はここに無い（Layer 0 を要るので対象外）。
              ★読むだけなので本物の本体 DB も指せる。ゲームには一切触らない。

          th09_record --dump-round-ranges <th09_ticks.db> [--session N]
              ★★段階 5 単位 8a。Layer 0 の valid tick を `(面, ラウンド)` の区間へ切って吐く
              （Layer 1 = round_metrics の生成に要る集計そのものはまだ無い。単位 8b の仕事）。
              ★--session を省くと Layer 0 に居る全セッション。
              ★出力は TSV 1 行 1 区間（session_id / stage_number / round_number / start / end）。
              ★読むだけ（Mode=ReadOnly）。本体 DB は開かない。ゲームには一切触らない。

          th09_record --dump-round-metrics <th09_ticks.db> --db <tracker.sqlite3> [--session N]
              ★★段階 5 単位 8b。round_ranges が切った区間から round_metrics の 36 列を作って吐く。
              ★--session を省くと Layer 0 に居る全セッション。
              ★出力は TSV 1 行 1 ラウンド（session_id / round_record_id / 36 列）。
              ★NULL は空文字列、0 は "0"（未計算の 6 列は常に空欄）。
              ★読むだけ（Layer 0・本体 DB とも ReadOnly）。round_metrics 表へは書かない。
              ★ゲームには一切触らない。

          th09_record --build-layer1 [--db <tracker.sqlite3>] [--layer0 <th09_ticks.db>]
                      [--check] [--all] [--session N]
                      [--writer-lock <名前> --scan-lock <名前>]
              ★★段階 5 単位 8c。Layer 1（round_metrics）を Layer 0 から作って本体 DB へ書く。
              ★本番の口（Python 側 tools/gen_round_metrics.py の移植）。
              ★--db / --layer0 を省くと本物の場所（--paths が出すもの）。★行き先は必ず 1 行目に出す。
              ★既定は差分（無い行と analysis_version が違う行だけ）。--all で全部、
                --session N でそのセッションだけ作り直す。★孤児（rounds に無い行）は毎回消す。
              ★--check は 1 バイトも書かずに「作る/作り直す/消す」を数えるだけ（ロックも取らない）。
              ★書く走り方は走査と同じロック 2 本を取る ——監視・走査と同時には走れない。
              ★--writer-lock / --scan-lock は検査用（別名。本物の名前は受け付けない。
                そのときは本物の本体 DB のフォルダも指せない）。
              ★ゲームには一切触らない。Layer 0 は読み取り専用で開く。

          th09_record --selftest
              ★自己検査。合成 ＋ 否定テスト。DB もゲームも触らない。
        """;

    public static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch (IOException) { }

        var opt = ParseArgs(args);
        if (args.Length == 0 || opt.ContainsKey("help") || opt.ContainsKey("h"))
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            if (opt.ContainsKey("selftest")) return SelfTest.Run();
            if (opt.ContainsKey("paths")) return RunPaths(opt);
            if (opt.ContainsKey("schema-shape")) return RunSchemaShape(opt);
            if (opt.ContainsKey("schema-check")) return RunSchemaCheck(opt);
            if (opt.ContainsKey("spec-check")) return RunSpecCheck(opt);
            if (opt.ContainsKey("dump")) return RunDump(opt);
            if (opt.ContainsKey("make-synthetic-ticks")) return RunMakeSyntheticTicks(opt);
            if (opt.ContainsKey("make-synthetic")) return RunMakeSynthetic(opt);
            if (opt.ContainsKey("replay")) return RunReplay(opt);
            if (opt.ContainsKey("capture")) return RunCapture(opt);
            if (opt.ContainsKey("live-source")) return RunLiveSource(opt);
            if (opt.ContainsKey("hit-window-collect")) return RunHitWindowCollect(opt);
            if (opt.ContainsKey("capture-loop")) return RunCaptureLoop(opt);
            if (opt.ContainsKey("count-paths")) return RunCountPaths(opt);
            if (opt.ContainsKey("session-writer")) return RunSessionWriter(opt);
            if (opt.ContainsKey("scan-ledger")) return RunScanLedger(opt);
            if (opt.ContainsKey("session-delete")) return RunSessionDelete(opt);
            if (opt.ContainsKey("session-restore")) return RunSessionRestore(opt);
            if (opt.ContainsKey("history-plan")) return RunHistoryPlan(opt, prune: false);
            if (opt.ContainsKey("history-prune")) return RunHistoryPlan(opt, prune: true);
            if (opt.ContainsKey("replay-register")) return RunReplayRegister(opt);
            if (opt.ContainsKey("dump-replay-watch"))
            {
                foreach (var line in Paths.ReplayWatchDumpLines(args)) Console.WriteLine(line);
                return 0;
            }
            if (opt.ContainsKey("replay-stages")) return RunReplayStages(opt);
            if (opt.ContainsKey("dump-self-bests")) return RunDumpSelfBests(opt);
            if (opt.ContainsKey("dump-live-progress")) return RunDumpLiveProgress(opt);
            if (opt.ContainsKey("dump-stream-panel")) return RunDumpStreamPanel(opt);
            if (opt.ContainsKey("dump-story-records")) return RunDumpStoryRecords(opt);
            if (opt.ContainsKey("player-aliases-list")) return RunPlayerAliasesList(opt);
            if (opt.ContainsKey("player-aliases-write")) return RunPlayerAliasesWrite(opt);
            if (opt.ContainsKey("dump-round-ranges")) return RunDumpRoundRanges(opt);
            if (opt.ContainsKey("dump-round-metrics")) return RunDumpRoundMetrics(opt);
            if (opt.ContainsKey("build-layer1")) return RunBuildLayer1(opt);
            if (opt.ContainsKey("dump-settings")) return RunDumpSettings(opt);
            if (opt.ContainsKey("write-settings")) return RunWriteSettings(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"落ちた: {e.GetType().Name}: {e.Message}");
            return 2;
        }

        Console.Error.WriteLine("引数が分かりません。--help を見てください。");
        return 1;
    }


    private static int RunPaths(Dictionary<string, string?> opt)
    {
        var paths = Paths.Default;
        var report = paths.Diagnose(probe: !opt.ContainsKey("no-probe"));
        foreach (var line in report.Lines) Console.WriteLine(line);
        return report.Severity == PathSeverity.Fatal ? 2 : 0;
    }


    private static int RunSchemaShape(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "out", out var outPath)) return 1;
        if (!string.Equals(Schema.Fingerprint(), RecordSchema.Sha256, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("★スキーマのフィンガープリントが生成物と違う。ビルドが古い可能性がある。");
            return 2;
        }
        var work = Path.Combine(Path.GetTempPath(), "th09_record_shape_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            using var db = Schema.CreateFrom(Path.Combine(work, "reference.sqlite3"));
            var shapes = Schema.Shapes(db);
            var lines = Schema.WriteShapes(shapes, outPath);
            Console.WriteLine($"書き出し: {outPath}");
            Console.WriteLine($"  {shapes.Count} 表 / {lines} 列（フィンガープリント {RecordSchema.Sha256[..12]}…）");
            return lines > 0 ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    private static int RunSchemaCheck(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "main", out var mainPath)) return 1;
        using var real = RecordDb.OpenReadOnly(mainPath);
        if (real is null)
        {
            Console.Error.WriteLine($"本体 DB を読み取り専用で開けません: {mainPath}");
            return 1;
        }
        var work = Path.Combine(Path.GetTempPath(), "th09_record_check_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            using var refDb = Schema.CreateFrom(Path.Combine(work, "reference.sqlite3"));
            var want = Schema.Shapes(refDb);
            var got = Schema.Shapes(real);
            var diff = Schema.Compare(want, got, Schema.PendingSet());
            Console.WriteLine($"実 DB {got.Count} 表 / {got.Sum(kv => kv.Value.Count)} 列、"
                              + $"定義元から組んだ DB {want.Count} 表 / {want.Sum(kv => kv.Value.Count)} 列");
            foreach (var m in diff.Pending) Console.WriteLine("  -- 移行待ち: " + m);
            foreach (var m in diff.Fatal) Console.WriteLine("  " + m);
            Console.WriteLine($"致命的な差 {diff.Fatal.Count} 件 / 移行待ち {diff.Pending.Count} 件");
            return diff.Fatal.Count == 0 ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    private static int RunSpecCheck(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "main", out var mainPath)) return 1;
        using var db = RecordDb.OpenReadOnly(mainPath);
        if (db is null)
        {
            Console.Error.WriteLine($"本体 DB を読み取り専用で開けません: {mainPath}");
            return 1;
        }
        var (missSpec, missDb, tables, columns) = ParityDump.CheckAgainstDb(db);
        Console.WriteLine($"実 DB: {tables} 表 {columns} 列");
        if (missSpec.Count > 0)
            Console.WriteLine($"  ★分類表に無い列 {missSpec.Count} 件: {string.Join(" ", missSpec.Take(30))}");
        if (missDb.Count > 0)
            Console.WriteLine($"  ★DB に無い列 {missDb.Count} 件: {string.Join(" ", missDb.Take(30))}");
        if (missSpec.Count == 0 && missDb.Count == 0) Console.WriteLine("  過不足なし");
        return missSpec.Count == 0 && missDb.Count == 0 ? 0 : 1;
    }


    private static int RunDump(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "main", out var mainPath) || !Require(opt, "out", out var outPath)) return 1;
        int? limit = opt.TryGetValue("limit", out var l) && l is not null
            ? int.Parse(l, CultureInfo.InvariantCulture) : null;
        using var db = RecordDb.OpenReadOnly(mainPath);
        if (db is null)
        {
            Console.Error.WriteLine($"本体 DB を読み取り専用で開けません: {mainPath}");
            return 1;
        }
        var stats = ParityDump.WriteDump(db, outPath, limit);
        Console.WriteLine($"書き出し: {outPath}");
        foreach (var line in ParityDump.HeaderLines(stats, db)) Console.WriteLine("  " + line);
        var withRows = stats.RowsPerTable.Count(x => x.Rows > 0);
        Console.WriteLine($"  # 合計 {stats.TotalRows} 行（{stats.Tables} 表のうち {withRows} 表が 1 行以上）");
        if (stats.CorpusMissing.Count > 0)
        {
            Console.Error.WriteLine("★コーパスの一部が DB に無い。母数が減っている。");
            return 1;
        }
        return stats.TotalRows > 0 ? 0 : 1;
    }

    private static int RunMakeSynthetic(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "out", out var outPath)) return 1;
        if (File.Exists(outPath)) File.Delete(outPath);
        var counts = Synthetic.Build(outPath);
        Console.WriteLine($"作った: {outPath}");
        foreach (var (table, rows) in counts) Console.WriteLine($"  {table}: {rows} 行");
        Console.WriteLine($"  合計 {counts.Sum(c => c.Rows)} 行 / {counts.Count} 表");
        return counts.Sum(c => c.Rows) > 0 ? 0 : 1;
    }

    private static int RunMakeSyntheticTicks(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "out", out var outPath)) return 1;
        var (cases, ticks) = SyntheticTicks.Write(outPath);
        Console.WriteLine($"書き出し: {outPath}");
        Console.WriteLine($"  {cases} 件 / {ticks} tick（{SyntheticTicks.FormatVersion}）");
        return cases > 0 && ticks > 0 ? 0 : 1;
    }


    private static int RunReplay(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "out-dir", out var dir)) return 1;
        opt.TryGetValue("corpus", out var corpusPath);
        opt.TryGetValue("ticks", out var ticksPath);
        if (corpusPath is null && ticksPath is null)
        {
            Console.Error.WriteLine("--corpus か --ticks のどちらかが要ります（両方でもよい）。");
            return 1;
        }
        Directory.CreateDirectory(dir);

        var corpus = corpusPath is null ? null : RecordCorpus.Load(corpusPath);
        using var db = ScratchRun.Create(Path.Combine(dir, "scratch.sqlite3"),
                                         corpus?.Layer0Path, Console.Error.WriteLine);

        var lines = new List<ScratchRun.Line>();
        if (corpus is not null)
        {
            using var layer0 = TickReplay.OpenLayer0(corpus.Layer0Path);
            lines.AddRange(ScratchRun.ReplayLayer0(db, layer0, corpus.Sessions));
        }
        if (ticksPath is not null)
        {
            lines.AddRange(ScratchRun.ReplaySynthetic(db, SyntheticTicks.Read(ticksPath)));
        }

        return WriteRunOutputs(db, dir, lines);
    }

    private static int WriteRunOutputs(RecordDb db, string dir, List<ScratchRun.Line> lines)
    {
        var rowsPath = Path.Combine(dir, "rows.txt");
        var stats = ParityDump.WriteDump(db, rowsPath, limit: null, useCorpus: false);
        ScratchRun.WriteLines(lines, Path.Combine(dir, "runs.txt"), "csharp");
        var (written, tombLines) =
            ScratchRun.DumpTombstones(db, Path.Combine(dir, "tombstones.txt"), ScratchRun.StartedAt);

        Console.WriteLine($"書き出し: {dir}");
        foreach (var line in ParityDump.HeaderLines(stats, db, useCorpus: false)) Console.WriteLine("  " + line);
        var withRows = stats.RowsPerTable.Count(x => x.Rows > 0);
        Console.WriteLine($"  # 流した {lines.Count} セッション / 読んだ {lines.Sum(x => x.TotalTicks)} tick"
                          + $" / 流した {lines.Sum(x => x.KeptTicks)} tick"
                          + $" / イベント {lines.Sum(x => x.Events)} 件");
        Console.WriteLine($"  # 書いた行 合計 {stats.TotalRows} 行（{stats.Tables} 表のうち {withRows} 表が 1 行以上）");
        Console.WriteLine($"  # 控え {written} 件を書き、{tombLines} 件を吐いた");
        return stats.TotalRows > 0 && lines.Count > 0 ? 0 : 1;
    }


    private static int RunCapture(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "out-dir", out var dir)) return 1;
        if (!RequireU32(opt, "from-seq", out var fromSeq)) return 1;
        if (!RequireU32(opt, "until-seq", out var untilSeq)) return 1;
        opt.TryGetValue("name", out var name);
        double seconds = opt.TryGetValue("seconds", out var s) && s is not null
            ? double.Parse(s, CultureInfo.InvariantCulture) : 30.0;
        int margin = opt.TryGetValue("margin", out var m) && m is not null
            ? int.Parse(m, CultureInfo.InvariantCulture) : TH09.TickBus.RingReader.DefaultMargin;

        Directory.CreateDirectory(dir);
        var scratchPath = Path.Combine(dir, "scratch.sqlite3");
        if (!GuardScratchPath(scratchPath)) return 2;

        var raws = new List<uint[]>();
        LiveCapture.CaptureResult cap;
        using (var bus = TH09.TickBus.TickBusReader.OpenVerified(name ?? TH09.TickBus.TickBusReader.DefaultName))
        {
            var header = bus.ReadHeader();
            Console.WriteLine($"  hook_state={header.HookState} / last_error={header.LastError}"
                              + $" / game_pid={header.GamePid} / write_index={header.WriteIndex}");
            cap = LiveCapture.Read(bus, fromSeq, untilSeq, seconds, margin, raws, Console.Error.WriteLine);
        }

        LiveCapture.WriteTicks(Path.Combine(dir, "capture_ticks.txt"), raws, cap);
        Console.WriteLine($"  ★母数: 要求 {cap.Requested} tick のうち {cap.Accepted} tick を拾った"
                          + $"（欠け {cap.Gaps} 件 / torn {cap.Torn} / 取りこぼし {cap.Lost}"
                          + $" / 再同期 {cap.GapEvents}）");
        Console.WriteLine($"  seq=[{cap.FromSeq}, {cap.UntilSeq}] / 開いた時の write_index={cap.HeadAtOpen}"
                          + $" / 待ち {cap.WaitedSeconds:F2} 秒 / 読み {cap.ReadSeconds:F2} 秒");
        if (cap.Late)
            Console.Error.WriteLine("  ★開いた時点で writer が from-seq を追い越していました"
                                    + "（★操縦する側の --lead を増やしてください）。");
        if (!cap.Complete)
        {
            Console.Error.WriteLine("  ★範囲を 1 tick も欠けずに拾えていません。"
                                    + "★これは『一致した』ではなく『測れていない』です。");
            return 1;
        }

        using var db = ScratchRun.Create(scratchPath, layer0Path: null, Console.Error.WriteLine);
        var lines = ScratchRun.ReplaySynthetic(db, [LiveCapture.ToCase(raws)]);
        return WriteRunOutputs(db, dir, lines);
    }

    private static bool GuardScratchPath(string scratchPath)
    {
        var target = Path.GetFullPath(scratchPath);
        var paths = Paths.Default;
        foreach (var (label, forbidden) in new[]
                 {
                     ("本体 DB", paths.MainDb),
                     ("Layer 0 の DB", paths.Layer0Db),
                 })
        {
            string full;
            try { full = Path.GetFullPath(forbidden); }
            catch (ArgumentException) { continue; }
            catch (IOException) { continue; }
            if (string.Equals(target, full, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"★{label}（{forbidden}）を scratch DB の行き先にはできません。"
                                        + "★捕捉は一時フォルダへ書いてください。");
                return false;
            }
        }
        return true;
    }


    private static int RunLiveSource(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "name", out var name)) return 1;
        if (!Require(opt, "script", out var script)) return 1;
        if (!Require(opt, "out", out var outPath)) return 1;

        foreach (var real in new[] { TH09.TickBus.TickBusReader.DefaultName,
                                     TH09.TickBus.CoordLayout.DefaultName })
        {
            if (!string.Equals(name, real, StringComparison.Ordinal)) continue;
            Console.Error.WriteLine(
                "★検査用の名前しか受け付けない（本物を指すと、いま動いているものへ"
                + "黙って相乗りする経路ができる）: " + real
                + "。★動いているゲームを読むなら --capture を使ってください。");
            return 1;
        }
        return LiveSourceScript.Run(name, script, outPath, Console.Error.WriteLine);
    }

    private static int RunHitWindowCollect(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "coord-name", out var name)) return 1;
        if (!Require(opt, "script", out var script)) return 1;
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "out", out var outPath)) return 1;

        foreach (var real in new[] { TH09.TickBus.CoordLayout.DefaultName,
                                     TH09.TickBus.TickBusReader.DefaultName })
        {
            if (!string.Equals(name, real, StringComparison.Ordinal)) continue;
            Console.Error.WriteLine(
                "★検査用の名前しか受け付けない（本物を指すと、いま動いているものへ"
                + "黙って相乗りする経路ができる）: " + real);
            return 1;
        }
        return HitWindowScript.Run(name, script, dbPath, outPath);
    }

    private static int RunCaptureLoop(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "script", out var script) || !Require(opt, "out", out var outPath)) return 1;
        opt.TryGetValue("name", out var name);
        opt.TryGetValue("coord-name", out var coordName);
        opt.TryGetValue("db", out var dbPath);
        opt.TryGetValue("layer0", out var layer0Path);

        foreach (var given in new[] { name, coordName })
        {
            if (given is null) continue;
            foreach (var real in new[] { TH09.TickBus.TickBusReader.DefaultName,
                                         TH09.TickBus.CoordLayout.DefaultName })
            {
                if (!string.Equals(given, real, StringComparison.Ordinal)) continue;
                Console.Error.WriteLine(
                    "★検査用の名前しか受け付けない（本物を指すと、いま動いているものへ"
                    + "黙って相乗りする経路ができる）: " + real);
                return 1;
            }
        }
        foreach (var (label, path) in new[] { ("--db", dbPath), ("--layer0", layer0Path) })
        {
            if (path is not null && File.Exists(path))
            {
                Console.Error.WriteLine($"★{label} は「まだ無いファイル」を渡してください"
                                        + "（既にあるものへは書きません）: " + path);
                return 1;
            }
        }
        return CaptureLoopScript.Run(name, coordName, script, dbPath, layer0Path,
                                     outPath, Console.Error.WriteLine);
    }

    private static int RunCountPaths(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "layer0", out var layer0Path)) return 1;
        opt.TryGetValue("out", out var outPath);
        using var layer0 = TickReplay.OpenLayer0(layer0Path);
        var (counts, ticks, valid, sessions) =
            PathCounts.Scan(layer0, null, m => Console.Error.WriteLine(m));

        var lines = new List<string>
        {
            "# th09-record-pathcount-v1",
            "# ★母数: Layer 0 の " + sessions.ToString(CultureInfo.InvariantCulture) + " セッション / "
              + ticks.ToString(CultureInfo.InvariantCulture) + " tick"
              + "（うち players+stats のフラグが立つ " + valid.ToString(CultureInfo.InvariantCulture) + " tick を数えた）",
            "# ★数えたのは合成 " + SyntheticTicks.All().Count.ToString(CultureInfo.InvariantCulture)
              + " 件のうち " + counts.Count.ToString(CultureInfo.InvariantCulture)
              + " 件（★1 語で決まる条件だけ。★残りを『0 件だった』と書かないこと）",
            "@name\thits\tsessions\tno_word\texample_sessions\twhy",
        };
        foreach (var c in counts)
            lines.Add(string.Join("\t", c.Name,
                                  c.Hits.ToString(CultureInfo.InvariantCulture),
                                  c.Sessions.ToString(CultureInfo.InvariantCulture),
                                  c.NoWord.ToString(CultureInfo.InvariantCulture),
                                  string.Join(",", c.Examples.Select(
                                      x => x.ToString(CultureInfo.InvariantCulture))),
                                  c.Why));
        foreach (var line in lines) Console.WriteLine(line);
        if (outPath is not null)
        {
            File.WriteAllText(outPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            Console.WriteLine("書き出し: " + outPath);
        }
        return valid > 0 ? 0 : 1;
    }


    private static int RunSessionWriter(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "writer-lock", out var writerLock)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(),
                                       new UTF8Encoding(false), 1 << 16);
        var rc = SessionWriterDump.Run(w, dbPath, writerLock);
        w.Flush();
        return rc;
    }

    private static int RunSessionDelete(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "script", out var scriptPath)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(),
                                       new UTF8Encoding(false), 1 << 16);
        int rc = SessionDeleteDump.Run(w, dbPath, scriptPath);
        w.Flush();
        return rc;
    }

    private static int RunSessionRestore(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "layer0", out var layer0Path)) return 1;
        if (!Require(opt, "session", out var sessionText)) return 1;
        var ids = new List<long>();
        foreach (var part in sessionText.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!long.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                Console.Error.WriteLine("--session は session_id をカンマ区切りで指してください: " + part);
                return 1;
            }
            ids.Add(id);
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        int rc = SessionRestoreDump.Run(w, dbPath, layer0Path, ids,
                                        dryRun: opt.ContainsKey("dry-run"),
                                        allowNoTombstone: opt.ContainsKey("no-tombstone"));
        w.Flush();
        return rc;
    }

    private static int RunHistoryPlan(Dictionary<string, string?> opt, bool prune)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "keep-aborted", out var keepA) || !Require(opt, "keep-completed", out var keepC)) return 1;
        if (!int.TryParse(keepA, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ka)
            || !int.TryParse(keepC, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kc))
        {
            Console.Error.WriteLine("--keep-aborted / --keep-completed は整数で指してください。");
            return 1;
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        int rc;
        if (prune)
        {
            if (!Require(opt, "layer0", out var layer0)) return 1;
            rc = HistoryPruneDump.RunPrune(w, dbPath, layer0 == HistoryPruneDump.NoLayer0 ? null : layer0, ka, kc);
        }
        else
        {
            rc = HistoryPruneDump.RunPlan(w, dbPath, ka, kc);
        }
        w.Flush();
        return rc;
    }

    private static int RunDumpSelfBests(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Flag01(opt, "own-only", out var ownOnly)) return 1;
        if (!Flag01(opt, "skip-scanned", out var skipScanned)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = SelfBestsDump.Run(w, dbPath, ownOnly, skipScanned);
        w.Flush();
        return rc;
    }

    private static int RunDumpStoryRecords(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = StoryRecordsDump.Run(w, dbPath);
        w.Flush();
        return rc;
    }

    private static int RunPlayerAliasesList(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = PlayerAliasesDump.List(w, dbPath);
        w.Flush();
        return rc;
    }

    private static int RunPlayerAliasesWrite(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "script", out var scriptPath)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = PlayerAliasesDump.Write(w, dbPath, scriptPath);
        w.Flush();
        return rc;
    }

    private static int RunDumpRoundRanges(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "dump-round-ranges", out var layer0Path)) return 1;
        long? onlySession = null;
        if (opt.TryGetValue("session", out var sessionText) && sessionText is not null)
        {
            if (!long.TryParse(sessionText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                               out var v))
            {
                Console.Error.WriteLine("--session は session_id の整数で指してください: " + sessionText);
                return 1;
            }
            onlySession = v;
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = RoundRangesDump.Run(w, layer0Path, onlySession);
        w.Flush();
        return rc;
    }

    private static int RunDumpRoundMetrics(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "dump-round-metrics", out var layer0Path)) return 1;
        if (!Require(opt, "db", out var dbPath)) return 1;
        long? onlySession = null;
        if (opt.TryGetValue("session", out var sessionText) && sessionText is not null)
        {
            if (!long.TryParse(sessionText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                               out var v))
            {
                Console.Error.WriteLine("--session は session_id の整数で指してください: " + sessionText);
                return 1;
            }
            onlySession = v;
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = RoundMetricsDump.Run(w, dbPath, layer0Path, onlySession);
        w.Flush();
        return rc;
    }

    private static int RunBuildLayer1(Dictionary<string, string?> opt)
    {
        long? onlySession = null;
        if (opt.TryGetValue("session", out var sessionText) && sessionText is not null)
        {
            if (!long.TryParse(sessionText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                               out var v))
            {
                Console.Error.WriteLine("--session は session_id の整数で指してください: " + sessionText);
                return 1;
            }
            onlySession = v;
        }
        var rebuildAll = opt.ContainsKey("all");
        var dbPath = opt.TryGetValue("db", out var d) && d is not null ? d : Paths.Default.MainDb;
        var layer0Path = opt.TryGetValue("layer0", out var l) && l is not null ? l : Paths.Default.Layer0Db;

        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        w.WriteLine("本体 DB : " + Path.GetFullPath(dbPath));
        w.WriteLine("Layer 0 : " + Path.GetFullPath(layer0Path));
        w.Flush();

        var hasWriterLock = opt.TryGetValue("writer-lock", out var writerLock) && writerLock is not null;
        var hasScanLock = opt.TryGetValue("scan-lock", out var scanLock) && scanLock is not null;
        if (hasWriterLock != hasScanLock)
        {
            Console.Error.WriteLine("--writer-lock と --scan-lock は両方そろえて指してください"
                                    + "（片方だけだと、もう片方が本物のロックになる）。");
            return 1;
        }
        if (hasWriterLock && RealDbGuard.InMainDbDir(dbPath))
        {
            Console.Error.WriteLine(
                "★別名のロック（＝検査の走り方）で、本物の本体 DB のフォルダは指せません: " + dbPath);
            return 1;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine("本体 DB がありません: " + dbPath);
            return 2;
        }
        if (opt.ContainsKey("check"))
        {
            var rc = Layer1Build.RunCheck(w, dbPath, onlySession, rebuildAll);
            w.Flush();
            return rc;
        }

        void Log(string m) { w.WriteLine(m); w.Flush(); }

        var ledger = hasWriterLock
            ? ScanLedger.OpenWithSpareLocks(dbPath, writerLock!, scanLock!, Log)
            : ScanLedger.Open(dbPath, Log);
        if (ledger is null)
        {
            Console.Error.WriteLine("排他のロックを取れませんでした（監視・走査と同時には走れません）。");
            return 1;
        }
        using (ledger)
        {
            Layer1Build.Run(ledger, layer0Path, onlySession, rebuildAll, Log);
        }
        w.Flush();
        return 0;
    }

    private static int RunDumpLiveProgress(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Flag01(opt, "own-only", out var ownOnly)) return 1;
        var sids = new List<long>();
        if (opt.TryGetValue("sids", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries
                                                 | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(part, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                                   out var v))
                {
                    Console.Error.WriteLine($"--sids は session_id をカンマで並べてください: {part}");
                    return 1;
                }
                sids.Add(v);
            }
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = LiveProgressDump.Run(w, dbPath, ownOnly, sids);
        w.Flush();
        return rc;
    }

    private static int RunDumpStreamPanel(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Flag01(opt, "own-only", out var ownOnly)) return 1;
        opt.TryGetValue("config", out var configPath);
        var sids = new List<long>();
        if (opt.TryGetValue("sids", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries
                                                 | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(part, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                                   out var v))
                {
                    Console.Error.WriteLine($"--sids は session_id をカンマで並べてください: {part}");
                    return 1;
                }
                sids.Add(v);
            }
        }
        using var w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        var rc = StreamPanelDump.Run(w, dbPath, configPath, ownOnly, sids);
        w.Flush();
        return rc;
    }

    private static bool Flag01(Dictionary<string, string?> opt, string key, out bool value)
    {
        value = true;
        if (!opt.TryGetValue(key, out var text) || text is null) return true;
        if (text is "1") { value = true; return true; }
        if (text is "0") { value = false; return true; }
        Console.Error.WriteLine($"--{key} は 0 か 1 で指してください: {text}");
        return false;
    }

    private static int RunReplayRegister(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "script", out var scriptPath)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(),
                                       new UTF8Encoding(false), 1 << 16);
        int rc = ReplayRegistrarDump.Run(w, dbPath, scriptPath);
        w.Flush();
        return rc;
    }

    private static int RunScanLedger(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "db", out var dbPath)) return 1;
        if (!Require(opt, "script", out var scriptPath)) return 1;
        if (!Require(opt, "writer-lock", out var writerLock)) return 1;
        if (!Require(opt, "scan-lock", out var scanLock)) return 1;

        foreach (var name in new[] { writerLock, scanLock })
        {
            if (!ScanLedger.IsRealLockName(name)) continue;
            Console.Error.WriteLine(
                "★検査用の名前しか受け付けない（本物のロックを握ると、"
                + "動いている監視・走査・watcher を実際に止めてしまう）: " + name);
            return 1;
        }

        var script = File.ReadAllLines(scriptPath);
        if (!File.Exists(dbPath))
        {
            using var fresh = Schema.CreateFrom(dbPath);
            PrepareFixtures(fresh.Connection, script);
        }

        var logs = new List<string>();
        using var ledger = ScanLedger.OpenWithLocks(dbPath, writerLock, scanLock, logs.Add);
        foreach (var line in logs) Console.WriteLine("log\t" + line);
        if (ledger is null)
        {
            Console.Error.WriteLine("★書き口を開けなかった（先に居る書き手が居る）");
            return 3;
        }

        Console.WriteLine("columns\t" + ScanLedger.ItemColumnCount.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("health\t" + ScanLedger.HealthColumns.Count.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("health_names\t" + string.Join(",", ScanLedger.HealthColumns));

        var jobId = 0L;
        foreach (var raw in script)
        {
            var f = raw.Split('\t');
            switch (f[0])
            {
                case "@job":
                    jobId = ledger.StartJob(Cell(f, 1), Cell(f, 2));
                    Console.WriteLine("job\t" + jobId.ToString(CultureInfo.InvariantCulture));
                    break;
                case "@item":
                {
                    var itemId = ledger.StartItem(
                        jobId, long.Parse(f[1], CultureInfo.InvariantCulture),
                        int.Parse(f[2], CultureInfo.InvariantCulture));
                    Console.WriteLine("item\t" + itemId.ToString(CultureInfo.InvariantCulture));
                    ledger.EndItem(itemId, new ScanItemResult(
                        f[3], Num(Cell(f, 4)), Cell(f, 5), Num(Cell(f, 6)), Cell(f, 7),
                        Health(Cell(f, 8))));
                    break;
                }
                case "@endjob":
                    ledger.EndJob(jobId, f[1]);
                    break;
                case "@reopen":
                {
                    using var again = ScanLedger.OpenWithLocks(dbPath, writerLock, scanLock, logs.Add);
                    Console.WriteLine("reopen\t" + (again is null ? "ng" : "ok"));
                    break;
                }
                default:
                    break;
            }
        }
        return 0;
    }

    private static void PrepareFixtures(SqliteConnection conn, IEnumerable<string> script)
    {
        foreach (var raw in script)
        {
            var f = raw.Split('\t');
            if (f.Length < 2) continue;
            if (f[0] == "@replay")
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO replays(replay_id,sha256,file_size,"
                                + "first_seen_at,last_seen_at) VALUES($0,$1,$2,$3,$4)";
                cmd.Parameters.AddWithValue("$0", long.Parse(f[1], CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$1", "fixture-" + f[1]);
                cmd.Parameters.AddWithValue("$2", 0L);
                cmd.Parameters.AddWithValue("$3", "fixture");
                cmd.Parameters.AddWithValue("$4", "fixture");
                cmd.ExecuteNonQuery();
            }
            else if (f[0] == "@session")
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO sessions(session_id,started_at,status)"
                                + " VALUES($0,$1,$2)";
                cmd.Parameters.AddWithValue("$0", long.Parse(f[1], CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$1", "fixture");
                cmd.Parameters.AddWithValue("$2", "fixture");
                cmd.ExecuteNonQuery();
            }
        }
    }

    private static string? Cell(string[] fields, int index) =>
        index < fields.Length && fields[index] != "~" ? fields[index] : null;

    private static long? Num(string? text) =>
        text is null ? null : long.Parse(text, CultureInfo.InvariantCulture);

    private static Dictionary<string, long?>? Health(string? text)
    {
        if (text is null) return null;
        var map = new Dictionary<string, long?>(StringComparer.Ordinal);
        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=');
            map[kv[0]] = kv[1] == "~" ? null : long.Parse(kv[1], CultureInfo.InvariantCulture);
        }
        return map;
    }



    private static int RunDumpSettings(Dictionary<string, string?> opt)
    {
        if (!Require(opt, "dump-settings", out var path)) return 1;
        using var w = new StreamWriter(Console.OpenStandardOutput(),
                                       new UTF8Encoding(false), 1 << 16);
        int rc = AppSettingsDump.Dump(w, path);
        w.Flush();
        return rc;
    }

    private static int RunWriteSettings(string[] args)
    {
        var at = Array.IndexOf(args, AppSettingsDump.WriteFlag);
        if (at < 0 || at + 1 >= args.Length)
        {
            Console.Error.WriteLine(
                AppSettingsDump.WriteFlag + " <config.json> <鍵>=<値> … の形で指してください。");
            return 1;
        }
        var assignments = args[(at + 2)..];
        using var w = new StreamWriter(Console.OpenStandardOutput(),
                                       new UTF8Encoding(false), 1 << 16);
        int rc = AppSettingsDump.Write(w, args[at + 1], assignments);
        w.Flush();
        return rc;
    }

    private static int RunReplayStages(Dictionary<string, string?> opt)
    {
        opt.TryGetValue("db", out var dbPath);
        opt.TryGetValue("script", out var scriptPath);
        opt.TryGetValue("json", out var jsonPath);
        var script = scriptPath ?? jsonPath;
        if (dbPath is null && script is null)
        {
            Console.Error.WriteLine("--db か --json のどちらかが要ります。--help を見てください。");
            return 1;
        }

        RecordDb? db = null;
        try
        {
            if (dbPath is not null)
            {
                db = RecordDb.OpenReadOnly(dbPath);
                if (db is null)
                {
                    Console.Error.WriteLine("★DB を読み取り専用で開けなかった: " + dbPath);
                    return 2;
                }
            }
            if (script is not null) return ReplayStagesScript(db, script);
            return ReplayStagesSweep(db!);
        }
        finally
        {
            db?.Dispose();
        }
    }

    private static int ReplayStagesSweep(RecordDb db)
    {
        var seen = 0;
        var empty = 0;
        var broken = 0;
        var rows = new List<(long Id, string? Json)>();
        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT {DbColumns.Replays.ReplayId},{DbColumns.Replays.DecodedJson}"
                            + $" FROM {DbColumns.Replays.Table} ORDER BY {DbColumns.Replays.ReplayId}";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                rows.Add((r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1)));
        }
        foreach (var (id, json) in rows)
        {
            seen++;
            var label = id.ToString(CultureInfo.InvariantCulture);
            if (!EmitStages(label, json, out var hasEmpty)) { broken++; continue; }
            if (hasEmpty) empty++;
        }
        Console.WriteLine("count\t" + seen.ToString(CultureInfo.InvariantCulture)
                          + "\t" + empty.ToString(CultureInfo.InvariantCulture)
                          + "\t" + broken.ToString(CultureInfo.InvariantCulture));
        return 0;
    }

    private static int ReplayStagesScript(RecordDb? db, string scriptPath)
    {
        var cases = 0;
        var verifies = 0;
        foreach (var raw in File.ReadAllLines(scriptPath))
        {
            if (raw.Length == 0) continue;
            var f = raw.Split('\t');
            switch (f[0])
            {
                case "@case":
                    cases++;
                    EmitStages(f[1], f.Length > 2 ? f[2] : null, out _);
                    break;
                case "@verify":
                {
                    verifies++;
                    var label = f[1];
                    if (db is null)
                    {
                        Console.Error.WriteLine("@verify には --db が要ります: " + label);
                        return 1;
                    }
                    try
                    {
                        var decoded = ReplayStages.Parse(f[3]);
                        var sid = long.Parse(f[2], CultureInfo.InvariantCulture);
                        var result = VerifySession.Run(db.Connection, sid, decoded);
                        Console.WriteLine("verify\t" + label + "\t" + result.Status);
                        foreach (var line in result.Lines)
                            Console.WriteLine("detail\t" + label + "\t" + line);
                        foreach (var line in VerifySession.CaptureSummary(db.Connection, sid))
                            Console.WriteLine("summary\t" + label + "\t" + line);
                    }
                    catch (Exception e) when (e is InvalidDataException or FormatException
                                                or OverflowException)
                    {
                        Console.WriteLine("error\t" + label + "\traised");
                    }
                    break;
                }
                default:
                    break;
            }
        }
        Console.WriteLine("count\t" + cases.ToString(CultureInfo.InvariantCulture)
                          + "\t" + verifies.ToString(CultureInfo.InvariantCulture));
        return 0;
    }

    private static bool EmitStages(string label, string? json, out bool hasEmpty)
    {
        hasEmpty = false;
        try
        {
            var raw = ReplayStages.RawP1Stages(json);
            var p1 = ReplayStages.P1Stages(json);
            hasEmpty = ReplayStages.HasEmptyStages(json);
            Console.WriteLine(string.Join('\t',
                "stages", label,
                raw.Count.ToString(CultureInfo.InvariantCulture),
                p1.Count.ToString(CultureInfo.InvariantCulture),
                hasEmpty ? "1" : "0",
                string.Join(',', raw.Select(s => s.Index.ToString(CultureInfo.InvariantCulture))),
                string.Join(',', p1.Select(s => s.Index.ToString(CultureInfo.InvariantCulture))),
                string.Join(',', p1.Select(s => s.Score is { } v ? CanonJson.Canon(v) : "~"))));
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or FormatException or OverflowException)
        {
            Console.WriteLine("error\t" + label + "\traised");
            return false;
        }
    }


    private static Dictionary<string, string?> ParseArgs(string[] args)
    {
        var opt = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal))
            {
                if (!a.StartsWith('-')) continue;
                opt[a.TrimStart('-')] = null;
                continue;
            }
            var key = a[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                opt[key] = args[++i];
            }
            else
            {
                opt[key] = null;
            }
        }
        return opt;
    }

    private static bool Require(Dictionary<string, string?> opt, string key, out string value)
    {
        if (opt.TryGetValue(key, out var v) && v is not null) { value = v; return true; }
        Console.Error.WriteLine($"--{key} が要ります。--help を見てください。");
        value = "";
        return false;
    }

    private static bool RequireU32(Dictionary<string, string?> opt, string key, out uint value)
    {
        value = 0;
        if (!Require(opt, key, out var text)) return false;
        if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)) return true;
        Console.Error.WriteLine($"--{key} は 0 以上の整数（u32）で指してください: {text}");
        return false;
    }
}
