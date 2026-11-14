using PharmaCore.Application.Abstractions;

namespace PharmaCore.Application.Drugs;

public sealed record DrugReferenceDto(Guid Id, string NameEn, string? NameAr, string? ScientificName, string? Manufacturer,
    string? DrugClass, string? Route, string? StrengthText, decimal ReferencePrice, bool IsDrug);

public sealed record DrugStagingRow(int RowNumber, string SourceKey, string NameEn, string? NameAr, string? ScientificName,
    string? ActiveKey, string? Manufacturer, string? DrugClass, string? Route, string? StrengthText, decimal Price, bool IsDrug);

public sealed record DrugImportIssue(int RowNumber, string Message, bool RowRejected);

public sealed record DrugImportReport(Guid BatchId, string FileName, int TotalRows, int ValidRows, int Inserted, int Updated,
    int Unchanged, int DuplicatesInFile, int Rejected, IReadOnlyList<DrugImportIssue> Issues);

public sealed class InvalidDrugFileException(string message) : Exception(message);

public interface IDrugReferenceRepository
{
    Task<PagedResult<DrugReferenceDto>> SearchAsync(string? search, bool drugsOnly, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<DrugReferenceDto>> GetAlternativesAsync(Guid id, CancellationToken ct);
    /// <summary>Loads rows into staging, merges them into the catalogue in ONE transaction and records the batch report.</summary>
    Task<DrugImportReport> ImportAsync(Guid userId, string fileName, int totalRows, IReadOnlyList<DrugStagingRow> rows,
        IReadOnlyList<DrugImportIssue> issues, CancellationToken ct);
}
