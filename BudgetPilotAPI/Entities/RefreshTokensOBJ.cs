using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetPilot_API.Entities
{
    [Table("refresh_tokens")]
    public class RefreshTokensOBJ
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }
    }
}
