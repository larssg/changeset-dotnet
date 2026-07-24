using System.Collections.Immutable;
using System.Reflection;

namespace Changeset;

internal interface IChangesetValue
{
    Type ModelType { get; }
    object? UntypedData { get; }
    ImmutableDictionary<string, object?> UntypedChanges { get; }
    bool IsValid { get; }
}

internal static class AssociationMaterializer
{
    public static IReadOnlyDictionary<string, object?> MaterializeChanges(
        IReadOnlyDictionary<string, object?> changes)
    {
        Dictionary<string, object?>? materialized = null;

        foreach (var (field, value) in changes)
        {
            if (value is not IChangesetValue child)
                continue;

            materialized ??= new Dictionary<string, object?>(changes);
            materialized[field] = Materialize(child);
        }

        return materialized ?? changes;
    }

    public static object Materialize(IChangesetValue changeset)
    {
        if (!changeset.IsValid)
            throw new InvalidOperationException(
                "Cannot materialize an association from an invalid changeset.");

        var target = changeset.UntypedData is not null
            ? Clone(changeset.ModelType, changeset.UntypedData)
            : Create(changeset.ModelType);

        Apply(target, changeset.ModelType, changeset.UntypedChanges);
        return target;
    }

    private static object Clone(Type type, object source)
    {
        var clone = Create(type);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite))
        {
            property.SetValue(clone, property.GetValue(source));
        }

        return clone;
    }

    private static object Create(Type type)
    {
        try
        {
            return Activator.CreateInstance(type)
                   ?? throw new InvalidOperationException($"Cannot create instance of {type.Name}.");
        }
        catch (MissingMethodException)
        {
            throw new InvalidOperationException(
                $"Association type '{type.Name}' does not have a parameterless constructor.");
        }
    }

    private static void Apply(
        object target,
        Type type,
        IReadOnlyDictionary<string, object?> changes)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var (field, value) in changes)
        {
            if (!properties.TryGetValue(field, out var property))
                continue;

            property.SetValue(target,
                value is IChangesetValue child ? Materialize(child) : value);
        }
    }
}
