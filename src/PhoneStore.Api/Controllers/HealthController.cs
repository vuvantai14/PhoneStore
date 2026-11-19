using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PhoneStore.Api.Contracts;
using PhoneStore.Api.Health;
using PhoneStore.Api.Infrastructure;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/health")]
[EnableRateLimiting(ApiPolicies.HealthRateLimit)]
[RequestTimeout(ApiPolicies.HealthTimeout)]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("live")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Live() => Ok(new HealthResponse("healthy"));

    [AllowAnonymous]
    [HttpGet]
    [HttpGet("ready")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Name == HealthCheckNames.Database,
            cancellationToken);
        if (report.Status == HealthStatus.Healthy)
            return Ok(new HealthResponse("healthy"));

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Service temporarily unavailable",
            Detail = "The data service health check did not succeed.",
            Instance = Request.Path
        };
        problem.Extensions["health"] = "unhealthy";
        var response = new ObjectResult(problem) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }
}
