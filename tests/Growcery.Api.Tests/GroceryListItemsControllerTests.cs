using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Growcery.Api.Models;
using Growcery.Data.Entities;
using Growcery.Data.Identity;
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
        var response = await client.PostAsJsonAsync("/api/lists", new CreateGroceryListRequest { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GroceryListResponse>())!;
    }

    private static async Task<GroceryListItemResponse> CreateItemAsync(HttpClient client, Guid listId, string name, decimal? quantity = null)
    {
        var response = await client.PostAsJsonAsync($"/api/lists/{listId}/items", new CreateGroceryListItemRequest { Name = name, Quantity = quantity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GroceryListItemResponse>())!;
    }

    private static Task<HttpResponseMessage> UpdateItemAsync(HttpClient client, Guid listId, Guid itemId, string name, bool isPurchased = false) =>
        client.PutAsJsonAsync($"/api/lists/{listId}/items/{itemId}", new UpdateGroceryListItemRequest { Name = name, IsPurchased = isPurchased });

    [Fact]
    public async Task Create_OnOwnList_ReturnsCreatedWithTrimmedFields()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items",
            new CreateGroceryListItemRequest { Name = "  Milk ", Quantity = 2, Unit = " L ", Notes = "   " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<GroceryListItemResponse>())!;
        Assert.Equal("Milk", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.Equal("L", item.Unit);
        Assert.Null(item.Notes);
        Assert.False(item.IsPurchased);
        Assert.Equal(list.Id, item.GroceryListId);
        Assert.Equal($"/api/lists/{list.Id}/items/{item.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Create_UpdatesParentListUpdatedAt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        await CreateItemAsync(client, list.Id, "Milk");

        var fetched = await client.GetFromJsonAsync<GroceryListResponse>($"/api/lists/{list.Id}");
        Assert.True(fetched!.UpdatedAt > list.UpdatedAt);
    }

    [Fact]
    public async Task Create_OnAnotherUsersList_ReturnsNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner);

        var response = await intruder.PostAsJsonAsync($"/api/lists/{list.Id}/items", new CreateGroceryListItemRequest { Name = "Milk" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutQuantity_ReturnsCreated()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var item = await CreateItemAsync(client, list.Id, "Bread");

        Assert.Null(item.Quantity);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.236")]
    public async Task Create_WithInvalidQuantity_ReturnsValidationProblem(string quantity)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items",
            new CreateGroceryListItemRequest { Name = "Milk", Quantity = decimal.Parse(quantity, CultureInfo.InvariantCulture) });

        await AssertValidationProblemAsync(response, "Quantity");
    }

    [Fact]
    public async Task Create_WithTwoDecimalQuantity_ReturnsCreated()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);

        var item = await CreateItemAsync(client, list.Id, "Cheese", 1.25m);

        Assert.Equal(1.25m, item.Quantity);
    }

    [Fact]
    public async Task Create_UnderEnZaCulture_ValidatesQuantity()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-ZA");

            var valid = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new CreateGroceryListItemRequest { Name = "Apples", Quantity = 2.5m });
            var invalid = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new CreateGroceryListItemRequest { Name = "Pears", Quantity = 0 });

            Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task GetItems_ReturnsItemsInCreationOrder()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        await CreateItemAsync(client, list.Id, "Milk");
        await CreateItemAsync(client, list.Id, "Bread");
        await CreateItemAsync(client, list.Id, "Eggs");

        var items = await client.GetFromJsonAsync<List<GroceryListItemResponse>>($"/api/lists/{list.Id}/items");

        Assert.Equal(["Milk", "Bread", "Eggs"], items!.Select(item => item.Name));
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
        var created = await CreateItemAsync(client, list.Id, "Milk", 2);

        var fetched = await client.GetFromJsonAsync<GroceryListItemResponse>($"/api/lists/{list.Id}/items/{created.Id}");

        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Update_WithIsPurchased_PersistsAndShowsInSummaryCounts()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");
        await CreateItemAsync(client, list.Id, "Bread");

        var response = await UpdateItemAsync(client, list.Id, item.Id, "Milk", isPurchased: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<GroceryListItemResponse>())!;
        Assert.True(updated.IsPurchased);
        Assert.True(updated.UpdatedAt > item.UpdatedAt);
        var summary = Assert.Single((await client.GetFromJsonAsync<List<GroceryListSummaryResponse>>("/api/lists"))!);
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(1, summary.PurchasedCount);
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
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateItemAsync(client, otherList.Id, item.Id, "Milk")).StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateItemAsync(intruder, list.Id, item.Id, "Hijacked")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsNoContentAndUpdatesParentListUpdatedAt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");
        var before = (await client.GetFromJsonAsync<GroceryListResponse>($"/api/lists/{list.Id}"))!;

        var response = await client.DeleteAsync($"/api/lists/{list.Id}/items/{item.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = (await client.GetFromJsonAsync<GroceryListResponse>($"/api/lists/{list.Id}"))!;
        Assert.Empty(after.Items);
        Assert.True(after.UpdatedAt > before.UpdatedAt);
    }

    [Fact]
    public async Task ItemEndpoints_OnSoftDeletedList_ReturnNotFound()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");
        await client.DeleteAsync($"/api/lists/{list.Id}");
        var itemUrl = $"/api/lists/{list.Id}/items/{item.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/lists/{list.Id}/items")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(itemUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new CreateGroceryListItemRequest { Name = "Bread" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateItemAsync(client, list.Id, item.Id, "Milk")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(itemUrl)).StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateNameIgnoringCaseAndWhitespace_ReturnsConflictProblem()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        await CreateItemAsync(client, list.Id, "Milk");

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new CreateGroceryListItemRequest { Name = " milk " });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("Duplicate item name", problem.Title);
        Assert.Equal("An item named 'milk' already exists in this list.", problem.Detail);
    }

    [Fact]
    public async Task Create_WithSameNameInDifferentList_ReturnsCreated()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "First");
        var otherList = await CreateListAsync(client, "Second");
        await CreateItemAsync(client, list.Id, "Milk");

        await CreateItemAsync(client, otherList.Id, "Milk");
    }

    [Fact]
    public async Task Create_WithNameOfPurchasedItem_ReturnsConflict()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");
        await UpdateItemAsync(client, list.Id, item.Id, "Milk", isPurchased: true);

        var response = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new CreateGroceryListItemRequest { Name = "Milk" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_RenamingToAnotherItemsName_ReturnsConflict()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        await CreateItemAsync(client, list.Id, "Milk");
        var bread = await CreateItemAsync(client, list.Id, "Bread");

        var response = await UpdateItemAsync(client, list.Id, bread.Id, "MILK");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("milk")]
    [InlineData("Milk")]
    public async Task Update_KeepingOwnNameOrChangingCase_ReturnsOk(string newName)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "milk");

        var response = await UpdateItemAsync(client, list.Id, item.Id, newName);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(newName, (await response.Content.ReadFromJsonAsync<GroceryListItemResponse>())!.Name);
    }

    [Fact]
    public async Task Create_WithNameOfDeletedItem_ReturnsCreated()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client);
        var item = await CreateItemAsync(client, list.Id, "Milk");
        await client.DeleteAsync($"/api/lists/{list.Id}/items/{item.Id}");

        await CreateItemAsync(client, list.Id, "Milk");
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, key => key.Equals(field, StringComparison.OrdinalIgnoreCase));
    }
}
