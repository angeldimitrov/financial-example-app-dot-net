namespace FinanceApp.Web.Models;

/// <summary>
/// Container for position trend analysis data
/// 
/// Business Context:
/// - Used to transfer time-series data from backend to frontend
/// - Structured for Chart.js multi-line chart consumption
/// - Contains both position metadata and their respective time series
/// - Supports filtering and dynamic chart generation
/// </summary>
public class TrendData
{
    /// <summary>
    /// List of all position names included in the trend analysis
    /// Used for filter dropdowns and legend generation
    /// </summary>
    public List<string> Positions { get; set; } = new();
    
    /// <summary>
    /// Time series data for each position
    /// Each series represents one line on the chart
    /// </summary>
    public List<PositionSeries> Series { get; set; } = new();
}

/// <summary>
/// Time series data for a single position/category
/// 
/// Business Context:
/// - Represents one line on a multi-line chart
/// - Contains all data points for a specific position over time
/// - Includes metadata for chart styling and filtering
/// </summary>
public class PositionSeries
{
    /// <summary>
    /// Display name of the position (e.g., "Personalkosten", "Umsatzerlöse")
    /// German accounting terms preserved from source data
    /// </summary>
    public string PositionName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type classification for filtering and styling
    /// Revenue positions typically shown in green, expenses in red
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Individual data points for this position over time
    /// Ordered chronologically for proper chart rendering
    /// </summary>
    public List<TrendDataPoint> DataPoints { get; set; } = new();
}

/// <summary>
/// Single data point in a time series
/// 
/// Business Context:
/// - Represents aggregated amount for a position in a specific period
/// - Period formatted with German month names for user display
/// - Amount aggregated from potentially multiple transactions
/// </summary>
public class TrendDataPoint
{
    /// <summary>
    /// Human-readable period label (e.g., "Jan 2024", "Feb 2024")
    /// Uses German date formatting for consistency with source documents
    /// </summary>
    public string Period { get; set; } = string.Empty;
    
    /// <summary>
    /// Aggregated amount for this position in this period
    /// Sum of all transactions for this position in the given month/year
    /// Positive for revenues, may be negative for expenses
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Year component for sorting and internal processing
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Month component (1-12) for sorting and internal processing
    /// </summary>
    public int Month { get; set; }
}