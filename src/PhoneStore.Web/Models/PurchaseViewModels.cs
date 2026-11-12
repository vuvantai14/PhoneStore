using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Web.Models;

public sealed record CartLineViewModel(CartItem Item, CartProduct? Product)
{
    public decimal LineTotal => (Product?.Price ?? 0) * Item.Quantity;
    public bool Available => Item.Quantity > 0 && Product is { IsActive: true } && Item.Quantity <= Product.StockQuantity;
}
public sealed class CartViewModel
{
    public IReadOnlyList<CartLineViewModel> Items { get; init; } = [];
    public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    public long TotalQuantity => Items.Sum(i => (long)i.Item.Quantity);
    public bool CanCheckout => Items.Count > 0 && Items.All(i => i.Available);
}
public sealed class CheckoutViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên người nhận.")]
    [StringLength(100, ErrorMessage = "Tên người nhận tối đa 100 ký tự.")]
    [Display(Name = "Tên người nhận")]
    public string ReceiverName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    [Display(Name = "Số điện thoại")]
    public string ReceiverPhone { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng.")]
    [StringLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
    [Display(Name = "Địa chỉ giao hàng")]
    public string ShippingAddress { get; set; } = string.Empty;
    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }
    public string? SubmissionToken { get; set; }
    [BindNever] public CartViewModel Cart { get; set; } = new();
}

public sealed class OrderFilterViewModel
{
    public OrderStatus? Status { get; set; }
    public string DateFilterType { get; set; } = "all";
    public string? Day { get; set; }
    public int? Month { get; set; }
    public int? MonthYear { get; set; }
    public int? Year { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public int Page { get; set; } = 1;

    public bool HasFilters =>
        Status.HasValue ||
        (!string.IsNullOrWhiteSpace(DateFilterType) && DateFilterType != "all" &&
         (DateFilterType == "day" && !string.IsNullOrWhiteSpace(Day)
          || DateFilterType == "month" && Month.HasValue && MonthYear.HasValue
          || DateFilterType == "year" && Year.HasValue
          || DateFilterType == "range" && (!string.IsNullOrWhiteSpace(FromDate) || !string.IsNullOrWhiteSpace(ToDate))));
}

public sealed class CustomerOrdersViewModel
{
    public IReadOnlyList<CustomerOrderRow> Orders { get; init; } = [];
    public OrderFilterViewModel Filter { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public int TotalCount { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int CurrentPage => PageNumber;
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

