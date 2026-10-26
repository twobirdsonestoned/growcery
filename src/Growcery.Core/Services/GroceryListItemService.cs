using Growcery.Core.Abstractions;
using Growcery.Core.Domain;
using Microsoft.Extensions.Logging;

namespace Growcery.Core.Services;

public sealed record ItemInput(string? Name, decimal? Quantity, UnitOfMeasure? Unit);

public interface IGroceryListItemService
{
    Task<Result<IReadOnlyList<GroceryListItem>>> GetItemsAsync(Guid listId, CancellationToken cancellationToken = default);

    Task<Result<GroceryListItem>> GetItemAsync(Guid listId, Guid itemId, CancellationToken cancellationToken = default);

    Task<Result<GroceryListItem>> AddItemAsync(Guid listId, ItemInput input, CancellationToken cancellationToken = default);

    Task<Result<GroceryListItem>> UpdateItemAsync(Guid listId, Guid itemId, ItemInput input, bool isPurchased, CancellationToken cancellationToken = default);

    Task<Result> RemoveItemAsync(Guid listId, Guid itemId, CancellationToken cancellationToken = default);
}

public sealed class GroceryListItemService : IGroceryListItemService
{
    private readonly IGroceryListRepository _lists;
    private readonly IPersonRepository _people;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<GroceryListItemService> _logger;

    public GroceryListItemService(
        IGroceryListRepository lists,
        IPersonRepository people,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        ILogger<GroceryListItemService> logger)
    {
        _lists = lists;
        _people = people;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<GroceryListItem>>> GetItemsAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var list = await _lists.GetForMemberAsync(listId, person.Id, cancellationToken);
        if (list is null)
        {
            return Result<IReadOnlyList<GroceryListItem>>.NotFound();
        }

        IReadOnlyList<GroceryListItem> items = list.Items.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToList();
        return Result<IReadOnlyList<GroceryListItem>>.Success(items);
    }

    public async Task<Result<GroceryListItem>> GetItemAsync(Guid listId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var item = (await _lists.GetForMemberAsync(listId, person.Id, cancellationToken))?.FindItem(itemId);
        return item is null ? Result<GroceryListItem>.NotFound() : Result<GroceryListItem>.Success(item);
    }

    public async Task<Result<GroceryListItem>> AddItemAsync(Guid listId, ItemInput input, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var list = await _lists.GetForUpdateAsync(listId, person.Id, cancellationToken);
        if (list is null)
        {
            return Result<GroceryListItem>.NotFound();
        }

        var details = ItemDetails.Create(input.Name, input.Quantity, input.Unit);
        if (!details.IsSuccess)
        {
            return Result<GroceryListItem>.FailureFrom(details);
        }

        var item = list.AddItem(details.Value, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Person {PersonId} added item {GroceryListItemId} to grocery list {GroceryListId}", person.Id, item.Id, list.Id);
        return Result<GroceryListItem>.Success(item);
    }

    public async Task<Result<GroceryListItem>> UpdateItemAsync(Guid listId, Guid itemId, ItemInput input, bool isPurchased, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var list = await _lists.GetForUpdateAsync(listId, person.Id, cancellationToken);
        var item = list?.FindItem(itemId);
        if (list is null || item is null)
        {
            return Result<GroceryListItem>.NotFound();
        }

        var details = ItemDetails.Create(input.Name, input.Quantity, input.Unit);
        if (!details.IsSuccess)
        {
            return Result<GroceryListItem>.FailureFrom(details);
        }

        list.UpdateItem(item, details.Value, isPurchased, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Person {PersonId} updated item {GroceryListItemId} in grocery list {GroceryListId}", person.Id, item.Id, list.Id);
        return Result<GroceryListItem>.Success(item);
    }

    public async Task<Result> RemoveItemAsync(Guid listId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var person = await CurrentPersonAsync(cancellationToken);
        var list = await _lists.GetForUpdateAsync(listId, person.Id, cancellationToken);
        var item = list?.FindItem(itemId);
        if (list is null || item is null)
        {
            return Result.NotFound();
        }

        list.RemoveItem(item, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Person {PersonId} removed item {GroceryListItemId} from grocery list {GroceryListId}", person.Id, itemId, list.Id);
        return Result.Success();
    }

    private Task<Person> CurrentPersonAsync(CancellationToken cancellationToken) =>
        _people.GetOrCreateByExternalIdAsync(_currentUser.ExternalId, _currentUser.DisplayName, _clock.UtcNow, cancellationToken);
}
