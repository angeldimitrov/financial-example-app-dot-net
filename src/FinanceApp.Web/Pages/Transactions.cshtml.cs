using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;

namespace FinanceApp.Web.Pages;

/// <summary>
/// Page model for the Transactions view
/// Displays detailed transaction data with filtering and grouping capabilities
/// 
/// Performance Optimizations (Issue #7: N+1 Query Problem):
/// - Single optimized query with proper joins
/// - Efficient data loading with Include statements
/// - Bulk processing for large datasets
/// - Input sanitization for security
/// </summary>
public class TransactionsModel : PageModel
{
    private readonly DataImportService _dataImportService;
    private readonly IInputSanitizationService _inputSanitization;
    private readonly ILogger<TransactionsModel> _logger;
    
    public List<TransactionGroup> GroupedTransactions { get; set; } = new();
    public List<int> AvailableYears { get; set; } = new();
    
    // Filter properties
    public int? SelectedYear { get; set; }
    public int? SelectedMonth { get; set; }
    public string? SelectedType { get; set; }
    
    // Summary properties
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetResult => TotalRevenue - TotalExpenses;
    
    // Performance metrics
    public int TotalTransactions { get; set; }
    public TimeSpan QueryTime { get; set; }
    
    public TransactionsModel(
        DataImportService dataImportService, 
        IInputSanitizationService inputSanitization,
        ILogger<TransactionsModel> logger)
    {
        _dataImportService = dataImportService;
        _inputSanitization = inputSanitization;
        _logger = logger;
    }
    
    /// <summary>
    /// Load transaction data with optimized queries
    /// Prevents N+1 queries by using single optimized query with proper joins
    /// 
    /// @param year - Optional year filter
    /// @param month - Optional month filter  
    /// @param type - Optional transaction type filter
    /// </summary>
    public async Task OnGetAsync(int? year, int? month, string? type)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Sanitize input parameters
            SelectedYear = year;
            SelectedMonth = month;
            SelectedType = _inputSanitization.SanitizeTransactionInput(type, "transactionType");
            
            _logger.LogInformation($"Loading transactions with filters - Year: {year}, Month: {month}, Type: {type}");
            
            // Load available years for filter dropdown (optimized query)
            await LoadAvailableYearsAsync();
            
            // Parse transaction type filter
            TransactionType? transactionType = null;
            if (!string.IsNullOrEmpty(SelectedType) && 
                Enum.TryParse<TransactionType>(SelectedType, true, out var parsedType))
            {
                transactionType = parsedType;
            }
            
            // Load transactions using optimized service method
            var transactions = await _dataImportService.GetTransactionsAsync(year, month, transactionType);
            TotalTransactions = transactions.Count;
            
            // Group transactions efficiently in memory (already loaded from DB)
            GroupedTransactions = GroupTransactionsForDisplay(transactions);
            
            // Calculate totals
            CalculateTotals(transactions);
            
            QueryTime = DateTime.UtcNow - startTime;
            
