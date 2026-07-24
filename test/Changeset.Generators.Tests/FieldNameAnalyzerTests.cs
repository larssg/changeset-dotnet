using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Changeset.Generators.Tests;

/// <summary>
/// Exact-diagnostic tests for <see cref="FieldNameAnalyzer"/> on the official Roslyn
/// analyzer test harness. Expected diagnostics are pinned to numbered markup spans
/// (<c>{|#n:...|}</c>) and verified with the exact severity, span, and full message;
/// the harness fails on any unexpected diagnostics.
/// </summary>
public class FieldNameAnalyzerTests
{
    private const string ProductModel = """
        using System.Collections.Generic;
        using Changeset;

        [ChangesetTarget]
        public class Product
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal Price { get; set; }
        }

        """;

    [Fact]
    public async Task ValidFieldNames_CollectionExpression_ReportsNothing()
    {
        var source = ProductModel + """
            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Product>.Cast(
                        new Dictionary<string, object?> { ["Naem"] = "params keys are not checked" },
                        ["Id", "Name", "Price"]);
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task CaseInsensitiveFieldNames_ReportNothing()
    {
        var source = ProductModel + """
            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Product>.Cast(
                        new Dictionary<string, object?>(),
                        ["name", "PRICE"]);
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task TypoAndUnknownFieldNames_CollectionExpression_ReportExactDiagnostics()
    {
        var source = ProductModel + """
            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Product>.Cast(
                        new Dictionary<string, object?>(),
                        [{|#0:"Naem"|}, {|#1:"Prise"|}, {|#2:"Wxyz"|}]);
                }
            }
            """;

        await Verify(
            source,
            Suggestion(0, "Naem", "Product", "Name"),
            Suggestion(1, "Prise", "Product", "Price"),
            Unknown(2, "Wxyz", "Product"));
    }

    [Fact]
    public async Task TypoFieldName_ImplicitArray_ReportsExactDiagnostic()
    {
        var source = ProductModel + """
            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Product>.Cast(
                        new Dictionary<string, object?>(),
                        new[] { {|#0:"Naem"|}, "Price" });
                }
            }
            """;

        await Verify(source, Suggestion(0, "Naem", "Product", "Name"));
    }

    [Fact]
    public async Task TypoFieldName_ExplicitArray_ReportsExactDiagnostic()
    {
        var source = ProductModel + """
            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Product>.Cast(
                        new Dictionary<string, object?>(),
                        new string[] { {|#0:"Naem"|} });
                }
            }
            """;

        await Verify(source, Suggestion(0, "Naem", "Product", "Name"));
    }

    [Fact]
    public async Task TypoFieldName_UpdateOverload_ReportsExactDiagnostic()
    {
        var source = ProductModel + """
            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Product>.Cast(
                        new Product(),
                        new Dictionary<string, object?>(),
                        [{|#0:"Naem"|}]);
                }
            }
            """;

        await Verify(source, Suggestion(0, "Naem", "Product", "Name"));
    }

    [Fact]
    public async Task NonWritableProperty_IsNotAValidFieldName()
    {
        var source = """
            using System.Collections.Generic;
            using Changeset;

            [ChangesetTarget]
            public class Order
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public decimal Subtotal { get; }
            }

            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Order>.Cast(
                        new Dictionary<string, object?>(),
                        [{|#0:"Subtotal"|}]);
                }
            }
            """;

        await Verify(source, Unknown(0, "Subtotal", "Order"));
    }

    [Fact]
    public async Task TypeWithoutChangesetTargetAttribute_ReportsNothing()
    {
        var source = """
            using System.Collections.Generic;
            using Changeset;

            public class Plain
            {
                public string Name { get; set; } = "";
            }

            public static class Usage
            {
                public static void Run()
                {
                    Changeset<Plain>.Cast(
                        new Dictionary<string, object?>(),
                        ["Naem", "Wxyz"]);
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task InheritedWritableProperty_IsAccepted()
    {
        var source = """
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
                public static void Run()
                {
                    Changeset<Customer>.Cast(
                        new Dictionary<string, object?>(),
                        ["ExternalId", "Name"]);
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task MisspelledInheritedProperty_SuggestsInheritedProperty()
    {
        var source = """
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
                public static void Run()
                {
                    Changeset<Customer>.Cast(
                        new Dictionary<string, object?>(),
                        [{|#0:"ExternalIt"|}]);
                }
            }
            """;

        await Verify(source, Suggestion(0, "ExternalIt", "Customer", "ExternalId"));
    }

    private static DiagnosticResult Suggestion(int markupKey, string field, string type, string suggestion) =>
        new DiagnosticResult("CHGSET001", DiagnosticSeverity.Warning)
            .WithLocation(markupKey)
            .WithMessage($"'{field}' is not a writable property on '{type}'; did you mean '{suggestion}'?");

    private static DiagnosticResult Unknown(int markupKey, string field, string type) =>
        new DiagnosticResult("CHGSET002", DiagnosticSeverity.Warning)
            .WithLocation(markupKey)
            .WithMessage($"'{field}' is not a writable property on '{type}'");

    private static Task Verify(string source, params DiagnosticResult[] expected)
    {
        var test = new AnalyzerHarnessTest { TestCode = source };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private sealed class AnalyzerHarnessTest : CSharpAnalyzerTest<FieldNameAnalyzer, DefaultVerifier>
    {
        public AnalyzerHarnessTest()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100;
            TestState.AdditionalReferences.Add(typeof(ChangesetTargetAttribute).Assembly);
        }

        protected override CompilationOptions CreateCompilationOptions() =>
            ((CSharpCompilationOptions)base.CreateCompilationOptions())
                .WithNullableContextOptions(NullableContextOptions.Enable);

        protected override ParseOptions CreateParseOptions() =>
            ((CSharpParseOptions)base.CreateParseOptions())
                .WithLanguageVersion(LanguageVersion.Latest);
    }
}
