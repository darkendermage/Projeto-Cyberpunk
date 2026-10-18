namespace CyberpunkDb.Application.Common;

/// <summary>Request is well-formed JSON but semantically invalid. Mapped to HTTP 400 with per-field errors.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("Um ou mais campos são inválidos.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);
