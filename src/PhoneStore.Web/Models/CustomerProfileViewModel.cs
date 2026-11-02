using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PhoneStore.Web.Models;

public sealed class CustomerProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, MinimumLength = 2,
        ErrorMessage = "Họ tên phải có từ 2 đến 100 ký tự."), Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [BindNever]
    public string Email { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Số điện thoại không vượt quá 20 ký tự."),
        Phone(ErrorMessage = "Số điện thoại không hợp lệ."), Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [StringLength(255, ErrorMessage = "Địa chỉ không vượt quá 255 ký tự."), Display(Name = "Địa chỉ")]
    public string? Address { get; set; }
}
