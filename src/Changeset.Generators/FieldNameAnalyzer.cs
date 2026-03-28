using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Changeset.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FieldNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CHGSET001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Invalid changeset field name",
        "'{0}' is not a writable property on '{1}'; did you mean '{2}'?",
        "Changeset.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field names passed to Changeset.Cast must match writable properties on the target type.");

    private static readonly DiagnosticDescriptor UnknownRule = new(
        "CHGSET002",
        "Unknown changeset field name",
        "'{0}' is not a writable property on '{1}'",
        "Changeset.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field names passed to Changeset.Cast must match writable properties on the target type.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule, UnknownRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a Changeset.Cast call
        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (methodSymbol is null)
            return;

        if (methodSymbol.Name != "Cast")
            return;

        var containingType = methodSymbol.ContainingType;
        if (containingType.Name != "Changeset" ||
            containingType.ContainingNamespace.ToDisplayString() != "Changeset" ||
            !containingType.IsGenericType)
            return;

        // Get the type argument T from the containing Changeset<T>
        var targetType = containingType.TypeArguments[0] as INamedTypeSymbol;
        if (targetType is null)
            return;

        // Check if T has [ChangesetTarget]
        var hasAttribute = targetType.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "Changeset.ChangesetTargetAttribute");
        if (!hasAttribute)
            return;

        // Get writable properties
        var validFields = new HashSet<string>();
        foreach (var member in targetType.GetMembers())
        {
            if (member is IPropertySymbol prop &&
                prop.DeclaredAccessibility == Accessibility.Public &&
                !prop.IsReadOnly &&
                !prop.IsStatic &&
                prop.SetMethod is not null)
            {
                validFields.Add(prop.Name);
            }
        }

        // Find the permitted fields argument (IReadOnlyList<string>)
        // It could be arg index 0 (insert) or 1 (update)
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var argExpr = arg.Expression;

            // Look for collection expressions: ["Name", "Emal"]
            if (argExpr is CollectionExpressionSyntax collection)
            {
                CheckFieldNames(context, collection.Elements, targetType, validFields);
            }
            // Look for array/list initializers
            else if (argExpr is ImplicitArrayCreationExpressionSyntax implicitArray &&
                     implicitArray.Initializer is not null)
            {
                CheckFieldNamesFromExpressions(context, implicitArray.Initializer.Expressions, targetType, validFields);
            }
            else if (argExpr is ArrayCreationExpressionSyntax arrayCreation &&
                     arrayCreation.Initializer is not null)
            {
                CheckFieldNamesFromExpressions(context, arrayCreation.Initializer.Expressions, targetType, validFields);
            }
        }
    }

    private static void CheckFieldNames(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<CollectionElementSyntax> elements,
        INamedTypeSymbol targetType,
        HashSet<string> validFields)
    {
        foreach (var element in elements)
        {
            if (element is ExpressionElementSyntax exprElement &&
                exprElement.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var fieldName = literal.Token.ValueText;
                CheckSingleFieldName(context, literal, fieldName, targetType, validFields);
            }
        }
    }

    private static void CheckFieldNamesFromExpressions(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ExpressionSyntax> expressions,
        INamedTypeSymbol targetType,
        HashSet<string> validFields)
    {
        foreach (var expr in expressions)
        {
            if (expr is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var fieldName = literal.Token.ValueText;
                CheckSingleFieldName(context, literal, fieldName, targetType, validFields);
            }
        }
    }

    private static void CheckSingleFieldName(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node,
        string fieldName,
        INamedTypeSymbol targetType,
        HashSet<string> validFields)
    {
        if (validFields.Contains(fieldName))
            return;

        // Case-insensitive match
        var caseMatch = validFields.FirstOrDefault(f =>
            string.Equals(f, fieldName, System.StringComparison.OrdinalIgnoreCase));
        if (caseMatch is not null)
            return; // Case-insensitive is allowed by default

        // Try to find a close match for the suggestion
        var suggestion = FindClosestMatch(fieldName, validFields);

        if (suggestion is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, node.GetLocation(), fieldName, targetType.Name, suggestion));
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnknownRule, node.GetLocation(), fieldName, targetType.Name));
        }
    }

    private static string? FindClosestMatch(string input, HashSet<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in candidates)
        {
            var distance = LevenshteinDistance(input.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (distance < bestDistance && distance <= 3) // Only suggest if reasonably close
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = System.Math.Min(
                    System.Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
