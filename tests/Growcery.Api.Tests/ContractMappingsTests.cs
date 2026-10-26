using System.Text.Json;
using Growcery.Api.Contracts;

namespace Growcery.Api.Tests;

public class ContractMappingsTests
{
    // Postgres numeric(10,3) returns 2 as 2.000; responses must not depend on where the value came from.
    [Theory]
    [InlineData("2.000", "2")]
    [InlineData("0.125", "0.125")]
    [InlineData("1.500", "1.5")]
    public void FormatQuantity_RemovesTrailingZeros(string stored, string expectedJson)
    {
        var quantity = decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expectedJson, JsonSerializer.Serialize(ContractMappings.FormatQuantity(quantity)));
    }
}
