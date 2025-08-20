using System.Globalization;
using System.Text;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.Tests;

/**
 * Unit tests for CsvExportService
 * 
 * Test Coverage:
 * - German number formatting (1.234,56)
 * - CSV generation with proper headers and delimiters
 * - Empty data handling
 * - Filename generation with date ranges
 * 
 * Business Context:
 * - Validates that financial data exports correctly for German Excel compatibility
 * - Ensures proper formatting for accounting software integration
 */
public class CsvExportServiceTests
{
    private readonly CsvExportService _service;
    private readonly Mock<ILogger<CsvExportService>> _loggerMock;

    public CsvExportServiceTests()
    {
        _loggerMock = new Mock<ILogger<CsvExportService>>();
        _service = new CsvExportService(_loggerMock.Object);
    }

    /**
     * Test that CSV is generated with correct German number formatting
     * 
     * Business Rule Validation:
     * - Numbers use comma as decimal separator (1.234,56)
     * - CSV uses semicolon delimiter for German Excel
     * - Profit margin is calculated as percentage
     * - German month names are used
     */
    [Fact]
    public async Task GenerateCsvAsync_WithValidData_ReturnsCorrectCsvFormat()
    {
        // Arrange - Create test financial periods with transaction data
        var periods = new List<FinancialPeriod>
        {
            new FinancialPeriod
            {
                Id = 1,
                Year = 2024,
                Month = 1,
                TransactionLines = new List<TransactionLine>
                {
                    // Revenue transaction (typical German BWA revenue category)
                    new TransactionLine 
                    { 
                        Category = "Umsatzerlöse",
                        Amount = 15234.56m,
                        Type = TransactionType.Revenue
                    },
                    // Expense transaction (typical German BWA expense category)
                    new TransactionLine 
                    { 
                        Category = "Personalkosten",
                        Amount = 12456.78m,
                        Type = TransactionType.Expense
                    }
                }
            },
            new FinancialPeriod
            {
                Id = 2,
                Year = 2024,
                Month = 2,
                TransactionLines = new List<TransactionLine>
                {
                    new TransactionLine 
                    { 
                        Category = "Umsatzerlöse",
                        Amount = 16543.21m,
                        Type = TransactionType.Revenue
                    },
                    new TransactionLine 
                    { 
                        Category = "Betriebliche Aufwendungen",
                        Amount = 13234.56m,
                        Type = TransactionType.Expense
                    }
                }
            }
        };

        // Act - Generate CSV content
        var csvBytes = await _service.GenerateCsvAsync(periods);
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert - Verify CSV structure and German formatting
        Assert.NotNull(csvBytes);
        Assert.True(csvBytes.Length > 0);
        
        // Check for UTF-8 BOM (required for Excel to recognize UTF-8 encoding)
        var bom = Encoding.UTF8.GetPreamble();
        Assert.True(csvBytes.Take(bom.Length).SequenceEqual(bom), "CSV should start with UTF-8 BOM");
        
        // Remove BOM for content checking
        if (csvBytes.Length > bom.Length)
        {
            csvContent = Encoding.UTF8.GetString(csvBytes, bom.Length, csvBytes.Length - bom.Length);
        }
        
        // Verify headers are in German
        Assert.Contains("Jahr;Monat;Umsatzerlöse;Gesamtkosten;Gewinn;Gewinnmarge", csvContent);
        
        // Verify German month names
        Assert.Contains("Januar", csvContent);
        Assert.Contains("Februar", csvContent);
        
        // Verify German number formatting (15.234,56 instead of 15,234.56)
        Assert.Contains("15.234,56", csvContent); // Revenue for January
        Assert.Contains("12.456,78", csvContent); // Expenses for January
        
        // Verify profit calculation (15234.56 - 12456.78 = 2777.78)
        Assert.Contains("2.777,78", csvContent);
        
        // Verify semicolon delimiter is used (German standard)
        Assert.Contains(";", csvContent);
        Assert.DoesNotContain("\t", csvContent); // Should not use tabs
        
        // Verify profit margin includes percentage sign
        Assert.Contains("%", csvContent);
    }

    /**
     * Test that appropriate response is returned when no financial data exists
     * 
     * Business Rule:
     * - Empty data should generate valid CSV with headers only
     * - No exceptions should be thrown
     * - File should still be valid for Excel import
     */
    [Fact]
    public async Task GenerateCsvAsync_WithNoData_ReturnsEmptyCsvWithHeaders()
    {
        // Arrange - Empty list of periods
        var periods = new List<FinancialPeriod>();

        // Act - Generate CSV for empty data
        var csvBytes = await _service.GenerateCsvAsync(periods);
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert - Verify CSV has headers but no data rows
        Assert.NotNull(csvBytes);
        Assert.True(csvBytes.Length > 0);
        
        // Check for UTF-8 BOM
        var bom = Encoding.UTF8.GetPreamble();
        Assert.True(csvBytes.Take(bom.Length).SequenceEqual(bom), "CSV should start with UTF-8 BOM");
        
        // Remove BOM for content checking
        if (csvBytes.Length > bom.Length)
        {
            csvContent = Encoding.UTF8.GetString(csvBytes, bom.Length, csvBytes.Length - bom.Length);
        }
        
        // Should contain headers
        Assert.Contains("Jahr;Monat;Umsatzerlöse;Gesamtkosten;Gewinn;Gewinnmarge", csvContent);
        
        // Should not contain any year data (no 2024, 2023, etc.)
        Assert.DoesNotContain("2024", csvContent);
        Assert.DoesNotContain("2023", csvContent);
        
        // Verify only one line (headers) plus potential newline
        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines); // Only header line
    }

    /**
     * Test filename generation with proper date range formatting
     * 
     * Business Rule:
     * - Filename should include start and end period
     * - Format: BWA-Export_YYYY-MM_YYYY-MM.csv
     */
    [Fact]
    public void GenerateFileName_WithValidPeriods_ReturnsCorrectFormat()
    {
        // Arrange - Multiple periods spanning several months
        var periods = new List<FinancialPeriod>
        {
            new FinancialPeriod { Year = 2024, Month = 3 },
            new FinancialPeriod { Year = 2024, Month = 1 },
            new FinancialPeriod { Year = 2024, Month = 12 },
            new FinancialPeriod { Year = 2024, Month = 6 }
        };

        // Act - Generate filename
        var filename = _service.GenerateFileName(periods);

        // Assert - Verify filename format
        Assert.Equal("BWA-Export_2024-01_2024-12.csv", filename);
    }

    /**
     * Test filename generation with empty data
     * 
     * Business Rule:
     * - Should return fallback filename with current date
     */
    [Fact]
    public void GenerateFileName_WithNoPeriods_ReturnsFallbackFormat()
    {
        // Arrange - Empty period list
        var periods = new List<FinancialPeriod>();

        // Act
        var filename = _service.GenerateFileName(periods);

        // Assert - Should contain BWA-Export and current date
        Assert.StartsWith("BWA-Export_", filename);
        Assert.EndsWith(".csv", filename);
        Assert.Contains(DateTime.Now.Year.ToString(), filename);
    }
}