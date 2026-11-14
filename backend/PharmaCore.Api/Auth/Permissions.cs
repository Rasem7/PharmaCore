using System.Security.Claims;

namespace PharmaCore.Api.Auth;

public static class Permissions
{
    public const string ProductsRead = "products.read";
    public const string ProductsWrite = "products.write";
    public const string UsersManage = "users.manage";
    public const string SyncView = "sync.view";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";

    public const string DrugRefRead = "drugref.read";
    public const string DrugRefImport = "drugref.import";

    public static readonly string[] All = [ProductsRead, ProductsWrite, UsersManage, SyncView, InventoryRead, InventoryWrite, DrugRefRead, DrugRefImport];
}

public static class ClaimsExtensions
{
    public static Guid GetTenantId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException("Missing tenant claim."));
}
