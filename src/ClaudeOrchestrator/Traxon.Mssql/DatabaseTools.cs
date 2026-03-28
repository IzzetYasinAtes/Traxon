using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;

namespace Traxon.Mssql;

[McpServerToolType]
public partial class DatabaseTools
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TrAxonCryptoTrader;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly HashSet<string> BlockedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE", "SP_", "XP_", "GRANT", "REVOKE", "DENY"
    };

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("TRAXON_DB_CONNECTION")
            ?? Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING")
            ?? DefaultConnectionString;
    }

    [McpServerTool(Name = "sql_query"), Description("Execute a SQL query against the database. SELECT returns JSON rows. INSERT/UPDATE/DELETE returns affected row count. DROP/TRUNCATE/ALTER/CREATE are blocked.")]
    public async Task<string> SqlQuery([Description("SQL query to execute")] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "Error: SQL query cannot be empty.";

        var normalized = sql.Trim();

        if (IsBlocked(normalized))
            return "Error: This SQL command is not allowed. Only SELECT, INSERT, UPDATE, DELETE are permitted.";

        try
        {
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            if (normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteSelectAsync(connection, normalized);
            }

            return await ExecuteDmlAsync(connection, normalized);
        }
        catch (SqlException ex)
        {
            return $"SQL Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_database_info", ReadOnly = true), Description("Get all table names and their column information from the database")]
    public async Task<string> GetDatabaseInfo()
    {
        const string query = """
            SELECT
                t.TABLE_SCHEMA,
                t.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.TABLES t
            INNER JOIN INFORMATION_SCHEMA.COLUMNS c
                ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION
            """;

        try
        {
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var sb = new StringBuilder();
            string? currentTable = null;

            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var table = reader.GetString(1);
                var fullName = $"{schema}.{table}";

                if (fullName != currentTable)
                {
                    if (currentTable is null)
                        sb.AppendLine("# Database Schema\n");
                    else
                        sb.AppendLine();

                    sb.AppendLine($"## {fullName}");
                    sb.AppendLine("| Column | Type | Nullable |");
                    sb.AppendLine("|--------|------|----------|");
                    currentTable = fullName;
                }

                var column = reader.GetString(2);
                var dataType = reader.GetString(3);
                var nullable = reader.GetString(4);
                var maxLen = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5);

                var typeStr = maxLen.HasValue ? $"{dataType}({maxLen})" : dataType;
                sb.AppendLine($"| {column} | {typeStr} | {nullable} |");
            }

            return sb.Length > 0 ? sb.ToString() : "No tables found in the database.";
        }
        catch (SqlException ex)
        {
            return $"SQL Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_trade_summary", ReadOnly = true), Description("Get trade count, PnL summary, and win rate from the database")]
    public async Task<string> GetTradeSummary()
    {
        const string query = """
            SELECT
                COUNT(*) AS TotalTrades,
                SUM(CASE WHEN PnL > 0 THEN 1 ELSE 0 END) AS WinningTrades,
                SUM(CASE WHEN PnL < 0 THEN 1 ELSE 0 END) AS LosingTrades,
                SUM(PnL) AS TotalPnL,
                AVG(PnL) AS AvgPnL,
                MAX(PnL) AS BestTrade,
                MIN(PnL) AS WorstTrade
            FROM Trades
            WHERE ClosedAt IS NOT NULL
            """;

        try
        {
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return "No trade data found.";

            var totalTrades = reader.GetInt32(0);
            if (totalTrades == 0)
                return "No closed trades found.";

            var winningTrades = reader.GetInt32(1);
            var losingTrades = reader.GetInt32(2);
            var totalPnL = reader.GetDecimal(3);
            var avgPnL = reader.GetDecimal(4);
            var bestTrade = reader.GetDecimal(5);
            var worstTrade = reader.GetDecimal(6);
            var winRate = (double)winningTrades / totalTrades * 100;

            var sb = new StringBuilder();
            sb.AppendLine("# Trade Summary\n");
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Total Trades | {totalTrades} |");
            sb.AppendLine($"| Winning | {winningTrades} |");
            sb.AppendLine($"| Losing | {losingTrades} |");
            sb.AppendLine($"| Win Rate | {winRate:F1}% |");
            sb.AppendLine($"| Total PnL | {totalPnL:F2} |");
            sb.AppendLine($"| Avg PnL | {avgPnL:F2} |");
            sb.AppendLine($"| Best Trade | {bestTrade:F2} |");
            sb.AppendLine($"| Worst Trade | {worstTrade:F2} |");

            return sb.ToString();
        }
        catch (SqlException ex)
        {
            return $"SQL Error: {ex.Message}";
        }
    }

    private static bool IsBlocked(string sql)
    {
        var firstWord = sql.Split([' ', '\t', '\n', '\r', '(', ';'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToUpperInvariant();

        if (firstWord is not ("SELECT" or "INSERT" or "UPDATE" or "DELETE"))
            return true;

        var words = WordBoundaryRegex().Matches(sql);
        foreach (Match word in words)
        {
            if (BlockedKeywords.Contains(word.Value))
                return true;
        }

        return false;
    }

    private static async Task<string> ExecuteSelectAsync(SqlConnection connection, string sql)
    {
        const int rowLimit = 1000;

        var limitedSql = TopLimitRegex().IsMatch(sql)
            ? sql
            : TopLimitRegex().Replace(sql, m => m.Value + $" TOP {rowLimit}", 1);

        if (limitedSql == sql)
            limitedSql = SelectRegex().Replace(sql, $"SELECT TOP {rowLimit}", 1);

        await using var command = new SqlCommand(limitedSql, connection);
        command.CommandTimeout = 30;
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        if (rows.Count == 0)
            return "Query returned 0 rows.";

        var result = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });

        if (rows.Count >= rowLimit)
            result += $"\n\n(1000 satir limiti uygulandi)";

        return result;
    }

    private static async Task<string> ExecuteDmlAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 30;
        var affected = await command.ExecuteNonQueryAsync();
        return $"{affected} row(s) affected.";
    }

    [GeneratedRegex(@"\b\w+\b")]
    private static partial Regex WordBoundaryRegex();

    [GeneratedRegex(@"\bSELECT\s+TOP\s+\d+", RegexOptions.IgnoreCase)]
    private static partial Regex TopLimitRegex();

    [GeneratedRegex(@"\bSELECT\b", RegexOptions.IgnoreCase)]
    private static partial Regex SelectRegex();
}
