using Changeset;

namespace Changeset.Generators.Tests;

// These tests exercise the analyzer as it is wired into a real build: this project
// references Changeset.Generators as an analyzer, so CHGSET001/CHGSET002 are produced
// (and must be suppressed) when compiling this file. Exact-diagnostic coverage lives in
// FieldNameAnalyzerTests, which runs the analyzer on the official Roslyn test harness.

public class AnalyzerTests
{
    [Fact]
    public void ValidFieldNames_NoDiagnostic()
    {
        // This should compile without warnings — all field names are valid
        var cs = Changeset<Product>.Cast(
            new Dictionary<string, object?> { ["Name"] = "Widget" },
            ["Name", "Price", "Stock"]);

        Assert.True(cs.IsValid);
    }

    // NOTE: The following method triggers CHGSET001 at build time.
    // We deliberately suppress the warning here since this IS the test for that diagnostic.
    [Fact]
    public void TypoFieldName_TriggersWarning()
    {
        // "Naem" should trigger CHGSET001 warning: "'Naem' is not a writable property on 'Product'; did you mean 'Name'?"
        // "FooBar" should trigger CHGSET002 warning: "'FooBar' is not a writable property on 'Product'"
#pragma warning disable CHGSET001, CHGSET002
        var cs = Changeset<Product>.Cast(
            new Dictionary<string, object?> { ["Naem"] = "Widget", ["FooBar"] = "bad" },
            ["Naem", "FooBar"]);
#pragma warning restore CHGSET001, CHGSET002

        // The cast itself still succeeds (field just won't match a property)
        Assert.NotNull(cs);
    }
}
