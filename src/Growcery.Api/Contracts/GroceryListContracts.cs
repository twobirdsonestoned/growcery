using Growcery.Core.Domain;

namespace Growcery.Api.Contracts;

// HTTP request and response shapes. Validation rules live in Growcery.Core, not here.

public sealed record CreateGroceryListRequest(string? Name);

public sealed record UpdateGroceryListRequest(string? Name);

public sealed record CreateGroceryListItemRequest(string? Name, decimal? Quantity, UnitOfMeasure? Unit);

public sealed record UpdateGroceryListItemRequest(string? Name, decimal? Quantity, UnitOfMeasure? Unit, bool IsPurchased);

public sealed record GroceryListSummaryResponse(
    Guid Id, string Name, int ItemCount, int PurchasedCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record GroceryListResponse(
    Guid Id, string Name, IReadOnlyList<GroceryListItemResponse> Items, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record GroceryListItemResponse(
    Guid Id, Guid GroceryListId, string Name, decimal Quantity, UnitOfMeasure Unit, bool IsPurchased,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public static class ContractMappings
{
    public static GroceryListSummaryResponse ToResponse(this GroceryListSummary summary) =>
        new(summary.Id, summary.Name, summary.ItemCount, summary.PurchasedCount, summary.CreatedAt, summary.UpdatedAt);

    public static GroceryListResponse ToResponse(this GroceryList list) =>
        new(list.Id, list.Name,
            list.Items.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Select(ToResponse).ToList(),
            list.CreatedAt, list.UpdatedAt);

    public static GroceryListItemResponse ToResponse(this GroceryListItem item) =>
        new(item.Id, item.GroceryListId, item.Name, FormatQuantity(item.Quantity), item.Unit, item.IsPurchased, item.CreatedAt, item.UpdatedAt);

    // The database returns quantities with its column scale (2 comes back as 2.000). Removing trailing zeros keeps
    // a response the same whether the value was just saved or read back.
    public static decimal FormatQuantity(decimal quantity) => quantity / 1.000000000000000000000000000000000m;
}
