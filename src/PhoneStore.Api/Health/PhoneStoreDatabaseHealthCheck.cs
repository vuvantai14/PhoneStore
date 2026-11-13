using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PhoneStore.Api.Health;

public sealed class PhoneStoreDatabaseHealthCheck(
    IPhoneStoreReadinessProbe probe,
    ILogger<PhoneStoreDatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await probe.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("PhoneStore database readiness check failed with {ExceptionType}.",
                exception.GetType().Name);
            return HealthCheckResult.Unhealthy();
        }
    }
}
