using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Growcery.Api.Contracts;
using Growcery.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Growcery.Api.Tests;

public class GroceryListItemsControllerTests : IClassFixture<GrowceryWebApplicationFactory>
{
    private const string ProblemJson = "application/problem+json";

    private readonly GrowceryWebApplicationFactory _factory;

    public GroceryListItemsControllerTests(GrowceryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<GroceryListResponse> CreateListAsync(HttpClient client, string name = "Weekly shop")
    {
        var response = await client.PostAsJsonAsync("/api/lists", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadJsonAsync<GroceryListResponse>();
    }

    private static async Task<GroceryListItemResponse> CreateItemAsync(HttpClient client, Guid listId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/lists/{listId}/items", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadJsonAsync<GroceryListItemResponse>();
    }

    private static Task<GroceryListItemResponse> CreateItemAsync(HttpClient client, Guid listId, string name) =>
        CreateItemAsync(client, listId, new { name });

    private static Task<HttpResponseMessage> UpdateItemAsync(HttpClient client, Guid listId, Guid itemId, object body) =>
        client.PutAsJsonAsync($"/api/lists/{listId}/items/{itemId}", body);

    [Fact]
    public async Task Create_WithAllFields_ReturnsCreatedWithTrimmedName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { name = "  Milk ", quantity = 2, unit = "Litre" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadJsonAsync<GroceryListItemResponse>();
        Assert.Equal("Milk", item.Name);
        Assert.Equal(2m, item.Quantity);
        Assert.Equal(UnitOfMeasure.Litre, item.Unit);
        Assert.False(item.IsPurchased);
        Assert.Equal(list.Id, item.GroceryListId);
        Assert.Equal($"/api/lists/{list.Id}/items/{item.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Create_ReturnsUnitAsString()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { name = "Flour", unit = "Kilogram" });

        Assert.Contains("\"unit\":\"Kilogram\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_WithNameOnly_DefaultsQuantityAndUnit()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var item = await CreateItemAsync(client, list.Id, "Bread");

        Assert.Equal(1m, item.Quantity);
        Assert.Equal(UnitOfMeasure.Each, item.Unit);
    }

    [Fact]
    public async Task Create_UpdatesParentListUpdatedAt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        await CreateItemAsync(client, list.Id, "Milk");

        var fetched = await client.GetJsonAsync<GroceryListResponse>($"/api/lists/{list.Id}");
        Assert.True(fetched.UpdatedAt > list.UpdatedAt);
    }

    [Fact]
    public async Task Create_OnAnotherUsersList_ReturnsNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner);

        var response = await intruder.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { name = "Milk" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_WithBlankName_ReturnsValidationProblem(string? name)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { name });

        await AssertValidationProblemAsync(response, "name");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.2345")]
    public async Task Create_WithInvalidQuantity_ReturnsValidationProblem(string quantity)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items",
            new { name = "Milk", quantity = decimal.Parse(quantity, CultureInfo.InvariantCulture) });

        await AssertValidationProblemAsync(response, "quantity");
    }

    [Fact]
    public async Task Create_WithThreeDecimalQuantity_ReturnsCreated()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var item = await CreateItemAsync(client, list.Id, new { name = "Cheese", quantity = 0.125m, unit = "Kilogram" });

