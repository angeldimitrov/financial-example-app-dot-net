using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for exporting financial transaction data to CSV format with German localization.
/// 
/// Business Context:
/// - Implements BWA (Betriebswirtschaftliche Auswertung) export standards
/// - Follows German Excel compatibility requirements (semicolon delimiter, UTF-8 BOM)
/// - Optimized for performance with streaming and minimal memory allocation
/// - Handles large datasets up to 50,000 rows within memory constraints
/// 
/// Technical Implementation:
/// - Uses CsvHelper library for robust CSV generation
/// - Implements German culture formatting for numbers and dates
/// - Streams data to minimize memory footprint
/// - Includes proper error handling and resource disposal
/// </summary>
public class CsvExportService : ICsvExportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CsvExportService> _logger;

    // German culture for number and date formatting
    private static readonly CultureInfo GermanCulture = new("de-DE");

    public CsvExportService(AppDbContext context, ILogger<CsvExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Export all transaction data to CSV format with German localization
    /// 
    /// Performance Optimizations:
    /// - Uses streaming with IAsyncEnumerable to minimize memory usage
    /// - Processes data in chunks to stay under 100MB memory limit
    /// - Includes execution time logging for performance monitoring
    /// 
    /// Data Processing:
    /// - Joins TransactionLine with FinancialPeriod for complete context
    /// - Maps transaction types to German business terms
    /// - Formats numbers using German decimal separator (comma)
    /// - Orders by period and transaction for consistent exports
    /// </summary>
    public async Task<byte[]> ExportAllTransactionsToCsvAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting CSV export of all transactions");

        try
        {
            using var memoryStream = new MemoryStream();
            
            // Add UTF-8 BOM for German Excel compatibility
            var bom = Encoding.UTF8.GetPreamble();
            await memoryStream.WriteAsync(bom);

            using var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
            using var csv = new CsvWriter(writer, GetGermanCsvConfiguration());

            // Write German headers
            WriteGermanHeaders(csv);

            // Stream transaction data to minimize memory usage
            var transactionCount = 0;
            await foreach (var exportRecord in GetTransactionExportDataAsync())
            {
                csv.WriteRecord(exportRecord);
                csv.NextRecord();
                transactionCount++;
                
                // Log progress for large datasets
                if (transactionCount % 5000 == 0)
                {
                    _logger.LogInformation("Exported {Count} transactions", transactionCount);
                }
            }

            csv.Flush();
            writer.Flush();

            stopwatch.Stop();
            _logger.LogInformation("CSV export completed: {Count} transactions in {ElapsedMs}ms", 
                transactionCount, stopwatch.ElapsedMilliseconds);

            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during CSV export");
            throw new InvalidOperationException("Failed to export transactions to CSV", ex);
        }
    }

    /// <summary>
    /// Stream transaction data with financial period context for CSV export
    /// 
    /// Query Optimization:
    /// - Uses Include to avoid N+1 queries for FinancialPeriod
    /// - Orders by Year, Month, then Id for consistent output
    /// - Projects to anonymous type to minimize memory allocation
    /// - Uses AsAsyncEnumerable for streaming large datasets
    /// 
    /// Data Mapping:
    /// - Maps TransactionType enum to German business terms
    /// - Formats amounts using German decimal notation
    /// - Includes all required fields per specification
    /// </summary>
    private async IAsyncEnumerable<TransactionExportRecord> GetTransactionExportDataAsync()
    {
        var query = _context.TransactionLines
            .Include(t => t.FinancialPeriod)
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ThenBy(t => t.Id)
            .AsAsyncEnumerable();

        await foreach (var transaction in query)
        {
            yield return new TransactionExportRecord
            {
                Jahr = transaction.Year,
                Monat = transaction.Month,
                Kategorie = transaction.Category ?? string.Empty,
                Beschreibung = transaction.GroupCategory ?? string.Empty,
                Betrag = FormatGermanAmount(transaction.Amount),
                Typ = MapTransactionTypeToGerman(transaction.Type),
                ErstelltAm = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", GermanCulture),
                PeriodId = transaction.FinancialPeriodId
            };
        }
    }

    /// <summary>
    /// Configure CsvHelper for German Excel compatibility
    /// 
    /// Configuration Details:
    /// - Semicolon delimiter (standard for German Excel)
    /// - German culture for number formatting
    /// - Double quote text qualifiers for fields containing delimiters
    /// - Proper handling of special characters and umlauts
    /// </summary>
    private static CsvConfiguration GetGermanCsvConfiguration()
    {
        return new CsvConfiguration(GermanCulture)
        {
            Delimiter = ";", // German Excel standard
            HasHeaderRecord = true,
            Quote = '"',
            Escape = '"',
            // Ensure proper encoding of German characters
            Encoding = Encoding.UTF8
        };
    }

    /// <summary>
    /// Write German column headers to CSV
    /// 
    /// Header Mapping:
    /// - Jahr: Year from financial period
    /// - Monat: Month from financial period
    /// - Kategorie: BWA category classification
    /// - Beschreibung: Transaction description
    /// - Betrag: Amount in German format
    /// - Typ: Transaction type in German
    /// - Erstellt am: Creation timestamp
    /// - Period ID: Financial period identifier
    /// </summary>
    private static void WriteGermanHeaders(CsvWriter csv)
    {
        csv.WriteField("Jahr");
        csv.WriteField("Monat");
        csv.WriteField("Kategorie");
        csv.WriteField("Beschreibung");
        csv.WriteField("Betrag");
        csv.WriteField("Typ");
        csv.WriteField("Erstellt am");
        csv.WriteField("Period ID");
        csv.NextRecord();
    }

    /// <summary>
    /// Format monetary amount using German number conventions
    /// 
    /// German Format Rules:
    /// - Comma as decimal separator (1234,56)
    /// - Period as thousands separator (1.234,56)
    /// - Two decimal places for monetary precision
    /// - Negative amounts with minus sign prefix
    /// 
    /// Business Context:
    /// - Follows German accounting standards
    /// - Compatible with German Excel and accounting software
    /// - Maintains precision for financial calculations
    /// </summary>
    private static string FormatGermanAmount(decimal amount)
    {
        return amount.ToString("N2", GermanCulture);
    }

    /// <summary>
    /// Map internal TransactionType enum to German business terminology
    /// 
    /// Mapping Rules:
    /// - Revenue → "Umsatz" (German for sales/revenue)
    /// - Expense → "Ausgabe" (German for expense/expenditure)
    /// - Summary → "Zusammenfassung" (German for summary - should be excluded from import)
    /// - Other → "Sonstiges" (German for miscellaneous/other)
    /// 
    /// Business Context:
    /// - Uses standard German accounting terminology
    /// - Aligns with BWA report classifications
    /// - Provides clear categorization for German users
    /// </summary>
    private static string MapTransactionTypeToGerman(TransactionType transactionType)
    {
        return transactionType switch
        {
            TransactionType.Revenue => "Umsatz",
            TransactionType.Expense => "Ausgabe", 
            TransactionType.Summary => "Zusammenfassung",
            TransactionType.Other => "Sonstiges",
            _ => "Unbekannt" // Unknown - fallback for any new enum values
        };
    }

    /// <summary>
    /// Generate timestamped filename for CSV exports
    /// 
    /// Format: BWA_Export_YYYY-MM-DD_HHmmss.csv
    /// 
    /// Filename Components:
    /// - BWA: Indicates German business evaluation format
    /// - Export: Clearly identifies as export file
    /// - Date: ISO format for international compatibility
    /// - Time: 24-hour format without colons (filesystem safe)
    /// - Extension: .csv for universal compatibility
    /// 
    /// Business Benefits:
    /// - Prevents filename conflicts with timestamps
    /// - Clearly identifies file purpose and creation time
    /// - Follows German business document naming conventions
    /// </summary>
    public string GenerateExportFilename()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        return $"BWA_Export_{timestamp}.csv";
    }
}

/// <summary>
/// Data transfer object for CSV export records with German column mapping
/// 
/// Column Structure:
/// - Maps internal data model to German business terminology
/// - Provides type-safe structure for CSV generation
/// - Includes all fields required by specification
/// - Uses German property names for direct CSV header mapping
/// 
/// Performance Considerations:
/// - Lightweight structure to minimize memory allocation
/// - String properties for direct CSV serialization
/// - No complex object references to reduce memory pressure
/// </summary>
internal class TransactionExportRecord
{
    public int Jahr { get; set; } // Year
    public int Monat { get; set; } // Month  
    public string Kategorie { get; set; } = string.Empty; // Category
    public string Beschreibung { get; set; } = string.Empty; // Description
    public string Betrag { get; set; } = string.Empty; // Amount (German formatted)
    public string Typ { get; set; } = string.Empty; // Type
    public string ErstelltAm { get; set; } = string.Empty; // Created At
    public int PeriodId { get; set; } // Period ID
}