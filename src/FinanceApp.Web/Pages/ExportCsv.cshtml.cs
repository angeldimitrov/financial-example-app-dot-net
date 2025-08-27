using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FinanceApp.Web.Services;

namespace FinanceApp.Web.Pages;

public class ExportCsvModel : PageModel
{
    private readonly DataImportService _dataImport;
    private readonly CsvExportService _csvExport;
    private readonly ILogger<ExportCsvModel> _logger;

    public ExportCsvModel(
        DataImportService dataImport,
        CsvExportService csvExport,
        ILogger<ExportCsvModel> logger)
    {
        _dataImport = dataImport;
        _csvExport = csvExport;
        _logger = logger;
    }

    /// <summary>
    /// Immediately exports all financial transactions to CSV format when page is accessed
    /// 
    /// Business Context:
    /// - Exports BWA transaction data for external financial analysis
    /// - Converts German number format to international CSV standards
    /// - Provides downloadable file with standardized naming convention
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        _logger.LogInformation("CSV export page accessed - generating CSV file");
        
        try
        {
            // Fetch all transactions from database
            var transactions = await _dataImport.GetTransactionsAsync();
            
            _logger.LogInformation($"Retrieved {transactions?.Count ?? 0} transactions for CSV export");
            
            if (transactions == null || !transactions.Any())
            {
                _logger.LogWarning("No transactions found for CSV export");
                TempData["Warning"] = "Keine Transaktionen zum Exportieren vorhanden.";
                return RedirectToPage("/Index");
            }
            
            // Generate CSV using the export service
            var csvBytes = _csvExport.ExportTransactionsToCsv(transactions);
            
            // Generate filename with current date
            var fileName = $"Financial_Export_{DateTime.Now:yyyy-MM-dd}.csv";
            
            _logger.LogInformation($"CSV export generated: {fileName} with {transactions.Count} transactions, {csvBytes.Length} bytes");
            
            // Return the CSV file for download with proper headers
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSV export");
            TempData["Error"] = "Fehler beim Exportieren der Daten.";
            return RedirectToPage("/Index");
        }
    }
}