using BudgetPilot_API.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides methods to execute stored procedures using ADO.NET for performance-critical operations.
/// </summary>
public class StoredProcedureService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoredProcedureService"/> class
    /// with the specified database context.
    /// </summary>
    /// <param name="context">The application database context for accessing the connection string.</param>
    public StoredProcedureService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Executes sp_GetAccountSummary to retrieve account details and the last 10 active transactions
    /// in a single database round-trip.
    /// </summary>
    /// <param name="accountId">The unique identifier of the account to summarize.</param>
    /// <param name="userId">The unique identifier of the authenticated user for ownership verification.</param>
    /// <returns>
    /// An <see cref="AccountSummaryDTO"/> containing account info and recent transactions,
    /// or null if the account is not found or does not belong to the user.
    /// </returns>
    public async Task<AccountSummaryDTO?> GetAccountSummaryAsync(Guid accountId, Guid userId)
    {
        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
            return null;

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand("dbo.sp_GetAccountSummary", connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@AccountId", accountId);
        command.Parameters.AddWithValue("@UserId", userId);

        using var reader = await command.ExecuteReaderAsync();

        var summary = new AccountSummaryDTO();

        if (await reader.ReadAsync())
        {
            summary.Account = new AccountInfoDTO
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                UserId = reader.GetGuid(reader.GetOrdinal("user_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Type = reader.GetString(reader.GetOrdinal("type")),
                Balance = reader.GetDecimal(reader.GetOrdinal("balance")),
                InterestRate = reader.IsDBNull(reader.GetOrdinal("interest_rate"))
                    ? null
                    : reader.GetDecimal(reader.GetOrdinal("interest_rate")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                Currency = reader.GetString(reader.GetOrdinal("currency"))
            };
        }
        else
        {
            return null;
        }

        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                summary.RecentTransactions.Add(new TransactionInfoDTO
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    AccountId = reader.GetGuid(reader.GetOrdinal("account_id")),
                    CategoryId = reader.GetGuid(reader.GetOrdinal("category_id")),
                    Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                    Type = reader.GetString(reader.GetOrdinal("type")),
                    Description = reader.IsDBNull(reader.GetOrdinal("description"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("description")),
                    Date = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("date")))
                });
            }
        }

        return summary;
    }

    /// <summary>
    /// Executes sp_ApplyMonthlyInterest to apply interest to all active savings accounts.
    /// </summary>
    /// <returns>
    /// An <see cref="ApplyInterestResultDTO"/> containing the number of accounts updated.
    /// </returns>
    public async Task<ApplyInterestResultDTO> ApplyMonthlyInterestAsync()
    {
        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
            return new ApplyInterestResultDTO { RowsAffected = 0 };

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand("dbo.sp_ApplyMonthlyInterest", connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };

        var rowsAffected = await command.ExecuteNonQueryAsync();

        return new ApplyInterestResultDTO { RowsAffected = rowsAffected };
    }
}
