# Troubleshooting

This guide starts with the observable symptom and points to the relevant
changeset behavior.

## A submitted field is missing from `Changes`

Check these conditions:

1. The field must be in the permitted list or property expression.
2. The input dictionary must contain a matching key.
3. The target property must be public and writable.
4. The value must cast successfully.
5. During an update, the converted value must differ from the existing value.

Inspect both collections and errors:

```csharp
changeset.Params.ContainsKey("Email");
changeset.CastFields.Contains("Email");
changeset.Changes.ContainsKey("Email");
changeset.ErrorsOn("Email");
```

When `CaseInsensitiveFields` is `false`, key and property casing must match.

## `ValidateRequired` rejects an update with an omitted field

`ValidateRequired` requires a value to exist in `Changes`. It does not fall back
to the current value in `Data`.

For a full replacement operation, include and require every required field. For
a PATCH-style operation, validate only submitted fields or use a custom
`ValidateChange` rule that rejects blank submitted values.

See [Perform a partial update](recipes.md#perform-a-partial-update).

## A validator did not run

Most field validators skip fields absent from `Changes`. This includes fields
that:

- were not submitted
- were not permitted
- failed casting
- were equal to the existing value during an update

`ValidateChange` and `ValidateChangeAsync` follow the same rule.
`ValidateRequired` is the exception and reports absent fields.

## A nullable property rejects an empty string

Empty strings are not automatically converted to `null`. Casting `""` into
`int?`, `Guid?`, or a nullable date/time value produces `invalid_cast`.

Normalize application input before casting if an empty string means no value:

```csharp
var normalized = parameters.ToDictionary(
    pair => pair.Key,
    pair => pair.Value is string value && value.Length == 0
        ? null
        : pair.Value);
```

## A numeric or date value parses differently than expected

CLR string parsing uses `CastOptions.FormatProvider`, which defaults to
invariant culture:

```csharp
var options = new CastOptions
{
    FormatProvider = CultureInfo.GetCultureInfo("nb-NO")
};
```

JSON number tokens are read through `System.Text.Json` and are not
culture-formatted text.

## Applying throws for an invalid changeset

This is expected. Check `IsValid`, inspect `Errors`, or use `ToResult` before
applying:

```csharp
if (!changeset.IsValid)
    return Reject(changeset.Errors);

var model = changeset.ApplyChanges();
```

## Applying an update requires a parameterless constructor

The reflection-based update path creates a new `T` and shallow-copies the
existing model. Even `ApplyChanges(factory)` currently uses this path for
updates and does not call the factory.

Add an appropriate constructor, map the update manually, or use EF Core
`ApplyTo` when mutating a tracked entity is the intended behavior.

## The original model changed after applying

Core `ApplyChanges` creates a new object, but the copy is shallow. Unchanged
lists and other reference-type properties remain shared, so later mutation
through either model is visible through the other.

EF Core `ApplyTo` intentionally behaves differently: it mutates the update
entity stored in `Data`.

## Applying a parent with `CastAssoc` fails

`CastAssoc` stores `Changeset<TAssoc>` in the parent `Changes`. Core and EF
apply operations do not recursively materialize it.

Retrieve the child with `GetAssoc`, apply it explicitly, and remove the nested
changeset from the parent's changes before applying the parent. See
[Associations](associations.md#materialization-limitation).

## A uniqueness check passed but saving failed

This can happen when concurrent requests validate the same value. A uniqueness
validator is an early user-friendly check, not a concurrency guarantee.

Keep a database unique constraint and translate the provider-specific
`DbUpdateException`. See
[Enforce uniqueness safely](recipes.md#enforce-uniqueness-safely).

## `ValidateUnique` did not query the database

Uniqueness validation skips a field absent from `Changes`. In an update,
casting removes values equal to the existing value, so an unchanged unique
field does not trigger a query.

Also verify that the string field name identifies a mapped EF property.

## ASP.NET responses do not include error codes

`ToValidationErrors`, `ToValidationProblemOrNull`, `ToProblemDetails`, and
`AddToModelState` use messages. They do not expose changeset codes or metadata.

Return a DTO based on `Errors` or `ErrorMap` when clients need the richer
contract.

## A base error appears under an empty field

`AddBaseError` stores `Field` as `""`. `ErrorMap` and ASP.NET validation
dictionaries therefore use the empty-string key.

Map that key to an application convention before serialization if required.

## `[ChangesetTarget]` generates compiler errors

Generated models must be constructible with `new T()` and every included
property must be assignable from generated code. Check for:

- a missing parameterless constructor
- private, protected, or init-only setters
- inaccessible model types
- unsupported property declarations

Remove the attribute to use the reflection fallback or adjust the model.

## A misspelled string field has no analyzer warning

The analyzer only checks literal strings in collection expressions or array
initializers passed directly to `Changeset<T>.Cast`, and the target must have
`[ChangesetTarget]`.

It does not analyze validator strings, dynamic collections, variables, or
association names. Prefer property-expression overloads wherever possible.

## Verify the documentation locally

Install the pinned tooling once:

```shell
python3 -m venv .venv
.venv/bin/python -m pip install -r docs/requirements.txt
```

Build exactly as CI does:

```shell
.venv/bin/mkdocs build --strict
```

Strict mode fails on warnings such as invalid navigation or documentation
links.
