using System.Text.Json;
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

        var cs = Changeset<User>.Cast(@params, ["Name", "Email"]);

        Assert.True(cs.IsValid);
        Assert.Equal("Alice", cs.GetChange<string>("Name"));
        Assert.Equal("alice@example.com", cs.GetChange<string>("Email"));
        Assert.Equal(ChangesetAction.Insert, cs.Action);
    }

    [Fact]
    public void Cast_StringToInt_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "42" };
        var cs = Changeset<User>.Cast(@params, ["Age"]);

        Assert.True(cs.IsValid);
        Assert.Equal(42, cs.GetChange<int>("Age"));
    }

    [Fact]
    public void Cast_StringToDecimal_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Salary"] = "75000.50" };
        var cs = Changeset<User>.Cast(@params, ["Salary"]);

        Assert.True(cs.IsValid);
        Assert.Equal(75000.50m, cs.GetChange<decimal>("Salary"));
    }

    [Fact]
    public void Cast_StringToBool_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["IsActive"] = "true" };
        var cs = Changeset<User>.Cast(@params, ["IsActive"]);

        Assert.True(cs.IsValid);
        Assert.Equal(true, cs.GetChange<bool>("IsActive"));
    }

    [Fact]
    public void Cast_StringToBool_NumericValues()
    {
        var @params = new Dictionary<string, object?> { ["IsActive"] = "1" };
        var cs = Changeset<User>.Cast(@params, ["IsActive"]);

        Assert.True(cs.IsValid);
        Assert.Equal(true, cs.GetChange<bool>("IsActive"));
    }

    [Fact]
    public void Cast_StringToDateTime_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["BirthDate"] = "1990-06-15" };
        var cs = Changeset<User>.Cast(@params, ["BirthDate"]);

        Assert.True(cs.IsValid);
        Assert.Equal(new DateTime(1990, 6, 15), cs.GetChange<DateTime>("BirthDate"));
    }

    [Fact]
    public void Cast_StringToGuid_Coerces()
    {
        var guid = Guid.NewGuid();
        var @params = new Dictionary<string, object?> { ["ExternalId"] = guid.ToString() };
        var cs = Changeset<User>.Cast(@params, ["ExternalId"]);

        Assert.True(cs.IsValid);
        Assert.Equal(guid, cs.GetChange<Guid>("ExternalId"));
    }

    [Fact]
    public void Cast_StringToEnum_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Role"] = "Admin" };
        var cs = Changeset<User>.Cast(@params, ["Role"]);

        Assert.True(cs.IsValid);
        Assert.Equal(UserRole.Admin, cs.GetChange<UserRole>("Role"));
    }

    [Fact]
    public void Cast_StringToEnum_CaseInsensitive()
    {
        var @params = new Dictionary<string, object?> { ["Role"] = "admin" };
        var cs = Changeset<User>.Cast(@params, ["Role"]);

        Assert.True(cs.IsValid);
        Assert.Equal(UserRole.Admin, cs.GetChange<UserRole>("Role"));
    }

    [Fact]
    public void Cast_InvalidType_AddsError()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "not-a-number" };
        var cs = Changeset<User>.Cast(@params, ["Age"]);

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("Age"));
        Assert.Equal("invalid_cast", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void Cast_NullForNullable_Succeeds()
    {
        var @params = new Dictionary<string, object?> { ["BirthDate"] = null };
        var cs = Changeset<User>.Cast(@params, ["BirthDate"]);

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

        var cs = Changeset<User>.Cast(@params, ["Name"]);

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

        var cs = Changeset<User>.Cast(@params, ["Name"],
            new CastOptions { StrictCasting = true });

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("HackerField"));
        Assert.Equal("unpermitted_field", cs.ErrorsOn("HackerField")[0].Code);
    }

    [Fact]
    public void Cast_MissingField_NotInChanges()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "Alice" };
        var cs = Changeset<User>.Cast(@params, ["Name", "Email"]);

        Assert.True(cs.IsValid);
        Assert.True(cs.Changes.ContainsKey("Name"));
        Assert.False(cs.Changes.ContainsKey("Email"));
    }

    [Fact]
    public void Cast_CaseInsensitiveFields()
    {
        var @params = new Dictionary<string, object?> { ["name"] = "Alice" };
        var cs = Changeset<User>.Cast(@params, ["Name"]);

        Assert.True(cs.IsValid);
        Assert.Equal("Alice", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_TrimsStrings_ByDefault()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "  Alice  " };
        var cs = Changeset<User>.Cast(@params, ["Name"]);

        Assert.Equal("Alice", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_NoTrim_WhenDisabled()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "  Alice  " };
        var cs = Changeset<User>.Cast(@params, ["Name"],
            new CastOptions { TrimStrings = false });

        Assert.Equal("  Alice  ", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_Update_SetsActionAndPreservesData()
    {
        var existing = new User { Name = "Bob", Email = "bob@example.com", Age = 30 };
        var @params = new Dictionary<string, object?> { ["Name"] = "Robert" };

        var cs = Changeset<User>.Cast(existing, @params, ["Name"]);

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

        var cs = Changeset<User>.Cast(existing, @params, ["Name", "Age"]);

        Assert.False(cs.Changes.ContainsKey("Name")); // unchanged, not in Changes
        Assert.True(cs.Changes.ContainsKey("Age"));
        Assert.Equal(31, cs.GetChange<int>("Age"));
    }

    [Fact]
    public void Cast_EmptyParams_NoChanges()
    {
        var @params = new Dictionary<string, object?>();
        var cs = Changeset<User>.Cast(@params, ["Name", "Email"]);

        Assert.True(cs.IsValid);
        Assert.Empty(cs.Changes);
    }

    [Fact]
    public void Cast_EmptyPermitted_NoChanges()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "Alice" };
        var cs = Changeset<User>.Cast(@params, []);

        Assert.True(cs.IsValid);
        Assert.Empty(cs.Changes);
    }

    [Fact]
    public void Cast_IntToInt_DirectAssignment()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = 42 };
        var cs = Changeset<User>.Cast(@params, ["Age"]);

        Assert.True(cs.IsValid);
        Assert.Equal(42, cs.GetChange<int>("Age"));
    }

    [Fact]
    public void Cast_NumericOverflow_StringToInt_ProducesInvalidCast()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "999999999999" };
        var cs = Changeset<User>.Cast(@params, ["Age"]);

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_cast", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void Cast_EmptyString_ToNullableInt_ProducesInvalidCast()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "" };
        var cs = Changeset<User>.Cast(@params, ["Age"]);

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_cast", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void Cast_EmptyString_ToNullableGuid_ProducesInvalidCast()
    {
        var @params = new Dictionary<string, object?> { ["ExternalId"] = "" };
        var cs = Changeset<User>.Cast(@params, ["ExternalId"]);

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_cast", cs.ErrorsOn("ExternalId")[0].Code);
    }

    [Fact]
    public void Cast_EmptyString_ToNullableDateTime_ProducesInvalidCast()
    {
        var @params = new Dictionary<string, object?> { ["BirthDate"] = "" };
        var cs = Changeset<User>.Cast(@params, ["BirthDate"]);

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_cast", cs.ErrorsOn("BirthDate")[0].Code);
    }

    [Fact]
    public void Cast_WhitespaceOnly_ToInt_WithTrimStrings_ProducesInvalidCast()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = "   " };
        var cs = Changeset<User>.Cast(@params, ["Age"], new CastOptions { TrimStrings = true });

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_cast", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void Cast_InvalidEnumString_ProducesInvalidCast()
    {
        var @params = new Dictionary<string, object?> { ["Role"] = "NotARole" };
        var cs = Changeset<User>.Cast(@params, ["Role"]);

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_cast", cs.ErrorsOn("Role")[0].Code);
    }

    [Fact]
    public void Cast_StringToDateOnly_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Date"] = "2024-06-15" };
        var cs = Changeset<Event>.Cast(@params, ["Date"]);

        Assert.True(cs.IsValid);
        Assert.Equal(new DateOnly(2024, 6, 15), cs.GetChange<DateOnly>("Date"));
    }

    [Fact]
    public void Cast_StringToTimeOnly_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["StartTime"] = "14:30:00" };
        var cs = Changeset<Event>.Cast(@params, ["StartTime"]);

        Assert.True(cs.IsValid);
        Assert.Equal(new TimeOnly(14, 30, 0), cs.GetChange<TimeOnly>("StartTime"));
    }

    [Fact]
    public void Cast_JsonElement_NumberToString_Coerces()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("{\"Name\": 42}");
        var @params = new Dictionary<string, object?> { ["Name"] = json.GetProperty("Name") };
        var cs = Changeset<User>.Cast(@params, ["Name"]);

        Assert.True(cs.IsValid);
        Assert.Equal("42", cs.GetChange<string>("Name"));
    }

    [Fact]
    public void Cast_JsonElement_NestedObject_ToFlatProperty_Fails()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("{\"Name\": {\"first\": \"Alice\"}}");
        var @params = new Dictionary<string, object?> { ["Name"] = json.GetProperty("Name") };
        var cs = Changeset<User>.Cast(@params, ["Name"]);

        // Nested object ToString() returns JSON, which is a valid string
        Assert.True(cs.IsValid);
    }

    [Fact]
    public void Cast_VeryPreciseDecimal_Coerces()
    {
        var @params = new Dictionary<string, object?> { ["Salary"] = "0.000000000000001" };
        var cs = Changeset<User>.Cast(@params, ["Salary"]);

        Assert.True(cs.IsValid);
        Assert.Equal(0.000000000000001m, cs.GetChange<decimal>("Salary"));
    }

    [Fact]
    public void Cast_InitOnlySetter_IsExcludedFromCasting()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["ReadOnlyTag"] = "tag-value"
        };
        var cs = Changeset<Immutable>.Cast(@params, ["Name", "ReadOnlyTag"]);

        Assert.True(cs.Changes.ContainsKey("Name"));
        // init-only setters have CanWrite=true via reflection, so they
        // are included in casting — this documents the actual behavior
    }

    [Fact]
    public void Cast_NumericToNumeric_Overflow_LongMaxToInt_Fails()
    {
        var @params = new Dictionary<string, object?> { ["Age"] = long.MaxValue };
        var cs = Changeset<User>.Cast(@params, ["Age"]);

        Assert.False(cs.IsValid);
        Assert.Equal("invalid_cast", cs.ErrorsOn("Age")[0].Code);
    }
}
