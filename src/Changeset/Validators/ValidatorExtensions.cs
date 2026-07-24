using System.Collections;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Changeset.Validators;

/// <summary>
/// Validation extension methods for <see cref="Changeset{T}"/>. Validators inspect
/// the changeset's Changes and accumulate errors without mutating the original
/// changeset; each returns a new changeset instance. Except for
/// <see cref="ValidateRequired{T}(Changeset{T}, IReadOnlyList{string})"/>, validators
/// skip fields that have no change, so they only run on values that were actually cast.
/// </summary>
public static class ValidatorExtensions
{
    /// <summary>
    /// Validates that each of the given fields has a change that is not null and,
    /// for strings, not empty or whitespace. Adds a "can't be blank" error with
    /// validation kind "required" for each missing field.
    /// </summary>
    public static Changeset<T> ValidateRequired<T>(
        this Changeset<T> changeset, IReadOnlyList<string> fields) where T : class
    {
        ImmutableArray<ChangesetError>.Builder? errors = null;
        foreach (var field in fields)
        {
            if (!changeset.Changes.TryGetValue(field, out var value) ||
                value is null ||
                (value is string str && string.IsNullOrWhiteSpace(str)))
            {
                errors ??= changeset.Errors.ToBuilder();
                errors.Add(ChangesetError.For(field, "can't be blank", "required"));
            }
        }

        return errors is null
            ? changeset
            : changeset with { Errors = errors.ToImmutable() };
    }

    /// <summary>
    /// Validates that the fields selected with a compile-time safe expression are present.
    /// Select a single property (<c>c => c.Name</c>) or several via an anonymous type
    /// (<c>c => new { c.Name, c.Email }</c>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> is null.</exception>
    public static Changeset<T> ValidateRequired<T>(
        this Changeset<T> changeset,
        Expression<Func<T, object>> fields) where T : class
    {
        ArgumentNullException.ThrowIfNull(fields);
        return changeset.ValidateRequired(ExpressionFieldExtractor.ExtractFieldNames(fields));
    }

