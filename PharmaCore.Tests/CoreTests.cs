using PharmaCore.Application.Products;
using PharmaCore.Domain.Entities;
using PharmaCore.Infrastructure.Security;
using Xunit;

namespace PharmaCore.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_succeeds_and_wrong_password_fails()
    {
        var hash = _hasher.Hash("Correct#Horse1");
        Assert.True(_hasher.Verify("Correct#Horse1", hash));
        Assert.False(_hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Same_password_produces_different_hashes()
        => Assert.NotEqual(_hasher.Hash("x"), _hasher.Hash("x"));

    [Fact]
    public void Malformed_hash_returns_false()
        => Assert.False(_hasher.Verify("x", "not-a-hash"));
}

public class ProductValidatorTests
{
    private static SaveProductRequest Valid() => new("Panadol 500mg", null, null, null, "6221000000001", "Tablet", 20m, 25m, 5m, 50m, ProductStatus.Active);
    private readonly SaveProductRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);
    [Fact] public void Empty_name_fails() => Assert.False(_v.Validate(Valid() with { CommercialName = "" }).IsValid);
    [Fact] public void Negative_price_fails() => Assert.False(_v.Validate(Valid() with { SellingPrice = -1m }).IsValid);
    [Fact] public void Max_below_min_fails() => Assert.False(_v.Validate(Valid() with { MinStock = 10m, MaxStock = 5m }).IsValid);
}
