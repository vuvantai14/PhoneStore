using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Business.Management;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class AdminProductApiTests
{
    private const string LegacyProducts = "/api/admin/products";
    private const string V1Products = "/api/v1/admin/products";
    private const string V1Brands = "/api/v1/admin/brands";
    private const string V1Categories = "/api/v1/admin/categories";

    // Valid 4x4 8-bit RGBA PNG (same fixture used in AdminCrudTests)
    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAQSURBVBhXY/jPAEIUAAYGAILBAf8lQkk6AAAAAElFTkSuQmCC");

    // Valid 1x1 baseline JPEG with SOI, APP0, DQT, SOF0 (1x1, 1 component), DHT, SOS, entropy data, EOI
    private static readonly byte[] ValidJpegBytes = BuildValid1x1Jpeg();

    private static byte[] BuildValid1x1Jpeg()
    {
        var bytes = new List<byte>();
        bytes.AddRange([0xFF, 0xD8]); // SOI
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]); // APP0 JFIF
        bytes.AddRange([0xFF, 0xDB, 0x00, 0x43, 0x00]); // DQT (65-byte payload)
        bytes.AddRange(Enumerable.Repeat((byte)0x10, 64));
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00]); // SOF0 1x1 grayscale
        bytes.AddRange([0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]); // DHT DC
        bytes.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]); // SOS
        bytes.AddRange([0x7F]); // Entropy-coded scan data
        bytes.AddRange([0xFF, 0xD9]); // EOI
        return [.. bytes];
    }

    [Fact]
    public async Task All_management_operations_require_an_active_database_admin()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, product) = await fixture.SeedCatalogAsync();
        var admin = await AddAdmin(fixture, false);
        using var anonymousHost = ApiAuthorizationTests.Host();
        using var anonymous = anonymousHost.CreateClient();
        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var restricted = host.CreateClient();

        await CheckBoundary(anonymous, HttpStatusCode.Unauthorized);
        foreach (var user in new[] { customer, admin })
        {
            restricted.DefaultRequestHeaders.Remove("Fixture-Subject");
            restricted.DefaultRequestHeaders.Add("Fixture-Subject", user.UserId.ToString());
            await CheckBoundary(restricted, HttpStatusCode.Forbidden);
        }

        async Task CheckBoundary(HttpClient client, HttpStatusCode expected)
        {
            foreach (var baseRoute in new[] { LegacyProducts, V1Products })
            {
                var item = $"{baseRoute}/{product.ProductId}";
                foreach (var (method, url) in new[]
                         {
                             (HttpMethod.Get, baseRoute),
                             (HttpMethod.Get, $"{baseRoute}/options"),
                             (HttpMethod.Get, item),
                             (HttpMethod.Post, baseRoute),
                             (HttpMethod.Put, item),
                             (HttpMethod.Patch, item + "/status"),
                             (HttpMethod.Get, item + "/images"),
                             (HttpMethod.Post, item + "/images"),
                             (HttpMethod.Delete, item + "/images/1"),
                             (HttpMethod.Patch, item + "/images/1/primary"),
                             (HttpMethod.Patch, item + "/images/1/order")
                         })
                {
                    using var request = new HttpRequestMessage(method, url);
                    using var response = await client.SendAsync(request);
                    Assert.Equal(expected, response.StatusCode);
                    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                }
            }

            foreach (var lookupRoute in new[] { V1Brands, "/api/admin/brands", V1Categories, "/api/admin/categories" })
            {
                foreach (var (method, url) in new[]
                         {
                             (HttpMethod.Get, lookupRoute),
                             (HttpMethod.Post, lookupRoute),
                             (HttpMethod.Put, $"{lookupRoute}/1"),
                             (HttpMethod.Patch, $"{lookupRoute}/1/status")
                         })
                {
                    using var request = new HttpRequestMessage(method, url);
                    using var response = await client.SendAsync(request);
                    Assert.Equal(expected, response.StatusCode);
                    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                }
            }
        }
    }

    [Theory]
    [InlineData(LegacyProducts)]
    [InlineData(V1Products)]
    public async Task Admin_can_manage_products_with_filters_and_original_stock_protection(string collection)
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (_, existing) = await fixture.SeedCatalogAsync();
        var admin = await AddAdmin(fixture, true);
        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());
        var create = new ProductWriteRequest
        {
            ProductName = "Fixture phone", BrandId = existing.BrandId,
            CategoryId = existing.CategoryId, StockQuantity = 3, IsActive = true, Price = 12.50m
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(collection, create with { Price = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(collection, create with { Price = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(collection, create with { StockQuantity = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(collection, create with { BrandId = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(collection, create with { CategoryId = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{collection}/{int.MaxValue}")).StatusCode);

        using var created = await client.PostAsJsonAsync(collection, create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<AdminProductId>())!.ProductId;
        var item = $"{collection}/{id}";
        Assert.Equal(item, created.Headers.Location!.AbsolutePath);
        var detail = await client.GetFromJsonAsync<AdminProductDetail>(item);
        Assert.Equal(3, detail!.OriginalStockQuantity);

        var update = new ProductUpdateRequest
        {
            ProductName = "Edited phone", Price = 20,
            BrandId = create.BrandId, CategoryId = create.CategoryId, StockQuantity = 2,
            OriginalStockQuantity = detail.OriginalStockQuantity, IsActive = true
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(item, update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(item, update with { Price = 99 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(item, create)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(item + "/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(item + "/status", new { isActive = false })).StatusCode);
        var stored = await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == id);
        Assert.Equal(20m, stored.Price);
        Assert.Equal(2, stored.StockQuantity);
        Assert.False(stored.IsActive);
        var page = await client.GetFromJsonAsync<AdminProductPage>(
            $"{collection}?search=Edited&brandId={create.BrandId}&categoryId={create.CategoryId}&isActive=false");
        Assert.Equal(id, Assert.Single(page!.Items).ProductId);
        page = await client.GetFromJsonAsync<AdminProductPage>($"{collection}?page=2&pageSize=1");
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(id, Assert.Single(page.Items).ProductId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(collection + "?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(item + "/status", new { isActive = true })).StatusCode);
        Assert.True((await client.GetFromJsonAsync<AdminProductDetail>(item))!.IsActive);
    }

    [Fact]
    public async Task Admin_can_manage_brands_categories_and_product_options_with_referential_integrity()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (_, product) = await fixture.SeedCatalogAsync();
        var admin = await AddAdmin(fixture, true);
        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());

        // Brands CRUD
        var createBrand = await client.PostAsJsonAsync(V1Brands, new AdminLookupWriteRequest
        {
            Name = "  Samsung Galaxy  ",
            Description = " Flagship line ",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.Created, createBrand.StatusCode);
        var brandId = (await createBrand.Content.ReadFromJsonAsync<AdminLookupId>())!.Id;

        // Duplicate brand name (case-insensitive) -> 409 Conflict
        var dupBrand = await client.PostAsJsonAsync(V1Brands, new AdminLookupWriteRequest { Name = "samsung galaxy" });
        Assert.Equal(HttpStatusCode.Conflict, dupBrand.StatusCode);

        // Validation failure -> 400 BadRequest
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(V1Brands, new AdminLookupWriteRequest { Name = "   " })).StatusCode);

        // Update brand -> 204 NoContent, missing -> 404 NotFound
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"{V1Brands}/{brandId}", new AdminLookupWriteRequest
            {
                Name = "Samsung Updated",
                Description = "Updated",
                IsActive = true
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"{V1Brands}/99999", new AdminLookupWriteRequest { Name = "Ghost" })).StatusCode);

        // Categories CRUD
        var createCategory = await client.PostAsJsonAsync(V1Categories, new AdminLookupWriteRequest
        {
            Name = "Foldable",
            Description = "Foldable phones",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.Created, createCategory.StatusCode);
        var categoryId = (await createCategory.Content.ReadFromJsonAsync<AdminLookupId>())!.Id;
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(V1Categories, new AdminLookupWriteRequest { Name = "FOLDABLE" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"{V1Categories}/{categoryId}", new AdminLookupWriteRequest
            {
                Name = "Foldable Pro",
                Description = "Updated category",
                IsActive = true
            })).StatusCode);

        // Deactivate brand & category -> 204
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"{V1Brands}/{brandId}/status", new AdminLookupStatusRequest { IsActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"{V1Categories}/{categoryId}/status", new AdminLookupStatusRequest { IsActive = false })).StatusCode);

        // New product cannot reference inactive brand/category -> 400
        var invalidNewProduct = new ProductWriteRequest
        {
            ProductName = "Cannot Use Inactive Lookup",
            BrandId = brandId,
            CategoryId = product.CategoryId,
            Price = 1000m,
            StockQuantity = 5,
            IsActive = true
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(V1Products, invalidNewProduct)).StatusCode);

        // Options endpoint excludes inactive lookups by default, includes them when includeInactive=true,
        // and includes the product's current lookup when productId is supplied even if that lookup became inactive
        var defaultOptions = await client.GetFromJsonAsync<AdminProductOptionsResponse>($"{V1Products}/options");
        Assert.DoesNotContain(defaultOptions!.Brands, b => b.Id == brandId);
        Assert.DoesNotContain(defaultOptions.Categories, c => c.Id == categoryId);

        var filterOptions = await client.GetFromJsonAsync<AdminProductOptionsResponse>($"{V1Products}/options?includeInactive=true");
        Assert.Contains(filterOptions!.Brands, b => b.Id == brandId && !b.IsActive);
        Assert.Contains(filterOptions.Categories, c => c.Id == categoryId && !c.IsActive);

        // Deactivate existing product's brand, verify product can still be updated while keeping its current brand,
        // and options?productId=existingProductId includes that inactive brand
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"{V1Brands}/{product.BrandId}/status", new AdminLookupStatusRequest { IsActive = false })).StatusCode);
        var editOptions = await client.GetFromJsonAsync<AdminProductOptionsResponse>($"{V1Products}/options?productId={product.ProductId}");
        Assert.Contains(editOptions!.Brands, b => b.Id == product.BrandId && !b.IsActive);

        var existingDetail = await client.GetFromJsonAsync<AdminProductDetail>($"{V1Products}/{product.ProductId}");
        var updateKeepingBrand = new ProductUpdateRequest
        {
            ProductName = existingDetail!.ProductName + " V2",
            BrandId = existingDetail.BrandId,
            CategoryId = existingDetail.CategoryId,
            Price = existingDetail.Price,
            StockQuantity = existingDetail.StockQuantity,
            OriginalStockQuantity = existingDetail.OriginalStockQuantity,
            IsActive = existingDetail.IsActive
        };
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"{V1Products}/{product.ProductId}", updateKeepingBrand)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_upload_order_set_primary_and_delete_product_images_with_content_security()
    {
        var tempUploads = Path.Combine(Path.GetTempPath(), "PhoneStore-S354C-Images-" + Guid.NewGuid().ToString("N"));
        await using var fixture = await TestDatabase.CreateAsync();
        var (_, product1) = await fixture.SeedCatalogAsync();
        var product2 = new Product
        {
            ProductName = "Second Phone",
            BrandId = product1.BrandId,
            CategoryId = product1.CategoryId,
            Price = 5000000m,
            StockQuantity = 4,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        fixture.Db.Products.Add(product2);
        await fixture.Db.SaveChangesAsync();
        var admin = await AddAdmin(fixture, true);

        using var host = ApiAuthorizationTests.Host(fixture.Db).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProductImageStorage>();
                services.AddSingleton<IProductImageStorage>(sp =>
                    new LocalProductImageStorage(tempUploads, sp.GetRequiredService<IProductImageContentValidator>()));
            });
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());

        try
        {
            // 1. Reject dangerous or invalid uploads: executable, webp, path traversal filename, fake PNG, fake JPEG
            using (var exeForm = CreateMultipart("malware.exe", [0x4D, 0x5A, 0x90, 0x00], "application/octet-stream"))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{V1Products}/{product1.ProductId}/images", exeForm)).StatusCode);

            using (var webpForm = CreateMultipart("photo.webp", [0x52, 0x49, 0x46, 0x46], "image/webp"))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{V1Products}/{product1.ProductId}/images", webpForm)).StatusCode);

            using (var traversalForm = CreateMultipart("../outside.png", ValidPngBytes, "image/png"))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{V1Products}/{product1.ProductId}/images", traversalForm)).StatusCode);

            using (var fakePngForm = CreateMultipart("fake.png", "not a png file"u8.ToArray(), "image/png"))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{V1Products}/{product1.ProductId}/images", fakePngForm)).StatusCode);

            using (var fakeJpgForm = CreateMultipart("fake.jpg", [0xFF, 0xD8, 0xFF, 0x01, 0xFF, 0xD9], "image/jpeg"))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{V1Products}/{product1.ProductId}/images", fakeJpgForm)).StatusCode);

            var corruptedZlibPng = (byte[])ValidPngBytes.Clone();
            corruptedZlibPng[37] = (byte)'a';
            corruptedZlibPng[38] = (byte)'c';
            corruptedZlibPng[39] = (byte)'T';
            corruptedZlibPng[40] = (byte)'L';
            using (var apngForm = CreateMultipart("animated.png", corruptedZlibPng, "image/png"))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{V1Products}/{product1.ProductId}/images", apngForm)).StatusCode);

            Assert.False(Directory.Exists(tempUploads));

            // 2. Upload first valid PNG -> becomes primary automatically
            using var pngForm = CreateMultipart("front.png", ValidPngBytes, "image/png");
            var upload1 = await client.PostAsync($"{V1Products}/{product1.ProductId}/images", pngForm);
            Assert.Equal(HttpStatusCode.Created, upload1.StatusCode);
            var img1 = (await upload1.Content.ReadFromJsonAsync<AdminProductImageUploadResponse>())!;
            Assert.True(img1.IsPrimary);
            Assert.Equal(0, img1.DisplayOrder);
            Assert.Matches("^/uploads/products/[0-9a-f]{32}\\.png$", img1.ImageUrl);
            Assert.True(File.Exists(Path.Combine(tempUploads, Path.GetFileName(img1.ImageUrl))));

            // 3. Upload second valid JPEG -> non-primary, displayOrder = 1
            using var jpgForm = CreateMultipart("back.jpg", ValidJpegBytes, "image/jpeg");
            var upload2 = await client.PostAsync($"{V1Products}/{product1.ProductId}/images", jpgForm);
            Assert.Equal(HttpStatusCode.Created, upload2.StatusCode);
            var img2 = (await upload2.Content.ReadFromJsonAsync<AdminProductImageUploadResponse>())!;
            Assert.False(img2.IsPrimary);
            Assert.Equal(1, img2.DisplayOrder);
            Assert.Matches("^/uploads/products/[0-9a-f]{32}\\.jpg$", img2.ImageUrl);

            // 4. Switch primary to 2nd image -> 204, verify exactly 1 primary image
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PatchAsync($"{V1Products}/{product1.ProductId}/images/{img2.ImageId}/primary", null)).StatusCode);
            var listAfterPrimary = await client.GetFromJsonAsync<AdminProductImageItem[]>($"{V1Products}/{product1.ProductId}/images");
            Assert.Equal(2, listAfterPrimary!.Length);
            Assert.Equal(img2.ImageId, Assert.Single(listAfterPrimary, i => i.IsPrimary).ImageId);

            // 5. Update display order -> 204
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PatchAsJsonAsync($"{V1Products}/{product1.ProductId}/images/{img1.ImageId}/order",
                    new AdminProductImageOrderRequest { DisplayOrder = 5 })).StatusCode);
            var listAfterOrder = await client.GetFromJsonAsync<AdminProductImageItem[]>($"{V1Products}/{product1.ProductId}/images");
            Assert.Equal(5, listAfterOrder!.Single(i => i.ImageId == img1.ImageId).DisplayOrder);

            // 6. Cross-product isolation: product2 cannot delete or modify product1's images -> 404 NotFound
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.DeleteAsync($"{V1Products}/{product2.ProductId}/images/{img1.ImageId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PatchAsync($"{V1Products}/{product2.ProductId}/images/{img1.ImageId}/primary", null)).StatusCode);

            // 7. Delete primary image (img2) from product1 -> remaining image (img1) is promoted to primary
            var delResp = await client.DeleteAsync($"{V1Products}/{product1.ProductId}/images/{img2.ImageId}");
            Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);
            var remaining = await client.GetFromJsonAsync<AdminProductImageItem[]>($"{V1Products}/{product1.ProductId}/images");
            var sole = Assert.Single(remaining!);
            Assert.Equal(img1.ImageId, sole.ImageId);
            Assert.True(sole.IsPrimary);
        }
        finally
        {
            if (Directory.Exists(tempUploads))
                Directory.Delete(tempUploads, recursive: true);
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

    private static async Task<User> AddAdmin(TestDatabase fixture, bool active)
    {
        var admin = new User
        {
            FullName = "Fixture", Email = "product-admin@test.local",
            PasswordHash = "unused", RoleId = SystemRoles.AdminId, IsActive = active
        };
        fixture.Db.Users.Add(admin);
        await fixture.Db.SaveChangesAsync();
        return admin;
    }
}