        Assert.Equal(0.125m, item.Quantity);
    }

    [Theory]
    [InlineData("\"Bucket\"")]
    [InlineData("99")]
    public async Task Create_WithUnknownUnit_ReturnsValidationProblem(string unitJson)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var response = await client.PostAsync($"/api/lists/{list.Id}/items",
            new StringContent($"{{\"name\":\"Milk\",\"unit\":{unitJson}}}", System.Text.Encoding.UTF8, "application/json"));

        await AssertValidationProblemAsync(response, "unit");
    }

    [Fact]
    public async Task Create_UnderEnZaCulture_ParsesDecimalQuantity()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-ZA");

            var valid = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { name = "Apples", quantity = 2.5m });
            var invalid = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { name = "Pears", quantity = 0 });

            Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
            Assert.Equal(2.5m, (await valid.Content.ReadJsonAsync<GroceryListItemResponse>()).Quantity);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsCreated()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        await CreateItemAsync(client, list.Id, "Milk");

        await CreateItemAsync(client, list.Id, " milk ");

        Assert.Equal(2, (await client.GetJsonAsync<List<GroceryListItemResponse>>($"/api/lists/{list.Id}/items")).Count);
    }

    [Fact]
    public async Task GetItems_ReturnsItemsInCreationOrder()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        await CreateItemAsync(client, list.Id, "Milk");
        await CreateItemAsync(client, list.Id, "Bread");
        await CreateItemAsync(client, list.Id, "Eggs");

        var items = await client.GetJsonAsync<List<GroceryListItemResponse>>($"/api/lists/{list.Id}/items");

        Assert.Equal(["Milk", "Bread", "Eggs"], items.Select(item => item.Name));
    }

    [Fact]
    public async Task GetItems_ForAnotherUsersList_ReturnsNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner);

        var response = await intruder.GetAsync($"/api/lists/{list.Id}/items");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_ReturnsItem()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var created = await CreateItemAsync(client, list.Id, new { name = "Milk", quantity = 2, unit = "Litre" });

        var fetched = await client.GetJsonAsync<GroceryListItemResponse>($"/api/lists/{list.Id}/items/{created.Id}");

        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Update_WithIsPurchased_PersistsAndShowsInSummaryCounts()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");
        await CreateItemAsync(client, list.Id, "Bread");

        var response = await UpdateItemAsync(client, list.Id, item.Id, new { name = "Milk", quantity = 3, unit = "Litre", isPurchased = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadJsonAsync<GroceryListItemResponse>();
        Assert.True(updated.IsPurchased);
        Assert.Equal(3m, updated.Quantity);
        Assert.Equal(UnitOfMeasure.Litre, updated.Unit);
        Assert.True(updated.UpdatedAt > item.UpdatedAt);
        var summary = Assert.Single(await client.GetJsonAsync<List<GroceryListSummaryResponse>>("/api/lists"));
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(1, summary.PurchasedCount);
    }

    [Fact]
    public async Task Update_WithInvalidQuantity_ReturnsValidationProblem()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");

        var response = await UpdateItemAsync(client, list.Id, item.Id, new { name = "Milk", quantity = 0, isPurchased = false });

        await AssertValidationProblemAsync(response, "quantity");
    }

    [Fact]
    public async Task ItemEndpoints_WithWrongListId_ReturnNotFound()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Has the item");
        var otherList = await CreateListAsync(client, "Doesn't have it");
        var item = await CreateItemAsync(client, list.Id, "Milk");
        var wrongUrl = $"/api/lists/{otherList.Id}/items/{item.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(wrongUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateItemAsync(client, otherList.Id, item.Id, new { name = "Milk" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(wrongUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/lists/{list.Id}/items/{item.Id}")).StatusCode);
    }

    [Fact]
    public async Task ItemEndpoints_ForAnotherUsersItem_ReturnNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner);
        var item = await CreateItemAsync(owner, list.Id, "Milk");
        var url = $"/api/lists/{list.Id}/items/{item.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateItemAsync(intruder, list.Id, item.Id, new { name = "Hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentAndUpdatesParentListUpdatedAt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");
        var before = await client.GetJsonAsync<GroceryListResponse>($"/api/lists/{list.Id}");

        var response = await client.DeleteAsync($"/api/lists/{list.Id}/items/{item.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await client.GetJsonAsync<GroceryListResponse>($"/api/lists/{list.Id}");
        Assert.Empty(after.Items);
        Assert.True(after.UpdatedAt > before.UpdatedAt);
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem.Errors.Keys, key => key.Contains(field, StringComparison.OrdinalIgnoreCase));
    }
}
