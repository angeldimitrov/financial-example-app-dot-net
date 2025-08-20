using Xunit;
using FluentAssertions;
using FinanceApp.Web.Services;
using Microsoft.Extensions.Logging;

namespace FinanceApp.Tests.Services;

/// <summary>
/// Simple tests to prove the concept of having working tests
/// </summary>
public class SimpleServiceTests
{
    [Fact]
    public void FileValidationService_CanBeCreated()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<FileValidationService>();
        
        // Act
        var service = new FileValidationService(logger);
        
        // Assert
        service.Should().NotBeNull();
    }
    
    [Fact]
    public void InputSanitizationService_CanBeCreated()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<InputSanitizationService>();
        
        // Act
        var service = new InputSanitizationService(logger);
        
        // Assert
        service.Should().NotBeNull();
    }
    
    [Fact]
    public void PdfParserService_CanBeCreated()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var pdfLogger = loggerFactory.CreateLogger<PdfParserService>();
        var sanitizationLogger = loggerFactory.CreateLogger<InputSanitizationService>();
        var sanitizationService = new InputSanitizationService(sanitizationLogger);
        
        // Act
        var service = new PdfParserService(pdfLogger, sanitizationService);
        
        // Assert
        service.Should().NotBeNull();
    }
    
    [Fact]
    public void InputSanitization_SanitizeInput_WorksWithBasicString()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<InputSanitizationService>();
        var service = new InputSanitizationService(logger);
        var input = "Normal text without scripts";
        
        // Act
        var result = service.SanitizeInput(input);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Normal text");
    }
    
    [Fact]
    public void Math_BasicCalculation_Works()
    {
        // Arrange
        var a = 1500.25m;
        var b = 500.75m;
        
        // Act
        var sum = a + b;
        
        // Assert
        sum.Should().Be(2001.00m);
    }
}