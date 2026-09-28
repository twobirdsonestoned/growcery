namespace growcery.Models;

public class GroceryListItem
{
    public Guid Id { get; set; }

    public Guid GroceryListId { get; set; }

    public GroceryList? GroceryList { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    public string? Unit { get; set; }

    public string? Notes { get; set; }

    public bool IsPurchased { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
