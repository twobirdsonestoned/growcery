namespace Growcery.Core.Domain;

// Aggregate root: members and items are only changed through the list.
public sealed class GroceryList
{
    private readonly List<ListMember> _members = new();
    private readonly List<GroceryListItem> _items = new();

    // Used when a persistence layer rehydrates the entity.
    private GroceryList()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ListMember> Members => _members.AsReadOnly();

    public IReadOnlyCollection<GroceryListItem> Items => _items.AsReadOnly();

    // name must already be resolved through ListName.Resolve.
    public static GroceryList Create(string name, Person owner, DateTimeOffset now)
    {
        var list = new GroceryList
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        list._members.Add(new ListMember(list.Id, owner.Id, ListRole.Owner, now));
        return list;
    }

    public bool IsMember(Guid personId) => _members.Any(member => member.PersonId == personId);

    public bool IsOwner(Guid personId) =>
        _members.Any(member => member.PersonId == personId && member.Role == ListRole.Owner);

    public void AddMember(Person person, ListRole role, DateTimeOffset now)
    {
        if (!IsMember(person.Id))
        {
            _members.Add(new ListMember(Id, person.Id, role, now));
        }
    }

    public void Rename(string name, DateTimeOffset now)
    {
        Name = name;
        UpdatedAt = now;
    }

    public GroceryListItem? FindItem(Guid itemId) => _items.SingleOrDefault(item => item.Id == itemId);

    public GroceryListItem AddItem(ItemDetails details, DateTimeOffset now)
    {
        var item = new GroceryListItem(Id, details, now);
        _items.Add(item);
        UpdatedAt = now;
        return item;
    }

    public void UpdateItem(GroceryListItem item, ItemDetails details, bool isPurchased, DateTimeOffset now)
    {
        item.Update(details, isPurchased, now);
        UpdatedAt = now;
    }

    public void RemoveItem(GroceryListItem item, DateTimeOffset now)
    {
        _items.Remove(item);
        UpdatedAt = now;
    }
}

// Read model for list overviews, so counts can be computed without loading every item.
public sealed record GroceryListSummary(
    Guid Id, string Name, int ItemCount, int PurchasedCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
