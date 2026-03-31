using BudgetPilot_API.Entities;
using Microsoft.EntityFrameworkCore;
using BudgetPilot_API.Dtos;

public class UserService // This class is a service that contains the business logic related to users. It interacts with the AppDbContext to perform operations on the database. The UserService is used by the UsersController to handle HTTP requests related to users.
{

    private readonly AppDbContext _context; // This is a private field that holds a reference to the AppDbContext. The AppDbContext is responsible for managing the database connection and providing access to the database tables. The UserService uses the AppDbContext to perform operations on the "users" table in the database.

    public UserService(AppDbContext context) // This is the constructor for the UserService. It takes an AppDbContext as a parameter and assigns it to the private field _context. This allows the UserService to use the AppDbContext to interact with the database and perform operations related to users.
    {
        _context = context;
    }

    public async Task<List<UsersOBJ>> GetUsers() // This is a public method that returns a list of UsersOBJ objects. It is an asynchronous method that uses the async and await keywords to perform the database operation asynchronously. The method retrieves all users from the database by calling _context.Users.ToListAsync(), which returns a list of users from the "users" table in the database.
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<UsersOBJ> CreateUser(UsersDTO userDto) // This is a public method that creates a new user in the database. It takes a UsersDTO object as a parameter, which contains the data for the new user (name, email, and password). The method creates a new UsersOBJ entity, sets its properties based on the data from the UsersDTO, and then adds it to the database context. Finally, it saves the changes to the database and returns the created user entity.
    {
        var userEntity = new UsersOBJ
        {
            Id = Guid.NewGuid(),
            Name = userDto.Name,
            Email = userDto.Email,
            PasswordHash = HashPassword(userDto.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(userEntity);
        await _context.SaveChangesAsync();

        return userEntity;
    }

    private string HashPassword(string password) // This is a private method that takes a plain text password as input and returns a hashed version of the password. It uses the BCrypt library to hash the password, which is a secure way to store passwords in the database. The method calls BCrypt.Net.BCrypt.HashPassword(password) to generate the hashed password and returns it.
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

}
