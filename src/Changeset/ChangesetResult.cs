using System.Collections.Immutable;

namespace Changeset;

public abstract record ChangesetResult<T> where T : class
{
    private ChangesetResult() { }

    public sealed record Valid(T Value) : ChangesetResult<T>;
    public sealed record Invalid(ImmutableArray<ChangesetError> Errors) : ChangesetResult<T>;
}
