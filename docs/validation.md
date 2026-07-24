# Validation

Validators inspect the values in `Changes` and return a changeset. They do not
mutate the model or the input dictionary, and errors from multiple validators
accumulate.

```csharp
var changeset = Changeset<User>
    .Cast(parameters, user => new { user.Name, user.Email, user.Age })
    .ValidateRequired(user => new { user.Name, user.Email })
    .ValidateFormat(user => user.Email, emailPattern)
    .ValidateLength(user => user.Name, min: 2, max: 100)
    .ValidateNumber(user => user.Age, greaterThanOrEqual: 0);
```

## Changed-field semantics

Built-in field validators operate on `Changes`. Except for
`ValidateRequired`, they skip a field that was not successfully cast or did not
change during an update.

This makes partial updates natural:

```csharp
// Email is not in the request, so email format validation is skipped.
var changeset = Changeset<User>
    .Cast(existingUser, parameters, user => new { user.Name, user.Email })
    .ValidateFormat(user => user.Email, emailPattern);
```

`ValidateRequired` intentionally behaves differently: it adds an error when a
required field is absent from `Changes`, null, empty, or whitespace. Apply it
when the operation requires the field in the submitted change set. Be deliberate
when using it for PATCH-like updates.

## Field selectors

Prefer property expressions for fields known at compile time:

```csharp
changeset
    .ValidateRequired(user => new { user.Name, user.Email })
    .ValidateLength(user => user.Name, min: 2);
```

String overloads are available for dynamic field selection:

```csharp
changeset.ValidateLength("Name", min: 2);
```

Single-field expressions must be direct property accesses such as
`user => user.Email`. The required-fields expression accepts a direct property
or an anonymous object containing direct properties.

## Required

```csharp
changeset.ValidateRequired(user => new { user.Name, user.Email });
```

A value fails when the field is missing from `Changes`, is `null`, or is a
string containing no non-whitespace characters.

| Default message | Code |
|---|---|
| `can't be blank` | `required` |

## Format

```csharp
changeset.ValidateFormat(
    user => user.Email,
    @"^[^@]+@[^@]+\.[^@]+$",
    message: "must be an email address");
```

Format validation applies only to changed string values. It uses
`Regex.IsMatch` and stores the pattern in error metadata.

To use a compile-time generated regex, pass the generated `Regex` instance:

```csharp
[GeneratedRegex(@"^[^@]+@[^@]+\.[^@]+$")]
private static partial Regex EmailRegex();

changeset.ValidateFormat(user => user.Email, EmailRegex());
```

| Default message | Code | Metadata |
|---|---|---|
| `has invalid format` | `format` | `pattern` |

## Length

```csharp
changeset.ValidateLength(user => user.Name, min: 2, max: 100);
changeset.ValidateLength("Tags", @is: 3);
```

Length works with strings, `ICollection`, and general `IEnumerable` values.
For an enumerable, validation stops as soon as it has counted enough elements
to establish a maximum or exact-length failure.

The available constraints are:

- `min`: at least this many elements
- `max`: at most this many elements
- `is`: exactly this many elements

All length failures use the `length` code. Metadata contains configured `min`,
`max`, and `is` values, while the default message describes the first failing
constraint.

## Number

```csharp
changeset.ValidateNumber(
    user => user.Age,
    greaterThanOrEqual: 0,
    lessThan: 150);
```

The constraints are evaluated in this order:

1. `greaterThan`
2. `greaterThanOrEqual`
3. `lessThan`
4. `lessThanOrEqual`
5. `equalTo`

Changed values must implement `IComparable`. A failure uses the `number` code
and a message such as `must be greater than or equal to 0`.

## Inclusion and exclusion

```csharp
IReadOnlySet<UserRole> allowedRoles =
    new HashSet<UserRole> { UserRole.Member, UserRole.Admin };

changeset
    .ValidateInclusion(user => user.Role, allowedRoles)
    .ValidateExclusion(user => user.Name, new[] { "admin", "root" });
```

The typed overload accepts `IReadOnlyCollection<TValue>`. Passing an
`IReadOnlySet<TValue>` enables set membership lookup.

| Validator | Default message | Code |
|---|---|---|
| `ValidateInclusion` | `is invalid` | `inclusion` |
| `ValidateExclusion` | `is reserved` | `exclusion` |

## Confirmation

Confirmation compares a changed field with a value in the original `Params`
dictionary named `<field>_confirmation`:

```csharp
var parameters = new Dictionary<string, object?>
{
    ["Password"] = "correct horse battery staple",
    ["Password_confirmation"] = "correct horse battery staple"
};

var changeset = Changeset<Account>
    .Cast(parameters, account => account.Password)
    .ValidateConfirmation(account => account.Password);
```

The confirmation value does not need to be a writable or permitted model
property. A mismatch adds an error to `Password_confirmation` with the code
`confirmation`.

!!! note

    The suffix is exactly `_confirmation`, while the property portion keeps the
    field's casing.

## Custom field validation

`ValidateChange` runs only when the field exists in `Changes`:

```csharp
var changeset = changeset.ValidateChange(
    user => user.Email,
    (current, value) =>
    {
        var email = (string)value!;
        return email.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase)
            ? current
            : current.AddError(
                "Email",
                "must use the example.com domain",
                "company_email");
    });
```

Return the supplied changeset unchanged on success. Use `AddError` or
`AddBaseError` to return a new invalid changeset.

## Whole-changeset validation

`Validate` always calls the supplied function:

```csharp
var changeset = changeset.Validate(current =>
{
    var start = current.GetChange<DateOnly>("StartDate");
    var end = current.GetChange<DateOnly>("EndDate");

    return start is not null && end is not null && end < start
        ? current.AddBaseError(
            "end date must not precede start date",
            "invalid_date_range")
        : current;
});
```

This is useful for cross-field and domain-level rules.

## Asynchronous pipelines

Use `ValidateChangeAsync` for I/O-bound field checks:

```csharp
var changeset = await Changeset<User>
    .Cast(parameters, user => new { user.Email, user.Name })
    .ValidateChangeAsync(user => user.Email, async (current, value) =>
    {
        var email = (string)value!;
        return await EmailExists(email)
            ? current.AddError(
                "Email",
                "has already been taken",
                "uniqueness")
            : current;
    })
    .ValidateAsync(current =>
        current.ValidateLength(user => user.Name, min: 2));
```

Task-returning pipelines provide:

- `ValidateChangeAsync` to continue with another asynchronous field validator
- `ValidateAsync` to continue with a synchronous whole-changeset function

The EF Core package also supplies `ValidateUniqueAsync`; see
[EF Core & ASP.NET Core](ef-core.md).

## Custom messages and stable codes

Most built-in validators accept a custom `message`. The validator's error code
does not change:

```csharp
changeset.ValidateLength(
    user => user.Name,
    min: 2,
    message: "is too short");
```

Treat codes as the machine-readable part of an error and messages as display
text. See [Errors](errors.md) for structured error handling and
[Associations](associations.md) for nested validation.
