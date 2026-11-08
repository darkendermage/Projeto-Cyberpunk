using CyberpunkDb.Application.Localization;
namespace CyberpunkDb.Application.Common;

// valid JSON but semantically wrong -> 400 with per-field errors
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception(L.T("Um ou mais campos são inválidos.", "One or more fields are invalid."))
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

// -> 403
public sealed class ForbiddenException(string message) : Exception(message);

// -> 429
public sealed class TooManyRequestsException(string message) : Exception(message);

// bad or revoked seat link -> 401
public sealed class UnauthorizedException(string message) : Exception(message);
