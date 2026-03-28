using System.Reflection;

namespace Changeset;

public static class ChangesetExtensions
{
    public static T ApplyChanges<T>(this Changeset<T> changeset) where T : class, new()
    {
        if (!changeset.IsValid)
            throw new InvalidOperationException(
                $"Cannot apply changes from an invalid changeset. " +
                $"Changeset has {changeset.Errors.Length} error(s). " +
                $"Check IsValid before calling ApplyChanges().");

        // Use generated applier if available (no reflection)
        var applier = ChangesetApplierRegistry.Get<T>();
        if (applier is not null)
        {
            return changeset.Data is not null
                ? applier.Apply(changeset.Data, changeset.Changes)
                : applier.Create(changeset.Changes);
        }

        var target = changeset.Data is not null
            ? ShallowClone(changeset.Data)
            : new T();

        ApplyChangesToTarget(target, changeset.Changes);
        return target;
    }

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

        ApplyChangesToTarget(target, changeset.Changes);
        return target;
    }

    public static ChangesetResult<T> ToResult<T>(this Changeset<T> changeset) where T : class, new()
    {
        if (changeset.IsValid)
            return new ChangesetResult<T>.Valid(changeset.ApplyChanges());

        return new ChangesetResult<T>.Invalid(changeset.Errors);
    }

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
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propMap = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        foreach (var p in properties)
        {
            if (p.CanWrite)
                propMap[p.Name] = p;
        }

        foreach (var (field, value) in changes)
        {
            if (propMap.TryGetValue(field, out var prop))
                prop.SetValue(target, value);
        }
    }

    private static T ShallowClone<T>(T source) where T : class
    {
        var type = typeof(T);
        var clone = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Cannot create instance of {type.Name}");

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead && prop.CanWrite)
                prop.SetValue(clone, prop.GetValue(source));
        }

        return (T)clone;
    }
}
