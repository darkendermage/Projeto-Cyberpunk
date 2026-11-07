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

/// <summary>The caller is known but may not do this (a player editing another seat's image). Mapped to HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>The caller did this too often (a seat's image uploads per hour). Mapped to HTTP 429.</summary>
public sealed class TooManyRequestsException(string message) : Exception(message);

/// <summary>The request has no valid credential (a virtual table seat link that is wrong or revoked). Mapped to HTTP 401.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
