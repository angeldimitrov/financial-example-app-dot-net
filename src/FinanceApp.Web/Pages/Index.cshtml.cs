using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Services;

namespace FinanceApp.Web.Pages;

public class IndexModel : PageModel
{
    private readonly PdfParserService _pdfParser;
    private readonly DataImportService _dataImport;
    private readonly IFileValidationService _fileValidation;
    private readonly IInputSanitizationService _inputSanitization;
    private readonly CsvExportService _csvExport;
    private readonly AppDbContext _context;
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
        AppDbContext context,
        ILogger<IndexModel> logger)
    {
        _pdfParser = pdfParser;
        _dataImport = dataImport;
        _fileValidation = fileValidation;
        _inputSanitization = inputSanitization;
        _csvExport = csvExport;
        _context = context;
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
    
    /**
     * Handler for CSV export functionality
     * 
     * Business Logic:
     * - Retrieves all financial periods with their transaction lines
     * - Generates CSV with German formatting (1.234,56)
     * - Returns file download with appropriate headers
     * - Handles empty data gracefully with user feedback
     * 
     * Security considerations:
     * - No user input processed (GET request only)
     * - Data filtered through EF Core queries
     * - File content generated in memory (no file system access)
     */
    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        try
        {
            _logger.LogInformation("CSV export requested");
            
            // Retrieve all financial periods with their transaction lines
            // Include transaction lines for revenue/expense calculations
            var periods = await _context.FinancialPeriods
                .Include(p => p.TransactionLines)
                .OrderBy(p => p.Year)
                .ThenBy(p => p.Month)
                .ToListAsync();
            
            // Check if we have data to export
            if (!periods.Any())
            {
                TempData["Warning"] = "Keine Daten zum Exportieren vorhanden. Bitte laden Sie zuerst eine PDF-Datei hoch.";
                _logger.LogWarning("CSV export attempted with no data available");
                return RedirectToPage();
            }
            
            // Generate CSV content
            var csvContent = await _csvExport.GenerateCsvAsync(periods);
            var fileName = _csvExport.GenerateFileName(periods);
            
            _logger.LogInformation("CSV export successful: {FileName}, Size: {Size} bytes", 
                fileName, csvContent.Length);
            
            // Return file download with proper MIME type for CSV
            return File(
                csvContent,
                "text/csv; charset=utf-8",
                fileName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV export");
            TempData["Error"] = "Fehler beim Exportieren der Daten. Bitte versuchen Sie es erneut.";
            return RedirectToPage();
        }
    }
}