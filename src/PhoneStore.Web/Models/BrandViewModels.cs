namespace PhoneStore.Web.Models;

public sealed record BrandItemViewModel(
    int Id,
    string Name,
    string? Description,
    int ProductCount,
    string? LogoUrl = null
);

public sealed class BrandsViewModel
{
    public IReadOnlyList<BrandItemViewModel> Brands { get; init; } = [];
}

public static class BrandLogoMap
{
    private static readonly Dictionary<string, string> Logos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Apple"] = "/images/brands/apple.svg",
        ["Google"] = "/images/brands/google.svg",
        ["OnePlus"] = "/images/brands/oneplus.svg",
        ["OPPO"] = "/images/brands/oppo.svg",
        ["realme"] = "/images/brands/realme.svg",
        ["Samsung"] = "/images/brands/samsung.svg",
        ["vivo"] = "/images/brands/vivo.svg",
        ["Xiaomi"] = "/images/brands/xiaomi.svg"
    };

    public static string? GetLogoPath(string brandName) =>
        Logos.TryGetValue(brandName, out var file) ? file : null;

    public static string GetCssSlug(string brandName) =>
        brandName.ToLowerInvariant() switch
        {
            "apple" => "apple",
            "google" => "google",
            "oneplus" => "oneplus",
            "oppo" => "oppo",
            "realme" => "realme",
            "samsung" => "samsung",
            "vivo" => "vivo",
            "xiaomi" => "xiaomi",
            _ => "default"
        };
}
