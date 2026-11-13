namespace PhoneStore.Api.Contracts.Products;

public sealed record PagedProductsResponse(
    IReadOnlyList<ProductSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
