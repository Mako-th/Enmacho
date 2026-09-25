#nullable enable

namespace TH09.Generated;

internal static class MenuMapLayout
{
    public const int ConstantCount = 48;
    public const int RegionCount = 9;

    public const uint MainMenuPtr = 0x004AC8C0u;
    public const uint AReplayPath = 0x004A7ED5u;

    public const int OffCursor = 0x00000;
    public const int OffSubstate = 0x00028;
    public const int OffPrevScreen = 0x00084;
    public const int OffSlotPtrs = 0x0681C;
    public const int OffSlotHeader = 0x068E4;
    public const int OffChosenCell = 0x0C908;
    public const int OffScreenId = 0x1B228;
    public const int OffAnimStep = 0x1B230;
    public const int OffTransitioning = 0x1B238;
    public const int OffReplayPlaybackFlag = 0x00114;
    public const int OffStageOffsets = 0x00020;

    public const int ScreenTitle = 1;
    public const int ScreenReplayList = 11;
    public const int ScreenMusicRoom = 12;
    public const int ScreenCharSelect = 13;
    public const int ScreenMin = 1;
    public const int ScreenMax = 16;

    public const int TitleItemReplay = 3;
    public const int TitleCursorMax = 15;

    public const int SubEntering = 0;
    public const int SubReady = 1;
    public const int SubDetail = 2;
    public const int SubMax = 2;

    public const uint GsTitleDemo = 0x0002u;
    public const uint GsInGameSticky = 0x0004u;
    public const uint GsReplayPlaying = 0x0008u;
    public const uint GsFreezeP1 = 0x0800u;
    public const uint GsFreezeP2 = 0x1000u;
    public const uint GsContinued = 0x2000u;
    public const uint GsContinuedLevelFreeze = 0x4000u;

    public const uint GfTimeStop = 0x0001u;

    public const uint BitOk = 0x0001u;
    public const uint BitCancel = 0x0008u;
    public const uint BitUp = 0x0010u;
    public const uint BitDown = 0x0020u;
    public const uint BitLeft = 0x0040u;
    public const uint BitRight = 0x0080u;
    public const uint BitSkip = 0x0100u;

    public const int CellCount = 50;
    public const int NumberedCells = 25;
    public const int SlotHeaderStride = 492;
    public const int LateralStep = 25;
    public const int ReplayPathMax = 64;
    public const uint PtrMin = 0x00010000u;
    public const uint PtrMax = 0x7FFE0000u;
    public const int StageSlots = 10;

    public static readonly (int Offset, string Name, int Size)[] Regions =
    [
        (0x00000, "cursor", 4),
        (0x00028, "substate", 4),
        (0x00084, "prev_screen", 4),
        (0x0681C, "slot_ptrs", 200),
        (0x068E4, "slot_header", 24600),
        (0x0C908, "chosen_cell", 4),
        (0x1B228, "screen_id", 4),
        (0x1B230, "anim_step", 4),
        (0x1B238, "transitioning", 4),
    ];

    public static readonly (int Id, string Name)[] ScreenNames =
    [
        (1, "タイトル"),
        (11, "リプレイ選択"),
        (12, "Music Room"),
        (13, "キャラ選択"),
    ];

    public static readonly (int Id, string Name)[] SubstateNames =
    [
        (0, "入場アニメ"),
        (1, "操作可能"),
        (2, "概要"),
    ];

    public static readonly (uint Bit, string Name)[] BitNames =
    [
        (0x0001u, "OK"),
        (0x0008u, "X"),
        (0x0010u, "上"),
        (0x0020u, "下"),
        (0x0040u, "左"),
        (0x0080u, "右"),
        (0x0100u, "SKIP"),
    ];

    public static readonly (uint Bit, string Name)[] GlobalStateBitNames =
    [
        (0x0002u, "タイトルデモ"),
        (0x0004u, "本編sticky"),
        (0x0008u, "リプレイ再生中"),
        (0x0800u, "1Pの陣で演出中"),
        (0x1000u, "2Pの陣で演出中"),
        (0x2000u, "コンティニュー済"),
        (0x4000u, "CPUレベル据え置き"),
    ];

    public static readonly (uint Bit, string Name)[] GameFlagsBitNames =
    [
        (0x0001u, "時止め"),
    ];

    public const string DemoPathMark = "demo/";
}
