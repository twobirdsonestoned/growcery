using Growcery.Api.Contracts;
using Growcery.Api.Infrastructure;
using Growcery.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Growcery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lists")]
[Consumes("application/json")]
public class GroceryListsController : ControllerBase
{
    private readonly IGroceryListService _lists;

    public GroceryListsController(IGroceryListService lists)
    {
        _lists = lists;
    }

    [HttpGet]
    [ProducesResponseType<IEnumerable<GroceryListSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<GroceryListSummaryResponse>>> GetLists(CancellationToken cancellationToken)
    {
        var lists = await _lists.GetListsAsync(cancellationToken);
        return Ok(lists.Select(list => list.ToResponse()));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroceryListResponse>> GetList(Guid id, CancellationToken cancellationToken)
    {
        var result = await _lists.GetListAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value.ToResponse()) : this.ToProblem(result);
    }

    [HttpPost]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListResponse>> CreateList(CreateGroceryListRequest request, CancellationToken cancellationToken)
    {
        var result = await _lists.CreateListAsync(request.Name, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetList), new { id = result.Value.Id }, result.Value.ToResponse())
            : this.ToProblem(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListResponse>> UpdateList(Guid id, UpdateGroceryListRequest request, CancellationToken cancellationToken)
    {
        var result = await _lists.RenameListAsync(id, request.Name, cancellationToken);
        return result.IsSuccess ? Ok(result.Value.ToResponse()) : this.ToProblem(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteList(Guid id, CancellationToken cancellationToken)
    {
        var result = await _lists.DeleteListAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }
}
