using Changeset;
using ChangesetFactory = Changeset.Changeset;

namespace Changeset.Generators.Tests;

[ChangesetTarget]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
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
        var cs = ChangesetFactory.Cast<Product>(
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
        var cs = ChangesetFactory.Cast(existing,
            new Dictionary<string, object?> { ["Price"] = 15m },
            ["Price"]);

        var updated = cs.ApplyChanges();

        Assert.NotSame(existing, updated);
        Assert.Equal(5, updated.Id);
        Assert.Equal("Old", updated.Name);
        Assert.Equal(15m, updated.Price);
        Assert.Equal(20, updated.Stock);
    }
}
