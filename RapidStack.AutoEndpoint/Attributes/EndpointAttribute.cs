

namespace RapidStack.AutoEndpoint.Attributes;

// Attribute to configure individual endpoints
[AttributeUsage(AttributeTargets.Method)]
public class EndpointAttribute : Attribute
{
    public string Route { get; set; } = "";
    public string HttpMethod { get; set; } = "GET";
    public string[] HttpMethods { get; set; } = new string[0];

    public EndpointAttribute(string route = "", string httpMethod = "GET")
    {
        Route = route;
        HttpMethod = httpMethod;
    }
}
