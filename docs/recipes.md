# Recipes

These examples combine the core operations into common application workflows.
For parameter details, use the [API Reference](api-reference.md).

## Create from a Minimal API request

Accept a JSON object, restrict its fields, validate it, and return all messages
in ASP.NET Core's standard validation shape:

```csharp
app.MapPost("/users", async (
    JsonElement body,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var parameters =
        JsonSerializer.Deserialize<Dictionary<string, object?>>(
            body.GetRawText())
        ?? new Dictionary<string, object?>();

    var changeset = Changeset<User>
        .Cast(parameters, user => new
        {
            user.Name,
            user.Email,
            user.Age
        })
        .ValidateRequired(user => new { user.Name, user.Email })
        .ValidateFormat(
            user => user.Email,
            @"^[^@]+@[^@]+\.[^@]+$")
        .ValidateNumber(
            user => user.Age,
            greaterThanOrEqual: 0,
            lessThan: 150);

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

The allowlist prevents fields such as `Id` or `IsAdmin` from being assigned even
if they appear in the request.

## Perform a partial update

Only validate optional fields when they actually changed:

```csharp
var changeset = Changeset<User>
    .Cast(existing, parameters, user => new
    {
        user.Name,
        user.Email,
        user.Age
    })
    .ValidateLength(user => user.Name, min: 2, max: 100)
    .ValidateFormat(user => user.Email, emailPattern)
    .ValidateNumber(user => user.Age, greaterThanOrEqual: 0);
```

These validators skip absent and unchanged fields. Avoid an unconditional
`ValidateRequired` in a PATCH-style operation because it considers a missing
field an error.

If a submitted field must not be blank, combine `ValidateChange` with a custom
rule:

```csharp
changeset = changeset.ValidateChange(
    user => user.Name,
    (current, value) =>
        value is string name && !string.IsNullOrWhiteSpace(name)
            ? current
            : current.AddError("Name", "can't be blank", "required"));
```

## Return structured error codes

`ToValidationErrors` intentionally returns messages only. Return `ErrorMap`
when an API client needs codes and metadata:

```csharp
if (!changeset.IsValid)
{
    return Results.Json(
        new
        {
            errors = changeset.ErrorMap
        },
        statusCode: StatusCodes.Status422UnprocessableEntity);
}
```

Serializer configuration controls property casing. Consider mapping errors to a
versioned application DTO when the response contract is public.

## Validate password confirmation

Keep the confirmation value in input parameters without adding it to the model:

```csharp
var parameters = new Dictionary<string, object?>
{
    ["Password"] = "correct horse battery staple",
    ["Password_confirmation"] = "correct horse battery staple"
};

var changeset = Changeset<Account>
    .Cast(parameters, account => account.Password)
    .ValidateRequired(account => account.Password)
    .ValidateLength(account => account.Password, min: 12)
    .ValidateConfirmation(account => account.Password);
```

The confirmation key is exactly `<PropertyName>_confirmation`.

## Cast and constrain an enum

String-to-enum casting ignores case:

```csharp
public enum UserRole
{
    Member,
    Moderator,
    Admin
}

var allowed = new HashSet<UserRole>
{
    UserRole.Member,
    UserRole.Moderator
};

var changeset = Changeset<User>
    .Cast(parameters, user => user.Role)
    .ValidateInclusion(user => user.Role, allowed);
```

An unknown enum string produces `invalid_cast`. A valid enum excluded by the
operation produces `inclusion`.

## Add an asynchronous custom validator

Run I/O only when a field was successfully cast and changed:

```csharp
var changeset = await Changeset<User>
    .Cast(parameters, user => new { user.Email, user.Name })
    .ValidateChangeAsync(user => user.Email, async (current, value) =>
    {
        var email = (string)value!;
        var blocked = await reputationService.IsBlocked(email);

        return blocked
            ? current.AddError(
                "Email",
                "cannot be used",
                "blocked_email")
            : current;
    })
    .ValidateAsync(current =>
        current.ValidateLength(user => user.Name, min: 2));
```

`ValidateAsync` continues a task-based pipeline with synchronous validation.

## Validate a cross-field rule

Use `Validate` for rules involving several changes:

```csharp
var changeset = changeset.Validate(current =>
{
    var startsAt = current.GetChange<DateTime?>("StartsAt");
    var endsAt = current.GetChange<DateTime?>("EndsAt");

    if (startsAt.HasValue &&
        endsAt.HasValue &&
        endsAt.Value < startsAt.Value)
    {
        return current.AddBaseError(
            "the end must not precede the start",
            "invalid_time_range");
    }

    return current;
});
```

For updates, `GetChange` sees proposed changes only. Read `current.Data` as well
when a rule must combine changed and unchanged model values.

## Use a model without a parameterless constructor

Supply a factory for inserts:

```csharp
var changeset = Changeset<Order>.Cast(
    parameters,
    ["Reference", "Notes"]);

Order order = changeset.ApplyChanges(
    () => new Order(customerId));
```

The factory overload does not remove the parameterless-constructor requirement
from reflection-based updates. See
[Applying Changes](applying-changes.md#types-without-a-parameterless-constructor).

## Cast and validate a nested object

```csharp
var changeset = Changeset<User>
    .Cast(parameters, user => user.Name)
    .CastAssoc<User, Address>(
        "Address",
        ["Street", "City", "Zip"])
    .ValidateAssoc<User, Address>(
        "Address",
        address => address.ValidateRequired(["Street", "City"]));
```

The parent reports fields such as `Address.Street`. Apply the parent to
recursively materialize the child:

```csharp
var user = changeset.ApplyChanges();
```

See [Associations](associations.md#materialization) for insert, update, and
Entity Framework behavior.

## Enforce uniqueness safely

Use changeset validation for a friendly early error and a database constraint
for correctness. `TryApplyToAsync` maps the constraint violation into a
changeset error when the preflight check loses the race:

```csharp
changeset = await changeset.ValidateUniqueAsync(
    user => user.Email,
    dbContext,
    cancellationToken: cancellationToken);

if (!changeset.IsValid)
    return Results.ValidationProblem(changeset.ToValidationErrors());

var result = await changeset.TryApplyToAsync(
    dbContext,
    exception => IsUniqueEmailViolation(exception)
        ? ChangesetError.For("Email", "has already been taken", "uniqueness")
        : null,
    cancellationToken);

return result switch
{
    ChangesetResult<User>.Valid(var user) =>
        Results.Created($"/users/{user.Id}", user),
    ChangesetResult<User>.Invalid(var errors) =>
        Results.ValidationProblem(errors.ToValidationErrors()),
};
```

Provider-specific code must implement `IsUniqueEmailViolation` — inspect the
`DbUpdateException.InnerException` for the provider's error code (for example
`2601`/`2627` for SQL Server or `23505` for PostgreSQL) and the index name.
