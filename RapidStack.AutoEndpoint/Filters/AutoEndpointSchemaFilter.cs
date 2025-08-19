using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using RapidStack.AutoEndpoint.Attributes;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace RapidStack.AutoEndpoint.Filters;

// Custom schema filter to register AutoEndpoint schemas
public class AutoEndpointSchemaFilter : ISchemaFilter
{
    private static readonly HashSet<Type> _registeredTypes = new();

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // This method is called for each type that SwaggerGen encounters
        // We use this opportunity to register related types from our AutoEndpoint services

        if (_registeredTypes.Contains(context.Type))
            return;

        _registeredTypes.Add(context.Type);

        // Check if this type is used in any AutoEndpoint service
        RegisterRelatedAutoEndpointTypes(context);
    }

    private void RegisterRelatedAutoEndpointTypes(SchemaFilterContext context)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var autoEndpointServices = assembly.GetTypes()
                    .Where(type => type.GetCustomAttribute<AutoEndpointAttribute>() != null)
                    .Where(type => type.IsClass && !type.IsAbstract);

                foreach (var serviceType in autoEndpointServices)
                {
                    var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => !m.IsSpecialName)
                        .Where(m => m.DeclaringType == serviceType)
                        .Where(m => m.GetCustomAttribute<IgnoreEndpointAttribute>() == null);

                    foreach (var method in methods)
                    {
                        RegisterTypesFromMethod(context, method);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }
        }
    }

    private void RegisterTypesFromMethod(SchemaFilterContext context, MethodInfo method)
    {
        // Register parameter types
        foreach (var param in method.GetParameters())
        {
            if (!IsSimpleType(param.ParameterType) && !IsSpecialType(param.ParameterType))
            {
                EnsureTypeIsRegistered(context, param.ParameterType);
            }
        }

        // Register return type
        var returnType = method.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnType = returnType.GetGenericArguments()[0];
        }

        if (!IsSimpleType(returnType) && returnType != typeof(void) && returnType != typeof(Task))
        {
            EnsureTypeIsRegistered(context, returnType);
        }
    }

    private void EnsureTypeIsRegistered(SchemaFilterContext context, Type type)
    {
        if (_registeredTypes.Contains(type))
            return;

        // Generate schema for this type if it doesn't exist
        context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository);
        _registeredTypes.Add(type);
    }

    private static bool IsSimpleType(Type type)
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

    private static bool IsSpecialType(Type type)
    {
        return type == typeof(HttpContext) ||
               type == typeof(HttpRequest) ||
               type == typeof(HttpResponse) ||
               type == typeof(CancellationToken);
    }

}
