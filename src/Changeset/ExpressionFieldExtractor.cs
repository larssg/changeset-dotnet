using System.Linq.Expressions;

namespace Changeset;

internal static class ExpressionFieldExtractor
{
    /// <summary>
    /// Extracts property names from an expression like <c>u => new { u.Name, u.Email }</c>
    /// or a single property access like <c>u => u.Name</c>.
    /// </summary>
    public static IReadOnlyList<string> ExtractFieldNames<T>(Expression<Func<T, object>> expression)
        => ExtractFieldNames(expression.Body);

    public static IReadOnlyList<string> ExtractFieldNames<T, TValue>(
        Expression<Func<T, TValue>> expression)
        => ExtractFieldNames(expression.Body);

    private static IReadOnlyList<string> ExtractFieldNames(Expression body)
    {
        // Handle implicit boxing conversion: u => (object)u.Name
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        // Single property: u => u.Name
        if (body is MemberExpression member)
            return [member.Member.Name];

        // Anonymous type: u => new { u.Name, u.Email }
        if (body is NewExpression newExpr)
        {
            if (newExpr.Members is null || newExpr.Members.Count == 0)
                throw new ArgumentException(
                    "Expression must select properties, e.g. u => new { u.Name, u.Email }");

            var names = new string[newExpr.Members.Count];
            for (var i = 0; i < newExpr.Members.Count; i++)
                names[i] = newExpr.Members[i].Name;
            return names;
        }

        throw new ArgumentException(
            "Expression must be a property access (u => u.Name) or anonymous type (u => new { u.Name, u.Email })");
    }
}
