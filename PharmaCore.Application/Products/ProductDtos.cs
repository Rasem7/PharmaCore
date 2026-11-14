using FluentValidation;
using PharmaCore.Domain.Entities;

namespace PharmaCore.Application.Products;

public sealed record ProductDto(Guid Id, string CommercialName, string? NameAr, string? NameEn, string? ScientificName,
    string? PrimaryBarcode, string? PharmaceuticalForm, decimal PurchasePrice, decimal SellingPrice,
    decimal MinStock, decimal? MaxStock, ProductStatus Status);

public sealed record SaveProductRequest(string CommercialName, string? NameAr, string? NameEn, string? ScientificName,
    string? PrimaryBarcode, string? PharmaceuticalForm, decimal PurchasePrice, decimal SellingPrice,
    decimal MinStock, decimal? MaxStock, ProductStatus Status);

public sealed class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(x => x.CommercialName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200);
        RuleFor(x => x.ScientificName).MaximumLength(300);
        RuleFor(x => x.PrimaryBarcode).MaximumLength(64);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxStock).GreaterThanOrEqualTo(x => x.MinStock).When(x => x.MaxStock.HasValue);
        RuleFor(x => x.Status).IsInEnum();
    }
}
