using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for creating or updating a transaction.
    /// Contains the fields accepted from the client request body.
    /// </summary>
    public class TransactionsDTO
    {
        /// <summary>
        /// Gets or sets the identifier of the account associated with the transaction.
        /// Must reference an account owned by the authenticated user.
        /// </summary>
        [Required(ErrorMessage = "AccountId is required.")]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the category associated with the transaction.
        /// Must reference a category owned by the authenticated user.
        /// </summary>
        [Required(ErrorMessage = "CategoryId is required.")]
        public Guid CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the monetary amount of the transaction.
        /// Must be strictly greater than zero.
        /// </summary>
        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets the type of the transaction.
        /// Must be one of: income, expense and must match the referenced category's type.
        /// </summary>
        [Required(ErrorMessage = "Type is required.")]
        [RegularExpression("^(income|expense)$",
            ErrorMessage = "Type must be one of: income, expense.")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional note describing the transaction.
        /// Must not exceed 500 characters when provided.
        /// </summary>
        [MaxLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
        public string? Description { get; set; }
    }
}
