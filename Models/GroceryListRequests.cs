using System.ComponentModel.DataAnnotations;

namespace growcery.Models;

public class CreateGroceryListRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class UpdateGroceryListRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class CreateGroceryListItemRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    [MaxDecimalPlaces(2)]
    public decimal? Quantity { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class UpdateGroceryListItemRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    [MaxDecimalPlaces(2)]
    public decimal? Quantity { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsPurchased { get; set; }
}
