using Microsoft.AspNetCore.Http;

namespace RapidStack.AutoEndpoint;

public static class Helpers
{
    public static bool IsSimpleType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType.IsPrimitive ||
               underlyingType == typeof(string) ||
               underlyingType == typeof(DateTime) ||
               underlyingType == typeof(DateTimeOffset) ||
               underlyingType == typeof(Guid) ||
               underlyingType == typeof(decimal) ||
               underlyingType.IsEnum;
    }

    public static bool IsSpecialType(Type type)
    {
        return type == typeof(HttpContext) ||
               type == typeof(HttpRequest) ||
               type == typeof(HttpResponse) ||
               type == typeof(CancellationToken);
    }
    public static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    public static object ConvertToType(string value, Type targetType)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
            {
                return Activator.CreateInstance(targetType);
            }
            return null;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        // Handle specific types that need special conversion
        try
        {
            return targetType switch
            {
                Type t when t == typeof(Guid) => Guid.Parse(value),
                Type t when t == typeof(DateTime) => DateTime.Parse(value),
                Type t when t == typeof(DateTimeOffset) => DateTimeOffset.Parse(value),
                Type t when t == typeof(TimeSpan) => TimeSpan.Parse(value),
                Type t when t == typeof(bool) => bool.Parse(value),
                Type t when t.IsEnum => Enum.Parse(targetType, value, true),
                _ => Convert.ChangeType(value, targetType)
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot convert value '{value}' to type '{targetType.Name}': {ex.Message}", ex);
        }
    }
}
