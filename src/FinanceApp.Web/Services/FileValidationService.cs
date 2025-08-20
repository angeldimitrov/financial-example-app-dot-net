using System.Text;
using System.Text.RegularExpressions;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for comprehensive server-side file validation with focus on PDF security
/// 
/// Security Features:
/// - Magic byte verification to prevent file extension spoofing
/// - Malicious content detection (JavaScript, embedded files, launch actions)
/// - Size limits and MIME type validation
/// - Path traversal prevention
/// 
/// German BWA Context:
/// Validates uploaded PDF files containing German Jahresübersicht/BWA reports
/// ensuring they are safe for processing by PdfParserService
/// </summary>
public interface IFileValidationService
{
    Task<ValidationResult> ValidateUploadedFileAsync(IFormFile file);
    bool IsValidPdfMagicBytes(byte[] fileBytes);
    bool ContainsMaliciousContent(byte[] fileBytes);
}

public class FileValidationService : IFileValidationService
{
    // PDF magic bytes for validation - prevents extension spoofing attacks
    private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    
    // Maximum file size: 50MB (typical German BWA reports are 1-5MB)
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;
    
    // Malicious content patterns - compiled for performance
    private static readonly Regex JavaScriptPattern = new(@"/JavaScript\s*\(|/JS\s*\(", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex LaunchActionPattern = new(@"/Launch\s*<<|/Action\s*<<", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex EmbeddedFilePattern = new(@"/EmbeddedFile|/FileAttachment", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private readonly ILogger<FileValidationService> _logger;

    public FileValidationService(ILogger<FileValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Comprehensive validation of uploaded PDF files
    /// Performs security checks before allowing PDF processing
    /// </summary>
    public async Task<ValidationResult> ValidateUploadedFileAsync(IFormFile file)
    {
        try
        {
            // Basic file checks
            if (file == null || file.Length == 0)
            {
                return new ValidationResult(false, "Keine Datei ausgewählt oder Datei ist leer.");
            }

            // File size validation
            if (file.Length > MaxFileSizeBytes)
            {
                return new ValidationResult(false, 
                    $"Datei zu groß. Maximum: {MaxFileSizeBytes / (1024 * 1024)} MB");
            }

            // MIME type validation
            if (!IsValidMimeType(file.ContentType))
            {
                return new ValidationResult(false, 
                    "Ungültiger Dateityp. Nur PDF-Dateien sind erlaubt.");
            }

            // File extension validation
            if (!HasValidPdfExtension(file.FileName))
            {
                return new ValidationResult(false, 
                    "Ungültige Dateiendung. Nur .pdf Dateien sind erlaubt.");
            }

            // Path traversal prevention
            if (ContainsPathTraversalAttempt(file.FileName))
            {
                _logger.LogWarning("Path traversal attempt detected: {FileName}", file.FileName);
                return new ValidationResult(false, 
                    "Ungültiger Dateiname erkannt.");
            }

            // Read file content for magic byte and malicious content validation
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Magic byte validation - ensures file is actually a PDF
            if (!IsValidPdfMagicBytes(fileBytes))
            {
                return new ValidationResult(false, 
                    "Datei ist keine gültige PDF-Datei.");
            }

            // Malicious content detection
            if (ContainsMaliciousContent(fileBytes))
            {
                _logger.LogWarning("Malicious content detected in file: {FileName}", file.FileName);
                return new ValidationResult(false, 
                    "PDF enthält potenziell schädlichen Inhalt.");
            }

            _logger.LogInformation("File validation successful: {FileName}, Size: {Size} bytes", 
                file.FileName, file.Length);

            return new ValidationResult(true, "Datei erfolgreich validiert.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file: {FileName}", file.FileName);
            return new ValidationResult(false, 
                "Fehler bei der Dateivalidierung. Bitte versuchen Sie es erneut.");
        }
    }

    /// <summary>
    /// Validates PDF magic bytes to prevent file extension spoofing
    /// Checks first 4 bytes for PDF signature (%PDF)
    /// </summary>
    public bool IsValidPdfMagicBytes(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length < 4)
            return false;

        // Check for PDF magic bytes at the beginning of file
        for (int i = 0; i < PdfMagicBytes.Length; i++)
        {
            if (fileBytes[i] != PdfMagicBytes[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Scans PDF content for malicious elements
    /// Detects JavaScript, launch actions, and embedded files
    /// </summary>
    public bool ContainsMaliciousContent(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length == 0)
            return false;

        try
        {
            // Convert to string for pattern matching
            var content = Encoding.ASCII.GetString(fileBytes);

            // Check for JavaScript execution
            if (JavaScriptPattern.IsMatch(content))
            {
                _logger.LogWarning("JavaScript content detected in PDF");
                return true;
            }

            // Check for launch actions (can execute external programs)
            if (LaunchActionPattern.IsMatch(content))
            {
                _logger.LogWarning("Launch action detected in PDF");
                return true;
            }

            // Check for embedded files (potential malware carrier)
            if (EmbeddedFilePattern.IsMatch(content))
            {
                _logger.LogWarning("Embedded file detected in PDF");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning PDF content for malicious patterns");
            // Fail secure - treat as malicious if we can't scan properly
            return true;
        }
    }

    private static bool IsValidMimeType(string contentType)
    {
        var validMimeTypes = new[] { "application/pdf", "application/x-pdf" };
        return !string.IsNullOrEmpty(contentType) && 
               validMimeTypes.Contains(contentType.ToLowerInvariant());
    }

    private static bool HasValidPdfExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        return Path.GetExtension(fileName).ToLowerInvariant() == ".pdf";
    }

    private static bool ContainsPathTraversalAttempt(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        // Check for common path traversal patterns
        var pathTraversalPatterns = new[] { "..", "\\", "/", ":", "*", "?", "\"", "<", ">", "|" };
        return pathTraversalPatterns.Any(pattern => fileName.Contains(pattern));
    }
}

/// <summary>
/// Result of file validation process
/// Contains success status and user-friendly German error messages
/// </summary>
public record ValidationResult(bool IsValid, string Message);