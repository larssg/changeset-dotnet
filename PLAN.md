# Changeset.NET — Implementation Plan

## 1. Package Name and Namespace

**Package:** `Changeset`
**Root namespace:** `Changeset`

Justification: Short, recognizable to anyone familiar with the Ecto concept, and not taken on NuGet. The name immediately communicates the core abstraction. Avoids `.NET` suffixes or `FluentX` naming conventions that would obscure the origin.

If `Changeset` is taken or too generic, fallback: `EctoNet` or `Changesets`.

Sub-namespaces:
- `Changeset` — core types (`Changeset<T>`, errors, casting)
- `Changeset.Validators` — built-in validators
- `Changeset.EntityFramework` — EF Core integration (separate package)
- `Changeset.Generators` — source generator (separate package)

---

## 2. Core Types

### `Changeset<T>` (sealed record class)

The central type. Represents a pending change to a model of type `T`.

```
Changeset<T>
├── T? Data              // the original model (null for inserts)
├── IReadOnlyDictionary<string, object?> Changes   // cast+validated field values
├── IReadOnlyList<ChangesetError> Errors            // accumulated errors
├── bool IsValid         // Errors.Count == 0
├── IReadOnlySet<string> CastFields                 // fields that were accepted via cast
├── IReadOnlyDictionary<string, object?> Params     // raw input params (before casting)
├── Action? Action       // enum: Insert | Update (inferred from whether Data is null)
```

### `ChangesetError` (record)

```
ChangesetError
├── string Field         // field name ("email", "address.city"), empty string for base errors
├── string Message       // human-readable ("can't be blank")
├── string Code          // machine-readable ("required", "invalid_format", "too_short")
├── IReadOnlyDictionary<string, object>? Metadata  // optional context (e.g. {"min": 3, "max": 50})
```

### `ChangesetAction` (enum)

```
Insert, Update
```

### `CastOptions` (record)

```
CastOptions
├── bool StrictCasting   // if true, unknown fields in params are errors (default: false)
├── IFormatProvider? FormatProvider  // culture for parsing numbers/dates
```

---

## 3. Casting

Casting converts untyped input into typed field values. It is the first step in building a changeset.

### API

```csharp
// From a dictionary (form data, JSON deserialized, etc.)
var cs = Changeset.Cast<User>(params, ["name", "email", "age"]);

// Update existing model
var cs = Changeset.Cast(existingUser, params, ["name", "email"]);

// With options
var cs = Changeset.Cast<User>(params, ["name", "email"], new CastOptions { StrictCasting = true });
```

`Cast` is a static factory method, not a constructor. It:

1. Accepts a `Dictionary<string, object?>` (or `IReadOnlyDictionary`) as the param source.
2. Takes a list of permitted field names (whitelist — fields not listed are silently dropped unless `StrictCasting` is true).
3. Matches field names to properties on `T` (case-insensitive by default, configurable).
4. For each permitted field present in params, attempts type coercion.

### Type Coercion Rules

| Source type | Target type | Behavior |
|---|---|---|
| Same type | Same type | Direct assignment |
| `string` | `int`, `long`, `decimal`, `double`, `float` | `TryParse` with `InvariantCulture` (or `CastOptions.FormatProvider`) |
| `string` | `bool` | `"true"`/`"false"` (case-insensitive), `"1"`/`"0"` |
| `string` | `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` | `TryParse` with round-trip format |
| `string` | `Guid` | `Guid.TryParse` |
| `string` | `Enum` | `Enum.TryParse` (case-insensitive) |
| `string` | `string` | Direct (with optional trim) |
| `long`/`int` | smaller numeric | checked narrowing, error on overflow |
| `JsonElement` | any above | extract value then apply above rules |
| Any | `Nullable<X>` | null → null, otherwise coerce to `X` |

**Cast failure:** does not throw. Adds a `ChangesetError` with code `"invalid_cast"` for that field, and the field is not included in `Changes`.

### Handling `JsonElement`

