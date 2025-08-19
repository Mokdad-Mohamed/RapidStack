# RapidStack.AutoDI

RapidStack.AutoDI is an open-source library for .NET that automates service registration in the dependency injection (DI) container. With simple attribute decoration, your classes are registered automatically, saving time and reducing configuration errors.

## Features

- **Automatic DI Registration:** Decorate classes with `[Injectable]` for automatic registration.
- **Configurable Lifetime:** Supports `Scoped` (default), `Singleton`, and `Transient` lifetimes.
- **Easy Integration:** Install via NuGet, add one registration line, and you're set!

## Installation

```shell
dotnet add package RapidStack.AutoDI
```

## Usage

1. **Decorate your classes:**

   ```csharp
   [Injectable] // Registers as scoped by default
   public class MyService { }

   [Injectable(ServiceScope.Singleton)]
   public class MySingletonService { }

   [Injectable(ServiceScope.Transient)]
   public class MyTransientService { }
   ```

2. **Register services in the DI container:**

   ```csharp
   builder.Services.AddRapidStackAutoDI(Assembly.GetExecutingAssembly());
   ```

## How It Works

- RapidStack.AutoDI scans your assemblies for `[Injectable]` attributes.
- It registers each class according to the specified service scope.
- Supported scopes:
  - `Scoped`: One instance per request (default).
  - `Singleton`: One instance for the application's lifetime.
  - `Transient`: New instance every time requested.

## Example

```csharp
using RapidStack.AutoDI;

[Injectable]
public class OrderService
{
    // Implementation
}

[Injectable(ServiceScope.Singleton)]
public class ConfigProvider
{
    // Implementation
}
```

## License

MIT
