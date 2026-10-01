using System.ComponentModel.DataAnnotations;

namespace PhoneStore.Web.Models;

public sealed class RegisterViewModel
{
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên cần từ 2 đến 100 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get => _fullName; set => _fullName = value?.Trim() ?? string.Empty; }

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự.")]
    public string Email { get => _email; set => _email = value?.Trim() ?? string.Empty; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu cần từ 8 đến 128 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginViewModel
{
    private string _email = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự.")]
    public string Email { get => _email; set => _email = value?.Trim() ?? string.Empty; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(128, ErrorMessage = "Mật khẩu tối đa 128 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

