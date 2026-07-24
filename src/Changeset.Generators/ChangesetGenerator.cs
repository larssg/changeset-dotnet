using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Changeset.Generators;

/// <summary>
/// Incremental source generator that emits a changeset applier for each class or record
/// decorated with <c>[ChangesetTarget]</c>. The generated applier creates and copies
/// instances, applies changes by field name, and is registered with the changeset applier
/// registry via a module initializer. Unsupported targets or properties are reported as
/// <c>CHANGESETGEN001</c> and <c>CHANGESETGEN002</c> diagnostics instead of generating code.
/// </summary>
[Generator]
public class ChangesetGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Changeset.ChangesetTargetAttribute";

    private static readonly DiagnosticDescriptor UnsupportedType = new(
        "CHANGESETGEN001",
        "Unsupported changeset target type",
        "Changeset target '{0}' is not supported: {1}",
        "Changeset.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedProperty = new(
        "CHANGESETGEN002",
        "Unsupported changeset target property",
        "Property '{0}' on changeset target '{1}' is not supported: {2}",
        "Changeset.Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Registers the incremental pipeline: finds types decorated with
    /// <c>[ChangesetTarget]</c>, reports diagnostics for unsupported targets, and adds a
    /// generated <c>ChangesetApplier</c> source file for each valid target.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes and records decorated with [ChangesetTarget]
        var classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, _) => GetModelInfo(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(classDeclarations, static (spc, model) =>
        {
            foreach (var diagnostic in model.Diagnostics)
                spc.ReportDiagnostic(diagnostic);

            if (model.Diagnostics.Length != 0)
                return;

            var source = GenerateSource(model);
            var hintName = (model.Namespace is not null ? $"{model.Namespace}.{model.TypeName}" : model.TypeName)
                .Replace("::", ".");
            spc.AddSource($"{hintName}.ChangesetApplier.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static ModelInfo? GetModelInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var typeLocation = typeSymbol.Locations.FirstOrDefault();
        var displayName = typeSymbol.ToDisplayString();

        if (typeSymbol.IsAbstract)
            AddTypeDiagnostic("abstract types cannot be instantiated");

        if (typeSymbol.ContainingType is not null)
            AddTypeDiagnostic("nested types are not supported");

        if (typeSymbol.Arity != 0)
            AddTypeDiagnostic("generic types are not supported");

        var generatedTypeName = $"{typeSymbol.Name}ChangesetApplier";
        var generatedRegistrarName = $"{generatedTypeName}Registrar";
        if (typeSymbol.ContainingNamespace.GetTypeMembers(generatedTypeName).Length != 0 ||
            typeSymbol.ContainingNamespace.GetTypeMembers(generatedRegistrarName).Length != 0)
        {
            AddTypeDiagnostic(
                $"the generated type name '{generatedTypeName}' or '{generatedRegistrarName}' is already in use");
        }

        var parameterlessConstructor = typeSymbol.InstanceConstructors.FirstOrDefault(
            constructor => constructor.Parameters.Length == 0 &&
                constructor.DeclaredAccessibility is Accessibility.Public
                    or Accessibility.Internal
                    or Accessibility.ProtectedOrInternal);
        if (parameterlessConstructor is null)
            AddTypeDiagnostic("an accessible parameterless constructor is required");

        var properties = new List<PropertyInfo>();
        foreach (var prop in TargetPropertyInspector.GetPublicInstanceProperties(typeSymbol))
        {
            var reason = TargetPropertyInspector.GetUnsupportedReason(prop);
            if (reason is not null)
            {
                diagnostics.Add(Diagnostic.Create(
                    UnsupportedProperty,
                    prop.Locations.FirstOrDefault() ?? typeLocation,
                    prop.Name,
                    displayName,
                    reason));
                continue;
            }

            properties.Add(new PropertyInfo(
                prop.Name,
                prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new ModelInfo(
            typeSymbol.Name,
            ns,
            typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            properties.ToImmutableArray(),
            diagnostics.ToImmutable());

        void AddTypeDiagnostic(string reason)
        {
            diagnostics.Add(Diagnostic.Create(
                UnsupportedType,
                typeLocation,
                displayName,
                reason));
        }
    }

    private static string GenerateSource(ModelInfo model)
    {
        var applierClass = $"{model.TypeName}ChangesetApplier";
        var fullType = model.FullName;

        var namespaceDecl = model.Namespace is not null
            ? $"namespace {model.Namespace};\n\n"
            : "";

        var validFields = string.Join("\n",
            model.Properties.Select(p => $"        \"{p.Name}\","));

        var copyProps = string.Join("\n",
            model.Properties.Select(p => $"        target.{p.Name} = source.{p.Name};"));

        var switchCases = string.Join("\n",
            model.Properties.Select(p =>
                $"                case \"{p.Name}\":\n" +
                $"                    target.{p.Name} = ({p.TypeFullName})value!;\n" +
                $"                    break;"));

        return $$"""
            // <auto-generated />
            #nullable enable

            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            {{namespaceDecl}}internal sealed class {{applierClass}} : global::Changeset.IChangesetApplier<{{fullType}}>
            {
                public static readonly {{applierClass}} Instance = new();

                private static readonly HashSet<string> _validFields = new(System.StringComparer.Ordinal)
                {
            {{validFields}}
                };

                public IReadOnlySet<string> ValidFields => _validFields;

                public {{fullType}} Create(IReadOnlyDictionary<string, object?> changes)
                {
                    var target = new {{fullType}}();
                    SetProperties(target, changes);
                    return target;
                }

                public {{fullType}} Apply({{fullType}} source, IReadOnlyDictionary<string, object?> changes)
                {
                    var target = new {{fullType}}();
            {{copyProps}}
                    SetProperties(target, changes);
                    return target;
                }

                private static void SetProperties({{fullType}} target, IReadOnlyDictionary<string, object?> changes)
                {
                    foreach (var (field, value) in changes)
                    {
                        switch (field)
                        {
            {{switchCases}}
                        }
                    }
                }
            }

            internal static class {{applierClass}}Registrar
            {
                [ModuleInitializer]
                internal static void Register()
                {
                    global::Changeset.ChangesetApplierRegistry.Register({{applierClass}}.Instance);
                }
            }
            """;
    }
}

internal class ModelInfo
{
    public string TypeName { get; }
    public string? Namespace { get; }
    public string FullName { get; }
    public ImmutableArray<PropertyInfo> Properties { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public ModelInfo(
        string typeName,
        string? ns,
        string fullName,
        ImmutableArray<PropertyInfo> properties,
        ImmutableArray<Diagnostic> diagnostics)
    {
        TypeName = typeName;
        Namespace = ns;
        FullName = fullName;
        Properties = properties;
        Diagnostics = diagnostics;
    }
}

internal class PropertyInfo
{
    public string Name { get; }
    public string TypeFullName { get; }

    public PropertyInfo(string name, string typeFullName)
    {
        Name = name;
        TypeFullName = typeFullName;
    }
}
