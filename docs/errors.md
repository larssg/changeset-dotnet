# Errors

Casting and validation failures are stored as `ChangesetError` values. Errors
accumulate, so one changeset can report several problems and several problems
for the same field.

## Error structure

Each error contains:

| Member | Purpose |
|---|---|
| `Field` | Model or input field associated with the failure |
| `Message` | Human-readable description |
| `Code` | Stable, machine-readable category |
| `Metadata` | Optional structured context such as a regex or expected type |

For example, a failed integer cast produces an error similar to:

```csharp
new ChangesetError(
    Field: "Age",
    Message: "is invalid",
    Code: "invalid_cast",
    Metadata: new Dictionary<string, object>
    {
        ["expected_type"] = "Int32",
        ["received_value"] = "old"
    }.ToImmutableDictionary());
```

Applications should use `Code` for program logic or localization and treat
`Message` as display text.

## Inspect errors

```csharp
changeset.IsValid;             // true exactly when Errors is empty
changeset.Errors;              // all errors in accumulation order
changeset.ErrorsOn("Email");   // errors whose Field is exactly "Email"
changeset.HasErrorOn("Email"); // whether that exact field has an error
changeset.BaseErrors;          // errors not attached to a field
changeset.ErrorMap;            // errors grouped by exact field
```

`ErrorsOn` and `HasErrorOn` use exact, case-sensitive field equality. Nested
association errors use dotted fields such as `Address.City` and can be queried
with that complete name.

## Add application errors

Custom validators can add field errors:

```csharp
var invalid = changeset.AddError(
    "Email",
    "has already been registered",
    "uniqueness");
```

Attach metadata when clients need structured context:

```csharp
var metadata = new Dictionary<string, object>
{
    ["minimum_age"] = 18
}.ToImmutableDictionary();

var invalid = changeset.AddError(
    "Age",
    "must be at least 18",
    "minimum_age",
    metadata);
```

Both methods return a new changeset and leave the original unchanged.

## Base errors

Some rules apply to the operation rather than one property:

```csharp
var invalid = changeset.AddBaseError(
    "the account cannot be changed in its current state",
    "account_locked");
```

A base error uses an empty string for `Field`. Consequently, `ErrorMap` and the
ASP.NET Core validation dictionary expose base errors under the `""` key:

```csharp
invalid.BaseErrors;
invalid.ErrorMap[""];
```

Choose an application-level convention if your API requires a different key,
such as `"$"` or `"general"`.

## Error codes

Built-in operations currently use these codes:

| Code | Produced by |
|---|---|
| `unpermitted_field` | Strict casting of an input key outside the allowlist |
| `unknown_field` | A permitted name that does not identify a writable property |
| `invalid_cast` | Type coercion failure |
| `required` | `ValidateRequired` |
| `format` | `ValidateFormat` |
| `length` | `ValidateLength` |
| `number` | `ValidateNumber` |
| `inclusion` | `ValidateInclusion` |
| `exclusion` | `ValidateExclusion` |
| `confirmation` | `ValidateConfirmation` |
| `invalid_assoc` | Unsupported nested association input |
| `uniqueness` | EF Core uniqueness validation |

Custom validators are free to define application-specific codes.

## Metadata

Metadata is currently supplied by these built-in failures:

| Error | Metadata |
|---|---|
| `invalid_cast` | `expected_type`, `received_value` |
| `format` | `pattern` |
| `length` | Configured `min`, `max`, and `is` values |

Metadata is optional, so consumers must handle `null`.

## Serialization

`ErrorMap` groups full `ChangesetError` records and can be serialized directly:

```csharp
return Results.Json(changeset.ErrorMap);
```

The precise JSON property naming depends on the serializer configuration. With
the ASP.NET Core web defaults, the shape is conceptually:

```json
{
  "Name": [
    {
      "field": "Name",
      "message": "can't be blank",
      "code": "required",
      "metadata": null
    }
  ]
}
```

If clients only need messages, the integration package provides:

```csharp
IDictionary<string, string[]> errors = changeset.ToValidationErrors();
```

This discards codes and metadata and produces the shape expected by
`Results.ValidationProblem`.

See [EF Core & ASP.NET Core](ef-core.md#aspnet-core) for response helpers and
[Associations](associations.md) for nested error propagation.
