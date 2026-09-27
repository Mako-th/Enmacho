namespace TH09.Drive;

public interface IMenuView
{
    MenuState Read();

    bool CellOccupied(int cell);

    bool ReplayPlaying() => false;

    bool? TitleDemo() => null;
}

public interface IInputSink
{
    bool Tap(uint mask, int ticks = 0);

    bool SetHold(uint mask, uint fields = 0);

    bool ReleaseAll();

    bool LockInput { get; set; }

    bool Renew() => true;

    uint? HookState() => null;
}
