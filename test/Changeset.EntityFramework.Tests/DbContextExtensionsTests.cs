using System.Collections.Immutable;
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
    public string Nickname { get; set; } = "";
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

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<TestUser>().Ignore(u => u.Nickname);
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

    [Fact]
    public void ValidateUnique_Update_ConflictsWithOtherRow_AddsError()
    {
        var alice = new TestUser { Id = 1, Name = "Alice", Email = "alice@test.com" };
        _db.Users.AddRange(alice,
            new TestUser { Id = 2, Name = "Bob", Email = "bob@test.com" });
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(alice,
            new Dictionary<string, object?> { ["Email"] = "bob@test.com" },
            ["Email"]);

        var result = cs.ValidateUnique("Email", _db);
        Assert.False(result.IsValid);
        Assert.Equal("uniqueness", result.Errors[0].Code);
    }

    [Fact]
    public void ValidateUnique_Update_MatchesOnlyCurrentRow_Passes()
    {
        _db.Users.Add(new TestUser { Id = 1, Name = "Alice", Email = "alice@test.com" });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        // A stale detached copy: its Email differs from the stored row, so the cast
        // records a change whose value matches only the row being updated. Primary-key
        // exclusion must keep that from counting as a conflict.
        var staleCopy = new TestUser { Id = 1, Name = "Alice", Email = "outdated@test.com" };
        var cs = Changeset<TestUser>.Cast(staleCopy,
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        var result = cs.ValidateUnique("Email", _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateUnique_Composite_Conflict_AddsErrorOnFirstField()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com", Age = 30 });
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Age"] = 30 },
            ["Name", "Age"]);

        var result = cs.ValidateUnique(["Name", "Age"], _db);
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Name", error.Field);
        Assert.Equal("uniqueness", error.Code);
        var fields = Assert.IsType<ImmutableArray<string>>(error.Metadata!["fields"]);
        Assert.Equal(["Name", "Age"], fields.AsEnumerable());
    }

    [Fact]
    public void ValidateUnique_Composite_DifferentCombination_Passes()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com", Age = 30 });
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Age"] = 40 },
            ["Name", "Age"]);

        var result = cs.ValidateUnique(["Name", "Age"], _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateUniqueAsync_Composite_ExpressionForm_Conflict_AddsError()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com", Age = 30 });
        await _db.SaveChangesAsync();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Age"] = 30 },
            ["Name", "Age"]);

        var result = await cs.ValidateUniqueAsync(u => new { u.Name, u.Age }, _db);
        Assert.False(result.IsValid);
        Assert.Equal("Name", result.Errors[0].Field);
    }

    [Fact]
    public void ValidateUnique_Composite_FallsBackToDataForUnchangedFields()
    {
        var bob = new TestUser { Id = 2, Name = "Bob", Email = "bob@test.com", Age = 30 };
        _db.Users.AddRange(
            new TestUser { Id = 1, Name = "Alice", Email = "alice@test.com", Age = 30 },
            bob);
        _db.SaveChanges();

        // Only Name changes; Age comes from Data (30) and collides with Alice's row.
        var cs = Changeset<TestUser>.Cast(bob,
            new Dictionary<string, object?> { ["Name"] = "Alice" },
            ["Name"]);

        var result = cs.ValidateUnique(["Name", "Age"], _db);
        Assert.False(result.IsValid);
        Assert.Equal("uniqueness", result.Errors[0].Code);
    }

    [Fact]
    public void ValidateUnique_Composite_FallbackValueAvoidsConflict_Passes()
    {
        var bob = new TestUser { Id = 2, Name = "Bob", Email = "bob@test.com", Age = 25 };
        _db.Users.AddRange(
            new TestUser { Id = 1, Name = "Alice", Email = "alice@test.com", Age = 30 },
            bob);
        _db.SaveChanges();

        // Alice+25 does not exist, so renaming Bob to Alice is fine.
        var cs = Changeset<TestUser>.Cast(bob,
            new Dictionary<string, object?> { ["Name"] = "Alice" },
            ["Name"]);

        var result = cs.ValidateUnique(["Name", "Age"], _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateUnique_Composite_NoFieldsChanged_Skipped()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com", Age = 30 });
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "new@test.com" },
            ["Email"]);

        var result = cs.ValidateUnique(["Name", "Age"], _db);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateUnique_ScopeExcludesConflictingRow_Passes()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com", Age = 30 });
        _db.SaveChanges();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        var result = cs.ValidateUnique("Email", _db, scope: u => u.Age == 99);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateUniqueAsync_ScopeIncludesConflictingRow_AddsError()
    {
        _db.Users.Add(new TestUser { Name = "Alice", Email = "alice@test.com", Age = 30 });
        await _db.SaveChangesAsync();

        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        var result = await cs.ValidateUniqueAsync(
            u => u.Email, _db, scope: u => u.Age == 30);
        Assert.False(result.IsValid);
        Assert.Equal("uniqueness", result.Errors[0].Code);
    }

    [Fact]
    public void ValidateUnique_EmptyFields_Throws()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        Assert.Throws<ArgumentException>(() => cs.ValidateUnique([], _db));
    }

    [Fact]
    public void ValidateUnique_UnmappedProperty_Throws()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Nickname"] = "Al" },
            ["Nickname"]);

        var exception = Assert.Throws<ArgumentException>(
            () => cs.ValidateUnique("Nickname", _db));
        Assert.Contains("Nickname", exception.Message);
    }

    [Fact]
    public async Task TryApplyToAsync_Valid_SavesAndReturnsValid()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = "alice@test.com" },
            ["Name", "Email"]);

        var result = await cs.TryApplyToAsync(_db, _ => null);

        var valid = Assert.IsType<ChangesetResult<TestUser>.Valid>(result);
        Assert.Equal("Alice", valid.Value.Name);
        Assert.Single(_db.Users);
    }

    [Fact]
    public async Task TryApplyToAsync_InvalidChangeset_ReturnsInvalidWithoutSaving()
    {
        var cs = Changeset<TestUser>.Cast(
                new Dictionary<string, object?> { ["Name"] = "Alice" },
                ["Name"])
            .AddError("Name", "is invalid", "custom");

        var result = await cs.TryApplyToAsync(_db, _ => null);

        var invalid = Assert.IsType<ChangesetResult<TestUser>.Invalid>(result);
        Assert.Equal("custom", invalid.Errors[0].Code);
        Assert.Empty(_db.Users);
    }

    [Fact]
    public async Task TryApplyToAsync_SaveFails_MapsExceptionToError()
    {
        using var db = CreateThrowingContext();
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        var result = await cs.TryApplyToAsync(db, exception =>
            exception.Message.Contains("IX_Users_Email")
                ? ChangesetError.For("Email", "has already been taken", "uniqueness")
                : null);

        var invalid = Assert.IsType<ChangesetResult<TestUser>.Invalid>(result);
        var error = Assert.Single(invalid.Errors);
        Assert.Equal("Email", error.Field);
        Assert.Equal("uniqueness", error.Code);
    }

    [Fact]
    public async Task TryApplyToAsync_SaveFails_UnmappedExceptionRethrows()
    {
        using var db = CreateThrowingContext();
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Email"] = "alice@test.com" },
            ["Email"]);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => cs.TryApplyToAsync(db, _ => null));
    }

    private static ThrowingSaveDbContext CreateThrowingContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private sealed class ThrowingSaveDbContext : TestDbContext
    {
        public ThrowingSaveDbContext(DbContextOptions<TestDbContext> options)
            : base(options) { }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException(
                "duplicate key value violates unique constraint \"IX_Users_Email\"");
    }
}
