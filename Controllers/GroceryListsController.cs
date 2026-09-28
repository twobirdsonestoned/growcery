using growcery.Data;
using growcery.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace growcery.Controllers;

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
}
