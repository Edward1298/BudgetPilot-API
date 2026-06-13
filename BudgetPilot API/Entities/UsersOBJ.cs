using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetPilot_API.Entities
{
    [Table("users")] 
    public class UsersOBJ // This obj is used to map the database table "users" to a C# class. It represents a user entity in the application.
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password_hash")] 
        public string PasswordHash { get; set; }

        [Column("created_at")] 
        public DateTime CreatedAt { get; set; }
    }
}
