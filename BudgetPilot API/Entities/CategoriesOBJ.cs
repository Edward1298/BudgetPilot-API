using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BudgetPilot_API.Entities
{
    /// <summary>
    /// Represents an income or expense category mapped to the "categories" table.
    /// Categories are scoped to a single user and referenced by transactions.
    /// </summary>
    [Table("categories")]
    public class CategoriesOBJ
    {
        /// <summary>
        /// Gets or sets the unique identifier for the category.
        /// Generated server-side as a UUID v4.
        /// </summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who owns this category.
        /// Derived from the JWT token and never exposed to API responses.
        /// </summary>
        [Column("user_id")]
        [JsonIgnore]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable label for the category (e.g. "Food", "Salary").
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the category.
        /// Valid values are "income" and "expense".
        /// </summary>
        [Column("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this category is active.
        /// Inactive categories are excluded from queries (soft delete).
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
