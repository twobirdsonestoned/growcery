using Growcery.Core.Abstractions;
using Growcery.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Growcery.Data.Repositories;

public sealed class EfGroceryListRepository : IGroceryListRepository
{
    private readonly ApplicationDbContext _db;

    public EfGroceryListRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<GroceryListSummary>> GetSummariesForMemberAsync(Guid personId, CancellationToken cancellationToken) =>
        await _db.GroceryLists
            .AsNoTracking()
            .Where(list => list.Members.Any(member => member.PersonId == personId))
            .OrderByDescending(list => list.UpdatedAt)
            .ThenBy(list => list.Id)
            // Counted in SQL, so item rows are never loaded for an overview.
            .Select(list => new GroceryListSummary(
                list.Id, list.Name, list.Items.Count, list.Items.Count(item => item.IsPurchased), list.CreatedAt, list.UpdatedAt))
            .ToListAsync(cancellationToken);

    // AsNoTracking: EF skips the change-tracking snapshot for data that will only be read, which makes reads cheaper.
    public Task<GroceryList?> GetForMemberAsync(Guid listId, Guid personId, CancellationToken cancellationToken) =>
        QueryForMember(listId, personId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    // Tracked, so changes made through the domain methods are saved by EfUnitOfWork.
    public Task<GroceryList?> GetForUpdateAsync(Guid listId, Guid personId, CancellationToken cancellationToken) =>
        QueryForMember(listId, personId).FirstOrDefaultAsync(cancellationToken);

    public void Add(GroceryList list) => _db.GroceryLists.Add(list);

    public void Remove(GroceryList list) => _db.GroceryLists.Remove(list);

    private IQueryable<GroceryList> QueryForMember(Guid listId, Guid personId) =>
        _db.GroceryLists
            .Include(list => list.Members)
            .Include(list => list.Items)
            .Where(list => list.Id == listId && list.Members.Any(member => member.PersonId == personId));
}
