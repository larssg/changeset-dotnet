using System.Globalization;
using System.Text.Json;

namespace Changeset.Casting;

internal static class TypeCoercion
{
    public static (bool Success, object? Value) TryCoerce(
        object? source, Type targetType, IFormatProvider? formatProvider)
    {
        formatProvider ??= CultureInfo.InvariantCulture;

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlyingType != null;
        var actualTarget = underlyingType ?? targetType;

        if (source is null)
        {
            if (isNullable || !actualTarget.IsValueType)
                return (true, null);
            return (false, null);
        }

        if (source is JsonElement jsonElement)
            return TryCoerceJsonElement(jsonElement, actualTarget, formatProvider, isNullable);

        if (actualTarget.IsInstanceOfType(source))
            return (true, source);

        if (source is string str)
            return TryCoerceString(str, actualTarget, formatProvider);

        if (IsNumericType(source.GetType()) && IsNumericType(actualTarget))
            return TryCoerceNumeric(source, actualTarget);

        return (false, null);
    }

    private static (bool, object?) TryCoerceString(
        string value, Type target, IFormatProvider formatProvider)
    {
        if (target == typeof(string))
            return (true, value);

        if (target == typeof(int))
            return int.TryParse(value, NumberStyles.Integer, formatProvider, out var i)
                ? (true, i) : (false, null);

        if (target == typeof(long))
            return long.TryParse(value, NumberStyles.Integer, formatProvider, out var l)
                ? (true, l) : (false, null);

        if (target == typeof(decimal))
            return decimal.TryParse(value, NumberStyles.Number, formatProvider, out var d)
                ? (true, d) : (false, null);

        if (target == typeof(double))
            return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out var db)
                ? (true, db) : (false, null);

        if (target == typeof(float))
            return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, formatProvider, out var f)
                ? (true, f) : (false, null);

        if (target == typeof(bool))
            return value.ToLowerInvariant() switch
            {
                "true" or "1" => (true, true),
                "false" or "0" => (true, false),
                _ => (false, null)
            };

        if (target == typeof(DateTime))
            return DateTime.TryParse(value, formatProvider, DateTimeStyles.RoundtripKind, out var dt)
                ? (true, dt) : (false, null);

        if (target == typeof(DateTimeOffset))
            return DateTimeOffset.TryParse(value, formatProvider, DateTimeStyles.RoundtripKind, out var dto)
                ? (true, dto) : (false, null);

        if (target == typeof(DateOnly))
            return DateOnly.TryParse(value, formatProvider, DateTimeStyles.None, out var dateOnly)
                ? (true, dateOnly) : (false, null);

        if (target == typeof(TimeOnly))
            return TimeOnly.TryParse(value, formatProvider, DateTimeStyles.None, out var timeOnly)
                ? (true, timeOnly) : (false, null);

        if (target == typeof(Guid))
            return Guid.TryParse(value, out var g) ? (true, g) : (false, null);

        if (target.IsEnum)
            return Enum.TryParse(target, value, ignoreCase: true, out var e)
                ? (true, e) : (false, null);

        return (false, null);
    }

    private static (bool, object?) TryCoerceJsonElement(
        JsonElement element, Type target, IFormatProvider formatProvider, bool isNullable)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            if (isNullable || !target.IsValueType)
                return (true, null);
            return (false, null);
        }

        if (target == typeof(string))
            return (true, element.ToString());

        if (target == typeof(int) && element.ValueKind == JsonValueKind.Number)
            return element.TryGetInt32(out var i) ? (true, i) : (false, null);

        if (target == typeof(long) && element.ValueKind == JsonValueKind.Number)
            return element.TryGetInt64(out var l) ? (true, l) : (false, null);

        if (target == typeof(decimal) && element.ValueKind == JsonValueKind.Number)
            return element.TryGetDecimal(out var d) ? (true, d) : (false, null);

        if (target == typeof(double) && element.ValueKind == JsonValueKind.Number)
            return element.TryGetDouble(out var db) ? (true, db) : (false, null);

        if (target == typeof(float) && element.ValueKind == JsonValueKind.Number)
            return element.TryGetDouble(out var fv) ? (true, (float)fv) : (false, null);

        if (target == typeof(bool) && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
            return (true, element.GetBoolean());

        if (target == typeof(Guid) && element.ValueKind == JsonValueKind.String)
            return element.TryGetGuid(out var g) ? (true, g) : (false, null);

        if (target == typeof(DateTime) && element.ValueKind == JsonValueKind.String)
            return element.TryGetDateTime(out var dt) ? (true, dt) : (false, null);

        if (target == typeof(DateTimeOffset) && element.ValueKind == JsonValueKind.String)
            return element.TryGetDateTimeOffset(out var dto) ? (true, dto) : (false, null);

        // Fallback: try coercing the string representation
        if (element.ValueKind == JsonValueKind.String)
            return TryCoerceString(element.GetString()!, target, formatProvider);

        if (element.ValueKind == JsonValueKind.Number)
            return TryCoerceString(element.GetRawText(), target, formatProvider);

        return (false, null);
    }

    private static (bool, object?) TryCoerceNumeric(object source, Type target)
    {
        try
        {
            var converted = Convert.ChangeType(source, target);
            return (true, converted);
        }
        catch (OverflowException)
        {
            return (false, null);
        }
        catch (InvalidCastException)
        {
            return (false, null);
        }
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }
}
