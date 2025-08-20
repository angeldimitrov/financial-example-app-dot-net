using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for comprehensive input sanitization with focus on XSS prevention
/// 
/// Security Features:
/// - HTML tag removal and encoding
/// - Script injection prevention
/// - Dangerous character neutralization
/// - German character preservation (ä, ö, ü, ß, €)
/// 
/// German BWA Context:
/// Sanitizes user input while preserving German business terminology
/// and financial data formatting used in BWA reports
/// </summary>
public interface IInputSanitizationService
{
    string SanitizeInput(string input);
    string SanitizeHtml(string input);
    string SanitizeFileName(string fileName);
    string SanitizeBwaCategory(string category);
    bool ContainsScriptInjection(string input);
    string SanitizeTransactionInput(string input, string inputType);
    string CreateSafeDisplayText(string input);
    string SanitizeNumericInput(string input);
}

public class InputSanitizationService : IInputSanitizationService
{
    // Script injection patterns - compiled for performance
    private static readonly Regex ScriptPattern = new(
        @"<\s*script[^>]*>.*?<\s*/\s*script\s*>|javascript\s*:|on\w+\s*=|<\s*iframe[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // HTML tag pattern for removal
    private static readonly Regex HtmlTagPattern = new(
        @"<[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // SQL injection patterns for database safety
    private static readonly Regex SqlInjectionPattern = new(
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|UNION|SCRIPT)\b)|('|('')|;|--|/\*|\*/)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // German characters that should be preserved in BWA data
    private static readonly HashSet<char> GermanChars = new() { 'ä', 'ö', 'ü', 'ß', 'Ä', 'Ö', 'Ü', '€' };

    private readonly ILogger<InputSanitizationService> _logger;

    public InputSanitizationService(ILogger<InputSanitizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// General input sanitization for form data and user input
    /// Preserves German characters while removing dangerous content
    /// </summary>
    public string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            // Check for script injection attempts
            if (ContainsScriptInjection(input))
            {
                _logger.LogWarning("Script injection attempt detected: {Input}", 
                    input.Length > 100 ? input[..100] + "..." : input);
                return string.Empty; // Reject completely
            }

            // HTML encode to prevent XSS
            var sanitized = HttpUtility.HtmlEncode(input);

            // Remove any remaining HTML tags
            sanitized = HtmlTagPattern.Replace(sanitized, string.Empty);

            // Remove potential SQL injection patterns
            if (SqlInjectionPattern.IsMatch(sanitized))
            {
                _logger.LogWarning("Potential SQL injection pattern detected in input");
                sanitized = SqlInjectionPattern.Replace(sanitized, string.Empty);
            }

            // Trim and normalize whitespace
            sanitized = NormalizeWhitespace(sanitized);

            // Validate length (prevent buffer overflow attacks)
            if (sanitized.Length > 1000)
            {
                _logger.LogWarning("Input too long, truncating: {Length} characters", sanitized.Length);
                sanitized = sanitized[..1000];
            }

            return sanitized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing input: {Input}", input);
            return string.Empty; // Fail secure
        }
    }

    /// <summary>
    /// HTML-specific sanitization for rich text content
    /// Removes all HTML while preserving German business text
    /// </summary>
    public string SanitizeHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            // First check for malicious content
            if (ContainsScriptInjection(input))
            {
                _logger.LogWarning("Malicious HTML content detected");
                return string.Empty;
            }

            // Remove all HTML tags
            var sanitized = HtmlTagPattern.Replace(input, string.Empty);

            // Decode HTML entities while preserving German characters
            sanitized = HttpUtility.HtmlDecode(sanitized);

            // Additional XSS prevention - encode special characters
            sanitized = sanitized
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#x27;")
                .Replace("/", "&#x2F;");

            return NormalizeWhitespace(sanitized);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing HTML: {Input}", input);
            return string.Empty;
        }
    }

    /// <summary>
    /// File name sanitization for secure file handling
    /// Prevents path traversal and dangerous characters
    /// </summary>
    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "unknown_file";

        try
        {
            // Remove directory path components
            var sanitized = Path.GetFileName(fileName);

            // Remove dangerous characters for file systems
            var dangerousChars = new[] { '<', '>', ':', '"', '|', '?', '*', '\\', '/' };
            foreach (var ch in dangerousChars)
            {
                sanitized = sanitized.Replace(ch, '_');
            }

            // Remove control characters
            sanitized = new string(sanitized.Where(c => !char.IsControl(c)).ToArray());

            // Ensure reasonable length
            if (sanitized.Length > 255)
            {
                var extension = Path.GetExtension(sanitized);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
                sanitized = nameWithoutExt[..(255 - extension.Length)] + extension;
            }

            // Ensure file has a name
            if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(sanitized)))
            {
                sanitized = "sanitized_file" + Path.GetExtension(sanitized);
            }

            return sanitized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing filename: {FileName}", fileName);
            return "error_file.pdf";
        }
    }

    /// <summary>
    /// BWA category sanitization for German financial data
    /// Preserves German accounting terminology while preventing injection
    /// </summary>
    public string SanitizeBwaCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return string.Empty;

        try
        {
            // BWA categories should only contain letters, spaces, numbers, and German chars
            var allowedPattern = new Regex(@"[^a-zA-ZäöüßÄÖÜ0-9\s\-\.\,€]", RegexOptions.Compiled);
            var sanitized = allowedPattern.Replace(category, string.Empty);

            // Normalize whitespace
            sanitized = NormalizeWhitespace(sanitized);

            // Validate against common BWA categories
            if (IsValidBwaCategory(sanitized))
            {
                return sanitized;
            }

            _logger.LogWarning("Invalid BWA category format: {Category}", category);
            return "Unbekannte Kategorie";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing BWA category: {Category}", category);
            return "Fehlerhafte Kategorie";
        }
    }

    /// <summary>
    /// Detects script injection attempts in user input
    /// Comprehensive pattern matching for various XSS vectors
    /// </summary>
    public bool ContainsScriptInjection(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        try
        {
            // Check for common script injection patterns
            if (ScriptPattern.IsMatch(input))
                return true;

            // Check for encoded script attempts
            var decoded = HttpUtility.HtmlDecode(input);
            if (ScriptPattern.IsMatch(decoded))
                return true;

            // Check for URL-encoded attempts
            var urlDecoded = HttpUtility.UrlDecode(input);
            if (ScriptPattern.IsMatch(urlDecoded))
                return true;

            // Check for various encoding bypasses
            var lowercaseInput = input.ToLower();
            var suspiciousPatterns = new[]
            {
                "javascript:",
                "vbscript:",
                "data:text/html",
                "eval(",
                "expression(",
                "alert(",
                "confirm(",
                "prompt(",
                "document.cookie",
                "window.location",
                "<img src=x onerror=",
                "<svg onload="
            };

            return suspiciousPatterns.Any(pattern => lowercaseInput.Contains(pattern));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for script injection: {Input}", input);
            return true; // Fail secure
        }
    }

    /// <summary>
    /// Normalizes whitespace in sanitized content
    /// Removes excessive spaces while preserving readability
    /// </summary>
    private static string NormalizeWhitespace(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Replace multiple whitespace with single space
        var normalized = Regex.Replace(input, @"\s+", " ", RegexOptions.Compiled);

        // Trim leading and trailing whitespace
        return normalized.Trim();
    }

    /// <summary>
    /// Validates if category matches common German BWA patterns
    /// Helps detect legitimate vs malicious category names
    /// </summary>
    private static bool IsValidBwaCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return false;

        // Common German BWA category patterns
        var validPatterns = new[]
        {
            @"^[a-zA-ZäöüßÄÖÜ\s\-]{2,}$", // General German text
            @"^Umsatz.*", // Revenue categories
            @"^Personal.*", // Personnel costs
            @"^Raum.*", // Space costs
            @"^Material.*", // Material costs
            @"^Abschreibung.*", // Depreciation
            @"^Sonstige.*", // Other costs
            @"^Zins.*", // Interest
            @"^Steuer.*", // Taxes
            @"^\d+\s+.*" // Categories starting with numbers
        };

        return validPatterns.Any(pattern => 
            Regex.IsMatch(category, pattern, RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Sanitize transaction input based on type
    /// </summary>
    public string SanitizeTransactionInput(string input, string inputType)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return inputType.ToLower() switch
        {
            "category" => SanitizeBwaCategory(input),
            "numeric" => SanitizeNumericInput(input) ?? "0",
            _ => SanitizeInput(input)
        };
    }

    /// <summary>
    /// Create safe display text for UI output
    /// </summary>
    public string CreateSafeDisplayText(string input)
    {
        return SanitizeHtml(input);
    }

    /// <summary>
    /// Sanitize numeric input for financial data
    /// </summary>
    public string SanitizeNumericInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove any non-numeric characters except German decimal separators
        var sanitized = Regex.Replace(input, @"[^0-9.,\-]", "", RegexOptions.Compiled);
        return sanitized;
    }
}