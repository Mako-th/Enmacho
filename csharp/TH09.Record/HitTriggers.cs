using TH09.Record.Generated;
using TH09.TickBus;
using R = TH09.Generated.TickWords.Record;

namespace TH09.Record;

public static class HitTriggers
{
    public readonly record struct Signal(int Side, string Trigger);

    public static bool PlayersReadable(TickRecord rec) =>
        (rec.At(R.FlagsOffset) & HitWindowConst.FlagPlayersValid) != 0;

    public static bool RoundIsLive(TickRecord rec, TickRecord? prev)
    {
        if (prev is not TickRecord p) return false;
        return p.At(R.ResultStateOffset) == 0
               && rec.At(R.RoundFramesOffset) >= p.At(R.RoundFramesOffset)
               && rec.At(R.CompletedRoundsOffset) >= p.At(R.CompletedRoundsOffset)
               && rec.At(R.StageIndexOffset) == p.At(R.StageIndexOffset);
    }

    public static bool BombHeld(uint inputReplay) => (inputReplay & HitWindowConst.InputReplayBomb) != 0;

    public static bool RingAdvanced(TickRecord rec, int side) =>
        (rec.At(side == 1 ? R.P1HitListCountOffset : R.P2HitListCountOffset)
         & HitWindowConst.HitlistValid) != 0;

    public static List<Signal> HitsIn(TickRecord rec, TickRecord? prev, IReadOnlyList<string> triggers)
    {
        var outv = new List<Signal>();
        if (!PlayersReadable(rec)) return outv;
        bool prevOk = prev is TickRecord pr && PlayersReadable(pr);
        bool wantLife = triggers.Contains(HitWindowConst.TriggerLifeRaw, StringComparer.Ordinal);
        bool wantKind = triggers.Contains(HitWindowConst.TriggerHitKind, StringComparer.Ordinal);
        bool wantQuick = triggers.Contains(HitWindowConst.TriggerQuick, StringComparer.Ordinal);
        bool live = wantLife && prevOk && RoundIsLive(rec, prev);

        for (int side = 1; side <= 2; side++)
        {
            int lifeOff = side == 1 ? R.P1LifeRawOffset : R.P2LifeRawOffset;
            int kindOff = side == 1 ? R.P1HitKindOffset : R.P2HitKindOffset;
            int inputOff = side == 1 ? R.P1InputReplayOffset : R.P2InputReplayOffset;
            int gaugeOff = side == 1 ? R.P1GaugeOffset : R.P2GaugeOffset;

            if (wantLife && live && prev is TickRecord p1 && rec.At(lifeOff) < p1.At(lifeOff))
                outv.Add(new Signal(side, HitWindowConst.TriggerLifeRaw));
            if (wantKind && (rec.At(kindOff) & HitWindowConst.HitValid) != 0)
                outv.Add(new Signal(side, HitWindowConst.TriggerHitKind));
            if (wantQuick && prevOk && prev is TickRecord p2
                && BombHeld(rec.At(inputOff)) && rec.At(gaugeOff) < p2.At(gaugeOff))
                outv.Add(new Signal(side, HitWindowConst.TriggerQuick));
        }
        return outv;
    }
}
