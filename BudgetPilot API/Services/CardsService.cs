using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using BudgetPilot_API.Services;
using Microsoft.EntityFrameworkCore;

namespace BudgetPilot_API.Services
{
    /// <summary>
    /// Service for managing payment cards with encryption support.
    /// </summary>
    public class CardsService
    {
        private readonly AppDbContext _context;
        private readonly DataProtectionService _dataProtection;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the CardsService.
        /// </summary>
        public CardsService(AppDbContext context, DataProtectionService dataProtection, IConfiguration configuration)
        {
            _context = context;
            _dataProtection = dataProtection;
            _configuration = configuration;
        }

        /// <summary>
        /// Gets all cards for a user with pagination and optional filtering.
        /// </summary>
        public async Task<(List<CardsOBJ> Items, int TotalCount)> GetCards(
            Guid userId, int page, int pageSize, string? type = null)
        {
            var query = _context.Cards
                .Where(c => c.UserId == userId && c.IsActive);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(c => c.Type == type);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            foreach (var card in items)
            {
                card.CardNumber = _dataProtection.Decrypt(card.CardNumber);
                card.Cvc = _dataProtection.Decrypt(card.Cvc);
            }

            return (items, totalCount);
        }

        /// <summary>
        /// Gets a single card by ID.
        /// </summary>
        public async Task<CardsOBJ?> GetCardById(Guid id)
        {
            var card = await _context.Cards
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (card != null)
            {
                card.CardNumber = _dataProtection.Decrypt(card.CardNumber);
                card.Cvc = _dataProtection.Decrypt(card.Cvc);
            }

            return card;
        }

        /// <summary>
        /// Creates a new card with encrypted sensitive data.
        /// Applies default APR and minimum payment percentage for credit cards
        /// when not explicitly provided, and falls back to the user's name
        /// when NameOnCard is not provided.
        /// </summary>
        public async Task<CardsOBJ> CreateCard(CardsDTO dto, Guid userId)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.UserId == userId && a.IsActive);

            if (account == null)
                throw new InvalidOperationException("Account not found or does not belong to the user.");

            var nameOnCard = dto.NameOnCard;
            if (string.IsNullOrWhiteSpace(nameOnCard))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                nameOnCard = user?.Name ?? string.Empty;
            }

            var apr = dto.Apr;
            var minimumPaymentPercentage = dto.MinimumPaymentPercentage;

            if (dto.Type == "credit")
            {
                apr ??= _configuration.GetValue<decimal?>("CardDefaults:DefaultApr");
                minimumPaymentPercentage ??= _configuration.GetValue<decimal?>("CardDefaults:DefaultMinimumPaymentPercentage");
            }

            var card = new CardsOBJ
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountId = dto.AccountId,
                Type = dto.Type,
                CardNumber = _dataProtection.Encrypt(dto.CardNumber),
                ExpirationDate = dto.ExpirationDate,
                Cvc = _dataProtection.Encrypt(dto.Cvc),
                NameOnCard = nameOnCard,
                CreditLimit = dto.CreditLimit,
                Apr = apr,
                StatementDate = dto.StatementDate,
                DueDate = dto.DueDate,
                MinimumPaymentPercentage = minimumPaymentPercentage,
                CurrentBalance = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Cards.Add(card);
            await _context.SaveChangesAsync();

            return new CardsOBJ
            {
                Id = card.Id,
                UserId = card.UserId,
                AccountId = card.AccountId,
                Type = card.Type,
                CardNumber = _dataProtection.Decrypt(card.CardNumber),
                ExpirationDate = card.ExpirationDate,
                Cvc = _dataProtection.Decrypt(card.Cvc),
                NameOnCard = card.NameOnCard,
                CreditLimit = card.CreditLimit,
                Apr = card.Apr,
                StatementDate = card.StatementDate,
                DueDate = card.DueDate,
                MinimumPaymentPercentage = card.MinimumPaymentPercentage,
                CurrentBalance = card.CurrentBalance,
                IsActive = card.IsActive,
                CreatedAt = card.CreatedAt
            };
        }

        /// <summary>
        /// Updates a card with partial data.
        /// </summary>
        public async Task<CardsOBJ?> UpdateCard(Guid id, CardUpdateDTO dto, Guid userId)
        {
            var card = await _context.Cards
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.IsActive);

            if (card == null)
                return null;

            if (dto.CardNumber != null)
                card.CardNumber = _dataProtection.Encrypt(dto.CardNumber);

            if (dto.ExpirationDate.HasValue)
                card.ExpirationDate = dto.ExpirationDate.Value;

            if (dto.Cvc != null)
                card.Cvc = _dataProtection.Encrypt(dto.Cvc);

            if (dto.NameOnCard != null)
                card.NameOnCard = dto.NameOnCard;

            if (dto.CreditLimit.HasValue)
                card.CreditLimit = dto.CreditLimit;

            if (dto.Apr.HasValue)
                card.Apr = dto.Apr;

            if (dto.StatementDate.HasValue)
                card.StatementDate = dto.StatementDate;

            if (dto.DueDate.HasValue)
                card.DueDate = dto.DueDate;

            if (dto.MinimumPaymentPercentage.HasValue)
                card.MinimumPaymentPercentage = dto.MinimumPaymentPercentage;

            await _context.SaveChangesAsync();

            return new CardsOBJ
            {
                Id = card.Id,
                UserId = card.UserId,
                AccountId = card.AccountId,
                Type = card.Type,
                CardNumber = _dataProtection.Decrypt(card.CardNumber),
                ExpirationDate = card.ExpirationDate,
                Cvc = _dataProtection.Decrypt(card.Cvc),
                NameOnCard = card.NameOnCard,
                CreditLimit = card.CreditLimit,
                Apr = card.Apr,
                StatementDate = card.StatementDate,
                DueDate = card.DueDate,
                MinimumPaymentPercentage = card.MinimumPaymentPercentage,
                CurrentBalance = card.CurrentBalance,
                IsActive = card.IsActive,
                CreatedAt = card.CreatedAt
            };
        }

        /// <summary>
        /// Deletes a card (soft delete for regular users, hard delete for admins).
        /// </summary>
        public async Task<bool> DeleteCard(Guid id, Guid userId, bool isAdmin)
        {
            var card = await _context.Cards
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.IsActive);

            if (card == null)
                return false;

            if (isAdmin)
                _context.Cards.Remove(card);
            else
                card.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
