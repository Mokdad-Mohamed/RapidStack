using System.Reflection;

namespace RapidStack.AutoEndpoint.Models;

// Information about discovered endpoints
public class EndpointInfo
{
    public Type ServiceType { get; set; }
    public MethodInfo Method { get; set; }
    public string Route { get; set; }
    public string[] HttpMethods { get; set; }
    public string Tag { get; set; }
}
