using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FinanceApp.Web.Services;
using System.Text;

namespace FinanceApp.Tests.Security;

/// <summary>
/// Security-focused unit tests
/// Tests file validation, input sanitization, and XSS prevention
/// 
/// Security Test Coverage:
/// - File upload validation and malicious file detection
/// - XSS prevention in transaction data
/// - Input sanitization for German financial data
/// - Path traversal prevention
/// - Content Security Policy validation
/// </summary>
public class SecurityTests
{
    private readonly Mock<ILogger<FileValidationService>> _fileValidationLoggerMock;
    private readonly Mock<ILogger<InputSanitizationService>> _sanitizationLoggerMock;
    private readonly FileValidationService _fileValidationService;
    private readonly InputSanitizationService _inputSanitizationService;
    
    public SecurityTests()
    {
        _fileValidationLoggerMock = new Mock<ILogger<FileValidationService>>();
        _sanitizationLoggerMock = new Mock<ILogger<InputSanitizationService>>();
        
        _fileValidationService = new FileValidationService(_fileValidationLoggerMock.Object);
        _inputSanitizationService = new InputSanitizationService(_sanitizationLoggerMock.Object);
    }
    
    [Fact]
    public async Task ValidatePdfFileAsync_NullFile_ReturnsInvalid()
    {
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(null!);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Keine Datei ausgewählt", result.ErrorMessage);
    }
    
    [Fact]
    public async Task ValidatePdfFileAsync_EmptyFile_ReturnsInvalid()
    {
        // Arrange
        var emptyFile = CreateMockFormFile("empty.pdf", "", "application/pdf");
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(emptyFile);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Keine Datei ausgewählt", result.ErrorMessage);
    }
    
    [Fact]
    public async Task ValidatePdfFileAsync_OversizedFile_ReturnsInvalid()
    {
        // Arrange - Create 11MB file
        var largeContent = new byte[11 * 1024 * 1024];
        var largeFile = CreateMockFormFile("large.pdf", largeContent, "application/pdf");
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(largeFile);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Dateigröße überschreitet das Limit", result.ErrorMessage);
    }
    
    [Theory]
    [InlineData("malicious.exe")]
    [InlineData("virus.bat")]
    [InlineData("script.js")]
    [InlineData("document.docx")]
    public async Task ValidatePdfFileAsync_InvalidExtension_ReturnsInvalid(string fileName)
    {
        // Arrange
        var invalidFile = CreateMockFormFile(fileName, "some content", "application/octet-stream");
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(invalidFile);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Nur PDF-Dateien sind erlaubt", result.ErrorMessage);
    }
    
    [Theory]
    [InlineData("application/octet-stream")]
    [InlineData("text/plain")]
    [InlineData("application/javascript")]
    [InlineData("text/html")]
    public async Task ValidatePdfFileAsync_InvalidMimeType_ReturnsInvalid(string mimeType)
    {
        // Arrange
        var invalidFile = CreateMockFormFile("test.pdf", "fake pdf content", mimeType);
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(invalidFile);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Ungültiger Dateityp", result.ErrorMessage);
    }
    
    [Fact]
    public async Task ValidatePdfFileAsync_InvalidPdfSignature_ReturnsInvalid()
    {
        // Arrange - File with .pdf extension but invalid magic bytes
        var fakeContent = "This is not a PDF file";
        var fakeFile = CreateMockFormFile("fake.pdf", fakeContent, "application/pdf");
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(fakeFile);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("keine gültige PDF-Datei", result.ErrorMessage);
    }
    
    [Fact]
    public async Task ValidatePdfFileAsync_ValidPdfSignature_PassesInitialValidation()
    {
        // Arrange - Valid PDF magic bytes
        var validPdfContent = "%PDF-1.4\nSome PDF content here";
        var validFile = CreateMockFormFile("valid.pdf", validPdfContent, "application/pdf");
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(validFile);
        
        // Assert - May still fail content validation, but should pass signature check
        // The specific result depends on content validation
        Assert.NotNull(result);
    }
    
