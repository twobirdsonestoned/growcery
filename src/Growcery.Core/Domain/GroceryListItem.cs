namespace Growcery.Core.Domain;

// A line on a grocery list. Changed only through its GroceryList, which keeps the list's UpdatedAt current.
public sealed class GroceryListItem
{
    internal GroceryListItem(Guid groceryListId, ItemDetails details, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        GroceryListId = groceryListId;
        Apply(details);
        CreatedAt = now;
        UpdatedAt = now;
    }

    // Used when a persistence layer rehydrates the entity.
    private GroceryListItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroceryListId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public UnitOfMeasure Unit { get; private set; }

    public bool IsPurchased { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Update(ItemDetails details, bool isPurchased, DateTimeOffset now)
    {
        Apply(details);
        IsPurchased = isPurchased;
        UpdatedAt = now;
    }

    private void Apply(ItemDetails details)
    {
        Name = details.Name;
        Quantity = details.Quantity;
        Unit = details.Unit;
    }
}
