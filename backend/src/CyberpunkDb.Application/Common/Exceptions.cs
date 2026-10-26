using CyberpunkDb.Application.Localization;
namespace CyberpunkDb.Application.Common;

/// <summary>Request is well-formed JSON but semantically invalid. Mapped to HTTP 400 with per-field errors.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception(L.T("Um ou mais campos são inválidos.", "One or more fields are invalid."))
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);
