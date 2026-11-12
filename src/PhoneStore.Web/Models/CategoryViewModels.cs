namespace PhoneStore.Web.Models;

public sealed record CategoryItemViewModel(
    int Id,
    string Name,
    string? Description,
    int ProductCount
);

public sealed class CategoriesViewModel
{
    public IReadOnlyList<CategoryItemViewModel> Categories { get; init; } = [];
}
