using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetPilot_API.Entities
{
    /// <summary>
    /// Represents a user role entity mapped to the "roles" table in the database.
    /// Roles define authorization levels (Admin, User) for access control.
    /// </summary>
    [Table("roles")]
    public class RolesOBJ
    {
        /// <summary>
        /// Gets or sets the unique identifier for the role.
        /// Generated server-side as a UUID v4.
        /// </summary>
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the role.
        /// Valid values are "Admin" and "User".
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
