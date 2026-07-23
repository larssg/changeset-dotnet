# Getting Started

## Installation

Install the core package:

```shell
dotnet add package Changeset
```

For EF Core and ASP.NET Core integration:

```shell
dotnet add package Changeset.EntityFramework
```

For the source generator and analyzer:

```shell
dotnet add package Changeset.Generators
```

## First changeset

```csharp
using Changeset;
using Changeset.Validators;

// Untrusted input — e.g. deserialized JSON from a request body
var params = new Dictionary<string, object?>
{
    ["Name"] = "Ada",
    ["Email"] = "ada@example.com",
    ["Age"] = "36",          // strings are coerced to the target type
    ["IsAdmin"] = true,       // not permitted below — silently dropped
};

var cs = Changeset<User>.Cast(params, u => new { u.Name, u.Email, u.Age })
    .ValidateRequired(["Name", "Email"])
    .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
    .ValidateLength("Name", min: 2, max: 100)
    .ValidateNumber("Age", greaterThanOrEqual: 0, lessThan: 150);

if (cs.IsValid)
{
    User user = cs.ApplyChanges();
}
else
{
    var errors = cs.ErrorMap; // ready to serialize into an API response
}
```

The flow is always the same:

1. **Cast** — filter raw input down to permitted fields and coerce types. See [Casting](casting.md).
2. **Validate** — chain validators; errors accumulate, nothing throws. See [Validation](validation.md).
3. **Apply** — materialize the model when valid. See [Applying Changes](applying-changes.md).
