using Changeset;
using Changeset.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Changeset.EntityFramework.Tests;

public class TestUser
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
}

public class TestAddress
{
    public int Id { get; set; }
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public class TestUserWithAddress
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? AddressId { get; set; }
    public TestAddress? Address { get; set; }
}

public class TestDbContext : DbContext
{
    public DbSet<TestUser> Users => Set<TestUser>();
    public DbSet<TestUserWithAddress> UsersWithAddress => Set<TestUserWithAddress>();
    public DbSet<TestAddress> Addresses => Set<TestAddress>();

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
}

public class DbContextExtensionsTests : IDisposable
{
    private readonly TestDbContext _db;

    public DbContextExtensionsTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void ApplyTo_Insert_AddsEntityToContext()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = "alice@test.com", ["Age"] = 30 },
            ["Name", "Email", "Age"]);

        var entity = cs.ApplyTo(_db);

        Assert.Equal(EntityState.Added, _db.Entry(entity).State);
        Assert.Equal("Alice", entity.Name);
        Assert.Equal("alice@test.com", entity.Email);
        Assert.Equal(30, entity.Age);
    }

    [Fact]
    public async Task ApplyToAsync_Insert_SavesEntity()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Bob", ["Email"] = "bob@test.com" },
            ["Name", "Email"]);

        var entity = await cs.ApplyToAsync(_db);

        Assert.Single(_db.Users);
    }

    [Fact]
    public void ApplyTo_Update_MarksOnlyChangedFieldsAsModified()
    {
        var existing = new TestUser { Id = 1, Name = "Old", Email = "old@test.com", Age = 25 };
        _db.Users.Add(existing);
        _db.SaveChanges();
        _db.Entry(existing).State = EntityState.Unchanged;

        var cs = Changeset<TestUser>.Cast(existing,
            new Dictionary<string, object?> { ["Name"] = "New" },
            ["Name", "Email"]);

        cs.ApplyTo(_db);

        var entry = _db.Entry(existing);
        Assert.True(entry.Property("Name").IsModified);
        Assert.False(entry.Property("Email").IsModified);
        Assert.False(entry.Property("Age").IsModified);
        Assert.Equal("New", existing.Name);
    }

    [Fact]
    public void ApplyTo_InvalidChangeset_Throws()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "X" },
            ["Name"]);
        cs = cs.AddError("Name", "too short", "length");

        Assert.Throws<InvalidOperationException>(() => cs.ApplyTo(_db));
    }

    [Fact]
    public void ValidateUnique_NoConflict_Passes()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com" });
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "bob@test.com" },
            ["Email"]);

        var result = cs.ValidateUnique(u => u.Email, _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateUnique_Conflict_AddsError()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com" });
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        var result = cs.ValidateUnique("Email", _db);
        Assert.False(result.IsValid);
        Assert.Equal("uniqueness", result.Errors[0].Code);
    }

    [Fact]
    public void ValidateUnique_SameEntityUpdate_Passes()
    {
        var existing = new TestUser { Id = 1, Name = "Alice", Email = "alice@test.com" };
        _db.Users.Add(existing);
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(existing,
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        var result = cs.ValidateUnique("Email", _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateUnique_FieldNotInChanges_Skipped()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" },
            ["Name"]);

        var result = cs.ValidateUnique("Email", _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateUniqueAsync_NullValue_Passes()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com" });
        await _db.SaveChangesAsync();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = null },
            ["Email"]);

        var result = await cs.ValidateUniqueAsync(u => u.Email, _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ApplyTo_Update_DoesNotTouchNavigationProperties()
    {
        var address = new TestAddress { Id = 1, Street = "123 Main", City = "Springfield" };
        var user = new TestUserWithAddress { Id = 1, Name = "Alice", AddressId = 1, Address = address };
        _db.Addresses.Add(address);
        _db.UsersWithAddress.Add(user);
        _db.SaveChanges();
        _db.Entry(user).State = EntityState.Unchanged;
        _db.Entry(address).State = EntityState.Unchanged;

        var cs = Changeset<TestUserWithAddress>.Cast(user,
            new Dictionary<string, object?> { ["Name"] = "Bob" },
            ["Name"]);

        cs.ApplyTo(_db);

        var entry = _db.Entry(user);
        Assert.True(entry.Property("Name").IsModified);
        Assert.False(entry.Property("AddressId").IsModified);
        Assert.Equal("Bob", user.Name);
        Assert.Same(address, user.Address); // navigation property untouched
    }

    [Fact]
    public void ApplyTo_Insert_MaterializesAndTracksAssociation()
    {
        var cs = Changeset<TestUserWithAddress>.Cast(
                new Dictionary<string, object?>
                {
                    ["Name"] = "Alice",
                    ["Address"] = new Dictionary<string, object?>
                    {
                        ["Street"] = "123 Main",
                        ["City"] = "Springfield"
                    }
                },
                ["Name"])
            .CastAssoc<TestUserWithAddress, TestAddress>(
                "Address", ["Street", "City"]);

        var user = cs.ApplyTo(_db);

        Assert.NotNull(user.Address);
        Assert.Equal("123 Main", user.Address.Street);
        Assert.Equal(EntityState.Added, _db.Entry(user).State);
        Assert.Equal(EntityState.Added, _db.Entry(user.Address).State);
    }

    [Fact]
    public void ApplyTo_Update_AppliesAssociationChangesToTrackedEntity()
    {
        var address = new TestAddress { Id = 1, Street = "Old", City = "Keep" };
        var user = new TestUserWithAddress
        {
            Id = 1, Name = "Alice", AddressId = 1, Address = address
        };
        _db.Add(user);
        _db.SaveChanges();
        _db.Entry(user).State = EntityState.Unchanged;
        _db.Entry(address).State = EntityState.Unchanged;

        var cs = Changeset<TestUserWithAddress>.Cast(
                user,
                new Dictionary<string, object?>
                {
                    ["Address"] = new Dictionary<string, object?> { ["Street"] = "New" }
                },
                ["Name"])
            .CastAssoc<TestUserWithAddress, TestAddress>(
                "Address", ["Street", "City"]);

        var result = cs.ApplyTo(_db);

        Assert.Same(user, result);
        Assert.Same(address, result.Address);
        Assert.Equal("New", address.Street);
        Assert.Equal("Keep", address.City);
        Assert.True(_db.Entry(address).Property(a => a.Street).IsModified);
        Assert.False(_db.Entry(address).Property(a => a.City).IsModified);
    }

    [Fact]
    public async Task ValidateUniqueAsync_FieldNotInChanges_Skipped()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com" });
        await _db.SaveChangesAsync();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" },
            ["Name"]);

        var result = await cs.ValidateUniqueAsync("Email", _db);
        Assert.True(result.IsValid);
    }
}
