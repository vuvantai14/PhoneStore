using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Api.Authorization;

public static class ApiAccess
{
    public const string Customer = "Api.Customer";
    public const string Admin = "Api.Admin";
    public const string Authenticated = "Api.Authenticated";
    public const string Scheme = "PhoneStore.Api";

    internal static int? UserId(ClaimsPrincipal principal)
    {
        var subjects = principal.Identities.Where(i => i.IsAuthenticated)
            .SelectMany(i => i.FindAll(ClaimTypes.NameIdentifier)).ToArray();
        return subjects.Length == 1 && int.TryParse(subjects[0].Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    internal static void Configure(AuthorizationOptions options)
    {
        // An unclassified controller action stays closed until its audience is specified.
        options.DefaultPolicy = new AuthorizationPolicyBuilder(Scheme)
            .RequireAssertion(_ => false).Build();
        AddRolePolicy(options, Customer, SystemRoles.Customer);
        AddRolePolicy(options, Admin, SystemRoles.Admin);
        options.AddPolicy(Authenticated, policy => policy.AddAuthenticationSchemes(Scheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new ActiveAnyRole([SystemRoles.Customer, SystemRoles.Admin])));
        options.AddPolicy("Orders.Read", policy => policy.AddAuthenticationSchemes(Scheme)
            .RequireClaim("scope", "orders:read"));
        options.AddPolicy("Orders.Write", policy => policy.AddAuthenticationSchemes(Scheme)
            .RequireClaim("scope", "orders:write"));
    }

    private static void AddRolePolicy(AuthorizationOptions options, string name, string role) =>
        options.AddPolicy(name, policy => policy.AddAuthenticationSchemes(Scheme)
            .RequireAuthenticatedUser().AddRequirements(new ActiveRole(role)));
}

internal sealed record ActiveRole(string Role) : IAuthorizationRequirement;
internal sealed record ActiveAnyRole(string[] Roles) : IAuthorizationRequirement;

internal sealed class ActiveRoleHandler(PhoneStoreDbContext db) : AuthorizationHandler<ActiveRole>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveRole requirement)
    {
        if (context.Resource is not HttpContext http) return;
        if (ApiAccess.UserId(context.User) is not int userId) return;

        if (await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive &&
            u.Role.RoleName == requirement.Role, http.RequestAborted))
            context.Succeed(requirement);
    }
}

internal sealed class ActiveAnyRoleHandler(PhoneStoreDbContext db) : AuthorizationHandler<ActiveAnyRole>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveAnyRole requirement)
    {
        if (context.Resource is not HttpContext http) return;
        if (ApiAccess.UserId(context.User) is not int userId) return;

        if (await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive &&
            requirement.Roles.Contains(u.Role.RoleName), http.RequestAborted))
            context.Succeed(requirement);
    }
}
