using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PhoneStore.Api.Infrastructure;

internal sealed class AuthorizationDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any()) return;
        var policies = metadata.OfType<IAuthorizeData>().Select(a => a.Policy).ToArray();
        var policy = policies.SingleOrDefault(p => p is Authorization.ApiAccess.Customer or Authorization.ApiAccess.Admin);
        if (policy is null) return;
        operation.Extensions["x-phonestore-policy"] = new OpenApiString(policy);
        operation.Description = policies.Contains("Orders.Read")
            ? $"Requires {policy} and the orders:read scope."
            : $"Requires {policy}.";
    }
}
