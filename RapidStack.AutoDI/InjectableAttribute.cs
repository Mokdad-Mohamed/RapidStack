using Microsoft.Extensions.DependencyInjection;

namespace RapidStack.AutoDI;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class InjectableAttribute : Attribute
{
    public ServiceLifetime Lifetime { get; }

    public InjectableAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        Lifetime = lifetime;
    }
}
