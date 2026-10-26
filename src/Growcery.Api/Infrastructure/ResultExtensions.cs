using Growcery.Core;
using Microsoft.AspNetCore.Mvc;

namespace Growcery.Api.Infrastructure;

// Translates a failed business Result into an HTTP ProblemDetails response.
public static class ResultExtensions
{
    public static ActionResult ToProblem(this ControllerBase controller, Result result) => result.Status switch
    {
        // [ApiController] turns a bare 404 into ProblemDetails.
        ResultStatus.NotFound => controller.NotFound(),
        ResultStatus.Forbidden => controller.Problem(statusCode: StatusCodes.Status403Forbidden),
        ResultStatus.Invalid => controller.ValidationProblem(new ValidationProblemDetails(
            result.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))),
        _ => throw new InvalidOperationException($"A {result.Status} result is not a failure."),
    };
}
