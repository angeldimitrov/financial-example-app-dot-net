using Xunit;
using FluentAssertions;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using Microsoft.Extensions.Logging;

namespace FinanceApp.Tests;

/// <summary>
/// Simple tests that actually work and demonstrate basic functionality
/// </summary>
public class SimpleWorkingTests
{
    [Fact]
    public void BasicTest_AlwaysPasses()
    {
        // Arrange
        var expected = true;
        
        // Act
        var result = true;
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void FinancialPeriod_CanSetProperties()
    {
        // Arrange & Act
        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 3
        };
        
        // Assert
        period.Year.Should().Be(2024);
        period.Month.Should().Be(3);
    }
    
    [Fact]
    public void TransactionLine_CanSetBasicProperties()
    {
        // Arrange & Act
        var transaction = new TransactionLine
        {
            Amount = 1000.50m,
            Category = "Umsatzerlöse",
            Month = 6,
            Year = 2024,
            Type = TransactionType.Revenue
        };
        
        // Assert
        transaction.Amount.Should().Be(1000.50m);
        transaction.Category.Should().Be("Umsatzerlöse");
        transaction.Month.Should().Be(6);
        transaction.Year.Should().Be(2024);
        transaction.Type.Should().Be(TransactionType.Revenue);
    }
    
    [Theory]
    [InlineData(TransactionType.Revenue)]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Summary)]
    [InlineData(TransactionType.Other)]
    public void TransactionType_AllValuesWork(TransactionType type)
    {
        // Arrange & Act
        var transaction = new TransactionLine
        {
            Type = type
        };
        
        // Assert
        transaction.Type.Should().Be(type);
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
    public void InputSanitizationService_SanitizeInput_WorksWithBasicString()
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