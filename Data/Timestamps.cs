namespace growcery.Data;

public static class Timestamps
{
    // Postgres timestamptz stores microseconds; truncating keeps a value identical before and after a round trip.
    public static DateTimeOffset UtcNow()
    {
        var now = DateTimeOffset.UtcNow;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
