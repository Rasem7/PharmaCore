using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PharmaCore.Api.Auth;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Api.Controllers;

[ApiController, Route("api/inventory")]
public sealed class InventoryController(InventoryService service,
    IValidator<ReceiveStockRequest> receiveValidator, IValidator<AdjustStockRequest> adjustValidator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    [HttpGet("warehouses"), Authorize(Policy = Permissions.InventoryRead)]
    public Task<IReadOnlyList<WarehouseDto>> Warehouses(CancellationToken ct)
        => service.GetWarehousesAsync(User.GetTenantId(), ct);

    [HttpGet("stock"), Authorize(Policy = Permissions.InventoryRead)]
    public Task<IReadOnlyList<StockRow>> Stock([FromQuery] Guid? productId, [FromQuery] Guid? warehouseId, CancellationToken ct)
        => service.GetStockAsync(User.GetTenantId(), productId, warehouseId, ct);

    [HttpGet("expiring"), Authorize(Policy = Permissions.InventoryRead)]
    public Task<IReadOnlyList<StockRow>> Expiring([FromQuery] int days = 90, CancellationToken ct = default)
        => service.GetExpiringAsync(User.GetTenantId(), days, ct);

    [HttpPost("receive"), Authorize(Policy = Permissions.InventoryWrite)]
    public async Task<IActionResult> Receive(ReceiveStockRequest req, CancellationToken ct)
    {
        var v = await receiveValidator.ValidateAsync(req, ct);
        if (!v.IsValid) return ValidationProblem(ToModelState(v));
        await service.ReceiveAsync(User.GetTenantId(), UserId, req, ct);
        return NoContent();
    }

    [HttpPost("adjust"), Authorize(Policy = Permissions.InventoryWrite)]
    public async Task<IActionResult> Adjust(AdjustStockRequest req, CancellationToken ct)
    {
        var v = await adjustValidator.ValidateAsync(req, ct);
        if (!v.IsValid) return ValidationProblem(ToModelState(v));
        await service.AdjustAsync(User.GetTenantId(), UserId, req, ct);
        return NoContent();
    }

    private static ModelStateDictionary ToModelState(FluentValidation.Results.ValidationResult r)
    {
        var ms = new ModelStateDictionary();
        foreach (var e in r.Errors) ms.AddModelError(e.PropertyName, e.ErrorMessage);
        return ms;
    }
}
