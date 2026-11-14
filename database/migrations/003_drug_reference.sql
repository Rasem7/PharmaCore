/* PharmaCore migration 003: shared drug reference catalogue (NOT tenant-scoped) + import staging.
   Source data has no barcode, no registration number, no strength/form columns (see docs/migration/drug-reference-data.md). */

CREATE TABLE dbo.DrugImportBatches (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DrugImportBatches PRIMARY KEY,
    FileName NVARCHAR(260) NOT NULL,
    TotalRows INT NOT NULL,
    ValidRows INT NOT NULL,
    Inserted INT NOT NULL,
    Updated INT NOT NULL,
    Unchanged INT NOT NULL,
    DuplicatesInFile INT NOT NULL,
    Rejected INT NOT NULL,
    ReportJson NVARCHAR(MAX) NULL,
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DrugImportBatches_Created DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.DrugImportStaging (
    BatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_DrugImportStaging_Batch REFERENCES dbo.DrugImportBatches(Id),
    RowNumber INT NOT NULL,
    SourceKey CHAR(64) NOT NULL,
    NameEn NVARCHAR(400) NOT NULL,
    NameAr NVARCHAR(400) NULL,
    ScientificName NVARCHAR(1000) NULL,
    ActiveKey NVARCHAR(400) NULL,
    Manufacturer NVARCHAR(400) NULL,
    DrugClass NVARCHAR(400) NULL,
    Route NVARCHAR(50) NULL,
    StrengthText NVARCHAR(100) NULL,
    Price DECIMAL(18,4) NOT NULL,
    IsDrug BIT NOT NULL,
    CONSTRAINT PK_DrugImportStaging PRIMARY KEY (BatchId, RowNumber)
);

CREATE TABLE dbo.DrugReference (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DrugReference PRIMARY KEY,
    SourceKey CHAR(64) NOT NULL,                -- SHA-256 of NAME|MANUFACTURER|ROUTE (source has no id)
    NameEn NVARCHAR(400) NOT NULL,
    NameAr NVARCHAR(400) NULL,
    ScientificName NVARCHAR(1000) NULL,
    ActiveKey NVARCHAR(400) NULL,               -- normalized, sorted active ingredients; NULL when unknown
    Manufacturer NVARCHAR(400) NULL,
    DrugClass NVARCHAR(400) NULL,
    Route NVARCHAR(50) NULL,
    StrengthText NVARCHAR(100) NULL,            -- parsed from the name (heuristic), e.g. 500MG
    ReferencePrice DECIMAL(18,4) NOT NULL,      -- public reference price in EGP, not a binding sale price
    IsDrug BIT NOT NULL,                        -- heuristic: false for cosmetics / skin / hair care
    ImportBatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_DrugReference_Batch REFERENCES dbo.DrugImportBatches(Id),
    UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DrugReference_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_DrugReference_SourceKey UNIQUE (SourceKey)
);
CREATE INDEX IX_DrugReference_NameEn ON dbo.DrugReference(NameEn);
CREATE INDEX IX_DrugReference_Alt ON dbo.DrugReference(ActiveKey, StrengthText, Route) WHERE ActiveKey IS NOT NULL;

INSERT dbo.Permissions (Code, Description) VALUES
 ('drugref.read', 'Search the drug reference catalogue'),
 ('drugref.import', 'Import the shared drug reference catalogue');

INSERT dbo.RolePermissions (RoleId, PermissionCode)
SELECT r.Id, p.Code FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE r.Name = N'Administrator' AND p.Code IN ('drugref.read', 'drugref.import');
