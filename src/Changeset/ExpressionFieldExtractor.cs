using System.Linq.Expressions;

namespace Changeset;

internal static class ExpressionFieldExtractor
{
    /// <summary>
    /// Extracts property names from an expression like <c>u => new { u.Name, u.Email }</c>
    /// or a single property access like <c>u => u.Name</c>.
    /// </summary>
    public static IReadOnlyList<string> ExtractFieldNames<T>(Expression<Func<T, object>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ExtractFieldNames(expression.Body, expression.Parameters[0]);
    }

    public static IReadOnlyList<string> ExtractFieldNames<T, TValue>(
        Expression<Func<T, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ExtractFieldNames(expression.Body, expression.Parameters[0]);
    }

    public static string ExtractFieldName<T, TValue>(
        Expression<Func<T, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var body = UnwrapConversion(expression.Body);
        if (body is MemberExpression member)
            return DirectMemberName(member, expression.Parameters[0]);

        throw new ArgumentException(
            "Expression must be a property access (u => u.Name)",
            nameof(expression));
    }

    private static IReadOnlyList<string> ExtractFieldNames(
        Expression body, ParameterExpression parameter)
    {
        body = UnwrapConversion(body);

        // Single property: u => u.Name
        if (body is MemberExpression member)
            return [DirectMemberName(member, parameter)];

        // Anonymous type: u => new { u.Name, u.Email }
        if (body is NewExpression newExpr)
        {
            if (newExpr.Members is null || newExpr.Members.Count == 0)
                throw new ArgumentException(
                    "Expression must select properties, e.g. u => new { u.Name, u.Email }");

            var names = new string[newExpr.Members.Count];
            for (var i = 0; i < newExpr.Members.Count; i++)
            {
                if (UnwrapConversion(newExpr.Arguments[i]) is not MemberExpression argMember)
                    throw new ArgumentException(
                        $"Anonymous type member '{newExpr.Members[i].Name}' must be a property access " +
                        "on the lambda parameter, e.g. u => new { u.Name, u.Email }");

                var name = DirectMemberName(argMember, parameter);
                if (name != newExpr.Members[i].Name)
                    throw new ArgumentException(
                        $"Anonymous type member '{newExpr.Members[i].Name}' does not match the selected " +
                        $"property '{name}'. Aliases are not supported; use u => new {{ u.{name} }}");

                names[i] = name;
            }

            return names;
        }

        throw new ArgumentException(
            "Expression must be a property access (u => u.Name) or anonymous type (u => new { u.Name, u.Email })");
    }

    private static string DirectMemberName(MemberExpression member, ParameterExpression parameter)
    {
        if (member.Expression != parameter)
            throw new ArgumentException(
                $"Expression '{member}' must access a property directly on the lambda parameter " +
                $"'{parameter.Name}'. Nested paths like {parameter.Name} => {parameter.Name}.Child.Property are not supported.");

        return member.Member.Name;
    }

    private static Expression UnwrapConversion(Expression body) =>
        body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : body;
}
