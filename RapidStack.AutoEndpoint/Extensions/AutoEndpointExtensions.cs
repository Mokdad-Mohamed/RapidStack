using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using RapidStack.AutoEndpoint.Filters;
using RapidStack.AutoEndpoint.Models;
using RapidStack.AutoEndpoint.Services;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

namespace RapidStack.AutoEndpoint;

// Extension methods for easy integration
public static class AutoEndpointExtensions
{
    private static bool _validationEnabled = false;
    public static IServiceCollection AddAutoEndpoints(this IServiceCollection services)
    {
        services.AddSingleton<AutoEndpointDiscovery>();
        return services;
    }

    public static IServiceCollection AddAutoEndpointsSwagger(this IServiceCollection services)
    {
        services.AddSingleton<AutoEndpointDiscovery>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SchemaFilter<AutoEndpointSchemaFilter>();
        });
        return services;
    }

    public static WebApplication MapAutoEndpoints(this WebApplication app)
    {
        var discovery = app.Services.GetRequiredService<AutoEndpointDiscovery>();
        var endpoints = discovery.DiscoverEndpoints();

        foreach (var endpointInfo in endpoints)
        {
            MapEndpoint(app, endpointInfo);
        }

        return app;
    }

    private static void MapEndpoint(WebApplication app, EndpointInfo endpointInfo)
    {
        foreach (var httpMethod in endpointInfo.HttpMethods)
        {
            var routePattern = $"/{endpointInfo.Route.TrimStart('/')}";

            var routeBuilder = app.MapMethods(routePattern, new[] { httpMethod.ToUpperInvariant() },
                async (HttpContext context) =>
                {
                    return await InvokeServiceMethod(context, endpointInfo);
                });

            // Configure endpoint metadata for OpenAPI
            var tag = string.IsNullOrEmpty(endpointInfo.Tag) ? endpointInfo.ServiceType.Name : endpointInfo.Tag;

            routeBuilder
                .WithName($"{httpMethod}_{endpointInfo.ServiceType.Name}_{endpointInfo.Method.Name}")
                .WithTags(tag);

            // Add OpenAPI metadata using the simpler approach
            AddEndpointMetadata(routeBuilder, endpointInfo, httpMethod);
        }
    }

    private static void AddEndpointMetadata(RouteHandlerBuilder routeBuilder, EndpointInfo endpointInfo, string httpMethod)
    {
        var method = endpointInfo.Method;
        var parameters = method.GetParameters();

        // Find complex parameter for request body
        var complexParam = parameters.FirstOrDefault(p => !Helpers.IsSimpleType(p.ParameterType) && !Helpers.IsSpecialType(p.ParameterType));

        // Configure request body type if exists
        if (complexParam != null && httpMethod.ToUpperInvariant() is "POST" or "PUT" or "PATCH")
        {
            routeBuilder.Accepts(complexParam.ParameterType, "application/json");
        }

        // Configure return type  
        var returnType = method.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnType = returnType.GetGenericArguments()[0];
        }

        if (returnType != typeof(void) && returnType != typeof(Task))
        {
            routeBuilder.Produces(200, returnType, "application/json");
        }
        else
        {
            routeBuilder.Produces(200);
        }

        // Add error responses
        routeBuilder.Produces(400);
        routeBuilder.Produces(500);

        // Configure OpenAPI with parameter documentation
        routeBuilder.WithOpenApi(operation =>
        {
            operation.Summary = GenerateOperationSummary(method, httpMethod);
            operation.Description = GenerateOperationDescription(method);

            // Add parameter documentation
            ConfigureOpenApiParameters(operation, method, endpointInfo.Route, httpMethod);

            return operation;
        });
    }

    private static string GenerateOperationSummary(MethodInfo method, string httpMethod)
    {
        var methodName = method.Name;
        var action = httpMethod.ToLowerInvariant() switch
        {
            "get" => "Get",
            "post" => "Create",
            "put" => "Update",
            "delete" => "Delete",
            "patch" => "Patch",
            _ => methodName
        };

        return $"{action} {methodName}";
    }

    private static string GenerateOperationDescription(MethodInfo method)
    {
        var serviceName = method.DeclaringType?.Name ?? "Service";
        return $"Endpoint generated from {serviceName}.{method.Name}";
    }

    private static void ConfigureOpenApiParameters(OpenApiOperation operation, MethodInfo method, string route, string httpMethod)
    {
        var parameters = method.GetParameters();

        // Clear any existing parameters to avoid duplicates
        operation.Parameters?.Clear();
        operation.Parameters ??= new List<OpenApiParameter>();

        foreach (var param in parameters)
        {
            if (Helpers.IsSpecialType(param.ParameterType))
                continue; // Skip HttpContext, etc.

            if (Helpers.IsSimpleType(param.ParameterType))
            {
                // Determine if this is a path or query parameter
                var paramLocation = DetermineParameterLocation(param.Name, route);

                var openApiParam = new OpenApiParameter
                {
                    Name = param.Name,
                    In = paramLocation,
                    Required = paramLocation == ParameterLocation.Path || IsRequired(param),
                    Description = $"The {param.Name} parameter",
                    Schema = GetOpenApiSchemaForType(param.ParameterType)
                };

                operation.Parameters.Add(openApiParam);
            }
            // Complex types will be handled as request body by the .Accepts() call
            else if (httpMethod == "GET")
            {
                var properties = param.ParameterType.GetProperties();
                foreach (var property in properties)
                {
                    var openApiParam = new OpenApiParameter
                    {
                        Name = $"{param.Name}_{property.Name}",
                        In = ParameterLocation.Query,
                        Required = (/*!property.HasDefaultValue &&*/ IsRequiredProperty(property)),
                        Description = $"The {param.Name}_{property.Name} parameter",
                        Schema = GetOpenApiSchemaForType(property.PropertyType)
                    };

                    operation.Parameters.Add(openApiParam);
                }
            }
        }
    }

    private static bool IsRequiredProperty(PropertyInfo property)
    {
        // [Required] attribute (System.ComponentModel.DataAnnotations)
        bool hasRequiredAttribute = Attribute.IsDefined(property, typeof(RequiredAttribute));

        // 'required' keyword (emits RequiredMemberAttribute)
        bool hasRequiredKeyword = property.CustomAttributes
            .Any(attr => attr.AttributeType.Name == "RequiredMemberAttribute");

        return hasRequiredAttribute || hasRequiredKeyword;
    }

    private static bool IsRequired(ParameterInfo param)
    {
        // Check if the parameter's type has any required properties
        if (HasRequiredProperties(param.ParameterType))
            return true;

        // If no default value and not nullable, it's required
        return !param.HasDefaultValue && !Helpers.IsNullableType(param.ParameterType);
    }

    private static bool HasRequiredProperties(Type type)
    {
        // Skip primitives (int, string, bool, etc.)
        if (type.IsPrimitive || type == typeof(string))
            return false;

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p =>
                Attribute.IsDefined(p, typeof(RequiredAttribute)) ||
                p.CustomAttributes.Any(attr => attr.AttributeType.Name == "RequiredMemberAttribute")
            );
    }

    private static ParameterLocation DetermineParameterLocation(string paramName, string route)
    {
        // Check if this parameter appears in the route template
        if (route.Contains($"{{{paramName}}}", StringComparison.OrdinalIgnoreCase))
        {
            return ParameterLocation.Path;
        }

        return ParameterLocation.Query;
    }

    private static OpenApiSchema GetOpenApiSchemaForType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType switch
        {
            Type t when t == typeof(string) => new OpenApiSchema { Type = "string" },
            Type t when t == typeof(int) => new OpenApiSchema { Type = "integer", Format = "int32" },
            Type t when t == typeof(long) => new OpenApiSchema { Type = "integer", Format = "int64" },
            Type t when t == typeof(double) => new OpenApiSchema { Type = "number", Format = "double" },
            Type t when t == typeof(float) => new OpenApiSchema { Type = "number", Format = "float" },
            Type t when t == typeof(decimal) => new OpenApiSchema { Type = "number", Format = "decimal" },
            Type t when t == typeof(bool) => new OpenApiSchema { Type = "boolean" },
            Type t when t == typeof(DateTime) => new OpenApiSchema { Type = "string", Format = "date-time" },
            Type t when t == typeof(DateTimeOffset) => new OpenApiSchema { Type = "string", Format = "date-time" },
            Type t when t == typeof(DateOnly) => new OpenApiSchema { Type = "string", Format = "date" },
            Type t when t == typeof(TimeOnly) => new OpenApiSchema { Type = "string", Format = "time" },
            Type t when t == typeof(Guid) => new OpenApiSchema { Type = "string", Format = "uuid" },
            Type t when t.IsEnum => new OpenApiSchema
            {
                Type = "string",
                Enum = Enum.GetNames(t).Select(name => (Microsoft.OpenApi.Any.IOpenApiAny)new Microsoft.OpenApi.Any.OpenApiString(name)).ToList()
            },
            _ => new OpenApiSchema { Type = "string" }
        };
    }

    private static async Task<IResult> InvokeServiceMethod(HttpContext context, EndpointInfo endpointInfo)
    {
        try
        {
            // Get service instance from DI container
            var service = context.RequestServices.GetService(endpointInfo.ServiceType);
            if (service == null)
            {
                return Results.Problem($"Service {endpointInfo.ServiceType.Name} not registered in DI container",
                                     statusCode: 500);
            }

            // Prepare method parameters
            var parameters = await PrepareParameters(context, endpointInfo.Method);

            // Validate complex parameters generically
            var methodParams = endpointInfo.Method.GetParameters();
            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                if (!Helpers.IsSimpleType(param.ParameterType) && !Helpers.IsSpecialType(param.ParameterType))
                {
                    var errors = ValidateObject(parameters[i], context.RequestServices);
                    if (errors.Count > 0)
                    {
                        return Results.BadRequest(new { Errors = errors });
                    }
                }
            }

            // Invoke method
            var result = endpointInfo.Method.Invoke(service, parameters);

            // Handle async methods
            if (result is Task task)
            {
                await task;

                // Get result from Task<T>
                if (task.GetType().IsGenericType)
                {
                    var property = task.GetType().GetProperty("Result");
                    result = property?.GetValue(task);
                }
                else
                {
                    result = null; // Task without return value
                }
            }

            // Return appropriate response
            return result switch
            {
                null => Results.Ok(),
                IResult iresult => iresult,
                _ => Results.Ok(result)
            };
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: 500);
        }
    }

    private static async Task<object[]> PrepareParameters(HttpContext context, MethodInfo method)
    {
        var parameters = method.GetParameters();
        var args = new object[parameters.Length];

        // Check if we need to read the body for complex types
        string requestBody = null;
        var hasComplexParameter = parameters.Any(p => !Helpers.IsSimpleType(p.ParameterType) &&
                                                      !Helpers.IsSpecialType(p.ParameterType));

        if (hasComplexParameter)
        {
            if (context.Request.Method == "GET")
            {

            }
            else if (context.Request.ContentLength > 0)
            {
                context.Request.EnableBuffering(); // Allow multiple reads
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0; // Reset for potential future reads
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            // Handle special framework types
            if (Helpers.IsSpecialType(param.ParameterType))
            {
                args[i] = GetSpecialTypeValue(context, param.ParameterType);
            }
            // Handle simple types from route/query parameters
            else if (Helpers.IsSimpleType(param.ParameterType))
            {
                args[i] = GetSimpleParameterValue(context, param);
            }
            // Handle complex types from request body
            else
            {
                args[i] = await GetComplexParameterValue(context, param, requestBody);
            }
        }

        return args;
    }

    private static object GetSpecialTypeValue(HttpContext context, Type type)
    {
        return type switch
        {
            Type t when t == typeof(HttpContext) => context,
            Type t when t == typeof(HttpRequest) => context.Request,
            Type t when t == typeof(HttpResponse) => context.Response,
            Type t when t == typeof(CancellationToken) => context.RequestAborted,
            _ => null
        };
    }

    private static object GetSimpleParameterValue(HttpContext context, ParameterInfo param)
    {
        // Try route values first (for path parameters like /users/{id})
        var value = context.Request.RouteValues[param.Name]?.ToString();

        // If not found in route, try query parameters
        if (string.IsNullOrEmpty(value))
        {
            value = context.Request.Query[param.Name].FirstOrDefault();
        }

        // If still not found and parameter is optional, use default value
        if (string.IsNullOrEmpty(value))
        {
            if (param.HasDefaultValue)
            {
                return param.DefaultValue;
            }
            else if (param.ParameterType.IsValueType &&
                     Nullable.GetUnderlyingType(param.ParameterType) == null)
            {
                // Non-nullable value type without default - this might cause issues
                return Activator.CreateInstance(param.ParameterType);
            }
            else
            {
                return null; // Nullable type or reference type
            }
        }

        return Helpers.ConvertToType(value, param.ParameterType);
    }

    private static async Task<object> GetComplexParameterValue(HttpContext context, ParameterInfo param, string requestBody)
    {
        if (context.Request.Method == "GET")
        {
            var prefix = $"{param.Name}_";

            var dict = context.Request.Query
                .Where(kvp => kvp.Key.StartsWith(prefix))
                .ToDictionary(
                    kvp => kvp.Key.Substring(prefix.Length), // remove the prefix
                    kvp => kvp.Value.ToString()
                );

            // Serialize to JSON
            string json = JsonSerializer.Serialize(dict);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Deserialize into the target type
            return JsonSerializer.Deserialize(json, param.ParameterType, options);
        }

        if (string.IsNullOrEmpty(requestBody))
        {
            // Try to create default instance if no body provided
            if (param.HasDefaultValue)
            {
                return param.DefaultValue;
            }

            try
            {
                return Activator.CreateInstance(param.ParameterType);
            }
            catch
            {
                return null;
            }
        }

        try
        {
            // Handle JSON deserialization
            if (context.Request.HasJsonContentType() ||
                context.Request.ContentType?.Contains("application/json") == true)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Deserialize(requestBody, param.ParameterType, options);
            }

            // Handle form data
            if (context.Request.HasFormContentType)
            {
                return await BindFromForm(context, param.ParameterType);
            }

            // Fallback: try JSON parsing anyway
            return JsonSerializer.Deserialize(requestBody, param.ParameterType);
        }
        catch (Exception ex)
        {
            // Log the exception or handle it appropriately
            throw new InvalidOperationException(
                $"Failed to bind parameter '{param.Name}' of type '{param.ParameterType.Name}': {ex.Message}", ex);
        }
    }

    private static async Task<object> BindFromForm(HttpContext context, Type parameterType)
    {
        var form = await context.Request.ReadFormAsync();
        var instance = Activator.CreateInstance(parameterType);

        var properties = parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        foreach (var property in properties)
        {
            var formValue = form[property.Name].FirstOrDefault();
            if (!string.IsNullOrEmpty(formValue))
            {
                try
                {
                    var convertedValue = Helpers.ConvertToType(formValue, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
                catch
                {
                    // Skip properties that can't be converted
                    continue;
                }
            }
        }

        return instance;
    }
    #region Validation
    public static IServiceCollection UseValidation(this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        _validationEnabled = true;
        RegisterValidatorsByConvention(services, lifetime);
        return services;
    }

    private static bool IsApplicationAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        return name != null &&
               !name.StartsWith("System.") &&
               !name.StartsWith("Microsoft.") &&
               !name.StartsWith("netstandard");
    }
    private static void RegisterValidatorsByConvention(IServiceCollection services, ServiceLifetime lifetime)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic
                        && !string.IsNullOrWhiteSpace(a.Location)
                        && IsApplicationAssembly(a));

        foreach (var assembly in assemblies)
        {
            try
            {
                var validatorTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .SelectMany(t => t.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                        .Select(i => new { ValidatorType = t, ServiceType = i }));

                foreach (var vt in validatorTypes)
                {
                    services.Add(new ServiceDescriptor(vt.ServiceType, vt.ValidatorType, lifetime));
                }
            }
            catch (ReflectionTypeLoadException) { /* Skip problematic assemblies */ }
        }

    }

    private static List<string> ValidateObject(object obj, IServiceProvider services)
    {
        var errors = new List<string>();
        if (obj == null || !_validationEnabled) return errors;

        // Data Annotations
        var context = new ValidationContext(obj, services, null);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(obj, context, results, true))
        {
            errors.AddRange(results.Select(r => r.ErrorMessage));
        }

        // FluentValidation (if registered)
        var validatorType = typeof(IValidator<>).MakeGenericType(obj.GetType());
        var validator = services.GetService(validatorType) as IValidator;
        if (validator != null)
        {
            var validationResult = validator.Validate(new ValidationContext<object>(obj));
            if (!validationResult.IsValid)
            {
                errors.AddRange(validationResult.Errors.Select(e => e.ErrorMessage));
            }
        }

        return errors;
    }
    #endregion

}
