using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using FinanceApp.Web.Models;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for parsing German financial PDF reports (Jahresübersicht)
/// Extracts financial data dynamically without hardcoding dates or values
/// Handles German number format (1.234,56) and BWA accounting structure
/// </summary>
public class PdfParserService
{
    private readonly ILogger<PdfParserService> _logger;
    
    // German culture for parsing numbers with comma as decimal separator
    private readonly CultureInfo _germanCulture = new CultureInfo("de-DE");
    
    public PdfParserService(ILogger<PdfParserService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Parse a financial PDF and extract all transaction data
    /// Dynamically identifies year, months, and transaction categories
    /// </summary>
    public Task<ParsedFinancialData> ParsePdfAsync(Stream pdfStream, string fileName)
    {
        var result = new ParsedFinancialData
        {
            SourceFileName = fileName,
            TransactionLines = new List<ParsedTransactionLine>()
        };
        
        try
        {
            using var document = PdfDocument.Open(pdfStream);
            
            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                _logger.LogDebug($"Processing {lines.Length} lines from PDF page");
                
                // Extract year from header or filename
                result.Year = ExtractYear(lines, fileName);
                _logger.LogInformation($"Extracted year: {result.Year}");
                
                // Find the header line with months and parse the data table
                ParseDataTable(lines, result);
            }
            
            _logger.LogInformation($"Parsed {result.TransactionLines.Count} transaction lines from PDF");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PDF");
            throw new InvalidOperationException($"Failed to parse PDF: {ex.Message}", ex);
        }
        
        return Task.FromResult(result);
    }
    
    /// <summary>
    /// Parse the data table from the PDF text lines
    /// This PDF format appears to have everything in one concatenated line
    /// </summary>
    private void ParseDataTable(string[] lines, ParsedFinancialData result)
    {
        // The PDF data seems to be in one big concatenated line
        // Find the line that contains the financial data
        string dataLine = "";
        foreach (var line in lines)
        {
            if (line.Contains("Jan/" + result.Year) && line.Length > 1000) // Big line with data
            {
                dataLine = line;
                _logger.LogDebug($"Found data line with length: {line.Length}");
                break;
            }
        }
        
        if (string.IsNullOrEmpty(dataLine))
        {
            _logger.LogWarning("No concatenated data line found in PDF");
            return;
        }
        
        // Extract months from header pattern
        var months = ExtractMonthsFromHeader(dataLine, result.Year);
        if (!months.Any())
        {
            _logger.LogWarning("No month columns found in PDF header");
            return;
        }
        
        _logger.LogInformation($"Found months: {string.Join(", ", months)}");
        
        // Parse the financial categories and their values from the concatenated line
        ParseConcatenatedFinancialData(dataLine, months, result);
    }
    
