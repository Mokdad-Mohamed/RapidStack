namespace RapidStack.AutoEndpoint.Attributes;

// Attribute to mark services for automatic endpoint generation
[AttributeUsage(AttributeTargets.Class)]
public class AutoEndpointAttribute : Attribute
{
    public string RoutePrefix { get; set; } = "";
    public string Tag { get; set; } = "";

    public AutoEndpointAttribute(string routePrefix = "")
    {
        RoutePrefix = routePrefix;
    }
}
