namespace growcery.Models;

public class GroceryList
{
    public Guid Id { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser? Owner { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<GroceryListItem> Items { get; set; } = new();
}
