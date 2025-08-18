using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApp.Web.Models;

/// <summary>
/// Represents a single line item from the financial report (Jahresübersicht)
/// Business Context: Each row in the PDF becomes a TransactionLine with amounts for each month
/// German accounting terms are preserved as they appear in source documents
/// </summary>
public class TransactionLine
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the financial period this transaction belongs to
    /// </summary>
    public int FinancialPeriodId { get; set; }
    
    /// <summary>
    /// Navigation property to the parent period
    /// </summary>
    public FinancialPeriod FinancialPeriod { get; set; } = null!;
    
    /// <summary>
    /// Category/Description from the PDF (e.g., "Umsatzerlöse", "Personalkosten")
    /// German terms preserved as they appear in the source BWA report
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// The specific month this amount applies to (1-12)
    /// </summary>
    [Range(1, 12)]
    public int Month { get; set; }
    
    /// <summary>
    /// The year this transaction applies to
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Transaction amount in EUR
    /// Negative values represent expenses, positive values represent revenue
    /// German format (1.234,56) is converted to decimal during import
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Type of transaction for easier filtering
    /// </summary>
    public TransactionType Type { get; set; }
    
    /// <summary>
    /// Optional grouping category for reporting
    /// (e.g., "Kostenarten" for expense categories)
    /// </summary>
    [MaxLength(100)]
    public string? GroupCategory { get; set; }
}

/// <summary>
/// Classification of transaction types based on German accounting categories
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Revenue items (Erlöse, Umsatzerlöse)
    /// </summary>
    Revenue,
    
    /// <summary>
    /// Expense items (Kosten, Aufwand)
    /// </summary>
    Expense,
    
    /// <summary>
    /// Summary/calculated items (Ergebnis, Rohertrag)
    /// </summary>
    Summary,
    
    /// <summary>
    /// Other/neutral items
    /// </summary>
    Other
}