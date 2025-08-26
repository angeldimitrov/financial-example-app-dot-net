using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FinanceApp.Web.Services;
using FinanceApp.Web.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;

namespace FinanceApp.Web.Pages;

public class IndexModel : PageModel
{
    private readonly PdfParserService _pdfParser;
    private readonly DataImportService _dataImport;
    private readonly IFileValidationService _fileValidation;
    private readonly IInputSanitizationService _inputSanitization;
    private readonly CsvExportService _csvExportService;
    private readonly IAntiforgery _antiforgery;
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
        CsvExportService csvExportService,
        IAntiforgery antiforgery,
        ILogger<IndexModel> logger)
    {
        _pdfParser = pdfParser;
        _dataImport = dataImport;
        _fileValidation = fileValidation;
        _inputSanitization = inputSanitization;
        _csvExportService = csvExportService;
        _antiforgery = antiforgery;
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
    /// Handle CSV export with comprehensive options from the premium modal
    /// 
    /// Business Context:
    /// - Processes export options from AJAX form submission
    /// - Applies date range and transaction type filtering
    /// - Supports German Excel format for proper decimal handling
    /// - Returns CSV file as download with appropriate headers
    /// 
    /// Security Features:
    /// - Anti-forgery token validation
    /// - Input sanitization and validation
    /// - Controlled file download headers
    /// 
    /// @returns FileResult with CSV data or JsonResult with error
    /// </summary>
    public async Task<IActionResult> OnPostExportCsvWithOptionsAsync(
        [FromBody] ExportOptions exportOptions)
    {
        // Validate anti-forgery token
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            _logger.LogWarning("Anti-forgery token validation failed for CSV export");
            return new JsonResult(new { 
                success = false, 
                error = "Sicherheitsvalidierung fehlgeschlagen. Bitte laden Sie die Seite neu." 
            }) { StatusCode = 400 };
        }
        
        try
        {
            // Validate the export options according to business rules
            var validationErrors = exportOptions.Validate();
            if (validationErrors.Any())
            {
                _logger.LogWarning($"CSV export validation failed: {string.Join(", ", validationErrors)}");
                return new JsonResult(new { 
                    success = false, 
                    error = "Ungültige Export-Parameter: " + string.Join(", ", validationErrors) 
                }) { StatusCode = 400 };
            }

            _logger.LogInformation($"Processing CSV export request - StartDate: {exportOptions.StartDate:yyyy-MM-dd}, EndDate: {exportOptions.EndDate:yyyy-MM-dd}, TransactionType: {exportOptions.GetTransactionTypeFilter()}, Format: {exportOptions.ExportFormat}");

            // Generate CSV data using the service
            var csvData = await _csvExportService.ExportToCsvAsync(
                exportOptions.StartDate,
                exportOptions.EndDate,
                exportOptions.GetTransactionTypeFilter()
            );

            // Create filename with date range for clarity
            var dateRangeStr = "";
            if (exportOptions.StartDate.HasValue || exportOptions.EndDate.HasValue)
            {
                var startStr = exportOptions.StartDate?.ToString("yyyy-MM-dd") ?? "start";
                var endStr = exportOptions.EndDate?.ToString("yyyy-MM-dd") ?? "end";
                dateRangeStr = $"_{startStr}_to_{endStr}";
            }
            
            var filename = $"BWA_Export{dateRangeStr}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            
            // Determine content type based on export format
            var contentType = exportOptions.UseGermanFormatting() 
                ? "text/csv; charset=utf-8" 
                : "application/csv";

            _logger.LogInformation($"CSV export completed successfully. Generated {csvData.Length} bytes for file: {filename}");

            // Return file for download with proper headers
            return File(csvData, contentType, filename);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments provided for CSV export");
            return new JsonResult(new { 
                success = false, 
                error = "Ungültige Parameter: " + ex.Message 
            }) { StatusCode = 400 };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "CSV export operation failed");
            return new JsonResult(new { 
                success = false, 
                error = "Export fehlgeschlagen: " + ex.Message 
            }) { StatusCode = 500 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CSV export");
            return new JsonResult(new { 
                success = false, 
                error = "Ein unerwarteter Fehler ist aufgetreten. Bitte versuchen Sie es erneut." 
            }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Get estimated record count for export preview
    /// 
    /// Business Context:
    /// - Provides real-time preview of how many records will be exported
    /// - Helps users understand the scope of their export before processing
    /// - Applies same filtering logic as actual export
    /// 
    /// @returns JsonResult with estimated record count
    /// </summary>
    public async Task<IActionResult> OnPostGetEstimatedRecordCountAsync(
        [FromBody] ExportOptions exportOptions)
    {
        // Validate anti-forgery token
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            _logger.LogWarning("Anti-forgery token validation failed for record count");
            return new JsonResult(new { 
                success = false, 
                count = 0 
            });
        }
        
        try
        {
            // Get all transactions to calculate estimate
            var allTransactions = await _dataImport.GetTransactionsAsync();
            
            // Apply same filtering logic as export
            var filtered = allTransactions.AsQueryable();

            // Apply date range filter
            if (exportOptions.StartDate.HasValue)
            {
                var startYear = exportOptions.StartDate.Value.Year;
                var startMonth = exportOptions.StartDate.Value.Month;
                
                filtered = filtered.Where(t => 
                    t.Year > startYear || 
                    (t.Year == startYear && t.Month >= startMonth));
            }

            if (exportOptions.EndDate.HasValue)
            {
                var endYear = exportOptions.EndDate.Value.Year;
                var endMonth = exportOptions.EndDate.Value.Month;
                
                filtered = filtered.Where(t => 
                    t.Year < endYear || 
                    (t.Year == endYear && t.Month <= endMonth));
            }

            // Apply transaction type filter
            var transactionType = exportOptions.GetTransactionTypeFilter();
            if (!string.IsNullOrEmpty(transactionType) && 
                !transactionType.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<TransactionType>(transactionType, true, out var parsedType))
                {
                    filtered = filtered.Where(t => t.Type == parsedType);
                }
            }

            var estimatedCount = filtered.Count();

            _logger.LogInformation($"Estimated record count: {estimatedCount} for export options");

            return new JsonResult(new { 
                success = true, 
                count = estimatedCount 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating estimated record count");
            return new JsonResult(new { 
                success = false, 
                count = 0 
            });
        }
    }
}