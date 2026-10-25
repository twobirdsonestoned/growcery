using Growcery.Core.Domain;
using Growcery.Core.Services;

namespace Growcery.Core.Tests.Services;

public class GroceryListItemServiceTests
{
    private readonly ServiceFixture _fixture = new();

    private async Task<GroceryList> CreateListAsync() =>
        (await _fixture.ListService().CreateListAsync("Weekly shop")).Value;

    [Fact]
    public async Task AddItem_WithNameOnly_UsesDefaultsAndUpdatesList()
    {
        var list = await CreateListAsync();
        _fixture.Clock.Advance();

        var result = await _fixture.ItemService().AddItemAsync(list.Id, new ItemInput("Milk", null, null));

        Assert.True(result.IsSuccess);
        var item = result.Value;
        Assert.Equal("Milk", item.Name);
        Assert.Equal(1m, item.Quantity);
        Assert.Equal(UnitOfMeasure.Each, item.Unit);
        Assert.False(item.IsPurchased);
        Assert.Equal(list.Id, item.GroceryListId);
        Assert.Equal(_fixture.Clock.UtcNow, list.UpdatedAt);
    }

    [Fact]
    public async Task AddItem_WithDuplicateName_IsAllowed()
    {
        var list = await CreateListAsync();
        var service = _fixture.ItemService();

        await service.AddItemAsync(list.Id, new ItemInput("Milk", null, null));
        var second = await service.AddItemAsync(list.Id, new ItemInput("milk", null, null));

        Assert.True(second.IsSuccess);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public async Task AddItem_WithInvalidInput_IsInvalidAndSavesNothing()
    {
        var list = await CreateListAsync();
        var savesBefore = _fixture.UnitOfWork.SaveCount;

        var result = await _fixture.ItemService().AddItemAsync(list.Id, new ItemInput("Milk", 0, null));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(list.Items);
        Assert.Equal(savesBefore, _fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AddItem_ToNonMembersList_ReturnsNotFound()
    {
        var list = await CreateListAsync();
        _fixture.ActAs("someone-else");

        var result = await _fixture.ItemService().AddItemAsync(list.Id, new ItemInput("Milk", null, null));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task UpdateItem_MarkPurchased_UpdatesItemAndListTimestamps()
    {
        var list = await CreateListAsync();
        var service = _fixture.ItemService();
        var item = (await service.AddItemAsync(list.Id, new ItemInput("Milk", null, null))).Value;
        _fixture.Clock.Advance();

        var result = await service.UpdateItemAsync(list.Id, item.Id, new ItemInput("Milk", 2, UnitOfMeasure.Litre), isPurchased: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPurchased);
        Assert.Equal(2m, result.Value.Quantity);
        Assert.Equal(UnitOfMeasure.Litre, result.Value.Unit);
        Assert.Equal(_fixture.Clock.UtcNow, result.Value.UpdatedAt);
        Assert.Equal(_fixture.Clock.UtcNow, list.UpdatedAt);
    }

    [Fact]
    public async Task UpdateItem_WithWrongListId_ReturnsNotFound()
    {
        var list = await CreateListAsync();
        var otherList = await CreateListAsync();
        var service = _fixture.ItemService();
        var item = (await service.AddItemAsync(list.Id, new ItemInput("Milk", null, null))).Value;

        var result = await service.UpdateItemAsync(otherList.Id, item.Id, new ItemInput("Milk", null, null), isPurchased: true);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.False(item.IsPurchased);
    }

    [Fact]
    public async Task RemoveItem_RemovesItAndUpdatesList()
    {
        var list = await CreateListAsync();
        var service = _fixture.ItemService();
        var item = (await service.AddItemAsync(list.Id, new ItemInput("Milk", null, null))).Value;
        _fixture.Clock.Advance();

        var result = await service.RemoveItemAsync(list.Id, item.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(list.Items);
        Assert.Equal(_fixture.Clock.UtcNow, list.UpdatedAt);
    }

    [Fact]
    public async Task GetItems_ReturnsItemsInCreationOrder()
    {
        var list = await CreateListAsync();
        var service = _fixture.ItemService();
        foreach (var name in new[] { "Milk", "Bread", "Eggs" })
        {
            _fixture.Clock.Advance();
            await service.AddItemAsync(list.Id, new ItemInput(name, null, null));
        }

        var result = await service.GetItemsAsync(list.Id);

        Assert.Equal(["Milk", "Bread", "Eggs"], result.Value.Select(item => item.Name));
    }

    [Fact]
    public async Task GetItem_ForNonMember_ReturnsNotFound()
    {
        var list = await CreateListAsync();
        var item = (await _fixture.ItemService().AddItemAsync(list.Id, new ItemInput("Milk", null, null))).Value;
        _fixture.ActAs("someone-else");

        var result = await _fixture.ItemService().GetItemAsync(list.Id, item.Id);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
