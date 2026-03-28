using System.Collections.Immutable;
using System.Linq.Expressions;
using Changeset.Casting;

namespace Changeset;

public sealed record Changeset<T> where T : class
{
    public T? Data { get; init; }
    public ImmutableDictionary<string, object?> Changes { get; init; }
    public ImmutableArray<ChangesetError> Errors { get; init; }
    public ImmutableHashSet<string> CastFields { get; init; }
    public ImmutableDictionary<string, object?> Params { get; init; }
    public ChangesetAction Action { get; init; }

    public bool IsValid => Errors.IsEmpty;

    internal Changeset(
        T? data,
        ImmutableDictionary<string, object?> changes,
        ImmutableArray<ChangesetError> errors,
        ImmutableHashSet<string> castFields,
        ImmutableDictionary<string, object?> @params,
        ChangesetAction action)
    {
        Data = data;
        Changes = changes;
        Errors = errors;
        CastFields = castFields;
        Params = @params;
        Action = action;
    }

    public Changeset<T> AddError(string field, string message, string code) =>
        this with { Errors = Errors.Add(ChangesetError.For(field, message, code)) };

    public Changeset<T> AddError(string field, string message, string code,
        ImmutableDictionary<string, object> metadata) =>
        this with { Errors = Errors.Add(ChangesetError.For(field, message, code, metadata)) };

    public Changeset<T> AddBaseError(string message, string code) =>
        this with { Errors = Errors.Add(ChangesetError.Base(message, code)) };

    public ImmutableArray<ChangesetError> ErrorsOn(string field) =>
        Errors.Where(e => e.Field == field).ToImmutableArray();

    public bool HasErrorOn(string field) =>
        Errors.Any(e => e.Field == field);

    public ImmutableArray<ChangesetError> BaseErrors =>
        Errors.Where(e => e.Field == "").ToImmutableArray();

    public ImmutableDictionary<string, ImmutableArray<ChangesetError>> ErrorMap =>
        Errors
            .GroupBy(e => e.Field)
            .ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());

    public TValue? GetChange<TValue>(string field) =>
        Changes.TryGetValue(field, out var value) && value is TValue typed
            ? typed
            : default;

    internal Changeset<T> WithChange(string field, object? value) =>
        this with { Changes = Changes.SetItem(field, value) };

    public static Changeset<T> Cast(
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions? options = null)
    {
        return Caster.Cast<T>(null, @params, permitted, options ?? CastOptions.Default);
    }

    public static Changeset<T> Cast(
        T data,
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Caster.Cast(data, @params, permitted, options ?? CastOptions.Default);
    }

    /// <summary>
    /// Cast with compile-time safe field selection using an expression.
    /// <code>Changeset&lt;User&gt;.Cast(params, u => new { u.Name, u.Email })</code>
    /// </summary>
    public static Changeset<T> Cast(
        IReadOnlyDictionary<string, object?> @params,
        Expression<Func<T, object>> fields,
        CastOptions? options = null)
    {
        var permitted = ExpressionFieldExtractor.ExtractFieldNames(fields);
        return Caster.Cast<T>(null, @params, permitted, options ?? CastOptions.Default);
    }

    /// <summary>
    /// Cast with compile-time safe field selection using an expression (update variant).
    /// <code>Changeset&lt;User&gt;.Cast(existingUser, params, u => new { u.Name, u.Email })</code>
    /// </summary>
    public static Changeset<T> Cast(
        T data,
        IReadOnlyDictionary<string, object?> @params,
        Expression<Func<T, object>> fields,
        CastOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var permitted = ExpressionFieldExtractor.ExtractFieldNames(fields);
        return Caster.Cast(data, @params, permitted, options ?? CastOptions.Default);
    }
}
