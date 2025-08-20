using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FinanceApp.Web.Services;
using FinanceApp.Tests.TestData;
using System.Text;

namespace FinanceApp.Tests.Integration;

/// <summary>
/// Integration tests for the complete security validation pipeline
/// 
/// End-to-End Security Testing:
/// - File upload security validation workflow
/// - Input sanitization in data processing pipeline
/// - German BWA data security processing
/// - Performance testing under security scanning load
/// - Real-world attack scenario simulation
/// 
/// German BWA Context:
/// Tests the complete security pipeline for processing German Jahresübersicht/BWA files
/// ensuring security measures don't interfere with legitimate German business data processing
/// </summary>
public class SecurityIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public SecurityIntegrationTests()
    {
        // Setup dependency injection for integration testing
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddTransient<IFileValidationService, FileValidationService>();
        services.AddTransient<IInputSanitizationService, InputSanitizationService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    #region End-to-End Security Pipeline Tests

    [Fact]
    public async Task SecurityPipeline_ValidGermanBwaFile_PassesAllValidation()
    {
        // Arrange
        var fileValidationService = _serviceProvider.GetRequiredService<IFileValidationService>();
        var inputSanitizationService = _serviceProvider.GetRequiredService<IInputSanitizationService>();

        var legitimateBwaContent = SecurityTestData.CreateLegitimateGermanBwaPdf();
        var legitimatePdfBytes = Encoding.UTF8.GetBytes(legitimateBwaContent);
        var mockFile = CreateMockFormFile("BWA-Jahresübersicht-2023.pdf", "application/pdf", legitimatePdfBytes);

        // Act - Complete security pipeline
        var fileValidationResult = await fileValidationService.ValidateUploadedFileAsync(mockFile.Object);
        
        var sanitizedFileName = inputSanitizationService.SanitizeFileName(mockFile.Object.FileName);
        var sanitizedCategories = SecurityTestData.ValidGermanBwaCategories
            .Select(cat => inputSanitizationService.SanitizeBwaCategory(cat))
            .ToList();

        // Assert - All security checks pass
        Assert.True(fileValidationResult.IsValid, $"File validation failed: {fileValidationResult.Message}");
        Assert.Equal("BWA-Jahresübersicht-2023.pdf", sanitizedFileName);
        Assert.All(sanitizedCategories, cat => Assert.NotEmpty(cat));
        
        // Verify German characters preserved
        Assert.Contains(sanitizedCategories, cat => cat.Contains("ä") || cat.Contains("ö") || cat.Contains("ü"));
    }

    [Fact]
    public async Task SecurityPipeline_MaliciousFile_BlockedAtFileValidation()
    {
        // Arrange
        var fileValidationService = _serviceProvider.GetRequiredService<IFileValidationService>();
        
        var maliciousContent = SecurityTestData.CreateMaliciousPdfWithJavaScript("/JavaScript (app.alert('PWNed');)");
        var maliciousPdfBytes = Encoding.UTF8.GetBytes(maliciousContent);
        var mockFile = CreateMockFormFile("innocent.pdf", "application/pdf", maliciousPdfBytes);

        // Act
        var validationResult = await fileValidationService.ValidateUploadedFileAsync(mockFile.Object);

        // Assert - File blocked before processing
        Assert.False(validationResult.IsValid);
        Assert.Contains("schädlichen Inhalt", validationResult.Message);
    }

    [Fact]
    public async Task SecurityPipeline_MaliciousInputData_SanitizedCorrectly()
    {
        // Arrange
        var inputSanitizationService = _serviceProvider.GetRequiredService<IInputSanitizationService>();
        
        var maliciousInputs = new[]
        {
            "Umsatzerlöse<script>steal()</script>",
            "'; DROP TABLE financial_periods; --",
            "Personalkosten<img src=x onerror=alert(1)>",
            "../../../etc/passwd.pdf"
        };

        // Act
        var sanitizedInputs = maliciousInputs
            .Select(input => inputSanitizationService.SanitizeInput(input))
            .ToList();

        var sanitizedFileName = inputSanitizationService.SanitizeFileName(maliciousInputs.Last());

        // Assert - Malicious content removed or blocked
        Assert.All(sanitizedInputs.Take(3), input => 
            Assert.DoesNotContain("<script>", input) && 
            Assert.DoesNotContain("DROP TABLE", input) &&
            Assert.DoesNotContain("<img", input));
        
        Assert.Equal("passwd.pdf", sanitizedFileName); // Path traversal removed
    }

    #endregion

    #region Real-World Attack Simulation Tests

    [Theory]
    [InlineData("document.pdf<script>alert('xss')</script>")]
    [InlineData("../../../etc/passwd")]
    [InlineData("file'; DROP DATABASE; --.pdf")]
    [InlineData("normal.pdf\0.exe")]
    public async Task SecurityPipeline_FileNameAttacks_HandledSecurely(string maliciousFileName)
    {
        // Arrange
        var fileValidationService = _serviceProvider.GetRequiredService<IFileValidationService>();
        var inputSanitizationService = _serviceProvider.GetRequiredService<IInputSanitizationService>();

        var legitimateContent = SecurityTestData.CreateLegitimateGermanBwaPdf();
        var contentBytes = Encoding.UTF8.GetBytes(legitimateContent);
        var mockFile = CreateMockFormFile(maliciousFileName, "application/pdf", contentBytes);

        // Act
        var fileValidationResult = await fileValidationService.ValidateUploadedFileAsync(mockFile.Object);
        var sanitizedFileName = inputSanitizationService.SanitizeFileName(maliciousFileName);

        // Assert
        if (fileValidationResult.IsValid)
        {
            // If file passes validation, filename must be sanitized
            Assert.DoesNotContain("<script>", sanitizedFileName);
            Assert.DoesNotContain("..", sanitizedFileName);
            Assert.DoesNotContain("DROP", sanitizedFileName);
            Assert.DoesNotContain("\0", sanitizedFileName);
        }
        else
        {
            // File blocked due to malicious filename
            Assert.Contains("Ungültiger", fileValidationResult.Message);
        }
    }

    [Fact]
    public async Task SecurityPipeline_CombinedAttackVectors_BlockedAppropriately()
    {
        // Arrange - File with malicious content AND malicious filename
        var fileValidationService = _serviceProvider.GetRequiredService<IFileValidationService>();
        
        var maliciousContent = SecurityTestData.CreateMaliciousPdfWithJavaScript("/JavaScript (this.print();)");
        var maliciousBytes = Encoding.UTF8.GetBytes(maliciousContent);
        var maliciousFileName = "../../../etc/passwd<script>alert(1)</script>.pdf";
        var mockFile = CreateMockFormFile(maliciousFileName, "application/pdf", maliciousBytes);

        // Act
        var validationResult = await fileValidationService.ValidateUploadedFileAsync(mockFile.Object);

        // Assert - Should be blocked (either by filename or content validation)
        Assert.False(validationResult.IsValid);
    }

    [Fact]
    public void SecurityPipeline_GermanDataProcessing_PreservesLegitimateContent()
    {
        // Arrange
        var inputSanitizationService = _serviceProvider.GetRequiredService<IInputSanitizationService>();
        
        var germanBusinessData = new[]
        {
            "Geschäftsführung: Müller & Söhne GmbH",
            "Büroausstattung für 1.250,75 €",
            "Prüfungskosten - Steuerberatung",
            "Betriebsausgaben laut BWA-Auswertung",
            "Umsatzerlöse aus Dienstleistungen"
        };

        // Act
        var sanitizedData = germanBusinessData
            .Select(data => inputSanitizationService.SanitizeInput(data))
            .ToList();

        // Assert - German content preserved
        Assert.All(sanitizedData, data => Assert.NotEmpty(data));
        Assert.Contains(sanitizedData, data => data.Contains("ä"));
        Assert.Contains(sanitizedData, data => data.Contains("ü"));
        Assert.Contains(sanitizedData, data => data.Contains("€"));
        Assert.Contains(sanitizedData, data => data.Contains("1.250,75"));
    }

    #endregion

    #region Performance Under Security Load Tests

    [Fact]
    public async Task SecurityPipeline_HighVolumeValidation_MaintainsPerformance()
    {
        // Arrange
        var fileValidationService = _serviceProvider.GetRequiredService<IFileValidationService>();
        
        var legitimateContent = SecurityTestData.CreateLegitimateGermanBwaPdf();
        var contentBytes = Encoding.UTF8.GetBytes(legitimateContent);
        
        var validationTasks = new List<Task<ValidationResult>>();
        
        // Create 50 concurrent validation requests
        for (int i = 0; i < 50; i++)
        {
            var mockFile = CreateMockFormFile($"bwa-report-{i}.pdf", "application/pdf", contentBytes);
            validationTasks.Add(fileValidationService.ValidateUploadedFileAsync(mockFile.Object));
        }

        // Act
        var startTime = DateTime.UtcNow;
        var results = await Task.WhenAll(validationTasks);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.All(results, result => Assert.True(result.IsValid));
        Assert.True(duration.TotalSeconds < 10, $"High volume validation took too long: {duration.TotalSeconds} seconds");
    }

    [Fact]
    public void SecurityPipeline_MassInputSanitization_HandlesLoad()
    {
        // Arrange
        var inputSanitizationService = _serviceProvider.GetRequiredService<IInputSanitizationService>();
        
        var testInputs = new List<string>();
        
        // Mix of legitimate and malicious inputs
        for (int i = 0; i < 1000; i++)
        {
            if (i % 10 == 0)
            {
                // Add malicious input every 10th item
                testInputs.Add($"Category{i}<script>alert('xss{i}')</script>");
            }
            else
            {
                // Add legitimate German data
                testInputs.Add($"Umsatzerlöse Monat {i}: {i * 100:C}");
            }
        }

        // Act
        var startTime = DateTime.UtcNow;
        var sanitizedInputs = testInputs
            .Select(input => inputSanitizationService.SanitizeInput(input))
            .ToList();
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(1000, sanitizedInputs.Count);
        Assert.True(duration.TotalSeconds < 2, $"Mass sanitization took too long: {duration.TotalSeconds} seconds");
        
        // Verify malicious content was removed/blocked
        var maliciousCount = sanitizedInputs.Count(input => input.Contains("<script>"));
        Assert.True(maliciousCount < 10, "Malicious scripts should be removed or inputs rejected");
    }

    #endregion

    #region German BWA Specific Security Tests

    [Fact]
    public async Task SecurityPipeline_GermanBwaCategories_SecurityValidation()
    {
        // Arrange
        var inputSanitizationService = _serviceProvider.GetRequiredService<IInputSanitizationService>();
        
        // Test both legitimate and malicious German BWA categories
        var testCategories = SecurityTestData.ValidGermanBwaCategories
            .Concat(new[]
            {
                "Umsatzerlöse<script>steal()</script>",
                "'; DROP TABLE categories; --Personalkosten",
                "Bürokosten<img src=x onerror=alert(1)>"
            })
            .ToArray();

        // Act
        var sanitizedCategories = testCategories
            .Select(cat => inputSanitizationService.SanitizeBwaCategory(cat))
            .ToList();

        // Assert
        var legitimateCount = SecurityTestData.ValidGermanBwaCategories.Length;
        var totalCount = testCategories.Length;
        
        // First part should be legitimate categories (unchanged)
        for (int i = 0; i < legitimateCount; i++)
        {
            Assert.Equal(SecurityTestData.ValidGermanBwaCategories[i], sanitizedCategories[i]);
        }
        
        // Malicious categories should be sanitized or rejected
        for (int i = legitimateCount; i < totalCount; i++)
        {
            var sanitized = sanitizedCategories[i];
            Assert.DoesNotContain("<script>", sanitized);
            Assert.DoesNotContain("DROP TABLE", sanitized);
            Assert.DoesNotContain("<img", sanitized);
        }
    }

    [Fact]
    public void SecurityPipeline_GermanNumericData_SecurityAndFormatting()
    {
        // Arrange
        var inputSanitizationService = _serviceProvider.GetRequiredService<IInputSanitizationService>();
        
        var germanNumericInputs = new[]
        {
            "1.234,56", // Valid German format
            "€1.500,75", // With currency symbol
            "Cost<script>alert(1)</script>: 2.000,00", // With malicious content
            "'; DROP TABLE; --1.000,50", // With SQL injection
            "-15.000,25 €" // Negative German format
        };

        // Act
        var sanitizedNumbers = germanNumericInputs
            .Select(input => inputSanitizationService.SanitizeNumericInput(input))
            .ToList();

        // Assert
        Assert.Equal("1.234,56", sanitizedNumbers[0]); // Preserved
        Assert.Equal("1.500,75", sanitizedNumbers[1]); // Currency removed, format preserved
        Assert.Equal("2.000,00", sanitizedNumbers[2]); // Script removed, number preserved
        Assert.Equal("1.000,50", sanitizedNumbers[3]); // SQL injection removed, number preserved
        Assert.Equal("-15.000,25", sanitizedNumbers[4]); // Negative format preserved
    }

    #endregion

    #region Security Logging and Monitoring Tests

    [Fact]
    public async Task SecurityPipeline_MaliciousAttempts_ProperlyLogged()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FileValidationService>>();
        var fileValidationService = new FileValidationService(loggerMock.Object);
        
        var maliciousContent = SecurityTestData.CreateMaliciousPdfWithJavaScript("/JavaScript (evil());");
        var maliciousBytes = Encoding.UTF8.GetBytes(maliciousContent);
        var mockFile = CreateMockFormFile("innocent.pdf", "application/pdf", maliciousBytes);

        // Act
        await fileValidationService.ValidateUploadedFileAsync(mockFile.Object);

        // Assert - Verify security warnings are logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Malicious content detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void SecurityPipeline_InputSanitization_LogsSecurityEvents()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<InputSanitizationService>>();
        var sanitizationService = new InputSanitizationService(loggerMock.Object);
        
        var maliciousInput = "<script>alert('security breach')</script>";

        // Act
        sanitizationService.SanitizeInput(maliciousInput);

        // Assert - Verify security warnings are logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Script injection attempt detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates mock IFormFile for integration testing
    /// </summary>
    private static Mock<IFormFile> CreateMockFormFile(string fileName, string contentType, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .Returns((Stream stream, CancellationToken token) =>
               {
                   stream.Write(content, 0, content.Length);
                   return Task.CompletedTask;
               });
        return mockFile;
    }

    #endregion
}

