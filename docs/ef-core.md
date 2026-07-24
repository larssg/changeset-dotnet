# EF Core & ASP.NET Core

The `Changeset.EntityFramework` package integrates changesets with EF Core
persistence and ASP.NET Core validation responses. It targets .NET 10 and
references the ASP.NET Core shared framework.

```shell
dotnet add package Changeset.EntityFramework
```

Import its extension methods:

```csharp
using Changeset.EntityFramework;
```

## EF Core

### Apply an insert

`ApplyTo` materializes an insert changeset and adds the entity to the
appropriate `DbSet<T>`:

```csharp
var changeset = Changeset<User>
    .Cast(parameters, user => new { user.Name, user.Email })
    .ValidateRequired(user => new { user.Name, user.Email });

if (!changeset.IsValid)
    return;

User entity = changeset.ApplyTo(dbContext);

dbContext.Entry(entity).State; // EntityState.Added
await dbContext.SaveChangesAsync(cancellationToken);
```

The synchronous method does not call `SaveChanges`.

Use `ApplyToAsync` to apply and immediately save:

```csharp
User entity = await changeset.ApplyToAsync(
    dbContext,
    cancellationToken);
```

Despite its name, materialization itself is synchronous; this method is
asynchronous because it calls `SaveChangesAsync`.

### Apply an update

For an update, pass the entity as `Data` when casting:

```csharp
var entity = await dbContext.Users.FindAsync([id], cancellationToken);

if (entity is null)
    return Results.NotFound();

var changeset = Changeset<User>
    .Cast(entity, parameters, user => new { user.Name, user.Email })
    .ValidateLength(user => user.Name, min: 2, max: 100);

if (!changeset.IsValid)
    return Results.ValidationProblem(changeset.ToValidationErrors());

changeset.ApplyTo(dbContext);
await dbContext.SaveChangesAsync(cancellationToken);
```

Unlike the core `ApplyChanges`, `ApplyTo` mutates the existing entity in
`changeset.Data`. If it is detached, the method attaches it. It then assigns
only entries in `Changes` and marks only those scalar properties as modified.
Unchanged properties and navigation properties not present in `Changes` are
left alone.

!!! warning

    The changeset must be valid. `ApplyTo` and `ApplyToAsync` throw
    `InvalidOperationException` for an invalid changeset.

### Validate uniqueness

Check a changed value against the database:

```csharp
var changeset = Changeset<User>
    .Cast(parameters, user => user.Email)
    .ValidateUnique(user => user.Email, dbContext);
```

The asynchronous form supports cancellation:

```csharp
var changeset = await Changeset<User>
    .Cast(parameters, user => user.Email)
    .ValidateUniqueAsync(
        user => user.Email,
        dbContext,
        cancellationToken: cancellationToken);
```

Both methods:

- skip the database query when the field is absent from `Changes`
- query with `AsNoTracking`
- add `has already been taken` with the code `uniqueness` on conflict
- accept a custom message
- provide string and direct-property-expression overloads

```csharp
changeset = await changeset.ValidateUniqueAsync(
    user => user.Email,
    dbContext,
    message: "is already registered",
    cancellationToken: cancellationToken);
```

An unchanged value in an ordinary update cast is omitted from `Changes`, so its
uniqueness query is skipped.

!!! important

    Application-side uniqueness checks do not replace a database unique
    constraint. Concurrent requests can pass validation before either writes.
    Keep the constraint and translate its database exception when necessary.

The query compares one property for equality. Composite uniqueness, filtered
indexes, tenant scoping, normalization, and provider-specific comparison rules
need a custom validator or query.

### Entity requirements and limitations

`ApplyTo<T>` and `ApplyToAsync<T>` require `T : class, new()`. Inserts therefore
need a public parameterless constructor.

The integration recursively applies child changesets created by `CastAssoc`.
On updates, existing tracked child entities are retained and only their changed
scalar properties are marked modified.

## ASP.NET Core

The same package contains helpers for Minimal APIs, `ProblemDetails`, and MVC
model state.

### Validation dictionaries

Convert errors to the shape expected by ASP.NET Core:

```csharp
IDictionary<string, string[]> errors =
    changeset.ToValidationErrors();

return Results.ValidationProblem(errors);
```

The dictionary groups error messages by exact field name. Codes and metadata
are intentionally omitted. Base errors appear under the empty-string key and
nested errors retain dotted keys such as `Address.City`.

### Minimal APIs

`ToValidationProblemOrNull` supports an early-return pattern:

```csharp
app.MapPost("/users", async (
    Dictionary<string, object?> parameters,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var changeset = Changeset<User>
        .Cast(parameters, user => new { user.Name, user.Email })
        .ValidateRequired(user => new { user.Name, user.Email });

    changeset = await changeset.ValidateUniqueAsync(
        user => user.Email,
        dbContext,
        cancellationToken: cancellationToken);

    if (changeset.ToValidationProblemOrNull() is { } problem)
        return problem;

    var user = await changeset.ApplyToAsync(
        dbContext,
        cancellationToken);

    return Results.Created($"/users/{user.Id}", user);
});
```

The helper returns `null` for a valid changeset and the result of
`Results.ValidationProblem` for an invalid one.

### Problem details

Create an `HttpValidationProblemDetails` instance when an endpoint or service
needs to customize it:

```csharp
var details = changeset.ToProblemDetails();
details.Title = "The user could not be saved";
details.Extensions["traceId"] = Activity.Current?.Id;

return Results.Problem(details);
```

`ToProblemDetails` creates an object even for a valid changeset; its `Errors`
dictionary is simply empty. Check `IsValid` when the response should only be
created for failures.

### MVC controllers

Copy every changeset message into `ModelState`:

```csharp
changeset.AddToModelState(ModelState);

if (!ModelState.IsValid)
    return ValidationProblem(ModelState);
```

Existing model-state errors are preserved. A valid changeset adds nothing.

## Complete sample

The repository contains a runnable Minimal API under
[`samples/Changeset.Sample.WebApi`](https://github.com/larssg/changeset-dotnet/tree/main/samples/Changeset.Sample.WebApi).
It demonstrates JSON input, create and update changesets, and validation
responses without a database.
