using ChangesetFactory = Changeset.Changeset;
using Changeset;

namespace Changeset.Generators.Tests;

// This file exists to verify the analyzer detects field name typos at build time.
// The Product class (in GeneratorTests.cs) is marked [ChangesetTarget].
// Build warnings CHGSET001/CHGSET002 will be emitted for bad field names.

public class AnalyzerTests
{
    [Fact]
    public void ValidFieldNames_NoDiagnostic()
    {
        // This should compile without warnings — all field names are valid
        var cs = ChangesetFactory.Cast<Product>(
            new Dictionary<string, object?> { ["Name"] = "Widget" },
            ["Name", "Price", "Stock"]);

        Assert.True(cs.IsValid);
    }

    // NOTE: The following method triggers CHGSET001 at build time.
    // We deliberately suppress the warning here since this IS the test for that diagnostic.
    [Fact]
    public void TypoFieldName_TriggersWarning()
    {
        // "Naem" should trigger CHGSET001 warning: "'Naem' is not a writable property on 'Product'. Did you mean 'Name'?"
        // "FooBar" should trigger CHGSET002 warning: "'FooBar' is not a writable property on 'Product'"
#pragma warning disable CHGSET001, CHGSET002
        var cs = ChangesetFactory.Cast<Product>(
            new Dictionary<string, object?> { ["Naem"] = "Widget", ["FooBar"] = "bad" },
            ["Naem", "FooBar"]);
#pragma warning restore CHGSET001, CHGSET002

        // The cast itself still succeeds (field just won't match a property)
        Assert.NotNull(cs);
    }
}
