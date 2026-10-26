using Growcery.Core.Abstractions;
using Growcery.Core.Domain;
using Microsoft.Extensions.Logging;

namespace Growcery.Core.Services;

public interface IGroceryListService
{
    Task<IReadOnlyList<GroceryListSummary>> GetListsAsync(CancellationToken cancellationToken = default);

    Task<Result<GroceryList>> GetListAsync(Guid listId, CancellationToken cancellationToken = default);

    Task<Result<GroceryList>> CreateListAsync(string? name, CancellationToken cancellationToken = default);

    Task<Result<GroceryList>> RenameListAsync(Guid listId, string? name, CancellationToken cancellationToken = default);

    Task<Result> DeleteListAsync(Guid listId, CancellationToken cancellationToken = default);
}

public sealed class GroceryListService : IGroceryListService
{
    private readonly IGroceryListRepository _lists;
    private readonly IPersonRepository _people;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<GroceryListService> _logger;

    public GroceryListService(
        IGroceryListRepository lists,
        IPersonRepository people,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        ILogger<GroceryListService> logger)
    {
        _lists = lists;
        _people = people;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GroceryListSummary>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        return await _lists.GetSummariesForMemberAsync(person.Id, cancellationToken);
    }

    public async Task<Result<GroceryList>> GetListAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var list = await _lists.GetForMemberAsync(listId, person.Id, cancellationToken);
        return list is null ? Result<GroceryList>.NotFound() : Result<GroceryList>.Success(list);
    }

    public async Task<Result<GroceryList>> CreateListAsync(string? name, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var resolvedName = ListName.Resolve(name, now);
        if (!resolvedName.IsSuccess)
        {
            return Result<GroceryList>.FailureFrom(resolvedName);
        }

        var person = await CurrentPersonAsync(cancellationToken);
        var list = GroceryList.Create(resolvedName.Value, person, now);
        _lists.Add(list);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Person {PersonId} created grocery list {GroceryListId}", person.Id, list.Id);
        return Result<GroceryList>.Success(list);
    }

    public async Task<Result<GroceryList>> RenameListAsync(Guid listId, string? name, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var list = await _lists.GetForUpdateAsync(listId, person.Id, cancellationToken);
        if (list is null)
        {
            return Result<GroceryList>.NotFound();
        }

        if (!list.IsOwner(person.Id))
        {
            return Result<GroceryList>.Forbidden();
        }

        var now = _clock.UtcNow;
        var resolvedName = ListName.Resolve(name, now);
        if (!resolvedName.IsSuccess)
        {
            return Result<GroceryList>.FailureFrom(resolvedName);
        }

        list.Rename(resolvedName.Value, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Person {PersonId} renamed grocery list {GroceryListId}", person.Id, list.Id);
        return Result<GroceryList>.Success(list);
    }

    public async Task<Result> DeleteListAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var list = await _lists.GetForUpdateAsync(listId, person.Id, cancellationToken);
        if (list is null)
        {
            return Result.NotFound();
        }

        if (!list.IsOwner(person.Id))
        {
            return Result.Forbidden();
        }

        _lists.Remove(list);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Person {PersonId} deleted grocery list {GroceryListId}", person.Id, list.Id);
        return Result.Success();
    }

    private Task<Person> CurrentPersonAsync(CancellationToken cancellationToken) =>
        _people.GetOrCreateByExternalIdAsync(_currentUser.ExternalId, _currentUser.DisplayName, _clock.UtcNow, cancellationToken);
}
