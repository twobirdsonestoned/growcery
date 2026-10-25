using Growcery.Core.Domain;

namespace Growcery.Core.Tests.Domain;

public class ListNameTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 23, 59, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithBlankName_ReturnsDateBasedDefault(string? name)
    {
        var result = ListName.Resolve(name, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Shopping list 25 Oct 2026", result.Value);
    }

    [Fact]
    public void Resolve_WithName_ReturnsTrimmedName()
    {
        var result = ListName.Resolve("  Weekly shop ", Now);

        Assert.Equal("Weekly shop", result.Value);
    }

    [Fact]
    public void Resolve_WithNameOver100Characters_IsInvalidOnName()
    {
        var result = ListName.Resolve(new string('a', 101), Now);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.True(result.Errors.ContainsKey("name"));
    }
}
