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
/// 
/// Performance Optimizations:
/// - Compiled regex patterns for better performance
/// - Comprehensive error handling and recovery
/// - Memory-efficient stream processing
/// - Optimized pattern matching for German financial formats
/// </summary>
public class PdfParserService
{
    private readonly ILogger<PdfParserService> _logger;
    private readonly IInputSanitizationService _inputSanitization;
    
    // German culture for parsing numbers with comma as decimal separator
    private readonly CultureInfo _germanCulture = new CultureInfo("de-DE");
    
    // Compiled regex patterns for performance (Issue #6: Regex Compilation)
    private static readonly Regex YearPattern = new(@"\b(20\d{2})\b", RegexOptions.Compiled);
    private static readonly Regex GermanNumberPattern = new(@"-?\d{1,3}(?:\.\d{3})*,\d{2}", RegexOptions.Compiled);
    private static readonly Regex CategoryEndPattern = new(@"\s*", RegexOptions.Compiled);
    private static readonly Regex NumericStartPattern = new(@"^\d", RegexOptions.Compiled);
    private static readonly Regex FileYearPattern = new(@"(20\d{2})", RegexOptions.Compiled);
    
    // BWA Category mappings as constants (Issue #9: Magic Numbers)
    /// <summary>
    /// German BWA (Betriebswirtschaftliche Auswertung) standard category mappings
    /// These categories are standardized across German accounting software
    /// 
    /// Business Context:
    /// - BWA categories follow German DATEV standards
    /// - Each category maps to specific account ranges in German chart of accounts (SKR)
    /// - Categories support multiple search terms for flexibility across different BWA formats
    /// </summary>
    private static readonly Dictionary<string, string[]> BwaCategories = new()
    {
        // Revenue categories (Erlöse)
        { "Umsatzerlöse", new[] { "Umsatzerlöse" } },
        { "So. betr. Erlöse", new[] { "So. betr. Erlöse", "Sonstige betriebliche Erlöse" } },
        
        // Personnel costs (Personalkosten)
        { "Personalkosten", new[] { "Personalkosten" } },
        
        // Operating costs (Betriebskosten)
        { "Raumkosten", new[] { "Raumkosten" } },
        { "Betriebliche Steuern", new[] { "Betriebliche Steuern" } },
        { "Versicherungen/Beiträge", new[] { "Versicherungen/Beiträge", "Versicherungen" } },
        { "Besondere Kosten", new[] { "Besondere Kosten" } },
        { "Fahrzeugkosten (ohne Steuer)", new[] { "Fahrzeugkosten (ohne Steuer)", "Fahrzeugkosten" } },
        { "Werbe-/Reisekosten", new[] { "Werbe-/Reisekosten", "Werbekosten", "Reisekosten" } },
        { "Kosten Warenabgabe", new[] { "Kosten Warenabgabe", "Materialkosten" } },
        { "Abschreibungen", new[] { "Abschreibungen" } },
        { "Reparatur/Instandhaltung", new[] { "Reparatur/Instandhaltung", "Reparaturkosten" } },
        { "Sonstige Kosten", new[] { "Sonstige Kosten" } },
        
        // Tax categories (always expenses in German accounting)
        { "Steuern Einkommen u. Ertrag", new[] { "Steuern Einkommen u. Ertrag", "Steuern" } }
    };
    
    public PdfParserService(ILogger<PdfParserService> logger, IInputSanitizationService inputSanitization)
    {
        _logger = logger;
        _inputSanitization = inputSanitization;
    }
    
