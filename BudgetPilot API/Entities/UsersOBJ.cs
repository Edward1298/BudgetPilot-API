using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetPilot_API.Entities
{
    [Table("users")] // tabla en minúscula como está en PostgreSQL
    public class UsersOBJ
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password_hash")] // snake_case como está en PostgreSQL
        public string PasswordHash { get; set; }

        [Column("created_at")] // snake_case como está en PostgreSQL
        public DateTime CreatedAt { get; set; }
    }
}