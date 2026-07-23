# Changeset.NET

Ecto-style changesets for C#. Cast untyped input into typed models, validate with a composable pipeline, and apply changes — all immutable, all without exceptions.

```csharp
using Changeset;
using Changeset.Validators;

var cs = Changeset<User>.Cast(params, u => new { u.Name, u.Email, u.Age })
    .ValidateRequired(u => new { u.Name, u.Email })
    .ValidateFormat(u => u.Email, @"^[^@]+@[^@]+\.[^@]+$")
    .ValidateLength(u => u.Name, min: 2, max: 100)
    .ValidateNumber(u => u.Age, greaterThanOrEqual: 0, lessThan: 150);

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

## Why changesets?

Changesets separate the concerns of *receiving* untrusted input, *validating* it, and *applying* it to your domain models:

- **Casting** filters and coerces raw input (`Dictionary<string, object?>`) into typed field values — only permitted fields get through.
- **Validation** is a pipeline of pure functions. Each validator returns a new immutable changeset; errors accumulate instead of throwing.
- **Applying** produces your model only when you decide to — and only if the changeset is valid.

Head to [Getting Started](getting-started.md) to install the packages, or dive into the guide:

- [Casting](casting.md) — turning untyped input into typed field values
- [Validation](validation.md) — the validator pipeline, async validators, nested changesets
- [Errors](errors.md) — inspecting and serializing errors
- [Applying Changes](applying-changes.md) — materializing models and result types
- [EF Core & ASP.NET Core](ef-core.md) — database persistence and web framework helpers
- [Source Generator](source-generator.md) — reflection-free property setting and compile-time field checks

## License

MIT
