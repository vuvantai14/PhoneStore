using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Business.Purchasing;
using PhoneStore.Business.Management;
using PhoneStore.Models.Enums;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Controllers;

[Authorize(Policy = "Customer"), Route("orders")]
public sealed class OrdersController(IOrderService orders, IOrderManagementService management) : Controller
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private static readonly TimeZoneInfo VietnamTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] OrderFilterViewModel? filter, CancellationToken ct)
    {
        filter ??= new OrderFilterViewModel();
        try
        {
            var (isValid, errorMessage, fromUtc, toUtc) = ValidateAndResolveDateRange(filter, VietnamTz);
            if (!isValid)
            {
                return View(new CustomerOrdersViewModel
                {
                    Orders = [],
                    Filter = filter,
                    ErrorMessage = errorMessage
                });
            }

            var result = await orders.GetCustomerOrdersAsync(UserId, filter.Status, fromUtc, toUtc, ct);
            return View(new CustomerOrdersViewModel
            {
                Orders = result,
                Filter = filter
            });
        }
        catch (DbException) { return StatusCode(503, "Chưa thể tải danh sách đơn hàng. Vui lòng thử lại sau."); }
    }

    private static (bool IsValid, string? ErrorMessage, DateTime? FromUtc, DateTime? ToExclusiveUtc) ValidateAndResolveDateRange(OrderFilterViewModel filter, TimeZoneInfo tz)
    {
        switch (filter.DateFilterType?.ToLowerInvariant())
        {
            case "day":
                if (string.IsNullOrWhiteSpace(filter.Day))
                    return (false, "Vui lòng chọn ngày cần lọc.", null, null);

                if (!DateOnly.TryParse(filter.Day, out var day))
                    return (false, "Ngày đã chọn không hợp lệ.", null, null);

                var startDayLocal = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                var endDayLocal = DateTime.SpecifyKind(day.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                return (true, null, TimeZoneInfo.ConvertTimeToUtc(startDayLocal, tz), TimeZoneInfo.ConvertTimeToUtc(endDayLocal, tz));

            case "month":
                if (!filter.Month.HasValue || !filter.MonthYear.HasValue)
                    return (false, "Vui lòng chọn đầy đủ tháng và năm cần lọc.", null, null);

                if (filter.Month is < 1 or > 12)
                    return (false, "Tháng không hợp lệ (phải từ 1 đến 12).", null, null);

                if (filter.MonthYear is < 2000 or > 2100)
                    return (false, "Năm không hợp lệ.", null, null);

                var m = filter.Month.Value;
                var y = filter.MonthYear.Value;
                var firstDay = new DateOnly(y, m, 1);
                var nextMonthFirstDay = firstDay.AddMonths(1);
                var startMonthLocal = DateTime.SpecifyKind(firstDay.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                var endMonthLocal = DateTime.SpecifyKind(nextMonthFirstDay.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                return (true, null, TimeZoneInfo.ConvertTimeToUtc(startMonthLocal, tz), TimeZoneInfo.ConvertTimeToUtc(endMonthLocal, tz));

            case "year":
                if (!filter.Year.HasValue)
                    return (false, "Vui lòng chọn năm cần lọc.", null, null);

                if (filter.Year is < 2000 or > 2100)
                    return (false, "Năm không hợp lệ.", null, null);

                var yr = filter.Year.Value;
                var startYearLocal = DateTime.SpecifyKind(new DateTime(yr, 1, 1, 0, 0, 0), DateTimeKind.Unspecified);
                var endYearLocal = DateTime.SpecifyKind(new DateTime(yr + 1, 1, 1, 0, 0, 0), DateTimeKind.Unspecified);
                return (true, null, TimeZoneInfo.ConvertTimeToUtc(startYearLocal, tz), TimeZoneInfo.ConvertTimeToUtc(endYearLocal, tz));

            case "range":
                if (string.IsNullOrWhiteSpace(filter.FromDate) || string.IsNullOrWhiteSpace(filter.ToDate))
                    return (false, "Vui lòng nhập đầy đủ Từ ngày và Đến ngày.", null, null);

                if (!DateOnly.TryParse(filter.FromDate, out var fromDate))
                    return (false, "Từ ngày không đúng định dạng ngày hợp lệ.", null, null);

                if (!DateOnly.TryParse(filter.ToDate, out var toDate))
                    return (false, "Đến ngày không đúng định dạng ngày hợp lệ.", null, null);

                if (fromDate > toDate)
                    return (false, "Từ ngày không được lớn hơn Đến ngày.", null, null);

                var startRangeLocal = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                var endRangeLocal = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                return (true, null, TimeZoneInfo.ConvertTimeToUtc(startRangeLocal, tz), TimeZoneInfo.ConvertTimeToUtc(endRangeLocal, tz));

            default:
                break;
        }

        return (true, null, null, null);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        try
        {
            var order = await orders.GetReceiptAsync(id, UserId, ct);
            return order is null ? NotFound() : View(order);
        }
        catch (DbException) { return StatusCode(503, "Chưa thể tải thông tin đơn hàng. Vui lòng thử lại sau."); }
    }

    [HttpPost("{id:int}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        // Do not expose whether an order belongs to another account.
        try
        {
            if (await orders.GetReceiptAsync(id, UserId, ct) is null) return NotFound();
            var result = await management.CancelForCustomerAsync(id, UserId, ct);
            TempData["OrderMessage"] = result.Succeeded ? "Đơn hàng đã được hủy." : result.Error;
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (DbException) { return StatusCode(503, "Chưa thể hủy đơn hàng. Vui lòng thử lại sau."); }
    }
}
