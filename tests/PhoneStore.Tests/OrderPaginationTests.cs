using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Purchasing;
using PhoneStore.Web.Controllers;
using PhoneStore.Web.Models;

namespace PhoneStore.Tests;

public sealed class OrderPaginationTests
{
    [Fact]
    public void CustomerOrdersViewModel_Calculates_PageCount_And_Navigation_Correctly()
    {
        // 12 orders with PageSize = 5 -> 3 pages
        var vmPage1 = new CustomerOrdersViewModel
        {
            TotalCount = 12,
            PageNumber = 1,
            PageSize = 5
        };
        Assert.Equal(5, vmPage1.PageSize);
        Assert.Equal(3, vmPage1.TotalPages);
        Assert.False(vmPage1.HasPreviousPage);
        Assert.True(vmPage1.HasNextPage);

        var vmPage2 = new CustomerOrdersViewModel
        {
            TotalCount = 12,
            PageNumber = 2,
            PageSize = 5
        };
        Assert.Equal(3, vmPage2.TotalPages);
        Assert.True(vmPage2.HasPreviousPage);
        Assert.True(vmPage2.HasNextPage);

        var vmPage3 = new CustomerOrdersViewModel
        {
            TotalCount = 12,
            PageNumber = 3,
            PageSize = 5
        };
        Assert.Equal(3, vmPage3.TotalPages);
        Assert.True(vmPage3.HasPreviousPage);
        Assert.False(vmPage3.HasNextPage);

        // 5 or fewer orders -> 1 page
        var vmSinglePage = new CustomerOrdersViewModel
        {
            TotalCount = 5,
            PageNumber = 1,
            PageSize = 5
        };
        Assert.Equal(1, vmSinglePage.TotalPages);
        Assert.False(vmSinglePage.HasPreviousPage);
        Assert.False(vmSinglePage.HasNextPage);
    }

    [Fact]
    public void BuildFilterUrl_Preserves_Filters_And_Formats_Page_Correctly()
    {
        var filter = new OrderFilterViewModel
        {
            Status = OrderStatus.Pending,
            DateFilterType = "month",
            Month = 11,
            MonthYear = 2026
        };

        // Page 1 should not have page=1 in URL
        var urlPage1 = OrdersController.BuildFilterUrl(filter, targetPage: 1);
        Assert.DoesNotContain("page=", urlPage1);
        Assert.Contains("status=Pending", urlPage1);
        Assert.Contains("dateFilterType=month", urlPage1);
        Assert.Contains("month=11", urlPage1);
        Assert.Contains("monthYear=2026", urlPage1);

        // Page 2 should include page=2 and keep all other parameters
        var urlPage2 = OrdersController.BuildFilterUrl(filter, targetPage: 2);
        Assert.Contains("page=2", urlPage2);
        Assert.Contains("status=Pending", urlPage2);
        Assert.Contains("month=11", urlPage2);
    }

