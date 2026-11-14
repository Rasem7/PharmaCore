using Dapper;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Persistence;

public sealed class InventoryRepository(ISqlConnectionFactory db) : IInventoryRepository
{
    public async Task ReceiveAsync(Guid tenantId, Guid userId, ReceiveStockRequest req, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Idempotency: a replayed key must not change stock twice.
        if (await MovementExists(conn, tx, tenantId, req.IdempotencyKey, ct)) { await tx.CommitAsync(ct); return; }

        await EnsureWarehouse(conn, tx, tenantId, req.WarehouseId, ct);
        var productOk = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.Products WHERE Id = @ProductId AND TenantId = @tenantId", new { req.ProductId, tenantId }, tx, cancellationToken: ct));
        if (productOk == 0) throw new ProductNotFoundException();

        var batchId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT Id FROM dbo.Batches WITH (UPDLOCK, HOLDLOCK) WHERE ProductId = @ProductId AND BatchNumber = @BatchNumber",
            new { req.ProductId, req.BatchNumber }, tx, cancellationToken: ct));
        if (batchId is null)
        {
            batchId = Guid.NewGuid();
            await conn.ExecuteAsync(new CommandDefinition(
                @"INSERT dbo.Batches (Id, TenantId, ProductId, BatchNumber, ProductionDate, ExpiryDate)
                  VALUES (@id, @tenantId, @ProductId, @BatchNumber, @ProductionDate, @ExpiryDate)",
                new { id = batchId, tenantId, req.ProductId, req.BatchNumber, req.ProductionDate, req.ExpiryDate }, tx, cancellationToken: ct));
        }

        await ApplyDelta(conn, tx, req.WarehouseId, batchId.Value, req.ProductId, req.Quantity, ct);
        await InsertMovement(conn, tx, tenantId, userId, req.WarehouseId, batchId.Value, req.ProductId,
            MovementType.Receive, req.Quantity, null, req.IdempotencyKey, ct);
        await tx.CommitAsync(ct);
    }

    public async Task AdjustAsync(Guid tenantId, Guid userId, AdjustStockRequest req, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        if (await MovementExists(conn, tx, tenantId, req.IdempotencyKey, ct)) { await tx.CommitAsync(ct); return; }

        await EnsureWarehouse(conn, tx, tenantId, req.WarehouseId, ct);
        var productId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT ProductId FROM dbo.Batches WHERE Id = @BatchId AND TenantId = @tenantId", new { req.BatchId, tenantId }, tx, cancellationToken: ct))
            ?? throw new BatchNotFoundException();

        await ApplyDelta(conn, tx, req.WarehouseId, req.BatchId, productId, req.QuantityDelta, ct);
        await InsertMovement(conn, tx, tenantId, userId, req.WarehouseId, req.BatchId, productId,
            MovementType.Adjustment, req.QuantityDelta, req.Reason, req.IdempotencyKey, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<StockRow>> GetStockAsync(Guid tenantId, Guid? productId, Guid? warehouseId, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<StockRow>(new CommandDefinition(StockSelect + @"
            WHERE b.TenantId = @tenantId AND sb.Quantity > 0
              AND (@productId IS NULL OR sb.ProductId = @productId) AND (@warehouseId IS NULL OR sb.WarehouseId = @warehouseId)
            ORDER BY p.CommercialName, b.ExpiryDate", new { tenantId, productId, warehouseId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StockRow>> GetExpiringAsync(Guid tenantId, DateOnly until, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<StockRow>(new CommandDefinition(StockSelect + @"
            WHERE b.TenantId = @tenantId AND sb.Quantity > 0 AND b.ExpiryDate <= @until
            ORDER BY b.ExpiryDate, p.CommercialName", new { tenantId, until }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(Guid tenantId, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var rows = await conn.QueryAsync<WarehouseDto>(new CommandDefinition(
            @"SELECT w.Id, br.Name AS BranchName, w.Name, w.IsDefault
              FROM dbo.Warehouses w JOIN dbo.Branches br ON br.Id = w.BranchId
              WHERE br.TenantId = @tenantId AND w.IsActive = 1 AND br.IsActive = 1
              ORDER BY br.Name, w.Name", new { tenantId }, cancellationToken: ct));
        return rows.ToList();
    }

    private const string StockSelect = @"
        SELECT sb.ProductId, p.CommercialName AS ProductName, sb.BatchId, b.BatchNumber, b.ExpiryDate, sb.WarehouseId, sb.Quantity
        FROM dbo.StockBalances sb
        JOIN dbo.Batches b ON b.Id = sb.BatchId
        JOIN dbo.Products p ON p.Id = sb.ProductId ";

    private static async Task<bool> MovementExists(System.Data.Common.DbConnection c, System.Data.Common.DbTransaction tx, Guid tenantId, string key, CancellationToken ct)
        => await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.StockMovements WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @tenantId AND IdempotencyKey = @key",
            new { tenantId, key }, tx, cancellationToken: ct)) > 0;

    private static async Task EnsureWarehouse(System.Data.Common.DbConnection c, System.Data.Common.DbTransaction tx, Guid tenantId, Guid warehouseId, CancellationToken ct)
    {
        var ok = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM dbo.Warehouses w JOIN dbo.Branches br ON br.Id = w.BranchId
              WHERE w.Id = @warehouseId AND br.TenantId = @tenantId", new { warehouseId, tenantId }, tx, cancellationToken: ct));
        if (ok == 0) throw new WarehouseNotFoundException();
    }

    private static async Task ApplyDelta(System.Data.Common.DbConnection c, System.Data.Common.DbTransaction tx,
        Guid warehouseId, Guid batchId, Guid productId, decimal delta, CancellationToken ct)
    {
        var updated = await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.StockBalances WITH (UPDLOCK, HOLDLOCK) SET Quantity = Quantity + @delta
              WHERE WarehouseId = @warehouseId AND BatchId = @batchId AND Quantity + @delta >= 0",
            new { delta, warehouseId, batchId }, tx, cancellationToken: ct));
        if (updated == 1) return;

        var exists = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.StockBalances WHERE WarehouseId = @warehouseId AND BatchId = @batchId",
            new { warehouseId, batchId }, tx, cancellationToken: ct));
        if (exists > 0 || delta < 0) throw new NegativeStockException();

        await c.ExecuteAsync(new CommandDefinition(
            "INSERT dbo.StockBalances (WarehouseId, BatchId, ProductId, Quantity) VALUES (@warehouseId, @batchId, @productId, @delta)",
            new { warehouseId, batchId, productId, delta }, tx, cancellationToken: ct));
    }

    private static Task InsertMovement(System.Data.Common.DbConnection c, System.Data.Common.DbTransaction tx, Guid tenantId, Guid userId,
        Guid warehouseId, Guid batchId, Guid productId, MovementType type, decimal qty, string? reason, string key, CancellationToken ct)
        => c.ExecuteAsync(new CommandDefinition(
            @"INSERT dbo.StockMovements (Id, TenantId, WarehouseId, BatchId, ProductId, MovementType, Quantity, Reason, IdempotencyKey, CreatedBy)
              VALUES (NEWID(), @tenantId, @warehouseId, @batchId, @productId, @type, @qty, @reason, @key, @userId)",
            new { tenantId, warehouseId, batchId, productId, type = (byte)type, qty, reason, key, userId }, tx, cancellationToken: ct));
}
