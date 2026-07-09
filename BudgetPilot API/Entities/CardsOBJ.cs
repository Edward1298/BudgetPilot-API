using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BudgetPilot_API.Entities
{
    /// <summary>
    /// Represents a payment card (debit or credit) linked to an account.
    /// Card numbers and CVCs are encrypted at rest using AES encryption.
    /// </summary>
    [Table("cards")]
    public class CardsOBJ
    {
        /// <summary>
        /// Gets or sets the unique identifier for the card.
        /// Generated server-side as a UUID v4.
        /// </summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who owns this card.
        /// Derived from the JWT token and never exposed to API responses.
        /// </summary>
        [Column("user_id")]
        [JsonIgnore]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the account linked to this card.
        /// </summary>
        [Column("account_id")]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Gets or sets the type of the card.
        /// Valid values are "debit" and "credit".
        /// </summary>
        [Column("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the encrypted card number.
        /// Stored as base64-encoded ciphertext; decrypted on read.
        /// </summary>
        [Column("card_number")]
        public string CardNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the card expiration date.
        /// </summary>
        [Column("expiration_date")]
        public DateOnly ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the encrypted CVC code.
        /// Stored as base64-encoded ciphertext; decrypted on read.
        /// </summary>
        [Column("cvc")]
        public string Cvc { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name printed on the card.
        /// </summary>
        [Column("name_on_card")]
        public string NameOnCard { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the credit limit for credit cards.
        /// Null for debit cards.
        /// </summary>
        [Column("credit_limit")]
        public decimal? CreditLimit { get; set; }

        /// <summary>
        /// Gets or sets the annual percentage rate for credit cards.
        /// Null for debit cards.
        /// </summary>
        [Column("apr")]
        public decimal? Apr { get; set; }

        /// <summary>
        /// Gets or sets the statement date (day of month, 1-31) for credit cards.
        /// Null for debit cards.
        /// </summary>
        [Column("statement_date")]
        public int? StatementDate { get; set; }

        /// <summary>
        /// Gets or sets the payment due date (day of month, 1-31) for credit cards.
        /// Null for debit cards.
        /// </summary>
        [Column("due_date")]
        public int? DueDate { get; set; }

        /// <summary>
        /// Gets or sets the minimum payment percentage for credit cards.
        /// Null for debit cards.
        /// </summary>
        [Column("minimum_payment_percentage")]
        public decimal? MinimumPaymentPercentage { get; set; }

        /// <summary>
        /// Gets or sets the current outstanding balance for credit cards.
        /// Defaults to 0.
        /// </summary>
        [Column("current_balance")]
        public decimal CurrentBalance { get; set; }

        /// <summary>
        /// Gets or sets whether this card is active.
        /// Inactive cards are excluded from queries (soft delete).
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the timestamp when the card was created.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
