using Changeset.Validators;

namespace Changeset.Tests;

public class ValidatorTests
{
    [Fact]
    public void ValidateRequired_Present_Passes()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateRequired(["Name"]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateRequired_Missing_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?>(), ["Name"])
            .ValidateRequired(["Name"]);

        Assert.False(cs.IsValid);
        Assert.Equal("required", cs.ErrorsOn("Name")[0].Code);
        Assert.Equal("can't be blank", cs.ErrorsOn("Name")[0].Message);
    }

    [Fact]
    public void ValidateRequired_Null_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["BirthDate"] = null }, ["BirthDate"])
            .ValidateRequired(["BirthDate"]);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateRequired_EmptyString_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "" }, ["Name"])
            .ValidateRequired(["Name"]);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateRequired_WhitespaceString_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "   " }, ["Name"],
            new CastOptions { TrimStrings = false })
            .ValidateRequired(["Name"]);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateFormat_ValidEmail_Passes()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Email"] = "alice@example.com" }, ["Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateFormat_InvalidEmail_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Email"] = "not-an-email" }, ["Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        Assert.False(cs.IsValid);
        Assert.Equal("format", cs.ErrorsOn("Email")[0].Code);
    }

    [Fact]
    public void ValidateFormat_FieldNotInChanges_Skipped()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?>(), ["Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_Min_TooShort_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "A" }, ["Name"])
            .ValidateLength("Name", min: 2);

        Assert.False(cs.IsValid);
        Assert.Equal("length", cs.ErrorsOn("Name")[0].Code);
        Assert.Contains("at least 2", cs.ErrorsOn("Name")[0].Message);
    }

    [Fact]
    public void ValidateLength_Max_TooLong_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = new string('A', 101) }, ["Name"])
            .ValidateLength("Name", max: 100);

        Assert.False(cs.IsValid);
        Assert.Contains("at most 100", cs.ErrorsOn("Name")[0].Message);
    }

    [Fact]
    public void ValidateLength_Exact_WrongLength_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "ABC" }, ["Name"])
            .ValidateLength("Name", @is: 5);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_WithinBounds_Passes()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateLength("Name", min: 2, max: 100);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateNumber_GreaterThanOrEqual_Passes()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Age"] = 18 }, ["Age"])
            .ValidateNumber("Age", greaterThanOrEqual: 0);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateNumber_LessThan_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Age"] = 200 }, ["Age"])
            .ValidateNumber("Age", lessThan: 150);

        Assert.False(cs.IsValid);
        Assert.Equal("number", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void ValidateInclusion_Valid_Passes()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Role"] = UserRole.Admin }, ["Role"])
            .ValidateInclusion("Role", [UserRole.Member, UserRole.Admin]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateInclusion_Invalid_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Role"] = UserRole.Guest }, ["Role"])
            .ValidateInclusion("Role", [UserRole.Member, UserRole.Admin]);

        Assert.False(cs.IsValid);
        Assert.Equal("inclusion", cs.ErrorsOn("Role")[0].Code);
    }

    [Fact]
    public void ValidateExclusion_Excluded_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "admin" }, ["Name"])
            .ValidateExclusion("Name", ["admin", "root", "superuser"]);

        Assert.False(cs.IsValid);
        Assert.Equal("exclusion", cs.ErrorsOn("Name")[0].Code);
    }

    [Fact]
    public void ValidateExclusion_NotExcluded_Passes()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateExclusion("Name", ["admin", "root"]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateConfirmation_Matching_Passes()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Email"] = "alice@example.com",
            ["Email_confirmation"] = "alice@example.com"
        };

        var cs = Changeset.Cast<User>(@params, ["Email"])
            .ValidateConfirmation("Email");

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateConfirmation_NotMatching_Fails()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Email"] = "alice@example.com",
            ["Email_confirmation"] = "bob@example.com"
        };

        var cs = Changeset.Cast<User>(@params, ["Email"])
            .ValidateConfirmation("Email");

        Assert.False(cs.IsValid);
        Assert.Equal("confirmation", cs.ErrorsOn("Email_confirmation")[0].Code);
    }

    [Fact]
    public void ValidateChange_CustomValidator_Passes()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateChange("Name", (changeset, value) =>
            {
                if (value is string s && s.StartsWith("A"))
                    return changeset;
                return changeset.AddError("Name", "must start with A", "custom");
            });

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateChange_CustomValidator_Fails()
    {
        var cs = Changeset.Cast<User>(
            new Dictionary<string, object?> { ["Name"] = "Bob" }, ["Name"])
            .ValidateChange("Name", (changeset, value) =>
            {
                if (value is string s && s.StartsWith("A"))
                    return changeset;
                return changeset.AddError("Name", "must start with A", "custom");
            });

        Assert.False(cs.IsValid);
        Assert.Equal("custom", cs.ErrorsOn("Name")[0].Code);
    }

    [Fact]
    public void Validate_WholeChangeset_CustomValidator()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com"
        };

        var cs = Changeset.Cast<User>(@params, ["Name", "Email"])
            .Validate(changeset =>
            {
                var name = changeset.GetChange<string>("Name");
                var email = changeset.GetChange<string>("Email");
                if (name != null && email != null && !email.Contains(name.ToLower()))
                    return changeset.AddBaseError("email must contain name", "consistency");
                return changeset;
            });

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void MultipleValidators_AccumulateErrors()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "",
            ["Email"] = "bad"
        };

        var cs = Changeset.Cast<User>(@params, ["Name", "Email"])
            .ValidateRequired(["Name", "Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
            .ValidateLength("Name", min: 2);

        Assert.False(cs.IsValid);
        Assert.True(cs.HasErrorOn("Name"));
        Assert.True(cs.HasErrorOn("Email"));
        // Name has required error, Email has format error
        Assert.True(cs.Errors.Length >= 2);
    }

    [Fact]
    public void Validators_DoNotMutate_Original()
    {
        var @params = new Dictionary<string, object?> { ["Name"] = "" };
        var cs = Changeset.Cast<User>(@params, ["Name"]);
        var validated = cs.ValidateRequired(["Name"]);

        Assert.True(cs.IsValid);       // original untouched
        Assert.False(validated.IsValid); // new instance has error
    }
}
