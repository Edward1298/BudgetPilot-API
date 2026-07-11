using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Dtos
{
    public class BudgetUpdateDTO
    {
        public Guid? CategoryId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal? Amount { get; set; }

        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
        public int? Month { get; set; }

        [Range(2000, 2100, ErrorMessage = "Year must be a valid year.")]
        public int? Year { get; set; }
    }
}
