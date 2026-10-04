namespace PhoneStore.Business.Management;

public interface IProductImageStorage
{
    Task<string> StoreAsync(string sourceFile, CancellationToken cancellationToken = default);
    bool TryDelete(string imageUrl);
}
