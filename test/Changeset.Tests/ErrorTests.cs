using System.Collections.Immutable;

namespace Changeset.Tests;

public class ErrorTests
{
    [Fact]
    public void ErrorsOn_ReturnsFieldSpecificErrors()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = "bad", ["Name"] = 42 },
            ["Age", "Name"]);

        Assert.True(cs.HasErrorOn("Age"));
        Assert.True(cs.HasErrorOn("Name"));
        var ageErrors = cs.ErrorsOn("Age");
        Assert.Single(ageErrors);
        Assert.Equal("invalid_cast", ageErrors[0].Code);
    }

    [Fact]
    public void ErrorMap_GroupsByField()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = "bad", ["Name"] = 42 },
            ["Age", "Name"]);

        var map = cs.ErrorMap;
        Assert.True(map.ContainsKey("Age"));
        Assert.True(map.ContainsKey("Name"));
    }

    [Fact]
    public void BaseErrors_ReturnsNonFieldErrors()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .AddBaseError("something went wrong", "base_error");

        Assert.Single(cs.BaseErrors);
        Assert.Equal("base_error", cs.BaseErrors[0].Code);
        Assert.Equal("", cs.BaseErrors[0].Field);
    }

    [Fact]
    public void AddError_ReturnsNewInstance()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"]);

        var cs2 = cs.AddError("Name", "is taken", "uniqueness");

        Assert.True(cs.IsValid);       // original unchanged
        Assert.False(cs2.IsValid);
        Assert.Equal("uniqueness", cs2.ErrorsOn("Name")[0].Code);
    }

    [Fact]
    public void AddError_WithMetadata_PreservesMetadata()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .AddError("Name", "too common", "custom",
                ImmutableDictionary.CreateRange(new[]
                {
                    KeyValuePair.Create<string, object>("suggestion", "AliceX")
                }));

        var error = cs.ErrorsOn("Name")[0];
        Assert.Equal("AliceX", error.Metadata!["suggestion"]);
    }
}
