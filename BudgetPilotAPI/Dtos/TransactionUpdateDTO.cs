using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for partial transaction updates.
    /// Only non-null fields are applied to the transaction entity.
    /// The transaction type is derived from the referenced category.
    /// </summary>
    public class TransactionUpdateDTO
    {
        /// <summary>
        /// Gets or sets the updated identifier of the account associated with the transaction.
        /// When provided, must reference an account owned by the authenticated user.
        /// </summary>
        public Guid? AccountId { get; set; }

        /// <summary>
        /// Gets or sets the updated identifier of the category associated with the transaction.
        /// When provided, must reference a category owned by the authenticated user.
        /// The transaction type is derived from this category's type.
        /// </summary>
        public Guid? CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the updated monetary amount of the transaction.
        /// When provided, must be strictly greater than zero.
        /// </summary>
        [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal? Amount { get; set; }

        /// <summary>
        /// Gets or sets the updated note describing the transaction.
        /// When provided, must not exceed 500 characters.
        /// </summary>
        [MaxLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
        public string? Description { get; set; }
    }
}
