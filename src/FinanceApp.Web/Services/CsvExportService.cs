using System.Globalization;
using System.Text;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for exporting financial transaction data to CSV format with filtering capabilities
/// Business Context: Supports German financial data export with proper formatting for Excel compatibility
/// 
/// Key Features:
/// - Date range filtering with inclusive boundaries
/// - Transaction type filtering (Revenue, Expense, Both)
/// - German number formatting (1.234,56) for Excel compatibility
/// - UTF-8 BOM for proper character encoding in German Excel
/// - Comprehensive data validation and edge case handling
/// </summary>
public class CsvExportService
{
    private readonly DataImportService _dataImportService;
    private readonly ILogger<CsvExportService> _logger;
    private readonly IInputSanitizationService _inputSanitization;

    public CsvExportService(
        DataImportService dataImportService,
        ILogger<CsvExportService> logger,
        IInputSanitizationService inputSanitization)
    {
        _dataImportService = dataImportService;
        _logger = logger;
        _inputSanitization = inputSanitization;
    }

    /// <summary>
    /// Export transaction data to CSV format with comprehensive filtering options
    /// 
    /// Business Rules:
    /// - Date filtering uses inclusive boundaries (both startDate and endDate included)
    /// - Transaction type filtering: "Revenue", "Expense", "Both", or null (defaults to "Both")
    /// - German number formatting ensures Excel compatibility
    /// - All text fields are sanitized for CSV safety
    /// 
    /// @param startDate - Optional start date filter (inclusive)
    /// @param endDate - Optional end date filter (inclusive)
    /// @param transactionType - Optional transaction type filter ("Revenue", "Expense", "Both")
    /// @returns CSV data as UTF-8 byte array with BOM
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync(DateTime? startDate = null, DateTime? endDate = null, string? transactionType = "Both")
    {
        try
        {
            _logger.LogInformation($"Starting CSV export with filters - StartDate: {startDate:yyyy-MM-dd}, EndDate: {endDate:yyyy-MM-dd}, Type: {transactionType}");

            // Validate and sanitize input parameters
            var sanitizedTransactionType = _inputSanitization.SanitizeTransactionInput(transactionType, "transactionType") ?? "Both";
            
            // Validate date range
            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            {
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));
            }

            // Get all transactions from database
            var allTransactions = await _dataImportService.GetTransactionsAsync();
            
            // Apply filters
            var filteredTransactions = ApplyFilters(allTransactions, startDate, endDate, sanitizedTransactionType);
            
            _logger.LogInformation($"Filtered {allTransactions.Count} transactions to {filteredTransactions.Count} for export");

            // Generate CSV content
            var csvContent = GenerateCsvContent(filteredTransactions);
            
            // Convert to UTF-8 bytes with BOM for German Excel compatibility
            var preamble = Encoding.UTF8.GetPreamble();
            var content = Encoding.UTF8.GetBytes(csvContent);
            var result = new byte[preamble.Length + content.Length];
            Array.Copy(preamble, 0, result, 0, preamble.Length);
            Array.Copy(content, 0, result, preamble.Length, content.Length);

            _logger.LogInformation($"CSV export completed successfully. Generated {result.Length} bytes");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV export");
            throw new InvalidOperationException($"CSV Export fehlgeschlagen: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Apply filtering criteria to transaction list
    /// 
    /// Date Filter Logic:
    /// - Uses inclusive date boundaries (both start and end dates are included)
    /// - Compares against Year and Month properties to create date range
    /// - Handles edge cases where only start or end date is provided
    /// 
    /// Transaction Type Filter Logic:
    /// - "Revenue": Only Revenue type transactions
    /// - "Expense": Only Expense type transactions  
    /// - "Both" or null: All transaction types
    /// - Case-insensitive matching
    /// 
    /// @param transactions - Source transaction list
    /// @param startDate - Optional inclusive start date
    /// @param endDate - Optional inclusive end date
    /// @param transactionType - Transaction type filter
    /// @returns Filtered transaction list
    /// </summary>
    private List<TransactionLine> ApplyFilters(
        List<TransactionLine> transactions, 
        DateTime? startDate, 
        DateTime? endDate, 
        string transactionType)
    {
        var filtered = transactions.AsQueryable();

        // Apply date range filter with inclusive boundaries
        if (startDate.HasValue)
        {
            // Convert start date to first day of month for inclusive filtering
            var startYear = startDate.Value.Year;
            var startMonth = startDate.Value.Month;
            
            filtered = filtered.Where(t => 
                t.Year > startYear || 
                (t.Year == startYear && t.Month >= startMonth));
        }

        if (endDate.HasValue)
        {
            // Convert end date to last day of month for inclusive filtering
            var endYear = endDate.Value.Year;
            var endMonth = endDate.Value.Month;
            
            filtered = filtered.Where(t => 
                t.Year < endYear || 
                (t.Year == endYear && t.Month <= endMonth));
        }

        // Apply transaction type filter
        if (!string.IsNullOrEmpty(transactionType) && 
            !transactionType.Equals("Both", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<TransactionType>(transactionType, true, out var parsedType))
            {
                filtered = filtered.Where(t => t.Type == parsedType);
            }
        }

        return filtered.OrderBy(t => t.Year).ThenBy(t => t.Month).ThenBy(t => t.Category).ToList();
    }

    /// <summary>
    /// Generate CSV content from filtered transaction data
    /// 
    /// CSV Format:
    /// - UTF-8 encoding with BOM for German Excel compatibility
    /// - Comma separator with quoted fields containing commas
    /// - German decimal format (1.234,56) for amounts
    /// - All text fields sanitized for CSV safety
    /// 
    /// Columns:
    /// - Jahr (Year)
    /// - Monat (Month) 
    /// - Kategorie (Category)
    /// - Typ (Type)
    /// - Betrag (Amount) - in German decimal format
    /// - Gruppenkategorie (Group Category)
    /// 
    /// @param transactions - Filtered transaction data
    /// @returns CSV content as string
    /// </summary>
    private string GenerateCsvContent(List<TransactionLine> transactions)
    {
        var csv = new StringBuilder();
        
        // Add German CSV header
        csv.AppendLine("Jahr,Monat,Kategorie,Typ,Betrag,Gruppenkategorie");

        // German culture for number formatting (1.234,56)
        var germanCulture = new CultureInfo("de-DE");

        foreach (var transaction in transactions)
        {
            var row = new[]
            {
                transaction.Year.ToString(),
                transaction.Month.ToString(),
                EscapeCsvField(transaction.Category),
                transaction.Type.ToString(),
                transaction.Amount.ToString("N2", germanCulture), // German decimal format: 1.234,56
                EscapeCsvField(transaction.GroupCategory ?? "")
            };

            csv.AppendLine(string.Join(",", row));
        }

        return csv.ToString();
    }

    /// <summary>
    /// Escape CSV field to handle commas, quotes, and line breaks
    /// Follows RFC 4180 CSV specification with additional sanitization
    /// 
    /// @param field - Field value to escape
    /// @returns Properly escaped CSV field
    /// </summary>
    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        // Sanitize the field for security
        field = _inputSanitization.CreateSafeDisplayText(field);

        // Quote field if it contains comma, quote, or newline
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            // Escape quotes by doubling them and wrap in quotes
            field = "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}