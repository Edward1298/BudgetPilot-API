using BudgetPilot_API.Entities;
using Microsoft.EntityFrameworkCore;
using BudgetPilot_API.Dtos;

public class UserService
{

    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UsersOBJ>> GetUsers()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<UsersOBJ> CreateUser(UsersDTO userDto)
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

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    //public async Task<UsersOBJ> CreateUser(UsersDTO user)
    //{
    //    _context.Users.Add(user);
    //    await _context.SaveChangesAsync();

    //    return user;
    //}
}
