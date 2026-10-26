namespace Growcery.Core.Domain;

// Gives a Person access to a GroceryList. All access checks go through membership.
public sealed class ListMember
{
    internal ListMember(Guid groceryListId, Guid personId, ListRole role, DateTimeOffset createdAt)
    {
        GroceryListId = groceryListId;
        PersonId = personId;
        Role = role;
        CreatedAt = createdAt;
    }

    // Used when a persistence layer rehydrates the entity.
    private ListMember()
    {
    }

    public Guid GroceryListId { get; private set; }

    public Guid PersonId { get; private set; }

    public ListRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
