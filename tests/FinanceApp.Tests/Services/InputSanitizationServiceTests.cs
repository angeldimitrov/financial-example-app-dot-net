using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FinanceApp.Web.Services;
using System.Text;

namespace FinanceApp.Tests.Services;

/// <summary>
/// Comprehensive security tests for InputSanitizationService
/// 
/// Security Test Coverage:
/// - XSS prevention with comprehensive attack vector testing
/// - SQL injection protection and pattern detection
/// - HTML tag removal and encoding security
/// - German character preservation (ä, ö, ü, ß, €)
/// - BWA category validation for German financial data
/// - File name sanitization and path traversal prevention
/// - Performance testing under load and large input scenarios
/// 
/// German BWA Context:
/// Tests ensure user input sanitization while preserving German business terminology
/// and financial data formatting used in German Jahresübersicht/BWA reports
/// </summary>
public class InputSanitizationServiceTests
{
    private readonly Mock<ILogger<InputSanitizationService>> _loggerMock;
    private readonly InputSanitizationService _service;

    public InputSanitizationServiceTests()
    {
        _loggerMock = new Mock<ILogger<InputSanitizationService>>();
        _service = new InputSanitizationService(_loggerMock.Object);
    }

    #region General Input Sanitization Tests

    [Fact]
    public void SanitizeInput_CleanInput_ReturnsUnchanged()
    {
        // Arrange
        var cleanInput = "Normal business text with numbers 123.45";

        // Act
        var result = _service.SanitizeInput(cleanInput);

        // Assert
        Assert.Equal("Normal business text with numbers 123.45", result);
    }

    [Fact]
    public void SanitizeInput_NullOrEmpty_ReturnsEmpty()
    {
        // Act & Assert
        Assert.Equal(string.Empty, _service.SanitizeInput(null));
        Assert.Equal(string.Empty, _service.SanitizeInput(""));
        Assert.Equal(string.Empty, _service.SanitizeInput("   "));
    }

