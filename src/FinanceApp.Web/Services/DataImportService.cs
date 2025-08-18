using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for importing parsed financial data into the database
/// Handles duplicate checking and transaction atomicity
/// Ensures data integrity by checking for existing periods before import
/// </summary>
public class DataImportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DataImportService> _logger;
    
    public DataImportService(AppDbContext context, ILogger<DataImportService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    /// <summary>
    /// Import parsed financial data into the database
    /// Checks for existing data to prevent duplicates
    /// Returns result indicating success or skip reason
    /// </summary>
    public async Task<ImportResult> ImportDataAsync(ParsedFinancialData parsedData)
    {
        var result = new ImportResult();
        
        try
        {
            // Get unique months from parsed data
            var monthsInData = parsedData.TransactionLines
                .Select(t => t.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
            
            if (!monthsInData.Any())
            {
                result.Success = false;
                result.Message = "No transaction data found in PDF";
                return result;
            }
            
            // Check which months already exist in database
            var existingPeriods = await _context.FinancialPeriods
                .Where(p => p.Year == parsedData.Year && monthsInData.Contains(p.Month))
                .Select(p => p.Month)
                .ToListAsync();
            
            // Find months that are new (not already imported)
            var newMonths = monthsInData.Except(existingPeriods).ToList();
            
            if (!newMonths.Any())
            {
                result.Success = true;
                result.Skipped = true;
                result.Message = $"All months in {parsedData.Year} have already been imported";
                result.SkippedMonths = monthsInData;
                _logger.LogInformation(result.Message);
                return result;
            }
            
            // Begin transaction for atomic import
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Create financial periods for new months
                foreach (var month in newMonths)
                {
                    var period = new FinancialPeriod
                    {
                        Year = parsedData.Year,
                        Month = month,
                        SourceFileName = parsedData.SourceFileName,
                        ImportedAt = DateTime.UtcNow
                    };
                    
                    _context.FinancialPeriods.Add(period);
                    await _context.SaveChangesAsync();
                    
                    // Add transaction lines for this period
                    var monthTransactions = parsedData.TransactionLines
                        .Where(t => t.Month == month)
                        .Select(t => new TransactionLine
                        {
                            FinancialPeriodId = period.Id,
                            Category = t.Category,
                            Month = t.Month,
                            Year = t.Year,
                            Amount = t.Amount,
                            Type = t.Type,
                            GroupCategory = DetermineGroupCategory(t.Category)
                        });
                    
                    _context.TransactionLines.AddRange(monthTransactions);
                    result.ImportedTransactionCount += monthTransactions.Count();
                }
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                result.Success = true;
                result.ImportedMonths = newMonths;
                result.SkippedMonths = existingPeriods;
                result.Message = $"Successfully imported {newMonths.Count} month(s) with {result.ImportedTransactionCount} transactions";
                
                if (existingPeriods.Any())
                {
                    result.Message += $". Skipped {existingPeriods.Count} month(s) already in database";
                }
                
                _logger.LogInformation(result.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"Failed to import data: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing financial data");
            result.Success = false;
            result.Message = $"Import failed: {ex.Message}";
        }
        
        return result;
    }
    
    /// <summary>
    /// Determine group category based on transaction category name
    /// Used for grouping in reports (e.g., "Kostenarten" for expense categories)
    /// </summary>
    private string? DetermineGroupCategory(string category)
    {
        var lowerCategory = category.ToLower();
        
        // Group main revenue categories
        if (lowerCategory.Contains("umsatz") || lowerCategory.Contains("erlös"))
        {
            return "Erlöse";
        }
        
        // Group cost categories
        if (lowerCategory.Contains("personal") || lowerCategory.Contains("raum") || 
            lowerCategory.Contains("fahrzeug") || lowerCategory.Contains("werbe") ||
            lowerCategory.Contains("abschreibung") || lowerCategory.Contains("versicherung"))
        {
            return "Kostenarten";
        }
        
        // Group result categories
        if (lowerCategory.Contains("ergebnis") || lowerCategory.Contains("rohertrag"))
        {
            return "Ergebnisse";
        }
        
        return null;
    }
    
    /// <summary>
    /// Get summary of imported data grouped by month
    /// </summary>
    public async Task<List<MonthSummary>> GetMonthlySummaryAsync(int? year = null)
    {
        var query = _context.TransactionLines.AsQueryable();
        
        if (year.HasValue)
        {
            query = query.Where(t => t.Year == year.Value);
        }
        
        var summaries = await query
            .GroupBy(t => new { t.Year, t.Month })
            .Select(g => new MonthSummary
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalRevenue = g.Where(t => t.Type == TransactionType.Revenue)
                    .Sum(t => t.Amount),
                TotalExpenses = g.Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => Math.Abs(t.Amount)),
                TransactionCount = g.Count()
            })
            .OrderBy(s => s.Year)
            .ThenBy(s => s.Month)
            .ToListAsync();
        
        return summaries;
    }
}

/// <summary>
/// Result of a data import operation
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<int> ImportedMonths { get; set; } = new();
    public List<int> SkippedMonths { get; set; } = new();
    public int ImportedTransactionCount { get; set; }
}

/// <summary>
/// Summary of financial data for a specific month
/// </summary>
public class MonthSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetResult => TotalRevenue - TotalExpenses;
    public int TransactionCount { get; set; }
    
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
}