Since `System.Text.Json` deserializes unknown shapes to `JsonElement`, the caster will unwrap `JsonElement` values before applying coercion rules. This makes `Changeset.Cast` work naturally with `JsonSerializer.Deserialize<Dictionary<string, object?>>()`.

---

## 4. Validation Pipeline

### Design: Extension methods on `Changeset<T>`

Validators are pure functions: `Changeset<T> → Changeset<T>`. They are composed via method chaining on the changeset instance. This mirrors the Elixir pipe operator.

```csharp
var cs = Changeset.Cast<User>(params, ["name", "email", "age"])
    .ValidateRequired(["name", "email"])
    .ValidateFormat("email", @"^[^@]+@[^@]+\.[^@]+$")
    .ValidateLength("name", min: 2, max: 100)
    .ValidateNumber("age", greaterThanOrEqual: 0, lessThan: 150)
    .ValidateChange("email", UniqueEmailValidator);
```

Each validator method:
1. Checks if the field exists in `Changes` (if not, skips — validators only run on cast fields).
2. Checks if the field already has errors (configurable: skip or continue).
3. If validation fails, returns a **new** `Changeset<T>` with the error appended (immutable).
4. If validation passes, returns the same instance (no allocation).

### Built-in Validators

| Method | Purpose |
|---|---|
| `ValidateRequired(fields)` | field must be present in changes and not null/empty-string |
| `ValidateFormat(field, regex)` | string must match regex |
| `ValidateLength(field, min?, max?, is?)` | string/collection length bounds |
| `ValidateNumber(field, gt?, gte?, lt?, lte?, eq?)` | numeric range |
| `ValidateInclusion(field, values)` | value must be one of a set |
| `ValidateExclusion(field, values)` | value must not be one of a set |
| `ValidateConfirmation(field)` | `field` must equal `field_confirmation` in params |
| `ValidateChange(field, func)` | custom validator for a single field |
| `Validate(func)` | custom validator across the whole changeset |
| `ValidateAssoc(field, func)` | validate a nested/associated changeset |

### Custom Validators

```csharp
// Single-field custom validator
static Changeset<User> ValidateUniqueEmail(Changeset<User> cs, object? value)
{
    var email = (string)value!;
    if (db.Users.Any(u => u.Email == email))
        return cs.AddError("email", "has already been taken", "uniqueness");
    return cs;
}

// Whole-changeset custom validator
static Changeset<User> ValidatePasswordMatch(Changeset<User> cs)
{
    var pw = cs.GetChange<string>("password");
    var confirm = cs.GetChange<string>("password_confirmation");
    if (pw != confirm)
        return cs.AddError("password_confirmation", "does not match", "confirmation");
    return cs;
}
```

### Error Accumulation

Validators never throw. They always return a changeset. Multiple validators can add multiple errors to the same field. By default validators run even if the field already has errors (full error reporting). An opt-in `shortCircuit: true` parameter on individual validators skips if field already errored.

---

## 5. Error Representation

### `ChangesetError` (detailed above)

Errors are a flat list on the changeset. Field paths use dot-notation for nested objects: `"address.city"`, `"items[0].quantity"`.

### Convenience Methods

```csharp
cs.Errors                           // all errors
cs.ErrorsOn("email")                // errors for a specific field
cs.ErrorMap                         // Dictionary<string, List<ChangesetError>>
cs.HasErrorOn("email")              // bool
cs.BaseErrors                       // errors with Field == ""
```

### Serialization

`ChangesetError` is a record and serializes cleanly with `System.Text.Json`. The `ErrorMap` property produces JSON like:

```json
{
  "name": [{"message": "can't be blank", "code": "required"}],
  "email": [{"message": "has already been taken", "code": "uniqueness"}]
}
```

This shape maps directly to what frontend frameworks (React Hook Form, etc.) expect.

---

## 6. Applying Changes

A valid changeset can be materialized into a model instance.

### API

```csharp
// For inserts — creates a new T
if (cs.IsValid)
{
    User user = cs.ApplyChanges();
}

// For updates — applies changes to the existing model
if (cs.IsValid)
{
    User updated = cs.ApplyChanges(); // returns new instance with changes applied
}

// Pattern matching style
var result = cs.ToResult(); // Result<T, IReadOnlyList<ChangesetError>>
```

