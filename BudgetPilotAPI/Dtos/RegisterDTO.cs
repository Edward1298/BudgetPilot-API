using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for user registration requests.
    /// Contains the fields required to create a new user account.
    /// </summary>
    public class RegisterDTO
    {
        /// <summary>
        /// Gets or sets the display name for the new user.
        /// Must be between 1 and 100 characters.
        /// </summary>
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the email address for the new user.
        /// Must be a valid email format and unique in the system.
        /// </summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "A valid email address is required.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plain-text password for the new user.
        /// Must be between 8 and 128 characters. Stored as a BCrypt hash.
        /// </summary>
        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [MaxLength(128, ErrorMessage = "Password must not exceed 128 characters.")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the role to assign to the new user.
        /// Must reference an existing role in the roles table.
        /// </summary>
        [Required(ErrorMessage = "RoleId is required.")]
        public Guid RoleId { get; set; }
    }
}
