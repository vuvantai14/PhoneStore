using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Web.Models;

public sealed record CartLineViewModel(CartItem Item, CartProduct? Product)
{
    public decimal LineTotal => (Product?.Price ?? 0) * Item.Quantity;
    public bool Available => Product is { IsActive: true } && Item.Quantity <= Product.StockQuantity;
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

