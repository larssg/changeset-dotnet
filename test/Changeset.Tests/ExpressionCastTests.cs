namespace Changeset.Tests;

public class ExpressionCastTests
{
    [Fact]
    public void Cast_AnonymousType_ExtractsFieldNames()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@test.com",
            ["Age"] = 30
        };

        var cs = Changeset<User>.Cast(@params, u => new { u.Name, u.Email });

        Assert.True(cs.IsValid);
        Assert.Equal("Alice", cs.GetChange<string>("Name"));
        Assert.Equal("alice@test.com", cs.GetChange<string>("Email"));
        // Age not permitted, should not be in changes
        Assert.False(cs.Changes.ContainsKey("Age"));
    }

    [Fact]
    public void Cast_SingleProperty_Works()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@test.com"
        };

        var cs = Changeset<User>.Cast(@params, u => u.Name);

        Assert.True(cs.IsValid);
        Assert.Equal("Alice", cs.GetChange<string>("Name"));
        Assert.False(cs.Changes.ContainsKey("Email"));
    }

    [Fact]
    public void Cast_Update_WithExpression()
    {
        var existing = new User { Name = "Old", Email = "old@test.com", Age = 25 };
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "New",
            ["Email"] = "old@test.com"
        };

        var cs = Changeset<User>.Cast(existing, @params, u => new { u.Name, u.Email });

        Assert.Equal(ChangesetAction.Update, cs.Action);
        Assert.Equal("New", cs.GetChange<string>("Name"));
        // Email unchanged, should not be in changes
        Assert.False(cs.Changes.ContainsKey("Email"));
    }

    [Fact]
    public void Cast_Expression_WithOptions()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "  Alice  ",
            ["Salary"] = "50000.50"
        };

        var cs = Changeset<User>.Cast(@params, u => new { u.Name, u.Salary },
            new CastOptions { TrimStrings = true });

        Assert.Equal("Alice", cs.GetChange<string>("Name"));
        Assert.Equal(50000.50m, cs.GetChange<decimal>("Salary"));
    }

    [Fact]
    public void Cast_Expression_AllFieldTypes()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Age"] = 30,
            ["IsActive"] = true,
            ["Salary"] = 75000m
        };

        var cs = Changeset<User>.Cast(@params, u => new { u.Name, u.Age, u.IsActive, u.Salary });

        Assert.True(cs.IsValid);
        Assert.Equal(4, cs.Changes.Count);
    }
}
