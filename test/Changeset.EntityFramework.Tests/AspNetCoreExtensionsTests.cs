using Changeset;
using Changeset.EntityFramework;
using Changeset.Validators;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Changeset.EntityFramework.Tests;

public class AspNetCoreExtensionsTests
{
    [Fact]
    public void ToValidationErrors_MapsErrorsByField()
    {
        var cs = Changeset<TestUser>.Cast(
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
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" },
            ["Name"]);

        Assert.Null(cs.ToValidationProblemOrNull());
    }

    [Fact]
    public void ToValidationProblemOrNull_InvalidChangeset_ReturnsResult()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "" },
            ["Name"])
            .ValidateRequired(["Name"]);

        var result = cs.ToValidationProblemOrNull();
        Assert.NotNull(result);
    }

    [Fact]
    public void ToProblemDetails_ContainsErrors()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "" },
            ["Name"])
            .ValidateRequired(["Name"]);

        var details = cs.ToProblemDetails();

        Assert.True(details.Errors.ContainsKey("Name"));
    }

    [Fact]
    public void AddToModelState_AddsAllErrors()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "", ["Email"] = "bad" },
            ["Name", "Email"])
            .ValidateRequired(["Name"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$");

        var modelState = new ModelStateDictionary();
        cs.AddToModelState(modelState);

        Assert.False(modelState.IsValid);
        Assert.True(modelState.ContainsKey("Name"));
        Assert.True(modelState.ContainsKey("Email"));
        Assert.Equal("can't be blank", modelState["Name"]!.Errors[0].ErrorMessage);
        Assert.Equal("has invalid format", modelState["Email"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public void AddToModelState_ValidChangeset_NoErrors()
    {
        var cs = Changeset<TestUser>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Alice" },
            ["Name"]);

        var modelState = new ModelStateDictionary();
        cs.AddToModelState(modelState);

        Assert.True(modelState.IsValid);
    }
}
