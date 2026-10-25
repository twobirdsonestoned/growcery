namespace Growcery.Core;

public enum ResultStatus
{
    Ok,
    NotFound,
    Forbidden,
    Invalid,
}

// The outcome of a business operation. Deliberately free of HTTP: the presentation layer decides how to show it.
public class Result
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    protected Result(ResultStatus status, IReadOnlyDictionary<string, string[]>? errors)
    {
        Status = status;
        Errors = errors ?? NoErrors;
    }

    public ResultStatus Status { get; }

    // Validation errors keyed by field name (camelCase, matching the API's JSON). Empty unless Status is Invalid.
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result Success() => new(ResultStatus.Ok, null);

    public static Result NotFound() => new(ResultStatus.NotFound, null);

    public static Result Forbidden() => new(ResultStatus.Forbidden, null);

    public static Result Invalid(IReadOnlyDictionary<string, string[]> errors) => new(ResultStatus.Invalid, errors);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(ResultStatus status, T? value, IReadOnlyDictionary<string, string[]>? errors)
        : base(status, errors)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A {Status} result has no value.");

    public static Result<T> Success(T value) => new(ResultStatus.Ok, value, null);

    public static new Result<T> NotFound() => new(ResultStatus.NotFound, default, null);

    public static new Result<T> Forbidden() => new(ResultStatus.Forbidden, default, null);

    public static new Result<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(ResultStatus.Invalid, default, errors);

    // Carries a failed result of another type through unchanged.
    public static Result<T> FailureFrom(Result failure) =>
        failure.IsSuccess
            ? throw new ArgumentException("Only a failed result can be converted.", nameof(failure))
            : new(failure.Status, default, failure.Errors);
}

internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = new List<string>();
            _errors[field] = messages;
        }

        messages.Add(message);
    }

    public IReadOnlyDictionary<string, string[]> ToDictionary() =>
        _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
}
