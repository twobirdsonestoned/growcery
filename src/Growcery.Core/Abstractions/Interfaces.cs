using Growcery.Core.Domain;

namespace Growcery.Core.Abstractions;

// Implemented by the data layer (Growcery.Data today). Core only ever sees these interfaces.

public interface IGroceryListRepository
{
    Task<IReadOnlyList<GroceryListSummary>> GetSummariesForMemberAsync(Guid personId, CancellationToken cancellationToken);

    // Read-only: the returned list must not be modified. Null when it doesn't exist or the person isn't a member.
    Task<GroceryList?> GetForMemberAsync(Guid listId, Guid personId, CancellationToken cancellationToken);

    // Like GetForMemberAsync, but changes to the returned list are saved by IUnitOfWork.SaveChangesAsync.
    Task<GroceryList?> GetForUpdateAsync(Guid listId, Guid personId, CancellationToken cancellationToken);

    void Add(GroceryList list);

    void Remove(GroceryList list);
}

public interface IPersonRepository
{
    // Returns the Person for this login, creating and saving it on first use.
    Task<Person> GetOrCreateByExternalIdAsync(string externalId, string displayName, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

// The signed-in user, as provided by the presentation layer.
public interface ICurrentUser
{
    string ExternalId { get; }

    string DisplayName { get; }
}
