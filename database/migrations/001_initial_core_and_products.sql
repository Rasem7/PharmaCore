/* PharmaCore migration 001: core, security, products, audit, sync outbox.
   NOT idempotent by itself: the DbMigrator (Persistence/DbMigrator.cs) applies each script once,
   inside a transaction, and records it in dbo.SchemaVersions.
   Money columns use DECIMAL(18,4), quantities DECIMAL(18,3). All timestamps are UTC (DATETIME2(3)). */

CREATE TABLE dbo.Tenants (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Tenants_Active DEFAULT 1,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Tenants_Created DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Branches (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Branches PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Branches_Tenants REFERENCES dbo.Tenants(Id),
    Code NVARCHAR(20) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Address NVARCHAR(400) NULL,
    Phone NVARCHAR(40) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Branches_Active DEFAULT 1,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Branches_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Branches_Tenant_Code UNIQUE (TenantId, Code)
);

CREATE TABLE dbo.Warehouses (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Warehouses PRIMARY KEY,
    BranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Warehouses_Branches REFERENCES dbo.Branches(Id),
    Name NVARCHAR(200) NOT NULL,
    IsDefault BIT NOT NULL CONSTRAINT DF_Warehouses_Default DEFAULT 0,
    IsActive BIT NOT NULL CONSTRAINT DF_Warehouses_Active DEFAULT 1
);

CREATE TABLE dbo.Users (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Users_Tenants REFERENCES dbo.Tenants(Id),
    UserName NVARCHAR(100) NOT NULL,
    FullName NVARCHAR(200) NULL,
    PasswordHash NVARCHAR(400) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Users_Active DEFAULT 1,
    FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_Failed DEFAULT 0,
    LockedUntilUtc DATETIME2(3) NULL,
    LastLoginAtUtc DATETIME2(3) NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Users_Created DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_Users_UserName ON dbo.Users(UserName);

CREATE TABLE dbo.Roles (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Roles_Tenants REFERENCES dbo.Tenants(Id),
    Name NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_Roles_Tenant_Name UNIQUE (TenantId, Name)
);

CREATE TABLE dbo.Permissions (
    Code NVARCHAR(100) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
    Description NVARCHAR(300) NULL
);

CREATE TABLE dbo.RolePermissions (
    RoleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_RolePermissions_Roles REFERENCES dbo.Roles(Id),
    PermissionCode NVARCHAR(100) NOT NULL CONSTRAINT FK_RolePermissions_Permissions REFERENCES dbo.Permissions(Code),
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionCode)
);

CREATE TABLE dbo.UserRoles (
    UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserRoles_Users REFERENCES dbo.Users(Id),
    RoleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UserRoles_Roles REFERENCES dbo.Roles(Id),
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE dbo.UserBranchAccess (
    UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UBA_Users REFERENCES dbo.Users(Id),
    BranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_UBA_Branches REFERENCES dbo.Branches(Id),
    CONSTRAINT PK_UserBranchAccess PRIMARY KEY (UserId, BranchId)
);

CREATE TABLE dbo.AuditLogs (
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NULL,
    UserId UNIQUEIDENTIFIER NULL,
    Action NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(100) NULL,
    EntityId NVARCHAR(100) NULL,
    Details NVARCHAR(MAX) NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AuditLogs_Created DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_AuditLogs_Tenant_Created ON dbo.AuditLogs(TenantId, CreatedAtUtc DESC);

CREATE TABLE dbo.Products (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Products_Tenants REFERENCES dbo.Tenants(Id),
    CommercialName NVARCHAR(200) NOT NULL,
    NameAr NVARCHAR(200) NULL,
    NameEn NVARCHAR(200) NULL,
    ScientificName NVARCHAR(300) NULL,
    PrimaryBarcode NVARCHAR(64) NULL,
    PharmaceuticalForm NVARCHAR(100) NULL,
    PurchasePrice DECIMAL(18,4) NOT NULL CONSTRAINT DF_Products_PP DEFAULT 0,
    SellingPrice DECIMAL(18,4) NOT NULL CONSTRAINT DF_Products_SP DEFAULT 0,
    MinStock DECIMAL(18,3) NOT NULL CONSTRAINT DF_Products_Min DEFAULT 0,
    MaxStock DECIMAL(18,3) NULL,
    Status TINYINT NOT NULL CONSTRAINT DF_Products_Status DEFAULT 1,
    CreatedAtUtc DATETIME2(3) NOT NULL,
    UpdatedAtUtc DATETIME2(3) NULL,
    CONSTRAINT CK_Products_Prices CHECK (PurchasePrice >= 0 AND SellingPrice >= 0)
);
-- Barcode unique per tenant when present (filtered unique index).
CREATE UNIQUE INDEX UX_Products_Tenant_Barcode ON dbo.Products(TenantId, PrimaryBarcode) WHERE PrimaryBarcode IS NOT NULL;
CREATE INDEX IX_Products_Tenant_Name ON dbo.Products(TenantId, CommercialName);

CREATE TABLE dbo.OutboxMessages (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OutboxMessages PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NULL,
    MessageType NVARCHAR(200) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    IdempotencyKey NVARCHAR(100) NOT NULL,
    Status TINYINT NOT NULL CONSTRAINT DF_Outbox_Status DEFAULT 0, -- 0 pending, 1 synced, 2 failed
    Attempts INT NOT NULL CONSTRAINT DF_Outbox_Attempts DEFAULT 0,
    LastError NVARCHAR(2000) NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Outbox_Created DEFAULT SYSUTCDATETIME(),
    SyncedAtUtc DATETIME2(3) NULL,
    CONSTRAINT UQ_Outbox_Idempotency UNIQUE (IdempotencyKey)
);
CREATE INDEX IX_Outbox_Status ON dbo.OutboxMessages(Status, CreatedAtUtc);

INSERT dbo.Permissions(Code, Description) VALUES
 ('products.read','View products'),('products.write','Create and edit products'),
 ('users.manage','Manage users and roles'),('sync.view','View synchronization status');

