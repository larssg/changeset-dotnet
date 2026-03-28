using Changeset.Validators;

namespace Changeset.Tests;

public class ApplyChangesTests
{
    [Fact]
    public void ApplyChanges_Insert_CreatesNewInstance()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com",
            ["Age"] = 30
        };

        var cs = Changeset<User>.Cast(@params, ["Name", "Email", "Age"]);
        var user = cs.ApplyChanges();

        Assert.Equal("Alice", user.Name);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public void ApplyChanges_Update_AppliesOnlyChanges()
    {
        var existing = new User
        {
            Name = "Bob",
            Email = "bob@example.com",
            Age = 25,
            IsActive = true
        };

        var @params = new Dictionary<string, object?> { ["Name"] = "Robert" };
        var cs = Changeset<User>.Cast(existing, @params, ["Name"]);
        var updated = cs.ApplyChanges();

        Assert.Equal("Robert", updated.Name);
        Assert.Equal("bob@example.com", updated.Email); // preserved
        Assert.Equal(25, updated.Age);                   // preserved
        Assert.True(updated.IsActive);                   // preserved
        Assert.NotSame(existing, updated);               // new instance
    }

    [Fact]
    public void ApplyChanges_Invalid_Throws()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "not-a-number" };
        var cs = Changeset<User>.Cast(@params, ["Age"]);

        Assert.False(cs.IsValid);
        Assert.Throws<InvalidOperationException>(() => cs.ApplyChanges());
    }

    [Fact]
    public void ApplyChanges_NoParameterlessConstructor_NoFactory_ThrowsClearError()
    {
        var existing = new NoDefaultConstructor("Alice") { Email = "alice@example.com" };
        var @params = new Dictionary<string, object?> { ["Name"] = "Bob" };
        var cs = Changeset<NoDefaultConstructor>.Cast(existing, @params, ["Name"]);

        // Update path calls ShallowClone which uses Activator.CreateInstance — fails without parameterless ctor
        var ex = Assert.Throws<InvalidOperationException>(
            () => cs.ApplyChanges(() => new NoDefaultConstructor("default")));
        Assert.Contains("does not have a parameterless constructor", ex.Message);
        Assert.Contains("NoDefaultConstructor", ex.Message);
    }

    [Fact]
    public void ToResult_Valid_ReturnsValidResult()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com"
        };

        var cs = Changeset<User>.Cast(@params, ["Name", "Email"]);
        var result = cs.ToResult();

        Assert.IsType<ChangesetResult<User>.Valid>(result);
        var valid = (ChangesetResult<User>.Valid)result;
        Assert.Equal("Alice", valid.Value.Name);
    }

    [Fact]
    public void ToResult_Invalid_ReturnsInvalidResult()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "bad" };
        var cs = Changeset<User>.Cast(@params, ["Age"]);
        var result = cs.ToResult();

        Assert.IsType<ChangesetResult<User>.Invalid>(result);
        var invalid = (ChangesetResult<User>.Invalid)result;
        Assert.NotEmpty(invalid.Errors);
    }

    [Fact]
    public void ApplyChanges_WithFactory_UsesFactory()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "Alice" };
        var cs = Changeset<User>.Cast(@params, ["Name"]);

        var user = cs.ApplyChanges(() => new User { IsActive = true });

        Assert.Equal("Alice", user.Name);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void FullPipeline_CastValidateApply()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com",
            ["Age"] = "30"
        };

        var result = Changeset<User>.Cast(@params, ["Name", "Email", "Age"])
            .ValidateRequired(["Name", "Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
            .ValidateLength("Name", min: 2, max: 100)
            .ValidateNumber("Age", greaterThanOrEqual: 0, lessThan: 150)
            .ToResult();

        Assert.IsType<ChangesetResult<User>.Valid>(result);
        var user = ((ChangesetResult<User>.Valid)result).Value;
        Assert.Equal("Alice", user.Name);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public void FullPipeline_WithErrors_AccumulatesAll()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "",
            ["Email"] = "bad",
            ["Age"] = "200"
        };

        var cs = Changeset<User>.Cast(@params, ["Name", "Email", "Age"])
            .ValidateRequired(["Name"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
            .ValidateNumber("Age", lessThan: 150);

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("Name"));
        Assert.True(cs.HasErrorOn("Email"));
        Assert.True(cs.HasErrorOn("Age"));
    }

    [Fact]
    public void ApplyChanges_WithFactory_Update_IgnoresFactory()
    {
        var existing = new User
        {
            Name = "Bob",
            Email = "bob@example.com",
            Age = 25,
            IsActive = false
        };

        var @params = new Dictionary<string, object?> { ["Name"] = "Robert" };
        var cs = Changeset<User>.Cast(existing, @params, ["Name"]);

        // Factory sets IsActive=true, but update mode should ignore the factory
        var updated = cs.ApplyChanges(() => new User { IsActive = true });

        Assert.Equal("Robert", updated.Name);
        Assert.False(updated.IsActive); // preserved from existing, factory not used
        Assert.Equal("bob@example.com", updated.Email);
    }

    [Fact]
    public void ToResult_Valid_ReturnsValidWithCorrectlyAppliedValues()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com",
            ["Age"] = "30",
            ["IsActive"] = "true"
        };

        var result = Changeset<User>.Cast(@params, ["Name", "Email", "Age", "IsActive"])
            .ValidateRequired(["Name", "Email"])
            .ToResult();

        Assert.IsType<ChangesetResult<User>.Valid>(result);
        var user = ((ChangesetResult<User>.Valid)result).Value;
        Assert.Equal("Alice", user.Name);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal(30, user.Age);
        Assert.True(user.IsActive);
    }
}
