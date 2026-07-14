using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for partial category updates.
    /// Only non-null fields are applied to the category entity.
    /// </summary>
    public class CategoryUpdateDTO
    {
        /// <summary>
        /// Gets or sets the updated human-readable label for the category.
        /// When provided, must be between 1 and 100 characters and not whitespace-only.
        /// </summary>
        [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the updated type of the category.
        /// When provided, must be one of: income, expense.
        /// </summary>
        [RegularExpression("^(income|expense)$",
            ErrorMessage = "Type must be one of: income, expense.")]
        public string? Type { get; set; }
    }
}
