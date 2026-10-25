namespace Growcery.Core.Domain;

// A user of Growcery as the business sees them. Linked to the login only by ExternalId, so the identity
// provider (ASP.NET Identity today) can change without touching the domain.
public sealed class Person
{
    public Person(string externalId, string displayName, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        ExternalId = externalId;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    // Used when a persistence layer rehydrates the entity.
    private Person()
    {
    }

    public Guid Id { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
