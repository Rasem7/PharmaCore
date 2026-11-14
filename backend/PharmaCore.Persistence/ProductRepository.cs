using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Domain.Entities;

namespace PharmaCore.Persistence;

public sealed class ProductRepository(ISqlConnectionFactory db) : IProductRepository
{
    private const string Columns = @"Id, TenantId, CommercialName, NameAr, NameEn, ScientificName, PrimaryBarcode,
        PharmaceuticalForm, PurchasePrice, SellingPrice, MinStock, MaxStock, Status, CreatedAtUtc, UpdatedAtUtc";

    public async Task<PagedResult<Product>> SearchAsync(Guid tenantId, string? search, int page, int pageSize, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var like = string.IsNullOrWhiteSpace(search) ? null : "%" + EscapeLike(search) + "%";
        const string where = @"TenantId = @tenantId AND (@like IS NULL OR CommercialName LIKE @like ESCAPE '\'
            OR NameAr LIKE @like ESCAPE '\' OR NameEn LIKE @like ESCAPE '\'
            OR ScientificName LIKE @like ESCAPE '\' OR PrimaryBarcode = @exact)";
        var p = new { tenantId, like, exact = search, offset = (page - 1) * pageSize, pageSize };
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM dbo.Products WHERE {where}", p, cancellationToken: ct));
        var items = await conn.QueryAsync<Product>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.Products WHERE {where} ORDER BY CommercialName OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            p, cancellationToken: ct));
        return new PagedResult<Product>(items.ToList(), total, page, pageSize);
    }

    public async Task<Product?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.Products WHERE TenantId = @tenantId AND Id = @id", new { tenantId, id }, cancellationToken: ct));
    }

    public async Task<Guid> InsertAsync(Product p, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO dbo.Products (Id, TenantId, CommercialName, NameAr, NameEn, ScientificName, PrimaryBarcode,
                PharmaceuticalForm, PurchasePrice, SellingPrice, MinStock, MaxStock, Status, CreatedAtUtc)
            VALUES (@Id, @TenantId, @CommercialName, @NameAr, @NameEn, @ScientificName, @PrimaryBarcode,
                @PharmaceuticalForm, @PurchasePrice, @SellingPrice, @MinStock, @MaxStock, @Status, @CreatedAtUtc)", p, cancellationToken: ct));
        return p.Id;
    }

    public async Task<bool> UpdateAsync(Product p, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE dbo.Products SET CommercialName=@CommercialName, NameAr=@NameAr, NameEn=@NameEn,
                ScientificName=@ScientificName, PrimaryBarcode=@PrimaryBarcode, PharmaceuticalForm=@PharmaceuticalForm,
                PurchasePrice=@PurchasePrice, SellingPrice=@SellingPrice, MinStock=@MinStock, MaxStock=@MaxStock,
                Status=@Status, UpdatedAtUtc=@UpdatedAtUtc
            WHERE Id=@Id AND TenantId=@TenantId", p, cancellationToken: ct));
        return rows == 1;
    }

    public async Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, Guid? excludeProductId, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(1) FROM dbo.Products
            WHERE TenantId=@tenantId AND PrimaryBarcode=@barcode AND (@exclude IS NULL OR Id <> @exclude)",
            new { tenantId, barcode, exclude = excludeProductId }, cancellationToken: ct)) > 0;
    }

    private static string EscapeLike(string s) => s.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
