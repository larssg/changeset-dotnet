using Changeset.Validators;

namespace Changeset.Tests;

public class CastingTests
{
    [Fact]
    public void Cast_StringFields_SetsChanges()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com"
        };

        var cs = Changeset.Cast<User>(@params, ["Name", "Email"]);

        Assert.True(cs.IsValid);
        Assert.Equal("Alice", cs.GetChange<string>("Name"));
        Assert.Equal("alice@example.com", cs.GetChange<string>("Email"));
        Assert.Equal(ChangesetAction.Insert, cs.Action);
    }

    [Fact]
    public void Cast_StringToInt_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "42" };
        var cs = Changeset.Cast<User>(@params, ["Age"]);

        Assert.True(cs.IsValid);
        Assert.Equal(42, cs.GetChange<int>("Age"));
    }

    [Fact]
    public void Cast_StringToDecimal_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Salary"] = "75000.50" };
        var cs = Changeset.Cast<User>(@params, ["Salary"]);

        Assert.True(cs.IsValid);
        Assert.Equal(75000.50m, cs.GetChange<decimal>("Salary"));
    }

    [Fact]
    public void Cast_StringToBool_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["IsActive"] = "true" };
        var cs = Changeset.Cast<User>(@params, ["IsActive"]);

        Assert.True(cs.IsValid);
        Assert.Equal(true, cs.GetChange<bool>("IsActive"));
    }

    [Fact]
    public void Cast_StringToBool_NumericValues()
    {
        var @params = new Dictionary<string, object?> { ["IsActive"] = "1" };
        var cs = Changeset.Cast<User>(@params, ["IsActive"]);

        Assert.True(cs.IsValid);
        Assert.Equal(true, cs.GetChange<bool>("IsActive"));
    }

    [Fact]
    public void Cast_StringToDateTime_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["BirthDate"] = "1990-06-15" };
        var cs = Changeset.Cast<User>(@params, ["BirthDate"]);

        Assert.True(cs.IsValid);
        Assert.Equal(new DateTime(1990, 6, 15), cs.GetChange<DateTime>("BirthDate"));
    }

    [Fact]
    public void Cast_StringToGuid_Coerces()
    {
        var guid = Guid.NewGuid();
        var @params = new Dictionary<string, object?> { ["ExternalId"] = guid.ToString() };
        var cs = Changeset.Cast<User>(@params, ["ExternalId"]);

        Assert.True(cs.IsValid);
        Assert.Equal(guid, cs.GetChange<Guid>("ExternalId"));
    }

    [Fact]
    public void Cast_StringToEnum_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Role"] = "Admin" };
        var cs = Changeset.Cast<User>(@params, ["Role"]);

        Assert.True(cs.IsValid);
        Assert.Equal(UserRole.Admin, cs.GetChange<UserRole>("Role"));
    }

    [Fact]
    public void Cast_StringToEnum_CaseInsensitive()
    {
        var @params = new Dictionary<string, object?> { ["Role"] = "admin" };
        var cs = Changeset.Cast<User>(@params, ["Role"]);

        Assert.True(cs.IsValid);
        Assert.Equal(UserRole.Admin, cs.GetChange<UserRole>("Role"));
    }

    [Fact]
    public void Cast_InvalidType_AddsError()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "not-a-number" };
        var cs = Changeset.Cast<User>(@params, ["Age"]);

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("Age"));
        Assert.Equal("invalid_cast", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void Cast_NullForNullable_Succeeds()
    {
        var @params = new Dictionary<string, object?> { ["BirthDate"] = null };
        var cs = Changeset.Cast<User>(@params, ["BirthDate"]);

        Assert.True(cs.IsValid);
        Assert.Null(cs.GetChange<DateTime?>("BirthDate"));
    }

    [Fact]
    public void Cast_UnpermittedFields_Dropped()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com",
            ["Age"] = "42"
        };

        var cs = Changeset.Cast<User>(@params, ["Name"]);

        Assert.True(cs.IsValid);
        Assert.True(cs.Changes.ContainsKey("Name"));
        Assert.False(cs.Changes.ContainsKey("Email"));
        Assert.False(cs.Changes.ContainsKey("Age"));
    }

    [Fact]
    public void Cast_StrictMode_ErrorOnUnpermitted()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["HackerField"] = "evil"
        };

        var cs = Changeset.Cast<User>(@params, ["Name"],
            new CastOptions { StrictCasting = true });

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("HackerField"));
        Assert.Equal("unpermitted_field", cs.ErrorsOn("HackerField")[0].Code);
    }

    [Fact]
    public void Cast_MissingField_NotInChanges()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "Alice" };
        var cs = Changeset.Cast<User>(@params, ["Name", "Email"]);

        Assert.True(cs.IsValid);
        Assert.True(cs.Changes.ContainsKey("Name"));
        Assert.False(cs.Changes.ContainsKey("Email"));
    }

    [Fact]
    public void Cast_CaseInsensitiveFields()
    {
        var @params = new Dictionary<string, object?> { ["name"] = "Alice" };
        var cs = Changeset.Cast<User>(@params, ["Name"]);

        Assert.True(cs.IsValid);
        Assert.Equal("Alice", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_TrimsStrings_ByDefault()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "  Alice  " };
        var cs = Changeset.Cast<User>(@params, ["Name"]);

        Assert.Equal("Alice", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_NoTrim_WhenDisabled()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "  Alice  " };
        var cs = Changeset.Cast<User>(@params, ["Name"],
            new CastOptions { TrimStrings = false });

        Assert.Equal("  Alice  ", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_Update_SetsActionAndPreservesData()
    {
        var existing = new User { Name = "Bob", Email = "bob@example.com", Age = 30 };
        var @params = new Dictionary<string, object?> { ["Name"] = "Robert" };

        var cs = Changeset.Cast(existing, @params, ["Name"]);

        Assert.Equal(ChangesetAction.Update, cs.Action);
        Assert.Same(existing, cs.Data);
        Assert.Equal("Robert", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_Update_SkipsUnchangedValues()
    {
        var existing = new User { Name = "Alice", Age = 30 };
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice", // same value
            ["Age"] = "31"      // different
        };

        var cs = Changeset.Cast(existing, @params, ["Name", "Age"]);

        Assert.False(cs.Changes.ContainsKey("Name")); // unchanged, not in Changes
        Assert.True(cs.Changes.ContainsKey("Age"));
        Assert.Equal(31, cs.GetChange<int>("Age"));
    }

    [Fact]
    public void Cast_EmptyParams_NoChanges()
    {
        var @params = new Dictionary<string, object?>();
        var cs = Changeset.Cast<User>(@params, ["Name", "Email"]);

        Assert.True(cs.IsValid);
        Assert.Empty(cs.Changes);
    }

    [Fact]
    public void Cast_EmptyPermitted_NoChanges()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "Alice" };
        var cs = Changeset.Cast<User>(@params, []);

        Assert.True(cs.IsValid);
        Assert.Empty(cs.Changes);
    }

    [Fact]
    public void Cast_IntToInt_DirectAssignment()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = 42 };
        var cs = Changeset.Cast<User>(@params, ["Age"]);

        Assert.True(cs.IsValid);
        Assert.Equal(42, cs.GetChange<int>("Age"));
    }
}
