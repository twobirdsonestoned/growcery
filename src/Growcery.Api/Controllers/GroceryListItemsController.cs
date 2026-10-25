using Growcery.Api.Contracts;
using Growcery.Api.Infrastructure;
using Growcery.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Growcery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lists/{listId:guid}/items")]
[Consumes("application/json")]
public class GroceryListItemsController : ControllerBase
{
    private readonly IGroceryListItemService _items;

    public GroceryListItemsController(IGroceryListItemService items)
    {
        _items = items;
    }

    [HttpGet]
    [ProducesResponseType<IEnumerable<GroceryListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<GroceryListItemResponse>>> GetItems(Guid listId, CancellationToken cancellationToken)
    {
        var result = await _items.GetItemsAsync(listId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value.Select(item => item.ToResponse())) : this.ToProblem(result);
    }

    [HttpGet("{itemId:guid}")]
    [ProducesResponseType<GroceryListItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroceryListItemResponse>> GetItem(Guid listId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _items.GetItemAsync(listId, itemId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value.ToResponse()) : this.ToProblem(result);
    }

    [HttpPost]
    [ProducesResponseType<GroceryListItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListItemResponse>> CreateItem(Guid listId, CreateGroceryListItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _items.AddItemAsync(listId, new ItemInput(request.Name, request.Quantity, request.Unit), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetItem), new { listId, itemId = result.Value.Id }, result.Value.ToResponse())
            : this.ToProblem(result);
    }

    [HttpPut("{itemId:guid}")]
    [ProducesResponseType<GroceryListItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<GroceryListItemResponse>> UpdateItem(Guid listId, Guid itemId, UpdateGroceryListItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _items.UpdateItemAsync(
            listId, itemId, new ItemInput(request.Name, request.Quantity, request.Unit), request.IsPurchased, cancellationToken);
        return result.IsSuccess ? Ok(result.Value.ToResponse()) : this.ToProblem(result);
    }

    [HttpDelete("{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(Guid listId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _items.RemoveItemAsync(listId, itemId, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }
}
