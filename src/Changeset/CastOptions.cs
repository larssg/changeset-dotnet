namespace Changeset;

public sealed record CastOptions
{
    public bool StrictCasting { get; init; }
    public IFormatProvider? FormatProvider { get; init; }
    public bool TrimStrings { get; init; } = true;
    public bool CaseInsensitiveFields { get; init; } = true;

    public static CastOptions Default { get; } = new();
}
