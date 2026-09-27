namespace TH09.Shell.Views;

internal static class ShellLayout
{

    public const double HitLeftWidth = 436;

    public const double HitRightMinWidth = 340;

    public const double HitRightGap = 20;

    public const double HitLeftGap = HitRightGap;

    public static Avalonia.Thickness BoardLeftMargin => new(HitLeftGap, 0, 0, 0);

    public const double HitBodySideMargin = 18 * 2;


    public static double HitBoardDefaultWidth =>
        ViewModels.HitWindowViewModel.BoardWidthAt(ViewModels.HitWindowOptions.ZoomDefault);

    public static double HitBoardMinHeight =>
        ViewModels.HitWindowViewModel.BoardHeightAt(ViewModels.HitWindowOptions.ZoomMin);


    public const double DriveBarSideMargin = 10 * 2;

    public const double DriveButtonGap = 8;

    public const double DriveMonitorWidth = 128;

    public const double DriveScanWidth = 76;

    public const double DriveSettingsWidth = 76;

    public const double DriveStreamWidth = 168;

    public const double DriveButtonGroupGap = 16;

    public static double DriveBarButtonsWidth =>
        DriveBarSideMargin + DriveMonitorWidth + DriveStreamWidth
        + DriveScanWidth + DriveSettingsWidth + DriveButtonGap * 2 + DriveButtonGroupGap;


    public const double ScrollBarWidth = 18;

    public static double HitBodyMinWidth =>
        HitBodySideMargin + HitLeftWidth + HitLeftGap + HitBoardDefaultWidth
        + HitRightGap + HitRightMinWidth + ScrollBarWidth;

    public static double MainMinWidth => Math.Max(HitBodyMinWidth, DriveBarButtonsWidth);

    public static double MainMinHeight => HitBoardMinHeight + HitChromeHeight;

    public const double HitChromeHeight = 260;
}
