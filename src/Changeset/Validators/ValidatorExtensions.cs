using System.Collections;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Changeset.Validators;

public static class ValidatorExtensions
{
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

    public static Changeset<T> ValidateRequired<T>(
        this Changeset<T> changeset,
        Expression<Func<T, object>> fields) where T : class
    {
        ArgumentNullException.ThrowIfNull(fields);
        return changeset.ValidateRequired(ExpressionFieldExtractor.ExtractFieldNames(fields));
    }

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

    public static Changeset<T> ValidateFormat<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        string pattern, string? message = null) where T : class =>
        changeset.ValidateFormat(FieldName(field), pattern, message);

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

    public static Changeset<T> ValidateInclusion<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        IReadOnlyList<object> values, string? message = null) where T : class =>
        changeset.ValidateInclusion(FieldName(field), values, message);

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

    public static Changeset<T> ValidateExclusion<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        IReadOnlyList<object> values, string? message = null) where T : class =>
        changeset.ValidateExclusion(FieldName(field), values, message);

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

    public static Changeset<T> ValidateConfirmation<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        string? message = null) where T : class =>
        changeset.ValidateConfirmation(FieldName(field), message);

    public static Changeset<T> ValidateChange<T>(
        this Changeset<T> changeset, string field,
        Func<Changeset<T>, object?, Changeset<T>> validator) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        return validator(changeset, value);
    }

    public static Changeset<T> ValidateChange<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        Func<Changeset<T>, object?, Changeset<T>> validator) where T : class =>
        changeset.ValidateChange(FieldName(field), validator);

    public static Changeset<T> Validate<T>(
        this Changeset<T> changeset,
        Func<Changeset<T>, Changeset<T>> validator) where T : class
    {
        return validator(changeset);
    }

    public static async Task<Changeset<T>> ValidateChangeAsync<T>(
        this Changeset<T> changeset, string field,
        Func<Changeset<T>, object?, Task<Changeset<T>>> validator) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        return await validator(changeset, value);
    }

    public static Task<Changeset<T>> ValidateChangeAsync<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        Func<Changeset<T>, object?, Task<Changeset<T>>> validator) where T : class =>
        changeset.ValidateChangeAsync(FieldName(field), validator);

    public static async Task<Changeset<T>> ValidateAsync<T>(
        this Task<Changeset<T>> changesetTask,
        Func<Changeset<T>, Changeset<T>> validator) where T : class
    {
        var changeset = await changesetTask;
        return validator(changeset);
    }

    public static async Task<Changeset<T>> ValidateChangeAsync<T>(
        this Task<Changeset<T>> changesetTask, string field,
        Func<Changeset<T>, object?, Task<Changeset<T>>> validator) where T : class
    {
        var changeset = await changesetTask;
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        return await validator(changeset, value);
    }

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
