using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service interface for analyzing financial trends over time
/// 
/// Business Context:
/// - Aggregates transaction data by position/category across time periods
/// - Supports filtering by year, position names, and transaction types
/// - Provides data suitable for Chart.js time-series visualizations
/// - Uses German date formatting for user-friendly period labels
/// </summary>
public interface ITrendAnalysisService
{
    /// <summary>
    /// Get position trends data for Chart.js visualization
    /// 
    /// Data Processing:
    /// - Groups transactions by category/position and time period
    /// - Calculates monthly aggregates for each position
    /// - Applies optional year and position filtering
    /// - Returns data in format ready for frontend charting
    /// 
    /// Business Rules:
    /// - Revenue positions show positive trend lines
    /// - Expense positions show their actual values (may be negative)
    /// - Summary positions are excluded from trend analysis
    /// - German month names used for period labels
    /// 
    /// @param year Optional year filter - if null, includes all years
    /// @param positionFilter Optional list of specific positions to include
    /// @returns TrendData with positions list and time series for each position
    /// </summary>
    Task<TrendData> GetPositionTrendsAsync(int? year = null, List<string>? positionFilter = null);
}