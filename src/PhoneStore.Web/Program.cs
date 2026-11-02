using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Business.Catalog;
using PhoneStore.Data.Catalog;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using PhoneStore.Business.Authentication;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Entities;
using PhoneStore.Web.Authentication;
using PhoneStore.Web.Purchasing;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Purchasing;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // Development runs may not have permission to write to the Windows Event Log.
    // Keep diagnostics available without allowing an Event Log failure to abort requests.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.Name = "PhoneStore.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddScoped<SessionCart>();
builder.Services.AddScoped<CartPresenter>();
builder.Services.AddSingleton<CheckoutSubmissionGuard>();
builder.Services.AddScoped<PurchaseStore>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<PhoneStore.Data.Management.OrderManagementStore>();
builder.Services.AddScoped<PhoneStore.Business.Management.IOrderManagementService, PhoneStore.Business.Management.OrderManagementService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ProductCatalogReader>();
builder.Services.AddScoped<AuthUserStore>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<CustomerProfileStore>();
builder.Services.AddScoped<ICustomerProfileService, CustomerProfileService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<CustomerCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
    options.Cookie.Name = "PhoneStore.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.EventsType = typeof(CustomerCookieEvents);
});
builder.Services.AddAuthorization(options =>
    options.AddPolicy("Customer", policy => policy.RequireAuthenticatedUser().RequireRole("Customer")));
builder.Services.AddDbContext<PhoneStoreDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PhoneStoreConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
