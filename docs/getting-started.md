# Getting Started

This tutorial builds a complete changeset for creating and updating a user.
It uses only the core `Changeset` package.

## Requirements and installation

Create a .NET 10 project and add the package:

```shell
dotnet add package Changeset
```

Import the core namespace and the validator extensions:

```csharp
using Changeset;
using Changeset.Validators;
```

Define a model with public writable properties:

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public bool IsAdmin { get; set; }
}
```

## Create a user

Start with untrusted input. Values may already have their target type or may be
strings and `JsonElement` values produced by request parsing.

```csharp
var parameters = new Dictionary<string, object?>
{
    ["Name"] = "  Ada Lovelace  ",
    ["Email"] = "ada@example.com",
    ["Age"] = "36",
    ["IsAdmin"] = true
};
```

Cast only the fields this operation accepts:

```csharp
var changeset = Changeset<User>.Cast(
    parameters,
    user => new { user.Name, user.Email, user.Age });
```

`Name` is trimmed, `Age` is converted to `int`, and `IsAdmin` is ignored because
it was not permitted. This allowlist is the main input boundary; it prevents a
caller from setting arbitrary writable properties.

Add validation:

```csharp
var emailPattern = @"^[^@]+@[^@]+\.[^@]+$";

changeset = changeset
    .ValidateRequired(user => new { user.Name, user.Email })
    .ValidateFormat(user => user.Email, emailPattern)
    .ValidateLength(user => user.Name, min: 2, max: 100)
    .ValidateNumber(
        user => user.Age,
        greaterThanOrEqual: 0,
        lessThan: 150);
```

Validators return a changeset rather than changing the existing instance.
Errors accumulate, so the caller can return all detected problems at once.

Apply only a valid changeset:

```csharp
if (changeset.IsValid)
{
    User user = changeset.ApplyChanges();
    Console.WriteLine($"{user.Name} is {user.Age}");
}
else
{
    foreach (var error in changeset.Errors)
        Console.WriteLine($"{error.Field}: {error.Message} ({error.Code})");
}
```

For a non-throwing branch at the call site, use `ToResult`:

```csharp
var result = changeset.ToResult();

switch (result)
{
    case ChangesetResult<User>.Valid(var user):
        Console.WriteLine($"Created {user.Name}");
        break;

    case ChangesetResult<User>.Invalid(var errors):
        Console.WriteLine($"Rejected with {errors.Length} error(s)");
        break;
}
```

## Update an existing user

Pass the existing instance as the first argument to create an update changeset:

```csharp
var existing = new User
{
    Id = 42,
    Name = "Ada",
    Email = "ada@example.com",
    Age = 36
};

var parameters = new Dictionary<string, object?>
{
    ["Name"] = "Augusta Ada",
    ["Email"] = "ada@example.com"
};

var changeset = Changeset<User>
    .Cast(existing, parameters, user => new { user.Name, user.Email })
    .ValidateLength(user => user.Name, min: 2, max: 100);
```

Only `Name` appears in `Changes`; the unchanged email is omitted. Applying the
changeset creates a shallow copy, copies the existing public read/write
properties, and applies the changed values:

```csharp
User updated = changeset.ApplyChanges();

Console.WriteLine(updated.Id);              // 42
Console.WriteLine(updated.Name);            // Augusta Ada
Console.WriteLine(ReferenceEquals(existing, updated)); // false
```

The original instance is not mutated by the core `ApplyChanges` method.

## Inspect what happened

The most commonly inspected properties are:

```csharp
changeset.Action      // Insert or Update
changeset.Data        // null for insert; existing model for update
changeset.Params      // original input dictionary
changeset.CastFields  // successfully cast fields that actually changed
changeset.Changes     // typed proposed values
changeset.Errors      // structured cast and validation errors
changeset.IsValid     // true when Errors is empty
```

Continue with [The Changeset Lifecycle](lifecycle.md) for the precise model,
[Casting](casting.md) for conversion behavior, and [Validation](validation.md)
for every validator.
