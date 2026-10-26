using System.Globalization;

namespace Growcery.Core.Domain;

// The user-editable part of a grocery list item, validated as a whole.
public sealed class ItemDetails
{
    public const int MaxNameLength = 100;
    public const int QuantityDecimalPlaces = 3;
    public const decimal MaxQuantity = 9_999_999.999m;

    private ItemDetails(string name, decimal quantity, UnitOfMeasure unit)
    {
        Name = name;
        Quantity = quantity;
        Unit = unit;
    }

    public string Name { get; }

    public decimal Quantity { get; }

    public UnitOfMeasure Unit { get; }

    public static Result<ItemDetails> Create(string? name, decimal? quantity, UnitOfMeasure? unit)
    {
        var errors = new ValidationErrors();

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            errors.Add("name", "Name is required.");
        }
        else if (trimmedName.Contains('\0'))
        {
            errors.Add("name", "Name must not contain null characters.");
        }
        else if (trimmedName.Length > MaxNameLength)
        {
            errors.Add("name", $"Name must be at most {MaxNameLength} characters.");
        }

        var resolvedQuantity = quantity ?? 1m;
        if (resolvedQuantity <= 0)
        {
            errors.Add("quantity", "Quantity must be greater than 0.");
        }
        else if (resolvedQuantity > MaxQuantity)
        {
            errors.Add("quantity", $"Quantity must be at most {MaxQuantity.ToString(CultureInfo.InvariantCulture)}.");
        }
        else if (decimal.Round(resolvedQuantity, QuantityDecimalPlaces) != resolvedQuantity)
        {
            errors.Add("quantity", $"Quantity must have at most {QuantityDecimalPlaces} decimal places.");
        }

        var resolvedUnit = unit ?? UnitOfMeasure.Each;
        if (!Enum.IsDefined(resolvedUnit))
        {
            errors.Add("unit", $"Unit must be one of: {string.Join(", ", Enum.GetNames<UnitOfMeasure>())}.");
        }

        return errors.Any
            ? Result<ItemDetails>.Invalid(errors.ToDictionary())
            : Result<ItemDetails>.Success(new ItemDetails(trimmedName, resolvedQuantity, resolvedUnit));
    }
}
