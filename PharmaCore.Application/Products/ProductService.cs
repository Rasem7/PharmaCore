using PharmaCore.Application.Abstractions;
using PharmaCore.Domain.Entities;

namespace PharmaCore.Application.Products;

public sealed class DuplicateBarcodeException(string barcode) : Exception($"Barcode '{barcode}' is already used by another product.");

public sealed class ProductService(IProductRepository repo)
{
    public async Task<PagedResult<ProductDto>> SearchAsync(Guid tenantId, string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var r = await repo.SearchAsync(tenantId, search?.Trim(), page, pageSize, ct);
        return new PagedResult<ProductDto>(r.Items.Select(ToDto).ToList(), r.Total, r.Page, r.PageSize);
    }

    public async Task<ProductDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => (await repo.GetAsync(tenantId, id, ct)) is { } p ? ToDto(p) : null;

    public async Task<ProductDto> CreateAsync(Guid tenantId, SaveProductRequest req, CancellationToken ct)
    {
        await EnsureBarcodeFreeAsync(tenantId, req.PrimaryBarcode, null, ct);
        var p = Apply(new Product { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAtUtc = DateTime.UtcNow }, req);
        await repo.InsertAsync(p, ct);
        return ToDto(p);
    }

    public async Task<ProductDto?> UpdateAsync(Guid tenantId, Guid id, SaveProductRequest req, CancellationToken ct)
    {
        var existing = await repo.GetAsync(tenantId, id, ct);
        if (existing is null) return null;
        await EnsureBarcodeFreeAsync(tenantId, req.PrimaryBarcode, id, ct);
        Apply(existing, req);
        existing.UpdatedAtUtc = DateTime.UtcNow;
        return await repo.UpdateAsync(existing, ct) ? ToDto(existing) : null;
    }

    private async Task EnsureBarcodeFreeAsync(Guid tenantId, string? barcode, Guid? exclude, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        if (await repo.BarcodeExistsAsync(tenantId, barcode.Trim(), exclude, ct))
            throw new DuplicateBarcodeException(barcode);
    }

    private static Product Apply(Product p, SaveProductRequest r)
    {
        p.CommercialName = r.CommercialName.Trim();
        p.NameAr = r.NameAr?.Trim();
        p.NameEn = r.NameEn?.Trim();
        p.ScientificName = r.ScientificName?.Trim();
        p.PrimaryBarcode = string.IsNullOrWhiteSpace(r.PrimaryBarcode) ? null : r.PrimaryBarcode.Trim();
        p.PharmaceuticalForm = r.PharmaceuticalForm?.Trim();
        p.PurchasePrice = r.PurchasePrice;
        p.SellingPrice = r.SellingPrice;
        p.MinStock = r.MinStock;
        p.MaxStock = r.MaxStock;
        p.Status = r.Status;
        return p;
    }

    private static ProductDto ToDto(Product p) => new(p.Id, p.CommercialName, p.NameAr, p.NameEn, p.ScientificName,
        p.PrimaryBarcode, p.PharmaceuticalForm, p.PurchasePrice, p.SellingPrice, p.MinStock, p.MaxStock, p.Status);
}
