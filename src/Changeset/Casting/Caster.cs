using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;

namespace Changeset.Casting;

internal static class Caster
{
    private static readonly ConcurrentDictionary<(Type, bool), Dictionary<string, PropertyInfo>> PropertyCache = new();

    public static Changeset<T> Cast<T>(
        T? data,
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions options) where T : class
    {
        var action = data is null ? ChangesetAction.Insert : ChangesetAction.Update;
        var properties = GetPropertyMap<T>(options.CaseInsensitiveFields);
        var permittedSet = permitted.ToHashSet(
            options.CaseInsensitiveFields ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var changesBuilder = ImmutableDictionary.CreateBuilder<string, object?>();
        var castFieldsBuilder = ImmutableHashSet.CreateBuilder<string>();
        var errorsBuilder = ImmutableArray.CreateBuilder<ChangesetError>();

        var paramsDict = @params as ImmutableDictionary<string, object?>
            ?? @params.ToImmutableDictionary();

        if (options.StrictCasting)
        {
            foreach (var key in @params.Keys)
            {
                if (!permittedSet.Contains(key))
                {
                    errorsBuilder.Add(ChangesetError.For(key, "is not permitted", "unpermitted_field"));
                }
            }
        }

        foreach (var fieldName in permitted)
        {
            // Find the matching param key
            var paramKey = FindParamKey(@params, fieldName, options.CaseInsensitiveFields);
            if (paramKey is null)
                continue;

            // Find the matching property
            if (!properties.TryGetValue(fieldName, out var property))
            {
                errorsBuilder.Add(ChangesetError.For(
                    fieldName, $"unknown field '{fieldName}'", "unknown_field"));
                continue;
            }

            var rawValue = @params[paramKey];

            // Trim strings if configured
            if (options.TrimStrings && rawValue is string strVal)
                rawValue = strVal.Trim();

            // Attempt coercion
            var (success, coercedValue) = TypeCoercion.TryCoerce(
                rawValue, property.PropertyType, options.FormatProvider);

            if (success)
            {
                // For updates, skip if value hasn't changed
                if (data is not null)
                {
                    var currentValue = property.GetValue(data);
                    if (Equals(currentValue, coercedValue))
                        continue;
                }

                changesBuilder[property.Name] = coercedValue;
                castFieldsBuilder.Add(property.Name);
            }
            else
            {
                errorsBuilder.Add(ChangesetError.For(
                    property.Name,
                    $"is invalid",
                    "invalid_cast",
                    ImmutableDictionary.CreateRange(new[]
                    {
                        KeyValuePair.Create<string, object>("expected_type", property.PropertyType.Name),
                        KeyValuePair.Create<string, object>("received_value", rawValue?.ToString() ?? "null")
                    })));
            }
        }

        return new Changeset<T>(
            data,
            changesBuilder.ToImmutable(),
            errorsBuilder.ToImmutable(),
            castFieldsBuilder.ToImmutable(),
            paramsDict,
            action);
    }

    private static string? FindParamKey(
        IReadOnlyDictionary<string, object?> @params, string fieldName, bool caseInsensitive)
    {
        if (@params.ContainsKey(fieldName))
            return fieldName;

        if (!caseInsensitive)
            return null;

        foreach (var key in @params.Keys)
        {
            if (string.Equals(key, fieldName, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return null;
    }

    private static Dictionary<string, PropertyInfo> GetPropertyMap<T>(bool caseInsensitive)
    {
        return PropertyCache.GetOrAdd((typeof(T), caseInsensitive), static key =>
        {
            var comparer = key.Item2 ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var result = new Dictionary<string, PropertyInfo>(comparer);

            foreach (var prop in key.Item1.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanWrite)
                    result[prop.Name] = prop;
            }

            return result;
        });
    }
}
