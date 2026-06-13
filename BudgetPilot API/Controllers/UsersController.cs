using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;

[ApiController]
[Route("api/[controller]")] // This attribute defines the route for the controller. It means that the controller will handle requests sent to "api/users".
public class UsersController : ControllerBase // This class is a controller in an ASP.NET Core Web API application. It is responsible for handling HTTP requests related to users, such as retrieving user data or creating new users. The controller uses the UserService to perform the necessary business logic and interacts with the database through the AppDbContext.
{
    private readonly UserService _userService; // This is a private field that holds a reference to the UserService. The UserService is responsible for handling the business logic related to users, such as retrieving user data from the database or creating new users.

    public UsersController(UserService userService) // This is the constructor for the UsersController. It takes a UserService as a parameter and assigns it to the private field _userService. This allows the controller to use the UserService to perform operations related to users.
    {
        _userService = userService; 
    }

    [HttpGet] // This attribute indicates that this method will handle HTTP GET requests. When a GET request is sent to "api/users", this method will be invoked.
    public async Task<IActionResult> GetUsers() // This method is responsible for handling GET requests to retrieve user data. It calls the GetUsers method of the UserService to get a list of users from the database, and then returns the list of users in the response using the Ok() method, which indicates a successful response with a status code of 200.
    {
        var users = await _userService.GetUsers();
        return Ok(users);
    }

    [HttpPost] // This attribute indicates that this method will handle HTTP POST requests. When a POST request is sent to "api/users", this method will be invoked.
    public async Task<IActionResult> CreateUser(UsersDTO user) // This method is responsible for handling POST requests to create a new user. It takes a UsersDTO object as a parameter, which contains the data for the new user. The method calls the CreateUser method of the UserService to create a new user in the database, and then returns the created user in the response using the Ok() method, which indicates a successful response with a status code of 200.
    {
        var createdUser = await _userService.CreateUser(user);
        return Ok(createdUser);
    }
}
