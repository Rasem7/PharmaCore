/* PharmaCore migration 002: inventory (batches, balances, append-only movements). */

CREATE TABLE dbo.Batches (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Batches PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Batches_Tenants REFERENCES dbo.Tenants(Id),
    ProductId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Batches_Products REFERENCES dbo.Products(Id),
    BatchNumber NVARCHAR(60) NOT NULL,
    ProductionDate DATE NULL,
    ExpiryDate DATE NOT NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Batches_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Batches_Product_Number UNIQUE (ProductId, BatchNumber)
);
CREATE INDEX IX_Batches_Tenant_Expiry ON dbo.Batches(TenantId, ExpiryDate);

CREATE TABLE dbo.StockBalances (
    WarehouseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockBalances_Warehouses REFERENCES dbo.Warehouses(Id),
    BatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockBalances_Batches REFERENCES dbo.Batches(Id),
    ProductId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockBalances_Products REFERENCES dbo.Products(Id),
    Quantity DECIMAL(18,3) NOT NULL,
    CONSTRAINT PK_StockBalances PRIMARY KEY (WarehouseId, BatchId),
    CONSTRAINT CK_StockBalances_NonNegative CHECK (Quantity >= 0)
);
CREATE INDEX IX_StockBalances_Product ON dbo.StockBalances(ProductId, WarehouseId);

/* Append-only ledger. Quantity is signed. Stock is never changed without a movement row. */
CREATE TABLE dbo.StockMovements (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockMovements PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockMovements_Warehouses REFERENCES dbo.Warehouses(Id),
    BatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockMovements_Batches REFERENCES dbo.Batches(Id),
    ProductId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockMovements_Products REFERENCES dbo.Products(Id),
    MovementType TINYINT NOT NULL,          -- 1 receive, 2 adjustment, 3 sale, 4 return, 5 transfer, 6 damaged/expired
    Quantity DECIMAL(18,3) NOT NULL,
    Reason NVARCHAR(300) NULL,
    IdempotencyKey NVARCHAR(100) NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_StockMovements_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_StockMovements_NonZero CHECK (Quantity <> 0),
    CONSTRAINT UQ_StockMovements_Idempotency UNIQUE (TenantId, IdempotencyKey)
);
CREATE INDEX IX_StockMovements_Product ON dbo.StockMovements(ProductId, CreatedAtUtc DESC);

INSERT dbo.Permissions (Code, Description) VALUES
 ('inventory.read', 'View stock and batches'), ('inventory.write', 'Receive stock and make adjustments');

-- Existing Administrator roles get the new permissions.
INSERT dbo.RolePermissions (RoleId, PermissionCode)
SELECT r.Id, p.Code FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE r.Name = N'Administrator' AND p.Code IN ('inventory.read', 'inventory.write');
