# The Changeset Lifecycle

A `Changeset<T>` is an immutable description of an attempted insert or update.
It contains the original input, successfully converted changes, and any errors
found along the way.

## The four stages

```text
untrusted input
      |
      v
    Cast  -----> cast errors
      |
      v
  Validate ----> validation errors
      |
      v
   Inspect
      |
      v
Apply / ToResult
```

Each validation call returns either the same instance or a copied record with
additional errors. Your model remains untouched until the final apply step.

## Changeset state

| Member | Meaning |
|---|---|
| `Data` | The existing model for an update, or `null` for an insert |
| `Params` | A snapshot of the original input dictionary |
| `Changes` | Successfully cast values that differ from the existing model |
| `CastFields` | Property names represented in `Changes` after casting |
| `Errors` | Cast and validation errors accumulated so far |
| `Action` | `ChangesetAction.Insert` or `ChangesetAction.Update` |
| `IsValid` | `true` exactly when `Errors` is empty |

For nested associations, a value in `Changes` can itself be a child changeset.

## Insert versus update

An insert starts without existing data:

```csharp
var insert = Changeset<User>.Cast(parameters, user => new { user.Name });

insert.Action == ChangesetAction.Insert;
insert.Data is null;
```

An update holds the existing object and drops successfully cast values that are
equal to the current property value:

```csharp
var update = Changeset<User>.Cast(
    existing,
    parameters,
    user => new { user.Name });

update.Action == ChangesetAction.Update;
ReferenceEquals(update.Data, existing);
```

This lets validators and EF Core integration focus on actual changes.

## Immutability

Changesets are records with immutable collections. Adding an error produces a
new logical value:

```csharp
var invalid = changeset.AddError(
    "Email",
    "is already registered",
    "uniqueness");

changeset.IsValid; // remains unchanged
invalid.IsValid;   // false
```

The `Data` object itself is not deep-cloned when the changeset is created.
Treat the existing model and values stored in `Params` as application-owned
objects; changeset immutability does not recursively freeze them.

## Failures as data

Bad user input produces errors with a field, message, stable code, and optional
metadata:

```csharp
foreach (var error in changeset.Errors)
{
    Console.WriteLine(error.Field);
    Console.WriteLine(error.Message);
    Console.WriteLine(error.Code);
    Console.WriteLine(error.Metadata);
}
```

This applies to conversion and validation failures. API misuse can still throw:
invalid field-selector expressions throw `ArgumentException`, null arguments
can throw `ArgumentNullException`, and applying an invalid changeset throws
`InvalidOperationException`.

## Final outcomes

Use `IsValid` when ordinary control flow is clearest:

```csharp
if (!changeset.IsValid)
    return errors;

var model = changeset.ApplyChanges();
```

Use `ToResult` when callers benefit from an explicit discriminated result:

```csharp
return changeset.ToResult() switch
{
    ChangesetResult<User>.Valid valid => Save(valid.Value),
    ChangesetResult<User>.Invalid invalid => Reject(invalid.Errors),
    _ => throw new UnreachableException()
};
```

Read [Errors](errors.md) for inspection helpers and
[Applying Changes](applying-changes.md) for materialization semantics.
