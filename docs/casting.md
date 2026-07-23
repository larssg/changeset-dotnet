# Casting

Casting is an allowlist and conversion step. It selects accepted input fields,
maps them to public writable properties on `T`, and stores successfully coerced
values in `Changes`.

## Insert and update

Create an insert changeset without existing data:

```csharp
var changeset = Changeset<User>.Cast(
    parameters,
    user => new { user.Name, user.Email, user.Age });
```

Create an update changeset by passing the existing model:

```csharp
var changeset = Changeset<User>.Cast(
    existingUser,
    parameters,
    user => new { user.Name, user.Email });
```

During updates, a converted value equal to the current property value is
omitted from `Changes` and `CastFields`.

## Select permitted fields

Property expressions are the preferred option for fields known at compile time:

```csharp
var changeset = Changeset<User>.Cast(
    parameters,
    user => new { user.Name, user.Email });
```

The expression must contain direct property accesses. A single direct property
is also accepted:

```csharp
var changeset = Changeset<User>.Cast(parameters, user => user.Email);
```

Use string lists when the accepted fields are selected dynamically:

```csharp
IReadOnlyList<string> permitted = GetPermittedFieldsForCurrentOperation();
var changeset = Changeset<User>.Cast(parameters, permitted);
```

Unknown permitted property names produce an `unknown_field` error when the
corresponding input key is present.

## Unpermitted input

By default, input keys not in the permitted list are silently ignored:

```csharp
var parameters = new Dictionary<string, object?>
{
    ["Name"] = "Ada",
    ["IsAdmin"] = true
};

var changeset = Changeset<User>.Cast(parameters, user => user.Name);

changeset.Changes.ContainsKey("IsAdmin"); // false
```

This is useful for mass-assignment protection. Enable strict casting when an
unexpected key should instead be reported:

```csharp
var options = new CastOptions { StrictCasting = true };
var changeset = Changeset<User>.Cast(
    parameters,
    user => user.Name,
    options);
```

Every unexpected key receives:

```text
message: is not permitted
code:    unpermitted_field
```

## Conversion matrix

Values already assignable to the target type pass through unchanged. Additional
coercions include:

| Input | Supported targets |
|---|---|
| `string` | `string`, integral numeric types, `decimal`, `double`, `float`, `bool`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `Guid`, enums |
| Numeric CLR value | Other numeric CLR types |
| JSON number | `int`, `long`, `decimal`, `double`, `float`, plus compatible string parsing |
| JSON string | `string`, `Guid`, date/time types, enums, booleans, and numeric types |
| JSON boolean | `bool` |
| JSON `null` | Nullable value types and reference types |

Boolean strings accept `true`, `false`, `1`, and `0`, ignoring letter case.
Enum strings are also matched case-insensitively.

Numeric conversions are checked by the runtime conversion APIs. Overflow
becomes an `invalid_cast` error rather than escaping as `OverflowException`.
Nested JSON objects are not automatically mapped to flat model properties; use
nested changesets for associations.

## Null and empty strings

`null` can be assigned to nullable value types and reference types. It is
invalid for non-nullable value types.

An empty string is still a string value. It does not become `null`, so casting
`""` to `int?`, `Guid?`, or `DateTime?` fails. Use input normalization before
casting if your application treats empty strings as null.

## Cast options

```csharp
var options = new CastOptions
{
    TrimStrings = true,
    CaseInsensitiveFields = true,
    StrictCasting = false,
    FormatProvider = CultureInfo.InvariantCulture
};
```

| Option | Default | Effect |
|---|---:|---|
| `TrimStrings` | `true` | Trims raw CLR string values before coercion |
| `CaseInsensitiveFields` | `true` | Matches input keys, permitted names, and model properties without case sensitivity |
| `StrictCasting` | `false` | Adds an error for each input key that is not permitted |
| `FormatProvider` | invariant culture | Controls parsing of CLR string numbers and date/time values |

Use a culture explicitly when accepting culture-specific text:

```csharp
var options = new CastOptions
{
    FormatProvider = CultureInfo.GetCultureInfo("nb-NO")
};
```

JSON numeric tokens use `System.Text.Json` numeric readers; the format provider
primarily affects values represented as strings.

## Cast errors

A failed conversion adds an error to the target property:

```csharp
var changeset = Changeset<User>.Cast(
    new Dictionary<string, object?> { ["Age"] = "old" },
    user => user.Age);

var error = changeset.ErrorsOn("Age").Single();

error.Code;                         // "invalid_cast"
error.Metadata?["expected_type"];   // "Int32"
error.Metadata?["received_value"];  // "old"
```

Other permitted fields continue to cast, so one failure does not hide unrelated
input problems.

## Working with JSON

Deserializing to `Dictionary<string, object?>` commonly leaves values as
`JsonElement`. The caster understands scalar JSON elements:

```csharp
using System.Text.Json;

var parameters =
    JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
    ?? new Dictionary<string, object?>();

var changeset = Changeset<User>.Cast(
    parameters,
    user => new { user.Name, user.Age });
```

For a nested JSON object, use `CastAssoc`; see
[Associations](associations.md).
