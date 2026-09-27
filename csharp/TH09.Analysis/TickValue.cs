using System.Globalization;

namespace TH09.Analysis;

public enum TickKind { Missing, Float, Int }

public readonly struct TickValue
{
    private readonly double _float;
    private readonly long _int;
    public TickKind Kind { get; }

    private TickValue(TickKind kind, double f, long i) { Kind = kind; _float = f; _int = i; }

    public static readonly TickValue Missing = new(TickKind.Missing, 0, 0);
    public static TickValue Float(double v) => new(TickKind.Float, v, 0);
    public static TickValue Int(long v) => new(TickKind.Int, 0, v);

    public bool IsMissing => Kind == TickKind.Missing;

    public double AsDouble => Kind switch
    {
        TickKind.Float => _float,
        TickKind.Int => _int,
        _ => throw new InvalidOperationException("欠測の値を数として読もうとした"),
    };

    public long AsLong => Kind switch
    {
        TickKind.Int => _int,
        TickKind.Float => throw new InvalidOperationException("float の語を整数として読もうとした"),
        _ => throw new InvalidOperationException("欠測の値を整数として読もうとした"),
    };

    public override string ToString() => Kind switch
    {
        TickKind.Missing => "-",
        TickKind.Float => _float.ToString("R", CultureInfo.InvariantCulture),
        _ => _int.ToString(CultureInfo.InvariantCulture),
    };
}
