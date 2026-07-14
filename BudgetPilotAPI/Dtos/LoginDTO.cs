using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for user login requests.
    /// Contains the email and password credentials required for authentication.
    /// </summary>
    public class LoginDTO
    {
        /// <summary>
        /// Gets or sets the email address used for authentication.
        /// </summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "A valid email address is required.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plain-text password used for authentication.
        /// </summary>
        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }
}
