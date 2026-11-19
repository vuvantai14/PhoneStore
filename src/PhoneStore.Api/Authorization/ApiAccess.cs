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
    public const string Scheme = "PhoneStore.Api";

    internal static void Configure(AuthorizationOptions options)
    {
        // An unclassified controller action stays closed until its audience is specified.
        options.DefaultPolicy = new AuthorizationPolicyBuilder(Scheme)
            .RequireAssertion(_ => false).Build();
        AddRolePolicy(options, Customer, SystemRoles.Customer);
        AddRolePolicy(options, Admin, SystemRoles.Admin);
    }

    private static void AddRolePolicy(AuthorizationOptions options, string name, string role) =>
        options.AddPolicy(name, policy => policy.AddAuthenticationSchemes(Scheme)
            .RequireAuthenticatedUser().AddRequirements(new ActiveRole(role)));
}

internal sealed record ActiveRole(string Role) : IAuthorizationRequirement;

internal sealed class ActiveRoleHandler(PhoneStoreDbContext db) : AuthorizationHandler<ActiveRole>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveRole requirement)
    {
        if (context.Resource is not HttpContext http) return;
        var subjects = context.User.Identities.Where(i => i.IsAuthenticated)
            .SelectMany(i => i.FindAll(ClaimTypes.NameIdentifier)).ToArray();
        if (subjects.Length != 1 || !int.TryParse(subjects[0].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var userId) || userId <= 0) return;

        if (await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive &&
            u.Role.RoleName == requirement.Role, http.RequestAborted))
            context.Succeed(requirement);
    }
}
