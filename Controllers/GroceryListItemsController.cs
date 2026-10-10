using growcery.Data;
using growcery.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace growcery.Controllers;

[ApiController]
[Authorize]
[Route("api/lists/{listId:guid}/items")]
[Consumes("application/json")]
public class GroceryListItemsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<GroceryListItemsController> _logger;

    public GroceryListItemsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<GroceryListItemsController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    private string CurrentUserId => _userManager.GetUserId(User)!;

    [HttpGet]
    [ProducesResponseType<IEnumerable<GroceryListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<GroceryListItemResponse>>> GetItems(Guid listId)
    {
        var userId = CurrentUserId;
        if (!await _db.GroceryLists.AnyAsync(list => list.Id == listId && list.OwnerId == userId))
        {
            return NotFound();
        }

        var items = await _db.GroceryListItems
            .AsNoTracking()
            .Where(item => item.GroceryListId == listId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(GroceryListMappings.ToItemResponse)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{itemId:guid}")]
    [ProducesResponseType<GroceryListItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroceryListItemResponse>> GetItem(Guid listId, Guid itemId)
    {
        var item = await FindItemQuery(listId, itemId)
            .AsNoTracking()
            .Select(GroceryListMappings.ToItemResponse)
            .FirstOrDefaultAsync();

        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<GroceryListItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListItemResponse>> CreateItem(Guid listId, CreateGroceryListItemRequest request)
    {
        var userId = CurrentUserId;
        var list = await _db.GroceryLists.FirstOrDefaultAsync(list => list.Id == listId && list.OwnerId == userId);
        if (list is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var normalizedName = Normalize(name);
        if (await NameExistsAsync(listId, normalizedName, exceptItemId: null))
        {
            return DuplicateName(name);
        }

        var now = Timestamps.UtcNow();
        var item = new GroceryListItem
        {
            GroceryListId = listId,
            Name = name,
            NormalizedName = normalizedName,
            Quantity = request.Quantity,
            Unit = TrimToNull(request.Unit),
            Notes = TrimToNull(request.Notes),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.GroceryListItems.Add(item);
        list.UpdatedAt = now;

        if (!await TrySaveAsync(listId))
        {
            return DuplicateName(name);
        }

        _logger.LogInformation("User {UserId} added item {GroceryListItemId} to grocery list {GroceryListId}", userId, item.Id, listId);
        return CreatedAtAction(nameof(GetItem), new { listId, itemId = item.Id }, item.ToResponse());
    }

    [HttpPut("{itemId:guid}")]
    [ProducesResponseType<GroceryListItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListItemResponse>> UpdateItem(Guid listId, Guid itemId, UpdateGroceryListItemRequest request)
    {
        var item = await FindItemQuery(listId, itemId).Include(item => item.GroceryList).FirstOrDefaultAsync();
        if (item is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        var normalizedName = Normalize(name);
        if (await NameExistsAsync(listId, normalizedName, exceptItemId: itemId))
        {
            return DuplicateName(name);
        }

        var now = Timestamps.UtcNow();
        item.Name = name;
        item.NormalizedName = normalizedName;
        item.Quantity = request.Quantity;
        item.Unit = TrimToNull(request.Unit);
        item.Notes = TrimToNull(request.Notes);
        item.IsPurchased = request.IsPurchased;
        item.UpdatedAt = now;
        item.GroceryList!.UpdatedAt = now;

        if (!await TrySaveAsync(listId))
        {
            return DuplicateName(name);
        }

        _logger.LogInformation("User {UserId} updated item {GroceryListItemId} in grocery list {GroceryListId}", CurrentUserId, item.Id, listId);
        return Ok(item.ToResponse());
    }

    [HttpDelete("{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(Guid listId, Guid itemId)
    {
        var item = await FindItemQuery(listId, itemId).Include(item => item.GroceryList).FirstOrDefaultAsync();
        if (item is null)
        {
            return NotFound();
        }

        _db.GroceryListItems.Remove(item);
        item.GroceryList!.UpdatedAt = Timestamps.UtcNow();
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted item {GroceryListItemId} from grocery list {GroceryListId}", CurrentUserId, itemId, listId);
        return NoContent();
    }

    // The query filters already hide items of soft-deleted lists; this adds ownership and the list/item pairing.
    private IQueryable<GroceryListItem> FindItemQuery(Guid listId, Guid itemId)
    {
        var userId = CurrentUserId;
        return _db.GroceryListItems.Where(item =>
            item.Id == itemId && item.GroceryListId == listId && item.GroceryList!.OwnerId == userId);
    }

    private Task<bool> NameExistsAsync(Guid listId, string normalizedName, Guid? exceptItemId) =>
        _db.GroceryListItems.AnyAsync(item =>
            item.GroceryListId == listId && item.NormalizedName == normalizedName && item.Id != exceptItemId);

    // A concurrent request can pass the NameExistsAsync check first; the unique index then rejects this save.
    private async Task<bool> TrySaveAsync(Guid listId)
    {
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _logger.LogWarning("Duplicate item name rejected by the unique index for grocery list {GroceryListId}", listId);
            return false;
        }
    }

    private ObjectResult DuplicateName(string name) =>
        Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Duplicate item name",
            detail: $"An item named '{name}' already exists in this list.");

    private static string Normalize(string name) => name.ToUpperInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