### Mechanics

`ApplyChanges()`:
- If `cs.Action == Insert`: creates a new `T` via parameterless constructor, sets properties from `Changes`.
- If `cs.Action == Update`: clones `Data` (via record `with` or shallow copy), applies `Changes` on top.
- Throws `InvalidOperationException` if `!cs.IsValid` (this is the one place we throw — you must check first).

### Result Type

Provide a `ToResult()` that returns a discriminated union-style result to avoid the throw:

```csharp
public ChangesetResult<T> ToResult()

// ChangesetResult<T> is:
public abstract record ChangesetResult<T>;
public record ValidResult<T>(T Value) : ChangesetResult<T>;
public record InvalidResult<T>(IReadOnlyList<ChangesetError> Errors) : ChangesetResult<T>;
```

Or simpler — return a `(T? Value, bool IsValid, IReadOnlyList<ChangesetError> Errors)` tuple. Prefer the result type for API clarity.

---

## 7. EF Core Integration

**Separate package:** `Changeset.EntityFramework`

### Applying to DbContext

```csharp
// Insert
if (cs.IsValid)
{
    var user = cs.ApplyChanges();
    dbContext.Users.Add(user);
    await dbContext.SaveChangesAsync();
}

// Update — track changes on an existing entity
await cs.ApplyToAsync(dbContext);
// Internally: finds the entity, sets modified properties, calls SaveChangesAsync
```

### Extension Methods

```csharp
// Apply changeset to a tracked entity
public static async Task ApplyToAsync<T>(this Changeset<T> changeset, DbContext context)
    where T : class
{
    // Attaches entity, marks only Changed fields as modified
}

// Unique constraint validation
public static Changeset<T> ValidateUnique<T>(
    this Changeset<T> changeset,
    string field,
    DbContext context) where T : class
{
    // Queries DB to check uniqueness
}
```

### ASP.NET Core Integration

```csharp
// Map changeset errors to ModelStateDictionary
public static void AddToModelState(this Changeset<T> changeset, ModelStateDictionary modelState)

// Map changeset errors to ProblemDetails
public static ValidationProblemDetails ToProblemDetails<T>(this Changeset<T> changeset)
```

---

## 8. Immutability Strategy

**`Changeset<T>` is a sealed record class.**

Rationale:
- **Record** gives value-equality semantics, `with` expressions for creating modified copies, and clean `ToString()`.
- **Class** (not struct) because changesets carry collections (errors, changes) — a struct would copy those references on every pass, and the value-type semantics would be misleading. Records are heap-allocated but immutable-by-convention which is what we want.
- **Sealed** for performance (devirtualization) and to prevent inheritance that could break invariants.

Every validator returns a new changeset instance (or the same instance if no error was added). The `Changes`, `Errors`, and `CastFields` collections use immutable types (`ImmutableDictionary`, `ImmutableList`, `ImmutableHashSet`) internally, exposed as `IReadOnly*` interfaces.

Tradeoff: allocation pressure on large validation pipelines. Mitigated by:
1. Returning `this` when no modification occurs (most validators pass).
2. Using `ImmutableList<T>.Builder` internally when batching multiple errors.

---

## 9. Source Generator Opportunities

**Separate package:** `Changeset.Generators`

### Field Registration

A source generator can inspect `T` and generate:
1. A compile-time list of valid field names — catches typos in `Cast(params, ["naem"])` at build time.
2. A strongly-typed cast method: `Changeset.Cast<User>(params, u => new { u.Name, u.Email })` backed by generated code.
3. A fast property setter that avoids reflection — the generator emits a switch over field names that directly sets properties.

### Approach

Use an incremental source generator (`IIncrementalGenerator`). Trigger on types annotated with `[Changeset]` attribute or types used as `T` in `Changeset<T>`.

