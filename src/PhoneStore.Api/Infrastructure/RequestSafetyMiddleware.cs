using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace PhoneStore.Api.Infrastructure;

public sealed partial class RequestSafetyMiddleware(
    RequestDelegate next,
    ILogger<RequestSafetyMiddleware> logger,
    ApiOperationalOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public const string CorrelationHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.QueryString.HasValue &&
            context.Request.QueryString.Value!.Length > options.MaxQueryStringLength)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                "Request query is too long", "The query string exceeds the allowed length.");
            return;
        }

        var requestTargetLength = context.Request.Path.Value?.Length ?? 0;
        requestTargetLength += context.Request.QueryString.Value?.Length ?? 0;
        if (requestTargetLength > options.MaxRequestLineSize)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                "Request target is too long", "The request target exceeds the allowed length.");
            return;
        }

        var suppliedCorrelation = context.Request.Headers[CorrelationHeader].FirstOrDefault();
        if (IsValidCorrelationId(suppliedCorrelation))
            context.TraceIdentifier = suppliedCorrelation!;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        var stopwatch = Stopwatch.StartNew();
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = context.TraceIdentifier
        });
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {DurationMs} ms. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                context.TraceIdentifier);
        }
    }

    private static bool IsValidCorrelationId(string? value) =>
        value is { Length: >= 1 and <= 64 } && CorrelationIdPattern().IsMatch(value);

    internal static async Task WriteProblemAsync(
        HttpContext context, int status, string title, string detail, int? retryAfterSeconds = null)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        if (retryAfterSeconds.HasValue)
            context.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions, CancellationToken.None);
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();
}
