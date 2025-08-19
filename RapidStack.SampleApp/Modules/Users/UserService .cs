using RapidStack.AutoDI;
using RapidStack.AutoEndpoint.Attributes;

namespace RapidStack.SampleApp.Modules.Users;

public interface IUserService
{
    Task<List<User>> GetAllUsers();
    Task<User?> GetUserById(int id);
    Task<User> CreateUser(User user);
    void InternalMethod();
}

[Injectable(ServiceLifetime.Scoped)]
[AutoEndpoint("api/users")]
public class UserService: IUserService
{
    List<User> _users;
    public UserService()
    {
        _users = new List<User>() { new User(1, "Mohamed"), new User(2, "Abdellah"), new User(3, "Abdelrahmane") };
    }
    public async Task<List<User>> GetAllUsers()
    {
        // Implementation
        return _users;
    }

    [Endpoint("find/{id}", "GET")]
    public async Task<User?> GetUserById(int id)
    {
        // Implementation
        return _users.FirstOrDefault(x => x.Id == id);
    }

    public async Task<User> CreateUser(User user)
    {
        // Implementation - will be POST by default
        _users.Add(user);
        return user;
    }

    [IgnoreEndpoint]
    public void InternalMethod()
    {
        // This won't be exposed as an endpoint
    }
}


public record User(int Id, string Name);