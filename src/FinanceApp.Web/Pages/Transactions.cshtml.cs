using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Pages;

public class TransactionsModel : PageModel
{
    private readonly AppDbContext _context;
    
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
    
    public TransactionsModel(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task OnGetAsync(int? year, int? month, string? type)
    {
        SelectedYear = year;
        SelectedMonth = month;
        SelectedType = type;
        
        // Get available years for filter dropdown
        AvailableYears = await _context.TransactionLines
            .Select(t => t.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();
        
        // Build query with filters
        var query = _context.TransactionLines.AsQueryable();
        
        if (year.HasValue)
        {
            query = query.Where(t => t.Year == year.Value);
        }
        
        if (month.HasValue)
        {
            query = query.Where(t => t.Month == month.Value);
        }
        
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, out var transactionType))
        {
            query = query.Where(t => t.Type == transactionType);
        }
        
        // Load transactions grouped by month
        var transactions = await query
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ThenBy(t => t.Category)
            .ToListAsync();
        
        // Group transactions by month/year and separate by type
        GroupedTransactions = transactions
            .GroupBy(t => new { t.Year, t.Month })
            .Select(g => new TransactionGroup
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                MonthYear = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                RevenueTransactions = g.Where(t => t.Type == TransactionType.Revenue).OrderBy(t => t.Category).ToList(),
                ExpenseTransactions = g.Where(t => t.Type == TransactionType.Expense).OrderBy(t => t.Category).ToList(),
                OtherTransactions = g.Where(t => t.Type != TransactionType.Revenue && t.Type != TransactionType.Expense).OrderBy(t => t.Category).ToList(),
                MonthlyRevenue = g.Where(t => t.Type == TransactionType.Revenue).Sum(t => t.Amount),
                MonthlyExpenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => Math.Abs(t.Amount)),
                ExpenseBreakdown = g.Where(t => t.Type == TransactionType.Expense)
                    .GroupBy(t => t.Category)
                    .Select(categoryGroup => new ExpenseCategory
                    {
                        Category = categoryGroup.Key,
                        Amount = categoryGroup.Sum(t => Math.Abs(t.Amount)),
                        Color = GetCategoryColor(categoryGroup.Key)
                    })
                    .OrderByDescending(ec => ec.Amount)
                    .ToList()
            })
            .ToList();
        
        // Calculate totals
        TotalRevenue = transactions
            .Where(t => t.Type == TransactionType.Revenue)
            .Sum(t => t.Amount);
        
        TotalExpenses = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => Math.Abs(t.Amount));
    }
    
    /// <summary>
    /// Get consistent color for German expense categories across all charts
    /// Uses colorblind-friendly palette with sufficient contrast
    /// </summary>
    private static string GetCategoryColor(string category)
    {
        var germanCategoryColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Personalkosten", "#dc3545" },        // Red - Personnel costs
            { "Materialaufwand", "#fd7e14" },      // Orange - Material expenses
            { "Raumkosten", "#6f42c1" },           // Purple - Space costs
            { "Fahrzeugkosten", "#0dcaf0" },       // Cyan - Vehicle costs
            { "Versicherungen", "#198754" },       // Green - Insurance
            { "Steuern", "#6c757d" },              // Gray - Taxes
            { "Werbekosten", "#e83e8c" },          // Pink - Advertising costs
            { "Abschreibungen", "#ffc107" },       // Yellow - Depreciation
            { "Zinsen", "#20c997" },               // Teal - Interest
            { "Sonstige", "#495057" }              // Dark gray - Other
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
        
        // Default color for unknown categories
        return "#adb5bd"; // Bootstrap gray-400
    }
}

/// <summary>
/// Represents expense category data for pie chart visualization
/// German categories preserved (e.g., "Personalkosten", "Materialaufwand")
/// </summary>
public class ExpenseCategory
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Color { get; set; } = string.Empty; // For consistent chart colors
}

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
    /// </summary>
    public List<ExpenseCategory> ExpenseBreakdown { get; set; } = new();
}