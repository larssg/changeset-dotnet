using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Changeset.EntityFramework;

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
    /// Validates that a field value is unique in the database.
    /// Excludes the current entity (if updating) from the uniqueness check.
    /// </summary>
    public static Changeset<T> ValidateUnique<T>(
        this Changeset<T> changeset, string field, DbContext context,
        string? message = null) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        var exists = context.Set<T>().AsNoTracking()
            .Any(e => EF.Property<object>(e, field) == value);

        if (exists && changeset.Data is not null)
        {
            var prop = typeof(T).GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            var currentValue = prop?.GetValue(changeset.Data);
            if (Equals(currentValue, value))
                return changeset; // Same entity, same value — not a conflict
        }

        if (exists)
            return changeset.AddError(field, message ?? "has already been taken", "uniqueness");

        return changeset;
    }

    public static Changeset<T> ValidateUnique<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        DbContext context, string? message = null) where T : class =>
        changeset.ValidateUnique(FieldName(field), context, message);

    /// <summary>
    /// Async version of ValidateUnique that uses EF Core's async query capabilities.
    /// Uses a compiled expression for efficient DB-side filtering.
    /// </summary>
    public static async Task<Changeset<T>> ValidateUniqueAsync<T>(
        this Changeset<T> changeset, string field, DbContext context,
        string? message = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!changeset.Changes.TryGetValue(field, out var value))
            return changeset;

        var prop = typeof(T).GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
            return changeset;

        var exists = await context.Set<T>().AsNoTracking()
            .AnyAsync(e => EF.Property<object>(e, field) == value, cancellationToken);

        if (exists && changeset.Data is not null)
        {
            // Check if it's the same entity
            var currentValue = prop.GetValue(changeset.Data);
            if (Equals(currentValue, value))
                return changeset; // Same entity, same value — not a conflict
        }

        if (exists)
            return changeset.AddError(field, message ?? "has already been taken", "uniqueness");

        return changeset;
    }

    public static Task<Changeset<T>> ValidateUniqueAsync<T, TValue>(
        this Changeset<T> changeset, Expression<Func<T, TValue>> field,
        DbContext context, string? message = null,
        CancellationToken cancellationToken = default) where T : class =>
        changeset.ValidateUniqueAsync(
            FieldName(field), context, message, cancellationToken);

    private static string FieldName<T, TValue>(Expression<Func<T, TValue>> field)
    {
        ArgumentNullException.ThrowIfNull(field);
        Expression body = field.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;
        if (body is MemberExpression member && member.Expression == field.Parameters[0])
            return member.Member.Name;

        throw new ArgumentException(
            "Expression must be a direct property access, e.g. u => u.Email",
            nameof(field));
    }
}
