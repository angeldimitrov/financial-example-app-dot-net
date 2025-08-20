using Xunit;
using FluentAssertions;
using FinanceApp.Web.Models;
using System.Globalization;

namespace FinanceApp.Tests;

/// <summary>
/// Simple unit tests written by AI to prove the concept
/// These tests are fast, isolated, and perfect for GitHub Actions
/// </summary>
public class SimpleUnitTests
{
    [Fact]
    public void FinancialPeriod_CanBeCreated_WithValidData()
    {
        // Arrange & Act
        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 3,
            SourceFileName = "test.pdf"
        };
        
        // Assert
        period.Year.Should().Be(2024);
        period.Month.Should().Be(3);
        period.SourceFileName.Should().Be("test.pdf");
    }
    
    [Fact]
    public void TransactionLine_CanBeCreated_WithGermanFinancialData()
    {
        // Arrange & Act
        var transaction = new TransactionLine
        {
            Amount = 1234.56m,
            Category = "Umsatzerlöse",
            Type = TransactionType.Revenue,
            Year = 2024,
            Month = 3
        };
        
        // Assert
        transaction.Amount.Should().Be(1234.56m);
        transaction.Category.Should().Be("Umsatzerlöse");
        transaction.Type.Should().Be(TransactionType.Revenue);
        transaction.Amount.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void TransactionType_Enum_HasAllExpectedValues()
    {
        // Assert - verify all BWA transaction types exist
        Enum.IsDefined(typeof(TransactionType), TransactionType.Revenue).Should().BeTrue();
        Enum.IsDefined(typeof(TransactionType), TransactionType.Expense).Should().BeTrue();
        Enum.IsDefined(typeof(TransactionType), TransactionType.Summary).Should().BeTrue();
        Enum.IsDefined(typeof(TransactionType), TransactionType.Other).Should().BeTrue();
    }
    
    [Fact]
    public void GermanCulture_NumberFormatting_WorksCorrectly()
    {
        // Arrange
        var germanCulture = new CultureInfo("de-DE");
        var testNumber = 1234.56m;
        
        // Act
        var formattedNumber = testNumber.ToString("N2", germanCulture);
        
        // Assert - German formatting uses comma as decimal separator
        formattedNumber.Should().Contain(",");
        formattedNumber.Should().Be("1.234,56");
    }
    
    [Fact]
    public void BasicMath_ForFinancialCalculations_WorksCorrectly()
    {
        // Arrange - typical BWA amounts
        var revenue = 50000.00m;
        var expenses = 35000.00m;
        
        // Act
        var profit = revenue - expenses;
        var profitMargin = (profit / revenue) * 100;
        
        // Assert
        profit.Should().Be(15000.00m);
        profitMargin.Should().Be(30.00m);
    }
}