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
```

Both require `T : class, new()`.

```csharp
Changeset<T> ValidateUnique(
    string field,
    DbContext context,
    string? message = null);

Changeset<T> ValidateUnique<TValue>(
    Expression<Func<T, TValue>> field,
    DbContext context,
    string? message = null);

Task<Changeset<T>> ValidateUniqueAsync(
    string field,
    DbContext context,
    string? message = null,
    CancellationToken cancellationToken = default);
```

The async method also has a typed property overload.

## ASP.NET Core

Extension methods live in `Changeset.EntityFramework`.

```csharp
IDictionary<string, string[]> ToValidationErrors<T>();
IResult? ToValidationProblemOrNull<T>();
HttpValidationProblemDetails ToProblemDetails<T>();
void AddToModelState<T>(ModelStateDictionary modelState);
```

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
