using System.Globalization;
using Growcery.Api.Models;
using Growcery.Data.Entities;
using Growcery.Data.Identity;

namespace Growcery.Api.Tests;

public class MaxDecimalPlacesAttributeTests
{
    private readonly MaxDecimalPlacesAttribute _attribute = new(2);

    [Fact]
    public void IsValid_WithNull_ReturnsTrue()
    {
        Assert.True(_attribute.IsValid(null));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.25")]
    [InlineData("1.250")]
    public void IsValid_WithAtMostTwoDecimalPlaces_ReturnsTrue(string value)
    {
        Assert.True(_attribute.IsValid(decimal.Parse(value, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("1.251")]
    [InlineData("0.001")]
    public void IsValid_WithMoreThanTwoDecimalPlaces_ReturnsFalse(string value)
    {
        Assert.False(_attribute.IsValid(decimal.Parse(value, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void FormatErrorMessage_IncludesFieldNameAndPlaces()
    {
        Assert.Equal("Quantity must have at most 2 decimal places.", _attribute.FormatErrorMessage("Quantity"));
    }
}
