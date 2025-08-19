using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Web.Pages;

/**
 * Page model for Position Trends analysis functionality.
 * 
 * Business Context:
 * - Displays time-series charts showing how individual BWA positions develop over time
 * - German position examples: "Personalkosten" (personnel costs), "Umsatzerlöse" (sales revenue)
 * - Supports filtering by position, transaction type (revenue/expense), and year
 * - Uses authentic German financial data from BWA (Betriebswirtschaftliche Auswertung) reports
 * - Integrates with Chart.js for interactive multi-line visualization
 * 
 * German Financial Context:
 * - BWA positions represent standardized German accounting categories
 * - Revenue positions (Erlöse) typically trend upward in growing businesses
 * - Major expense positions like Personalkosten show seasonal and growth patterns
 * - Summary positions (Gesamtkosten, Betriebsergebnis) excluded to prevent double-counting
 * 
 * Data Flow:
 * - Page loads with available filter options (German position names, years)
 * - JavaScript makes AJAX calls to PositionTrends handler with encoded German parameters
 * - Backend aggregates BWA data by position and time period with German date formatting
 * - Client-side code renders interactive multi-line charts with German number formatting
 * - Color coding: Green for Erlöse (revenue), Red for Kosten (expenses)
 */
public class PositionTrendsModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<PositionTrendsModel> _logger;
    private readonly ITrendAnalysisService _trendAnalysisService;
    
    /// <summary>
    /// List of unique position names available in the database for filter dropdown
    /// Extracted from transaction_lines.category column
    /// </summary>
    public List<string>? AvailablePositions { get; set; }
    
    /// <summary>
    /// List of years with data available for year filter dropdown
    /// Extracted from financial_periods.year column
    /// </summary>
    public List<int>? AvailableYears { get; set; }
    
    public PositionTrendsModel(
        AppDbContext context, 
        ILogger<PositionTrendsModel> logger,
        ITrendAnalysisService trendAnalysisService)
    {
        _context = context;
        _logger = logger;
        _trendAnalysisService = trendAnalysisService;
    }
    
    /// <summary>
    /// Initializes the page with filter dropdown data
    /// 
    /// Loads:
    /// - All unique position names from transaction data
    /// - All years that have financial data
    /// - Excludes summary/total positions to focus on actual line items
    /// </summary>
    public async Task OnGetAsync()
    {
        try
        {
            // Get all unique position names, excluding summary positions
            // Summary positions (like "Gesamtkosten") are excluded as they represent totals
            AvailablePositions = await _context.TransactionLines
                .Where(t => t.Type != TransactionType.Summary) // Exclude summary rows
                .Select(t => t.Category)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();
            
            // Get all years that have financial data
            AvailableYears = await _context.FinancialPeriods
                .Select(fp => fp.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
                
            _logger.LogInformation(
                "Position Trends page loaded with {PositionCount} positions and {YearCount} years",
                AvailablePositions?.Count ?? 0, 
                AvailableYears?.Count ?? 0
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Position Trends page data");
            AvailablePositions = new List<string>();
            AvailableYears = new List<int>();
        }
    }
    
    /// <summary>
    /// AJAX endpoint that returns position trend data for Chart.js
    /// 
    /// Query Parameters:
    /// - positions: Optional comma-separated list of position names to include
    /// - year: Optional year filter (if not provided, includes all years)
    /// - type: Optional type filter ("revenue", "expenses", or "all")
    /// 
    /// Returns JSON in format expected by the frontend:
    /// {
    ///   "positions": ["Personalkosten", "Umsatzerlöse", ...],
    ///   "series": [
    ///     {
    ///       "positionName": "Personalkosten",
    ///       "type": "Expense",
    ///       "dataPoints": [
    ///         { "period": "Jan 2024", "amount": 15234.56 },
    ///         { "period": "Feb 2024", "amount": 15456.78 }
    ///       ]
    ///     }
    ///   ]
    /// }
    /// 
    /// Business Rules:
    /// - Data points are aggregated by month/year periods
    /// - Amounts are summed for positions that appear multiple times in a period
    /// - Revenue positions get positive values, expenses remain as stored
    /// - German date formatting for period labels ("Jan 2024")
    /// </summary>
    public async Task<IActionResult> OnGetPositionTrendsAsync(
        string? positions = null, 
        int? year = null, 
        string? type = null)
    {
        try
        {
            _logger.LogInformation(
                "Position Trends API called with positions={Positions}, year={Year}, type={Type}",
                positions, year, type
            );
            
            // Parse position filter if provided
            List<string>? positionFilter = null;
            if (!string.IsNullOrWhiteSpace(positions))
            {
                positionFilter = positions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
            }
            
            // Get trend data from service
            var trendData = await _trendAnalysisService.GetPositionTrendsAsync(year, positionFilter);
            
            // Apply type filter if specified
            if (!string.IsNullOrWhiteSpace(type) && type.ToLower() != "all")
            {
                var filteredType = type.ToLower() switch
                {
                    "revenue" => "Revenue",
                    "expenses" => "Expense",
                    _ => null
                };
                
                if (filteredType != null)
                {
                    // Filter series to only include matching types
                    trendData.Series = trendData.Series
                        .Where(s => s.Type == filteredType)
                        .ToList();
                    
                    // Update positions list to match filtered series
                    trendData.Positions = trendData.Series
                        .Select(s => s.PositionName)
                        .Distinct()
                        .OrderBy(p => p)
                        .ToList();
                }
            }
            
            _logger.LogInformation(
                "Returning trend data with {PositionCount} positions and {SeriesCount} series",
                trendData.Positions.Count, 
                trendData.Series.Count
            );
            
            return new JsonResult(trendData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Position Trends API endpoint");
            return new JsonResult(new { error = "An error occurred while loading the data." })
            {
                StatusCode = 500
            };
        }
    }
}