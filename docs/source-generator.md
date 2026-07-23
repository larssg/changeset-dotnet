# Source Generator

The `Changeset.Generators` package provides reflection-free `ApplyChanges` and build-time field name validation.

```shell
dotnet add package Changeset.Generators
```

Add `[ChangesetTarget]` to your models:

```csharp
[ChangesetTarget]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

The generator emits a switch-based property setter (no reflection at runtime) and registers it automatically via module initializer.

## Analyzer diagnostics

The package also includes a diagnostic analyzer for field names used in `Cast` and validators:

| ID | Diagnostic |
|---|---|
| `CHGSET001` | Field name typo with "did you mean?" suggestion |
| `CHGSET002` | Completely unknown field name |
