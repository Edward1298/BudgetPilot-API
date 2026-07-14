namespace BudgetPilot_API.Dtos
{
    public class ReportRequestDTO
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public string? Type { get; set; }
    }

    public class CategoryReportDTO
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
        public int TransactionCount { get; set; }
    }

    public class ReportResponseDTO
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TotalTransactions { get; set; }
        public List<CategoryReportDTO> Categories { get; set; } = new();
    }
}
