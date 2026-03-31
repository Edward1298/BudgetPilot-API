namespace BudgetPilot_API.Dtos
{
    public class UsersDTO // This dto is used to transfer user data between the client and the server. It contains properties for the user's name, email, and password, which are necessary for creating or updating a user in the application.
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