            _logger.LogInformation($"Loaded {TotalTransactions} transactions in {QueryTime.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading transactions page");
            // Set empty results to prevent page errors
            GroupedTransactions = new List<TransactionGroup>();
            TotalTransactions = 0;
        }
    }
    
    /// <summary>
    /// Load available years for the filter dropdown
    /// Uses optimized query with distinct values only
    /// </summary>
    private async Task LoadAvailableYearsAsync()
    {
        try
        {
            // Get statistics which includes year range information
            var stats = await _dataImportService.GetDatabaseStatisticsAsync();
            
            // Generate year range from earliest to latest
            if (stats.EarliestDataYear > 0 && stats.LatestDataYear > 0)
            {
                AvailableYears = Enumerable.Range(stats.EarliestDataYear, stats.DataYearSpan)
                    .OrderByDescending(y => y)
                    .ToList();
            }
            else
            {
                AvailableYears = new List<int>();
            }
            
            _logger.LogDebug($"Available years: {string.Join(", ", AvailableYears)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available years");
            AvailableYears = new List<int> { DateTime.Now.Year };
        }
    }
    
    /// <summary>
    /// Group transactions for display with optimized in-memory processing
    /// Processes already-loaded data efficiently without additional database queries
    /// 
    /// @param transactions - Pre-loaded transaction data from database
    /// @returns Grouped transaction data optimized for display
    /// </summary>
    private List<TransactionGroup> GroupTransactionsForDisplay(List<TransactionLine> transactions)
    {
        try
        {
            // Group transactions by month/year using LINQ in-memory processing
            var groups = transactions
                .GroupBy(t => new { t.Year, t.Month })
                .Select(g => new TransactionGroup
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthYear = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    
                    // Separate transactions by type with sanitized display
                    RevenueTransactions = g.Where(t => t.Type == TransactionType.Revenue)
                        .Select(SanitizeTransactionForDisplay)
                        .OrderBy(t => t.Category)
                        .ToList(),
                        
                    ExpenseTransactions = g.Where(t => t.Type == TransactionType.Expense)
                        .Select(SanitizeTransactionForDisplay)
                        .OrderBy(t => t.Category)
                        .ToList(),
                        
                    OtherTransactions = g.Where(t => t.Type != TransactionType.Revenue && t.Type != TransactionType.Expense)
                        .Select(SanitizeTransactionForDisplay)
                        .OrderBy(t => t.Category)
                        .ToList(),
                    
                    // Calculate monthly totals
                    MonthlyRevenue = g.Where(t => t.Type == TransactionType.Revenue).Sum(t => t.Amount),
                    MonthlyExpenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => Math.Abs(t.Amount)),
                    
                    // Create expense breakdown for charts (sanitized categories)
                    ExpenseBreakdown = g.Where(t => t.Type == TransactionType.Expense)
                        .GroupBy(t => t.Category)
                        .Select(categoryGroup => new ExpenseCategory
                        {
                            Category = _inputSanitization.CreateSafeDisplayText(categoryGroup.Key),
                            Amount = categoryGroup.Sum(t => Math.Abs(t.Amount)),
                            Color = GetCategoryColor(categoryGroup.Key)
                        })
                        .OrderByDescending(ec => ec.Amount)
                        .ToList()
                })
                .OrderByDescending(g => g.Year)
                .ThenByDescending(g => g.Month)
                .ToList();
                
            _logger.LogDebug($"Created {groups.Count} transaction groups for display");
            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grouping transactions for display");
            return new List<TransactionGroup>();
        }
    }
    
    /// <summary>
    /// Sanitize a single transaction for safe display
    /// Prevents XSS by sanitizing all user-visible content
    /// 
    /// @param transaction - Original transaction from database
    /// @returns Sanitized copy safe for HTML display
    /// </summary>
    private TransactionLine SanitizeTransactionForDisplay(TransactionLine transaction)
    {
        return new TransactionLine
        {
            Id = transaction.Id,
            Category = _inputSanitization.CreateSafeDisplayText(transaction.Category),
            Month = transaction.Month,
            Year = transaction.Year,
            Amount = transaction.Amount,
            Type = transaction.Type,
            GroupCategory = _inputSanitization.CreateSafeDisplayText(transaction.GroupCategory),
            FinancialPeriodId = transaction.FinancialPeriodId,
            FinancialPeriod = transaction.FinancialPeriod // Reference is safe
        };
    }
    
    /// <summary>
    /// Calculate financial totals from loaded transactions
    /// 
    /// @param transactions - Pre-loaded transaction data
    /// </summary>
    private void CalculateTotals(List<TransactionLine> transactions)
    {
        TotalRevenue = transactions
            .Where(t => t.Type == TransactionType.Revenue)
            .Sum(t => t.Amount);
        
        TotalExpenses = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => Math.Abs(t.Amount));
            
        _logger.LogDebug($"Financial totals - Revenue: €{TotalRevenue:N2}, Expenses: €{TotalExpenses:N2}, Net: €{NetResult:N2}");
    }
    
    /// <summary>
    /// Get consistent color for German expense categories across all charts
    /// Uses colorblind-friendly palette with sufficient contrast
    /// Enhanced with additional German BWA categories
    /// 
    /// @param category - German category name (sanitized)
    /// @returns CSS color value for consistent chart display
    /// </summary>
    private static string GetCategoryColor(string category)
    {
        // Comprehensive German BWA category color mapping
        var germanCategoryColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Personnel costs (highest priority - red tones)
            { "Personalkosten", "#dc3545" },        // Red - Personnel costs
            { "Personal", "#e74c3c" },              // Red variant
            
            // Material costs (orange tones)
            { "Materialaufwand", "#fd7e14" },       // Orange - Material expenses
            { "Kosten Warenabgabe", "#ff8c00" },    // Dark orange - Goods costs
            { "Materialkosten", "#ff7f50" },        // Coral - Material costs
            
            // Operating costs (blue/purple tones)
            { "Raumkosten", "#6f42c1" },            // Purple - Space costs
            { "Betriebliche Steuern", "#6610f2" },  // Indigo - Operating taxes
            { "Betriebskosten", "#6f42c1" },        // Purple - Operating costs
            
            // Vehicle and transportation (cyan tones)
            { "Fahrzeugkosten (ohne Steuer)", "#0dcaf0" }, // Cyan - Vehicle costs
            { "Fahrzeugkosten", "#17a2b8" },        // Teal - Vehicle costs
            
            // Insurance and legal (green tones)
            { "Versicherungen/Beiträge", "#198754" }, // Green - Insurance
            { "Versicherungen", "#28a745" },        // Success green
            
            // Taxes (gray tones)
            { "Steuern", "#6c757d" },               // Gray - Taxes
            { "Steuern Einkommen u. Ertrag", "#495057" }, // Dark gray - Income tax
            
            // Marketing and sales (pink tones)
            { "Werbe-/Reisekosten", "#e83e8c" },    // Pink - Advertising costs
            { "Werbekosten", "#d63384" },           // Pink variant
            { "Reisekosten", "#f8d7da" },           // Light pink
            
            // Depreciation and maintenance (yellow tones)
            { "Abschreibungen", "#ffc107" },        // Yellow - Depreciation
            { "Reparatur/Instandhaltung", "#ffca2c" }, // Yellow variant
            { "Reparaturkosten", "#ffd43b" },       // Light yellow
            
            // Financial costs (teal tones)
            { "Zinsen", "#20c997" },                // Teal - Interest
            { "Finanzierungskosten", "#17a2b8" },   // Info blue
            
            // Special costs (brown tones)
            { "Besondere Kosten", "#8b4513" },      // Saddle brown
            
            // Other/miscellaneous (neutral tones)
            { "Sonstige Kosten", "#495057" },       // Dark gray - Other costs
            { "Sonstige", "#6c757d" },              // Gray - Other
            
            // Revenue categories (green tones for positive)
            { "Umsatzerlöse", "#28a745" },          // Success green - Revenue
            { "So. betr. Erlöse", "#20c997" },      // Teal - Other operating revenue
        };
        
        // Check for exact matches first
        if (germanCategoryColors.TryGetValue(category, out var exactColor))
        {
            return exactColor;
        }
        
        // Check for partial matches for categories containing key terms
        foreach (var (key, color) in germanCategoryColors)
        {
            if (category.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return color;
            }
        }
        
        // Default color for unknown categories (light gray)
        return "#adb5bd"; // Bootstrap gray-400
    }
}

