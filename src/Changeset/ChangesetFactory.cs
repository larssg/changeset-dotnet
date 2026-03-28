using System.Linq.Expressions;
using Changeset.Casting;

namespace Changeset;

public static class Changeset
{
    public static Changeset<T> Cast<T>(
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions? options = null) where T : class
    {
        return Caster.Cast<T>(null, @params, permitted, options ?? CastOptions.Default);
    }

    public static Changeset<T> Cast<T>(
        T data,
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions? options = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(data);
        return Caster.Cast(data, @params, permitted, options ?? CastOptions.Default);
    }

    /// <summary>
    /// Cast with compile-time safe field selection using an expression.
    /// <code>Changeset.Cast&lt;User&gt;(params, u => new { u.Name, u.Email })</code>
    /// </summary>
    public static Changeset<T> Cast<T>(
        IReadOnlyDictionary<string, object?> @params,
        Expression<Func<T, object>> fields,
        CastOptions? options = null) where T : class
    {
        var permitted = ExpressionFieldExtractor.ExtractFieldNames(fields);
        return Caster.Cast<T>(null, @params, permitted, options ?? CastOptions.Default);
    }

    /// <summary>
    /// Cast with compile-time safe field selection using an expression (update variant).
    /// <code>Changeset.Cast(existingUser, params, u => new { u.Name, u.Email })</code>
    /// </summary>
    public static Changeset<T> Cast<T>(
        T data,
        IReadOnlyDictionary<string, object?> @params,
        Expression<Func<T, object>> fields,
        CastOptions? options = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(data);
        var permitted = ExpressionFieldExtractor.ExtractFieldNames(fields);
        return Caster.Cast(data, @params, permitted, options ?? CastOptions.Default);
    }
}
