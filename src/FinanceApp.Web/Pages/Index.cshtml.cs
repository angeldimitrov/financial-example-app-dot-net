using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FinanceApp.Web.Services;

namespace FinanceApp.Web.Pages;

public class IndexModel : PageModel
{
    private readonly PdfParserService _pdfParser;
    private readonly DataImportService _dataImport;
    private readonly IFileValidationService _fileValidation;
    private readonly IInputSanitizationService _inputSanitization;
    private readonly CsvExportService _csvExport;
    private readonly ILogger<IndexModel> _logger;
    
    public List<MonthSummary>? MonthlySummaries { get; set; }
    
    // Chart data properties
    public string ChartLabelsJson { get; set; } = "[]";
    public string RevenueDataJson { get; set; } = "[]";
    public string ExpenseDataJson { get; set; } = "[]";
    
    public IndexModel(
        PdfParserService pdfParser,
        DataImportService dataImport,
        IFileValidationService fileValidation,
        IInputSanitizationService inputSanitization,
        CsvExportService csvExport,
        ILogger<IndexModel> logger)
    {
        _pdfParser = pdfParser;
        _dataImport = dataImport;
        _fileValidation = fileValidation;
        _inputSanitization = inputSanitization;
        _csvExport = csvExport;
        _logger = logger;
    }
    
    public async Task OnGetAsync()
    {
        // Load monthly summaries for display
        MonthlySummaries = await _dataImport.GetMonthlySummaryAsync();
        
        // Prepare chart data if we have summaries
        if (MonthlySummaries != null && MonthlySummaries.Any())
        {
            var labels = MonthlySummaries.Select(ms => new DateTime(ms.Year, ms.Month, 1).ToString("MMM yyyy")).ToList();
            var revenueData = MonthlySummaries.Select(ms => ms.TotalRevenue).ToList();
            var expenseData = MonthlySummaries.Select(ms => ms.TotalExpenses).ToList();
            
            ChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(labels);
            RevenueDataJson = System.Text.Json.JsonSerializer.Serialize(revenueData);
            ExpenseDataJson = System.Text.Json.JsonSerializer.Serialize(expenseData);
        }
    }
    
    public async Task<IActionResult> OnPostAsync(IFormFile pdfFile)
    {
        // Validate file using security service
        var validationResult = await _fileValidation.ValidateUploadedFileAsync(pdfFile);
        if (!validationResult.IsValid)
        {
            TempData["Error"] = validationResult.Message;
            _logger.LogWarning($"File validation failed: {validationResult.Message}");
            return Page();
        }
        
        try
        {
            // Sanitize the file name before processing
            var safeFileName = _inputSanitization.SanitizeFileName(pdfFile.FileName);
            
            // Parse the PDF
            using var stream = pdfFile.OpenReadStream();
            var parsedData = await _pdfParser.ParsePdfAsync(stream, safeFileName);
            
            // Import to database
            var importResult = await _dataImport.ImportDataAsync(parsedData);
            
            if (importResult.Success)
            {
                if (importResult.Skipped)
                {
                    TempData["Warning"] = importResult.Message;
                }
                else
                {
                    TempData["Success"] = importResult.Message;
                }
            }
            else
            {
                TempData["Error"] = importResult.Message;
            }
            
            _logger.LogInformation($"PDF upload processed: {safeFileName} - {importResult.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing PDF upload: {pdfFile?.FileName}");
            TempData["Error"] = "Ein Fehler ist beim Verarbeiten der PDF-Datei aufgetreten.";
        }
        
        // Reload the page with updated data
        return RedirectToPage();
    }
    
    /// <summary>
    /// Exports all financial transactions to CSV format
    /// 
    /// Business Context:
    /// - Exports BWA transaction data for external financial analysis
    /// - Converts German number format to international CSV standards
    /// - Provides downloadable file with standardized naming convention
    /// </summary>
    public async Task<IActionResult> OnGetExportCsv()
    {
        _logger.LogInformation("CSV export handler called!");
        
        try
        {
            // Fetch all transactions from database
            var transactions = await _dataImport.GetTransactionsAsync();
            
            _logger.LogInformation($"Retrieved {transactions?.Count ?? 0} transactions for CSV export");
            
            if (transactions == null || !transactions.Any())
            {
                _logger.LogWarning("No transactions found for CSV export");
                TempData["Warning"] = "Keine Transaktionen zum Exportieren vorhanden.";
                return RedirectToPage();
            }
            
            // Generate CSV using the export service
            var csvBytes = _csvExport.ExportTransactionsToCsv(transactions);
            
            // Generate filename with current date
            var fileName = $"Financial_Export_{DateTime.Now:yyyy-MM-dd}.csv";
            
            _logger.LogInformation($"CSV export generated: {fileName} with {transactions.Count} transactions");
            
            // Return the CSV file for download
            return File(csvBytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSV export");
            TempData["Error"] = "Fehler beim Exportieren der Daten.";
            return RedirectToPage();
        }
    }
}