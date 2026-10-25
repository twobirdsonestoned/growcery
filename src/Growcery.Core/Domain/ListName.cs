using System.Globalization;

namespace Growcery.Core.Domain;

public static class ListName
{
    public const int MaxLength = 100;

    // A blank name falls back to a date-based default, so starting a list needs no input (spec 002 D13).
    public static Result<string> Resolve(string? name, DateTimeOffset now)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result<string>.Success(Default(now));
        }

        if (trimmed.Length > MaxLength)
        {
            var errors = new ValidationErrors();
            errors.Add("name", $"Name must be at most {MaxLength} characters.");
            return Result<string>.Invalid(errors.ToDictionary());
        }

        return Result<string>.Success(trimmed);
    }

    public static string Default(DateTimeOffset now) =>
        "Shopping list " + now.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
