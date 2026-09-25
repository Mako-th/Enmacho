#nullable enable

namespace TH09.Generated;

internal static class GameAddresses
{
    public const int Count = 44;

    public const uint BattleBgmId = 0x004DC690u;
    public const uint CompletedRounds = 0x004A7E90u;
    public const uint Difficulty = 0x004A7EACu;
    public const uint FieldId = 0x004A7E84u;
    public const uint GameManager = 0x004A7D90u;
    public const uint GameMode = 0x004A7EA8u;
    public const uint GlobalState = 0x004A7EC4u;
    public const uint GuiPtr = 0x004A7E38u;
    public const uint HitDamageBase = 0x004A7E48u;
    public const uint InputMask = 0x004ACF34u;
    public const uint InternalRank = 0x004A7E44u;
    public const uint LilyCounter = 0x004A7E5Cu;
    public const uint P1BulletPtr = 0x004A7D98u;
    public const uint P1Character = 0x004A7DB0u;
    public const uint P1CpuLevel = 0x004A7DBCu;
    public const uint P1EnemyPtr = 0x004A7DA0u;
    public const uint P1GameFlags = 0x004A7DC4u;
    public const uint P1HumanCpu = 0x004A7DB8u;
    public const uint P1InputReplay = 0x004ACE44u;
    public const uint P1PlayerPtr = 0x004A7D94u;
    public const uint P1PreviousCharacter = 0x004A7DB4u;
    public const uint P1StatsPtr = 0x004A7DACu;
    public const uint P1Wins = 0x004A7E98u;
    public const uint P2BulletPtr = 0x004A7DD0u;
    public const uint P2Character = 0x004A7DE8u;
    public const uint P2CpuLevel = 0x004A7DF4u;
    public const uint P2EnemyPtr = 0x004A7DD8u;
    public const uint P2GameFlags = 0x004A7DFCu;
    public const uint P2HumanCpu = 0x004A7DF0u;
    public const uint P2InputReplay = 0x004ACED2u;
    public const uint P2PlayerPtr = 0x004A7DCCu;
    public const uint P2PreviousCharacter = 0x004A7DECu;
    public const uint P2StatsPtr = 0x004A7DE4u;
    public const uint P2Wins = 0x004A7E9Cu;
    public const uint PauseUsed = 0x004A7EC0u;
    public const uint RankInterval = 0x004A7E54u;
    public const uint RankMax = 0x004A7E58u;
    public const uint ReplayManagerPtr = 0x004A7E64u;
    public const uint RngState = 0x004ACE0Cu;
    public const uint RoundFramesA = 0x004A7E40u;
    public const uint RoundFramesB = 0x004A7E50u;
    public const uint RoundFramesC = 0x004A7E60u;
    public const uint RoundsRequired = 0x004A7E94u;
    public const uint StoryStageIndex = 0x004A7E8Cu;

    public static class Carried
    {
        public const int Count = 4;

        public static readonly (string Name, uint Address)[] Fields =
        [
            ("round_frames_b", RoundFramesB),
            ("round_frames_c", RoundFramesC),
            ("p1_previous_character", P1PreviousCharacter),
            ("p2_previous_character", P2PreviousCharacter),
        ];
    }

    public static class CpuSettings
    {
        public const uint TableBase = 0x004A0330u;
        public const int Stride = 0x1C;
        public const int MinLevel = 0;
        public const int MaxLevel = 70;
        public const int ColumnCount = 7;

        public static readonly int[] ColumnOffsets = [0x00, 0x04, 0x08, 0x0C, 0x10, 0x14, 0x18];

        public static readonly string[] Suffixes = ["quick_seconds_50", "quick_seconds_mid", "quick_seconds_05", "hit_seconds_50", "hit_seconds_mid", "hit_seconds_05", "damage_floor"];

        public static readonly string[] Fields = ["p1_quick_seconds_50", "p1_quick_seconds_mid", "p1_quick_seconds_05", "p1_hit_seconds_50", "p1_hit_seconds_mid", "p1_hit_seconds_05", "p1_damage_floor", "p2_quick_seconds_50", "p2_quick_seconds_mid", "p2_quick_seconds_05", "p2_hit_seconds_50", "p2_hit_seconds_mid", "p2_hit_seconds_05", "p2_damage_floor"];
    }
}
