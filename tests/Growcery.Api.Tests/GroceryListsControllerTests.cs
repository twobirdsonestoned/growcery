using System.Net;
using System.Net.Http.Json;
using System.Text;
using Growcery.Data;
using Growcery.Api.Models;
using Growcery.Data.Entities;
using Growcery.Data.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Growcery.Api.Tests;

public class GroceryListsControllerTests : IClassFixture<GrowceryWebApplicationFactory>
{
    private const string ProblemJson = "application/problem+json";

    private readonly GrowceryWebApplicationFactory _factory;

    public GroceryListsControllerTests(GrowceryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<GroceryListResponse> CreateListAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/lists", new CreateGroceryListRequest { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GroceryListResponse>())!;
    }

    // Item endpoints are covered in GroceryListItemsControllerTests; here items are seeded directly.
    private async Task AddItemsAsync(Guid listId, params (string Name, bool IsPurchased)[] items)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = Timestamps.UtcNow();
        foreach (var (name, isPurchased) in items)
        {
            db.GroceryListItems.Add(new GroceryListItem
            {
                GroceryListId = listId,
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
                IsPurchased = isPurchased,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
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

    [Fact]
    public async Task Create_WithValidName_ReturnsCreatedWithLocationAndTrimmedName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/lists", new CreateGroceryListRequest { Name = "  Weekly shop  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<GroceryListResponse>();
        Assert.NotNull(list);
        Assert.Equal("Weekly shop", list.Name);
        Assert.Empty(list.Items);
        Assert.Null(list.DeletedAt);
        Assert.Equal($"/api/lists/{list.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_WithBlankName_ReturnsValidationProblem(string? name)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/lists", new { name });

        await AssertValidationProblemAsync(response, "Name");
    }

    [Fact]
    public async Task Create_WithNameOver100Characters_ReturnsValidationProblem()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/lists", new CreateGroceryListRequest { Name = new string('a', 101) });

        await AssertValidationProblemAsync(response, "Name");
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
        var list = (await response.Content.ReadFromJsonAsync<GroceryListResponse>())!;
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
        await AddItemsAsync(list.Id, ("Milk", true), ("Bread", false), ("Eggs", true));

        var lists = await client.GetFromJsonAsync<List<GroceryListSummaryResponse>>("/api/lists");

        var summary = Assert.Single(lists!);
        Assert.Equal(list.Id, summary.Id);
        Assert.Equal(3, summary.ItemCount);
        Assert.Equal(2, summary.PurchasedCount);
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

        var fetched = await client.GetFromJsonAsync<GroceryListResponse>($"/api/lists/{created.Id}");

        Assert.Equal(created.CreatedAt, fetched!.CreatedAt);
        Assert.Equal(created.UpdatedAt, fetched.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, created.CreatedAt.Offset);
    }

    [Fact]
    public async Task Update_WithValidName_ChangesNameAndAdvancesUpdatedAt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var created = await CreateListAsync(client, "Old name");

        var response = await client.PutAsJsonAsync($"/api/lists/{created.Id}", new UpdateGroceryListRequest { Name = " New name " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<GroceryListResponse>())!;
        Assert.Equal("New name", updated.Name);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public async Task Update_ForAnotherUsersList_ReturnsNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Private");

        var response = await intruder.PutAsJsonAsync($"/api/lists/{list.Id}", new UpdateGroceryListRequest { Name = "Hijacked" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_SoftDeletesListAndHidesIt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "To delete");

        var response = await client.DeleteAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/lists/{list.Id}")).StatusCode);
        var lists = await client.GetFromJsonAsync<List<GroceryListSummaryResponse>>("/api/lists");
        Assert.DoesNotContain(lists!, summary => summary.Id == list.Id);
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
    public async Task Update_OnDeletedList_ReturnsNotFound()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Deleted");
        await client.DeleteAsync($"/api/lists/{list.Id}");

        var response = await client.PutAsJsonAsync($"/api/lists/{list.Id}", new UpdateGroceryListRequest { Name = "Revived?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDeleted_ReturnsOnlyCallersDeletedLists()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var otherClient = await _factory.CreateAuthenticatedClientAsync();
        var deleted = await CreateListAsync(client, "Deleted");
        await CreateListAsync(client, "Active");
        var othersDeleted = await CreateListAsync(otherClient, "Theirs");
        await client.DeleteAsync($"/api/lists/{deleted.Id}");
        await otherClient.DeleteAsync($"/api/lists/{othersDeleted.Id}");

        var lists = await client.GetFromJsonAsync<List<GroceryListSummaryResponse>>("/api/lists/deleted");

        var summary = Assert.Single(lists!);
        Assert.Equal(deleted.Id, summary.Id);
        Assert.NotNull(summary.DeletedAt);
    }

    [Fact]
    public async Task Restore_DeletedList_ReturnsListWithItemsUnchanged()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Restore me");
        await AddItemsAsync(list.Id, ("Milk", true), ("Bread", false));
        await client.DeleteAsync($"/api/lists/{list.Id}");

        var response = await client.PostAsync($"/api/lists/{list.Id}/restore", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var restored = (await response.Content.ReadFromJsonAsync<GroceryListResponse>())!;
        Assert.Null(restored.DeletedAt);
        Assert.Equal(["Milk", "Bread"], restored.Items.Select(item => item.Name).Order().Reverse());
        Assert.Equal(1, restored.Items.Count(item => item.IsPurchased));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/lists/{list.Id}")).StatusCode);
    }

    [Fact]
    public async Task Restore_ActiveList_ReturnsNotFound()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(client, "Active");

        var response = await client.PostAsync($"/api/lists/{list.Id}/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Restore_AnotherUsersDeletedList_ReturnsNotFound()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync();
        var intruder = await _factory.CreateAuthenticatedClientAsync();
        var list = await CreateListAsync(owner, "Private");
        await owner.DeleteAsync($"/api/lists/{list.Id}");

        var response = await intruder.PostAsync($"/api/lists/{list.Id}/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, key => key.Equals(field, StringComparison.OrdinalIgnoreCase));
    }
}
