using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Changeset.Generators;

internal static class TargetPropertyInspector
{
    public static IEnumerable<IPropertySymbol> GetPublicInstanceProperties(INamedTypeSymbol targetType)
    {
        var seen = new HashSet<string>();

        for (var type = targetType;
             type is not null && type.SpecialType != SpecialType.System_Object;
             type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol property &&
                    property.DeclaredAccessibility == Accessibility.Public &&
                    !property.IsStatic &&
                    seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    public static string? GetUnsupportedReason(IPropertySymbol property)
    {
        if (property.IsIndexer)
            return "indexers cannot be changeset fields";
        if (property.IsRequired)
            return "required members cannot be initialized by the generated applier";
        if (property.SetMethod is null)
            return "a setter is required";
        if (property.SetMethod.IsInitOnly)
            return "init-only properties cannot be assigned by the generated applier";
        if (!IsAccessible(property.SetMethod.DeclaredAccessibility))
            return "the setter must be accessible from generated code";
        if (property.GetMethod is null || !IsAccessible(property.GetMethod.DeclaredAccessibility))
            return "the getter must be accessible from generated code";

        return null;
    }

    private static bool IsAccessible(Accessibility accessibility) =>
        accessibility is Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal;
}
