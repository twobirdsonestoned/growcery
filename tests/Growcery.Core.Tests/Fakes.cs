using Growcery.Core.Abstractions;
using Growcery.Core.Domain;
using Growcery.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Growcery.Core.Tests;

// In-memory implementations of Core's interfaces, so business rules are tested without a database or HTTP.

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);

    public void Advance() => UtcNow = UtcNow.AddMinutes(1);
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public string ExternalId { get; set; } = "identity-user-1";

    public string DisplayName { get; set; } = "Thandi";
}

public sealed class FakePersonRepository : IPersonRepository
{
    public List<Person> People { get; } = new();

    public Task<Person> GetOrCreateByExternalIdAsync(string externalId, string displayName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var person = People.SingleOrDefault(p => p.ExternalId == externalId);
        if (person is null)
        {
            person = new Person(externalId, displayName, now);
            People.Add(person);
        }

        return Task.FromResult(person);
    }
}

public sealed class FakeGroceryListRepository : IGroceryListRepository
{
    public List<GroceryList> Lists { get; } = new();

    public Task<IReadOnlyList<GroceryListSummary>> GetSummariesForMemberAsync(Guid personId, CancellationToken cancellationToken)
    {
        IReadOnlyList<GroceryListSummary> summaries = Lists
            .Where(list => list.IsMember(personId))
            .OrderByDescending(list => list.UpdatedAt)
            .ThenBy(list => list.Id)
            .Select(list => new GroceryListSummary(
                list.Id, list.Name, list.Items.Count, list.Items.Count(item => item.IsPurchased), list.CreatedAt, list.UpdatedAt))
            .ToList();
        return Task.FromResult(summaries);
    }

    public Task<GroceryList?> GetForMemberAsync(Guid listId, Guid personId, CancellationToken cancellationToken) =>
        Task.FromResult(Lists.SingleOrDefault(list => list.Id == listId && list.IsMember(personId)));

    public Task<GroceryList?> GetForUpdateAsync(Guid listId, Guid personId, CancellationToken cancellationToken) =>
        GetForMemberAsync(listId, personId, cancellationToken);

    public void Add(GroceryList list) => Lists.Add(list);

    public void Remove(GroceryList list) => Lists.Remove(list);
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

public sealed class ServiceFixture
{
    public FakeClock Clock { get; } = new();
    public FakeCurrentUser CurrentUser { get; } = new();
    public FakePersonRepository People { get; } = new();
    public FakeGroceryListRepository Lists { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();

    public GroceryListService ListService() =>
        new(Lists, People, UnitOfWork, Clock, CurrentUser, NullLogger<GroceryListService>.Instance);

    public GroceryListItemService ItemService() =>
        new(Lists, People, UnitOfWork, Clock, CurrentUser, NullLogger<GroceryListItemService>.Instance);

    public void ActAs(string externalId, string displayName = "Someone")
    {
        CurrentUser.ExternalId = externalId;
        CurrentUser.DisplayName = displayName;
    }
}
