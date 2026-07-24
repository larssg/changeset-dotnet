# Changeset.NET

Changeset.NET provides an immutable boundary between untrusted input and your
C# domain models.

```csharp
var changeset = Changeset<User>
    .Cast(parameters, user => new { user.Name, user.Email, user.Age })
    .ValidateRequired(user => new { user.Name, user.Email })
    .ValidateFormat(user => user.Email, emailPattern)
    .ValidateNumber(user => user.Age, greaterThanOrEqual: 0);

return changeset.IsValid
    ? Results.Ok(changeset.ApplyChanges())
    : Results.ValidationProblem(changeset.ToValidationErrors());
```

## Why use a changeset?

Request data, form values, imported records, and message payloads are not domain
objects yet. A changeset keeps the steps between those two forms visible:

1. **Cast** only explicitly permitted fields and coerce their values.
2. **Validate** the proposed changes with a composable pipeline.
3. **Inspect** changes and structured errors without mutating the model.
4. **Apply** a valid changeset when the caller decides it is safe.

This is useful for create forms, partial updates, API endpoints, imports, and
other boundaries where input must be filtered before it reaches application
state.

!!! note

    Casting and validation failures are represented by `ChangesetError`
    values. Calling `ApplyChanges` on an invalid changeset is a programmer
    error and throws `InvalidOperationException`; check `IsValid` or use
    `ToResult` first.

## Choose your packages

| Package | Provides |
|---|---|
| `Changeset` | The core changeset type, casting, validators, associations, errors, and applying |
| `Changeset.EntityFramework` | `ApplyTo`, uniqueness validation, Minimal API results, `ProblemDetails`, and `ModelState` helpers |
| `Changeset.Generators` | Generated property appliers and diagnostics for string field lists passed to `Cast` |

The core and EF Core packages target .NET 8 and .NET 10.

## Where to begin

- Follow [Getting Started](getting-started.md) for a complete create and update
  example.
- Read [The Changeset Lifecycle](lifecycle.md) to understand the state held by
  a changeset.
- Use [Casting](casting.md) for the input allowlist, conversion matrix, and
  cast options.
- Use [Validation](validation.md) for built-in, custom, and asynchronous
  validators.
- See [Errors](errors.md) and [Applying Changes](applying-changes.md) for the
  two possible outcomes.
- Continue to [EF Core & ASP.NET Core](ef-core.md) or the
  [Source Generator](source-generator.md) when you need integrations.
- Copy a complete workflow from [Recipes](recipes.md).
- Look up signatures in the [API Reference](api-reference.md) or start from a
  symptom in [Troubleshooting](troubleshooting.md).
