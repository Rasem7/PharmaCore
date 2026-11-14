using FluentValidation;

namespace PharmaCore.Application.Inventory;

public enum MovementType : byte { Receive = 1, Adjustment = 2, Sale = 3, Return = 4, Transfer = 5, DamagedOrExpired = 6 }

public sealed record ReceiveStockRequest(Guid WarehouseId, Guid ProductId, string BatchNumber, DateOnly? ProductionDate,
    DateOnly ExpiryDate, decimal Quantity, string IdempotencyKey);

/// <summary>Signed adjustment of one existing batch (negative = remove). Reason is mandatory for the audit trail.</summary>
public sealed record AdjustStockRequest(Guid WarehouseId, Guid BatchId, decimal QuantityDelta, string Reason, string IdempotencyKey);

public sealed record StockRow(Guid ProductId, string ProductName, Guid BatchId, string BatchNumber,
    DateOnly ExpiryDate, Guid WarehouseId, decimal Quantity);

public sealed record WarehouseDto(Guid Id, string BranchName, string Name, bool IsDefault);

public sealed class WarehouseNotFoundException() : Exception("Warehouse not found for this tenant.");
public sealed class ProductNotFoundException() : Exception("Product not found for this tenant.");
public sealed class BatchNotFoundException() : Exception("Batch not found for this tenant.");
public sealed class NegativeStockException() : Exception("The adjustment would make the batch quantity negative.");

public sealed class ReceiveStockRequestValidator : AbstractValidator<ReceiveStockRequest>
{
    public ReceiveStockRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProductionDate).LessThan(x => x.ExpiryDate).When(x => x.ProductionDate.HasValue);
    }
}

public sealed class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.QuantityDelta).NotEqual(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}

public interface IInventoryRepository
{
    /// <summary>One transaction: upsert batch, add to balance, append movement. Replaying the same idempotency key is a no-op.</summary>
    Task ReceiveAsync(Guid tenantId, Guid userId, ReceiveStockRequest req, CancellationToken ct);
    Task AdjustAsync(Guid tenantId, Guid userId, AdjustStockRequest req, CancellationToken ct);
    Task<IReadOnlyList<StockRow>> GetStockAsync(Guid tenantId, Guid? productId, Guid? warehouseId, CancellationToken ct);
    Task<IReadOnlyList<StockRow>> GetExpiringAsync(Guid tenantId, DateOnly until, CancellationToken ct);
    Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(Guid tenantId, CancellationToken ct);
}

public sealed class InventoryService(IInventoryRepository repo)
{
    public Task ReceiveAsync(Guid tenantId, Guid userId, ReceiveStockRequest req, CancellationToken ct)
        => repo.ReceiveAsync(tenantId, userId, req with { BatchNumber = req.BatchNumber.Trim() }, ct);

    public Task AdjustAsync(Guid tenantId, Guid userId, AdjustStockRequest req, CancellationToken ct)
        => repo.AdjustAsync(tenantId, userId, req, ct);

    public Task<IReadOnlyList<StockRow>> GetStockAsync(Guid tenantId, Guid? productId, Guid? warehouseId, CancellationToken ct)
        => repo.GetStockAsync(tenantId, productId, warehouseId, ct);

    public Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(Guid tenantId, CancellationToken ct)
        => repo.GetWarehousesAsync(tenantId, ct);

    public Task<IReadOnlyList<StockRow>> GetExpiringAsync(Guid tenantId, int days, CancellationToken ct)
        => repo.GetExpiringAsync(tenantId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Math.Clamp(days, 0, 730)), ct);
}
