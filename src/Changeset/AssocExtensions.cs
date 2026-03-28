using System.Collections.Immutable;
using Changeset.Casting;

namespace Changeset;

public static class AssocExtensions
{
    /// <summary>
    /// Casts and embeds a nested association (e.g. User has Address).
    /// Extracts the nested params from the parent params under the given field name,
    /// creates a child Changeset&lt;TAssoc&gt;, and stores it in the parent's Changes.
    /// </summary>
    public static Changeset<T> CastAssoc<T, TAssoc>(
        this Changeset<T> changeset,
        string field,
        IReadOnlyList<string> permitted,
        CastOptions? options = null) where T : class where TAssoc : class
    {
        // Look for nested params under the field name
        if (!changeset.Params.TryGetValue(field, out var rawAssocParams))
            return changeset;

        var assocParams = CoerceToParamsDictionary(rawAssocParams);
        if (assocParams is null)
            return changeset.AddError(field, "is invalid", "invalid_assoc");

        // Get existing associated data if updating
        TAssoc? existingAssoc = null;
        if (changeset.Data is not null)
        {
            var prop = typeof(T).GetProperty(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            existingAssoc = prop?.GetValue(changeset.Data) as TAssoc;
        }

        var opts = options ?? CastOptions.Default;
        var childCs = existingAssoc is not null
            ? Caster.Cast(existingAssoc, assocParams, permitted, opts)
            : Caster.Cast<TAssoc>(null, assocParams, permitted, opts);

        // Store the child changeset in Changes
        var result = changeset with
        {
            Changes = changeset.Changes.SetItem(field, childCs),
            CastFields = changeset.CastFields.Add(field)
        };

        // Propagate child errors with dot-notation field paths
        if (!childCs.IsValid)
        {
            var errors = result.Errors;
            foreach (var error in childCs.Errors)
            {
                var prefixedField = string.IsNullOrEmpty(error.Field)
                    ? field
                    : $"{field}.{error.Field}";
                errors = errors.Add(ChangesetError.For(prefixedField, error.Message, error.Code));
            }
            result = result with { Errors = errors };
        }

        return result;
    }

    /// <summary>
    /// Validates a nested association changeset using the provided validation function.
    /// The function receives the child changeset and returns a validated version.
    /// Child errors are propagated to the parent with dot-notation field paths.
    /// </summary>
    public static Changeset<T> ValidateAssoc<T, TAssoc>(
        this Changeset<T> changeset,
        string field,
        Func<Changeset<TAssoc>, Changeset<TAssoc>> validator)
        where T : class where TAssoc : class
    {
        if (!changeset.Changes.TryGetValue(field, out var rawChild))
            return changeset;

        if (rawChild is not Changeset<TAssoc> childCs)
            return changeset;

        var validatedChild = validator(childCs);

        // If no new errors were added, return unchanged
        if (ReferenceEquals(validatedChild, childCs))
            return changeset;

        // Update the stored child changeset
        var result = changeset with
        {
            Changes = changeset.Changes.SetItem(field, validatedChild)
        };

        // Find new errors (in validated but not in original)
        var existingErrorCount = childCs.Errors.Length;
        if (validatedChild.Errors.Length > existingErrorCount)
        {
            var errors = result.Errors;
            for (var i = existingErrorCount; i < validatedChild.Errors.Length; i++)
            {
                var error = validatedChild.Errors[i];
                var prefixedField = string.IsNullOrEmpty(error.Field)
                    ? field
                    : $"{field}.{error.Field}";
                errors = errors.Add(ChangesetError.For(prefixedField, error.Message, error.Code));
            }
            result = result with { Errors = errors };
        }

        return result;
    }

    /// <summary>
    /// Retrieves the nested child changeset for an association field.
    /// </summary>
    public static Changeset<TAssoc>? GetAssoc<T, TAssoc>(
        this Changeset<T> changeset, string field)
        where T : class where TAssoc : class
    {
        return changeset.Changes.TryGetValue(field, out var value)
            ? value as Changeset<TAssoc>
            : null;
    }

    private static IReadOnlyDictionary<string, object?>? CoerceToParamsDictionary(object? raw)
    {
        if (raw is IReadOnlyDictionary<string, object?> roDict)
            return roDict;
        if (raw is IDictionary<string, object?> dict)
            return new Dictionary<string, object?>(dict);
        if (raw is System.Text.Json.JsonElement jsonEl &&
            jsonEl.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var result = new Dictionary<string, object?>();
            foreach (var prop in jsonEl.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                    ? null
                    : (object)prop.Value;
            }
            return result;
        }

        return null;
    }
}
