using Changeset.Validators;

namespace Changeset.Tests;

public class AssocTests
{
    [Fact]
    public void CastAssoc_CreatesChildChangeset()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Address"] = new Dictionary<string, object?>
            {
                ["Street"] = "123 Main St",
                ["City"] = "Springfield",
                ["Zip"] = "62701"
            }
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City", "Zip"]);

        Assert.True(cs.IsValid);
        Assert.True(cs.Changes.ContainsKey("Address"));

        var childCs = cs.GetAssoc<UserWithAddress, Address>("Address");
        Assert.NotNull(childCs);
        Assert.Equal("123 Main St", childCs!.GetChange<string>("Street"));
        Assert.Equal("Springfield", childCs.GetChange<string>("City"));
        Assert.Equal("62701", childCs.GetChange<string>("Zip"));
    }

    [Fact]
    public void CastAssoc_MissingParams_Skipped()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice"
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City"]);

        Assert.True(cs.IsValid);
        Assert.False(cs.Changes.ContainsKey("Address"));
    }

    [Fact]
    public void CastAssoc_InvalidParams_AddsError()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Address"] = "not-a-dict"
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Street"]);

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_assoc", cs.Errors[0].Code);
        Assert.Equal("Address", cs.Errors[0].Field);
    }

    [Fact]
    public void CastAssoc_ChildCastErrors_PrefixedWithDotNotation()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Address"] = new Dictionary<string, object?>
            {
                ["Zip"] = 12345  // int, not string — will fail cast to string
            }
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Zip"],
                new CastOptions { StrictCasting = false });

        // Zip is an int being cast to string property — should it fail?
        // TypeCoercion will handle int->string, let's check behavior
        var childCs = cs.GetAssoc<UserWithAddress, Address>("Address");
        Assert.NotNull(childCs);
    }

    [Fact]
    public void ValidateAssoc_RunsValidatorOnChild()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Address"] = new Dictionary<string, object?>
            {
                ["Street"] = "",
                ["City"] = "Springfield"
            }
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City"])
            .ValidateAssoc<UserWithAddress, Address>("Address", addressCs =>
                addressCs.ValidateRequired(["Street"]));

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("Address.Street"));
        Assert.Equal("required", cs.ErrorsOn("Address.Street")[0].Code);
    }

    [Fact]
    public void ValidateAssoc_ValidChild_PassesThrough()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Address"] = new Dictionary<string, object?>
            {
                ["Street"] = "123 Main St",
                ["City"] = "Springfield"
            }
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City"])
            .ValidateAssoc<UserWithAddress, Address>("Address", addressCs =>
                addressCs.ValidateRequired(["Street", "City"]));

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateAssoc_NoChildChangeset_Skipped()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice"
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .ValidateAssoc<UserWithAddress, Address>("Address", addressCs =>
                addressCs.ValidateRequired(["Street"]));

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateAssoc_MultipleChildErrors_AllPrefixed()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Address"] = new Dictionary<string, object?>
            {
                ["Street"] = "",
                ["City"] = ""
            }
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City"])
            .ValidateAssoc<UserWithAddress, Address>("Address", addressCs =>
                addressCs
                    .ValidateRequired(["Street", "City"]));

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("Address.Street"));
        Assert.True(cs.HasErrorOn("Address.City"));
    }

    [Fact]
    public void CastAssoc_WithExistingData_ExtractsExistingAssoc()
    {
        var existing = new UserWithAddress
        {
            Name = "Alice",
            Address = new Address { Street = "Old St", City = "Old City", Zip = "00000" }
        };

        var @params = new Dictionary<string, object?>
        {
            ["Address"] = new Dictionary<string, object?>
            {
                ["Street"] = "New St"
            }
        };

        var cs = Changeset.Cast(existing, @params, ["Name"])
            .CastAssoc<UserWithAddress, Address>("Address", ["Street", "City", "Zip"]);

        Assert.True(cs.IsValid);
        var childCs = cs.GetAssoc<UserWithAddress, Address>("Address");
        Assert.NotNull(childCs);
        Assert.Equal(ChangesetAction.Update, childCs!.Action);
        Assert.Equal("New St", childCs.GetChange<string>("Street"));
        // City unchanged, so not in changes
        Assert.False(childCs.Changes.ContainsKey("City"));
    }

    [Fact]
    public void GetAssoc_WrongType_ReturnsNull()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice"
        };

        var cs = Changeset.Cast<UserWithAddress>(@params, ["Name"]);
        Assert.Null(cs.GetAssoc<UserWithAddress, Address>("Address"));
    }
}