Generated code:
```csharp
// Generated
internal static partial class UserChangesetExtensions
{
    public static readonly IReadOnlySet<string> ValidFields = new HashSet<string> { "Name", "Email", "Age" };

    public static User ApplyChanges(IReadOnlyDictionary<string, object?> changes, User? existing)
    {
        var target = existing ?? new User();
        if (changes.TryGetValue("Name", out var name)) target.Name = (string)name!;
        if (changes.TryGetValue("Email", out var email)) target.Email = (string)email!;
        if (changes.TryGetValue("Age", out var age)) target.Age = (int)age!;
        return target;
    }
}
```

This eliminates reflection entirely at runtime. The generator is optional — the library works without it via reflection as fallback.

### Priority

Source generator is a **v2 feature**. Ship the core library first using reflection, then add the generator as a performance/DX optimization.

---

## 10. Project Structure

```
Changeset.sln
├── src/
│   ├── Changeset/                          # Core library
│   │   ├── Changeset.csproj                # net8.0; net9.0
│   │   ├── Changeset.cs                    # Changeset<T> record
│   │   ├── ChangesetError.cs               # Error record
│   │   ├── ChangesetResult.cs              # Result type
│   │   ├── ChangesetAction.cs              # Enum
│   │   ├── CastOptions.cs                  # Casting configuration
│   │   ├── Casting/
│   │   │   ├── Caster.cs                   # Core casting logic
│   │   │   ├── TypeCoercion.cs             # Coercion rules registry
│   │   │   └── JsonElementUnwrapper.cs     # JsonElement handling
│   │   ├── Validators/
│   │   │   ├── RequiredValidator.cs
│   │   │   ├── FormatValidator.cs
│   │   │   ├── LengthValidator.cs
│   │   │   ├── NumberValidator.cs
│   │   │   ├── InclusionValidator.cs
│   │   │   ├── ExclusionValidator.cs
│   │   │   ├── ConfirmationValidator.cs
│   │   │   └── ValidatorExtensions.cs      # Extension method entry points
│   │   └── ChangesetExtensions.cs          # ApplyChanges, ToResult, helpers
│   │
│   ├── Changeset.EntityFramework/          # EF Core integration
│   │   ├── Changeset.EntityFramework.csproj
│   │   ├── DbContextExtensions.cs
│   │   └── AspNetCoreExtensions.cs
│   │
│   └── Changeset.Generators/              # Source generator (v2)
│       ├── Changeset.Generators.csproj
│       └── ChangesetGenerator.cs
│
├── test/
│   ├── Changeset.Tests/                    # Unit tests for core
│   │   ├── CastingTests.cs
│   │   ├── ValidatorTests.cs
│   │   ├── ChangesetTests.cs
│   │   ├── ErrorTests.cs
│   │   └── ApplyChangesTests.cs
│   │
│   ├── Changeset.EntityFramework.Tests/    # EF Core integration tests
│   │
│   └── Changeset.Generators.Tests/         # Generator snapshot tests (v2)
│
├── samples/
│   └── Changeset.Sample.WebApi/            # ASP.NET Core sample
│
├── PLAN.md
├── README.md
└── LICENSE
```

### Target Frameworks

- `Changeset`: `net8.0; net9.0`
- `Changeset.EntityFramework`: `net8.0; net9.0` (depends on `Microsoft.EntityFrameworkCore` 8.0+)
- Test projects: `net9.0`

---

## 11. Testing Strategy

### Casting Tests

- Cast string to int, decimal, bool, DateTime, Guid, enum — happy path
- Cast failure — wrong type, overflow, malformed string → error with code `"invalid_cast"`
- Null handling for nullable vs non-nullable target types
- Unpermitted fields silently dropped (and error when `StrictCasting` is true)
- Missing fields not in `Changes` but no error
- `JsonElement` unwrapping for each supported type
- Case-insensitive field matching

### Validator Tests

For each built-in validator:
- Happy path — valid value passes through, no errors added
- Failure — correct error field, message, code, metadata
- Skipped when field not in `Changes`
- Multiple validators on same field accumulate errors
- `ValidateRequired` catches null, empty string, whitespace

### Custom Validator Tests

- `ValidateChange` receives correct value
- `Validate` receives full changeset
- Custom validator can add multiple errors
- Custom validator returning changeset unchanged

