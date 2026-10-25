using System.Linq.Expressions;
using Growcery.Data.Entities;

namespace Growcery.Api.Models;

public sealed record GroceryListSummaryResponse(
    Guid Id, string Name, int ItemCount, int PurchasedCount,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt);

public sealed record GroceryListResponse(
    Guid Id, string Name, IReadOnlyList<GroceryListItemResponse> Items,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt);

public sealed record GroceryListItemResponse(
    Guid Id, Guid GroceryListId, string Name, decimal? Quantity, string? Unit,
    string? Notes, bool IsPurchased, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public static class GroceryListMappings
{
    // Used inside EF queries so summaries are computed in SQL rather than by loading items.
    public static readonly Expression<Func<GroceryList, GroceryListSummaryResponse>> ToSummary = list =>
        new GroceryListSummaryResponse(
            list.Id, list.Name, list.Items.Count, list.Items.Count(item => item.IsPurchased),
            list.CreatedAt, list.UpdatedAt, list.DeletedAt);

    public static readonly Expression<Func<GroceryListItem, GroceryListItemResponse>> ToItemResponse = item =>
        new GroceryListItemResponse(
            item.Id, item.GroceryListId, item.Name, item.Quantity, item.Unit,
            item.Notes, item.IsPurchased, item.CreatedAt, item.UpdatedAt);

    private static readonly Func<GroceryListItem, GroceryListItemResponse> ToItemResponseCompiled = ToItemResponse.Compile();

    public static GroceryListItemResponse ToResponse(this GroceryListItem item) => ToItemResponseCompiled(item);

    public static GroceryListResponse ToResponse(this GroceryList list) =>
        new(list.Id, list.Name,
            list.Items.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Select(ToItemResponseCompiled).ToList(),
            list.CreatedAt, list.UpdatedAt, list.DeletedAt);
}
