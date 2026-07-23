# Casting

Casting converts untyped `Dictionary<string, object?>` input into typed field values. Only permitted fields are accepted — everything else is silently dropped (or errors with `StrictCasting`).

```csharp
// Insert — creates a new changeset
var cs = Changeset<User>.Cast(params, ["Name", "Email", "Age"]);

// Update — only includes fields that actually changed
var cs = Changeset<User>.Cast(existingUser, params, ["Name", "Email"]);

// Expression syntax — compile-time safe field names
var cs = Changeset<User>.Cast(params, u => new { u.Name, u.Email });
```

## Type coercion

Type coercion handles:

- string-to-number
- string-to-bool
- string-to-DateTime
- `JsonElement` unwrapping
- nullable types
- checked numeric narrowing

Cast failures produce errors, never exceptions.

## Cast options

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
