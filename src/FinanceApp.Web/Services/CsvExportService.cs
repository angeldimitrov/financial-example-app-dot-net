using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/**
 * Service for exporting financial data to CSV format with German formatting standards
 * 
 * Business Context:
 * - Exports monthly financial summaries for analysis in Excel/spreadsheet applications
 * - Uses German number formatting (1.234,56) and semicolon delimiter for compatibility
 * - Includes UTF-8 BOM for proper German character display in Excel
 * 
 * CSV Format:
 * - Delimiter: Semicolon (;) - standard for German CSV files
 * - Number format: German (1.234,56 instead of 1,234.56)
 * - Date format: German month names (Januar, Februar, etc.)
 * - Encoding: UTF-8 with BOM for Excel compatibility
 */
public class CsvExportService
{
    private readonly ILogger<CsvExportService> _logger;
    
    // German month names for CSV export
    private static readonly string[] GermanMonthNames = 
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    public CsvExportService(ILogger<CsvExportService> logger)
    {
        _logger = logger;
    }

    /**
     * Generate CSV content from financial periods with German formatting
     * 
     * Business Rules:
     * - Each period becomes one row in the CSV
     * - Revenue is calculated from all Revenue type transactions
     * - Expenses are calculated from all Expense type transactions
     * - Profit is calculated as Revenue - Expenses
     * - Profit margin is calculated as (Profit / Revenue) * 100
     * - Summary type transactions are excluded (to prevent double counting)
     * 
     * @param periods List of financial periods with their transaction lines
     * @return CSV content as byte array with UTF-8 BOM encoding
     */
    public virtual async Task<byte[]> GenerateCsvAsync(List<FinancialPeriod> periods)
    {
        try
        {
            _logger.LogInformation("Starting CSV export for {Count} periods", periods.Count);
            
            // German culture for number formatting (1.234,56)
            var germanCulture = new CultureInfo("de-DE");
            
            // CSV configuration for German format
            var config = new CsvConfiguration(germanCulture)
            {
                Delimiter = ";", // German standard delimiter
                HasHeaderRecord = true,
                Encoding = Encoding.UTF8
            };

            using var memoryStream = new MemoryStream();
            
            // Write UTF-8 BOM for Excel compatibility with German characters
            var bom = Encoding.UTF8.GetPreamble();
            await memoryStream.WriteAsync(bom, 0, bom.Length);
            
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
            using (var csv = new CsvWriter(writer, config))
            {
                // Write headers in German
                csv.WriteField("Jahr");
                csv.WriteField("Monat");
                csv.WriteField("Umsatzerlöse");
                csv.WriteField("Gesamtkosten");
                csv.WriteField("Gewinn");
                csv.WriteField("Gewinnmarge");
                await csv.NextRecordAsync();

                // Sort periods by year and month for logical ordering
                var sortedPeriods = periods.OrderBy(p => p.Year).ThenBy(p => p.Month);

                foreach (var period in sortedPeriods)
                {
                    // Calculate financial metrics from transaction lines
                    // Only include actual transactions, not summary lines (to avoid double counting)
                    var revenue = period.TransactionLines
                        .Where(t => t.Type == TransactionType.Revenue)
                        .Sum(t => Math.Abs(t.Amount)); // Revenue is typically positive
                    
                    var expenses = period.TransactionLines
                        .Where(t => t.Type == TransactionType.Expense)
                        .Sum(t => Math.Abs(t.Amount)); // Expenses are typically negative, take absolute
                    
                    var profit = revenue - expenses;
                    
                    // Calculate profit margin as percentage
                    // Avoid division by zero if no revenue
                    var profitMargin = revenue > 0 ? (profit / revenue) * 100 : 0;

                    // Write row with German formatting
                    csv.WriteField(period.Year.ToString());
                    csv.WriteField(GermanMonthNames[period.Month - 1]); // Convert 1-12 to 0-11 array index
                    csv.WriteField(revenue.ToString("N2", germanCulture)); // Format as 1.234,56
                    csv.WriteField(expenses.ToString("N2", germanCulture));
                    csv.WriteField(profit.ToString("N2", germanCulture));
                    csv.WriteField(profitMargin.ToString("N2", germanCulture) + "%");
                    
                    await csv.NextRecordAsync();
                    
                    _logger.LogDebug("Exported period {Year}-{Month}: Revenue={Revenue}, Expenses={Expenses}", 
                        period.Year, period.Month, revenue, expenses);
                }

                await csv.FlushAsync();
            }

            var result = memoryStream.ToArray();
            _logger.LogInformation("CSV export completed successfully. Size: {Size} bytes", result.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSV export");
            throw new InvalidOperationException("Failed to generate CSV export", ex);
        }
    }

    /**
     * Generate a filename for the CSV export with date range
     * 
     * Format: BWA-Export_YYYY-MM_YYYY-MM.csv
     * Example: BWA-Export_2024-01_2024-12.csv
     * 
     * @param periods List of financial periods to determine date range
     * @return Formatted filename string
     */
    public virtual string GenerateFileName(List<FinancialPeriod> periods)
    {
        if (!periods.Any())
        {
            // Fallback filename if no periods
            return $"BWA-Export_{DateTime.Now:yyyy-MM-dd}.csv";
        }

        var sortedPeriods = periods.OrderBy(p => p.Year).ThenBy(p => p.Month).ToList();
        var firstPeriod = sortedPeriods.First();
        var lastPeriod = sortedPeriods.Last();

        // Format: BWA-Export_2024-01_2024-12.csv
        return $"BWA-Export_{firstPeriod.Year:0000}-{firstPeriod.Month:00}_{lastPeriod.Year:0000}-{lastPeriod.Month:00}.csv";
    }
}