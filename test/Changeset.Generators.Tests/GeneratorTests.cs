using Changeset;

namespace Changeset.Generators.Tests;

[ChangesetTarget]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

[ChangesetTarget]
public record ProductRecord
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
}

public class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

[ChangesetTarget]
public class DerivedEntity : BaseEntity
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

[ChangesetTarget]
public class WithStaticProps
{
    public static int Counter { get; set; }
    public static string DefaultName { get; set; } = "default";
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class GeneratorTests
{
    [Fact]
    public void GeneratedApplier_IsRegistered()
    {
        var applier = ChangesetApplierRegistry.Get<Product>();
        Assert.NotNull(applier);
    }

    [Fact]
    public void GeneratedApplier_ValidFields_ContainsAllProperties()
    {
        var applier = ChangesetApplierRegistry.Get<Product>()!;
        Assert.Contains("Id", applier.ValidFields);
        Assert.Contains("Name", applier.ValidFields);
        Assert.Contains("Price", applier.ValidFields);
        Assert.Contains("Stock", applier.ValidFields);
    }

    [Fact]
    public void GeneratedApplier_Create_SetsProperties()
    {
        var applier = ChangesetApplierRegistry.Get<Product>()!;
        var changes = new Dictionary<string, object?>
        {
            ["Name"] = "Widget",
            ["Price"] = 9.99m,
            ["Stock"] = 100
        };

        var product = applier.Create(changes);

        Assert.Equal("Widget", product.Name);
        Assert.Equal(9.99m, product.Price);
        Assert.Equal(100, product.Stock);
    }

    [Fact]
    public void GeneratedApplier_Apply_ClonesAndAppliesChanges()
    {
        var applier = ChangesetApplierRegistry.Get<Product>()!;
        var source = new Product { Id = 1, Name = "Old", Price = 5.00m, Stock = 50 };
        var changes = new Dictionary<string, object?> { ["Name"] = "New" };

        var result = applier.Apply(source, changes);

        Assert.NotSame(source, result);
        Assert.Equal("New", result.Name);
        Assert.Equal(1, result.Id);
        Assert.Equal(5.00m, result.Price);
        Assert.Equal(50, result.Stock);
    }

    [Fact]
    public void ApplyChanges_UsesGeneratedApplier()
    {
        // The generated applier is auto-registered via module initializer,
        // so ApplyChanges should use it instead of reflection
        var cs = Changeset<Product>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Gadget", ["Price"] = 19.99m },
            ["Name", "Price"]);

        var product = cs.ApplyChanges();

        Assert.Equal("Gadget", product.Name);
        Assert.Equal(19.99m, product.Price);
    }

    [Fact]
    public void ApplyChanges_Update_UsesGeneratedApplier()
    {
        var existing = new Product { Id = 5, Name = "Old", Price = 10m, Stock = 20 };
        var cs = Changeset<Product>.Cast(existing,
            new Dictionary<string, object?> { ["Price"] = 15m },
            ["Price"]);

        var updated = cs.ApplyChanges();

        Assert.NotSame(existing, updated);
        Assert.Equal(5, updated.Id);
        Assert.Equal("Old", updated.Name);
        Assert.Equal(15m, updated.Price);
        Assert.Equal(20, updated.Stock);
    }

    [Fact]
    public void RecordType_IsRegistered()
    {
        var applier = ChangesetApplierRegistry.Get<ProductRecord>();
        Assert.NotNull(applier);
    }

    [Fact]
    public void RecordType_Create_SetsProperties()
    {
        var applier = ChangesetApplierRegistry.Get<ProductRecord>()!;
        var changes = new Dictionary<string, object?>
        {
            ["Sku"] = "ABC-123",
            ["Price"] = 29.99m
        };

        var record = applier.Create(changes);

        Assert.Equal("ABC-123", record.Sku);
        Assert.Equal(29.99m, record.Price);
    }

    [Fact]
    public void RecordType_Apply_ClonesAndAppliesChanges()
    {
        var applier = ChangesetApplierRegistry.Get<ProductRecord>()!;
        var source = new ProductRecord { Id = 1, Sku = "OLD", Price = 5.00m };
        var changes = new Dictionary<string, object?> { ["Sku"] = "NEW" };

        var result = applier.Apply(source, changes);

        Assert.NotSame(source, result);
        Assert.Equal("NEW", result.Sku);
        Assert.Equal(1, result.Id);
        Assert.Equal(5.00m, result.Price);
    }

    [Fact]
    public void InheritedProperties_AreIncludedInValidFields()
    {
        var applier = ChangesetApplierRegistry.Get<DerivedEntity>()!;

        Assert.Contains("Id", applier.ValidFields);
        Assert.Contains("CreatedAt", applier.ValidFields);
        Assert.Contains("Name", applier.ValidFields);
        Assert.Contains("Description", applier.ValidFields);
    }

    [Fact]
    public void InheritedProperties_Create_SetsBaseAndDerivedProperties()
    {
        var applier = ChangesetApplierRegistry.Get<DerivedEntity>()!;
        var now = DateTime.UtcNow;
        var changes = new Dictionary<string, object?>
        {
            ["Id"] = 42,
            ["CreatedAt"] = now,
            ["Name"] = "Test",
            ["Description"] = "A test entity"
        };

        var entity = applier.Create(changes);

        Assert.Equal(42, entity.Id);
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal("Test", entity.Name);
        Assert.Equal("A test entity", entity.Description);
    }

    [Fact]
    public void InheritedProperties_Apply_CopiesBaseAndDerivedProperties()
    {
        var applier = ChangesetApplierRegistry.Get<DerivedEntity>()!;
        var now = DateTime.UtcNow;
        var source = new DerivedEntity { Id = 1, CreatedAt = now, Name = "Old", Description = "Old desc" };
        var changes = new Dictionary<string, object?> { ["Name"] = "New" };

        var result = applier.Apply(source, changes);

        Assert.NotSame(source, result);
        Assert.Equal(1, result.Id);
        Assert.Equal(now, result.CreatedAt);
        Assert.Equal("New", result.Name);
        Assert.Equal("Old desc", result.Description);
    }

    [Fact]
    public void StaticProperties_AreExcludedFromValidFields()
    {
        var applier = ChangesetApplierRegistry.Get<WithStaticProps>()!;

        Assert.DoesNotContain("Counter", applier.ValidFields);
        Assert.DoesNotContain("DefaultName", applier.ValidFields);
        Assert.Contains("Id", applier.ValidFields);
        Assert.Contains("Name", applier.ValidFields);
        Assert.Equal(2, applier.ValidFields.Count);
    }

    [Fact]
    public void StaticProperties_Create_OnlySetsInstanceProperties()
    {
        var applier = ChangesetApplierRegistry.Get<WithStaticProps>()!;
        var changes = new Dictionary<string, object?>
        {
            ["Id"] = 10,
            ["Name"] = "Test"
        };

        var result = applier.Create(changes);

        Assert.Equal(10, result.Id);
        Assert.Equal("Test", result.Name);
    }
}
