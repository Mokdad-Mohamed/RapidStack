# RapidStack.AutoEndpoint

RapidStack.AutoEndpoint is a powerful .NET library that automatically generates RESTful API endpoints from your application services. With attribute-based configuration and smart conventions, you can expose your business logic as HTTP endpoints with minimal effort, reducing manual controller code and boilerplate.

---

## Features

- **Automatic Endpoint Generation:** Just tag your classes or methods with `[AutoEndpoint]`.
- **Base Route Configuration:** Specify a base route for groups of endpoints.
- **Convention-Based Routing:** Methods are exposed as endpoints using naming conventions.
- **Parameter Mapping:** Input parameters (route, query, body) are auto-mapped.
- **Supports Multiple HTTP Verbs:** Automatically selects HTTP method (GET, POST, etc.) based on method name or attributes.
- **Extensible:** Customize endpoint generation and parameter binding.
- **Selective Exposure:** Exclude specific methods or classes from endpoint generation with `[IgnoreEndpoint]`.
- **OpenAPI Support:** Easily generate Swagger documentation for your endpoints.

---

## Installation

Install the NuGet package:

```shell
dotnet add package RapidStack.AutoEndpoint
```

---

## Quick Start

1. **Decorate your service class:**

   ```csharp
   [AutoEndpoint("users")]
   public class UserAppService
   {
       public UserDto GetUser(int id) => ...;          // Exposed as GET /users/getuser/id=1
       public IEnumerable<UserDto> GetAll() => ...;    // Exposed as GET /users/getall
       public void CreateUser(UserDto user) => ...;    // Exposed as POST /users/createuser
       public IEnumerable<UserDto> GetPaged(int id, QueryParams queryParams) => ...;          // Exposed as GET /users/getPaged?id=1&queryParams_page=1&queryParams_pageSize=10&....
   }
   ```

2. **Register endpoints in Startup/Program:**

   ```csharp
   builder.Services.AddAutoEndpoints();
   // OR, to enable OpenAPI/Swagger documentation:
   builder.Services.AddAutoEndpointsSwagger();
   ```

3. **Map endpoints in your app:**

   ```csharp
   app.MapAutoEndpoints();
   ```

   > **Note:**  
   > - Use `AddAutoEndpointsSwagger()` if you want automatic OpenAPI/Swagger documentation for your endpoints.  
   > - Use `AddAutoEndpoints()` for endpoint generation only, without OpenAPI support.  
   > - `app.MapAutoEndpoints();` is required in both cases.

---

## Usage Details

### Route and HTTP Verb Conventions

- Method names starting with `Get`, `Find`, `Search` or `List` → GET
- Method names starting with `Create`, `Add`, `Post` → POST
- Method names starting with `Update`, `Put`, `Edit` → PUT
- Method names starting with `Delete`, `Remove` → DELETE
- Method names starting with `Patch` → PATCH
- You can override the HTTP verb with endpont attribute if needed like ([Endpoint("find/{id}", "GET")]).

### Parameter Handling

- **Primitive and string parameters** are mapped to query or route parameters.
- **Complex types** (e.g., DTOs) are bound from the request body for POST/PUT.
- **Collections** are supported.

#### Example

```csharp
[AutoEndpoint("products")]
public class ProductService
{
    public ProductDto GetProduct(int id) { ... }
    public IEnumerable<ProductDto> ListProducts() { ... }
    public void UpdateProduct(int id, ProductDto product) { ... }
}
```

| Method           | Route                  | HTTP Verb | Parameters           |
|------------------|-----------------------|-----------|----------------------|
| GetProduct       | /products/getproduct/id   | GET       | id (path)           |
| ListProducts     | /products/listproducts | GET       |                      |
| UpdateProduct    | /products/updateproduct/id| PUT       | id (path), product (body) |

---

## Ignoring Specific Methods or Classes

You can use the `[IgnoreEndpoint]` attribute to exclude specific methods or entire classes from endpoint generation.

### Usage

**Ignore a method:**

```csharp
[AutoEndpoint("orders")]
public class OrderService
{
    public OrderDto GetOrder(int id) { /* ... */ }

    [IgnoreEndpoint]
    public string InternalOperation() { /* Not exposed as an endpoint */ }
}
```

**Ignore a class:**

```csharp
[AutoEndpoint("admin")]
[IgnoreEndpoint]
public class AdminService
{
    public void DangerousOperation() { /* Not exposed as any endpoint */ }
}
```

This gives you fine-grained control over which code is accessible via HTTP.

---

## OpenAPI (Swagger) Documentation Support

RapidStack.AutoEndpoint supports automatic generation of OpenAPI documentation for your endpoints.

**To enable OpenAPI/Swagger:**

1. Register endpoints with Swagger support:

   ```csharp
   builder.Services.AddAutoEndpointsSwagger();
   ```

2. Map endpoints in your app:

   ```csharp
   app.MapAutoEndpoints();
   ```

This setup will automatically generate and serve OpenAPI documentation for your auto-generated endpoints.

**If you do not need OpenAPI support:**

- Just use `builder.Services.AddAutoEndpoints();` and `app.MapAutoEndpoints();`

---

## Customization

- You can customize route names, HTTP verbs, and parameter binding using additional attributes.
- Supports extension points for advanced scenarios (e.g., custom route templates).

---

## Error Handling

- Exceptions thrown in your service methods are returned as proper HTTP error responses.
- You can implement exception filters or middleware for more control.

---

## Best Practices

- Use descriptive method names for clear routing.
- Validate input parameters in your service methods.
- Secure sensitive endpoints (consider using ASP.NET Core authentication/authorization).
- Use `[IgnoreEndpoint]` for internal or sensitive logic you do not want exposed.

---

## Troubleshooting & FAQ

**Q**: Why isn’t my endpoint showing up?  
**A**: Ensure your class is decorated with `[AutoEndpoint]` and registered via `AddAutoEndpoints`. If a method or class has `[IgnoreEndpoint]`, it will be excluded.

**Q**: How do I bind complex parameters?  
**A**: Complex types are mapped from the request body for POST/PUT methods and as query parameters (complexObjectType_propertyName...) fot GET.

**Q**: How do I secure my endpoints?  
**A**: Use ASP.NET Core’s built-in authorization attributes or middleware.

**Q**: How do I add OpenAPI/Swagger support for my endpoints?  
**A**: Use `builder.Services.AddAutoEndpointsSwagger();` and `app.MapAutoEndpoints();` in your configuration.

---

## Example

```csharp
using RapidStack.AutoEndpoint;

[AutoEndpoint("orders")]
public class OrderService
{
    public OrderDto GetOrder(int id) { /* ... */ }

    [IgnoreEndpoint]
    public string InternalOperation() { /* Not exposed as endpoint */ }
}
```

---

## License

MIT
