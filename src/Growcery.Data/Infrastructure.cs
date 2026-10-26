using Growcery.Core.Abstractions;

namespace Growcery.Data;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;

    public EfUnitOfWork(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}

// Postgres timestamptz stores microseconds; truncating keeps a value identical before and after a round trip.
public sealed class PostgresClock : IClock
{
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        }
    }
}
