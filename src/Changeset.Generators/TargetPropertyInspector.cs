using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Changeset.Generators;

/// <summary>
/// Shared helper for the generator and analyzer that discovers the public instance
/// properties of a changeset target type and decides which of them can be changeset fields.
/// </summary>
internal static class TargetPropertyInspector
{
    /// <summary>
    /// Enumerates the public instance properties of <paramref name="targetType"/> and its
    /// base types (excluding <c>object</c>). Properties hidden by a derived declaration are
    /// yielded once, using the most-derived declaration.
    /// </summary>
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

    /// <summary>
    /// Returns the reason a property cannot be a changeset field (indexer, required,
    /// missing or init-only setter, or an inaccessible accessor), or null if the property
    /// is supported.
    /// </summary>
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
