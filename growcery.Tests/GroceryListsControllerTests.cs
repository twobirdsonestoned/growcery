using System.Net;
using System.Net.Http.Json;
using growcery.Models;

namespace growcery.Tests;

public class GroceryListsControllerTests : IClassFixture<GrowceryWebApplicationFactory>
{
    private const string ProblemJson = "application/problem+json";

    private readonly GrowceryWebApplicationFactory _factory;

    public GroceryListsControllerTests(GrowceryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLists_WhenUnauthenticated_ReturnsUnauthorizedProblem()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/lists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetLists_ForNewUser_ReturnsEmptyArray()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/lists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lists = await response.Content.ReadFromJsonAsync<List<GroceryListSummaryResponse>>();
        Assert.NotNull(lists);
        Assert.Empty(lists);
    }
}