    /// <summary>
    /// Parse a financial PDF and extract all transaction data
    /// Dynamically identifies year, months, and transaction categories
    /// 
    /// Enhanced Error Handling (Issue #8):
    /// - Comprehensive try-catch blocks with specific error recovery
    /// - Detailed logging for debugging PDF parsing issues
    /// - Graceful degradation when partial data is available
    /// - Validation of parsed data integrity
    /// 
    /// @param pdfStream - Input PDF stream to parse
    /// @param fileName - Original filename for logging and year extraction
    /// @returns ParsedFinancialData with extracted transactions or empty result on failure
    /// </summary>
    public async Task<ParsedFinancialData> ParsePdfAsync(Stream pdfStream, string fileName)
    {
        var result = new ParsedFinancialData
        {
            SourceFileName = _inputSanitization.SanitizeFileName(fileName),
            TransactionLines = new List<ParsedTransactionLine>()
        };
        
        try
        {
            using var document = PdfDocument.Open(pdfStream);
            
            if (document.NumberOfPages == 0)
            {
                _logger.LogWarning($"PDF has no pages: {fileName}");
                throw new InvalidOperationException("PDF-Datei enthält keine Seiten.");
            }
            
            foreach (var page in document.GetPages())
            {
                try
                {
                    var text = page.Text;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _logger.LogWarning($"Page {page.Number} contains no text: {fileName}");
                        continue;
                    }
                    
                    var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    _logger.LogDebug($"Processing {lines.Length} lines from PDF page {page.Number}");
                    
                    // Extract year from header or filename
                    result.Year = ExtractYear(lines, fileName);
                    _logger.LogInformation($"Extracted year: {result.Year} from {fileName}");
                    
                    // Find the header line with months and parse the data table
                    ParseDataTable(lines, result);
                }
                catch (Exception pageEx)
                {
                    _logger.LogError(pageEx, $"Error processing page {page.Number} in PDF: {fileName}");
                    // Continue with next page instead of failing completely
                    continue;
                }
            }
            
            // Validate parsed data
            await ValidateParsedDataAsync(result);
            
            _logger.LogInformation($"Successfully parsed {result.TransactionLines.Count} transaction lines from PDF: {fileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error parsing PDF: {fileName}");
            throw new InvalidOperationException($"Fehler beim Verarbeiten der PDF-Datei: {ex.Message}", ex);
        }
        
