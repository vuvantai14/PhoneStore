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

    [Fact]
    public async Task V1_admin_customer_update_edits_profile_fields_only_and_protects_admin_and_sensitive_fields()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var admin = Account("Operator", 1);
        var customer = Account("Buyer", 2);
        customer.Address = "Old Address";
        fixture.Db.AddRange(admin, customer);
        await fixture.Db.SaveChangesAsync();

        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());

        var v1Route = "/api/v1/admin/customers";
        // List includes Address and CreatedAt
        var page = await client.GetFromJsonAsync<CustomerPage>(v1Route);
        var summary = Assert.Single(page!.Items);
        Assert.Equal("Old Address", summary.Address);

        // Update customer profile fields -> 204
        var putResp = await client.PutAsJsonAsync($"{v1Route}/{customer.UserId}", new AdminCustomerUpdateRequest
        {
            FullName = "Updated Buyer",
            Phone = "0912345678",
            Address = "456 New Street"
        });
        Assert.Equal(HttpStatusCode.NoContent, putResp.StatusCode);

        var updated = await client.GetFromJsonAsync<CustomerDetail>($"{v1Route}/{customer.UserId}");
        Assert.Equal("Updated Buyer", updated!.FullName);
        Assert.Equal("0912345678", updated.Phone);
        Assert.Equal("456 New Street", updated.Address);

        // Unmapped sensitive fields (roleId, passwordHash, email) -> 400
        foreach (var badJson in new[]
                 {
                     """{"fullName":"Hack","roleId":1}""",
                     """{"fullName":"Hack","passwordHash":"evil"}""",
                     """{"fullName":"Hack","email":"new@example.test"}""",
                     """{"fullName":"   "}"""
                 })
        {
            using var content = new StringContent(badJson, System.Text.Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"{v1Route}/{customer.UserId}", content)).StatusCode);
        }

        // Cannot edit Admin account or missing customer -> 404
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{v1Route}/{admin.UserId}",
            new AdminCustomerUpdateRequest { FullName = "Nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{v1Route}/99999",
            new AdminCustomerUpdateRequest { FullName = "Nope" })).StatusCode);

        // Verify Single Admin & PasswordHash untouched
        var allUsers = await fixture.Db.Users.AsNoTracking().ToListAsync();
        Assert.Single(allUsers, u => u.RoleId == 1);
        Assert.All(allUsers, u => Assert.Equal("unused", u.PasswordHash));
    }

    private static User Account(string name, int role) => new() { FullName = name, Email = name + "@example.test",
        PasswordHash = "unused", RoleId = role, IsActive = true };
}
