using System.Collections.Concurrent;

namespace Changeset;

/// <summary>
/// Registry for generated IChangesetApplier implementations.
/// The source generator emits a module initializer that registers appliers here.
/// Falls back to reflection if no applier is registered.
/// </summary>
public static class ChangesetApplierRegistry
{
    private static readonly ConcurrentDictionary<Type, object> Appliers = new();

    /// <summary>
    /// Registers an applier for <typeparamref name="T"/>, replacing any existing registration.
    /// </summary>
    public static void Register<T>(IChangesetApplier<T> applier) where T : class
    {
        Appliers[typeof(T)] = applier;
    }

    /// <summary>
    /// Returns the registered applier for <typeparamref name="T"/>, or <c>null</c> if none exists.
    /// </summary>
    public static IChangesetApplier<T>? Get<T>() where T : class
    {
        return Appliers.TryGetValue(typeof(T), out var applier)
            ? (IChangesetApplier<T>)applier
            : null;
    }
}
