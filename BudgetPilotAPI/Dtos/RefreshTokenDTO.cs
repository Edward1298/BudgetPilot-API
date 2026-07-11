using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    public class RefreshTokenRequestDTO
    {
        [Required(ErrorMessage = "RefreshToken is required.")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenResponseDTO
    {
        public string Token { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAt { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshExpiresAt { get; set; }
    }

    public class LogoutDTO
    {
        [Required(ErrorMessage = "RefreshToken is required.")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
