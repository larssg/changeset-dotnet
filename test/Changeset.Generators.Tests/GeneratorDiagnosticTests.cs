using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Changeset.Generators.Tests;

public class GeneratorDiagnosticTests
{
    public static TheoryData<string, string, string> UnsupportedTargets => new()
    {
        {
            "public abstract class Target { public int Id { get; set; } }",
            "CHANGESETGEN001",
            "abstract types cannot be instantiated"
        },
        {
            "public class Target<T> { public int Id { get; set; } }",
            "CHANGESETGEN001",
            "generic types are not supported"
        },
        {
            "public class Container { [ChangesetTarget] public class Target { public int Id { get; set; } } }",
            "CHANGESETGEN001",
            "nested types are not supported"
        },
        {
            "public class Target { public Target(int id) { } public int Id { get; set; } }",
            "CHANGESETGEN001",
            "an accessible parameterless constructor is required"
        },
        {
            "public class Target { public required string Name { get; set; } }",
            "CHANGESETGEN002",
            "required members cannot be initialized"
        },
        {
            "public class Target { public string Name { get; init; } = \"\"; }",
            "CHANGESETGEN002",
            "init-only properties cannot be assigned"
        },
        {
            "public class Target { public string Name { get; private set; } = \"\"; }",
            "CHANGESETGEN002",
            "the setter must be accessible"
        },
        {
            "public class Target { public int this[int index] { get => index; set { } } }",
            "CHANGESETGEN002",
            "indexers cannot be changeset fields"
        },
        {
            "public class Target { public int Id { get; set; } } internal class TargetChangesetApplier { }",
            "CHANGESETGEN001",
            "generated type name 'TargetChangesetApplier'"
        }
    };

    [Theory]
    [MemberData(nameof(UnsupportedTargets))]
    public void UnsupportedTarget_ReportsFocusedDiagnostic(
        string targetDeclaration,
        string expectedId,
        string expectedMessage)
    {
        var attribute = targetDeclaration.Contains("[ChangesetTarget]", StringComparison.Ordinal)
            ? ""
            : "[ChangesetTarget]";
        var source = $$"""
            using Changeset;

            {{attribute}}
            {{targetDeclaration}}
            """;

        var result = RunGenerator(source);
        var diagnostic = Assert.Single(result.Diagnostics, item =>
            item.Id == expectedId && item.GetMessage().Contains(expectedMessage, StringComparison.Ordinal));

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SupportedTarget_GeneratesWithoutDiagnostics()
    {
        const string source = """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public int Id { get; set; }
                internal string InternalValue { get; set; } = "";
                public static int Count { get; set; }
            }
            """;

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedSources);
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ChangesetTargetAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "GeneratorDiagnosticTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ChangesetGenerator().AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        return Assert.Single(driver.GetRunResult().Results);
    }
}