    /// <summary>
    /// Extract months from the header line
    /// </summary>
    private List<int> ExtractMonthsFromHeader(string headerLine, int year)
    {
        var months = new List<int>();
        var monthNames = new[] { "Jan", "Feb", "Mrz", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez" };
        
        for (int i = 0; i < monthNames.Length; i++)
        {
            if (headerLine.Contains(monthNames[i] + "/" + year))
            {
                months.Add(i + 1);
            }
        }
        
        return months;
    }
    
    /// <summary>
    /// Parse the concatenated financial data from the single line format
    /// Only includes individual line items, excludes summary totals to prevent double-counting
    /// Uses PDF table structure for accurate month alignment
    /// </summary>
    private void ParseConcatenatedFinancialData(string dataLine, List<int> months, ParsedFinancialData result)
    {
        _logger.LogDebug($"Parsing data line with {months.Count} months: {string.Join(",", months)}");
        
        // Define the exact categories from the PDF table structure
        var categoryMappings = new Dictionary<string, string[]>
        {
            // Revenue categories (use exact PDF text)
            { "Umsatzerlöse", new[] { "Umsatzerlöse" } },
            { "So. betr. Erlöse", new[] { "So. betr. Erlöse" } },
            
            // Individual expense categories (under Kostenarten section)
            { "Personalkosten", new[] { "Personalkosten" } },
            { "Raumkosten", new[] { "Raumkosten" } },
            { "Betriebliche Steuern", new[] { "Betriebliche Steuern" } },
            { "Versicherungen/Beiträge", new[] { "Versicherungen/Beiträge" } },
            { "Besondere Kosten", new[] { "Besondere Kosten" } },
            { "Fahrzeugkosten (ohne Steuer)", new[] { "Fahrzeugkosten (ohne Steuer)", "Fahrzeugkosten" } },
            { "Werbe-/Reisekosten", new[] { "Werbe-/Reisekosten" } },
            { "Kosten Warenabgabe", new[] { "Kosten Warenabgabe" } },
            { "Abschreibungen", new[] { "Abschreibungen" } },
            { "Reparatur/Instandhaltung", new[] { "Reparatur/Instandhaltung" } },
            { "Sonstige Kosten", new[] { "Sonstige Kosten" } },
            
            // Tax category
            { "Steuern Einkommen u. Ertrag", new[] { "Steuern Einkommen u. Ertrag" } }
        };
        
        foreach (var mapping in categoryMappings)
        {
            var displayName = mapping.Key;
            var searchTerms = mapping.Value;
            
            string? matchedTerm = null;
            int categoryIndex = -1;
            
            // Try to find the category with different search terms
            foreach (var searchTerm in searchTerms)
            {
                categoryIndex = dataLine.IndexOf(searchTerm);
                if (categoryIndex != -1)
                {
                    matchedTerm = searchTerm;
                    break;
                }
            }
            
            if (categoryIndex == -1 || matchedTerm == null) continue;
            
            _logger.LogDebug($"Found category '{displayName}' as '{matchedTerm}' at position {categoryIndex}");
            
            // Get the substring starting from the matched category
            var fromCategory = dataLine.Substring(categoryIndex);
            
            // Find the end of the category name by looking for the first number
            var categoryEndMatch = Regex.Match(fromCategory, matchedTerm + @"\s*");
            if (!categoryEndMatch.Success) continue;
            
            var afterCategoryName = fromCategory.Substring(categoryEndMatch.Length);
            
            // Extract all German-formatted numbers immediately after the category
            var numberPattern = @"-?\d{1,3}(?:\.\d{3})*,\d{2}";
            var matches = Regex.Matches(afterCategoryName, numberPattern);
            
            if (matches.Count == 0)
            {
                _logger.LogDebug($"No numbers found for category '{displayName}'");
                continue;
            }
            
            // Parse the numbers
            var numbers = new List<decimal>();
            foreach (Match match in matches)
            {
                var parsed = ParseGermanNumber(match.Value);
                if (parsed.HasValue)
                {
                    numbers.Add(parsed.Value);
                }
                
                // Stop after we have enough numbers for all months
                if (numbers.Count >= months.Count) break;
            }
            
            _logger.LogDebug($"Category '{displayName}': Found {numbers.Count} numbers: [{string.Join(", ", numbers)}]");
            
            // Create transactions for each month
            for (int i = 0; i < Math.Min(numbers.Count, months.Count); i++)
            {
                // Always create transaction entries, even for zero values, for debugging
                var amount = numbers[i];
                var month = months[i];
                
                _logger.LogDebug($"  {displayName} - Month {month}: €{amount}");
                
                if (amount != 0) // Only save non-zero amounts to database
                {
                    result.TransactionLines.Add(new ParsedTransactionLine
                    {
                        Category = displayName,
                        Month = month,
                        Year = result.Year,
                        Amount = amount,
                        Type = DetermineTransactionType(displayName)
                    });
                }
            }
        }
        
        _logger.LogInformation($"Total parsed transactions: {result.TransactionLines.Count}");
    }
    
    /// <summary>
    /// Parse a single financial data line
    /// </summary>
    private List<ParsedTransactionLine>? ParseFinancialDataLine(string line, List<int> months, int year)
    {
        // Skip header lines and empty lines
        if (string.IsNullOrWhiteSpace(line) || 
            line.Contains("Bezeichnung") || line.Contains("Jan/") || line.Contains("Feb/") ||
            line.Contains("BWA-") || line.Contains("SKR:") || line.Contains("Status") ||
            line.Contains("Werte in EUR") || line.Contains("Blatt") || line.Contains("Das vorläufige"))
        {
            return null;
        }
        
        // Extract category name (first word/phrase before the numbers)
        var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;
        
        var category = parts[0];
        
        // Skip if this looks like a number or is too short
        if (string.IsNullOrEmpty(category) || category.Length < 3 ||
            Regex.IsMatch(category, @"^\d"))
        {
            return null;
        }
        
        // Extract all numbers from the line
        var numberPattern = @"-?\d{1,3}(?:\.\d{3})*(?:,\d{2})";
        var matches = Regex.Matches(line, numberPattern);
        
        if (matches.Count == 0)
            return null;
        
        var numbers = matches.Select(m => ParseGermanNumber(m.Value))
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();
        
        if (!numbers.Any())
            return null;
        
        var transactions = new List<ParsedTransactionLine>();
        
        // Map numbers to months (take up to the number of months we have)
        int monthIndex = 0;
        foreach (var number in numbers.Take(months.Count))
        {
            if (number != 0 && monthIndex < months.Count)
            {
                transactions.Add(new ParsedTransactionLine
                {
                    Category = category,
                    Month = months[monthIndex],
                    Year = year,
                    Amount = number,
                    Type = DetermineTransactionType(category)
                });
            }
            monthIndex++;
        }
        
        return transactions.Any() ? transactions : null;
    }
    
    /// <summary>
    /// Extract year from PDF content or filename
    /// </summary>
    private int ExtractYear(string[] lines, string fileName)
    {
        // Try to find year in the header lines
        foreach (var line in lines.Take(10))
        {
            var yearMatch = Regex.Match(line, @"\b(20\d{2})\b");
            if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
            {
                return year;
            }
        }
        
        // Fallback: try to extract from filename
        var fileYearMatch = Regex.Match(fileName, @"(20\d{2})");
        if (fileYearMatch.Success && int.TryParse(fileYearMatch.Groups[1].Value, out var fileYear))
        {
            return fileYear;
        }
        
        // Default to current year if not found
        return DateTime.Now.Year;
    }
    
    /// <summary>
    /// Parse German number format (1.234,56) to decimal
    /// </summary>
    private decimal? ParseGermanNumber(string numberStr)
    {
        if (string.IsNullOrWhiteSpace(numberStr))
            return null;
        
        try
        {
            // Handle German format: 1.234,56
            // Remove thousands separators (.) and replace decimal separator (,) with (.)
            var normalized = numberStr.Replace(".", "").Replace(",", ".");
            
            if (decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, 
                CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Failed to parse number: {numberStr} - {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Determine transaction type based on German accounting category names
    /// Uses exact category matching for accurate classification
    /// </summary>
    private TransactionType DetermineTransactionType(string category)
    {
        var lowerCategory = category.ToLower();
        
        // Explicit tax categories (ALWAYS expenses)
        if (lowerCategory.Contains("steuer") || lowerCategory == "steuern einkommen u. ertrag")
        {
            return TransactionType.Expense;
        }
        
        // Revenue categories (income/sales)
        if (lowerCategory.Contains("umsatz") || lowerCategory.Contains("erlös") || 
            lowerCategory.Contains("einnahme") || lowerCategory == "so. betr. erlöse")
        {
            return TransactionType.Revenue;
        }
        
        // Expense categories (costs and expenses) 
        if (lowerCategory.Contains("kosten") || lowerCategory.Contains("aufwand") || 
            lowerCategory.Contains("abschreibung") || lowerCategory.Contains("personal") ||
            lowerCategory.Contains("raum") || lowerCategory.Contains("versicherung") ||
            lowerCategory.Contains("fahrzeug") || lowerCategory.Contains("werbe") ||
            lowerCategory.Contains("material") || lowerCategory.Contains("waren") ||
            lowerCategory.Contains("besondere") || lowerCategory.Contains("reparatur") ||
            lowerCategory.Contains("sonstige"))
        {
            return TransactionType.Expense;
        }
        
        // Summary/Result categories (computed totals)
        if (lowerCategory.Contains("ergebnis") || lowerCategory.Contains("rohertrag") || 
            lowerCategory.Contains("summe") || lowerCategory.Contains("gesamt") ||
            lowerCategory.Contains("betrieblich") || lowerCategory.Contains("ertrag"))
        {
            return TransactionType.Summary;
        }
        
        return TransactionType.Other;
    }
}

/// <summary>
/// Result of parsing a financial PDF
/// </summary>
public class ParsedFinancialData
{
    public int Year { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public List<ParsedTransactionLine> TransactionLines { get; set; } = new();
}

/// <summary>
/// A single parsed transaction line before database import
/// </summary>
public class ParsedTransactionLine
{
    public string Category { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
}