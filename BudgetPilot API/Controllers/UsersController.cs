using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userService.GetUsers();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(UsersDTO user)
    {
        var createdUser = await _userService.CreateUser(user);
        return Ok(createdUser);
    }
}