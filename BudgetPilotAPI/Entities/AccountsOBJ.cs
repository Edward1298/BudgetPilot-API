using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BudgetPilot_API.Entities
{
    /// <summary>
    /// Represents a financial account entity mapped to the "accounts" table in the database.
    /// Stores account details such as name, type, and balance.
    /// </summary>
    [Table("accounts")]
    public class AccountsOBJ
    {
        /// <summary>
        /// Gets or sets the unique identifier for the account.
        /// Generated server-side as a UUID v4.
        /// </summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who owns this account.
        /// Derived from the JWT token and never exposed to API responses.
        /// </summary>
        [Column("user_id")]
        [JsonIgnore]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable label for the account (e.g. "BBVA Debit").
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the account.
        /// Valid values are Cash, Credit Card, and Bank Account.
        /// </summary>
        [Column("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current balance of the account.
        /// Defaults to zero on creation. Negative balances are allowed only for credit card accounts.
        /// </summary>
        [Column("balance")]
        public decimal Balance { get; set; }

        /// <summary>
        /// Gets or sets the monthly interest rate for savings accounts.
        /// Only applicable when Type is "savingsAccount". Null for other account types.
        /// </summary>
        [Column("interest_rate")]
        public decimal? InterestRate { get; set; }

        /// <summary>
        /// Gets or sets whether this account is active.
        /// Inactive accounts are excluded from queries (soft delete).
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the currency of the account.
        /// Valid values are CRC (Costa Rican Colón), USD (US Dollar), and EUR (Euro).
        /// </summary>
        [Column("currency")]
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets the timestamp when the account was created.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
