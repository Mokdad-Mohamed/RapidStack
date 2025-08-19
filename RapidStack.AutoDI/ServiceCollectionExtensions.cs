using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace RapidStack.AutoDI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRapidStackAutoDI(this IServiceCollection services, params Assembly[] assemblies)
    {
        var allTypes = assemblies.SelectMany(a => a.GetTypes());

        foreach (var type in allTypes)
        {
            var attr = type.GetCustomAttribute<InjectableAttribute>();
            if (attr == null || !type.IsClass || type.IsAbstract)
                continue;

            var interfaces = type.GetInterfaces();
            if (interfaces.Length == 0)
            {
                services.Add(new ServiceDescriptor(type, type, attr.Lifetime));
            }
            else
            {
                foreach (var iface in interfaces)
                {
                    services.Add(new ServiceDescriptor(iface, type, attr.Lifetime));
                }
            }
            // ✅ Always register the concrete class too
            services.Add(new ServiceDescriptor(type, type, attr.Lifetime));
        }

        return services;
    }
}

