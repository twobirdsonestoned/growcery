using Growcery.Data;
using Growcery.Api.Models;
using Growcery.Data.Entities;
using Growcery.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Growcery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lists")]
[Consumes("application/json")]
public class GroceryListsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<GroceryListsController> _logger;

    public GroceryListsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<GroceryListsController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    private string CurrentUserId => _userManager.GetUserId(User)!;

    [HttpGet]
    [ProducesResponseType<IEnumerable<GroceryListSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<GroceryListSummaryResponse>>> GetLists()
    {
        var userId = CurrentUserId;
        var lists = await _db.GroceryLists
            .AsNoTracking()
            .Where(list => list.OwnerId == userId)
            .OrderByDescending(list => list.UpdatedAt)
            .ThenBy(list => list.Id)
            .Select(GroceryListMappings.ToSummary)
            .ToListAsync();

        return Ok(lists);
    }

    [HttpGet("deleted")]
    [ProducesResponseType<IEnumerable<GroceryListSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<GroceryListSummaryResponse>>> GetDeletedLists()
    {
        var userId = CurrentUserId;
        var lists = await _db.GroceryLists
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(list => list.OwnerId == userId && list.DeletedAt != null)
            .OrderByDescending(list => list.DeletedAt)
            .ThenBy(list => list.Id)
            .Select(GroceryListMappings.ToSummary)
            .ToListAsync();

        return Ok(lists);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroceryListResponse>> GetList(Guid id)
    {
        var userId = CurrentUserId;
        var list = await _db.GroceryLists
            .AsNoTracking()
            .Include(list => list.Items)
            .FirstOrDefaultAsync(list => list.Id == id && list.OwnerId == userId);

        if (list is null)
        {
            return NotFound();
        }

        return Ok(list.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListResponse>> CreateList(CreateGroceryListRequest request)
    {
        var now = Timestamps.UtcNow();
        var list = new GroceryList
        {
            OwnerId = CurrentUserId,
            Name = request.Name.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.GroceryLists.Add(list);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} created grocery list {GroceryListId}", list.OwnerId, list.Id);

        return CreatedAtAction(nameof(GetList), new { id = list.Id }, list.ToResponse());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListResponse>> UpdateList(Guid id, UpdateGroceryListRequest request)
    {
        var userId = CurrentUserId;
        var list = await _db.GroceryLists
            .Include(list => list.Items)
            .FirstOrDefaultAsync(list => list.Id == id && list.OwnerId == userId);

        if (list is null)
        {
            return NotFound();
        }

        list.Name = request.Name.Trim();
        list.UpdatedAt = Timestamps.UtcNow();
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} updated grocery list {GroceryListId}", userId, list.Id);

        return Ok(list.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteList(Guid id)
    {
        var userId = CurrentUserId;
        var list = await _db.GroceryLists.FirstOrDefaultAsync(list => list.Id == id && list.OwnerId == userId);

        if (list is null)
        {
            return NotFound();
        }

        list.DeletedAt = Timestamps.UtcNow();
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} soft-deleted grocery list {GroceryListId}", userId, list.Id);

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroceryListResponse>> RestoreList(Guid id)
    {
        var userId = CurrentUserId;
        var list = await _db.GroceryLists
            .IgnoreQueryFilters()
            .Include(list => list.Items)
            .FirstOrDefaultAsync(list => list.Id == id && list.OwnerId == userId && list.DeletedAt != null);

        if (list is null)
        {
            return NotFound();
        }

        list.DeletedAt = null;
        list.UpdatedAt = Timestamps.UtcNow();
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} restored grocery list {GroceryListId}", userId, list.Id);

        return Ok(list.ToResponse());
    }
}