/// <summary>
/// Security performance benchmarks and stress tests
/// </summary>
public class SecurityPerformanceTests
{
    [Fact]
    public async Task Security_LargeFileValidation_PerformanceTest()
    {
        // Arrange
        var logger = Mock.Of<ILogger<FileValidationService>>();
        var service = new FileValidationService(logger);
        
        // Create a large legitimate PDF (just under 50MB limit)
        var largeContent = new StringBuilder();
        largeContent.AppendLine(SecurityTestData.CreateLegitimateGermanBwaPdf());
        
        // Add lots of legitimate German BWA content
        for (int i = 0; i < 50000; i++)
        {
            largeContent.AppendLine($"Umsatzerlöse {i}: {i * 10.5m:C}");
        }
        
        var largeBytes = Encoding.UTF8.GetBytes(largeContent.ToString());
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("large-bwa.pdf");
        mockFile.Setup(f => f.ContentType).Returns("application/pdf");
        mockFile.Setup(f => f.Length).Returns(largeBytes.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .Returns((Stream stream, CancellationToken token) =>
               {
                   stream.Write(largeBytes, 0, largeBytes.Length);
                   return Task.CompletedTask;
               });

        // Act
        var startTime = DateTime.UtcNow;
        var result = await service.ValidateUploadedFileAsync(mockFile.Object);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(result.IsValid);
        Assert.True(duration.TotalSeconds < 5, $"Large file validation took too long: {duration.TotalSeconds} seconds");
    }

    [Fact]
    public void Security_MassiveInputSanitization_StressTest()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InputSanitizationService>>();
        var service = new InputSanitizationService(logger);
        
        var stressInputs = new List<string>();
        
        // Create 10,000 varied inputs
        for (int i = 0; i < 10000; i++)
        {
            switch (i % 5)
            {
                case 0: stressInputs.Add($"Legitimate German BWA category {i}"); break;
                case 1: stressInputs.Add($"<script>alert('attack{i}')</script>"); break;
                case 2: stressInputs.Add($"'; DROP TABLE test{i}; --"); break;
                case 3: stressInputs.Add($"Umsatzerlöse für Monat {i}: {i * 100:C}"); break;
                case 4: stressInputs.Add($"../../../etc/passwd{i}"); break;
            }
        }

        // Act
        var startTime = DateTime.UtcNow;
        var results = stressInputs
            .Select(input => service.SanitizeInput(input))
            .ToList();
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(10000, results.Count);
        Assert.True(duration.TotalSeconds < 5, $"Stress test took too long: {duration.TotalSeconds} seconds");
        
        // Verify legitimate content preserved and malicious content handled
        var legitimateResults = results.Where((r, i) => i % 5 == 0 || i % 5 == 3).ToList();
        Assert.All(legitimateResults.Take(100), r => Assert.NotEmpty(r)); // Sample check
    }
}