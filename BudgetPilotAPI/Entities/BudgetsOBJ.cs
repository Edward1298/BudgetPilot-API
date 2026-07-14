using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BudgetPilot_API.Entities
{
    /// <summary>
    /// Represents a budget entity mapped to the "budgets" table in the database.
    /// Budgets track monthly spending limits per category for a user.
    /// </summary>
    [Table("budgets")]
    public class BudgetsOBJ
    {
        /// <summary>
        /// Gets or sets the unique identifier for the budget.
        /// Generated server-side as a UUID v4.
        /// </summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who owns this budget.
        /// Derived from the JWT token and never exposed to API responses.
        /// </summary>
        [Column("user_id")]
        [JsonIgnore]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the category this budget applies to.
        /// </summary>
        [Column("category_id")]
        public Guid CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the budget limit amount for the month.
        /// </summary>
        [Column("amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets the month (1-12) this budget applies to.
        /// </summary>
        [Column("month")]
        public int Month { get; set; }

        /// <summary>
        /// Gets or sets the year this budget applies to.
        /// </summary>
        [Column("year")]
        public int Year { get; set; }

        /// <summary>
        /// Gets or sets whether this budget is active.
        /// Inactive budgets are excluded from queries (soft delete).
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the timestamp when the budget was created.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
