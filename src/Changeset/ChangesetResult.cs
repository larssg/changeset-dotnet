using System.Collections.Immutable;

namespace Changeset;

/// <summary>
/// The outcome of applying a changeset: either a <see cref="Valid"/> result carrying the
/// materialized model, or an <see cref="Invalid"/> result carrying the changeset's errors.
/// </summary>
/// <typeparam name="T">The model type the changeset targets.</typeparam>
public abstract record ChangesetResult<T> where T : class
{
    private ChangesetResult() { }

    /// <summary>
    /// A successful result.
    /// </summary>
    /// <param name="Value">The model instance with all changes applied.</param>
    public sealed record Valid(T Value) : ChangesetResult<T>;

    /// <summary>
    /// A failed result.
    /// </summary>
    /// <param name="Errors">The errors that made the changeset invalid.</param>
    public sealed record Invalid(ImmutableArray<ChangesetError> Errors) : ChangesetResult<T>;
}