/// <summary>
/// Represents expense category data for pie chart visualization
/// German categories preserved (e.g., "Personalkosten", "Materialaufwand")
/// Enhanced with security and validation
/// </summary>
public class ExpenseCategory
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Color { get; set; } = string.Empty; // For consistent chart colors
    
    // Display properties for templates
    public string FormattedAmount => $"€{Amount:N2}";
    public string SafeCategory => System.Web.HttpUtility.HtmlEncode(Category);
}

/// <summary>
/// Represents a group of transactions for a specific month/year
/// Enhanced with performance optimizations and security
/// </summary>
public class TransactionGroup
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthYear { get; set; } = string.Empty;
    public List<TransactionLine> RevenueTransactions { get; set; } = new();
    public List<TransactionLine> ExpenseTransactions { get; set; } = new();
    public List<TransactionLine> OtherTransactions { get; set; } = new();
    public decimal MonthlyRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyNetResult => MonthlyRevenue - MonthlyExpenses;
    
    /// <summary>
    /// Expense breakdown data for pie chart visualization
    /// German categories preserved (e.g., "Personalkosten", "Materialaufwand")
    /// All data sanitized for safe HTML display
    /// </summary>
    public List<ExpenseCategory> ExpenseBreakdown { get; set; } = new();
    
    // Display properties
    public string FormattedRevenue => $"€{MonthlyRevenue:N2}";
    public string FormattedExpenses => $"€{MonthlyExpenses:N2}";
    public string FormattedNetResult => $"€{MonthlyNetResult:N2}";
    public bool IsProfitable => MonthlyNetResult > 0;
    public int TotalTransactions => RevenueTransactions.Count + ExpenseTransactions.Count + OtherTransactions.Count;
}