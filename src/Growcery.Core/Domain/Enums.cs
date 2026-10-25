namespace Growcery.Core.Domain;

// Standard units so quantities can later be compared with retailer pricing (spec 002 D14).
// Persisted and serialized by name, so new values can be appended safely.
public enum UnitOfMeasure
{
    Each,
    Gram,
    Kilogram,
    Millilitre,
    Litre,
    Pack,
}

public enum ListRole
{
    Owner,

    // Reserved for list sharing (spec 002 §7); nothing assigns it yet.
    Editor,
}
