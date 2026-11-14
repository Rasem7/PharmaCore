using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PharmaCore.Application.Drugs;

/// <summary>
/// Heuristic normalization of the source catalogue. Everything here is deliberately conservative:
/// when unsure, return null so that no alternative is suggested rather than a wrong one.
/// </summary>
public static class DrugNormalizer
{
    private static readonly Regex Parenthetical = new(@"\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex StrengthInText = new(@"(?<![\d.,])(\d+(?:[.,]\d+)?)\s*(MG|MCG|GM|G|IU|%)(?![A-Z])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private static readonly string[] NonDrugClassKeywords =
        ["SKIN CARE", "HAIR CARE", "COSMETIC", "SHAMPOO", "SOAP", "DEODORANT", "SUN SCREEN", "SUNSCREEN", "SUN CARE", "LIP CARE", "MAKE UP", "MAKEUP", "TOOTHPASTE"];

    public const int MaxActiveKeyLength = 400;

    /// <summary>Sorted, de-duplicated, upper-case ingredient list joined by '+', without synonyms in parentheses or strengths.</summary>
    public static string? ActiveKey(string? scientificName)
    {
        if (string.IsNullOrWhiteSpace(scientificName)) return null;
        var s = Parenthetical.Replace(scientificName.ToUpperInvariant(), " ");
        var parts = s.Split('+')
            .Select(p => Spaces.Replace(StrengthInText.Replace(p, " "), " ").Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
        if (parts.Length == 0) return null;
        var key = string.Join('+', parts);
        return key.Length <= MaxActiveKeyLength ? key : null;
    }

    /// <summary>Strengths found in the commercial name, in order, e.g. "500MG" or "500MG+30MG". G/GM are converted to MG. Null if none.</summary>
    public static string? StrengthText(string? commercialName)
    {
        if (string.IsNullOrWhiteSpace(commercialName)) return null;
        var found = new List<string>();
        foreach (Match m in StrengthInText.Matches(commercialName))
        {
            if (!decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var n)) continue;
            var unit = m.Groups[2].Value.ToUpperInvariant();
            if (unit is "G" or "GM") { n *= 1000m; unit = "MG"; }
            found.Add(n.ToString("0.####", CultureInfo.InvariantCulture) + unit);
        }
        return found.Count == 0 ? null : string.Join('+', found);
    }

    public static bool IsDrug(string? drugClass, string? route)
    {
        if (string.Equals(route?.Trim(), "SOAP", StringComparison.OrdinalIgnoreCase)) return false;
        var c = drugClass?.ToUpperInvariant() ?? "";
        return !NonDrugClassKeywords.Any(k => c.Contains(k));
    }

    public static string SourceKey(string nameEn, string? manufacturer, string? route)
    {
        var raw = $"{nameEn.Trim().ToUpperInvariant()}|{manufacturer?.Trim().ToUpperInvariant()}|{route?.Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
