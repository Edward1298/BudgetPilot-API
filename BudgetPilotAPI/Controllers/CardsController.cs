using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;
using BudgetPilot_API.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace BudgetPilot_API.Controllers
{
    /// <summary>
    /// Controller for managing payment cards.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class CardsController : ControllerBase
    {
        private readonly CardsService _cardsService;

        /// <summary>
        /// Initializes a new instance of the CardsController.
        /// </summary>
        public CardsController(CardsService cardsService)
        {
            _cardsService = cardsService;
        }

        /// <summary>
        /// Gets all cards for the authenticated user with pagination.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCards(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? type = null)
        {
            if (page < 1)
                return BadRequest(new { statusCode = 400, message = "Page must be 1 or greater.", errors = Array.Empty<object>() });

            var userId = GetUserId();
            if (userId == null)
                return UnauthorizedError();

            var (items, totalCount) = await _cardsService.GetCards(userId.Value, page, pageSize, type);

            return Ok(new
            {
                data = items,
                page,
                pageSize,
                totalCount
            });
        }

        /// <summary>
        /// Gets a single card by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetCardById(Guid id)
        {
            var userId = GetUserId();
            if (userId == null)
                return UnauthorizedError();

            var card = await _cardsService.GetCardById(id);

            if (card == null)
                return NotFoundError("Card not found.");

            if (card.UserId != userId.Value)
                return ForbiddenError("You do not have access to this card.");

            return Ok(card);
        }

        /// <summary>
        /// Creates a new card.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateCard([FromBody] CardsDTO dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return UnauthorizedError();

            if (!ModelState.IsValid)
                return ValidationError();

            try
            {
                var card = await _cardsService.CreateCard(dto, userId.Value);
                return CreatedAtAction(nameof(GetCardById), new { id = card.Id }, card);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    statusCode = 400,
                    message = ex.Message,
                    errors = Array.Empty<object>()
                });
            }
            catch (CryptographicException)
            {
                return BadRequest(new
                {
                    statusCode = 400,
                    message = "Failed to process card data. Please contact support.",
                    errors = Array.Empty<object>()
                });
            }
        }

        /// <summary>
        /// Updates a card with partial data.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateCard(Guid id, [FromBody] CardUpdateDTO dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return UnauthorizedError();

            if (!ModelState.IsValid)
                return ValidationError();

            var card = await _cardsService.UpdateCard(id, dto, userId.Value);

            if (card == null)
            {
                var exists = await _cardsService.GetCardById(id);
                if (exists != null && exists.UserId != userId.Value)
                    return ForbiddenError("You do not have access to this card.");

                return NotFoundError("Card not found.");
            }

            return Ok(card);
        }

        /// <summary>
        /// Deletes a card (soft delete for regular users, hard delete for admins).
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCard(Guid id)
        {
            var userId = GetUserId();
            if (userId == null)
                return UnauthorizedError();

            var isAdmin = IsAdmin();
            var deleted = await _cardsService.DeleteCard(id, userId.Value, isAdmin);

            if (!deleted)
            {
                var exists = await _cardsService.GetCardById(id);
                if (exists != null && exists.UserId != userId.Value)
                    return ForbiddenError("You do not have access to this card.");

                return NotFoundError("Card not found.");
            }

            return NoContent();
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
                return null;
            return userId;
        }

        private bool IsAdmin()
        {
            return User.IsInRole("Admin");
        }

        private IActionResult UnauthorizedError()
        {
            return Unauthorized(new
            {
                statusCode = 401,
                message = "Authentication required.",
                errors = Array.Empty<object>()
            });
        }

        private IActionResult ForbiddenError(string message)
        {
            return StatusCode(403, new
            {
                statusCode = 403,
                message,
                errors = Array.Empty<object>()
            });
        }

        private IActionResult NotFoundError(string message)
        {
            return NotFound(new
            {
                statusCode = 404,
                message,
                errors = Array.Empty<object>()
            });
        }

        private IActionResult ValidationError()
        {
            var errors = new List<object>();

            foreach (var entry in ModelState)
            {
                foreach (var error in entry.Value.Errors)
                {
                    errors.Add(new
                    {
                        field = JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                        message = error.ErrorMessage
                    });
                }
            }

            return BadRequest(new
            {
                statusCode = 400,
                message = "Validation failed.",
                errors = errors.ToArray()
            });
        }
    }
}
