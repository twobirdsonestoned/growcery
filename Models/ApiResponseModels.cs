namespace growcery.Models;

public sealed record AccountResponse(string Message, string UserName);

public sealed record ErrorResponse(string Error);

public sealed record ValidationError(string Code, string Description);

public sealed record ValidationErrorResponse(IEnumerable<ValidationError> Errors);