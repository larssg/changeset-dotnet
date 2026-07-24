using System.Collections.Immutable;
using System.Linq.Expressions;
using Changeset.Casting;

namespace Changeset;

/// <summary>
/// An immutable description of proposed changes to a model of type <typeparamref name="T"/>,
/// produced by casting untrusted params and refined through validation before being applied.
/// </summary>
/// <typeparam name="T">The model type the changeset targets.</typeparam>
public sealed record Changeset<T> : IChangesetValue where T : class
{
    /// <summary>
    /// The existing model instance being updated, or <c>null</c> when creating a new one.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// The coerced field values that differ from <see cref="Data"/>, keyed by property name.
    /// </summary>
    public ImmutableDictionary<string, object?> Changes { get; init; }

    /// <summary>
    /// All casting and validation errors accumulated so far.
    /// </summary>
    public ImmutableArray<ChangesetError> Errors { get; init; }

    /// <summary>
    /// The names of fields that were present in the params and successfully cast,
    /// including unchanged values skipped from <see cref="Changes"/>.
    /// </summary>
    public ImmutableHashSet<string> CastFields { get; init; }

    /// <summary>
    /// The original raw params the changeset was cast from.
    /// </summary>
    public ImmutableDictionary<string, object?> Params { get; init; }

    /// <summary>
    /// Whether this changeset inserts a new model or updates an existing one.
    /// </summary>
    public ChangesetAction Action { get; init; }

    /// <summary>
    /// <c>true</c> when the changeset has no errors.
    /// </summary>
    public bool IsValid => Errors.IsEmpty;

    Type IChangesetValue.ModelType => typeof(T);
    object? IChangesetValue.UntypedData => Data;
    ImmutableDictionary<string, object?> IChangesetValue.UntypedChanges => Changes;

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

    /// <summary>
    /// Returns a new changeset with an error added for the given field.
    /// </summary>
    /// <param name="field">The field the error applies to.</param>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="code">A machine-readable error code.</param>
    public Changeset<T> AddError(string field, string message, string code) =>
        this with { Errors = Errors.Add(ChangesetError.For(field, message, code)) };

    /// <summary>
    /// Returns a new changeset with an error (including metadata) added for the given field.
    /// </summary>
    /// <param name="field">The field the error applies to.</param>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="code">A machine-readable error code.</param>
    /// <param name="metadata">Additional structured data describing the error.</param>
    public Changeset<T> AddError(string field, string message, string code,
        ImmutableDictionary<string, object> metadata) =>
        this with { Errors = Errors.Add(ChangesetError.For(field, message, code, metadata)) };

    /// <summary>
    /// Returns a new changeset with an error that applies to the changeset as a whole
    /// rather than a specific field.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="code">A machine-readable error code.</param>
    public Changeset<T> AddBaseError(string message, string code) =>
        this with { Errors = Errors.Add(ChangesetError.Base(message, code)) };

    /// <summary>
    /// Returns all errors recorded for the given field.
    /// </summary>
    public ImmutableArray<ChangesetError> ErrorsOn(string field) =>
        Errors.Where(e => e.Field == field).ToImmutableArray();

    /// <summary>
    /// Returns <c>true</c> if at least one error is recorded for the given field.
    /// </summary>
    public bool HasErrorOn(string field) =>
        Errors.Any(e => e.Field == field);

    /// <summary>
    /// Errors that apply to the changeset as a whole (added via <see cref="AddBaseError"/>).
    /// </summary>
    public ImmutableArray<ChangesetError> BaseErrors =>
        Errors.Where(e => e.Field == "").ToImmutableArray();

    /// <summary>
    /// All errors grouped by field name; base errors are keyed by the empty string.
    /// </summary>
    public ImmutableDictionary<string, ImmutableArray<ChangesetError>> ErrorMap =>
        Errors
            .GroupBy(e => e.Field)
            .ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());

    /// <summary>
    /// Gets the pending change for a field, or <c>default</c> if the field has no change
    /// or the change is not of type <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The expected type of the change value.</typeparam>
    /// <param name="field">The property name to look up in <see cref="Changes"/>.</param>
    public TValue? GetChange<TValue>(string field) =>
        Changes.TryGetValue(field, out var value) && value is TValue typed
            ? typed
            : default;

    internal Changeset<T> WithChange(string field, object? value) =>
        this with { Changes = Changes.SetItem(field, value) };

    /// <summary>
    /// Casts untrusted params into a changeset for creating a new <typeparamref name="T"/>,
    /// coercing permitted fields to the target property types and recording errors for
    /// values that cannot be coerced.
    /// </summary>
    /// <param name="params">The raw input values, keyed by field name.</param>
    /// <param name="permitted">The field names allowed to be cast; all others are ignored.</param>
    /// <param name="options">Casting options; defaults to <see cref="CastOptions.Default"/>.</param>
    public static Changeset<T> Cast(
        IReadOnlyDictionary<string, object?> @params,
        IReadOnlyList<string> permitted,
        CastOptions? options = null)
    {
        return Caster.Cast<T>(null, @params, permitted, options ?? CastOptions.Default);
    }

    /// <summary>
    /// Casts untrusted params into a changeset for updating an existing
    /// <typeparamref name="T"/>; values equal to the current data are not
    /// recorded as changes.
    /// </summary>
    /// <param name="data">The existing model instance being updated.</param>
    /// <param name="params">The raw input values, keyed by field name.</param>
    /// <param name="permitted">The field names allowed to be cast; all others are ignored.</param>
    /// <param name="options">Casting options; defaults to <see cref="CastOptions.Default"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <c>null</c>.</exception>
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
