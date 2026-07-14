namespace BudgetPilot_API.Dtos
{
    /// <summary>
    /// Represents the account summary result returned by sp_GetAccountSummary,
    /// containing account details and the last 10 active transactions.
    /// </summary>
    public class AccountSummaryDTO
    {
        /// <summary>
        /// Gets or sets the account information.
        /// </summary>
        public AccountInfoDTO Account { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of recent transactions (up to 10).
        /// </summary>
        public List<TransactionInfoDTO> RecentTransactions { get; set; } = new();
    }

    /// <summary>
    /// Represents account information returned in the account summary.
    /// </summary>
    public class AccountInfoDTO
    {
        /// <summary>
        /// Gets or sets the unique identifier of the account.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the user who owns the account.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the name of the account.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the account (cash, bankAccount, savingsAccount).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current balance of the account.
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Gets or sets the interest rate for savings accounts (null for other types).
        /// </summary>
        public decimal? InterestRate { get; set; }

        /// <summary>
        /// Gets or sets whether the account is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the currency of the account (CRC, USD, or EUR).
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets the timestamp when the account was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents transaction information returned in the account summary.
    /// </summary>
    public class TransactionInfoDTO
    {
        /// <summary>
        /// Gets or sets the unique identifier of the transaction.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the linked account.
        /// </summary>
        public Guid AccountId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the linked category.
        /// </summary>
        public Guid CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the transaction amount.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets the transaction type (income or expense).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional transaction description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the transaction date.
        /// </summary>
        public DateOnly Date { get; set; }
    }
}
