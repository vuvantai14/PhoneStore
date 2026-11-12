using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Management;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;

namespace PhoneStore.Tests;

public sealed class ManagementOrderingTests
{
    [Fact]
    public async Task Management_lists_are_sorted_by_numeric_id_ascending_after_filters()
    {
        await using var test = await TestDatabase.CreateAsync();
        var customerRoleId = await test.Db.Roles.Where(role => role.RoleName == "Customer")
            .Select(role => role.RoleId).SingleAsync();

        var brands = new[]
        {
            new Brand { BrandId = 20, BrandName = "Brand Z", IsActive = true },
            new Brand { BrandId = 5, BrandName = "Brand A", IsActive = true }
        };
        var categories = new[]
        {
            new Category { CategoryId = 20, CategoryName = "Category Z", IsActive = true },
            new Category { CategoryId = 5, CategoryName = "Category A", IsActive = true }
        };
        var customers = new[]
        {
            new User { UserId = 20, RoleId = customerRoleId, FullName = "Customer Z", Email = "z@test.local", PasswordHash = "hash", IsActive = true },
            new User { UserId = 5, RoleId = customerRoleId, FullName = "Customer A", Email = "a@test.local", PasswordHash = "hash", IsActive = true }
        };
        var products = new[]
        {
            new Product { ProductId = 20, ProductName = "Product Z", BrandId = 20, CategoryId = 20, Price = 20, StockQuantity = 1, IsActive = true },
            new Product { ProductId = 5, ProductName = "Product A", BrandId = 5, CategoryId = 5, Price = 5, StockQuantity = 1, IsActive = true }
        };
        var orders = new[]
        {
            new Order { OrderId = 20, UserId = 20, OrderCode = "ORD-20", ReceiverName = "Receiver Z", ReceiverPhone = "0900000020", ShippingAddress = "Address", OrderDate = DateTime.UtcNow.AddDays(-2), Status = OrderStatus.Pending },
            new Order { OrderId = 5, UserId = 5, OrderCode = "ORD-05", ReceiverName = "Receiver A", ReceiverPhone = "0900000005", ShippingAddress = "Address", OrderDate = DateTime.UtcNow, Status = OrderStatus.Pending }
        };
        test.Db.AddRange(brands);
        test.Db.AddRange(categories);
        test.Db.AddRange(customers);
        test.Db.AddRange(products);
        test.Db.AddRange(orders);
        await test.Db.SaveChangesAsync();

        var productRows = await new ProductManagementStore(test.Db).ListAsync(new(IsActive: true), default);
        var customerRows = await new CustomerManagementStore(test.Db).ListAsync(new(Search: null, IsActive: true), default);
        var orderRows = await new OrderManagementStore(test.Db).ListAsync(new(Search: null, Status: OrderStatus.Pending), default);
        var lookups = new LookupManagementStore(test.Db);
        var brandRows = await lookups.ListAsync(LookupKind.Brand, "Brand");
        var categoryRows = await lookups.ListAsync(LookupKind.Category, "Category");

        Assert.Equal([5, 20], productRows.Select(row => row.ProductId));
        Assert.Equal([5, 20], customerRows.Select(row => row.UserId));
        Assert.Equal([5, 20], orderRows.Select(row => row.OrderId));
        Assert.Equal([5, 20], brandRows.Select(row => row.Id));
        Assert.Equal([5, 20], categoryRows.Select(row => row.Id));
    }
}
