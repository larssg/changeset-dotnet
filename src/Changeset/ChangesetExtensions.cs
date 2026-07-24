using System.Collections.Concurrent;
using System.Reflection;

namespace Changeset;

/// <summary>
/// Extension methods for applying a valid changeset's changes to a model instance.
/// </summary>
public static class ChangesetExtensions
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReadWritePropertyCache = new();
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> WritablePropertyMapCache = new();

    private static PropertyInfo[] GetReadWriteProperties(Type type)
    {
        return ReadWritePropertyCache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray());
    }

    private static Dictionary<string, PropertyInfo> GetWritablePropertyMap(Type type)
    {
        return WritablePropertyMapCache.GetOrAdd(type, static t =>
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.CanWrite)
                    map[p.Name] = p;
            }
            return map;
        });
    }

    /// <summary>
    /// Applies the changeset's changes and returns the resulting model: a shallow clone
    /// of the existing data with changes on top for updates, or a new instance for inserts.
    /// Uses a generated <see cref="IChangesetApplier{T}"/> when registered, otherwise reflection.
    /// The original <see cref="Changeset{T}.Data"/> is never mutated.
    /// </summary>
    /// <exception cref="InvalidOperationException">The changeset has errors.</exception>
    public static T ApplyChanges<T>(this Changeset<T> changeset) where T : class, new()
    {
        if (!changeset.IsValid)
            throw new InvalidOperationException(
                $"Cannot apply changes from an invalid changeset. " +
                $"Changeset has {changeset.Errors.Length} error(s). " +
                $"Check IsValid before calling ApplyChanges().");

        // Use generated applier if available (no reflection)
        var applier = ChangesetApplierRegistry.Get<T>();
        var materializedChanges = AssociationMaterializer.MaterializeChanges(changeset.Changes);
        if (applier is not null)
        {
            return changeset.Data is not null
                ? applier.Apply(changeset.Data, materializedChanges)
                : applier.Create(materializedChanges);
        }

        var target = changeset.Data is not null
            ? ShallowClone(changeset.Data)
            : new T();

        ApplyChangesToTarget(target, materializedChanges);
        return target;
    }

    /// <summary>
    /// Applies the changeset's changes using a factory to create new instances, for model
    /// types without a parameterless constructor. Updates shallow-clone the existing data;
    /// inserts call <paramref name="factory"/>.
    /// </summary>
    /// <param name="changeset">The changeset to apply. Must be valid.</param>
    /// <param name="factory">Creates the target instance when there is no existing data.</param>
    /// <exception cref="InvalidOperationException">The changeset has errors.</exception>
    public static T ApplyChanges<T>(
        this Changeset<T> changeset, Func<T> factory) where T : class
    {
        if (!changeset.IsValid)
            throw new InvalidOperationException(
                $"Cannot apply changes from an invalid changeset. " +
                $"Changeset has {changeset.Errors.Length} error(s). " +
                $"Check IsValid before calling ApplyChanges().");

        var target = changeset.Data is not null
            ? ShallowClone(changeset.Data)
            : factory();

        ApplyChangesToTarget(target,
            AssociationMaterializer.MaterializeChanges(changeset.Changes));
        return target;
    }

    /// <summary>
    /// Converts the changeset into a <see cref="ChangesetResult{T}"/>: applies the changes
    /// and returns <see cref="ChangesetResult{T}.Valid"/> when valid, otherwise
    /// <see cref="ChangesetResult{T}.Invalid"/> with the errors.
    /// </summary>
    public static ChangesetResult<T> ToResult<T>(this Changeset<T> changeset) where T : class, new()
    {
        if (changeset.IsValid)
            return new ChangesetResult<T>.Valid(changeset.ApplyChanges());

        return new ChangesetResult<T>.Invalid(changeset.Errors);
    }

    /// <summary>
    /// Converts the changeset into a <see cref="ChangesetResult{T}"/> using a factory to
    /// create new instances, for model types without a parameterless constructor.
    /// </summary>
    /// <param name="changeset">The changeset to convert.</param>
    /// <param name="factory">Creates the target instance when there is no existing data.</param>
    public static ChangesetResult<T> ToResult<T>(
        this Changeset<T> changeset, Func<T> factory) where T : class
    {
        if (changeset.IsValid)
            return new ChangesetResult<T>.Valid(changeset.ApplyChanges(factory));

        return new ChangesetResult<T>.Invalid(changeset.Errors);
    }

    private static void ApplyChangesToTarget<T>(
        T target, IReadOnlyDictionary<string, object?> changes) where T : class
    {
        var propMap = GetWritablePropertyMap(typeof(T));

        foreach (var (field, value) in changes)
        {
            if (propMap.TryGetValue(field, out var prop))
                prop.SetValue(target, value);
        }
    }

    private static T ShallowClone<T>(T source) where T : class
    {
        var type = typeof(T);
        object clone;
        try
        {
            clone = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Cannot create instance of {type.Name}.");
        }
        catch (MissingMethodException)
        {
            throw new InvalidOperationException(
                $"Type '{type.Name}' does not have a parameterless constructor. " +
                $"Use ApplyChanges(factory) with a factory function that creates the instance.");
        }

        foreach (var prop in GetReadWriteProperties(type))
        {
            prop.SetValue(clone, prop.GetValue(source));
        }

        return (T)clone;
    }
}
