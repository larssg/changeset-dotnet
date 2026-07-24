# API Reference

This is a compact reference for the public Changeset.NET API. The conceptual
guides explain behavior and tradeoffs in more depth.

## Core types

### `Changeset<T>`

`T` must be a class.

| Member | Type | Description |
|---|---|---|
| `Data` | `T?` | Existing model for updates |
| `Changes` | `ImmutableDictionary<string, object?>` | Successfully cast changed values |
| `Errors` | `ImmutableArray<ChangesetError>` | Accumulated errors |
| `CastFields` | `ImmutableHashSet<string>` | Property names represented by cast changes |
| `Params` | `ImmutableDictionary<string, object?>` | Original input snapshot |
| `Action` | `ChangesetAction` | `Insert` or `Update` |
| `IsValid` | `bool` | Whether `Errors` is empty |
| `ErrorMap` | `ImmutableDictionary<string, ImmutableArray<ChangesetError>>` | Errors grouped by field |
| `BaseErrors` | `ImmutableArray<ChangesetError>` | Errors with an empty field |

Methods:

```csharp
Changeset<T> AddError(
    string field,
    string message,
    string code);

Changeset<T> AddError(
    string field,
    string message,
    string code,
    ImmutableDictionary<string, object> metadata);

Changeset<T> AddBaseError(string message, string code);
ImmutableArray<ChangesetError> ErrorsOn(string field);
bool HasErrorOn(string field);
TValue? GetChange<TValue>(string field);
```

### `ChangesetError`

```csharp
public sealed record ChangesetError(
    string Field,
    string Message,
    string Code,
    ImmutableDictionary<string, object>? Metadata = null);
```

Static factories:

```csharp
ChangesetError.For(field, message, code);
ChangesetError.For(field, message, code, metadata);
ChangesetError.Base(message, code);
```

### `ChangesetAction`

```csharp
ChangesetAction.Insert
ChangesetAction.Update
```

### `ChangesetResult<T>`

```csharp
ChangesetResult<T>.Valid(T Value)
ChangesetResult<T>.Invalid(ImmutableArray<ChangesetError> Errors)
```

## Casting

All `Cast` overloads accept optional `CastOptions`.

```csharp
Changeset<T>.Cast(
    IReadOnlyDictionary<string, object?> parameters,
    IReadOnlyList<string> permitted,
    CastOptions? options = null);

Changeset<T>.Cast(
    T data,
    IReadOnlyDictionary<string, object?> parameters,
    IReadOnlyList<string> permitted,
    CastOptions? options = null);

Changeset<T>.Cast(
    IReadOnlyDictionary<string, object?> parameters,
    Expression<Func<T, object>> fields,
    CastOptions? options = null);

Changeset<T>.Cast(
    T data,
    IReadOnlyDictionary<string, object?> parameters,
    Expression<Func<T, object>> fields,
    CastOptions? options = null);
```

`CastOptions`:

```csharp
public sealed record CastOptions
{
    public bool StrictCasting { get; init; }          // false
    public IFormatProvider? FormatProvider { get; init; }
    public bool TrimStrings { get; init; }            // true
    public bool CaseInsensitiveFields { get; init; }  // true
    public static CastOptions Default { get; }
}
```

See [Casting](casting.md) for supported conversions.

## Validators

Validator extension methods live in `Changeset.Validators`.

### Required

```csharp
ValidateRequired(IReadOnlyList<string> fields);
ValidateRequired(Expression<Func<T, object>> fields);
```

### Format

```csharp
ValidateFormat(
    string field,
    string pattern,
    string? message = null);

ValidateFormat(
    string field,
    Regex regex,
    string? message = null);

ValidateFormat<TValue>(
    Expression<Func<T, TValue>> field,
    string pattern,
    string? message = null);

ValidateFormat<TValue>(
    Expression<Func<T, TValue>> field,
    Regex regex,
    string? message = null);
```

### Length

```csharp
ValidateLength(
    string field,
    int? min = null,
    int? max = null,
    int? is = null,
    string? message = null);

ValidateLength<TValue>(
    Expression<Func<T, TValue>> field,
    int? min = null,
    int? max = null,
    int? is = null,
    string? message = null);
```

### Number

```csharp
ValidateNumber(
    string field,
    IComparable? greaterThan = null,
    IComparable? greaterThanOrEqual = null,
    IComparable? lessThan = null,
    IComparable? lessThanOrEqual = null,
    IComparable? equalTo = null,
    string? message = null);
```

The typed overload replaces `string field` with
`Expression<Func<T, TValue>> field`.

### Inclusion and exclusion

```csharp
ValidateInclusion(
    string field,
    IReadOnlyList<object> values,
    string? message = null);

ValidateInclusion<TValue>(
    Expression<Func<T, TValue>> field,
    IReadOnlyCollection<TValue> values,
    string? message = null);

ValidateExclusion(
    string field,
    IReadOnlyList<object> values,
    string? message = null);

ValidateExclusion<TValue>(
    Expression<Func<T, TValue>> field,
    IReadOnlyCollection<TValue> values,
    string? message = null);
```

### Confirmation

