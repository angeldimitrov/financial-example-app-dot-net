using System.ComponentModel.DataAnnotations;

namespace FinanceApp.Web.Models;

/// <summary>
/// Data model for CSV export options from the premium export modal
/// 
/// Business Context:
/// - Supports date range filtering with inclusive boundaries
/// - Transaction type filtering (Revenue, Expense, Both)
/// - German Excel format option for proper decimal formatting
/// - Comprehensive validation for security and data integrity
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Optional start date for filtering transactions (inclusive)
    /// Business Rule: If provided, only transactions from this date onwards are included
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Optional end date for filtering transactions (inclusive)
    /// Business Rule: If provided, only transactions up to this date are included
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Transaction type filter: "Revenue", "Expense", "Both", or null
    /// Business Rule: 
    /// - "Revenue": Only revenue transactions
    /// - "Expense": Only expense transactions  
    /// - "Both" or null: All transaction types
    /// Default: "Both"
    /// </summary>
    public string? TransactionType { get; set; } = "Both";

    /// <summary>
    /// Include revenue transactions in export
    /// Derived from frontend checkbox selection
    /// </summary>
    public bool IncludeRevenue { get; set; } = true;

    /// <summary>
    /// Include expense transactions in export
    /// Derived from frontend checkbox selection
    /// </summary>
    public bool IncludeExpenses { get; set; } = true;

    /// <summary>
    /// Export format: "standard" or "german"
    /// Business Rule:
    /// - "standard": Standard CSV with comma separator and dot decimal
    /// - "german": German Excel format with semicolon separator and comma decimal
    /// Default: "standard"
    /// </summary>
    public string ExportFormat { get; set; } = "standard";

    /// <summary>
    /// Validates the export options according to business rules
    /// 
    /// Validation Rules:
    /// - Start date cannot be after end date
    /// - At least one transaction type must be selected
    /// - Export format must be valid
    /// 
    /// @returns List of validation error messages (empty if valid)
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Validate date range
        if (StartDate.HasValue && EndDate.HasValue && StartDate > EndDate)
        {
            errors.Add("Das Startdatum darf nicht nach dem Enddatum liegen.");
        }

        // Validate at least one transaction type is selected
        if (!IncludeRevenue && !IncludeExpenses)
        {
            errors.Add("Mindestens ein Transaktionstyp muss ausgewählt werden.");
        }

        // Validate export format
        if (!string.IsNullOrEmpty(ExportFormat) && 
            !new[] { "standard", "german" }.Contains(ExportFormat.ToLower()))
        {
            errors.Add("Ungültiges Export-Format.");
        }

        return errors;
    }

    /// <summary>
    /// Converts frontend checkbox selections to backend transaction type filter
    /// 
    /// Mapping Logic:
    /// - Both Revenue AND Expenses checked: "Both"
    /// - Only Revenue checked: "Revenue"  
    /// - Only Expenses checked: "Expense"
    /// - Neither checked: Invalid (handled by validation)
    /// 
    /// @returns Appropriate transaction type string for backend filtering
    /// </summary>
    public string GetTransactionTypeFilter()
    {
        if (IncludeRevenue && IncludeExpenses)
        {
            return "Both";
        }
        else if (IncludeRevenue)
        {
            return "Revenue";
        }
        else if (IncludeExpenses)
        {
            return "Expense";
        }
        else
        {
            return "Both"; // Fallback, though validation should catch this
        }
    }

    /// <summary>
    /// Determines if German number formatting should be used
    /// 
    /// @returns True if German Excel format is selected
    /// </summary>
    public bool UseGermanFormatting()
    {
        return string.Equals(ExportFormat, "german", StringComparison.OrdinalIgnoreCase);
    }
}