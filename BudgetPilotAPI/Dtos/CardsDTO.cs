using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for creating a new card.
    /// </summary>
    public class CardsDTO
    {
        /// <summary>
        /// Gets or sets the account ID to link this card to.
        /// </summary>
        [Required(ErrorMessage = "AccountId is required.")]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Gets or sets the card type (debit or credit).
        /// </summary>
        [Required(ErrorMessage = "Type is required.")]
        [RegularExpression("^(debit|credit)$", ErrorMessage = "Type must be 'debit' or 'credit'.")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the card number.
        /// </summary>
        [Required(ErrorMessage = "CardNumber is required.")]
        [StringLength(20, MinimumLength = 13, ErrorMessage = "CardNumber must be between 13 and 20 characters.")]
        public string CardNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expiration date.
        /// </summary>
        [Required(ErrorMessage = "ExpirationDate is required.")]
        public DateOnly ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the CVC code.
        /// </summary>
        [Required(ErrorMessage = "Cvc is required.")]
        [StringLength(4, MinimumLength = 3, ErrorMessage = "Cvc must be 3 or 4 digits.")]
        public string Cvc { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name on the card. Falls back to the user's name if not provided.
        /// </summary>
        [StringLength(100, ErrorMessage = "NameOnCard must not exceed 100 characters.")]
        public string? NameOnCard { get; set; }

        /// <summary>
        /// Gets or sets the credit limit (credit cards only).
        /// </summary>
        [Range(0.01, double.MaxValue, ErrorMessage = "CreditLimit must be greater than 0.")]
        public decimal? CreditLimit { get; set; }

        /// <summary>
        /// Gets or sets the annual percentage rate (credit cards only).
        /// </summary>
        [Range(0, 100, ErrorMessage = "Apr must be between 0 and 100.")]
        public decimal? Apr { get; set; }

        /// <summary>
        /// Gets or sets the statement date (1-31, credit cards only).
        /// </summary>
        [Range(1, 31, ErrorMessage = "StatementDate must be between 1 and 31.")]
        public int? StatementDate { get; set; }

        /// <summary>
        /// Gets or sets the due date (1-31, credit cards only).
        /// </summary>
        [Range(1, 31, ErrorMessage = "DueDate must be between 1 and 31.")]
        public int? DueDate { get; set; }

        /// <summary>
        /// Gets or sets the minimum payment percentage (credit cards only).
        /// </summary>
        [Range(0, 100, ErrorMessage = "MinimumPaymentPercentage must be between 0 and 100.")]
        public decimal? MinimumPaymentPercentage { get; set; }
    }
}
