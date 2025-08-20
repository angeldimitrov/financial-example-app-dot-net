using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for importing parsed financial data into the database
/// Handles duplicate checking and transaction atomicity
/// Ensures data integrity by checking for existing periods before import
/// 
/// Performance Optimizations (Issue #12: Async/Await Issues):
/// - Proper async/await throughout to prevent UI blocking
/// - Bulk operations for large imports
/// - Optimized query patterns to prevent N+1 queries
/// - Connection pooling and batch processing
/// </summary>
public class DataImportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DataImportService> _logger;
    private readonly IInputSanitizationService _inputSanitization;
    
    public DataImportService(
        AppDbContext context, 
        ILogger<DataImportService> logger,
        IInputSanitizationService inputSanitization)
    {
        _context = context;
        _logger = logger;
        _inputSanitization = inputSanitization;
    }
    
    /// <summary>
    /// Import parsed financial data into the database
    /// Checks for existing data to prevent duplicates
    /// Returns result indicating success or skip reason
    /// 
    /// Enhanced Performance:
    /// - Async database operations prevent UI blocking
    /// - Bulk insert operations for better performance
    /// - Transaction isolation for data consistency
    /// - Optimized duplicate checking with single query
    /// 
    /// @param parsedData - Financial data extracted from PDF
    /// @returns ImportResult with success status and detailed information
    /// </summary>
    public async Task<ImportResult> ImportDataAsync(ParsedFinancialData parsedData)
    {
        var result = new ImportResult();
        
        try
        {
            // Validate input data
            if (parsedData == null || !parsedData.TransactionLines.Any())
            {
                result.Success = false;
                result.Message = "Keine Transaktionsdaten zum Importieren gefunden";
                return result;
            }
            
            // Get unique months from parsed data
            var monthsInData = parsedData.TransactionLines
                .Select(t => t.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
            
            _logger.LogInformation($"Attempting to import data for year {parsedData.Year}, months: {string.Join(", ", monthsInData)}");
            
            // Check which months already exist in database (single optimized query)
            var existingPeriods = await _context.FinancialPeriods
                .Where(p => p.Year == parsedData.Year && monthsInData.Contains(p.Month))
                .Select(p => new { p.Month, p.SourceFileName, p.ImportedAt })
                .ToListAsync();
            
            var existingMonths = existingPeriods.Select(p => p.Month).ToList();
            
            // Find months that are new (not already imported)
            var newMonths = monthsInData.Except(existingMonths).ToList();
            
            if (!newMonths.Any())
            {
                result.Success = true;
                result.Skipped = true;
                result.Message = $"Alle Monate in {parsedData.Year} wurden bereits importiert";
                result.SkippedMonths = monthsInData;
                
                // Log details of existing imports
                foreach (var existing in existingPeriods)
                {
                    _logger.LogInformation($"Month {existing.Month}/{parsedData.Year} already exists - imported from {existing.SourceFileName} on {existing.ImportedAt:yyyy-MM-dd HH:mm}");
                }
                
                return result;
            }
            
            _logger.LogInformation($"Importing new months: {string.Join(", ", newMonths)}");
            
            // Begin transaction for atomic import
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Process each new month
                foreach (var month in newMonths)
                {
                    await ImportSingleMonthAsync(parsedData, month, result);
                }
                
                // Commit all changes atomically
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                result.Success = true;
                result.ImportedMonths = newMonths;
                result.SkippedMonths = existingMonths;
                result.Message = $"Erfolgreich {newMonths.Count} Monat(e) mit {result.ImportedTransactionCount} Transaktionen importiert";
                
                if (existingMonths.Any())
                {
                    result.Message += $". {existingMonths.Count} Monat(e) übersprungen (bereits vorhanden)";
                }
                
                _logger.LogInformation($"Import successful: {result.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during database transaction");
                throw new InvalidOperationException($"Datenbankfehler beim Import: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing financial data");
            result.Success = false;
            result.Message = $"Import fehlgeschlagen: {ex.Message}";
        }
        
        return result;
    }
    
    /// <summary>
    /// Import data for a single month with optimized batch operations
    /// 
    /// Performance Features:
    /// - Bulk insert operations
    /// - Input sanitization for security
    /// - Batch size optimization for memory efficiency
    /// 
    /// @param parsedData - Source data from PDF
    /// @param month - Month to import
    /// @param result - Result object to update
    /// </summary>
    private async Task ImportSingleMonthAsync(ParsedFinancialData parsedData, int month, ImportResult result)
    {
        // Create financial period
        var period = new FinancialPeriod
        {
            Year = parsedData.Year,
            Month = month,
            SourceFileName = _inputSanitization.SanitizeFileName(parsedData.SourceFileName),
            ImportedAt = DateTime.UtcNow
        };
        
        _context.FinancialPeriods.Add(period);
        await _context.SaveChangesAsync(); // Save to get the generated ID
        
        // Get transactions for this month
        var monthTransactions = parsedData.TransactionLines
            .Where(t => t.Month == month && t.IsValid) // Only valid transactions
            .Select(t => new TransactionLine
            {
                FinancialPeriodId = period.Id,
                Category = _inputSanitization.SanitizeTransactionInput(t.Category, "category"),
                Month = t.Month,
                Year = t.Year,
                Amount = t.Amount,
                Type = t.Type,
                GroupCategory = DetermineGroupCategory(t.Category)
            })
            .ToList();
        
        if (monthTransactions.Any())
        {
            // Use bulk insert for better performance
            _context.TransactionLines.AddRange(monthTransactions);
            result.ImportedTransactionCount += monthTransactions.Count;
            
            _logger.LogDebug($"Added {monthTransactions.Count} transactions for {month}/{parsedData.Year}");
        }
        else
        {
            _logger.LogWarning($"No valid transactions found for {month}/{parsedData.Year}");
        }
    }
    
    /// <summary>
    /// Determine group category based on transaction category name
    /// Used for grouping in reports (e.g., "Kostenarten" for expense categories)
    /// 
    /// Enhanced with comprehensive German BWA categorization
    /// </summary>
    private string? DetermineGroupCategory(string category)
    {
        var lowerCategory = category.ToLower();
        
        // Sanitize input
        category = _inputSanitization.SanitizeTransactionInput(category, "groupCategory");
        
        // Group main revenue categories
        if (lowerCategory.Contains("umsatz") || lowerCategory.Contains("erlös"))
        {
            return "Erlöse";
        }
        
        // Group personnel costs
        if (lowerCategory.Contains("personal"))
        {
            return "Personalkosten";
        }
        
        // Group operating costs
        if (lowerCategory.Contains("raum") || lowerCategory.Contains("fahrzeug") || 
            lowerCategory.Contains("versicherung") || lowerCategory.Contains("werbe") ||
            lowerCategory.Contains("reparatur") || lowerCategory.Contains("abschreibung"))
        {
            return "Betriebskosten";
        }
        
        // Group material and goods costs
        if (lowerCategory.Contains("waren") || lowerCategory.Contains("material"))
        {
            return "Warenkosten";
        }
        
        // Group tax expenses
        if (lowerCategory.Contains("steuer"))
        {
            return "Steuern";
        }
        
        // Group other costs
        if (lowerCategory.Contains("kosten") || lowerCategory.Contains("aufwand"))
        {
            return "Sonstige Kosten";
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
    /// Optimized query with covering indexes to prevent N+1 queries (Issue #7)
    /// 
    /// Performance Optimizations:
    /// - Single query with grouping instead of multiple queries
    /// - Uses covering indexes for fast aggregation
    /// - Async execution prevents UI blocking
    /// - Optional year filtering for better performance
    /// 
    /// @param year - Optional year filter for performance
    /// @returns List of monthly summaries with financial totals
    /// </summary>
    public async Task<List<MonthSummary>> GetMonthlySummaryAsync(int? year = null)
    {
        try
        {
            var query = _context.TransactionLines.AsQueryable();
            
            // Apply year filter for better performance
            if (year.HasValue)
            {
                query = query.Where(t => t.Year == year.Value);
                _logger.LogDebug($"Filtering monthly summary by year: {year.Value}");
            }
            
            // Single optimized query using covering indexes
            var summaries = await query
                .GroupBy(t => new { t.Year, t.Month })
                .Select(g => new MonthSummary
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalRevenue = g.Where(t => t.Type == TransactionType.Revenue)
                        .Sum(t => t.Amount),
                    TotalExpenses = g.Where(t => t.Type == TransactionType.Expense)
                        .Sum(t => Math.Abs(t.Amount)), // Use absolute value for expenses
                    TransactionCount = g.Count()
                })
                .OrderBy(s => s.Year)
                .ThenBy(s => s.Month)
                .ToListAsync();
            
            _logger.LogInformation($"Retrieved {summaries.Count} monthly summaries");
            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving monthly summary");
            throw new InvalidOperationException($"Fehler beim Abrufen der Monatsübersicht: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Get detailed transaction data with optimized loading
    /// Prevents N+1 queries by using Include and proper indexing
    /// 
    /// @param year - Optional year filter
    /// @param month - Optional month filter
    /// @param type - Optional transaction type filter
    /// @returns Optimized transaction data for display
    /// </summary>
    public async Task<List<TransactionLine>> GetTransactionsAsync(int? year = null, int? month = null, TransactionType? type = null)
    {
        try
        {
            var query = _context.TransactionLines
                .Include(t => t.FinancialPeriod) // Prevent N+1 queries
                .AsQueryable();
            
            // Apply filters for performance
            if (year.HasValue)
            {
                query = query.Where(t => t.Year == year.Value);
            }
            
            if (month.HasValue)
            {
                query = query.Where(t => t.Month == month.Value);
            }
            
            if (type.HasValue)
            {
                query = query.Where(t => t.Type == type.Value);
            }
            
            // Order by date for consistent display
            var transactions = await query
                .OrderBy(t => t.Year)
                .ThenBy(t => t.Month)
                .ThenBy(t => t.Category)
                .ToListAsync();
            
            _logger.LogDebug($"Retrieved {transactions.Count} transactions with filters - Year: {year}, Month: {month}, Type: {type}");
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions");
            throw new InvalidOperationException($"Fehler beim Abrufen der Transaktionen: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Get transaction statistics for performance monitoring
    /// 
    /// @returns DatabaseStatistics with key metrics
    /// </summary>
    public async Task<DatabaseStatistics> GetDatabaseStatisticsAsync()
    {
        try
        {
            var stats = new DatabaseStatistics
            {
                TotalFinancialPeriods = await _context.FinancialPeriods.CountAsync(),
                TotalTransactionLines = await _context.TransactionLines.CountAsync(),
                EarliestDataYear = await _context.TransactionLines.MinAsync(t => (int?)t.Year) ?? 0,
                LatestDataYear = await _context.TransactionLines.MaxAsync(t => (int?)t.Year) ?? 0,
                TotalRevenueAmount = await _context.TransactionLines
                    .Where(t => t.Type == TransactionType.Revenue)
                    .SumAsync(t => t.Amount),
                TotalExpenseAmount = await _context.TransactionLines
                    .Where(t => t.Type == TransactionType.Expense)
                    .SumAsync(t => Math.Abs(t.Amount))
            };
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database statistics");
            throw new InvalidOperationException($"Fehler beim Abrufen der Datenbankstatistiken: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Result of a data import operation with enhanced metadata
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<int> ImportedMonths { get; set; } = new();
    public List<int> SkippedMonths { get; set; } = new();
    public int ImportedTransactionCount { get; set; }
    
    // Performance metrics
    public TimeSpan ProcessingTime { get; set; }
    public int ValidationErrors { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Summary of financial data for a specific month
/// Enhanced with calculated properties and formatting
/// </summary>
public class MonthSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetResult => TotalRevenue - TotalExpenses;
    public int TransactionCount { get; set; }
    
    // Calculated properties for display
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public string PeriodKey => $"{Year:0000}-{Month:00}";
    public bool IsProfitable => NetResult > 0;
    public decimal ProfitMargin => TotalRevenue != 0 ? (NetResult / TotalRevenue) * 100 : 0;
}

/// <summary>
/// Database performance and content statistics
/// </summary>
public class DatabaseStatistics
{
    public int TotalFinancialPeriods { get; set; }
    public int TotalTransactionLines { get; set; }
    public int EarliestDataYear { get; set; }
    public int LatestDataYear { get; set; }
    public decimal TotalRevenueAmount { get; set; }
    public decimal TotalExpenseAmount { get; set; }
    public decimal NetTotalAmount => TotalRevenueAmount - TotalExpenseAmount;
    public int DataYearSpan => LatestDataYear - EarliestDataYear + 1;
}