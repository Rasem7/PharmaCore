using PharmaCore.Domain.Entities;

namespace PharmaCore.Application.Abstractions;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record UserRecord(Guid Id, Guid TenantId, string UserName, string PasswordHash, bool IsActive,
    int FailedLoginCount, DateTime? LockedUntilUtc);

public interface IProductRepository
{
    Task<PagedResult<Product>> SearchAsync(Guid tenantId, string? search, int page, int pageSize, CancellationToken ct);
    Task<Product?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Guid> InsertAsync(Product product, CancellationToken ct);
    Task<bool> UpdateAsync(Product product, CancellationToken ct);
    Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, Guid? excludeProductId, CancellationToken ct);
}

public interface IUserRepository
{
    Task<UserRecord?> FindByUserNameAsync(string userName, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct);
    Task RegisterLoginResultAsync(Guid userId, bool success, CancellationToken ct);
    Task<bool> AnyUserAsync(CancellationToken ct);
    Task<Guid> CreateAdminAsync(string userName, string passwordHash, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    string CreateToken(Guid userId, Guid tenantId, string userName, IEnumerable<string> permissions);
}
