using PhoneStore.Models.Enums;

namespace PhoneStore.Data.Reporting;

internal static class SalesOrderStatuses
{
    internal static readonly OrderStatus[] Values =
        [OrderStatus.Confirmed, OrderStatus.Shipping, OrderStatus.Completed];
}