```csharp
ValidateConfirmation(
    string field,
    string? message = null);

ValidateConfirmation<TValue>(
    Expression<Func<T, TValue>> field,
    string? message = null);
```

### Custom validation

```csharp
Validate(
    Func<Changeset<T>, Changeset<T>> validator);

ValidateChange(
    string field,
    Func<Changeset<T>, object?, Changeset<T>> validator);

ValidateChange<TValue>(
    Expression<Func<T, TValue>> field,
    Func<Changeset<T>, object?, Changeset<T>> validator);

Task<Changeset<T>> ValidateChangeAsync(
    string field,
    Func<Changeset<T>, object?, Task<Changeset<T>>> validator);
```

`ValidateChangeAsync` also has a typed property overload and overloads that
extend `Task<Changeset<T>>`. A task-based pipeline can call:

```csharp
Task<Changeset<T>> ValidateAsync(
    Func<Changeset<T>, Changeset<T>> validator);
```

See [Validation](validation.md) for skip behavior and error codes.

## Associations

```csharp
Changeset<T> CastAssoc<T, TAssoc>(
    string field,
    IReadOnlyList<string> permitted,
    CastOptions? options = null);

Changeset<T> ValidateAssoc<T, TAssoc>(
    string field,
    Func<Changeset<TAssoc>, Changeset<TAssoc>> validator);

Changeset<TAssoc>? GetAssoc<T, TAssoc>(string field);
```

Both types must be classes. See [Associations](associations.md) for recursive
materialization behavior.

## Applying

```csharp
T ApplyChanges<T>();
T ApplyChanges<T>(Func<T> factory);

ChangesetResult<T> ToResult<T>();
ChangesetResult<T> ToResult<T>(Func<T> factory);
```

The overloads without a factory require `T : class, new()`. Factory overloads
require `T : class`. See [Applying Changes](applying-changes.md).

### Generated appliers

```csharp
public interface IChangesetApplier<T>
{
    IReadOnlySet<string> ValidFields { get; }
    T Create(IReadOnlyDictionary<string, object?> changes);
    T Apply(T source, IReadOnlyDictionary<string, object?> changes);
}

ChangesetApplierRegistry.Register<T>(IChangesetApplier<T> applier);
ChangesetApplierRegistry.Get<T>();
```

Most applications use `[ChangesetTarget]` instead of implementing and
registering this interface manually.

## EF Core

Extension methods live in `Changeset.EntityFramework`.

```csharp
T ApplyTo<T>(DbContext context);

Task<T> ApplyToAsync<T>(
    DbContext context,
    CancellationToken cancellationToken = default);

Task<ChangesetResult<T>> TryApplyToAsync<T>(
    DbContext context,
    Func<DbUpdateException, ChangesetError?> mapSaveError,
    CancellationToken cancellationToken = default);
```

All three require `T : class, new()`. `TryApplyToAsync` returns `Invalid` for
an already-invalid changeset, saves and returns `Valid` otherwise, and passes
a `DbUpdateException` to `mapSaveError`; a returned error yields `Invalid`
while `null` rethrows.

```csharp
Changeset<T> ValidateUnique(
    string field,
    DbContext context,
    string? message = null,
    Expression<Func<T, bool>>? scope = null);

Changeset<T> ValidateUnique<TValue>(
    Expression<Func<T, TValue>> fields,
    DbContext context,
    string? message = null,
    Expression<Func<T, bool>>? scope = null);

Changeset<T> ValidateUnique(
    IReadOnlyList<string> fields,
    DbContext context,
    string? message = null,
    Expression<Func<T, bool>>? scope = null);

Task<Changeset<T>> ValidateUniqueAsync(
    string field,
    DbContext context,
    string? message = null,
    Expression<Func<T, bool>>? scope = null,
    CancellationToken cancellationToken = default);
```

The async method has the same expression and field-list overloads. The
expression form accepts a single property (`u => u.Email`) or an anonymous
type for composite uniqueness (`u => new { u.TenantId, u.Email }`). `scope`
narrows the uniqueness query. On updates the current row is excluded by
primary key. An unmapped field throws `ArgumentException`.

## ASP.NET Core

Extension methods live in `Changeset.EntityFramework`.

```csharp
IDictionary<string, string[]> ToValidationErrors<T>();
IDictionary<string, string[]> ToValidationErrors(
    this ImmutableArray<ChangesetError> errors);
IResult? ToValidationProblemOrNull<T>();
HttpValidationProblemDetails ToProblemDetails<T>();
void AddToModelState<T>(ModelStateDictionary modelState);
```

The `ImmutableArray<ChangesetError>` overload converts an error list — for
example from `ChangesetResult<T>.Invalid` — without a changeset instance.

See [EF Core & ASP.NET Core](ef-core.md#aspnet-core) for response examples.

## Source generator

```csharp
[AttributeUsage(
    AttributeTargets.Class,
    Inherited = false,
    AllowMultiple = false)]
public sealed class ChangesetTargetAttribute : Attribute;
```

The generator currently discovers attributed class and record declarations.
See [Source Generator](source-generator.md) for model requirements and analyzer
diagnostics.
