using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BudgetPilot_API.Entities
{
    /// <summary>
    /// Represents a user entity mapped to the "users" table in the database.
    /// Contains authentication credentials and profile information.
    /// </summary>
    [Table("users")]
    public class UsersOBJ
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// Generated server-side as a UUID v4.
        /// </summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the user.
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the email address of the user.
        /// Must be unique across the system. Stored in lowercase after normalization.
        /// </summary>
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the BCrypt hash of the user's password.
        /// This field is never exposed in API responses.
        /// </summary>
        [Column("password_hash")]
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the user was registered.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
