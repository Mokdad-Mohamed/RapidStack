namespace RapidStack.AutoEndpoint.Attributes;

// Attribute to exclude methods from endpoint generation
[AttributeUsage(AttributeTargets.Method)]
public class IgnoreEndpointAttribute : Attribute
{
}

