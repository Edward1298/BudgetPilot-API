using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for partial user updates.
    /// Only non-null fields are applied to the user entity.
    /// </summary>
    public class UserUpdateDTO
    {
        /// <summary>
        /// Gets or sets the updated display name for the user.
        /// When provided, must be between 1 and 100 characters.
        /// </summary>
        [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the updated email address for the user.
        /// When provided, must be a valid email format.
        /// </summary>
        [EmailAddress(ErrorMessage = "A valid email address is required.")]
        public string? Email { get; set; }
    }
}
