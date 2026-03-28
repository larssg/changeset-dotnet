using System.Collections.Immutable;

namespace Changeset;

public sealed record ChangesetError(
    string Field,
    string Message,
    string Code,
    ImmutableDictionary<string, object>? Metadata = null)
{
    public static ChangesetError For(string field, string message, string code) =>
        new(field, message, code);

    public static ChangesetError For(
        string field, string message, string code,
        ImmutableDictionary<string, object> metadata) =>
        new(field, message, code, metadata);

    public static ChangesetError Base(string message, string code) =>
        new("", message, code);
}
