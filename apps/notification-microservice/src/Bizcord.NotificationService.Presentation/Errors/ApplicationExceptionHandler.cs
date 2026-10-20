using Bizcord.NotificationService.Application.Common;
using Bizcord.NotificationService.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bizcord.NotificationService.Presentation.Errors;

/// <summary>
/// Translates exceptions from the inner layers into HTTP responses, so controllers never
/// need try/catch and every error leaves the service as RFC 7807 problem details.
/// </summary>
public sealed class ApplicationExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApplicationExceptionHandler> _logger;

    public ApplicationExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ApplicationExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            DomainException => (StatusCodes.Status400BadRequest, "Invalid request"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while handling {Path}.", httpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation(
                "Request to {Path} rejected with {Status}: {Message}",
                httpContext.Request.Path,
                status,
                exception.Message);
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Internal errors never leak their message to the caller.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred. The incident has been logged."
                    : exception.Message,
                Instance = httpContext.Request.Path
            }
        });
    }
}
