using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Changeset.Generators.Tests;

/// <summary>
/// Exact-diagnostic tests for <see cref="ChangesetGenerator"/>. Sources mark the expected
/// diagnostic span with <c>[|...|]</c>; each test asserts the exact diagnostic ID, severity,
/// full formatted message, and source span, and that no other diagnostics are reported.
/// </summary>
public class GeneratorDiagnosticTests
{
    public static TheoryData<string, string, string> UnsupportedTargets => new()
    {
        {
            """
            using Changeset;

            [ChangesetTarget]
            public abstract class [|Target|]
            {
                public Target() { }

                public int Id { get; set; }
            }
            """,
            "CHANGESETGEN001",
            "Changeset target 'Target' is not supported: abstract types cannot be instantiated"
        },
        {
            """
            using Changeset;

            public class Container
            {
                [ChangesetTarget]
                public class [|Target|]
                {
                    public int Id { get; set; }
                }
            }
            """,
            "CHANGESETGEN001",
            "Changeset target 'Container.Target' is not supported: nested types are not supported"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class [|Target|]<T>
            {
                public int Id { get; set; }
            }
            """,
            "CHANGESETGEN001",
            "Changeset target 'Target<T>' is not supported: generic types are not supported"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class [|Target|]
            {
                public Target(int id) { }

                public int Id { get; set; }
            }
            """,
            "CHANGESETGEN001",
            "Changeset target 'Target' is not supported: an accessible parameterless constructor is required"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class [|Target|]
            {
                private Target() { }

                public int Id { get; set; }
            }
            """,
            "CHANGESETGEN001",
            "Changeset target 'Target' is not supported: an accessible parameterless constructor is required"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class [|Target|]
            {
                public int Id { get; set; }
            }

            internal class TargetChangesetApplier { }
            """,
            "CHANGESETGEN001",
            "Changeset target 'Target' is not supported: the generated type name 'TargetChangesetApplier' " +
            "or 'TargetChangesetApplierRegistrar' is already in use"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class [|Target|]
            {
                public int Id { get; set; }
            }

            internal class TargetChangesetApplierRegistrar { }
            """,
            "CHANGESETGEN001",
            "Changeset target 'Target' is not supported: the generated type name 'TargetChangesetApplier' " +
            "or 'TargetChangesetApplierRegistrar' is already in use"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public required string [|Name|] { get; set; }
            }
            """,
            "CHANGESETGEN002",
            "Property 'Name' on changeset target 'Target' is not supported: " +
            "required members cannot be initialized by the generated applier"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public string [|Name|] { get; init; } = "";
            }
            """,
            "CHANGESETGEN002",
            "Property 'Name' on changeset target 'Target' is not supported: " +
            "init-only properties cannot be assigned by the generated applier"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public string [|Name|] { get; } = "";
            }
            """,
            "CHANGESETGEN002",
            "Property 'Name' on changeset target 'Target' is not supported: a setter is required"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public string [|Name|] { get; private set; } = "";
            }
            """,
            "CHANGESETGEN002",
            "Property 'Name' on changeset target 'Target' is not supported: " +
            "the setter must be accessible from generated code"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public string [|Name|] { get; protected set; } = "";
            }
            """,
            "CHANGESETGEN002",
            "Property 'Name' on changeset target 'Target' is not supported: " +
            "the setter must be accessible from generated code"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public string [|Name|] { private get; set; } = "";
            }
            """,
            "CHANGESETGEN002",
            "Property 'Name' on changeset target 'Target' is not supported: " +
            "the getter must be accessible from generated code"
        },
        {
            """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public int [|this|][int index] { get => index; set { } }
            }
            """,
            "CHANGESETGEN002",
            "Property 'this[]' on changeset target 'Target' is not supported: indexers cannot be changeset fields"
        }
    };

    [Theory]
    [MemberData(nameof(UnsupportedTargets))]
    public void UnsupportedTarget_ReportsExactDiagnostic(
        string markup,
        string expectedId,
        string expectedMessage)
    {
        var (source, expectedSpan) = ParseMarkup(markup);

        var result = RunGenerator(source);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal(expectedSpan, diagnostic.Location.SourceSpan);
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void AbstractGenericTarget_ReportsBothTypeDiagnosticsAtTypeName()
    {
        var (source, expectedSpan) = ParseMarkup("""
            using Changeset;

            [ChangesetTarget]
            public abstract class [|Target|]<T>
            {
                public Target() { }

                public int Id { get; set; }
            }
            """);

        var result = RunGenerator(source);

        Assert.Collection(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal("CHANGESETGEN001", diagnostic.Id);
                Assert.Equal(
                    "Changeset target 'Target<T>' is not supported: abstract types cannot be instantiated",
                    diagnostic.GetMessage(CultureInfo.InvariantCulture));
                Assert.Equal(expectedSpan, diagnostic.Location.SourceSpan);
            },
            diagnostic =>
            {
                Assert.Equal("CHANGESETGEN001", diagnostic.Id);
                Assert.Equal(
                    "Changeset target 'Target<T>' is not supported: generic types are not supported",
                    diagnostic.GetMessage(CultureInfo.InvariantCulture));
                Assert.Equal(expectedSpan, diagnostic.Location.SourceSpan);
            });
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void AbstractTarget_WithImplicitProtectedConstructor_AlsoReportsConstructorDiagnostic()
    {
        var (source, expectedSpan) = ParseMarkup("""
            using Changeset;

            [ChangesetTarget]
            public abstract class [|Target|]
            {
                public int Id { get; set; }
            }
            """);

        var result = RunGenerator(source);

        Assert.Collection(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal("CHANGESETGEN001", diagnostic.Id);
                Assert.Equal(
                    "Changeset target 'Target' is not supported: abstract types cannot be instantiated",
                    diagnostic.GetMessage(CultureInfo.InvariantCulture));
                Assert.Equal(expectedSpan, diagnostic.Location.SourceSpan);
            },
            diagnostic =>
            {
                Assert.Equal("CHANGESETGEN001", diagnostic.Id);
                Assert.Equal(
                    "Changeset target 'Target' is not supported: an accessible parameterless constructor is required",
                    diagnostic.GetMessage(CultureInfo.InvariantCulture));
                Assert.Equal(expectedSpan, diagnostic.Location.SourceSpan);
            });
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void MultipleUnsupportedProperties_ReportOneDiagnosticEachAtPropertyName()
    {
        const string markup = """
            using Changeset;

            [ChangesetTarget]
            public class Target
            {
                public int Id { get; set; }
                public string [|First|] { get; init; } = "";
                public string [|Second|] { get; } = "";
            }
            """;
        var (source, spans) = ParseMultiMarkup(markup);

        var result = RunGenerator(source);

        Assert.Collection(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal("CHANGESETGEN002", diagnostic.Id);
                Assert.Equal(
                    "Property 'First' on changeset target 'Target' is not supported: " +
                    "init-only properties cannot be assigned by the generated applier",
                    diagnostic.GetMessage(CultureInfo.InvariantCulture));
                Assert.Equal(spans[0], diagnostic.Location.SourceSpan);
            },
            diagnostic =>
            {
                Assert.Equal("CHANGESETGEN002", diagnostic.Id);
                Assert.Equal(
                    "Property 'Second' on changeset target 'Target' is not supported: a setter is required",
                    diagnostic.GetMessage(CultureInfo.InvariantCulture));
                Assert.Equal(spans[1], diagnostic.Location.SourceSpan);
            });
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InvalidTarget_DoesNotBlockGenerationForValidTargets()
    {
        const string source = """
            using Changeset;

            [ChangesetTarget]
            public abstract class Broken
            {
                public Broken() { }

                public int Id { get; set; }
            }

            [ChangesetTarget]
            public class Valid
            {
                public int Id { get; set; }
            }
            """;

        var result = RunGenerator(source);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CHANGESETGEN001", diagnostic.Id);
        var generated = Assert.Single(result.GeneratedSources);
        Assert.Equal("Valid.ChangesetApplier.g.cs", generated.HintName);
    }

    [Fact]
    public void SupportedTarget_GeneratesExactSourceWithoutDiagnostics()
    {
        const string source = """
            using Changeset;

            namespace Store;

            [ChangesetTarget]
            public class Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """;

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        var generated = Assert.Single(result.GeneratedSources);
        Assert.Equal("Store.Product.ChangesetApplier.g.cs", generated.HintName);

        const string expected = """
            // <auto-generated />
            #nullable enable

            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            namespace Store;

            internal sealed class ProductChangesetApplier : global::Changeset.IChangesetApplier<global::Store.Product>
            {
                public static readonly ProductChangesetApplier Instance = new();

                private static readonly HashSet<string> _validFields = new(System.StringComparer.Ordinal)
                {
                    "Id",
                    "Name",
                };

                public IReadOnlySet<string> ValidFields => _validFields;

                public global::Store.Product Create(IReadOnlyDictionary<string, object?> changes)
                {
                    var target = new global::Store.Product();
                    SetProperties(target, changes);
                    return target;
                }

                public global::Store.Product Apply(global::Store.Product source, IReadOnlyDictionary<string, object?> changes)
                {
                    var target = new global::Store.Product();
                    target.Id = source.Id;
                    target.Name = source.Name;
                    SetProperties(target, changes);
                    return target;
                }

                private static void SetProperties(global::Store.Product target, IReadOnlyDictionary<string, object?> changes)
                {
                    foreach (var (field, value) in changes)
                    {
                        switch (field)
                        {
                            case "Id":
                                target.Id = (int)value!;
                                break;
                            case "Name":
                                target.Name = (string)value!;
                                break;
                        }
                    }
                }
            }

            internal static class ProductChangesetApplierRegistrar
            {
                [ModuleInitializer]
                internal static void Register()
                {
                    global::Changeset.ChangesetApplierRegistry.Register(ProductChangesetApplier.Instance);
                }
            }
            """;
        Assert.Equal(
            expected.ReplaceLineEndings("\n"),
            generated.SourceText.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void SupportedTarget_GlobalNamespace_ExcludesNonPublicStaticAndReadOnlyMembers()
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
        var generated = Assert.Single(result.GeneratedSources);
        Assert.Equal("Target.ChangesetApplier.g.cs", generated.HintName);

        var text = generated.SourceText.ToString();
        Assert.Contains("\"Id\",", text, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalValue", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Count", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportedTarget_GeneratedSourceCompilesWithoutErrors()
    {
        const string source = """
            using Changeset;

            namespace Store;

            [ChangesetTarget]
            public class Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """;

        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ChangesetGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static (string Source, TextSpan Span) ParseMarkup(string markup)
    {
        var (source, spans) = ParseMultiMarkup(markup);
        return (source, Assert.Single(spans));
    }

    private static (string Source, IReadOnlyList<TextSpan> Spans) ParseMultiMarkup(string markup)
    {
        var spans = new List<TextSpan>();
        var source = markup;

        while (true)
        {
            var start = source.IndexOf("[|", StringComparison.Ordinal);
            if (start < 0)
                break;

            var end = source.IndexOf("|]", start, StringComparison.Ordinal);
            Assert.True(end >= 0, "Unbalanced [| |] markup in test source.");

            source = source.Remove(end, 2).Remove(start, 2);
            spans.Add(TextSpan.FromBounds(start, end - 2));
        }

        Assert.NotEmpty(spans);
        return (source, spans);
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ChangesetGenerator().AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        return Assert.Single(driver.GetRunResult().Results);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ChangesetTargetAttribute).Assembly.Location));

        return CSharpCompilation.Create(
            "GeneratorDiagnosticTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}
