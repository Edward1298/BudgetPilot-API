using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for creating or updating a role.
    /// Contains the fields accepted from the client request body.
    /// </summary>
    public class RolesDTO
    {
        /// <summary>
        /// Gets or sets the name of the role.
        /// Must be between 1 and 50 characters and not whitespace-only.
        /// </summary>
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(50, ErrorMessage = "Name must not exceed 50 characters.")]
        public string Name { get; set; } = string.Empty;
    }
}
