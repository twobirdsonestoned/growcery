using Growcery.Core.Abstractions;
using Growcery.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Growcery.Data.Repositories;

public sealed class EfPersonRepository : IPersonRepository
{
    private readonly ApplicationDbContext _db;

    public EfPersonRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Person> GetOrCreateByExternalIdAsync(string externalId, string displayName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await _db.People.FirstOrDefaultAsync(person => person.ExternalId == externalId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new Person(externalId, displayName, now);
        _db.People.Add(created);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request created this Person first (two first calls at once); use theirs.
            _db.Entry(created).State = EntityState.Detached;
            return await _db.People.FirstAsync(person => person.ExternalId == externalId, cancellationToken);
        }
    }
}
