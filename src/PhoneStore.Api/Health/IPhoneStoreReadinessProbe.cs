namespace PhoneStore.Api.Health;

public interface IPhoneStoreReadinessProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
