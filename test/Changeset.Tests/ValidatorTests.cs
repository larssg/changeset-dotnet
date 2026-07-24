using Changeset.Validators;
using System.Text.RegularExpressions;

namespace Changeset.Tests;

public partial class ValidatorTests
{
    [GeneratedRegex(@"^[^@]+@[^@]+\.[^@]+$")]
    private static partial Regex EmailRegex();

    [Fact]
    public void ValidateRequired_Present_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateRequired(["Name"]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateRequired_Missing_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?>(), ["Name"])
            .ValidateRequired(["Name"]);

        Assert.False(cs.IsValid);
        Assert.Equal("required", cs.ErrorsOn("Name")[0].Code);
        Assert.Equal("can't be blank", cs.ErrorsOn("Name")[0].Message);
    }

    [Fact]
    public void ValidateRequired_Null_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["BirthDate"] = null }, ["BirthDate"])
            .ValidateRequired(["BirthDate"]);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateRequired_EmptyString_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "" }, ["Name"])
            .ValidateRequired(["Name"]);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateRequired_WhitespaceString_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "   " }, ["Name"],
            new CastOptions { TrimStrings = false })
            .ValidateRequired(["Name"]);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateFormat_ValidEmail_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Email"] = "alice@example.com" }, ["Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateFormat_InvalidEmail_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Email"] = "not-an-email" }, ["Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        Assert.False(cs.IsValid);
        Assert.Equal("format", cs.ErrorsOn("Email")[0].Code);
    }

    [Fact]
    public void ValidateFormat_GeneratedRegex_UsesGeneratedRegex()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Email"] = "not-an-email" }, ["Email"])
            .ValidateFormat(user => user.Email, EmailRegex());

        Assert.False(cs.IsValid);
        Assert.Equal(@"^[^@]+@[^@]+\.[^@]+$",
            cs.ErrorsOn("Email")[0].Metadata!["pattern"]);
    }

    [Fact]
    public void ValidateFormat_FieldNotInChanges_Skipped()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?>(), ["Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_Min_TooShort_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "A" }, ["Name"])
            .ValidateLength("Name", min: 2);

        Assert.False(cs.IsValid);
        Assert.Equal("length", cs.ErrorsOn("Name")[0].Code);
        Assert.Contains("at least 2", cs.ErrorsOn("Name")[0].Message);
    }

    [Fact]
    public void ValidateLength_Max_TooLong_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = new string('A', 101) }, ["Name"])
            .ValidateLength("Name", max: 100);

        Assert.False(cs.IsValid);
        Assert.Contains("at most 100", cs.ErrorsOn("Name")[0].Message);
    }

    [Fact]
    public void ValidateLength_Exact_WrongLength_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "ABC" }, ["Name"])
            .ValidateLength("Name", @is: 5);

        Assert.False(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_WithinBounds_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateLength("Name", min: 2, max: 100);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_PropertyExpression_TooShort_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "A" }, ["Name"])
            .ValidateLength(c => c.Name, min: 2, max: 100);

        Assert.False(cs.IsValid);
        Assert.Equal("length", cs.ErrorsOn("Name")[0].Code);
    }

    [Fact]
    public void ValidateLength_PropertyExpression_WithinBounds_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateLength(c => c.Name, min: 2, max: 100);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_EnumerableWithMax_StopsAfterFirstInvalidItem()
    {
        var items = new CountingEnumerable(100);

        var cs = Changeset<SequenceModel>.Cast(
                new Dictionary<string, object?> { ["Items"] = items }, ["Items"])
            .ValidateLength(model => model.Items, max: 5);

        Assert.False(cs.IsValid);
        Assert.Equal(6, items.EnumeratedCount);
        Assert.Equal("length", cs.ErrorsOn("Items").Single().Code);
    }

    [Fact]
    public void ValidateLength_EnumerablePreservesMinBeforeMaxErrorOrder()
    {
        var items = new CountingEnumerable(100);

        var cs = Changeset<SequenceModel>.Cast(
                new Dictionary<string, object?> { ["Items"] = items }, ["Items"])
            .ValidateLength(model => model.Items, min: 10, max: 5);

        Assert.False(cs.IsValid);
        Assert.Equal(10, items.EnumeratedCount);
        Assert.Contains("at most 5", cs.ErrorsOn("Items").Single().Message);
    }

    [Fact]
    public void PropertyExpressionOverloads_ValidateFields()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "admin",
            ["Email"] = "invalid",
            ["Email_confirmation"] = "different",
            ["Age"] = 200,
            ["Role"] = UserRole.Guest
        };

        var cs = Changeset<User>.Cast(
                @params, u => new { u.Name, u.Email, u.Age, u.Role })
            .ValidateRequired(u => new { u.Name, u.Email })
            .ValidateFormat(u => u.Email, @"^[^@]+@[^@]+\.[^@]+$")
            .ValidateNumber(u => u.Age, lessThan: 150)
            .ValidateInclusion(u => u.Role, [UserRole.Member, UserRole.Admin])
            .ValidateExclusion(u => u.Name, ["admin", "root"])
            .ValidateConfirmation(u => u.Email)
            .ValidateChange(u => u.Name, (changeset, _) =>
                changeset.AddError("Name", "custom failure", "custom"));

        Assert.Equal("format", cs.ErrorsOn("Email").Single().Code);
        Assert.Equal("number", cs.ErrorsOn("Age").Single().Code);
        Assert.Equal("inclusion", cs.ErrorsOn("Role").Single().Code);
        Assert.Equal(["exclusion", "custom"],
            cs.ErrorsOn("Name").Select(error => error.Code));
        Assert.Equal("confirmation", cs.ErrorsOn("Email_confirmation").Single().Code);
    }

    [Fact]
    public void ValidateRequired_PropertyExpression_MissingField_Fails()
    {
        var cs = Changeset<User>.Cast(
                new Dictionary<string, object?>(), u => new { u.Name, u.Email })
            .ValidateRequired(u => new { u.Name, u.Email });

        Assert.Equal(2, cs.Errors.Length);
        Assert.All(cs.Errors, error => Assert.Equal("required", error.Code));
    }

    [Fact]
    public async Task ValidateChangeAsync_PropertyExpression_WorksAcrossTaskPipeline()
    {
        var cs = await Changeset<User>.Cast(
                new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateChangeAsync(u => u.Name, (changeset, _) =>
                Task.FromResult(changeset))
            .ValidateChangeAsync(u => u.Name, (changeset, _) =>
                Task.FromResult(changeset.AddError("Name", "async failure", "async")));

        Assert.Equal("async", cs.ErrorsOn("Name").Single().Code);
    }

    [Fact]
    public void ValidateNumber_GreaterThanOrEqual_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = 18 }, ["Age"])
            .ValidateNumber("Age", greaterThanOrEqual: 0);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateNumber_LessThan_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = 200 }, ["Age"])
            .ValidateNumber("Age", lessThan: 150);

        Assert.False(cs.IsValid);
        Assert.Equal("number", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void ValidateInclusion_Valid_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Role"] = UserRole.Admin }, ["Role"])
            .ValidateInclusion("Role", [UserRole.Member, UserRole.Admin]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateInclusion_Invalid_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Role"] = UserRole.Guest }, ["Role"])
            .ValidateInclusion("Role", [UserRole.Member, UserRole.Admin]);

        Assert.False(cs.IsValid);
        Assert.Equal("inclusion", cs.ErrorsOn("Role")[0].Code);
    }

    [Fact]
    public void ValidateInclusion_PropertyExpression_UsesSetLookup()
    {
        var allowed = new TrackingReadOnlySet<UserRole>(UserRole.Admin);

        var cs = Changeset<User>.Cast(
                new Dictionary<string, object?> { ["Role"] = UserRole.Admin }, ["Role"])
            .ValidateInclusion(user => user.Role, allowed);

        Assert.True(cs.IsValid);
        Assert.Equal(1, allowed.ContainsCalls);
    }

    [Fact]
    public void ValidateInclusion_PropertyExpression_AllowsNullMember()
    {
        var cs = Changeset<User>.Cast(
                new Dictionary<string, object?> { ["BirthDate"] = null }, ["BirthDate"])
            .ValidateInclusion(user => user.BirthDate, [null]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateExclusion_Excluded_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "admin" }, ["Name"])
            .ValidateExclusion("Name", ["admin", "root", "superuser"]);

        Assert.False(cs.IsValid);
        Assert.Equal("exclusion", cs.ErrorsOn("Name")[0].Code);
    }

    [Fact]
    public void ValidateExclusion_NotExcluded_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" }, ["Name"])
            .ValidateExclusion("Name", ["admin", "root"]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateExclusion_PropertyExpression_UsesSetLookup()
    {
        var excluded = new TrackingReadOnlySet<UserRole>(UserRole.Admin);

        var cs = Changeset<User>.Cast(
                new Dictionary<string, object?> { ["Role"] = UserRole.Admin }, ["Role"])
            .ValidateExclusion(user => user.Role, excluded);

        Assert.False(cs.IsValid);
        Assert.Equal(1, excluded.ContainsCalls);
        Assert.Equal("exclusion", cs.ErrorsOn("Role").Single().Code);
    }

    [Fact]
    public void ValidateConfirmation_Matching_Passes()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Email"] = "alice@example.com",
            ["Email_confirmation"] = "alice@example.com"
        };

        var cs = Changeset<User>.Cast(@params, ["Email"])
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

        var cs = Changeset<User>.Cast(@params, ["Email"])
            .ValidateConfirmation("Email");

        Assert.False(cs.IsValid);
        Assert.Equal("confirmation", cs.ErrorsOn("Email_confirmation")[0].Code);
    }

    [Fact]
    public void ValidateChange_CustomValidator_Passes()
    {
        var cs = Changeset<User>.Cast(
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
        var cs = Changeset<User>.Cast(
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

        var cs = Changeset<User>.Cast(@params, ["Name", "Email"])
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

        var cs = Changeset<User>.Cast(@params, ["Name", "Email"])
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
        var cs = Changeset<User>.Cast(@params, ["Name"]);
        var validated = cs.ValidateRequired(["Name"]);

        Assert.True(cs.IsValid);       // original untouched
        Assert.False(validated.IsValid); // new instance has error
    }

    [Fact]
    public void ValidateRequired_ZeroInt_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = 0 }, ["Age"])
            .ValidateRequired(["Age"]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateRequired_FalseBool_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["IsActive"] = false }, ["IsActive"])
            .ValidateRequired(["IsActive"]);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_NullValueInChanges_Skips()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["BirthDate"] = null }, ["BirthDate"])
            .ValidateLength("BirthDate", min: 1);

        // null is not a string/collection/enumerable — should skip validation
        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateLength_NonStringNonCollection_Skips()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = 42 }, ["Age"])
            .ValidateLength("Age", min: 1);

        // int is not string/collection/enumerable — should return changeset as-is
        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateNumber_ExactlyEqualToGreaterThan_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = 18 }, ["Age"])
            .ValidateNumber("Age", greaterThan: 18);

        Assert.False(cs.IsValid);
        Assert.Equal("number", cs.ErrorsOn("Age")[0].Code);
    }

    [Fact]
    public void ValidateNumber_ExactlyEqualToGreaterThanOrEqual_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Age"] = 18 }, ["Age"])
            .ValidateNumber("Age", greaterThanOrEqual: 18);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateFormat_NullValueInChanges_Skips()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["BirthDate"] = null }, ["BirthDate"])
            .ValidateFormat("BirthDate", @"^\d+$");

        // null is not a string — TryGetValue succeeds but value is not string, so skipped
        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateConfirmation_ConfirmationPresent_MainFieldNotInChanges_Skips()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Email_confirmation"] = "alice@example.com"
        };

        var cs = Changeset<User>.Cast(@params, ["Email"])
            .ValidateConfirmation("Email");

        // Email not in changes, so confirmation validation is skipped
        Assert.True(cs.IsValid);
    }

    [Fact]
    public void ValidateInclusion_EmptyAllowedSet_Fails()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Role"] = UserRole.Admin }, ["Role"])
            .ValidateInclusion("Role", []);

        Assert.False(cs.IsValid);
        Assert.Equal("inclusion", cs.ErrorsOn("Role")[0].Code);
    }

    [Fact]
    public void ValidateExclusion_EmptyDisallowedSet_Passes()
    {
        var cs = Changeset<User>.Cast(
            new Dictionary<string, object?> { ["Name"] = "anything" }, ["Name"])
            .ValidateExclusion("Name", []);

        Assert.True(cs.IsValid);
    }

    [Fact]
    public async Task ChainAsyncAndSyncValidators_InOnePipeline()
    {
        var @params = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Email"] = "alice@example.com"
        };

        var cs = await Changeset<User>.Cast(@params, ["Name", "Email"])
            .ValidateRequired(["Name", "Email"])
            .ValidateChangeAsync("Name", async (changeset, value) =>
            {
                await Task.Delay(1); // simulate async check
                if (value is string s && s.Length < 2)
                    return changeset.AddError("Name", "too short", "async_length");
                return changeset;
            })
            .ValidateAsync(c => c.ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$"));

        Assert.True(cs.IsValid);
    }

    private sealed class CountingEnumerable(int count) : IEnumerable<int>
    {
        public int EnumeratedCount { get; private set; }

        public IEnumerator<int> GetEnumerator()
        {
            for (var value = 0; value < count; value++)
            {
                EnumeratedCount++;
                yield return value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    private sealed class TrackingReadOnlySet<T>(T value) : IReadOnlySet<T>
    {
        public int ContainsCalls { get; private set; }
        public int Count => 1;

        public bool Contains(T item)
        {
            ContainsCalls++;
            return EqualityComparer<T>.Default.Equals(value, item);
        }

        public IEnumerator<T> GetEnumerator() =>
            throw new InvalidOperationException("Set lookup should not enumerate.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsProperSupersetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsSubsetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsSupersetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool Overlaps(IEnumerable<T> other) => throw new NotSupportedException();
        public bool SetEquals(IEnumerable<T> other) => throw new NotSupportedException();
    }
}
