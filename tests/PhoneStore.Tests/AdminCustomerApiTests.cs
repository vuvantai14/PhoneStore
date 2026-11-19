using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Api.Contracts.Customers;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class AdminCustomerApiTests
{
    private const string Route = "/api/admin/customers";

    [Fact]
    public async Task All_routes_require_active_admin()
    {
        await using var db = await TestDatabase.CreateAsync();
        var admin = Account("Operator", 1);
        var customer = Account("Customer", 2);
        db.Db.AddRange(admin, customer);
        await db.Db.SaveChangesAsync();
        using var anonymousHost = ApiAuthorizationTests.Host();
        using var anon = anonymousHost.CreateClient();
        await Denied(anon, customer.UserId, HttpStatusCode.Unauthorized);
        using var host = ApiAuthorizationTests.Host(db.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", customer.UserId.ToString());
        await Denied(client, customer.UserId, HttpStatusCode.Forbidden);
        await db.Db.Users.Where(x => x.UserId == admin.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        client.DefaultRequestHeaders.Remove("Fixture-Subject");
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());
        await Denied(client, customer.UserId, HttpStatusCode.Forbidden);
        Assert.True((await db.Db.Users.AsNoTracking().SingleAsync(x => x.UserId == customer.UserId)).IsActive);
    }

    private static async Task Denied(HttpClient client, int id, HttpStatusCode status)
    {
        foreach (var (method, path) in new[] { (HttpMethod.Get, Route), (HttpMethod.Get, $"{Route}/{id}"),
            (HttpMethod.Patch, $"{Route}/{id}/status") })
        {
            using var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Patch) request.Content = JsonContent.Create(new { isActive = false });
            using var response = await client.SendAsync(request);
            Assert.Equal(status, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Active_admin_lists_customers_and_updates_only_customer_status()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var admin = Account("Operator", 1);
        var first = Account("Buyer", 2);
        var last = Account("Reader", 2);
        last.Phone = "555999";
        fixture.Db.AddRange(admin, first, last);
        await fixture.Db.SaveChangesAsync();
        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());

        var page = await client.GetFromJsonAsync<CustomerPage>($"{Route}?page=2&pageSize=1");
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(last.UserId, Assert.Single(page.Items).UserId);
        var item = $"{Route}/{last.UserId}";
        using var detail = JsonDocument.Parse(await client.GetStringAsync(item));
        Assert.Equal(new[] { "address", "createdAt", "email", "fullName", "isActive", "phone", "userId" },
            detail.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

        foreach (var id in new[] { admin.UserId, int.MaxValue })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"{Route}/{id}/status", new { isActive = false })).StatusCode);
        }
        foreach (var body in new object[] { new { }, new { isActive = "false" }, new { isActive = false, roleId = 1 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(item + "/status", body)).StatusCode);
        Assert.True((await client.GetFromJsonAsync<CustomerDetail>(item))!.IsActive);
        foreach (var active in new[] { false, false, true })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(item + "/status", new { isActive = active })).StatusCode);
            var filtered = await client.GetFromJsonAsync<CustomerPage>($"{Route}?search=5999&isActive={active}");
            Assert.Equal(last.UserId, Assert.Single(filtered!.Items).UserId);
            Assert.Equal(active, (await client.GetFromJsonAsync<CustomerDetail>(item))!.IsActive);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?pageSize=0")).StatusCode);
        var users = await fixture.Db.Users.AsNoTracking().ToListAsync();
        Assert.True(Assert.Single(users, u => u.RoleId == 1).IsActive);
        Assert.All(users, u => Assert.Equal("unused", u.PasswordHash));
        Assert.Equal(2, users.Single(u => u.UserId == last.UserId).RoleId);
    }

    private static User Account(string name, int role) => new() { FullName = name, Email = name + "@example.test",
        PasswordHash = "unused", RoleId = role, IsActive = true };
}
