using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for creating or updating an account.
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
        /// Must be one of: cash, creditCard, bankAccount.
        /// </summary>
        [Required(ErrorMessage = "Type is required.")]
        [RegularExpression("^(cash|creditCard|bankAccount)$",
            ErrorMessage = "Type must be one of: cash, creditCard, bankAccount.")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current balance of the account.
        /// Defaults to zero. Must be non-negative for <c>cash</c> and <c>bankAccount</c> types.
        /// </summary>
        public decimal Balance { get; set; }
    }
}