    /// <summary>
    /// Validates that a changed string field matches a regular expression pattern.
    /// Skipped when the field has no change or the change is not a string.
    /// Adds a "has invalid format" error with validation kind "format" on mismatch.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="pattern">The regular expression pattern the value must match.</param>
    /// <param name="message">Custom error message; defaults to "has invalid format".</param>
    public static Changeset<T> ValidateFormat<T>(
        this Changeset<T> changeset, string field, string pattern,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value) || value is not string str)
            return changeset;

        if (!Regex.IsMatch(str, pattern))
            return changeset.AddError(field, message ?? "has invalid format", "format",
                ImmutableDictionary.CreateRange(new[]
                {
                    KeyValuePair.Create<string, object>("pattern", pattern)
                }));

        return changeset;
    }

    /// <summary>
    /// Validates that a changed string field matches a precompiled <see cref="Regex"/>.
    /// Skipped when the field has no change or the change is not a string.
    /// Adds a "has invalid format" error with validation kind "format" on mismatch.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="regex">The regular expression the value must match.</param>
    /// <param name="message">Custom error message; defaults to "has invalid format".</param>
    public static Changeset<T> ValidateFormat<T>(
        this Changeset<T> changeset, string field, Regex regex,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value) || value is not string str)
            return changeset;

        if (!regex.IsMatch(str))
            return changeset.AddError(field, message ?? "has invalid format", "format",
                ImmutableDictionary.CreateRange(new[]
                {
                    KeyValuePair.Create<string, object>("pattern", regex.ToString())
                }));

        return changeset;
    }

    /// <summary>
    /// Validates that a changed string field, selected with a compile-time safe
    /// expression, matches a regular expression pattern.
    /// </summary>
    public static Changeset<T> ValidateFormat<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        string pattern, string? message = null) where T : class =>
        changeset.ValidateFormat(FieldName(field), pattern, message);

    /// <summary>
    /// Validates that a changed string field, selected with a compile-time safe
    /// expression, matches a precompiled <see cref="Regex"/>.
    /// </summary>
    public static Changeset<T> ValidateFormat<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        Regex regex, string? message = null) where T : class =>
        changeset.ValidateFormat(FieldName(field), regex, message);

    /// <summary>
    /// Validates the length of a changed field. Works on strings (character count),
    /// collections (element count), and other enumerables. Skipped when the field
    /// has no change or the change has no measurable length. Adds an error with
    /// validation kind "length" when a constraint is violated.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="min">The minimum allowed length, inclusive.</param>
    /// <param name="max">The maximum allowed length, inclusive.</param>
    /// <param name="is">The exact length required; checked before min and max.</param>
    /// <param name="message">Custom error message; defaults to a constraint-specific message.</param>
    public static Changeset<T> ValidateLength<T>(
        this Changeset<T> changeset, string field,
        int? min = null, int? max = null, int? @is = null,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        int length;
        if (value is string str)
            length = str.Length;
        else if (value is ICollection col)
            length = col.Count;
        else if (value is IEnumerable enumerable)
            length = CountUntil(enumerable, EnumerationLimit(min, max, @is));
        else
            return changeset;

        if (@is.HasValue && length != @is.Value)
            return changeset.AddError(field,
                message ?? $"should be {@is.Value} character(s)",
                "length",
                BuildLengthMetadata(min, max, @is));

        if (min.HasValue && length < min.Value)
            return changeset.AddError(field,
                message ?? $"should be at least {min.Value} character(s)",
                "length",
                BuildLengthMetadata(min, max, @is));

        if (max.HasValue && length > max.Value)
            return changeset.AddError(field,
                message ?? $"should be at most {max.Value} character(s)",
                "length",
                BuildLengthMetadata(min, max, @is));

        return changeset;
    }

    /// <summary>
    /// Validates the length of a changed field selected with a compile-time safe expression.
    /// </summary>
    /// <example>
    /// <code>changeset.ValidateLength(c => c.Name, min: 2, max: 100)</code>
    /// </example>
    public static Changeset<T> ValidateLength<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        int? min = null, int? max = null, int? @is = null,
        string? message = null) where T : class =>
        changeset.ValidateLength(FieldName(field), min, max, @is, message);

    /// <summary>
    /// Validates a changed numeric (or otherwise comparable) field against comparison
    /// constraints. Skipped when the field has no change or the change is not
    /// <see cref="IComparable"/>. Checks run in the order greaterThan,
    /// greaterThanOrEqual, lessThan, lessThanOrEqual, equalTo and stop at the first
    /// violation, adding an error with validation kind "number".
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="greaterThan">The value the change must be strictly greater than.</param>
    /// <param name="greaterThanOrEqual">The value the change must be greater than or equal to.</param>
    /// <param name="lessThan">The value the change must be strictly less than.</param>
    /// <param name="lessThanOrEqual">The value the change must be less than or equal to.</param>
    /// <param name="equalTo">The value the change must compare equal to.</param>
    /// <param name="message">Custom error message; defaults to a constraint-specific message.</param>
    public static Changeset<T> ValidateNumber<T>(
        this Changeset<T> changeset, string field,
        IComparable? greaterThan = null,
        IComparable? greaterThanOrEqual = null,
        IComparable? lessThan = null,
        IComparable? lessThanOrEqual = null,
        IComparable? equalTo = null,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value) || value is not IComparable comparable)
            return changeset;

        if (greaterThan is not null && comparable.CompareTo(greaterThan) <= 0)
            return changeset.AddError(field,
                message ?? $"must be greater than {greaterThan}", "number");

        if (greaterThanOrEqual is not null && comparable.CompareTo(greaterThanOrEqual) < 0)
            return changeset.AddError(field,
                message ?? $"must be greater than or equal to {greaterThanOrEqual}", "number");

        if (lessThan is not null && comparable.CompareTo(lessThan) >= 0)
            return changeset.AddError(field,
                message ?? $"must be less than {lessThan}", "number");

        if (lessThanOrEqual is not null && comparable.CompareTo(lessThanOrEqual) > 0)
            return changeset.AddError(field,
                message ?? $"must be less than or equal to {lessThanOrEqual}", "number");

        if (equalTo is not null && comparable.CompareTo(equalTo) != 0)
            return changeset.AddError(field,
                message ?? $"must be equal to {equalTo}", "number");

        return changeset;
    }

    /// <summary>
    /// Validates a changed comparable field, selected with a compile-time safe
    /// expression, against comparison constraints.
    /// </summary>
    public static Changeset<T> ValidateNumber<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        IComparable? greaterThan = null,
        IComparable? greaterThanOrEqual = null,
        IComparable? lessThan = null,
        IComparable? lessThanOrEqual = null,
        IComparable? equalTo = null,
        string? message = null) where T : class =>
        changeset.ValidateNumber(FieldName(field), greaterThan, greaterThanOrEqual,
            lessThan, lessThanOrEqual, equalTo, message);

    /// <summary>
    /// Validates that a changed field's value is one of the allowed values.
    /// Skipped when the field has no change. Adds an "is invalid" error with
    /// validation kind "inclusion" when the value is not in the list.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="values">The set of allowed values.</param>
    /// <param name="message">Custom error message; defaults to "is invalid".</param>
    public static Changeset<T> ValidateInclusion<T>(
        this Changeset<T> changeset, string field, IReadOnlyList<object> values,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        if (!values.Contains(value!))
            return changeset.AddError(field, message ?? "is invalid", "inclusion");

        return changeset;
    }

    /// <summary>
    /// Validates that a changed field, selected with a compile-time safe expression,
    /// has a value contained in the typed collection of allowed values.
    /// </summary>
    public static Changeset<T> ValidateInclusion<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        IReadOnlyCollection<TValue> values, string? message = null) where T : class
    {
        var fieldName = FieldName(field);
        if (!changeset.Changes.TryGetValue(fieldName, out var value))
            return changeset;

        return CollectionContains(values, value)
            ? changeset
            : changeset.AddError(fieldName, message ?? "is invalid", "inclusion");
    }

    /// <summary>
    /// Validates that a changed field's value is not one of the reserved values.
    /// Skipped when the field has no change. Adds an "is reserved" error with
    /// validation kind "exclusion" when the value is in the list.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="values">The set of disallowed values.</param>
    /// <param name="message">Custom error message; defaults to "is reserved".</param>
    public static Changeset<T> ValidateExclusion<T>(
        this Changeset<T> changeset, string field, IReadOnlyList<object> values,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        if (values.Contains(value!))
            return changeset.AddError(field, message ?? "is reserved", "exclusion");

        return changeset;
    }

    /// <summary>
    /// Validates that a changed field, selected with a compile-time safe expression,
    /// has a value not contained in the typed collection of reserved values.
    /// </summary>
    public static Changeset<T> ValidateExclusion<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        IReadOnlyCollection<TValue> values, string? message = null) where T : class
    {
        var fieldName = FieldName(field);
        if (!changeset.Changes.TryGetValue(fieldName, out var value))
            return changeset;

        return CollectionContains(values, value)
            ? changeset.AddError(fieldName, message ?? "is reserved", "exclusion")
            : changeset;
    }

    /// <summary>
    /// Validates that a changed field matches its confirmation parameter — the raw
    /// param named <c>{field}_confirmation</c> (e.g. <c>password_confirmation</c>).
    /// Skipped when the field has no change. On mismatch, adds a "does not match"
    /// error with validation kind "confirmation" on the confirmation field.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="message">Custom error message; defaults to "does not match".</param>
    public static Changeset<T> ValidateConfirmation<T>(
        this Changeset<T> changeset, string field,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        var confirmationField = $"{field}_confirmation";
        changeset.Params.TryGetValue(confirmationField, out var confirmationValue);

        if (!Equals(value, confirmationValue))
            return changeset.AddError(confirmationField,
                message ?? "does not match", "confirmation");

        return changeset;
    }

    /// <summary>
    /// Validates that a changed field, selected with a compile-time safe expression,
    /// matches its <c>{field}_confirmation</c> parameter.
    /// </summary>
    public static Changeset<T> ValidateConfirmation<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        string? message = null) where T : class =>
        changeset.ValidateConfirmation(FieldName(field), message);

    /// <summary>
    /// Runs a custom validator against a single changed field. Skipped when the field
    /// has no change. The validator receives the changeset and the changed value, and
    /// returns the changeset with any errors added.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="validator">Function receiving the changeset and the changed value.</param>
    public static Changeset<T> ValidateChange<T>(
        this Changeset<T> changeset, string field,
        Func<Changeset<T>, object?, Changeset<T>> validator) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        return validator(changeset, value);
    }

    /// <summary>
    /// Runs a custom validator against a single changed field selected with a
    /// compile-time safe expression.
    /// </summary>
    public static Changeset<T> ValidateChange<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        Func<Changeset<T>, object?, Changeset<T>> validator) where T : class =>
        changeset.ValidateChange(FieldName(field), validator);

    /// <summary>
    /// Runs a custom validator against the whole changeset. Unlike
    /// <see cref="ValidateChange{T}(Changeset{T}, string, Func{Changeset{T}, object?, Changeset{T}})"/>,
    /// it always runs, making it suitable for cross-field rules.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="validator">Function receiving the changeset and returning it with any errors added.</param>
    public static Changeset<T> Validate<T>(
        this Changeset<T> changeset,
        Func<Changeset<T>, Changeset<T>> validator) where T : class
    {
        return validator(changeset);
    }

    /// <summary>
    /// Runs an asynchronous custom validator against a single changed field — for
    /// example a database uniqueness check. Skipped when the field has no change.
    /// </summary>
    /// <param name="changeset">The changeset to validate.</param>
    /// <param name="field">The name of the field to validate.</param>
    /// <param name="validator">Async function receiving the changeset and the changed value.</param>
    public static async Task<Changeset<T>> ValidateChangeAsync<T>(
        this Changeset<T> changeset, string field,
        Func<Changeset<T>, object?, Task<Changeset<T>>> validator) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        return await validator(changeset, value);
    }

    /// <summary>
    /// Runs an asynchronous custom validator against a single changed field selected
    /// with a compile-time safe expression.
    /// </summary>
    public static Task<Changeset<T>> ValidateChangeAsync<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        Func<Changeset<T>, object?, Task<Changeset<T>>> validator) where T : class =>
        changeset.ValidateChangeAsync(FieldName(field), validator);

    /// <summary>
    /// Awaits a pending changeset and runs a synchronous validator on the result,
    /// allowing further validators to be chained after an async step.
    /// </summary>
    public static async Task<Changeset<T>> ValidateAsync<T>(
        this Task<Changeset<T>> changesetTask,
        Func<Changeset<T>, Changeset<T>> validator) where T : class
    {
        var changeset = await changesetTask;
        return validator(changeset);
    }

    /// <summary>
    /// Awaits a pending changeset and runs an asynchronous custom validator against
    /// a single changed field. Skipped when the field has no change.
    /// </summary>
    public static async Task<Changeset<T>> ValidateChangeAsync<T>(
        this Task<Changeset<T>> changesetTask, string field,
        Func<Changeset<T>, object?, Task<Changeset<T>>> validator) where T : class
    {
        var changeset = await changesetTask;
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        return await validator(changeset, value);
    }

    /// <summary>
    /// Awaits a pending changeset and runs an asynchronous custom validator against
    /// a single changed field selected with a compile-time safe expression.
    /// </summary>
    public static async Task<Changeset<T>> ValidateChangeAsync<T, TValue>(
        this Task<Changeset<T>> changesetTask, Expression<Func<T, TValue>> field,
        Func<Changeset<T>, object?, Task<Changeset<T>>> validator) where T : class
    {
        var changeset = await changesetTask;
        return await changeset.ValidateChangeAsync(field, validator);
    }

    private static string FieldName<T, TValue>(Expression<Func<T, TValue>> field) =>
        ExpressionFieldExtractor.ExtractFieldName(field);

    private static int? EnumerationLimit(int? min, int? max, int? @is)
    {
        if (@is is < int.MaxValue)
            return Math.Max(0, @is.Value + 1);

        if (max is < int.MaxValue)
            return Math.Max(Math.Max(0, max.Value + 1), min.GetValueOrDefault());

        return null;
    }

    private static int CountUntil(IEnumerable enumerable, int? limit)
    {
        var count = 0;
        if (limit == 0)
            return count;

        foreach (var _ in enumerable)
        {
            count++;
            if (count == limit)
                break;
        }

        return count;
    }

    private static bool CollectionContains<TValue>(
        IReadOnlyCollection<TValue> values, object? value)
    {
        if (value is TValue typedValue)
        {
            if (values is IReadOnlySet<TValue> set)
                return set.Contains(typedValue);

            var comparer = EqualityComparer<TValue>.Default;
            foreach (var candidate in values)
            {
                if (comparer.Equals(candidate, typedValue))
                    return true;
            }

            return false;
        }

        if (value is not null)
            return false;

        foreach (var candidate in values)
        {
            if (candidate is null)
                return true;
        }

        return false;
    }

    private static ImmutableDictionary<string, object> BuildLengthMetadata(
        int? min, int? max, int? @is)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, object>();
        if (min.HasValue) builder["min"] = min.Value;
        if (max.HasValue) builder["max"] = max.Value;
        if (@is.HasValue) builder["is"] = @is.Value;
        return builder.ToImmutable();
    }
}
