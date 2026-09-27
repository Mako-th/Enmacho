using System.Globalization;
using System.Runtime.Versioning;
using TH09.TickBus;

namespace TH09.Drive;

[SupportedOSPlatform("windows")]
internal static class InputProbe
{
    public const string Flag = "--probe-input";

    public static int Run(string[] args)
    {
        if (args.Length is not (4 or 5))
        {
            Console.Error.WriteLine(Flag + " <名前> <保持> <保持のfields> <叩くキー> [締め出し 0/1]"
                                    + " の形で呼んでください。");
            return 2;
        }
        string name = args[0];
        if (name.Length == 0)
        {
            Console.Error.WriteLine("名前が空です。");
            return 2;
        }
        if (string.Equals(name, TickBusReader.DefaultName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, CoordLayout.DefaultName, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("本物の Bus の名前は使えません（この口は検査用です）。");
            return 2;
        }
        if (!TryNumber(args[1], out uint hold) || !TryNumber(args[2], out uint fields)
            || !TryNumber(args[3], out uint key))
        {
            Console.Error.WriteLine("保持 / fields / キーは 10 進か 0x… で指してください。");
            return 2;
        }

        uint lockInput = 0u;
        if (args.Length == 5 && !TryNumber(args[4], out lockInput))
        {
            Console.Error.WriteLine("締め出しは 0 か 1 で指してください。");
            return 2;
        }

        using var channel = new TickBusChannel(name);
        var input = new TickBusInput(channel) { LockInput = lockInput != 0u };
        Console.WriteLine("name\t" + name);
        Console.WriteLine("lock_input\t" + (input.LockInput ? "1" : "0"));
        Console.WriteLine("tap_ticks\t" + input.TapTicks.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("hold_ticks\t" + input.HoldTicks.ToString(CultureInfo.InvariantCulture));

        int bad = 0;
        Step("set_hold", input.SetHold(hold, fields));
        Step("tap", input.Tap(key));
        if (input.LockInput) Step("drop_hold", input.SetHold(0));
        Step("renew", input.Renew());
        Step("release_all", input.ReleaseAll());
        Console.WriteLine("lock_input_after\t" + (input.LockInput ? "1" : "0"));
        Console.WriteLine("sent\t" + input.Sent.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("hook_state\t" + (input.HookState()?.ToString(CultureInfo.InvariantCulture) ?? ""));
        return bad == 0 ? 0 : 1;

        void Step(string label, bool acked)
        {
            if (!acked) bad++;
            Console.WriteLine("step\t" + label + "\t" + (acked ? "acked" : "no-ack"));
        }
    }

    private static bool TryNumber(string text, out uint value)
        => text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
