using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Drugs;

namespace PharmaCore.Persistence;

public sealed class DrugReferenceRepository(ISqlConnectionFactory db) : IDrugReferenceRepository
{
    private const string Cols = "Id, NameEn, NameAr, ScientificName, Manufacturer, DrugClass, Route, StrengthText, ReferencePrice, IsDrug";
    private const int MaxTerms = 5;

    public async Task<PagedResult<DrugReferenceDto>> SearchAsync(string? search, bool drugsOnly, int page, int pageSize, CancellationToken ct)
    {
        var terms = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MaxTerms).ToArray();
        var p = new DynamicParameters();
        var where = new List<string> { drugsOnly ? "IsDrug = 1" : "1 = 1" };
        for (var i = 0; i < terms.Length; i++)
        {
            p.Add($"t{i}", "%" + Escape(terms[i]) + "%");
            where.Add($"(NameEn LIKE @t{i} ESCAPE '\\' OR NameAr LIKE @t{i} ESCAPE '\\' OR ScientificName LIKE @t{i} ESCAPE '\\')");
        }
        p.Add("prefix", terms.Length > 0 ? Escape(terms[0]) + "%" : "%");
        p.Add("offset", (page - 1) * pageSize);
        p.Add("pageSize", pageSize);
        var w = string.Join(" AND ", where); // only fixed fragments + parameter names are concatenated; user text is always a parameter

