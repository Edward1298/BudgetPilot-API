using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for creating an account.
    /// Contains the fields accepted from the client request body.
    /// </summary>
    public class AccountsDTO
    {
        /// <summary>
        /// Gets or sets the human-readable label for the account.
        /// Must be between 1 and 100 characters and not whitespace-only.
        /// </summary>
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the account.
        /// Must be one of: bankAccount, savingsAccount, cash.
        /// </summary>
        [Required(ErrorMessage = "Type is required.")]
        [RegularExpression("^(bankAccount|savingsAccount|cash)$",
            ErrorMessage = "Type must be one of: bankAccount, savingsAccount, cash.")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current balance of the account.
        /// Defaults to zero. Validation rules depend on the account type.
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Gets or sets the monthly interest rate for savings accounts.
        /// Required and must be greater than zero when Type is "savingsAccount".
        /// </summary>
        public decimal? InterestRate { get; set; }

        /// <summary>
        /// Gets or sets the currency of the account.
        /// Must be one of: CRC, USD, EUR. Defaults to USD.
        /// </summary>
        [RegularExpression("^(CRC|USD|EUR)$",
            ErrorMessage = "Currency must be one of: CRC, USD, EUR.")]
        public string? Currency { get; set; }
    }
}
