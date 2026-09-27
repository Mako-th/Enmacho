using TH09.Record.Generated;

namespace TH09.Record;

public static class NewGameRule
{
    public const long MaxRoundFrames = 180;

    public const long MaxStoryStageIndex = 8;

    public const long MatchStageIndex = 9;

    public static bool IsNewGame(Snapshot s) =>
        HasKey(RecordLabels.Modes, s.Mode)
        && HasKey(RecordLabels.Difficulties, s.Difficulty)
        && s.P1Character < RecordLabels.Characters.Length
        && s.P2Character < RecordLabels.Characters.Length
        && s.CompletedRounds == 0 && s.P1Wins == 0 && s.P2Wins == 0
        && s.RoundFrames <= MaxRoundFrames
        && ((s.Mode is 0 or 1 && s.StageIndex <= MaxStoryStageIndex && s.RoundsRequired == 1)
            || (s.Mode == 2 && s.StageIndex == MatchStageIndex && s.RoundsRequired == 2));

    private static bool HasKey(System.Collections.Frozen.FrozenDictionary<int, string> map, long key)
    {
        var k = unchecked((int)key);
        return k == key && map.ContainsKey(k);
    }
}
