# Validation

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

## Built-in validators

| Validator | Checks |
|---|---|
| `ValidateRequired` | Fields are present and non-blank |
| `ValidateFormat` | Value matches a regex pattern |
| `ValidateLength` | String length within `min`/`max` bounds |
| `ValidateNumber` | Numeric comparisons (`greaterThan`, `lessThan`, `greaterThanOrEqual`, …) |
| `ValidateInclusion` | Value is in an allowed set |
| `ValidateExclusion` | Value is not in a forbidden set |
| `ValidateConfirmation` | Field matches its `*Confirmation` counterpart |
| `ValidateChange` | Custom per-field logic |
| `Validate` | Custom whole-changeset logic |

## Async validators

Use async validators for I/O-bound checks:

```csharp
var cs = await Changeset<User>.Cast(params, ["Email"])
    .ValidateChangeAsync("Email", async (cs, value) =>
    {
        if (await db.Users.AnyAsync(u => u.Email == (string)value!))
            return cs.AddError("Email", "has already been taken", "uniqueness");
        return cs;
    });
```

## Nested changesets

Cast and validate associated models with `CastAssoc` and `ValidateAssoc`:

```csharp
var cs = Changeset<UserWithAddress>.Cast(params, ["Name"])
    .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City", "Zip"])
    .ValidateAssoc<UserWithAddress, Address>("Address", addressCs =>
        addressCs.ValidateRequired(["Street", "City"]));

// Errors use dot-notation: "Address.Street"
```
