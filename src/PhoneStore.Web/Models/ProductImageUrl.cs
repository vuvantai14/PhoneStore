namespace PhoneStore.Web.Models;

public static class ProductImageUrl
{
    public static string LocalOrPlaceholder(string? image)
    {
        if (string.IsNullOrWhiteSpace(image) || image.StartsWith("//")
            || image.Contains(':') || image.Contains('\\'))
            return "/images/product-placeholder.svg";

        return "/" + image.TrimStart('~', '/');
    }
}
