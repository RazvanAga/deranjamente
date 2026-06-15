using Deranjamente.Api.Domain;
using Deranjamente.Api.Geo;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Tests;

/// <summary>
/// Table-driven resolution tests over a fixed SIRUTA subset (Timiș + a neighbouring Arad row to
/// prove județ isolation). Exercises the full ladder — exact, normalized (diacritic/prefix),
/// fuzzy-above-threshold, alias-for-mangled-form, and clean below-threshold failure.
/// </summary>
public class GeoResolverTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Fixed subset SIRUTA codes — local to the test, independent of the production seed.
    private const string Timisoara = "155252";
    private const string Giroc = "158800";
    private const string Sanandrei = "160100";
    private const string Pecica = "020099"; // Arad — must never be returned for a Timiș query.

    private async Task SeedAsync()
    {
        await using var ctx = fixture.NewContext();
        if (await ctx.Judete.AnyAsync(j => j.Code == "TM"))
        {
            return; // shared DB: seed once for the class
        }

        ctx.Judete.AddRange(
            new Judet { Code = "TM", Name = "Timiș", IsCovered = true },
            new Judet { Code = "AR", Name = "Arad", IsCovered = false });

        ctx.Localitati.AddRange(
            Loc(Timisoara, "Timișoara", "TM"),
            Loc(Giroc, "Giroc", "TM"),
            Loc(Sanandrei, "Sânandrei", "TM"),
            Loc(Pecica, "Pecica", "AR"));

        // Curated correction: the PDF-mangled "bnandrei" maps to Sânandrei within Timiș.
        ctx.LocalitateAliases.Add(new LocalitateAlias
        {
            JudetCode = "TM", NormalizedAlias = "bnandrei", SirutaCode = Sanandrei,
        });

        await ctx.SaveChangesAsync();
    }

    private static Localitate Loc(string siruta, string name, string judet) => new()
    {
        SirutaCode = siruta, Name = name, JudetCode = judet,
        NormalizedName = GeoNormalize.Normalize(name),
    };

    private async Task<GeoMatch> Resolve(string judet, string localitate)
    {
        await SeedAsync();
        await using var ctx = fixture.NewContext();
        return await new GeoResolver(ctx).ResolveAsync(judet, localitate);
    }

    [Theory]
    [InlineData("Timișoara", Timisoara, GeoMatchKind.Exact)]            // exact, with diacritics
    [InlineData("Giroc", Giroc, GeoMatchKind.Exact)]
    [InlineData("timisoara", Timisoara, GeoMatchKind.Normalized)]       // diacritics stripped
    [InlineData("Municipiul Timișoara", Timisoara, GeoMatchKind.Normalized)] // prefix dropped
    [InlineData("Comuna Giroc", Giroc, GeoMatchKind.Normalized)]
    [InlineData("Timisora", Timisoara, GeoMatchKind.Fuzzy)]             // 1-char typo, above threshold
    [InlineData("bnandrei", Sanandrei, GeoMatchKind.Alias)]            // PDF-mangled → alias wins
    public async Task Resolve_LadderMatches(string input, string expectedSiruta, GeoMatchKind expectedKind)
    {
        var match = await Resolve("Timiș", input);
        Assert.Equal(expectedKind, match.Kind);
        Assert.Equal(expectedSiruta, match.SirutaCode);
    }

    [Fact]
    public async Task Resolve_BelowThreshold_IsUnresolved_NotGuessed()
    {
        var match = await Resolve("Timiș", "Necunoscutville");
        Assert.False(match.Resolved);
        Assert.Equal(GeoMatchKind.Unresolved, match.Kind);
        Assert.Null(match.SirutaCode);
    }

    [Fact]
    public async Task Resolve_JudetIsNeverOverriddenByLocalitateMatch()
    {
        // "Pecica" only exists in Arad; querying within Timiș must NOT cross counties.
        var inTimis = await Resolve("Timiș", "Pecica");
        Assert.False(inTimis.Resolved);

        // Sanity: the same name resolves inside its real județ.
        var inArad = await Resolve("Arad", "Pecica");
        Assert.Equal(Pecica, inArad.SirutaCode);
        Assert.Equal(GeoMatchKind.Exact, inArad.Kind);
    }

    [Fact]
    public async Task Resolve_UnknownJudet_IsUnresolved()
    {
        var match = await Resolve("Cluj", "Timișoara");
        Assert.False(match.Resolved);
    }
}
