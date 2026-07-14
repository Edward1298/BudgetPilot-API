using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BudgetPilot_API.Entities
{
    /// <summary>
    /// Represents a financial transaction mapped to the "transactions" table.
    /// Each transaction links to an account and a category belonging to the same user.
    /// </summary>
    [Table("transactions")]
    public class TransactionsOBJ
    {
        /// <summary>
        /// Gets or sets the unique identifier for the transaction.
        /// Generated server-side as a UUID v4.
        /// </summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who owns this transaction.
        /// Derived from the JWT token and never exposed to API responses.
        /// </summary>
        [Column("user_id")]
        [JsonIgnore]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the account associated with this transaction.
        /// </summary>
        [Column("account_id")]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the category associated with this transaction.
        /// </summary>
        [Column("category_id")]
        public Guid CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the monetary amount of the transaction.
        /// Must be strictly greater than zero.
        /// </summary>
        [Column("amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets the type of the transaction.
        /// Valid values are "income" and "expense" and must match the linked category's type.
        /// </summary>
        [Column("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional note describing the transaction.
        /// </summary>
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the date the transaction occurred.
        /// Assigned server-side on creation and never modified afterward.
        /// </summary>
        [Column("date")]
        public DateOnly Date { get; set; }
    }
}
