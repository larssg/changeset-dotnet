using System.Collections.Immutable;

namespace Changeset;

/// <summary>
/// A single casting or validation error attached to a changeset.
/// </summary>
/// <param name="Field">The field the error applies to; empty for base (changeset-wide) errors.</param>
/// <param name="Message">A human-readable error message.</param>
/// <param name="Code">A machine-readable error code (e.g. <c>"required"</c>, <c>"invalid_cast"</c>).</param>
/// <param name="Metadata">Optional structured data describing the error, or <c>null</c>.</param>
public sealed record ChangesetError(
    string Field,
    string Message,
    string Code,
    ImmutableDictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Creates an error for the given field.
    /// </summary>
    public static ChangesetError For(string field, string message, string code) =>
        new(field, message, code);

    /// <summary>
    /// Creates an error for the given field with additional metadata.
    /// </summary>
    public static ChangesetError For(
        string field, string message, string code,
        ImmutableDictionary<string, object> metadata) =>
        new(field, message, code, metadata);

    /// <summary>
    /// Creates a base error that applies to the changeset as a whole
    /// rather than a specific field.
    /// </summary>
    public static ChangesetError Base(string message, string code) =>
        new("", message, code);
}
