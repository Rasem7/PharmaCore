using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PharmaCore.Api.Auth;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Products;

namespace PharmaCore.Api.Controllers;

[ApiController, Route("api/products")]
public sealed class ProductsController(ProductService service, IValidator<SaveProductRequest> validator) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.ProductsRead)]
    public Task<PagedResult<ProductDto>> Search([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => service.SearchAsync(User.GetTenantId(), search, page, pageSize, ct);

    [HttpGet("{id:guid}"), Authorize(Policy = Permissions.ProductsRead)]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct)
        => await service.GetAsync(User.GetTenantId(), id, ct) is { } p ? p : NotFound();

    [HttpPost, Authorize(Policy = Permissions.ProductsWrite)]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest req, CancellationToken ct)
    {
        var v = await validator.ValidateAsync(req, ct);
        if (!v.IsValid) return ValidationProblem(ToModelState(v));
        var created = await service.CreateAsync(User.GetTenantId(), req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Permissions.ProductsWrite)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, SaveProductRequest req, CancellationToken ct)
    {
        var v = await validator.ValidateAsync(req, ct);
        if (!v.IsValid) return ValidationProblem(ToModelState(v));
        return await service.UpdateAsync(User.GetTenantId(), id, req, ct) is { } p ? p : NotFound();
    }

    private ModelStateDictionary ToModelState(FluentValidation.Results.ValidationResult r)
    {
        var ms = new ModelStateDictionary();
        foreach (var e in r.Errors) ms.AddModelError(e.PropertyName, e.ErrorMessage);
        return ms;
    }
}
