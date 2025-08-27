using System.Globalization;
using System.Text;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for exporting financial transaction data to CSV format
/// Business Context: Provides standardized CSV exports for financial reporting and external analysis
/// 
/// Key Features:
/// - German number format conversion to international format (1.234,56 → 1234.56)
/// - UTF-8 BOM support for proper German character display in Excel
/// - Standardized header format for consistent data structure
/// - Culture-invariant number formatting for international compatibility
/// </summary>
public class CsvExportService
{
    private readonly ILogger<CsvExportService> _logger;
    
    public CsvExportService(ILogger<CsvExportService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Export transaction lines to CSV format with proper German character support
    /// 
    /// CSV Format:
    /// - Headers: Year,Month,Category,Description,Amount,Type
    /// - Numbers: International format with dot as decimal separator (1234.56)
    /// - Encoding: UTF-8 with BOM for Excel compatibility
    /// - No thousands separators in numeric values
    /// 
    /// Business Rules:
    /// - Amounts are converted from internal German decimal format to international
    /// - Category names preserved as-is to maintain German business terminology
    /// - Types exported as enum names (Revenue, Expense, etc.)
    /// 
    /// @param transactions - List of TransactionLine objects to export
    /// @returns UTF-8 encoded CSV content as byte array with BOM
    /// </summary>
    public byte[] ExportTransactionsToCsv(IEnumerable<TransactionLine> transactions)
    {
        if (transactions == null)
        {
            throw new ArgumentNullException(nameof(transactions));
        }
        
        var csv = new StringBuilder();
        
        // Add CSV headers - standardized format for consistent imports
        csv.AppendLine("Year,Month,Category,Description,Amount,Type");
        
        // Process each transaction with proper formatting
        foreach (var transaction in transactions)
        {
            // Convert amount to international format (dot as decimal separator)
            // This ensures CSV can be imported into international accounting software
            var amountFormatted = transaction.Amount.ToString("F2", CultureInfo.InvariantCulture);
            
            // Escape category for CSV - wrap in quotes if contains comma or special chars
            // Note: Category field serves as both category and description in the TransactionLine model
            var categoryEscaped = EscapeCsvField(transaction.Category);
            
            // Build CSV line with proper escaping
            // Using Category for both category and description columns as per model design
            var line = $"{transaction.Year},{transaction.Month},{categoryEscaped},{categoryEscaped},{amountFormatted},{transaction.Type}";
            csv.AppendLine(line);
        }
        
        // Add UTF-8 BOM for proper German character display in Excel
        // This ensures umlauts and special characters display correctly
        var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
        var bomBytes = Encoding.UTF8.GetPreamble();
        var result = new byte[bomBytes.Length + csvBytes.Length];
        
        Array.Copy(bomBytes, 0, result, 0, bomBytes.Length);
        Array.Copy(csvBytes, 0, result, bomBytes.Length, csvBytes.Length);
        
        _logger.LogInformation($"Exported {transactions.Count()} transactions to CSV format");
        
        return result;
    }
    
    /// <summary>
    /// Export monthly summary data to CSV format
    /// Provides aggregated view of financial performance by month
    /// 
    /// @param summaries - Monthly summary data from DataImportService
    /// @returns UTF-8 encoded CSV with monthly aggregates
    /// </summary>
    public byte[] ExportMonthlySummaryToCsv(IEnumerable<MonthSummary> summaries)
    {
        if (summaries == null)
        {
            throw new ArgumentNullException(nameof(summaries));
        }
        
        var csv = new StringBuilder();
        
        // Headers for monthly summary export
        csv.AppendLine("Year,Month,MonthName,TotalRevenue,TotalExpenses,NetResult,TransactionCount,ProfitMargin");
        
        foreach (var summary in summaries)
        {
            // Format all decimals with international format
            var revenue = summary.TotalRevenue.ToString("F2", CultureInfo.InvariantCulture);
            var expenses = summary.TotalExpenses.ToString("F2", CultureInfo.InvariantCulture);
            var netResult = summary.NetResult.ToString("F2", CultureInfo.InvariantCulture);
            var profitMargin = summary.ProfitMargin.ToString("F2", CultureInfo.InvariantCulture);
            
            var monthNameEscaped = EscapeCsvField(summary.MonthName);
            
            var line = $"{summary.Year},{summary.Month},{monthNameEscaped},{revenue},{expenses},{netResult},{summary.TransactionCount},{profitMargin}";
            csv.AppendLine(line);
        }
        
        // Add UTF-8 BOM
        var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
        var bomBytes = Encoding.UTF8.GetPreamble();
        var result = new byte[bomBytes.Length + csvBytes.Length];
        
        Array.Copy(bomBytes, 0, result, 0, bomBytes.Length);
        Array.Copy(csvBytes, 0, result, bomBytes.Length, csvBytes.Length);
        
        _logger.LogInformation($"Exported {summaries.Count()} monthly summaries to CSV format");
        
        return result;
    }
    
    /// <summary>
    /// Escape CSV field if it contains special characters
    /// Handles commas, quotes, and line breaks in data fields
    /// 
    /// CSV escaping rules:
    /// - Fields containing commas, quotes, or line breaks must be wrapped in quotes
    /// - Internal quotes are escaped by doubling them (" becomes "")
    /// 
    /// @param field - Field value to escape
    /// @returns Properly escaped CSV field value
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }
        
        // Check if field needs escaping (contains comma, quote, or newline)
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            // Escape internal quotes by doubling them
            var escaped = field.Replace("\"", "\"\"");
            
            // Wrap in quotes
            return $"\"{escaped}\"";
        }
        
        return field;
    }
}