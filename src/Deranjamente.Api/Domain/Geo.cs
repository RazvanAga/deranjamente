namespace Deranjamente.Api.Domain;

/// <summary>
/// A Romanian județ (county). Seeded from the canonical list (41 județe + municipiul București).
/// <see cref="Code"/> is the stable 2-letter mnemonic (e.g. "TM") used to key the choropleth
/// GeoJSON; județ is taken from crawler config, never decided by a fuzzy localitate match.
/// </summary>
public class Judet
{
    public int Id { get; set; }

    /// <summary>2-letter mnemonic, e.g. "TM" — the choropleth GeoJSON key.</summary>
    public required string Code { get; set; }

    /// <summary>Display name with diacritics, e.g. "Timiș".</summary>
    public required string Name { get; set; }

    /// <summary>SIRUTA county code; nullable until reconciled with the full official dataset.</summary>
    public string? SirutaCode { get; set; }

    /// <summary>Whether a crawler currently covers this județ (drives the map's "not-covered" state).</summary>
    public bool IsCovered { get; set; }
}

/// <summary>
/// A localitate (town/commune/village) within a județ, keyed by its canonical SIRUTA code.
/// <see cref="NormalizedName"/> is precomputed (diacritic/prefix/spacing-folded) so the
/// <c>GeoResolver</c> can do exact and fuzzy matching against the closed set for a județ.
/// </summary>
public class Localitate
{
    public int Id { get; set; }

    /// <summary>Canonical SIRUTA code — stable identity across sources.</summary>
    public required string SirutaCode { get; set; }

    /// <summary>Official name with diacritics.</summary>
    public required string Name { get; set; }

    /// <summary>Folded form used for normalized/fuzzy matching (see <c>GeoNormalize</c>).</summary>
    public required string NormalizedName { get; set; }

    public required string JudetCode { get; set; }
}

/// <summary>
/// An admin/curated alias mapping a (mangled or alternate) localitate spelling to a canonical
/// SIRUTA code within a județ. Consulted before fuzzy matching so a correction, once recorded,
/// forces a specific resolution permanently (e.g. PDF-mangled "bnandrei" → Sânandrei).
/// </summary>
public class LocalitateAlias
{
    public int Id { get; set; }

    public required string JudetCode { get; set; }

    /// <summary>The alias in folded form (matched against the normalized crawled name).</summary>
    public required string NormalizedAlias { get; set; }

    public required string SirutaCode { get; set; }
}
