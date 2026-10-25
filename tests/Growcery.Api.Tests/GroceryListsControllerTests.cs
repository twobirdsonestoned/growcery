using System.Net;
using System.Net.Http.Json;
using System.Text;
using Growcery.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Growcery.Api.Tests;

public class GroceryListsControllerTests : IClassFixture<GrowceryWebApplicationFactory>
{
    private const string ProblemJson = "application/problem+json";

    private readonly GrowceryWebApplicationFactory _factory;

    public GroceryListsControllerTests(GrowceryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<GroceryListResponse> CreateListAsync(HttpClient client, string? name)
    {
        var response = await client.PostAsJsonAsync("/api/lists", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadJsonAsync<GroceryListResponse>();
    }

    private static async Task<GroceryListItemResponse> AddItemAsync(HttpClient client, Guid listId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/lists/{listId}/items", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadJsonAsync<GroceryListItemResponse>();
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
        Assert.Empty(await response.Content.ReadJsonAsync<List<GroceryListSummaryResponse>>());
    }

    [Fact]
    public async Task Create_WithValidName_ReturnsCreatedWithLocationAndTrimmedName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/lists", new { name = "  Weekly shop  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var list = await response.Content.ReadJsonAsync<GroceryListResponse>();
        Assert.Equal("Weekly shop", list.Name);
        Assert.Empty(list.Items);
        Assert.Equal($"/api/lists/{list.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutName_UsesDateBasedDefaultName(string? name)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var list = await CreateListAsync(client, name);

        Assert.StartsWith("Shopping list ", list.Name);
    }

    [Fact]
    public async Task Create_WithEmptyBody_UsesDefaultName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/lists", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("Shopping list ", (await response.Content.ReadJsonAsync<GroceryListResponse>()).Name);
    }

    [Fact]
    public async Task Create_WithNameOver100Characters_ReturnsValidationProblem()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/lists", new { name = new string('a', 101) });

        await AssertValidationProblemAsync(response, "name");
    }

    [Fact]
    public async Task Create_WithFormContentType_ReturnsUnsupportedMediaType()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/lists",
            new StringContent("name=Weekly", Encoding.UTF8, "application/x-www-form-urlencoded"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientSuppliedIdAndOwnerId_IgnoresThem()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var suppliedId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/lists", new { id = suppliedId, ownerId = "someone-else", name = "Mine" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var list = await response.Content.ReadJsonAsync<GroceryListResponse>();
        Assert.NotEqual(suppliedId, list.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/lists/{list.Id}")).StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateListName_CreatesBoth()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var first = await CreateListAsync(client, "Weekly shop");
        var second = await CreateListAsync(client, "Weekly shop");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task GetLists_ReturnsOnlyCallersListsWithItemCounts()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var otherClient = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Mine");
        await CreateListAsync(otherClient, "Theirs");
        var milk = await AddItemAsync(client, list.Id, "Milk");
        await AddItemAsync(client, list.Id, "Bread");
        await client.PutAsJsonAsync($"/api/lists/{list.Id}/items/{milk.Id}", new { name = "Milk", isPurchased = true });

        var lists = await client.GetJsonAsync<List<GroceryListSummaryResponse>>("/api/lists");

        var summary = Assert.Single(lists);
        Assert.Equal(list.Id, summary.Id);
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(1, summary.PurchasedCount);
    }

    [Fact]
    public async Task GetList_ForAnotherUsersList_ReturnsNotFoundProblem()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Private");

        var response = await intruder.GetAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateThenGet_ReturnIdenticalTimestamps()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var created = await CreateListAsync(client, "Timestamps");

        var fetched = await client.GetJsonAsync<GroceryListResponse>($"/api/lists/{created.Id}");

        Assert.Equal(created.CreatedAt, fetched.CreatedAt);
        Assert.Equal(created.UpdatedAt, fetched.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, created.CreatedAt.Offset);
    }

    [Fact]
    public async Task Update_WithValidName_ChangesNameAndAdvancesUpdatedAt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var created = await CreateListAsync(client, "Old name");

        var response = await client.PutAsJsonAsync($"/api/lists/{created.Id}", new { name = " New name " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadJsonAsync<GroceryListResponse>();
        Assert.Equal("New name", updated.Name);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public async Task Update_WithBlankName_ResetsToDefaultName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var created = await CreateListAsync(client, "Named");

        var response = await client.PutAsJsonAsync($"/api/lists/{created.Id}", new { name = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("Shopping list ", (await response.Content.ReadJsonAsync<GroceryListResponse>()).Name);
    }

    [Fact]
    public async Task Update_ForAnotherUsersList_ReturnsNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Private");

        var response = await intruder.PutAsJsonAsync($"/api/lists/{list.Id}", new { name = "Hijacked" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesListAndItsItems()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "To delete");
        var item = await AddItemAsync(client, list.Id, "Milk");

        var response = await client.DeleteAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/lists/{list.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/lists/{list.Id}/items/{item.Id}")).StatusCode);
        Assert.Empty(await client.GetJsonAsync<List<GroceryListSummaryResponse>>("/api/lists"));
    }

    [Fact]
    public async Task Delete_ForAnotherUsersList_ReturnsNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Private");

        var response = await intruder.DeleteAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/lists/{list.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Twice_ReturnsNotFound()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Twice");
        await client.DeleteAsync($"/api/lists/{list.Id}");

        var response = await client.DeleteAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemovedEndpoints_DeletedAndRestore_NoLongerExist()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Gone");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/lists/deleted")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/lists/{list.Id}/restore", null)).StatusCode);
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem.Errors.Keys, key => key.Contains(field, StringComparison.OrdinalIgnoreCase));
    }
}
