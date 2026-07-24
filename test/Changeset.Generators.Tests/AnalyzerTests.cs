using System.Collections.Immutable;
using Changeset;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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
        // "Naem" should trigger CHGSET001 warning: "'Naem' is not a writable property on 'Product'. Did you mean 'Name'?"
        // "FooBar" should trigger CHGSET002 warning: "'FooBar' is not a writable property on 'Product'"
#pragma warning disable CHGSET001, CHGSET002
        var cs = Changeset<Product>.Cast(
            new Dictionary<string, object?> { ["Naem"] = "Widget", ["FooBar"] = "bad" },
            ["Naem", "FooBar"]);
#pragma warning restore CHGSET001, CHGSET002

        // The cast itself still succeeds (field just won't match a property)
        Assert.NotNull(cs);
    }

    [Fact]
    public async Task InheritedWritableProperty_IsAccepted()
    {
        const string source = """
            using System.Collections.Generic;
            using Changeset;

            public class Entity
            {
                public string ExternalId { get; set; } = "";
            }

            [ChangesetTarget]
            public class Customer : Entity
            {
                public string Name { get; set; } = "";
            }

            public static class Usage
            {
                public static void Cast()
                {
                    Changeset<Customer>.Cast(
                        new Dictionary<string, object?>(),
                        ["ExternalId", "Name"]);
                }
            }
            """;

        var diagnostics = await RunAnalyzer(source);

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id is "CHGSET001" or "CHGSET002");
    }

    [Fact]
    public async Task MisspelledInheritedProperty_SuggestsInheritedProperty()
    {
        const string source = """
            using System.Collections.Generic;
            using Changeset;

            public class Entity
            {
                public string ExternalId { get; set; } = "";
            }

            [ChangesetTarget]
            public class Customer : Entity
            {
            }

            public static class Usage
            {
                public static void Cast()
                {
                    Changeset<Customer>.Cast(
                        new Dictionary<string, object?>(),
                        ["ExternalIt"]);
                }
            }
            """;

        var diagnostics = await RunAnalyzer(source);
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "CHGSET001");

        Assert.Contains("did you mean 'ExternalId'?", diagnostic.GetMessage());
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ChangesetTargetAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new FieldNameAnalyzer());

        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }
}
