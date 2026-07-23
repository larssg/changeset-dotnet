# Source Generator

`Changeset.Generators` provides two related build-time features:

1. a generated property applier used by the core `ApplyChanges()` method
2. analyzer diagnostics for literal string field lists passed to `Cast`

```shell
dotnet add package Changeset.Generators
```

The package is a Roslyn component targeting `netstandard2.0`. The generated
application code runs in your project; there is no generator API to call at
runtime.

## Generate an applier

Mark a model with `[ChangesetTarget]`:

```csharp
using Changeset;

[ChangesetTarget]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

The generator emits an internal `IChangesetApplier<Product>` implementation.
A module initializer registers its singleton with
`ChangesetApplierRegistry`, so ordinary application code remains unchanged:

```csharp
var changeset = Changeset<Product>.Cast(
    parameters,
    product => new { product.Name, product.Price });

Product product = changeset.ApplyChanges();
```

When a registered applier exists, `ApplyChanges()` uses it in preference to the
reflection fallback.

## Generated behavior

For an insert, the generated applier:

1. creates a new model
2. loops over `Changes`
3. assigns matching properties through a generated `switch`

For an update, it:

1. creates a new model
2. shallow-copies all included properties from the existing instance
3. assigns the changed values

The original update instance is not mutated. Reference-type property values
remain shared when they are copied and not replaced.

## Supported models

The generator recognizes classes and records decorated with
`[ChangesetTarget]`. It includes public, writable, non-static instance
properties declared on the type and its base classes.

Models must:

- be accessible to the generated code
- be constructible with a parameterless `new T()`
- have assignable public setters for generated properties

Static and read-only properties are excluded. When a derived property hides an
inherited property with the same name, the most-derived property wins.

!!! note

    The attribute itself does not enforce these construction requirements.
    Incompatible models surface ordinary compiler errors in generated code.

## What is generated

Conceptually, the generated setter resembles:

```csharp
switch (field)
{
    case "Name":
        target.Name = (string)value!;
        break;
    case "Price":
        target.Price = (decimal)value!;
        break;
}
```

Casting still performs allowlisting and type coercion before this code runs.
The generated cast is therefore expected to succeed for values originating in
`Changes`.

The applier also exposes its property names through `ValidFields` and registers
itself at module initialization.

## Analyzer diagnostics

The analyzer checks literal strings in collection expressions and array
initializers passed to `Changeset<T>.Cast` when `T` has
`[ChangesetTarget]`.

```csharp
[ChangesetTarget]
public class Product
{
    public string Name { get; set; } = "";
}

var changeset = Changeset<Product>.Cast(
    parameters,
    ["Naem"]);
```

| ID | Severity | Meaning |
|---|---|---|
| `CHGSET001` | Warning | The name is invalid and a property within edit distance 3 is suggested |
| `CHGSET002` | Warning | The name is invalid and no close suggestion was found |

`"Naem"` produces a `CHGSET001` suggestion for `"Name"`. A completely unrelated
name such as `"FooBar"` produces `CHGSET002`.

Case-only differences do not produce a diagnostic because casting is
case-insensitive by default.

## Analyzer scope

The current analyzer is deliberately narrow. It does not inspect:

- property-expression overloads, which the C# compiler already checks
- string arguments passed to validators
- dynamically constructed field collections
- arbitrary string variables
- association field names

It analyzes `Cast` calls and literal string elements only. Runtime casting
continues to report `unknown_field` if an invalid permitted name reaches it.

The generator includes inherited properties, while the analyzer currently
collects properties directly exposed by the analyzed target symbol. Verify
string-based inherited fields in your build if your model hierarchy relies on
them.

## Prefer expressions when possible

Property expressions provide the broadest compile-time protection:

```csharp
var changeset = Changeset<Product>.Cast(
    parameters,
    product => new { product.Name, product.Price });
```

Use string lists when fields are selected dynamically or when an API boundary
already represents them as names. The analyzer adds protection for the common
literal-list case.

## Troubleshooting

### `ApplyChanges` still appears to use reflection

Check that:

- the generator package is referenced by the consuming project
- the model has `[ChangesetTarget]`
- generated files are enabled for inspection if you need to diagnose output
- the model compiled without generator errors

You can verify registration at runtime:

```csharp
var applier = ChangesetApplierRegistry.Get<Product>();
Debug.Assert(applier is not null);
```

### A string typo produces no diagnostic

Confirm that the target model has `[ChangesetTarget]` and that the field is a
literal inside a collection expression or array initializer passed directly to
`Changeset<T>.Cast`.

### A generated model fails to compile

Check for a missing parameterless constructor or a property that cannot be
assigned from generated code. Remove `[ChangesetTarget]` to use the reflection
path where appropriate, or adjust the model to satisfy the generated applier's
requirements.