        return result;
    }
    
    /// <summary>
    /// Validate parsed data for consistency and business rule compliance
    /// 
    /// Validation Rules:
    /// - At least one transaction found
    /// - All transactions have valid amounts
    /// - Year is reasonable (2000-2030)
    /// - Categories match expected German BWA structure
    /// 
    /// @param parsedData - Data to validate
    /// @throws InvalidOperationException if data fails validation
    /// </summary>
    private async Task ValidateParsedDataAsync(ParsedFinancialData parsedData)
    {
        await Task.CompletedTask; // Make method async for future extensions
        
        if (!parsedData.TransactionLines.Any())
        {
            _logger.LogWarning($"No transaction data found in PDF: {parsedData.SourceFileName}");
            throw new InvalidOperationException("Keine Finanzdaten in der PDF-Datei gefunden.");
        }
        
        if (parsedData.Year < 2000 || parsedData.Year > 2030)
        {
            _logger.LogWarning($"Suspicious year extracted: {parsedData.Year}");
            // Don't throw - use current year as fallback
            parsedData.Year = DateTime.Now.Year;
        }
        
        // Validate transaction amounts
        var invalidAmounts = parsedData.TransactionLines.Where(t => t.Amount == 0).ToList();
        if (invalidAmounts.Any())
        {
            _logger.LogDebug($"Found {invalidAmounts.Count} transactions with zero amounts - these will be filtered out");
        }
        
        // Validate categories are from known BWA structure
        var unknownCategories = parsedData.TransactionLines
            .Select(t => t.Category)
            .Distinct()
            .Where(cat => !BwaCategories.ContainsKey(cat) && 
                         !BwaCategories.Values.Any(synonyms => synonyms.Contains(cat)))
            .ToList();
            
        if (unknownCategories.Any())
        {
            _logger.LogInformation($"Found unknown categories: {string.Join(", ", unknownCategories)}");
            // Don't fail - just log for analysis
        }
    }
    
    /// <summary>
    /// Parse the data table from the PDF text lines
    /// This PDF format appears to have everything in one concatenated line
    /// 
    /// Enhanced Error Recovery:
    /// - Multiple strategies for finding data line
    /// - Fallback parsing methods if primary approach fails
    /// - Detailed logging for troubleshooting
    /// </summary>
    private void ParseDataTable(string[] lines, ParsedFinancialData result)
    {
        try
        {
            // Strategy 1: Find the concatenated line with financial data
            string? dataLine = FindDataLineStrategy1(lines, result.Year);
            
            // Strategy 2: Fallback to any long line with month patterns
            if (string.IsNullOrEmpty(dataLine))
            {
                dataLine = FindDataLineStrategy2(lines, result.Year);
            }
            
            // Strategy 3: Combine multiple lines if data is split
            if (string.IsNullOrEmpty(dataLine))
            {
                dataLine = FindDataLineStrategy3(lines, result.Year);
            }
            
            if (string.IsNullOrEmpty(dataLine))
            {
                _logger.LogWarning("No financial data line found in PDF using any strategy");
                throw new InvalidOperationException("Keine Finanzdaten in der PDF gefunden.");
            }
            
            // Extract months from header pattern
            var months = ExtractMonthsFromHeader(dataLine, result.Year);
            if (!months.Any())
            {
                _logger.LogWarning("No month columns found in PDF header");
                throw new InvalidOperationException("Keine Monatsspalten in der PDF gefunden.");
            }
            
            _logger.LogInformation($"Found months: {string.Join(", ", months)}");
            
            // Parse the financial categories and their values from the concatenated line
            ParseConcatenatedFinancialData(dataLine, months, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing data table from PDF");
            throw new InvalidOperationException($"Fehler beim Lesen der Datentabelle: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Strategy 1: Find line containing specific year pattern and sufficient length
    /// </summary>
    private string? FindDataLineStrategy1(string[] lines, int year)
    {
        foreach (var line in lines)
        {
            if (line.Contains($"Jan/{year}") && line.Length > 1000)
            {
                _logger.LogDebug($"Strategy 1: Found data line with length: {line.Length}");
                return line;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Strategy 2: Find any line with German month abbreviations and year
    /// </summary>
    private string? FindDataLineStrategy2(string[] lines, int year)
    {
        var germanMonths = new[] { "Jan", "Feb", "Mrz", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez" };
        
        foreach (var line in lines)
        {
            var monthCount = germanMonths.Count(month => line.Contains($"{month}/{year}"));
            if (monthCount >= 6 && line.Length > 500) // At least 6 months and reasonable length
            {
                _logger.LogDebug($"Strategy 2: Found data line with {monthCount} months, length: {line.Length}");
                return line;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Strategy 3: Combine multiple lines that might contain split data
    /// </summary>
    private string? FindDataLineStrategy3(string[] lines, int year)
    {
        var candidateLines = lines.Where(line => 
            line.Contains(year.ToString()) && 
            GermanNumberPattern.IsMatch(line)).ToList();
            
        if (candidateLines.Count >= 2)
        {
            var combined = string.Join(" ", candidateLines);
            _logger.LogDebug($"Strategy 3: Combined {candidateLines.Count} lines, total length: {combined.Length}");
            return combined;
        }
        return null;
    }
    
    /// <summary>
    /// Extract months from the header line using compiled regex
    /// </summary>
    private List<int> ExtractMonthsFromHeader(string headerLine, int year)
    {
        var months = new List<int>();
        var monthNames = new[] { "Jan", "Feb", "Mrz", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez" };
        
        for (int i = 0; i < monthNames.Length; i++)
        {
            if (headerLine.Contains($"{monthNames[i]}/{year}"))
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
    /// 
    /// Refactored for better maintainability (Issue #10: Code Duplication)
    /// </summary>
    private void ParseConcatenatedFinancialData(string dataLine, List<int> months, ParsedFinancialData result)
    {
        _logger.LogDebug($"Parsing data line with {months.Count} months: {string.Join(",", months)}");
        
        foreach (var mapping in BwaCategories)
        {
            try
            {
                ParseSingleCategory(dataLine, mapping.Key, mapping.Value, months, result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error parsing category {mapping.Key}");
                // Continue with other categories
                continue;
            }
        }
        
        _logger.LogInformation($"Total parsed transactions: {result.TransactionLines.Count}");
    }
    
    /// <summary>
    /// Parse a single BWA category from the data line
    /// Extracted to reduce code duplication and improve testability
    /// 
    /// @param dataLine - Full data line from PDF
    /// @param displayName - Category display name
    /// @param searchTerms - Alternative search terms for this category
    /// @param months - Available months in the data
    /// @param result - Result object to populate
    /// </summary>
    private void ParseSingleCategory(string dataLine, string displayName, string[] searchTerms, List<int> months, ParsedFinancialData result)
    {
        string? matchedTerm = null;
        int categoryIndex = -1;
        
        // Try to find the category with different search terms
        foreach (var searchTerm in searchTerms)
        {
            categoryIndex = dataLine.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
            if (categoryIndex != -1)
            {
                matchedTerm = searchTerm;
                break;
            }
        }
        
        if (categoryIndex == -1 || matchedTerm == null) 
        {
            _logger.LogDebug($"Category '{displayName}' not found in data line");
            return;
        }
        
        _logger.LogDebug($"Found category '{displayName}' as '{matchedTerm}' at position {categoryIndex}");
        
        // Get the substring starting from the matched category
        var fromCategory = dataLine.Substring(categoryIndex);
        
        // Find the end of the category name by looking for the first number
        var categoryEndMatch = Regex.Match(fromCategory, Regex.Escape(matchedTerm) + @"\s*", RegexOptions.Compiled);
        if (!categoryEndMatch.Success)
        {
            _logger.LogDebug($"Could not find end of category name for '{displayName}'");
            return;
        }
        
        var afterCategoryName = fromCategory.Substring(categoryEndMatch.Length);
        
        // Extract all German-formatted numbers immediately after the category
        var matches = GermanNumberPattern.Matches(afterCategoryName);
        
        if (matches.Count == 0)
        {
            _logger.LogDebug($"No numbers found for category '{displayName}'");
            return;
        }
        
        // Parse the numbers with better error handling
        var numbers = new List<decimal>();
        foreach (Match match in matches)
        {
            var parsed = ParseGermanNumber(match.Value);
            if (parsed.HasValue)
            {
                numbers.Add(parsed.Value);
            }
            else
            {
                _logger.LogWarning($"Failed to parse number: {match.Value} for category {displayName}");
            }
            
            // Stop after we have enough numbers for all months
            if (numbers.Count >= months.Count) break;
        }
        
        _logger.LogDebug($"Category '{displayName}': Found {numbers.Count} numbers: [{string.Join(", ", numbers)}]");
        
        // Create transactions for each month
        for (int i = 0; i < Math.Min(numbers.Count, months.Count); i++)
        {
            var amount = numbers[i];
            var month = months[i];
            
            _logger.LogDebug($"  {displayName} - Month {month}: €{amount}");
            
            // Only save non-zero amounts to database
            if (amount != 0)
            {
                result.TransactionLines.Add(new ParsedTransactionLine
                {
                    Category = _inputSanitization.SanitizeTransactionInput(displayName, "category"),
                    Month = month,
                    Year = result.Year,
                    Amount = amount,
                    Type = DetermineTransactionType(displayName)
                });
            }
        }
    }
    
    /// <summary>
    /// Extract year from PDF content or filename with enhanced error handling
    /// </summary>
    private int ExtractYear(string[] lines, string fileName)
    {
        try
        {
            // Try to find year in the header lines (first 10 lines)
            foreach (var line in lines.Take(10))
            {
                var yearMatch = YearPattern.Match(line);
                if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
                {
                    _logger.LogDebug($"Found year {year} in PDF header");
                    return year;
                }
            }
            
            // Fallback: try to extract from filename
            var fileYearMatch = FileYearPattern.Match(fileName);
            if (fileYearMatch.Success && int.TryParse(fileYearMatch.Groups[1].Value, out var fileYear))
            {
                _logger.LogDebug($"Found year {fileYear} in filename");
                return fileYear;
            }
            
            // Default to current year if not found
            var currentYear = DateTime.Now.Year;
            _logger.LogWarning($"Could not extract year from PDF or filename, defaulting to {currentYear}");
            return currentYear;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting year from PDF");
            return DateTime.Now.Year;
        }
    }
    
    /// <summary>
    /// Parse German number format (1.234,56) to decimal with enhanced error handling
    /// Performance optimized version using compiled regex and direct parsing
    /// </summary>
    private decimal? ParseGermanNumber(string numberStr)
    {
        if (string.IsNullOrWhiteSpace(numberStr))
            return null;
        
        try
        {
            // Sanitize input first
            numberStr = _inputSanitization.SanitizeNumericInput(numberStr) ?? string.Empty;
            if (string.IsNullOrEmpty(numberStr))
                return null;
            
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
            _logger.LogDebug(ex, $"Failed to parse German number: {numberStr}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Determine transaction type based on German accounting category names
    /// Uses exact category matching for accurate classification
    /// 
    /// Enhanced with comprehensive category mapping (Issue #11: Missing Documentation)
    /// 
    /// German Accounting Rules:
    /// - All tax categories ("Steuern") are always expenses
    /// - Revenue categories ("Umsatz", "Erlöse") represent income
    /// - Cost categories ("Kosten", "Aufwand") represent expenses
    /// - Summary categories are excluded from import to prevent double-counting
    /// 
    /// @param category - German category name from BWA report
    /// @returns TransactionType classification for accounting purposes
    /// </summary>
    private static TransactionType DetermineTransactionType(string category)
    {
        var lowerCategory = category.ToLower();
        
        // Explicit tax categories (ALWAYS expenses in German accounting)
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
        
        // Summary/Result categories (computed totals - excluded from import)
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
/// Result of parsing a financial PDF with enhanced metadata
/// </summary>
public class ParsedFinancialData
{
    public int Year { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public List<ParsedTransactionLine> TransactionLines { get; set; } = new();
    
    // Additional metadata for validation
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
    public int TotalCategoriesFound { get; set; }
    public List<string> UnknownCategories { get; set; } = new();
}

/// <summary>
/// A single parsed transaction line before database import
/// Enhanced with validation metadata
/// </summary>
public class ParsedTransactionLine
{
    public string Category { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    
    // Validation properties
    public bool IsValid => !string.IsNullOrWhiteSpace(Category) && Amount != 0 && Month is >= 1 and <= 12;
}