    [Fact]
    public async Task CustomerOrdersPagedAsync_Pages_At_Database_5_Per_Page_Ordered_Newest_First()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer1, product) = await test.SeedCatalogAsync();

        // Create second customer to verify data isolation
        var customerRoleId = customer1.RoleId;
        var customer2 = new User
        {
            FullName = "Khách hàng 2",
            Email = "customer2@test.local",
            PasswordHash = "hashed",
            IsActive = true,
            RoleId = customerRoleId
        };
        test.Db.Users.Add(customer2);
        await test.Db.SaveChangesAsync();

        // Seed 12 orders for customer 1 with different dates
        var baseDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 1; i <= 12; i++)
        {
            test.Db.Orders.Add(new Order
            {
                UserId = customer1.UserId,
                OrderCode = $"ORD-C1-{i:D2}",
                OrderDate = baseDate.AddHours(i),
                ReceiverName = customer1.FullName,
                ReceiverPhone = "0900000001",
                ShippingAddress = "Địa chỉ 1",
                Status = i % 2 == 0 ? OrderStatus.Confirmed : OrderStatus.Pending,
                TotalAmount = product.Price,
                OrderDetails = [new OrderDetail { ProductId = product.ProductId, Quantity = 1, UnitPrice = product.Price }]
            });
        }

        // Seed 3 orders for customer 2
        for (int i = 1; i <= 3; i++)
        {
            test.Db.Orders.Add(new Order
            {
                UserId = customer2.UserId,
                OrderCode = $"ORD-C2-{i:D2}",
                OrderDate = baseDate.AddHours(i),
                ReceiverName = customer2.FullName,
                ReceiverPhone = "0900000002",
                ShippingAddress = "Địa chỉ 2",
                Status = OrderStatus.Pending,
                TotalAmount = product.Price,
                OrderDetails = [new OrderDetail { ProductId = product.ProductId, Quantity = 1, UnitPrice = product.Price }]
            });
        }
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();

        var store = new PurchaseStore(test.Db);

        // Page 1: 5 newest orders
        var page1 = await store.CustomerOrdersPagedAsync(customer1.UserId, null, null, null, page: 1, pageSize: 5, default);
        Assert.Equal(12, page1.TotalCount);
        Assert.Equal(5, page1.Orders.Count);
        Assert.Equal(1, page1.PageNumber);
        Assert.Equal(5, page1.PageSize);
        Assert.Equal(3, page1.TotalPages);
        // Newest first: order 12 down to order 8
        Assert.Equal("ORD-C1-12", page1.Orders[0].OrderCode);
        Assert.Equal("ORD-C1-08", page1.Orders[4].OrderCode);

        // Page 2: next 5 orders (order 7 down to order 3)
        var page2 = await store.CustomerOrdersPagedAsync(customer1.UserId, null, null, null, page: 2, pageSize: 5, default);
        Assert.Equal(5, page2.Orders.Count);
        Assert.Equal(2, page2.PageNumber);
        Assert.Equal("ORD-C1-07", page2.Orders[0].OrderCode);
        Assert.Equal("ORD-C1-03", page2.Orders[4].OrderCode);

        // Check no overlap between page 1 and page 2
        var p1Codes = page1.Orders.Select(o => o.OrderCode).ToHashSet();
        Assert.DoesNotContain(page2.Orders, o => p1Codes.Contains(o.OrderCode));

        // Page 3: remaining 2 orders (order 2 and order 1)
        var page3 = await store.CustomerOrdersPagedAsync(customer1.UserId, null, null, null, page: 3, pageSize: 5, default);
        Assert.Equal(2, page3.Orders.Count);
        Assert.Equal(3, page3.PageNumber);
        Assert.Equal("ORD-C1-02", page3.Orders[0].OrderCode);
        Assert.Equal("ORD-C1-01", page3.Orders[1].OrderCode);

        // Clamping: page 99 should normalize to page 3
        var page99 = await store.CustomerOrdersPagedAsync(customer1.UserId, null, null, null, page: 99, pageSize: 5, default);
        Assert.Equal(3, page99.PageNumber);
        Assert.Equal(2, page99.Orders.Count);

        // Clamping: page -5 should normalize to page 1
        var pageNeg = await store.CustomerOrdersPagedAsync(customer1.UserId, null, null, null, page: -5, pageSize: 5, default);
        Assert.Equal(1, pageNeg.PageNumber);
        Assert.Equal(5, pageNeg.Orders.Count);

        // Customer isolation: customer 2 only gets their 3 orders
        var c2Orders = await store.CustomerOrdersPagedAsync(customer2.UserId, null, null, null, page: 1, pageSize: 5, default);
        Assert.Equal(3, c2Orders.TotalCount);
        Assert.Equal(3, c2Orders.Orders.Count);
        Assert.Equal(1, c2Orders.TotalPages);
        Assert.All(c2Orders.Orders, o => Assert.StartsWith("ORD-C2-", o.OrderCode));
    }
}
