using Growcery.Core.Domain;

namespace Growcery.Core.Tests.Services;

public class GroceryListServiceTests
{
    private readonly ServiceFixture _fixture = new();

    [Fact]
    public async Task CreateList_WithoutName_UsesDefaultNameAndMakesCallerOwner()
    {
        var result = await _fixture.ListService().CreateListAsync(name: null);

        Assert.True(result.IsSuccess);
        var list = result.Value;
        Assert.Equal("Shopping list 25 Oct 2026", list.Name);
        var person = Assert.Single(_fixture.People.People);
        var member = Assert.Single(list.Members);
        Assert.Equal(person.Id, member.PersonId);
        Assert.Equal(ListRole.Owner, member.Role);
        Assert.Equal(1, _fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateList_CalledTwiceBySameUser_CreatesOnePerson()
    {
        var service = _fixture.ListService();

        await service.CreateListAsync("One");
        await service.CreateListAsync("Two");

        var person = Assert.Single(_fixture.People.People);
        Assert.Equal(_fixture.CurrentUser.ExternalId, person.ExternalId);
        Assert.Equal(_fixture.CurrentUser.DisplayName, person.DisplayName);
    }

    [Fact]
    public async Task CreateList_WithTooLongName_IsInvalidAndSavesNothing()
    {
        var result = await _fixture.ListService().CreateListAsync(new string('a', 101));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_fixture.Lists.Lists);
        Assert.Equal(0, _fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetLists_ReturnsOnlyListsTheCallerIsAMemberOf()
    {
        var service = _fixture.ListService();
        var mine = (await service.CreateListAsync("Mine")).Value;
        _fixture.ActAs("someone-else");
        await service.CreateListAsync("Theirs");
        _fixture.ActAs("identity-user-1");

        var lists = await service.GetListsAsync();

        var summary = Assert.Single(lists);
        Assert.Equal(mine.Id, summary.Id);
    }

    [Fact]
    public async Task GetList_ForNonMember_ReturnsNotFound()
    {
        var service = _fixture.ListService();
        var list = (await service.CreateListAsync("Private")).Value;
        _fixture.ActAs("someone-else");

        var result = await service.GetListAsync(list.Id);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task RenameList_ByOwner_RenamesAndUpdatesTimestamp()
    {
        var service = _fixture.ListService();
        var list = (await service.CreateListAsync("Old")).Value;
        _fixture.Clock.Advance();

        var result = await service.RenameListAsync(list.Id, " New ");

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value.Name);
        Assert.Equal(_fixture.Clock.UtcNow, result.Value.UpdatedAt);
        Assert.True(result.Value.UpdatedAt > result.Value.CreatedAt);
    }

    [Fact]
    public async Task RenameList_WithBlankName_ResetsToDefaultName()
    {
        var service = _fixture.ListService();
        var list = (await service.CreateListAsync("Named")).Value;

        var result = await service.RenameListAsync(list.Id, "  ");

        Assert.Equal("Shopping list 25 Oct 2026", result.Value.Name);
    }

    [Fact]
    public async Task RenameList_ByEditor_ReturnsForbidden()
    {
        var service = _fixture.ListService();
        var list = (await service.CreateListAsync("Shared")).Value;
        var editor = new Person("editor-1", "Editor", _fixture.Clock.UtcNow);
        _fixture.People.People.Add(editor);
        list.AddMember(editor, ListRole.Editor, _fixture.Clock.UtcNow);
        _fixture.ActAs("editor-1");

        var result = await service.RenameListAsync(list.Id, "Hijacked");

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal("Shared", list.Name);
    }

    [Fact]
    public async Task DeleteList_ByOwner_RemovesList()
    {
        var service = _fixture.ListService();
        var list = (await service.CreateListAsync("Bye")).Value;

        var result = await service.DeleteListAsync(list.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_fixture.Lists.Lists);
    }

    [Fact]
    public async Task DeleteList_ForNonMember_ReturnsNotFoundAndKeepsList()
    {
        var service = _fixture.ListService();
        var list = (await service.CreateListAsync("Keep")).Value;
        _fixture.ActAs("someone-else");

        var result = await service.DeleteListAsync(list.Id);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Single(_fixture.Lists.Lists);
    }
}
