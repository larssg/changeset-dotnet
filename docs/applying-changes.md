# Applying Changes

Applying is the explicit boundary where a valid changeset becomes a model.
The core library does not modify a model while casting or validating.

## Apply an insert

For a type with a public parameterless constructor:

```csharp
if (!changeset.IsValid)
    return;

User user = changeset.ApplyChanges();
```

An insert creates a new `T`, then assigns every value in `Changes` to its
corresponding public writable property. Properties absent from `Changes` retain
their constructor or initializer defaults.

## Apply an update

An update produces a new object:

```csharp
var changeset = Changeset<User>.Cast(
    existing,
    parameters,
    user => new { user.Name, user.Email });

User updated = changeset.ApplyChanges();

ReferenceEquals(existing, updated); // false
```

The reflection-based path creates a new `T`, shallow-copies its public readable
and writable properties from `Data`, then assigns the changed values. The
existing object is not mutated.

Because the copy is shallow, reference-type property values that were not
changed are shared:

```csharp
ReferenceEquals(existing.Tags, updated.Tags); // true when Tags was unchanged
```

Use your own mapping or cloning strategy if the application requires a deep
copy.

## Types without a parameterless constructor

For an insert, provide a factory:

```csharp
Order order = changeset.ApplyChanges(
    () => new Order(currentCustomerId));
```

The factory creates the initial target, after which changes are assigned.

!!! warning

    On an update, the current implementation shallow-clones `Data` and does not
    call the factory. The model still needs a parameterless constructor for the
    reflection-based update path.

## Invalid changesets

`ApplyChanges` rejects an invalid changeset:

```csharp
if (!changeset.IsValid)
{
    // Return or display changeset.Errors.
    return;
}

var user = changeset.ApplyChanges();
```

Calling it without the check throws `InvalidOperationException`. Input failures
are represented as errors; attempting to materialize known-invalid input is a
programmer error.

## Use `ToResult`

`ToResult` combines validity checking and applying:

```csharp
ChangesetResult<User> result = changeset.ToResult();

switch (result)
{
    case ChangesetResult<User>.Valid(var user):
        Save(user);
        break;

    case ChangesetResult<User>.Invalid(var errors):
        Display(errors);
        break;
}
```

The invalid result contains the flat `ImmutableArray<ChangesetError>`.
`ToResult(factory)` supports insert models without a parameterless constructor
under the same rules as `ApplyChanges(factory)`.

## Reflection and generated appliers

By default, the core library uses cached reflection metadata to create, copy,
and assign models. When `Changeset.Generators` has registered an applier for
`T`, `ApplyChanges()` uses generated code instead:

```csharp
[ChangesetTarget]
public class Product
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

Both paths create a new object for core updates. The generated path avoids
reflection but still performs a shallow property copy and requires the
generated model to be constructible with `new T()`.

See [Source Generator](source-generator.md) for supported models and
installation.

## EF Core behaves differently

`ApplyTo` is designed for persistence and mutates the update entity held in
`Data`, marking changed properties as modified in the `DbContext`. It does not
use the copy-on-apply behavior described above.

See [EF Core & ASP.NET Core](ef-core.md#apply-an-update) before choosing between
`ApplyChanges` and `ApplyTo`.

## Nested changesets

The core apply operation recursively materializes a `Changeset<TAssoc>` stored
by `CastAssoc`. See [Associations](associations.md#materialization).
