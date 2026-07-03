using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for creating or updating a category.
    /// Contains the fields accepted from the client request body.
    /// </summary>
    public class CategoriesDTO
    {
        /// <summary>
        /// Gets or sets the human-readable label for the category.
        /// Must be between 1 and 100 characters and not whitespace-only.
        /// </summary>
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
        [RegularExpression(@"^.*\S.*$", ErrorMessage = "Name cannot be whitespace-only.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the category.
        /// Must be one of: income, expense.
        /// </summary>
        [Required(ErrorMessage = "Type is required.")]
        [RegularExpression("^(income|expense)$",
            ErrorMessage = "Type must be one of: income, expense.")]
        public string Type { get; set; } = string.Empty;
    }
}