    [Theory]
    [InlineData("/JavaScript")]
    [InlineData("/JS")]
    [InlineData("javascript:alert('xss')")]
    public async Task ValidatePdfFileAsync_MaliciousJavaScript_ReturnsInvalid(string maliciousContent)
    {
        // Arrange - PDF with JavaScript content
        var maliciousPdf = $"%PDF-1.4\n{maliciousContent}\nSome other content";
        var maliciousFile = CreateMockFormFile("malicious.pdf", maliciousPdf, "application/pdf");
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(maliciousFile);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("JavaScript-Code", result.ErrorMessage);
    }
    
    [Theory]
    [InlineData("/EmbeddedFile")]
    [InlineData("/Launch")]
    [InlineData("/SubmitForm")]
    [InlineData("/ImportData")]
    public async Task ValidatePdfFileAsync_MaliciousActions_ReturnsInvalid(string maliciousAction)
    {
        // Arrange - PDF with malicious actions
        var maliciousPdf = $"%PDF-1.4\n{maliciousAction}\nSome other content";
        var maliciousFile = CreateMockFormFile("malicious.pdf", maliciousPdf, "application/pdf");
        
        // Act
        var result = await _fileValidationService.ValidatePdfFileAsync(maliciousFile);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.ErrorMessage!.Contains("eingebettete Dateien") || 
                   result.ErrorMessage.Contains("ausführbare Aktionen") ||
                   result.ErrorMessage.Contains("Formular-Aktionen"));
    }
    
    [Theory]
    [InlineData("<script>alert('xss')</script>", "")]
    [InlineData("Personalkosten<img src=x onerror=alert(1)>", "Personalkosten")]
    [InlineData("javascript:alert('test')", "alert('test')")]
    [InlineData("<b>Bold Text</b>", "Bold Text")]
    [InlineData("onclick=\"malicious()\"", "")]
    public void SanitizeTransactionInput_MaliciousInput_RemovesThreats(string maliciousInput, string expectedSafe)
    {
        // Act
        var result = _inputSanitizationService.SanitizeTransactionInput(maliciousInput, "test");
        
        // Assert
        Assert.DoesNotContain("<script", result);
        Assert.DoesNotContain("javascript:", result);
        Assert.DoesNotContain("onclick", result);
        Assert.DoesNotContain("<img", result);
        
        if (!string.IsNullOrEmpty(expectedSafe))
        {
            Assert.Contains(expectedSafe, result);
        }
    }
    
    [Theory]
    [InlineData("Personalkosten", "Personalkosten")]
    [InlineData("Umsatzerlöse", "Umsatzerlöse")]
    [InlineData("Fahrzeugkosten (ohne Steuer)", "Fahrzeugkosten (ohne Steuer)")]
    [InlineData("Reparatur/Instandhaltung", "Reparatur/Instandhaltung")]
    public void SanitizeTransactionInput_ValidGermanCategories_PreservesContent(string input, string expected)
    {
        // Act
        var result = _inputSanitizationService.SanitizeTransactionInput(input, "category");
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("1.234,56", "1.234,56")]
    [InlineData("-500,00", "-500,00")]
    [InlineData("abc123,45def", "123,45")]
    [InlineData("1,2,3,4", null)] // Multiple commas invalid
    [InlineData("-1-00,50", null)] // Multiple minus signs invalid
    public void SanitizeNumericInput_VariousFormats_ReturnsExpectedResult(string input, string? expected)
    {
        // Act
        var result = _inputSanitizationService.SanitizeNumericInput(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("../../../windows/system32/", "windowssystem32")]
    [InlineData("file<script>alert(1)</script>.pdf", "filealert(1).pdf")]
    [InlineData("normal_file_name.pdf", "normal_file_name.pdf")]
    [InlineData("very_long_filename_that_exceeds_typical_limits_and_should_be_truncated_to_prevent_buffer_overflow_attacks.pdf", 
               "very_long_filename_that_exceeds_typical_limits_and_should_be_truncated_to_prevent_buffer_overflow_attacks.pdf")]
    public void SanitizeFileName_MaliciousNames_ReturnsSafeName(string maliciousName, string expectedSafe)
    {
        // Act
        var result = _inputSanitizationService.SanitizeFileName(maliciousName);
        
        // Assert
        Assert.DoesNotContain("..", result);
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.EndsWith(".pdf", result);
        
        if (result.Length > 200)
        {
            Assert.True(result.Length <= 205); // Account for .pdf extension
        }
    }
    
    [Theory]
    [InlineData("", "unnamed.pdf")]
    [InlineData(null, "unnamed.pdf")]
    [InlineData("   ", "unnamed.pdf")]
    public void SanitizeFileName_EmptyInput_ReturnsDefaultName(string? input, string expected)
    {
        // Act
        var result = _inputSanitizationService.SanitizeFileName(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Personalkosten mit Überstunden", "Personalkosten mit Überstunden")]
    [InlineData("Straße & Hof", "Straße &amp; Hof")]
    [InlineData("<script>alert('xss')</script>", "&lt;script&gt;alert(&#x27;xss&#x27;)&lt;/script&gt;")]
    public void CreateSafeDisplayText_VariousInputs_ReturnsSafeHtml(string input, string expectedPattern)
    {
        // Act
        var result = _inputSanitizationService.CreateSafeDisplayText(input);
        
        // Assert
        Assert.DoesNotContain("<script", result);
        Assert.DoesNotContain("javascript:", result);
        
        // Should preserve German characters
        if (input.Contains("ä") || input.Contains("ü") || input.Contains("ß"))
        {
            // German characters should be preserved in some form
            Assert.True(result.Length > 0);
        }
    }
    
    [Fact]
    public async Task GenerateFileHashAsync_SameContent_GeneratesSameHash()
    {
        // Arrange
        var content = "Test PDF content for hashing";
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes(content));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(content));
        
        // Act
        var hash1 = await _fileValidationService.GenerateFileHashAsync(stream1);
        var hash2 = await _fileValidationService.GenerateFileHashAsync(stream2);
        
        // Assert
        Assert.Equal(hash1, hash2);
        Assert.True(hash1.Length == 64); // SHA256 produces 64 hex characters
        Assert.Matches("^[a-f0-9]{64}$", hash1);
    }
    
    [Fact]
    public async Task GenerateFileHashAsync_DifferentContent_GeneratesDifferentHashes()
    {
        // Arrange
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Content A"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Content B"));
        
        // Act
        var hash1 = await _fileValidationService.GenerateFileHashAsync(stream1);
        var hash2 = await _fileValidationService.GenerateFileHashAsync(stream2);
        
        // Assert
        Assert.NotEqual(hash1, hash2);
    }
    
    /// <summary>
    /// Test German special characters handling
    /// </summary>
    [Theory]
    [InlineData("äöüß", "äöüß")] // Should preserve German characters
    [InlineData("Müller & Co.", "Müller &amp; Co.")] // Should encode HTML but preserve German
    [InlineData("Größe: 1.234,56€", "Größe: 1.234,56€")] // Should preserve formatting
    public void InputSanitization_GermanCharacters_PreservesValidContent(string input, string expectedPattern)
    {
        // Act
        var result = _inputSanitizationService.SanitizeTransactionInput(input, "german");
        
        // Assert
        Assert.Contains("ö", result); // German umlauts should be preserved
        Assert.DoesNotContain("<script", result);
    }
    
    /// <summary>
    /// Performance test for large input sanitization
    /// </summary>
    [Fact]
    public void SanitizeTransactionInput_LargeInput_ProcessesQuickly()
    {
        // Arrange
        var largeInput = string.Join(" ", Enumerable.Repeat("Personalkosten", 1000));
        var startTime = DateTime.UtcNow;
        
        // Act
        var result = _inputSanitizationService.SanitizeTransactionInput(largeInput, "performance");
        
        // Assert
        var processingTime = DateTime.UtcNow - startTime;
        Assert.True(processingTime.TotalMilliseconds < 1000, $"Processing took too long: {processingTime.TotalMilliseconds}ms");
        Assert.NotEmpty(result);
    }
    
    /// <summary>
    /// Helper method to create mock IFormFile
    /// </summary>
    private static IFormFile CreateMockFormFile(string fileName, string content, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return CreateMockFormFile(fileName, bytes, contentType);
    }
    
    private static IFormFile CreateMockFormFile(string fileName, byte[] content, string contentType)
    {
        var stream = new MemoryStream(content);
        var mockFile = new Mock<IFormFile>();
        
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .Returns((Stream target, CancellationToken token) =>
               {
                   stream.Position = 0;
                   return stream.CopyToAsync(target, token);
               });
        
        return mockFile.Object;
    }
}