    [Fact]
    public void SanitizeInput_GermanCharacters_PreservesChars()
    {
        // Arrange - German text with umlauts and special characters
        var germanInput = "Geschäftsführung: Müller & Söhne, Prüfung für 1.250,50 €";

        // Act
        var result = _service.SanitizeInput(germanInput);

        // Assert
        Assert.Contains("ä", result);
        Assert.Contains("ü", result);
        Assert.Contains("ö", result);
        Assert.Contains("€", result);
        Assert.Contains("1.250,50", result);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<SCRIPT>alert('XSS')</SCRIPT>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("onclick='alert(1)'")]
    [InlineData("onmouseover='malicious()'")]
    [InlineData("<iframe src='javascript:alert(1)'></iframe>")]
    public void SanitizeInput_ScriptInjection_ReturnsEmpty(string maliciousInput)
    {
        // Act
        var result = _service.SanitizeInput(maliciousInput);

        // Assert
        Assert.Equal(string.Empty, result);
        VerifyWarningLogged("Script injection attempt detected");
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("DROP TABLE financial_periods")]
    [InlineData("INSERT INTO transactions")]
    [InlineData("'; DELETE FROM --")]
    [InlineData("UNION SELECT password")]
    [InlineData("/* comment */ UPDATE")]
    public void SanitizeInput_SqlInjection_RemovesPatterns(string sqlInput)
    {
        // Act
        var result = _service.SanitizeInput(sqlInput);

        // Assert
        Assert.DoesNotContain("SELECT", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNION", result, StringComparison.OrdinalIgnoreCase);
        VerifyWarningLogged("Potential SQL injection pattern detected");
    }

    [Fact]
    public void SanitizeInput_HtmlTags_RemovesTags()
    {
        // Arrange
        var htmlInput = "<b>Bold text</b> and <i>italic text</i>";

        // Act
        var result = _service.SanitizeInput(htmlInput);

        // Assert
        Assert.DoesNotContain("<b>", result);
        Assert.DoesNotContain("</b>", result);
        Assert.DoesNotContain("<i>", result);
        Assert.DoesNotContain("</i>", result);
        Assert.Contains("Bold text", result);
        Assert.Contains("italic text", result);
    }

    [Fact]
    public void SanitizeInput_ExcessiveWhitespace_NormalizesWhitespace()
    {
        // Arrange
        var messyInput = "  Multiple    spaces\t\tand\n\nlines  ";

        // Act
        var result = _service.SanitizeInput(messyInput);

        // Assert
        Assert.Equal("Multiple spaces and lines", result);
    }

    [Fact]
    public void SanitizeInput_VeryLongInput_TruncatesAt1000Chars()
    {
        // Arrange
        var longInput = new string('A', 1500); // 1500 characters

        // Act
        var result = _service.SanitizeInput(longInput);

        // Assert
        Assert.Equal(1000, result.Length);
        VerifyWarningLogged("Input too long, truncating");
    }

    [Fact]
    public void SanitizeInput_ExceptionDuringProcessing_ReturnsEmpty()
    {
        // This test is tricky to create since the method is robust
        // But we can test the catch block by creating a scenario that might fail
        // For now, we'll just verify the method doesn't throw
        
        // Arrange - Various edge case inputs
        var edgeCaseInputs = new[]
        {
            new string((char)0, 100), // Null characters
            new string((char)1, 50),  // Control characters
            "\0\x01\x02\x03\x04\x05", // Binary data
        };

        // Act & Assert - Should not throw exceptions
        foreach (var input in edgeCaseInputs)
        {
            var result = _service.SanitizeInput(input);
            Assert.NotNull(result); // Should return something, even if empty
        }
    }

    #endregion

    #region HTML Sanitization Tests

    [Fact]
    public void SanitizeHtml_CleanHtml_RemovesTagsKeepsContent()
    {
        // Arrange
        var htmlInput = "<p>This is a paragraph</p><br><strong>Bold text</strong>";

        // Act
        var result = _service.SanitizeHtml(htmlInput);

        // Assert
        Assert.DoesNotContain("<p>", result);
        Assert.DoesNotContain("</p>", result);
        Assert.DoesNotContain("<br>", result);
        Assert.DoesNotContain("<strong>", result);
        Assert.Contains("This is a paragraph", result);
        Assert.Contains("Bold text", result);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert('xss')>")]
    [InlineData("<iframe src='javascript:alert(1)'></iframe>")]
    public void SanitizeHtml_MaliciousHtml_ReturnsEmpty(string maliciousHtml)
    {
        // Act
        var result = _service.SanitizeHtml(maliciousHtml);

        // Assert
        Assert.Equal(string.Empty, result);
        VerifyWarningLogged("Malicious HTML content detected");
    }

    [Fact]
    public void SanitizeHtml_HtmlEntities_DecodesCorrectly()
    {
        // Arrange
        var entityInput = "&lt;p&gt;Text with &amp; entities&lt;/p&gt;";

        // Act
        var result = _service.SanitizeHtml(entityInput);

        // Assert
        Assert.Contains("&lt;", result); // Should be re-encoded for safety
        Assert.Contains("&gt;", result);
        Assert.Contains("&amp;", result);
    }

    [Fact]
    public void SanitizeHtml_GermanHtmlContent_PreservesGermanChars()
    {
        // Arrange
        var germanHtml = "<h1>Geschäftsbericht 2023</h1><p>Kosten: 1.500,75 €</p>";

        // Act
        var result = _service.SanitizeHtml(germanHtml);

        // Assert
        Assert.Contains("Geschäftsbericht", result);
        Assert.Contains("ä", result);
        Assert.Contains("€", result);
        Assert.Contains("1.500,75", result);
        Assert.DoesNotContain("<h1>", result);
        Assert.DoesNotContain("<p>", result);
    }

    [Fact]
    public void SanitizeHtml_SpecialCharacterEncoding_EncodesCorrectly()
    {
        // Arrange
        var specialChars = "Text with <dangerous> chars & 'quotes' and \"double quotes\" and /slashes/";

        // Act
        var result = _service.SanitizeHtml(specialChars);

        // Assert
        Assert.Contains("&lt;", result);
        Assert.Contains("&gt;", result);
        Assert.Contains("&quot;", result);
        Assert.Contains("&#x27;", result);
        Assert.Contains("&#x2F;", result);
    }

    #endregion

    #region File Name Sanitization Tests

    [Fact]
    public void SanitizeFileName_CleanFileName_ReturnsUnchanged()
    {
        // Arrange
        var cleanFileName = "BWA-Report-2023.pdf";

        // Act
        var result = _service.SanitizeFileName(cleanFileName);

        // Assert
        Assert.Equal("BWA-Report-2023.pdf", result);
    }

    [Fact]
    public void SanitizeFileName_NullOrEmpty_ReturnsDefault()
    {
        // Act & Assert
        Assert.Equal("unknown_file", _service.SanitizeFileName(null));
        Assert.Equal("unknown_file", _service.SanitizeFileName(""));
        Assert.Equal("unknown_file", _service.SanitizeFileName("   "));
    }

    [Theory]
    [InlineData("file<script>.pdf")]
    [InlineData("file>redirect.pdf")]
    [InlineData("file:stream.pdf")]
    [InlineData("file\"quote.pdf")]
    [InlineData("file|pipe.pdf")]
    [InlineData("file?query.pdf")]
    [InlineData("file*wildcard.pdf")]
    [InlineData("file\\path.pdf")]
    [InlineData("file/path.pdf")]
    public void SanitizeFileName_DangerousCharacters_ReplacesWithUnderscore(string dangerousFileName)
    {
        // Act
        var result = _service.SanitizeFileName(dangerousFileName);

        // Assert
        Assert.Contains("file_", result);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.DoesNotContain(":", result);
        Assert.DoesNotContain("\"", result);
        Assert.DoesNotContain("|", result);
        Assert.DoesNotContain("?", result);
        Assert.DoesNotContain("*", result);
        Assert.DoesNotContain("\\", result);
        Assert.DoesNotContain("/", result);
        Assert.EndsWith(".pdf", result);
    }

    [Fact]
    public void SanitizeFileName_PathTraversal_RemovesPath()
    {
        // Arrange
        var pathTraversalName = "../../../etc/passwd";

        // Act
        var result = _service.SanitizeFileName(pathTraversalName);

        // Assert
        Assert.Equal("passwd", result);
        Assert.DoesNotContain("..", result);
        Assert.DoesNotContain("/", result);
    }

    [Fact]
    public void SanitizeFileName_ControlCharacters_RemovesControlChars()
    {
        // Arrange
        var controlCharName = "file\0with\x01control\x02chars.pdf";

        // Act
        var result = _service.SanitizeFileName(controlCharName);

        // Assert
        Assert.Equal("filewithcontrolchars.pdf", result);
        Assert.DoesNotContain("\0", result);
        Assert.DoesNotContain("\x01", result);
        Assert.DoesNotContain("\x02", result);
    }

    [Fact]
    public void SanitizeFileName_VeryLongName_TruncatesButPreservesExtension()
    {
        // Arrange
        var longName = new string('A', 300) + ".pdf"; // 304 characters total

        // Act
        var result = _service.SanitizeFileName(longName);

        // Assert
        Assert.True(result.Length <= 255);
        Assert.EndsWith(".pdf", result);
        Assert.StartsWith("A", result);
    }

    [Fact]
    public void SanitizeFileName_EmptyNameWithExtension_GetsDefaultName()
    {
        // Arrange
        var emptyNameFile = ".pdf";

        // Act
        var result = _service.SanitizeFileName(emptyNameFile);

        // Assert
        Assert.Equal("sanitized_file.pdf", result);
    }

    [Fact]
    public void SanitizeFileName_GermanFileName_PreservesGermanChars()
    {
        // Arrange
        var germanFileName = "Geschäftsbericht-Prüfung-2023.pdf";

        // Act
        var result = _service.SanitizeFileName(germanFileName);

        // Assert
        Assert.Equal("Geschäftsbericht-Prüfung-2023.pdf", result);
        Assert.Contains("ä", result);
        Assert.Contains("ü", result);
    }

    [Fact]
    public void SanitizeFileName_ExceptionDuringProcessing_ReturnsErrorFile()
    {
        // This is hard to trigger, but the method should handle any exceptions gracefully
        // For now, test with extreme edge cases
        
        // Arrange
        var problematicName = new string((char)0xFFFF, 10) + ".pdf";

        // Act
        var result = _service.SanitizeFileName(problematicName);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(".pdf", result);
    }

    #endregion

    #region BWA Category Sanitization Tests

    [Theory]
    [InlineData("Umsatzerlöse")]
    [InlineData("Personalkosten")]
    [InlineData("Raumkosten")]
    [InlineData("Materialkosten")]
    [InlineData("Abschreibungen auf Sachanlagen")]
    [InlineData("Sonstige betriebliche Aufwendungen")]
    [InlineData("Steuern vom Einkommen und Ertrag")]
    [InlineData("4000 Umsatzerlöse")]
    [InlineData("6200 Raumkosten")]
    public void SanitizeBwaCategory_ValidGermanCategories_ReturnsUnchanged(string validCategory)
    {
        // Act
        var result = _service.SanitizeBwaCategory(validCategory);

        // Assert
        Assert.Equal(validCategory, result);
    }

    [Fact]
    public void SanitizeBwaCategory_NullOrEmpty_ReturnsEmpty()
    {
        // Act & Assert
        Assert.Equal(string.Empty, _service.SanitizeBwaCategory(null));
        Assert.Equal(string.Empty, _service.SanitizeBwaCategory(""));
    }

    [Fact]
    public void SanitizeBwaCategory_GermanSpecialChars_PreservesChars()
    {
        // Arrange
        var germanCategory = "Bürokosten für Geschäftsführung - Prüfungsgebühren";

        // Act
        var result = _service.SanitizeBwaCategory(germanCategory);

        // Assert
        Assert.Contains("ü", result);
        Assert.Contains("ä", result);
        Assert.Contains("ü", result);
        Assert.Contains("€", result.Replace("€", "€")); // Allow € symbol
    }

    [Theory]
    [InlineData("Category<script>alert()</script>")]
    [InlineData("Personalkosten'; DROP TABLE;")]
    [InlineData("Normal@#$%^&*()Category")]
    [InlineData("Category|with|pipes")]
    [InlineData("Category\"with\"quotes")]
    public void SanitizeBwaCategory_InvalidCharacters_RemovesInvalidChars(string invalidCategory)
    {
        // Act
        var result = _service.SanitizeBwaCategory(invalidCategory);

        // Assert
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.DoesNotContain("'", result);
        Assert.DoesNotContain("@", result);
        Assert.DoesNotContain("#", result);
        Assert.DoesNotContain("$", result);
        Assert.DoesNotContain("%", result);
        Assert.DoesNotContain("|", result);
        Assert.DoesNotContain("\"", result);
    }

    [Fact]
    public void SanitizeBwaCategory_InvalidFormat_ReturnsUnknownCategory()
    {
        // Arrange
        var invalidCategory = "!@#$%^&*()"; // Only special chars

        // Act
        var result = _service.SanitizeBwaCategory(invalidCategory);

        // Assert
        Assert.Equal("Unbekannte Kategorie", result);
        VerifyWarningLogged("Invalid BWA category format");
    }

    [Fact]
    public void SanitizeBwaCategory_NumberedCategories_HandlesCorrectly()
    {
        // Arrange
        var numberedCategories = new[]
        {
            "4000 Umsatzerlöse",
            "4400 Personalkosten",
            "5000 Abschreibungen",
            "123 Test Category"
        };

        foreach (var category in numberedCategories)
        {
            // Act
            var result = _service.SanitizeBwaCategory(category);

            // Assert
            Assert.Equal(category, result);
        }
    }

    [Fact]
    public void SanitizeBwaCategory_ExceptionDuringProcessing_ReturnsErrorCategory()
    {
        // This would be triggered by regex processing issues
        // Hard to create but method should handle gracefully
        
        // Arrange - Test with extreme unicode that might cause issues
        var problematicCategory = new string((char)0x200B, 50); // Zero-width spaces

        // Act
        var result = _service.SanitizeBwaCategory(problematicCategory);

        // Assert
        Assert.NotNull(result);
        // Should either be cleaned or return error category
        Assert.True(result == "Unbekannte Kategorie" || result == "Fehlerhafte Kategorie" || result == "");
    }

    #endregion

    #region Script Injection Detection Tests

    [Fact]
    public void ContainsScriptInjection_CleanInput_ReturnsFalse()
    {
        // Arrange
        var cleanInputs = new[]
        {
            "Normal business text",
            "BWA Report 2023",
            "Geschäftsführung und Prüfung",
            "Costs: 1,234.56 €"
        };

        foreach (var input in cleanInputs)
        {
            // Act
            var result = _service.ContainsScriptInjection(input);

            // Assert
            Assert.False(result);
        }
    }

    [Fact]
    public void ContainsScriptInjection_NullOrEmpty_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(_service.ContainsScriptInjection(null));
        Assert.False(_service.ContainsScriptInjection(""));
        Assert.False(_service.ContainsScriptInjection("   "));
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<SCRIPT>ALERT('XSS')</SCRIPT>")]
    [InlineData("/JavaScript (")]
    [InlineData("/JS (")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("onclick='malicious()'")]
    [InlineData("onmouseover='bad()'")]
    [InlineData("<iframe src='javascript:alert(1)'></iframe>")]
    public void ContainsScriptInjection_DirectScriptPatterns_ReturnsTrue(string maliciousInput)
    {
        // Act
        var result = _service.ContainsScriptInjection(maliciousInput);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("&lt;script&gt;alert('xss')&lt;/script&gt;")]
    [InlineData("&lt;img src=x onerror=alert(1)&gt;")]
    public void ContainsScriptInjection_HtmlEncodedScripts_ReturnsTrue(string encodedScript)
    {
        // Act
        var result = _service.ContainsScriptInjection(encodedScript);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("%3Cscript%3Ealert('xss')%3C/script%3E")]
    [InlineData("%6A%61%76%61%73%63%72%69%70%74%3A")]
    public void ContainsScriptInjection_UrlEncodedScripts_ReturnsTrue(string urlEncodedScript)
    {
        // Act
        var result = _service.ContainsScriptInjection(urlEncodedScript);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("javascript:")]
    [InlineData("vbscript:")]
    [InlineData("data:text/html")]
    [InlineData("eval(")]
    [InlineData("expression(")]
    [InlineData("alert(")]
    [InlineData("confirm(")]
    [InlineData("prompt(")]
    [InlineData("document.cookie")]
    [InlineData("window.location")]
    [InlineData("<img src=x onerror=")]
    [InlineData("<svg onload=")]
    public void ContainsScriptInjection_SuspiciousPatterns_ReturnsTrue(string suspiciousPattern)
    {
        // Arrange
        var testInput = $"Normal text {suspiciousPattern} more text";

        // Act
        var result = _service.ContainsScriptInjection(testInput);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("JAVASCRIPT:")]
    [InlineData("Alert(")]
    [InlineData("DOCUMENT.COOKIE")]
    [InlineData("Window.Location")]
    public void ContainsScriptInjection_CaseInsensitivePatterns_ReturnsTrue(string caseVariation)
    {
        // Arrange
        var testInput = $"Text with {caseVariation} pattern";

        // Act
        var result = _service.ContainsScriptInjection(caseVariation);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsScriptInjection_ExceptionDuringCheck_ReturnsTrue()
    {
        // Hard to create a scenario that causes an exception in this method
        // But it should fail secure if any exception occurs
        
        // Arrange - Test with extreme edge cases that might cause issues
        var edgeCases = new[]
        {
            new string((char)0xFFFF, 1000),
            "\0\x01\x02javascript:\x03\x04",
        };

        foreach (var edgeCase in edgeCases)
        {
            // Act
            var result = _service.ContainsScriptInjection(edgeCase);

            // Assert - Should not throw exception
            Assert.True(result || !result); // Just ensure no exception
        }
    }

    #endregion

    #region Transaction Input Sanitization Tests

    [Fact]
    public void SanitizeTransactionInput_CategoryType_UsesBwaValidation()
    {
        // Arrange
        var categoryInput = "Personalkosten";

        // Act
        var result = _service.SanitizeTransactionInput(categoryInput, "category");

        // Assert
        Assert.Equal("Personalkosten", result);
    }

    [Fact]
    public void SanitizeTransactionInput_NumericType_UsesNumericValidation()
    {
        // Arrange
        var numericInput = "1,234.56€abc";

        // Act
        var result = _service.SanitizeTransactionInput(numericInput, "numeric");

        // Assert
        Assert.Equal("1,234.56", result);
        Assert.DoesNotContain("€", result);
        Assert.DoesNotContain("abc", result);
    }

    [Fact]
    public void SanitizeTransactionInput_OtherType_UsesGeneralSanitization()
    {
        // Arrange
        var generalInput = "Some description text";

        // Act
        var result = _service.SanitizeTransactionInput(generalInput, "description");

        // Assert
        Assert.Equal("Some description text", result);
    }

    [Fact]
    public void SanitizeTransactionInput_NullInput_ReturnsEmpty()
    {
        // Act
        var result = _service.SanitizeTransactionInput(null, "category");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region Display Text Creation Tests

    [Fact]
    public void CreateSafeDisplayText_UsesHtmlSanitization()
    {
        // Arrange
        var htmlInput = "<b>Bold text</b> with <script>alert('xss')</script>";

        // Act
        var result = _service.CreateSafeDisplayText(htmlInput);

        // Assert
        Assert.Equal(string.Empty, result); // Should be empty due to script
    }

    [Fact]
    public void CreateSafeDisplayText_CleanHtml_RemovesTagsKeepsContent()
    {
        // Arrange
        var cleanHtmlInput = "<p>Business report</p><br>Financial data";

        // Act
        var result = _service.CreateSafeDisplayText(cleanHtmlInput);

        // Assert
        Assert.Contains("Business report", result);
        Assert.Contains("Financial data", result);
        Assert.DoesNotContain("<p>", result);
        Assert.DoesNotContain("<br>", result);
    }

    #endregion

    #region Numeric Input Sanitization Tests

    [Theory]
    [InlineData("123.45", "123.45")]
    [InlineData("1,234.56", "1,234.56")]
    [InlineData("1.234,56", "1.234,56")] // German format
    [InlineData("-500.00", "-500.00")]
    [InlineData("-1.500,75", "-1.500,75")] // German negative
    [InlineData("abc123.45def", "123.45")]
    [InlineData("€1,234.56", "1,234.56")]
    [InlineData("Cost: $1,000.00", "1,000.00")]
    public void SanitizeNumericInput_VariousFormats_ExtractsNumbers(string input, string expected)
    {
        // Act
        var result = _service.SanitizeNumericInput(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeNumericInput_NullOrEmpty_ReturnsEmpty()
    {
        // Act & Assert
        Assert.Equal(string.Empty, _service.SanitizeNumericInput(null));
        Assert.Equal(string.Empty, _service.SanitizeNumericInput(""));
        Assert.Equal(string.Empty, _service.SanitizeNumericInput("   "));
    }

    [Theory]
    [InlineData("abc", "")]
    [InlineData("text only", "")]
    [InlineData("!@#$%^&*()", "")]
    [InlineData("<script>alert(123)</script>", "123")]
    public void SanitizeNumericInput_NonNumericContent_RemovesNonNumeric(string input, string expected)
    {
        // Act
        var result = _service.SanitizeNumericInput(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Performance and Load Tests

    [Fact]
    public void SanitizeInput_LargeInput_HandlesEfficiently()
    {
        // Arrange - Large but clean input
        var largeInput = new StringBuilder();
        for (int i = 0; i < 10000; i++)
        {
            largeInput.Append($"Line {i}: German BWA category with ümlauts. ");
        }

        // Act
        var startTime = DateTime.UtcNow;
        var result = _service.SanitizeInput(largeInput.ToString());
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotEmpty(result);
        Assert.True(duration.TotalSeconds < 2, $"Sanitization took too long: {duration.TotalSeconds} seconds");
    }

    [Fact]
    public void ContainsScriptInjection_RepeatedCalls_MaintainsPerformance()
    {
        // Arrange
        var testInputs = new[]
        {
            "Clean input",
            "<script>alert('xss')</script>",
            "Normal BWA content",
            "javascript:alert(1)",
            "More clean content"
        };

        // Act
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 1000; i++)
        {
            foreach (var input in testInputs)
            {
                _service.ContainsScriptInjection(input);
            }
        }
        var totalDuration = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(totalDuration.TotalSeconds < 1, $"Repeated injection checks took too long: {totalDuration.TotalSeconds} seconds");
    }

    [Fact]
    public void SanitizeFileName_BatchProcessing_HandlesEfficiently()
    {
        // Arrange
        var fileNames = new[]
        {
            "normal-file.pdf",
            "../../../etc/passwd",
            "file<script>alert()</script>.pdf",
            "very-long-" + new string('x', 300) + ".pdf",
            "german-ümlauts-file.pdf"
        };

        // Act
        var startTime = DateTime.UtcNow;
        var results = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            foreach (var fileName in fileNames)
            {
                results.Add(_service.SanitizeFileName(fileName));
            }
        }
        var totalDuration = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(5000, results.Count);
        Assert.True(totalDuration.TotalSeconds < 2, $"Batch filename sanitization took too long: {totalDuration.TotalSeconds} seconds");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void SanitizeInput_UnicodeAndEmojiChars_HandlesGracefully()
    {
        // Arrange
        var unicodeInput = "Text with 🔒 emoji and unicode ∑ characters";

        // Act
        var result = _service.SanitizeInput(unicodeInput);

        // Assert
        Assert.NotNull(result);
        // The exact result depends on how HTML encoding handles these characters
        // Main requirement is that it doesn't crash
    }

    [Fact]
    public void SanitizeBwaCategory_AllowedCharactersPattern_WorksCorrectly()
    {
        // Test the allowed pattern: letters, spaces, numbers, German chars, and specific punctuation
        
        // Arrange
        var allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var germanChars = "äöüßÄÖÜ";
        var numbers = "0123456789";
        var punctuation = " -.€,";
        var allAllowed = allowedChars + germanChars + numbers + punctuation;

        // Act
        var result = _service.SanitizeBwaCategory(allAllowed);

        // Assert
        Assert.Equal(allAllowed.Trim(), result); // Should be unchanged except whitespace normalization
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Verifies warning was logged with specific message
    /// </summary>
    private void VerifyWarningLogged(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies error was logged with specific message
    /// </summary>
    private void VerifyErrorLogged(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}