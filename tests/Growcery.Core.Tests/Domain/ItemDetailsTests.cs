using System.Globalization;
using Growcery.Core.Domain;

namespace Growcery.Core.Tests.Domain;

public class ItemDetailsTests
{
    [Fact]
    public void Create_WithNameOnly_DefaultsQuantityToOneAndUnitToEach()
    {
        var result = ItemDetails.Create("  Milk ", quantity: null, unit: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Milk", result.Value.Name);
        Assert.Equal(1m, result.Value.Quantity);
        Assert.Equal(UnitOfMeasure.Each, result.Value.Unit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_IsInvalidOnName(string? name)
    {
        var result = ItemDetails.Create(name, null, null);

        AssertInvalidOn(result, "name");
    }

    [Fact]
    public void Create_WithNameOver100Characters_IsInvalidOnName()
    {
        var result = ItemDetails.Create(new string('a', 101), null, null);

        AssertInvalidOn(result, "name");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.2345")]
    [InlineData("10000000")]
    public void Create_WithInvalidQuantity_IsInvalidOnQuantity(string quantity)
    {
        var result = ItemDetails.Create("Milk", decimal.Parse(quantity, CultureInfo.InvariantCulture), null);

        AssertInvalidOn(result, "quantity");
    }

    [Theory]
    [InlineData("0.125")]
    [InlineData("2.5")]
    [InlineData("9999999.999")]
    public void Create_WithValidQuantity_KeepsIt(string quantity)
    {
        var expected = decimal.Parse(quantity, CultureInfo.InvariantCulture);

        var result = ItemDetails.Create("Cheese", expected, UnitOfMeasure.Kilogram);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Quantity);
        Assert.Equal(UnitOfMeasure.Kilogram, result.Value.Unit);
    }

    [Fact]
    public void Create_WithUndefinedUnit_IsInvalidOnUnit()
    {
        var result = ItemDetails.Create("Milk", null, (UnitOfMeasure)99);

        AssertInvalidOn(result, "unit");
    }

    [Fact]
    public void Create_WithSeveralProblems_ReportsEachField()
    {
        var result = ItemDetails.Create(" ", 0, (UnitOfMeasure)99);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(["name", "quantity", "unit"], result.Errors.Keys.Order());
    }

    private static void AssertInvalidOn<T>(Result<T> result, string field)
    {
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.True(result.Errors.ContainsKey(field), $"Expected an error on '{field}'.");
    }
}
