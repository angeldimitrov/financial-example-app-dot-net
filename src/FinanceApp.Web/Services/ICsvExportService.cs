using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service interface for exporting financial transaction data to CSV format.
/// Provides German-localized CSV exports with BWA (Betriebswirtschaftliche Auswertung) formatting.
/// 
/// Business Context:
/// - Exports follow German Excel compatibility standards (semicolon delimiter, UTF-8 BOM)
/// - Number formatting uses German conventions (comma decimal separator)
/// - File naming includes timestamp for version control
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Export all transaction data to CSV format with German localization
    /// 
    /// Performance Requirements:
    /// - Target: <5 seconds for 10,000 transactions
    /// - Memory limit: <100MB for 50,000 rows
    /// - Uses streaming to minimize memory footprint
    /// 
    /// Format Specifications:
    /// - Delimiter: Semicolon (;) for German Excel compatibility
    /// - Encoding: UTF-8 with BOM
    /// - Number format: German (1.234,56 with comma decimal separator)
    /// - Date format: dd.MM.yyyy HH:mm:ss
    /// 
    /// Column Structure (German headers):
    /// - Jahr: Year from financial period
    /// - Monat: Month from financial period  
    /// - Kategorie: BWA category classification
    /// - Beschreibung: Transaction description
    /// - Betrag: Amount in German number format
    /// - Typ: Transaction type (Umsatz/Ausgabe/Sonstiges)
    /// - Erstellt am: Creation timestamp
    /// - Period ID: Financial period identifier
    /// </summary>
    /// <returns>CSV file content as byte array with UTF-8 BOM encoding</returns>
    /// <exception cref="InvalidOperationException">Thrown when database is unavailable</exception>
    /// <exception cref="OutOfMemoryException">Thrown when data set exceeds memory limits</exception>
    Task<byte[]> ExportAllTransactionsToCsvAsync();

    /// <summary>
    /// Generate timestamped filename for CSV exports
    /// 
    /// Format: BWA_Export_YYYY-MM-DD_HHmmss.csv
    /// Example: BWA_Export_2024-03-15_143052.csv
    /// 
    /// Business Context:
    /// - BWA prefix indicates German business evaluation format
    /// - Timestamp ensures unique filenames for version control
    /// - ISO date format for international compatibility
    /// </summary>
    /// <returns>Formatted filename string</returns>
    string GenerateExportFilename();
}