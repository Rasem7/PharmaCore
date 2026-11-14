using Dapper;
using PharmaCore.Application.Abstractions;

namespace PharmaCore.Persistence;

public sealed class UserRepository(ISqlConnectionFactory db) : IUserRepository
{
    public async Task<UserRecord?> FindByUserNameAsync(string userName, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<UserRecord>(new CommandDefinition(
            "SELECT Id, TenantId, UserName, PasswordHash, IsActive, FailedLoginCount, LockedUntilUtc FROM dbo.Users WHERE UserName = @userName",
            new { userName }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(@"
            SELECT DISTINCT rp.PermissionCode FROM dbo.UserRoles ur
            JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId WHERE ur.UserId = @userId",
            new { userId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task RegisterLoginResultAsync(Guid userId, bool success, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        // Lock for 15 minutes after 5 consecutive failures.
        var sql = success
            ? "UPDATE dbo.Users SET FailedLoginCount = 0, LockedUntilUtc = NULL, LastLoginAtUtc = SYSUTCDATETIME() WHERE Id = @userId"
            : @"UPDATE dbo.Users SET FailedLoginCount = FailedLoginCount + 1,
                   LockedUntilUtc = CASE WHEN FailedLoginCount + 1 >= 5 THEN DATEADD(MINUTE, 15, SYSUTCDATETIME()) ELSE LockedUntilUtc END
                WHERE Id = @userId";
        await conn.ExecuteAsync(new CommandDefinition(sql, new { userId }, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.AuditLogs (UserId, Action) VALUES (@userId, @action)",
            new { userId, action = success ? "login.success" : "login.failed" }, cancellationToken: ct));
    }

    public async Task<bool> AnyUserAsync(CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM dbo.Users", cancellationToken: ct)) > 0;
    }

    /// <summary>Development bootstrap: tenant, branch, Administrator role with all permissions, and one admin user, in one transaction.</summary>
    public async Task<Guid> CreateAdminAsync(string userName, string passwordHash, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var tenantId = Guid.NewGuid(); var branchId = Guid.NewGuid();
        var roleId = Guid.NewGuid(); var userId = Guid.NewGuid();
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT dbo.Tenants (Id, Name) VALUES (@tenantId, N'Demo Pharmacy (dev seed)');
            INSERT dbo.Branches (Id, TenantId, Code, Name) VALUES (@branchId, @tenantId, N'MAIN', N'Main Branch');
            INSERT dbo.Warehouses (Id, BranchId, Name, IsDefault) VALUES (NEWID(), @branchId, N'Main Warehouse', 1);
            INSERT dbo.Roles (Id, TenantId, Name) VALUES (@roleId, @tenantId, N'Administrator');
            INSERT dbo.RolePermissions (RoleId, PermissionCode) SELECT @roleId, Code FROM dbo.Permissions;
            INSERT dbo.Users (Id, TenantId, UserName, FullName, PasswordHash) VALUES (@userId, @tenantId, @userName, N'Administrator', @passwordHash);
            INSERT dbo.UserRoles (UserId, RoleId) VALUES (@userId, @roleId);
            INSERT dbo.UserBranchAccess (UserId, BranchId) VALUES (@userId, @branchId);",
            new { tenantId, branchId, roleId, userId, userName, passwordHash }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return userId;
    }
}
