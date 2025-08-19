using RapidStack.AutoEndpoint.Attributes;
using RapidStack.AutoEndpoint.Models;
using System.Reflection;

namespace RapidStack.AutoEndpoint.Services;

// Service for discovering and registering auto endpoints
public class AutoEndpointDiscovery
{
    private readonly IServiceProvider _serviceProvider;

    public AutoEndpointDiscovery(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IEnumerable<EndpointInfo> DiscoverEndpoints()
    {
        var endpoints = new List<EndpointInfo>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var typesWithAutoEndpoint = assembly.GetTypes()
                    .Where(type => type.GetCustomAttribute<AutoEndpointAttribute>() != null)
                    .Where(type => type.IsClass && !type.IsAbstract);

                foreach (var serviceType in typesWithAutoEndpoint)
                {
                    endpoints.AddRange(DiscoverEndpointsForService(serviceType));
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
                continue;
            }
        }

        return endpoints;
    }

    private IEnumerable<EndpointInfo> DiscoverEndpointsForService(Type serviceType)
    {
        var autoEndpointAttr = serviceType.GetCustomAttribute<AutoEndpointAttribute>();
        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName) // Exclude properties, operators, etc.
            .Where(m => m.DeclaringType == serviceType) // Only methods declared in this class
            .Where(m => m.GetCustomAttribute<IgnoreEndpointAttribute>() == null);

        foreach (var method in methods)
        {
            yield return CreateEndpointInfo(serviceType, method, autoEndpointAttr);
        }
    }

    private EndpointInfo CreateEndpointInfo(Type serviceType, MethodInfo method, AutoEndpointAttribute autoEndpointAttr)
    {
        var endpointAttr = method.GetCustomAttribute<EndpointAttribute>();

        // Determine route
        var route = "";
        if (!string.IsNullOrEmpty(endpointAttr?.Route))
        {
            route = endpointAttr.Route;
        }
        else
        {
            // Auto-generate route with parameters
            route = GenerateRouteFromMethod(method);
        }

        // Add prefix if specified
        if (!string.IsNullOrEmpty(autoEndpointAttr.RoutePrefix))
        {
            route = $"{autoEndpointAttr.RoutePrefix.TrimEnd('/')}/{route.TrimStart('/')}";
        }

        // Determine HTTP methods
        var httpMethods = new List<string>();
        if (endpointAttr?.HttpMethods?.Length > 0)
        {
            httpMethods.AddRange(endpointAttr.HttpMethods);
        }
        else if (!string.IsNullOrEmpty(endpointAttr?.HttpMethod))
        {
            httpMethods.Add(endpointAttr.HttpMethod);
        }
        else
        {
            // Default HTTP method based on method name or parameters
            httpMethods.Add(DetermineDefaultHttpMethod(method));
        }

        return new EndpointInfo
        {
            ServiceType = serviceType,
            Method = method,
            Route = route,
            HttpMethods = httpMethods.ToArray(),
            Tag = autoEndpointAttr.Tag
        };
    }
   

    
    private string GenerateRouteFromMethod(MethodInfo method)
    {
        var methodName = method.Name.ToLowerInvariant();
        var parameters = method.GetParameters();

        var route = methodName;

        // Only add route parameters for simple types in specific scenarios
        var simpleParams = parameters.Where(p =>
            Helpers.IsSimpleType(p.ParameterType) &&
            !Helpers.IsSpecialType(p.ParameterType)).ToArray();

        // Determine HTTP method to decide parameter placement
        var httpMethod = DetermineDefaultHttpMethod(method);
        var complexParameters = new List<ParameterInfo>();
        if (httpMethod == "GET")
        {
            complexParameters = parameters.Where(p => !Helpers.IsSimpleType(p.ParameterType) && !Helpers.IsSpecialType(p.ParameterType)).ToList();
        }

        // For GET requests with multiple parameters, use query params only
        if (httpMethod == "GET" && (simpleParams.Length > 1 || complexParameters.Count > 0))
        {
            // All parameters will be query parameters
            return route;
        }

        // For GET requests with single parameter, or non-GET with simple params, add to route
        if (httpMethod == "GET" && simpleParams.Length == 1 && complexParameters.Count == 0)
        {
            // Single parameter for GET becomes path parameter
            route += $"/{{{simpleParams[0].Name}}}";
        }
        else if (httpMethod != "GET")
        {
            // For POST/PUT/DELETE, simple params can be path params (except when there's a complex body param)
            var hasComplexParam = parameters.Any(p => !Helpers.IsSimpleType(p.ParameterType) && !Helpers.IsSpecialType(p.ParameterType));

            if (!hasComplexParam)
            {
                // No complex body parameter, so simple params can be path params
                foreach (var param in simpleParams)
                {
                    route += $"/{{{param.Name}}}";
                }
            }
            else
            {
                // Has complex body parameter, so only first simple param becomes path param (typically ID)
                if (simpleParams.Length > 0)
                {
                    route += $"/{{{simpleParams[0].Name}}}";
                }
            }
        }

        return route;
    }

    private string DetermineDefaultHttpMethod(MethodInfo method)
    {
        var methodName = method.Name.ToLowerInvariant();

        if (methodName.StartsWith("get") || methodName.StartsWith("find") || methodName.StartsWith("search") || methodName.StartsWith("list"))
            return "GET";
        if (methodName.StartsWith("post") || methodName.StartsWith("create") || methodName.StartsWith("add"))
            return "POST";
        if (methodName.StartsWith("put") || methodName.StartsWith("update") || methodName.StartsWith("edit"))
            return "PUT";
        if (methodName.StartsWith("delete") || methodName.StartsWith("remove"))
            return "DELETE";
        if (methodName.StartsWith("patch"))
            return "PATCH";

        // Check if method has parameters (likely POST/PUT) or no parameters (likely GET)
        return method.GetParameters().Length > 0 ? "POST" : "GET";
    }
}
