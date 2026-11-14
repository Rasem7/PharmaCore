using PharmaCore.Application.Drugs;
using Xunit;

namespace PharmaCore.Tests;

public class DrugNormalizerTests
{
    [Theory]
    [InlineData("PARACETAMOL(ACETAMINOPHEN)", "PARACETAMOL")]
    [InlineData("CHLORPHENIRAMINE+PARACETAMOL(ACETAMINOPHEN)+PSEUDOEPHEDRINE", "CHLORPHENIRAMINE+PARACETAMOL+PSEUDOEPHEDRINE")]
    [InlineData("pseudoephedrine + paracetamol", "PARACETAMOL+PSEUDOEPHEDRINE")]   // order-independent
    [InlineData("VITAMIN C 1 GM", "VITAMIN C")]                                  // strength removed
    [InlineData("AMOXICILLIN+CLAVULANIC ACID", "AMOXICILLIN+CLAVULANIC ACID")]
    public void ActiveKey_normalizes(string input, string expected) => Assert.Equal(expected, DrugNormalizer.ActiveKey(input));

    [Theory] [InlineData(null)] [InlineData("")] [InlineData("   ")] [InlineData("(ONLY SYNONYM)")]
    public void ActiveKey_returns_null_when_unknown(string? input) => Assert.Null(DrugNormalizer.ActiveKey(input));

    [Fact] public void ActiveKey_too_long_returns_null()
        => Assert.Null(DrugNormalizer.ActiveKey(string.Join('+', Enumerable.Range(0, 60).Select(i => "INGREDIENT" + i))));

    [Theory]
    [InlineData("PANADOL 500 MG 24 F.C.TABS.", "500MG")]
    [InlineData("AUGMENTIN 1 GM 14 F.C.TABS.", "1000MG")]
    [InlineData("CATAFLAM 15MG/ML ORAL DROPS 15 ML", "15MG")]
    [InlineData("SOMETHING 0.5 MG TABS", "0.5MG")]
    [InlineData("MIXTURE 500MG+30MG CAPS", "500MG+30MG")]
    public void StrengthText_parses_common_patterns(string name, string expected)
        => Assert.StartsWith(expected, DrugNormalizer.StrengthText(name));

    [Fact] public void StrengthText_ignores_volume_only_names() => Assert.Null(DrugNormalizer.StrengthText("1 2 3 SUSP. 120 ML"));

    [Theory]
    [InlineData("SKIN CARE", "TOPICAL", false)]
    [InlineData("HAIR CARE", "TOPICAL", false)]
    [InlineData("ANTINEOPLASTIC", "INJECTION", true)]
    [InlineData(null, "SOAP", false)]
    [InlineData(null, null, true)]
    public void IsDrug_flags_cosmetics(string? cls, string? route, bool expected) => Assert.Equal(expected, DrugNormalizer.IsDrug(cls, route));

    [Fact] public void SourceKey_is_stable_and_case_insensitive()
        => Assert.Equal(DrugNormalizer.SourceKey("Panadol", "GSK", "ORAL.SOLID"), DrugNormalizer.SourceKey("PANADOL ", "gsk", "oral.solid"));
}

public class CsvParserTests
{
    private static List<string[]> P(string s) => CsvParser.Parse(new StringReader(s)).ToList();

    [Fact] public void Parses_quotes_commas_and_escaped_quotes()
    {
        var rows = P("a,b,c\n\"x,y\",\"he said \"\"hi\"\"\",z\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["x,y", "he said \"hi\"", "z"], rows[1]);
    }

    [Fact] public void Handles_crlf_and_missing_final_newline()
    {
        var rows = P("a,b\r\n1,2");
        Assert.Equal(2, rows.Count); Assert.Equal(["1", "2"], rows[1]);
    }

    [Fact] public void Keeps_empty_fields_and_arabic_text()
        => Assert.Equal(["", "إكسترا", ""], P(",إكسترا,")[0]);

    [Fact] public void Newline_inside_quotes_stays_in_the_field()
        => Assert.Equal("l1\nl2", P("\"l1\nl2\",x")[0][0]);
}
