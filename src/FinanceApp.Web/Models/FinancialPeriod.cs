using System.ComponentModel.DataAnnotations;

namespace FinanceApp.Web.Models;

/// <summary>
/// Represents a financial reporting period (month/year) that has been imported
/// Business Context: Tracks which financial periods have already been processed
/// to prevent duplicate imports of the same month's data
/// </summary>
public class FinancialPeriod
{
    public int Id { get; set; }
    
    [Required]
    public int Year { get; set; }
    
    [Required]
    [Range(1, 12)]
    public int Month { get; set; }
    
    /// <summary>
    /// Original filename of the imported PDF
    /// Helps track the source of imported data for auditing
    /// </summary>
    public string? SourceFileName { get; set; }
    
    /// <summary>
    /// Timestamp when this period was imported
    /// </summary>
    public DateTime ImportedAt { get; set; }
    
    /// <summary>
    /// Navigation property to all transaction lines for this period
    /// </summary>
    public ICollection<TransactionLine> TransactionLines { get; set; } = new List<TransactionLine>();
    
    /// <summary>
    /// Creates a unique identifier string for this period
    /// Used for duplicate checking before import
    /// </summary>
    public string GetPeriodKey() => $"{Year:0000}-{Month:00}";
}