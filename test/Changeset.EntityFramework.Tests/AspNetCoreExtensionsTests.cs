using ChangesetFactory = Changeset.Changeset;
using Changeset.EntityFramework;
using Changeset.Validators;

namespace Changeset.EntityFramework.Tests;

public class AspNetCoreExtensionsTests
{
    [Fact]
    public void ToValidationErrors_MapsErrorsByField()
    {
        var cs = ChangesetFactory.Cast<TestUser>(
            new Dictionary<string, object?> { ["Name"] = "", ["Email"] = "bad" },
            ["Name", "Email"])
            .ValidateRequired(["Name"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        var errors = cs.ToValidationErrors();

        Assert.True(errors.ContainsKey("Name"));
        Assert.True(errors.ContainsKey("Email"));
        Assert.Contains("can't be blank", errors["Name"]);
        Assert.Contains("has invalid format", errors["Email"]);
    }

    [Fact]
    public void ToValidationProblemOrNull_ValidChangeset_ReturnsNull()
    {
        var cs = ChangesetFactory.Cast<TestUser>(
            new Dictionary<string, object?> { ["Name"] = "Alice" },
            ["Name"]);

        Assert.Null(cs.ToValidationProblemOrNull());
    }

    [Fact]
    public void ToValidationProblemOrNull_InvalidChangeset_ReturnsResult()
    {
        var cs = ChangesetFactory.Cast<TestUser>(
            new Dictionary<string, object?> { ["Name"] = "" },
            ["Name"])
            .ValidateRequired(["Name"]);

        var result = cs.ToValidationProblemOrNull();
        Assert.NotNull(result);
    }

    [Fact]
    public void ToProblemDetails_ContainsErrors()
    {
        var cs = ChangesetFactory.Cast<TestUser>(
            new Dictionary<string, object?> { ["Name"] = "" },
            ["Name"])
            .ValidateRequired(["Name"]);

        var details = cs.ToProblemDetails();

        Assert.True(details.Errors.ContainsKey("Name"));
    }
}
