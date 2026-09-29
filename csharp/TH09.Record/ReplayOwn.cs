namespace TH09.Record;

public static class ReplayOwn
{
    public static (bool Own, int? Side) Of(long? ownOverride, long? ownerSide)
    {
        var v = ownOverride ?? ownerSide;
        return (v is 1 or 2 or ReplayOwnership.OwnUnknownSide,
                v is 1 or 2 ? (int)v.Value : null);
    }
}
