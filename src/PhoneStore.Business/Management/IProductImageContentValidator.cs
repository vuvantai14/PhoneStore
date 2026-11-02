namespace PhoneStore.Business.Management;

// Platform decoder is supplied by the desktop composition root. No signature-only fallback.
public interface IProductImageContentValidator
{
    void Validate(Stream content, string extension, CancellationToken cancellationToken = default);
}
