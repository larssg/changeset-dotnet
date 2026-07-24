# Changeset.NET

Changeset.NET brings an Ecto-style cast, validate, and apply pipeline to C#.
It turns untrusted, untyped input into an immutable description of proposed
changes before anything is written to your model or database.

```csharp
using Changeset;
using Changeset.Validators;

var parameters = new Dictionary<string, object?>
{
    ["Name"] = "Ada",
    ["Email"] = "ada@example.com",
    ["Age"] = "36"
};

var changeset = Changeset<User>
    .Cast(parameters, user => new { user.Name, user.Email, user.Age })
    .ValidateRequired(user => new { user.Name, user.Email })
    .ValidateFormat(user => user.Email, @"^[^@]+@[^@]+\.[^@]+$")
    .ValidateNumber(user => user.Age, greaterThanOrEqual: 0);

if (changeset.IsValid)
{
    User user = changeset.ApplyChanges();
}
```

Casting acts as an allowlist, performs type coercion, and reports bad input as
data. Validators compose without mutating the changeset. Applying is an
explicit final step and is only allowed for a valid changeset.

## Packages

| Package | Use it for |
|---|---|
| `Changeset` | Casting, validation, nested changesets, errors, and materialization |
| `Changeset.EntityFramework` | EF Core persistence, uniqueness checks, and ASP.NET Core responses |
| `Changeset.Generators` | Reflection-free property application and string-field diagnostics |

The core and EF Core packages target .NET 8 and .NET 10. The generator targets
`netstandard2.0` so it can run as a Roslyn analyzer.

## Install

```shell
dotnet add package Changeset
```

Optional integrations:

```shell
dotnet add package Changeset.EntityFramework
dotnet add package Changeset.Generators
```

## Documentation

Start with the [getting-started tutorial](docs/getting-started.md), then use the
guides for:

- [the changeset lifecycle](docs/lifecycle.md)
- [casting and type coercion](docs/casting.md)
- [built-in and custom validation](docs/validation.md)
- [errors and API responses](docs/errors.md)
- [applying changes](docs/applying-changes.md)
- [nested associations](docs/associations.md)
- [EF Core and ASP.NET Core](docs/ef-core.md)
- [the source generator and analyzer](docs/source-generator.md)
- [common recipes](docs/recipes.md)
- [the public API reference](docs/api-reference.md)
- [troubleshooting](docs/troubleshooting.md)

The documentation site is built with MkDocs:

```shell
python3 -m venv .venv
.venv/bin/python -m pip install -r docs/requirements.txt
.venv/bin/mkdocs serve
```

The canonical Markdown guides also generate two LLM-oriented resources:

- `docs/llms.txt` is a compact index with descriptions and reading order.
- `docs/llms-full.txt` combines the complete documentation into one context
  file.

After changing a guide, regenerate them with:

```shell
python3 tools/generate-llm-docs.py
```

Run `python3 tools/generate-llm-docs.py --check` and
`.venv/bin/mkdocs build --strict` before submitting documentation changes.

## Development

```shell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

The repository contains the core library under `src/Changeset`, integrations
under `src/Changeset.EntityFramework`, the generator under
`src/Changeset.Generators`, and a Minimal API example under
`samples/Changeset.Sample.WebApi`.

## License

MIT
