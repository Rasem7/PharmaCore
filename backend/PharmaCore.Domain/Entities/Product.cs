namespace PharmaCore.Domain.Entities;

public enum ProductStatus : byte
{
    Active = 1,
    Inactive = 2,
    Discontinued = 3
}

public sealed class Product
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CommercialName { get; set; } = "";
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public string? ScientificName { get; set; }
    public string? PrimaryBarcode { get; set; }
    public string? PharmaceuticalForm { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Business rule: a product must not be sold below its purchase price without an explicit override.</summary>
    public bool IsSellingBelowCost => SellingPrice < PurchasePrice;
}
