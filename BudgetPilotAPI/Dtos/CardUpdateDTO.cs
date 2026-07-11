using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Data transfer object for updating a card (partial update).
    /// All fields are optional.
    /// </summary>
    public class CardUpdateDTO
    {
        /// <summary>
        /// Gets or sets the card number.
        /// </summary>
        [StringLength(20, MinimumLength = 13, ErrorMessage = "CardNumber must be between 13 and 20 characters.")]
        public string? CardNumber { get; set; }

        /// <summary>
        /// Gets or sets the expiration date.
        /// </summary>
        public DateOnly? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the CVC code.
        /// </summary>
        [StringLength(4, MinimumLength = 3, ErrorMessage = "Cvc must be 3 or 4 digits.")]
        public string? Cvc { get; set; }

        /// <summary>
        /// Gets or sets the name on the card.
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
