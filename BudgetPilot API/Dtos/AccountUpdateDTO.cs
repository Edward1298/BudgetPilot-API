using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for partial account updates.
    /// Only non-null fields are applied to the account entity.
    /// </summary>
    public class AccountUpdateDTO
    {
        /// <summary>
        /// Gets or sets the updated human-readable label for the account.
        /// When provided, must be between 1 and 100 characters and not whitespace-only.
        /// </summary>
        [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the updated type of the account.
        /// When provided, must be one of: bankAccount, savingsAccount, cash.
        /// </summary>
        [RegularExpression("^(bankAccount|savingsAccount|cash)$",
            ErrorMessage = "Type must be one of: bankAccount, savingsAccount, cash.")]
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets the updated balance of the account.
        /// When provided, validation rules depend on the account type.
        /// </summary>
        public decimal? Balance { get; set; }

        /// <summary>
        /// Gets or sets the updated monthly interest rate for savings accounts.
        /// When provided, must be greater than zero for savingsAccount type.
        /// </summary>
        public decimal? InterestRate { get; set; }
    }
}
