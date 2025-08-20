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
    private readonly ICsvExportService _csvExportService;
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
        ICsvExportService csvExportService,
        ILogger<IndexModel> logger)
    {
        _pdfParser = pdfParser;
        _dataImport = dataImport;
        _fileValidation = fileValidation;
        _inputSanitization = inputSanitization;
        _csvExportService = csvExportService;
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
    /// Handler method for CSV export requests
    /// Returns all transaction data in German-formatted CSV with BWA compliance
    /// </summary>
    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        try
        {
            _logger.LogInformation("Starting CSV export request");
            
            // Generate CSV data using the German formatting service
            var csvData = await _csvExportService.ExportAllTransactionsToCsvAsync();
            
            // Generate filename with timestamp
            var fileName = _csvExportService.GenerateExportFilename();
            
            _logger.LogInformation("CSV export completed successfully: {FileName}, Size: {Size} bytes", 
                fileName, csvData.Length);
            
            // Return file with proper German CSV content type and UTF-8 encoding
            return File(csvData, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during CSV export");
            TempData["Error"] = "Fehler beim Exportieren der CSV-Datei. Bitte versuchen Sie es erneut.";
            return RedirectToPage();
        }
    }
}