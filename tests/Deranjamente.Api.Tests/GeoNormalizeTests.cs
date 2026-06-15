using Deranjamente.Api.Geo;

namespace Deranjamente.Api.Tests;

/// <summary>Pure folding/distance tests for the geo normalizer — no DB.</summary>
public class GeoNormalizeTests
{
    [Theory]
    // Diacritics fold to ASCII.
    [InlineData("Timișoara", "timisoara")]
    [InlineData("TIMIȘOARA", "timisoara")]
    [InlineData("Sânandrei", "sanandrei")]
    [InlineData("Sânnicolau Mare", "sannicolau mare")]
    // Admin prefixes are dropped (but never the only word).
    [InlineData("Municipiul Timișoara", "timisoara")]
    [InlineData("Comuna Giroc", "giroc")]
    [InlineData("Oraș Recaș", "recas")]
    [InlineData("Sat Dumbrăvița", "dumbravita")]
    // Hyphens/underscores collapse to spaces; surrounding whitespace trimmed.
    [InlineData("Bistrița-Năsăud", "bistrita nasaud")]
    [InlineData("  Giroc  ", "giroc")]
    public void Normalize_FoldsDiacriticsPrefixesAndSpacing(string input, string expected)
    {
        Assert.Equal(expected, GeoNormalize.Normalize(input));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_BlankInput_IsEmpty(string input, string expected)
    {
        Assert.Equal(expected, GeoNormalize.Normalize(input));
    }

    [Fact]
    public void Similarity_IdenticalIsOne_DisjointIsLow()
    {
        Assert.Equal(1.0, GeoNormalize.Similarity("giroc", "giroc"));
        Assert.True(GeoNormalize.Similarity("timisoara", "timisora") > 0.85); // 1-char typo
        Assert.True(GeoNormalize.Similarity("giroc", "lovrin") < 0.5);
    }
}
