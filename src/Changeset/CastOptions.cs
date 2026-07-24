namespace Changeset;

/// <summary>
/// Options controlling how params are cast into a changeset.
/// </summary>
public sealed record CastOptions
{
    /// <summary>
    /// When <c>true</c>, params that are not in the permitted field list produce an
    /// <c>unpermitted_field</c> error instead of being silently ignored. Defaults to <c>false</c>.
    /// </summary>
    public bool StrictCasting { get; init; }

    /// <summary>
    /// The format provider used when parsing strings into numbers, dates, and similar
    /// types. Defaults to the invariant culture when <c>null</c>.
    /// </summary>
    public IFormatProvider? FormatProvider { get; init; }

    /// <summary>
    /// Whether string param values are trimmed of leading and trailing whitespace
    /// before coercion. Defaults to <c>true</c>.
    /// </summary>
    public bool TrimStrings { get; init; } = true;

    /// <summary>
    /// Whether param keys are matched to field names case-insensitively.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool CaseInsensitiveFields { get; init; } = true;

    /// <summary>
    /// The default options: lenient casting, invariant culture, trimmed strings,
    /// case-insensitive field matching.
    /// </summary>
    public static CastOptions Default { get; } = new();
}
