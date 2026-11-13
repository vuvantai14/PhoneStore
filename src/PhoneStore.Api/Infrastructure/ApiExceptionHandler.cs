using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PhoneStore.Api.Infrastructure;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.RequestAborted.IsCancellationRequested)
            return true;

        logger.LogError("Unhandled API exception of type {ExceptionType}. TraceId: {TraceId}",
            exception.GetType().Name, httpContext.TraceIdentifier);
        var unavailable = exception is DbException or DbUpdateException;
        var status = unavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status,
            Title = unavailable ? "Service temporarily unavailable" : "Unexpected server error",
            Detail = unavailable
                ? "The data service is temporarily unavailable. Please try again later."
                : "An unexpected error occurred while processing the request.",
            Instance = httpContext.Request.Path
        };
        details.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (!await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = details
            }))
        {
            await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);
        }
        return true;
    }
}