### Changeset Lifecycle Tests

- Insert flow: `Cast` → validate → `ApplyChanges` creates new instance
- Update flow: `Cast(existing, params)` → validate → `ApplyChanges` returns modified copy
- `ApplyChanges` throws when invalid
- `ToResult` returns `ValidResult` or `InvalidResult`
- Error inspection: `ErrorsOn`, `ErrorMap`, `HasErrorOn`

### Immutability Tests

- Validators return new instance when error added
- Validators return same instance when no error
- Original changeset not mutated after validation

### Edge Cases

- Empty params
- Empty permitted fields list
- Model with no parameterless constructor (should fail clearly)
- Deeply nested field paths
- Concurrent validation (thread safety of immutable collections)

---

## 12. Open Questions

### Q1: Field Name Representation — Strings vs. Expressions

Elixir uses atoms (`:email`). In C# we can use:
- **A) Strings** — `Cast(params, ["name", "email"])` — simple, matches Ecto closely, but no compile-time safety.
- **B) Expressions** — `Cast(params, x => new { x.Name, x.Email })` — compile-time safe, but more complex API, allocates expression trees.
- **C) `nameof` helper** — `Cast(params, [nameof(User.Name)])` — compile-time safe for simple cases, verbose.

**Recommendation:** Start with strings (A) for simplicity. Add expression overloads (B) as convenience. Source generator (v2) eliminates the typo risk for string-based API.

### Q2: Async Validators

Some validators need I/O (e.g. uniqueness check against DB). Options:
- **A) Sync only** — caller wraps async in `.Result` or validates after async pre-check.
- **B) `ValidateAsync` methods** — return `Task<Changeset<T>>`, breaks the fluent chain.
- **C) Two-phase** — sync validators via fluent chain, async validators via `await cs.ValidateAsync(...)` at the end.

**Recommendation:** C — keep the sync chain clean, provide `ValidateAsync` and `ValidateChangeAsync` for I/O-bound validators. The async methods return `Task<Changeset<T>>` and can be chained with `await`.

### Q3: Nested Changesets

For models with nested objects (e.g. `User` has `Address`):
- **A) Flat** — `Cast(params, ["address.city"])` with dot-notation.
- **B) Nested changeset** — `CastAssoc("address", addressParams, ["city", "zip"])` returning a `Changeset<Address>` embedded in the parent.

**Recommendation:** B — mirrors Ecto's `cast_assoc`. The parent changeset holds child changesets. Errors propagate with dot-notation paths. Implement in v1 but keep simple (single level of nesting).

### Q4: Changeset Without a Model Type

Sometimes you want to validate params without a backing model (e.g. search filters, login credentials):
- **A) Require a model always** — create DTOs/records for everything.
- **B) `Changeset.Cast(params, fields)` untyped** — returns `Changeset<Dictionary<string, object?>>` or a non-generic `Changeset`.

**Recommendation:** A for v1. Creating a record for the shape is cheap in modern C# and gives type safety. Revisit schemaless changesets in v2 if there's demand.

### Q5: Dependency on `System.Collections.Immutable`

Using `ImmutableDictionary`/`ImmutableList` adds a dependency. Options:
- **A) Use them** — correct immutability guarantees, well-tested.
- **B) Use frozen collections** (.NET 8+) — `FrozenDictionary` is faster for reads but slower to create.
- **C) Internal read-only wrappers** — avoid the dependency, but less robust.

**Recommendation:** A — `System.Collections.Immutable` is part of the .NET SDK, not an extra NuGet dependency. Use `ImmutableArray<ChangesetError>` for the error list (struct-based, cache-friendly).

### Q6: Should `ApplyChanges` Use Reflection or Require `T : new()`

- **A) `new()` constraint** — `ApplyChanges` calls `new T()` then sets properties.
- **B) Reflection only** — `Activator.CreateInstance`, no constraint.
- **C) Factory function** — `ApplyChanges(Func<T> factory)` overload.

**Recommendation:** A with C as overload. The `new()` constraint is the common case and enables the source generator path. The factory overload handles types without parameterless constructors.
