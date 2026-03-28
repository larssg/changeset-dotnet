namespace Changeset;

/// <summary>
/// Provides reflection-free property setting for a model type.
/// Implemented by the source generator.
/// </summary>
public interface IChangesetApplier<T> where T : class
{
    /// <summary>
    /// The set of valid writable field names for this type.
    /// </summary>
    IReadOnlySet<string> ValidFields { get; }

    /// <summary>
    /// Creates a new instance of T with the given changes applied.
    /// </summary>
    T Create(IReadOnlyDictionary<string, object?> changes);

    /// <summary>
    /// Creates a shallow copy of the source with the given changes applied on top.
    /// </summary>
    T Apply(T source, IReadOnlyDictionary<string, object?> changes);
}
