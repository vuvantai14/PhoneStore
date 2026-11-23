using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Data.Context;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class SharedProductUploadsIntegrationTests
{
    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAQSURBVBhXY/jPAEIUAAYGAILBAf8lQkk6AAAAAElFTkSuQmCC");

    [Fact]
    public void Development_configuration_resolves_shared_customer_web_uploads_directory_portably()
    {
        var repoRoot = FindRepositoryRoot();
        var apiRoot = Path.Combine(repoRoot, "src", "PhoneStore.Api");
        var devConfigPath = Path.Combine(apiRoot, "appsettings.Development.json");
        Assert.True(File.Exists(devConfigPath), $"Expected {devConfigPath} to exist.");

        using var doc = JsonDocument.Parse(File.ReadAllText(devConfigPath));
        var configured = doc.RootElement
            .GetProperty("ProductImages")
            .GetProperty("UploadsDirectory")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(configured));
        Assert.False(Path.IsPathRooted(configured!), "UploadsDirectory in appsettings.Development.json must be a portable relative path.");

        var resolved = Path.GetFullPath(configured!, apiRoot);
        var expected = Path.GetFullPath(Path.Combine(repoRoot, "src", "PhoneStore.Web", "wwwroot", "uploads", "products"));
        Assert.Equal(expected, resolved, ignoreCase: OperatingSystem.IsWindows());
    }

    [Fact]
    public async Task Api_uploaded_product_image_is_served_by_customer_web_from_shared_directory_and_rejects_traversal()
    {
        var tempWebRoot = Path.Combine(Path.GetTempPath(), "PhoneStore-SharedUploads-" + Guid.NewGuid().ToString("N"));
        var sharedUploads = Path.Combine(tempWebRoot, "uploads", "products");
        Directory.CreateDirectory(sharedUploads);

        await using var fixture = await TestDatabase.CreateAsync();
        var (_, product) = await fixture.SeedCatalogAsync();
        var admin = new User
        {
            FullName = "Uploads Fixture Admin",
            Email = "uploads-admin@test.local",
            PasswordHash = "unused",
            RoleId = SystemRoles.AdminId,
            IsActive = true
        };
        fixture.Db.Users.Add(admin);
        await fixture.Db.SaveChangesAsync();

        try
        {
            using var apiHost = ApiAuthorizationTests.Host(fixture.Db).WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ProductImages:UploadsDirectory", sharedUploads);
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProductImages:UploadsDirectory"] = sharedUploads
                }));
            });
            using var apiClient = apiHost.CreateClient();
            apiClient.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());

            await using var webHost = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseWebRoot(tempWebRoot);
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["CatalogApi:Enabled"] = "false"
                }));
                builder.ConfigureServices(services =>
                {
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                    services.RemoveAll<DbContextOptions<PhoneStoreDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<PhoneStoreDbContext>>();
                    services.RemoveAll<PhoneStoreDbContext>();
                    services.AddSingleton(fixture.Db);
                });
            });
            using var webClient = webHost.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

            // 1. Path traversal filename is rejected and creates no file
            using (var traversalForm = CreateMultipart("../escape.png", ValidPngBytes, "image/png"))
            {
                using var traversalResp = await apiClient.PostAsync($"/api/v1/admin/products/{product.ProductId}/images", traversalForm);
                Assert.Equal(HttpStatusCode.BadRequest, traversalResp.StatusCode);
            }
            Assert.Empty(Directory.GetFiles(sharedUploads));
            Assert.False(File.Exists(Path.Combine(tempWebRoot, "uploads", "escape.png")));

            // 2. Upload valid PNG via API using real Program.cs ProductImages:UploadsDirectory binding
            using var pngForm = CreateMultipart("shared-sync.png", ValidPngBytes, "image/png");
            using var uploadResp = await apiClient.PostAsync($"/api/v1/admin/products/{product.ProductId}/images", pngForm);
            Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);
            var uploaded = (await uploadResp.Content.ReadFromJsonAsync<AdminProductImageUploadResponse>())!;
            Assert.Matches("^/uploads/products/[0-9a-f]{32}\\.png$", uploaded.ImageUrl);

            var expectedDiskPath = Path.Combine(sharedUploads, Path.GetFileName(uploaded.ImageUrl));
            Assert.True(File.Exists(expectedDiskPath), "API must save image to configured shared uploads directory.");

            // 3. Customer Web serves the uploaded image statically with exact byte match
            using var staticResp = await webClient.GetAsync(uploaded.ImageUrl);
            Assert.Equal(HttpStatusCode.OK, staticResp.StatusCode);
            Assert.Equal("image/png", staticResp.Content.Headers.ContentType?.MediaType);
            Assert.Equal(ValidPngBytes, await staticResp.Content.ReadAsByteArrayAsync());

            // 4. Customer Web product detail HTML references the newly uploaded image
            fixture.Db.ChangeTracker.Clear();
            using var detailResp = await webClient.GetAsync($"/products/{product.ProductId}");
            Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
            var html = await detailResp.Content.ReadAsStringAsync();
            Assert.Contains(uploaded.ImageUrl, html, StringComparison.Ordinal);

            // 5. Removing image via API unlinks it from DB so Customer Web product detail no longer references it
            using var deleteResp = await apiClient.DeleteAsync($"/api/v1/admin/products/{product.ProductId}/images/{uploaded.ImageId}");
            Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
            var remainingImages = await apiClient.GetFromJsonAsync<AdminProductImageItem[]>($"/api/v1/admin/products/{product.ProductId}/images");
            Assert.Empty(remainingImages!);
            fixture.Db.ChangeTracker.Clear();
            using var detailAfterRemove = await webClient.GetAsync($"/products/{product.ProductId}");
            Assert.Equal(HttpStatusCode.OK, detailAfterRemove.StatusCode);
            var htmlAfterRemove = await detailAfterRemove.Content.ReadAsStringAsync();
            Assert.DoesNotContain(uploaded.ImageUrl, htmlAfterRemove, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempWebRoot))
                Directory.Delete(tempWebRoot, recursive: true);
        }
    }

    private static MultipartFormDataContent CreateMultipart(string fileName, byte[] content, string contentType)
    {
        var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(content);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(bytes, "file", fileName);
        return form;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PhoneStore.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate PhoneStore.sln repository root.");
    }
}
