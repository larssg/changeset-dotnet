using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Changeset.EntityFramework;

/// <summary>
/// Extension methods for applying changesets to an EF Core <see cref="DbContext"/> and for
/// database-backed uniqueness validation.
/// </summary>
public static class DbContextExtensions
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> WritablePropertyMapCache = new();

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
    /// Applies a valid changeset to the DbContext. For inserts, adds a new entity.
    /// For updates, attaches and marks only changed properties as modified.
    /// Returns the tracked entity.
    /// </summary>
    public static T ApplyTo<T>(this Changeset<T> changeset, DbContext context) where T : class, new()
    {
        if (!changeset.IsValid)
            throw new InvalidOperationException(
                $"Cannot apply an invalid changeset to DbContext. " +
                $"Changeset has {changeset.Errors.Length} error(s).");

        if (changeset.Action == ChangesetAction.Insert)
        {
            var entity = changeset.ApplyChanges();
            context.Set<T>().Add(entity);
            return entity;
        }

        // Update: attach the existing entity and mark only changed fields as modified
        var existing = changeset.Data
            ?? throw new InvalidOperationException("Update changeset must have Data set.");

        var entry = context.Entry(existing);
        if (entry.State == EntityState.Detached)
            context.Set<T>().Attach(existing);

        var propMap = GetWritablePropertyMap(typeof(T));

        foreach (var (field, value) in changeset.Changes)
        {
            if (propMap.TryGetValue(field, out var prop))
            {
                if (value is IChangesetValue association)
                {
                    prop.SetValue(existing, ApplyAssociation(association, context));
                    continue;
                }

                prop.SetValue(existing, value);
                entry.Property(field).IsModified = true;
            }
        }

        return existing;
    }

    private static object ApplyAssociation(IChangesetValue changeset, DbContext context)
    {
        var method = typeof(DbContextExtensions)
            .GetMethod(nameof(ApplyAssociationTyped),
                BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(changeset.ModelType);

        try
        {
            return method.Invoke(null, [changeset, context])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static TAssoc ApplyAssociationTyped<TAssoc>(
        IChangesetValue changeset, DbContext context) where TAssoc : class, new() =>
        ((Changeset<TAssoc>)changeset).ApplyTo(context);

    /// <summary>
    /// Applies a valid changeset and calls SaveChangesAsync.
    /// </summary>
    public static async Task<T> ApplyToAsync<T>(
        this Changeset<T> changeset, DbContext context,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var entity = changeset.ApplyTo(context);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// Applies a valid changeset, saves it, and maps database update failures into
    /// changeset errors. Returns <see cref="ChangesetResult{T}.Invalid"/> without touching
    /// the database when the changeset already has errors. When
    /// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> throws a
    /// <see cref="DbUpdateException"/> (for example, a unique-constraint violation),
    /// <paramref name="mapSaveError"/> decides how to translate it: return a
    /// <see cref="ChangesetError"/> to produce an invalid result, or null to rethrow.
    /// The failed entity remains tracked by the context.
    /// </summary>
    public static async Task<ChangesetResult<T>> TryApplyToAsync<T>(
        this Changeset<T> changeset, DbContext context,
        Func<DbUpdateException, ChangesetError?> mapSaveError,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(mapSaveError);

        if (!changeset.IsValid)
            return new ChangesetResult<T>.Invalid(changeset.Errors);

        var entity = changeset.ApplyTo(context);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new ChangesetResult<T>.Valid(entity);
        }
        catch (DbUpdateException exception)
        {
            var error = mapSaveError(exception);
            if (error is null)
                throw;

            return new ChangesetResult<T>.Invalid(changeset.Errors.Add(error));
        }
    }

    /// <summary>
    /// Validates that a changed field value does not already exist in the database.
    /// This is an advisory preflight check: a competing write can still occur between
    /// validation and persistence, so a database unique constraint remains authoritative.
    /// On updates the current row is excluded by primary key. Skipped when the field is
    /// absent from <c>Changes</c> or the compared value is null. An optional
    /// <paramref name="scope"/> predicate narrows the query, e.g. for tenant scoping or
    /// filtered (partial) unique indexes.
    /// </summary>
    public static Changeset<T> ValidateUnique<T>(
        this Changeset<T> changeset, string field, DbContext context,
        string? message = null, Expression<Func<T, bool>>? scope = null) where T : class =>
        changeset.ValidateUnique([field], context, message, scope);

    /// <summary>
    /// Expression form of <see cref="ValidateUnique{T}(Changeset{T}, string, DbContext, string?, Expression{Func{T, bool}}?)"/>.
    /// Accepts a single property (<c>u => u.Email</c>) or an anonymous type for composite
    /// uniqueness (<c>u => new { u.TenantId, u.Email }</c>).
    /// </summary>
    public static Changeset<T> ValidateUnique<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> fields,
        DbContext context, string? message = null,
        Expression<Func<T, bool>>? scope = null) where T : class =>
        changeset.ValidateUnique(
            ExpressionFieldExtractor.ExtractFieldNames(fields), context, message, scope);

    /// <summary>
    /// Validates that a combination of field values does not already exist in the database.
    /// The check runs only when at least one of <paramref name="fields"/> appears in
    /// <c>Changes</c>; values for the remaining fields are read from <c>Data</c>. A single
    /// error with code <c>uniqueness</c> is added to the first field on conflict.
    /// </summary>
    public static Changeset<T> ValidateUnique<T>(
        this Changeset<T> changeset, IReadOnlyList<string> fields, DbContext context,
        string? message = null, Expression<Func<T, bool>>? scope = null) where T : class
    {
        var query = BuildUniquenessQuery(changeset, fields, context, scope);
        if (query is null)
            return changeset;

        return query.Any()
            ? AddUniquenessError(changeset, fields, message)
            : changeset;
    }

    /// <summary>
    /// Async version of <see cref="ValidateUnique{T}(Changeset{T}, string, DbContext, string?, Expression{Func{T, bool}}?)"/>.
    /// </summary>
    public static async Task<Changeset<T>> ValidateUniqueAsync<T>(
        this Changeset<T> changeset, string field, DbContext context,
        string? message = null, Expression<Func<T, bool>>? scope = null,
        CancellationToken cancellationToken = default) where T : class =>
        await changeset.ValidateUniqueAsync(
            [field], context, message, scope, cancellationToken);

    /// <summary>
    /// Async expression form; accepts a single property or an anonymous type for
    /// composite uniqueness.
    /// </summary>
    public static Task<Changeset<T>> ValidateUniqueAsync<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> fields,
        DbContext context, string? message = null,
        Expression<Func<T, bool>>? scope = null,
        CancellationToken cancellationToken = default) where T : class =>
        changeset.ValidateUniqueAsync(
            ExpressionFieldExtractor.ExtractFieldNames(fields), context, message, scope,
            cancellationToken);

    /// <summary>
    /// Async version of <see cref="ValidateUnique{T}(Changeset{T}, IReadOnlyList{string}, DbContext, string?, Expression{Func{T, bool}}?)"/>.
    /// </summary>
    public static async Task<Changeset<T>> ValidateUniqueAsync<T>(
        this Changeset<T> changeset, IReadOnlyList<string> fields, DbContext context,
        string? message = null, Expression<Func<T, bool>>? scope = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var query = BuildUniquenessQuery(changeset, fields, context, scope);
        if (query is null)
            return changeset;

        return await query.AnyAsync(cancellationToken)
            ? AddUniquenessError(changeset, fields, message)
            : changeset;
    }

    private static readonly MethodInfo EfPropertyMethod =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!;

    private static IQueryable<T>? BuildUniquenessQuery<T>(
        Changeset<T> changeset, IReadOnlyList<string> fields, DbContext context,
        Expression<Func<T, bool>>? scope) where T : class
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count == 0)
            throw new ArgumentException(
                "At least one field is required.", nameof(fields));

        // Only query when at least one of the fields is actually being changed.
        if (!fields.Any(changeset.Changes.ContainsKey))
            return null;

        var entityType = context.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' is not part of the DbContext model.");

        var parameter = Expression.Parameter(typeof(T), "e");
        Expression? body = null;

        foreach (var field in fields)
        {
            var property = entityType.FindProperty(field)
                ?? throw new ArgumentException(
                    $"'{field}' is not a mapped property of '{typeof(T).Name}'.",
                    nameof(fields));

            var value = GetComparisonValue(changeset, field);
            if (value is null)
                return null; // A null component never conflicts under SQL semantics.

            body = Append(body, FieldEquals(parameter, property.ClrType, field, value));
        }

        var exclusion = BuildCurrentRowExclusion(changeset, entityType, parameter);
        if (exclusion is not null)
            body = Expression.AndAlso(body!, exclusion);

        var predicate = Expression.Lambda<Func<T, bool>>(body!, parameter);
        var query = context.Set<T>().AsNoTracking().Where(predicate);
        return scope is null ? query : query.Where(scope);
    }

    private static object? GetComparisonValue<T>(Changeset<T> changeset, string field)
        where T : class
    {
        if (changeset.Changes.TryGetValue(field, out var value))
            return value;

        if (changeset.Data is null)
            return null;

        var prop = typeof(T).GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(changeset.Data);
    }

    /// <summary>
    /// Builds a predicate excluding the row identified by <c>Data</c>'s primary key, so an
    /// update does not conflict with itself. Skipped when the key is shadow-state or unset.
    /// </summary>
    private static Expression? BuildCurrentRowExclusion<T>(
        Changeset<T> changeset, IEntityType entityType, ParameterExpression parameter)
        where T : class
    {
        if (changeset.Data is null)
            return null;

        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is null)
            return null;

        Expression? match = null;
        foreach (var keyProperty in primaryKey.Properties)
        {
            var keyValue = keyProperty.PropertyInfo?.GetValue(changeset.Data);
            if (keyValue is null)
                return null;

            match = Append(match,
                FieldEquals(parameter, keyProperty.ClrType, keyProperty.Name, keyValue));
        }

        return match is null ? null : Expression.Not(match);
    }

    private static Expression FieldEquals(
        ParameterExpression parameter, Type clrType, string field, object value)
    {
        if (!clrType.IsInstanceOfType(value))
            clrType = typeof(object);

        var propertyAccess = Expression.Call(
            EfPropertyMethod.MakeGenericMethod(clrType),
            parameter,
            Expression.Constant(field));

        return Expression.Equal(propertyAccess, Expression.Constant(value, clrType));
    }

    private static Expression Append(Expression? body, Expression next) =>
        body is null ? next : Expression.AndAlso(body, next);

    private static Changeset<T> AddUniquenessError<T>(
        Changeset<T> changeset, IReadOnlyList<string> fields, string? message) where T : class
    {
        message ??= "has already been taken";

        if (fields.Count == 1)
            return changeset.AddError(fields[0], message, "uniqueness");

        var metadata = ImmutableDictionary<string, object>.Empty
            .Add("fields", fields.ToImmutableArray());
        return changeset.AddError(fields[0], message, "uniqueness", metadata);
    }
}
