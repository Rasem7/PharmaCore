using System.Globalization;
using System.Text;
using PharmaCore.Application.Abstractions;

namespace PharmaCore.Application.Drugs;

public sealed class DrugReferenceService(IDrugReferenceRepository repo)
{
    private const int MaxRows = 200_000;
    private static readonly string[] Required = ["commercial_name_en", "price_egp"];

    public Task<PagedResult<DrugReferenceDto>> SearchAsync(string? search, bool drugsOnly, int page, int pageSize, CancellationToken ct)
        => repo.SearchAsync(search?.Trim(), drugsOnly, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);

    public Task<IReadOnlyList<DrugReferenceDto>> GetAlternativesAsync(Guid id, CancellationToken ct) => repo.GetAlternativesAsync(id, ct);

    /// <summary>Expected header (case-insensitive): commercial_name_en, price_egp required; commercial_name_ar, scientific_name, manufacturer, drug_class, route optional.</summary>
    public Task<DrugImportReport> ImportCsvAsync(Guid userId, string fileName, Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var rows = CsvParser.Parse(reader).GetEnumerator();
        if (!rows.MoveNext()) throw new InvalidDrugFileException("The file is empty.");

        var header = rows.Current.Select(h => h.Trim().TrimStart('﻿').ToLowerInvariant()).ToArray();
        var missing = Required.Where(r => Array.IndexOf(header, r) < 0).ToArray();
        if (missing.Length > 0) throw new InvalidDrugFileException("Missing required columns: " + string.Join(", ", missing));

        int Col(string name) => Array.IndexOf(header, name);
        int iEn = Col("commercial_name_en"), iAr = Col("commercial_name_ar"), iSci = Col("scientific_name"),
            iMan = Col("manufacturer"), iCls = Col("drug_class"), iRoute = Col("route"), iPrice = Col("price_egp");
        string? Get(string[] r, int i) => i >= 0 && i < r.Length && !string.IsNullOrWhiteSpace(r[i]) ? r[i].Trim() : null;

        var staged = new List<DrugStagingRow>();
        var issues = new List<DrugImportIssue>();
        var total = 0;
        while (rows.MoveNext())
        {
            var r = rows.Current;
            if (r.Length == 1 && string.IsNullOrWhiteSpace(r[0])) continue; // blank line
            total++;
            if (total > MaxRows) throw new InvalidDrugFileException($"The file has more than {MaxRows} rows.");
            var rowNo = total + 1; // +1 for the header, matches what a spreadsheet shows

            var nameEn = Get(r, iEn);
            if (nameEn is null) { issues.Add(new(rowNo, "Missing commercial_name_en.", true)); continue; }
            if (nameEn.Length > 400) { issues.Add(new(rowNo, "commercial_name_en is longer than 400 characters.", true)); continue; }
            if (!decimal.TryParse(Get(r, iPrice), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
            { issues.Add(new(rowNo, $"Invalid price '{Get(r, iPrice)}'.", true)); continue; }

            var sci = Get(r, iSci);
            var active = DrugNormalizer.ActiveKey(sci);
            if (sci is not null && active is null) issues.Add(new(rowNo, "Active ingredients could not be normalized; no alternatives will be suggested.", false));

            var man = Get(r, iMan); var route = Get(r, iRoute); var cls = Get(r, iCls);
            staged.Add(new DrugStagingRow(rowNo, DrugNormalizer.SourceKey(nameEn, man, route), nameEn, Trunc(Get(r, iAr), 400),
                Trunc(sci, 1000), active, Trunc(man, 400), Trunc(cls, 400), Trunc(route, 50),
                DrugNormalizer.StrengthText(nameEn), price, DrugNormalizer.IsDrug(cls, route)));
        }
        return repo.ImportAsync(userId, fileName, total, staged, issues, ct);
    }

    private static string? Trunc(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