        await using var conn = await db.OpenAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM dbo.DrugReference WHERE {w}", p, cancellationToken: ct));
        var items = await conn.QueryAsync<DrugReferenceDto>(new CommandDefinition(
            $@"SELECT {Cols} FROM dbo.DrugReference WHERE {w}
               ORDER BY CASE WHEN NameEn LIKE @prefix ESCAPE '\' THEN 0 ELSE 1 END, NameEn
               OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", p, cancellationToken: ct));
        return new PagedResult<DrugReferenceDto>(items.ToList(), total, page, pageSize);
    }

    public async Task<IReadOnlyList<DrugReferenceDto>> GetAlternativesAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await db.OpenAsync(ct);
        var src = await conn.QuerySingleOrDefaultAsync<(string? ActiveKey, string? StrengthText, string? Route)>(new CommandDefinition(
            "SELECT ActiveKey, StrengthText, Route FROM dbo.DrugReference WHERE Id = @id", new { id }, cancellationToken: ct));
        // Conservative: no active ingredient or no parsed strength means no suggestion.
        if (src.ActiveKey is null || src.StrengthText is null) return [];
        var rows = await conn.QueryAsync<DrugReferenceDto>(new CommandDefinition(
            $@"SELECT TOP (50) {Cols} FROM dbo.DrugReference
               WHERE ActiveKey = @ActiveKey AND StrengthText = @StrengthText AND ISNULL(Route, '') = ISNULL(@Route, '')
                 AND IsDrug = 1 AND Id <> @id
               ORDER BY ReferencePrice, NameEn", new { id, src.ActiveKey, src.StrengthText, src.Route }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<DrugImportReport> ImportAsync(Guid userId, string fileName, int totalRows, IReadOnlyList<DrugStagingRow> rows,
        IReadOnlyList<DrugImportIssue> issues, CancellationToken ct)
    {
        await using var conn = (SqlConnection)await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        var batchId = Guid.NewGuid();
        var rejected = issues.Count(i => i.RowRejected);

        // Batch row first (counts are filled in after the merge), so staging rows can reference it.
        await conn.ExecuteAsync(new CommandDefinition(
            @"INSERT dbo.DrugImportBatches (Id, FileName, TotalRows, ValidRows, Inserted, Updated, Unchanged, DuplicatesInFile, Rejected, CreatedBy)
              VALUES (@batchId, @fileName, @totalRows, @valid, 0, 0, 0, 0, @rejected, @userId)",
            new { batchId, fileName = Path.GetFileName(fileName), totalRows, valid = rows.Count, rejected, userId }, tx, cancellationToken: ct));

        var table = new DataTable();
        foreach (var (name, type) in new (string, Type)[] { ("BatchId", typeof(Guid)), ("RowNumber", typeof(int)), ("SourceKey", typeof(string)),
            ("NameEn", typeof(string)), ("NameAr", typeof(string)), ("ScientificName", typeof(string)), ("ActiveKey", typeof(string)),
            ("Manufacturer", typeof(string)), ("DrugClass", typeof(string)), ("Route", typeof(string)), ("StrengthText", typeof(string)),
            ("Price", typeof(decimal)), ("IsDrug", typeof(bool)) })
            table.Columns.Add(name, type);
        foreach (var r in rows)
            table.Rows.Add(batchId, r.RowNumber, r.SourceKey, r.NameEn, (object?)r.NameAr ?? DBNull.Value, (object?)r.ScientificName ?? DBNull.Value,
                (object?)r.ActiveKey ?? DBNull.Value, (object?)r.Manufacturer ?? DBNull.Value, (object?)r.DrugClass ?? DBNull.Value,
                (object?)r.Route ?? DBNull.Value, (object?)r.StrengthText ?? DBNull.Value, r.Price, r.IsDrug);

        using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tx) { DestinationTableName = "dbo.DrugImportStaging", BulkCopyTimeout = 300 })
        {
            foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            await bulk.WriteToServerAsync(table, ct);
        }

        // If a source key appears more than once in the file, the LAST row wins (reported as DuplicatesInFile).
        var counts = await conn.QuerySingleAsync<(int Inserted, int Updated, int Distinct)>(new CommandDefinition(@"
            DECLARE @actions TABLE (Act NVARCHAR(10));
            ;WITH src AS (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY SourceKey ORDER BY RowNumber DESC) AS rn
                FROM dbo.DrugImportStaging WHERE BatchId = @batchId)
            MERGE dbo.DrugReference AS t
            USING (SELECT * FROM src WHERE rn = 1) AS s ON t.SourceKey = s.SourceKey
            WHEN MATCHED AND EXISTS (
                    SELECT s.NameEn, s.NameAr, s.ScientificName, s.ActiveKey, s.Manufacturer, s.DrugClass, s.Route, s.StrengthText, s.Price, s.IsDrug
                    EXCEPT
                    SELECT t.NameEn, t.NameAr, t.ScientificName, t.ActiveKey, t.Manufacturer, t.DrugClass, t.Route, t.StrengthText, t.ReferencePrice, t.IsDrug)
                THEN UPDATE SET NameEn = s.NameEn, NameAr = s.NameAr, ScientificName = s.ScientificName, ActiveKey = s.ActiveKey,
                    Manufacturer = s.Manufacturer, DrugClass = s.DrugClass, Route = s.Route, StrengthText = s.StrengthText,
                    ReferencePrice = s.Price, IsDrug = s.IsDrug, ImportBatchId = @batchId, UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Id, SourceKey, NameEn, NameAr, ScientificName, ActiveKey, Manufacturer, DrugClass, Route, StrengthText, ReferencePrice, IsDrug, ImportBatchId)
                VALUES (NEWID(), s.SourceKey, s.NameEn, s.NameAr, s.ScientificName, s.ActiveKey, s.Manufacturer, s.DrugClass, s.Route, s.StrengthText, s.Price, s.IsDrug, @batchId)
            OUTPUT $action INTO @actions;
            SELECT (SELECT COUNT(*) FROM @actions WHERE Act = 'INSERT') AS Inserted,
                   (SELECT COUNT(*) FROM @actions WHERE Act = 'UPDATE') AS Updated,
                   (SELECT COUNT(DISTINCT SourceKey) FROM dbo.DrugImportStaging WHERE BatchId = @batchId) AS [Distinct];",
            new { batchId }, tx, commandTimeout: 300, cancellationToken: ct));

        var unchanged = counts.Distinct - counts.Inserted - counts.Updated;
        var duplicates = rows.Count - counts.Distinct;
        var shown = issues.Take(200).ToList();
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.DrugImportBatches SET Inserted = @Inserted, Updated = @Updated, Unchanged = @unchanged,
                 DuplicatesInFile = @duplicates, ReportJson = @json WHERE Id = @batchId",
            new { counts.Inserted, counts.Updated, unchanged, duplicates, json = JsonSerializer.Serialize(shown), batchId }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return new DrugImportReport(batchId, Path.GetFileName(fileName), totalRows, rows.Count, counts.Inserted, counts.Updated,
            unchanged, duplicates, rejected, shown);
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
