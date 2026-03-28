# Changeset.NET

Ecto-style changesets for C#. Cast untyped input into typed models, validate with a composable pipeline, and apply changes — all immutable, all without exceptions.

## Quick Example

```csharp
using Changeset;
using Changeset.Validators;

var cs = Changeset<User>.Cast(params, u => new { u.Name, u.Email, u.Age })
    .ValidateRequired(["Name", "Email"])
    .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
    .ValidateLength("Name", min: 2, max: 100)
    .ValidateNumber("Age", greaterThanOrEqual: 0, lessThan: 150);

if (cs.IsValid)
{
    User user = cs.ApplyChanges();
}
```

## Packages

| Package | Purpose |
|---|---|
| `Changeset` | Core library — casting, validation, error handling |
| `Changeset.EntityFramework` | EF Core integration — `ApplyTo`, `ValidateUnique`, ASP.NET Core helpers |
| `Changeset.Generators` | Source generator — reflection-free property setting, field name analyzer |

## Casting

Casting converts untyped `Dictionary<string, object?>` input into typed field values. Only permitted fields are accepted — everything else is silently dropped (or errors with `StrictCasting`).

```csharp
// Insert — creates a new changeset
var cs = Changeset<User>.Cast(params, ["Name", "Email", "Age"]);

// Update — only includes fields that actually changed
var cs = Changeset<User>.Cast(existingUser, params, ["Name", "Email"]);

// Expression syntax — compile-time safe field names
var cs = Changeset<User>.Cast(params, u => new { u.Name, u.Email });
```

Type coercion handles string-to-number, string-to-bool, string-to-DateTime, `JsonElement` unwrapping, nullable types, and checked numeric narrowing. Cast failures produce errors, never exceptions.

### Cast Options

```csharp
var options = new CastOptions
{
    TrimStrings = true,              // Trim whitespace from string values (default: true)
    CaseInsensitiveFields = true,    // Match field names case-insensitively (default: true)
    StrictCasting = false,           // Error on unpermitted fields instead of ignoring (default: false)
    FormatProvider = CultureInfo.InvariantCulture  // Culture for number/date parsing (default: InvariantCulture)
};

var cs = Changeset<User>.Cast(params, ["Name", "Email"], options);
```

## Validation

Validators are pure functions composed via method chaining. Each returns a new immutable changeset (or the same instance if no error was added).

```csharp
var cs = Changeset<User>.Cast(params, ["Name", "Email", "Age"])
    .ValidateRequired(["Name", "Email"])
    .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
    .ValidateLength("Name", min: 2, max: 100)
    .ValidateNumber("Age", greaterThanOrEqual: 0, lessThan: 150)
    .ValidateInclusion("Role", ["admin", "member", "guest"])
    .ValidateExclusion("Name", ["admin", "root"])
    .ValidateConfirmation("Password")
    .ValidateChange("Email", (cs, value) => /* custom logic */ cs)
    .Validate(cs => /* whole-changeset logic */ cs);
```

Async validators for I/O-bound checks:

```csharp
var cs = await Changeset<User>.Cast(params, ["Email"])
    .ValidateChangeAsync("Email", async (cs, value) =>
    {
        if (await db.Users.AnyAsync(u => u.Email == (string)value!))
            return cs.AddError("Email", "has already been taken", "uniqueness");
        return cs;
    });
```

### Nested Changesets

```csharp
var cs = Changeset<UserWithAddress>.Cast(params, ["Name"])
    .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City", "Zip"])
    .ValidateAssoc<UserWithAddress, Address>("Address", addressCs =>
        addressCs.ValidateRequired(["Street", "City"]));

// Errors use dot-notation: "Address.Street"
```

## Errors

Errors accumulate — multiple validators can report multiple errors on the same field. Errors serialize cleanly for API responses.

```csharp
cs.IsValid                  // bool
cs.Errors                   // all errors
cs.ErrorsOn("Email")        // errors for a specific field
cs.ErrorMap                 // Dictionary<string, ImmutableArray<ChangesetError>>
cs.HasErrorOn("Email")      // bool
```

```json
{
  "Name": [{"message": "can't be blank", "code": "required"}],
  "Email": [{"message": "has already been taken", "code": "uniqueness"}]
}
```

## Applying Changes

```csharp
// Create a new instance
User user = cs.ApplyChanges();

// With a factory (no parameterless constructor needed)
User user = cs.ApplyChanges(() => new User(someArg));

// Pattern matching with result type
var result = cs.ToResult();
// result is ChangesetResult<User>.Valid(user) or ChangesetResult<User>.Invalid(errors)
```

## EF Core Integration

```csharp
using Changeset.EntityFramework;

// Insert — adds to DbContext
var entity = await cs.ApplyToAsync(dbContext);

// Update — marks only changed properties as modified
cs.ApplyTo(dbContext);
await dbContext.SaveChangesAsync();

// Uniqueness validation against the database
cs = cs.ValidateUnique("Email", dbContext);
```

### ASP.NET Core

```csharp
// Minimal APIs
if (cs.ToValidationProblemOrNull() is { } problem)
    return problem;

// MVC Controllers
cs.AddToModelState(ModelState);

// ProblemDetails
var details = cs.ToProblemDetails();
```

## Source Generator

Add `[ChangesetTarget]` to your models for reflection-free `ApplyChanges` and build-time field name validation:

```csharp
[ChangesetTarget]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

The generator emits a switch-based property setter (no reflection at runtime) and registers it automatically via module initializer. It also includes a diagnostic analyzer:

- **CHGSET001**: Field name typo with "did you mean?" suggestion
- **CHGSET002**: Completely unknown field name

## Project Structure

```
src/
  Changeset/                        Core library
  Changeset.EntityFramework/        EF Core + ASP.NET Core integration
  Changeset.Generators/             Source generator + analyzer
test/
  Changeset.Tests/                  Core unit tests
  Changeset.EntityFramework.Tests/  EF integration tests
  Changeset.Generators.Tests/       Generator + analyzer tests
samples/
  Changeset.Sample.WebApi/          ASP.NET Core minimal API sample
```

## License

MIT
