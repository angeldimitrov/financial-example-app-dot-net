using System.Globalization;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for analyzing financial trends over time periods
/// 
/// Business Context:
/// - Aggregates transaction data by position across monthly periods
/// - Supports multiple filtering options for focused analysis
/// - Provides German-localized date formatting for user interface
/// - Excludes summary/total positions to focus on actual line items
/// 
/// Key Features:
/// - Time-series aggregation by position and period
/// - Automatic type classification (Revenue/Expense)
/// - German month formatting ("Jan 2024" style)
/// - Flexible filtering by year and position names
/// - Data structure optimized for Chart.js visualization
/// </summary>
public class TrendAnalysisService : ITrendAnalysisService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TrendAnalysisService> _logger;
    
    // German culture for month name formatting
    private readonly CultureInfo _germanCulture = new CultureInfo("de-DE");
    
    public TrendAnalysisService(AppDbContext context, ILogger<TrendAnalysisService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    /// <summary>
    /// Get position trends data aggregated by month/year periods
    /// 
    /// Processing Steps:
    /// 1. Filter transactions by year and positions if specified
    /// 2. Exclude summary positions to focus on actual line items
    /// 3. Group by position and time period
    /// 4. Aggregate amounts for each position/period combination
    /// 5. Format periods with German month names
    /// 6. Structure data for Chart.js consumption
    /// 
    /// Business Rules:
    /// - Only non-summary transactions included in analysis
    /// - Revenue and expense positions treated equally for aggregation
    /// - Zero amounts are included to show complete time series
    /// - Periods sorted chronologically for proper chart display
    /// 
    /// @param year Optional year filter - restricts analysis to specific year
    /// @param positionFilter Optional position names - limits analysis to specific categories
    /// @returns TrendData with aggregated position trends over time
    /// </summary>
    public async Task<TrendData> GetPositionTrendsAsync(int? year = null, List<string>? positionFilter = null)
    {
        try
        {
            _logger.LogInformation(
                "Getting position trends for year={Year}, positions={Positions}",
                year, positionFilter != null ? string.Join(",", positionFilter) : "all"
            );
            
            // Build base query - exclude summary positions as they represent calculated totals
            var query = _context.TransactionLines
                .Where(t => t.Type != TransactionType.Summary)
                .AsQueryable();
            
            // Apply year filter if specified
            if (year.HasValue)
            {
                query = query.Where(t => t.Year == year.Value);
            }
            
            // Apply position filter if specified
            if (positionFilter != null && positionFilter.Any())
            {
                query = query.Where(t => positionFilter.Contains(t.Category));
            }
            
            // Group by position (category) and time period, then aggregate amounts
            // This creates one data point per position per month/year combination
            var groupedData = await query
                .GroupBy(t => new { 
                    Position = t.Category, 
                    t.Year, 
                    t.Month, 
                    t.Type 
                })
                .Select(g => new {
                    Position = g.Key.Position,
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Type = g.Key.Type,
                    TotalAmount = g.Sum(t => t.Amount) // Aggregate all amounts for this position/period
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Position)
                .ToListAsync();
            
            _logger.LogInformation(
                "Found {DataPointCount} aggregated data points across {PositionCount} positions",
                groupedData.Count,
                groupedData.Select(d => d.Position).Distinct().Count()
            );
            
            // Group by position to create series
            var seriesData = groupedData
                .GroupBy(d => d.Position)
                .Select(positionGroup => new PositionSeries
                {
                    PositionName = positionGroup.Key,
                    Type = MapTransactionTypeToString(positionGroup.First().Type),
                    DataPoints = positionGroup
                        .Select(d => new TrendDataPoint
                        {
                            Period = FormatPeriodGerman(d.Year, d.Month),
                            Amount = d.TotalAmount,
                            Year = d.Year,
                            Month = d.Month
                        })
                        .OrderBy(dp => dp.Year)
                        .ThenBy(dp => dp.Month)
                        .ToList()
                })
                .OrderBy(s => s.PositionName)
                .ToList();
            
            // Extract unique position names for metadata
            var positions = seriesData
                .Select(s => s.PositionName)
                .OrderBy(p => p)
                .ToList();
            
            var result = new TrendData
            {
                Positions = positions,
                Series = seriesData
            };
            
            _logger.LogInformation(
                "Returning trend data: {PositionCount} positions, {SeriesCount} series, {TotalDataPoints} total data points",
                result.Positions.Count,
                result.Series.Count,
                result.Series.Sum(s => s.DataPoints.Count)
            );
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting position trends");
            throw new InvalidOperationException($"Failed to get position trends: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Format a year/month combination using German month names
    /// 
    /// Returns format like "Jan 2024", "Feb 2024" etc.
    /// Consistent with German financial reporting conventions
    /// 
    /// @param year The year component
    /// @param month The month component (1-12)
    /// @returns Formatted period string with German month abbreviation
    /// </summary>
    private string FormatPeriodGerman(int year, int month)
    {
        var date = new DateTime(year, month, 1);
        
        // Use German culture to get localized month abbreviations
        // This ensures consistency with German financial documents
        return date.ToString("MMM yyyy", _germanCulture);
    }
    
    /// <summary>
    /// Map TransactionType enum to user-friendly string
    /// 
    /// Used for frontend filtering and chart styling:
    /// - "Revenue" positions typically shown in green
    /// - "Expense" positions typically shown in red
    /// - Other types get neutral styling
    /// 
    /// @param type The transaction type enum value
    /// @returns User-friendly type string for frontend consumption
    /// </summary>
    private string MapTransactionTypeToString(TransactionType type)
    {
        return type switch
        {
            TransactionType.Revenue => "Revenue",
            TransactionType.Expense => "Expense", 
            TransactionType.Summary => "Summary",
            TransactionType.Other => "Other",
            _ => "Unknown"
        };
    }
}