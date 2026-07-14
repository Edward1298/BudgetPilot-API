namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Represents the result of applying monthly interest to savings accounts.
    /// </summary>
    public class ApplyInterestResultDTO
    {
        /// <summary>
        /// Gets or sets the number of accounts that were updated.
        /// </summary>
        public int RowsAffected { get; set; }
    }
}
