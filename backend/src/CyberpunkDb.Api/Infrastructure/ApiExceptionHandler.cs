using CyberpunkDb.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Api.Infrastructure;

// application exceptions -> RFC 7807 problem responses
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem = exception switch
        {
            RequestValidationException v => new ValidationProblemDetails(v.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = v.Message,
            },
            NotFoundException n => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = n.Message },
            UnauthorizedException u => new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = u.Message },
            ConflictException c => new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = c.Message },
            ForbiddenException f => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = f.Message },
            TooManyRequestsException m => new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = m.Message },
            BadHttpRequestException b => new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = b.Message },
            OperationCanceledException => new ProblemDetails { Status = 499, Title = L.T("Requisição cancelada.", "Request canceled.") },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = L.T("Falha inesperada no servidor. Tente novamente.", "Unexpected server failure. Try again."),
            },
        };

        if (problem.Status >= 500) logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);

        context.Response.StatusCode = problem.Status ?? 500;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
