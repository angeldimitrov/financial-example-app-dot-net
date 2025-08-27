using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;

namespace FinanceApp.Web.Tests.Services;

/// <summary>
/// Comprehensive unit tests for CsvExportService
/// 
/// Test Coverage:
/// - CSV header format validation
/// - German to international number format conversion
/// - UTF-8 BOM presence for German character support
/// - Data format accuracy and structure
/// - Edge cases and error handling
/// 
/// Business Context:
/// These tests ensure that financial data exports maintain data integrity
/// when transitioning from German accounting format to international CSV standard
/// </summary>
public class CsvExportServiceTests
{
    private readonly Mock<ILogger<CsvExportService>> _mockLogger;
    private readonly CsvExportService _csvExportService;

    public CsvExportServiceTests()
    {
        _mockLogger = new Mock<ILogger<CsvExportService>>();
        _csvExportService = new CsvExportService(_mockLogger.Object);
    }

    /// <summary>
    /// Test Case 1: Verify CSV Generation with Correct Headers and Format
    /// 
    /// Business Requirement:
    /// - CSV must have standardized headers for consistent data imports
    /// - Data must be properly formatted and structured
    /// - UTF-8 BOM must be present for German character support in Excel
    /// 
    /// Test Scenario:
    /// Creates sample TransactionLine objects with typical German financial data
    /// and verifies the complete CSV output structure and format
    /// </summary>
    [Fact]
    public void ExportTransactionsToCsv_WithSampleData_ReturnsCorrectHeadersAndFormat()
    {
        // Arrange: Create test transaction data representing typical German financial entries
        var testPeriod = new FinancialPeriod
        {
            Id = 1,
            Year = 2024,
            Month = 3,
            SourceFileName = "test-jahresuebersicht.pdf",
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<TransactionLine>
        {
            // Revenue transaction - typical German business revenue
            new TransactionLine
            {
                Id = 1,
                FinancialPeriodId = 1,
                FinancialPeriod = testPeriod,
                Category = "Umsatzerlöse",
                Month = 3,
                Year = 2024,
                Amount = 15750.45m, // This would display as 15.750,45 in German format
                Type = TransactionType.Revenue,
                GroupCategory = "Erlöse"
            },
            // Expense transaction - typical German business expense
            new TransactionLine
            {
                Id = 2,
                FinancialPeriodId = 1,
                FinancialPeriod = testPeriod,
                Category = "Personalkosten",
                Month = 3,
                Year = 2024,
                Amount = 8250.00m, // This would display as 8.250,00 in German format
                Type = TransactionType.Expense,
                GroupCategory = "Personalkosten"
            },
            // Transaction with special characters - testing German umlauts
            new TransactionLine
            {
                Id = 3,
                FinancialPeriodId = 1,
                FinancialPeriod = testPeriod,
                Category = "Bürokosten & Material",
                Month = 3,
                Year = 2024,
                Amount = 1234.56m,
                Type = TransactionType.Expense,
                GroupCategory = "Betriebskosten"
            }
        };

        // Act: Export transactions to CSV format
        byte[] csvBytes = _csvExportService.ExportTransactionsToCsv(transactions);

        // Assert: Verify UTF-8 BOM is present for German character support
        var bomBytes = Encoding.UTF8.GetPreamble();
        Assert.True(csvBytes.Length >= bomBytes.Length, "CSV should include UTF-8 BOM");
        
        // Verify BOM presence - critical for German character display in Excel
        for (int i = 0; i < bomBytes.Length; i++)
        {
            Assert.Equal(bomBytes[i], csvBytes[i]);
        }

        // Convert bytes to string for content verification
        string csvContent = Encoding.UTF8.GetString(csvBytes, bomBytes.Length, csvBytes.Length - bomBytes.Length);
        string[] lines = csvContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        // Verify CSV structure and header format
        Assert.True(lines.Length >= 4, "CSV should contain header + 3 data rows");
        
        // Test standardized header format - critical for data import consistency
        string expectedHeader = "Year,Month,Category,Description,Amount,Type";
        Assert.Equal(expectedHeader, lines[0]);

        // Verify first transaction data format and structure
        string[] firstRowData = lines[1].Split(',');
        Assert.Equal("2024", firstRowData[0]); // Year
        Assert.Equal("3", firstRowData[1]); // Month
        Assert.Equal("Umsatzerlöse", firstRowData[2]); // Category - German term preserved
        Assert.Equal("Umsatzerlöse", firstRowData[3]); // Description matches Category
        Assert.Equal("15750.45", firstRowData[4]); // Amount in international format
        Assert.Equal("Revenue", firstRowData[5]); // Type as enum string

        // Verify second transaction with expense data
        string[] secondRowData = lines[2].Split(',');
        Assert.Equal("2024", secondRowData[0]);
        Assert.Equal("3", secondRowData[1]);
        Assert.Equal("Personalkosten", secondRowData[2]);
        Assert.Equal("Personalkosten", secondRowData[3]);
        Assert.Equal("8250.00", secondRowData[4]); // Verify .00 decimal formatting
        Assert.Equal("Expense", secondRowData[5]);

        // Verify transaction with German special characters is properly handled
        string[] thirdRowData = lines[3].Split(',');
        Assert.Equal("2024", thirdRowData[0]);
        Assert.Equal("3", thirdRowData[1]);
        Assert.Contains("Bürokosten", thirdRowData[2]); // Verify umlaut preservation
        Assert.Equal("1234.56", thirdRowData[4]); // Verify decimal format

        // Verify logging was called for export operation
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exported 3 transactions to CSV format")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test Case 2: German to International Number Format Conversion
    /// 
    /// Business Requirement:
    /// German accounting systems use comma as decimal separator (1.234,56)
    /// International CSV format requires dot as decimal separator (1234.56)
    /// No thousands separators should be present in CSV output
    /// 
    /// Test Scenario:
    /// Creates transactions with various German-formatted amounts and verifies
    /// proper conversion to international number format in CSV output
    /// </summary>
    [Fact]
    public void ExportTransactionsToCsv_WithGermanNumberFormat_ConvertsToInternationalFormat()
    {
        // Arrange: Create transactions with amounts that would use German formatting
        var testPeriod = new FinancialPeriod
        {
            Id = 1,
            Year = 2024,
            Month = 5,
            SourceFileName = "test-bwa-format.pdf",
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<TransactionLine>
        {
            // Large amount with thousands - would be 1.234.567,89 in German format
            new TransactionLine
            {
                Id = 1,
                FinancialPeriodId = 1,
                FinancialPeriod = testPeriod,
                Category = "Jahresabschlusskosten",
                Month = 5,
                Year = 2024,
                Amount = 1234567.89m, // Large amount to test thousands handling
                Type = TransactionType.Expense
            },
            // Amount with precise decimal places - would be 999,99 in German format
            new TransactionLine
            {
                Id = 2,
                FinancialPeriodId = 1,
                FinancialPeriod = testPeriod,
                Category = "Kleinbetrag",
                Month = 5,
                Year = 2024,
                Amount = 999.99m,
                Type = TransactionType.Expense
            },
            // Zero decimal amount - would be 5.000,00 in German format
            new TransactionLine
            {
                Id = 3,
                FinancialPeriodId = 1,
                FinancialPeriod = testPeriod,
                Category = "Rundungsbetrag",
                Month = 5,
                Year = 2024,
                Amount = 5000.00m,
                Type = TransactionType.Revenue
            },
            // Negative amount for expenses - would be -2.500,50 in German format
            new TransactionLine
            {
                Id = 4,
                FinancialPeriodId = 1,
                FinancialPeriod = testPeriod,
                Category = "Steuern",
                Month = 5,
                Year = 2024,
                Amount = -2500.50m,
                Type = TransactionType.Expense
            }
        };

        // Act: Export transactions and extract CSV content for format verification
        byte[] csvBytes = _csvExportService.ExportTransactionsToCsv(transactions);
        
        // Skip UTF-8 BOM to get pure CSV content
        var bomBytes = Encoding.UTF8.GetPreamble();
        string csvContent = Encoding.UTF8.GetString(csvBytes, bomBytes.Length, csvBytes.Length - bomBytes.Length);
        string[] lines = csvContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        // Assert: Verify international number format conversion for each transaction
        
        // Test 1: Large amount - verify no thousands separators, dot as decimal separator
        string[] largeAmountRow = lines[1].Split(',');
        string largeAmountFormatted = largeAmountRow[4];
        Assert.Equal("1234567.89", largeAmountFormatted);
        Assert.DoesNotContain(",", largeAmountFormatted); // No German decimal separator
        Assert.DoesNotContain(" ", largeAmountFormatted); // No thousands separators
        
        // Verify this would be different in German culture format
        string germanFormat = 1234567.89m.ToString("N2", new CultureInfo("de-DE"));
        Assert.NotEqual(germanFormat.Replace(".", "").Replace(",", "."), largeAmountFormatted);
        
        // Test 2: Decimal precision - verify proper .99 formatting
        string[] precisionAmountRow = lines[2].Split(',');
        string precisionAmountFormatted = precisionAmountRow[4];
        Assert.Equal("999.99", precisionAmountFormatted);
        Assert.Contains(".", precisionAmountFormatted); // International decimal separator
        Assert.DoesNotContain(",", precisionAmountFormatted); // No German decimal separator
        
        // Test 3: Zero decimal - verify .00 is included
        string[] roundAmountRow = lines[3].Split(',');
        string roundAmountFormatted = roundAmountRow[4];
        Assert.Equal("5000.00", roundAmountFormatted);
        Assert.EndsWith(".00", roundAmountFormatted); // Verify two decimal places maintained
        
        // Test 4: Negative amount - verify proper negative formatting
        string[] negativeAmountRow = lines[4].Split(',');
        string negativeAmountFormatted = negativeAmountRow[4];
        Assert.Equal("-2500.50", negativeAmountFormatted);
        Assert.StartsWith("-", negativeAmountFormatted); // Negative sign preserved
        Assert.Contains(".50", negativeAmountFormatted); // Decimal formatting correct
        
        // Verify all amounts use invariant culture formatting (international standard)
        foreach (var line in lines.Skip(1)) // Skip header
        {
            var parts = line.Split(',');
            var amountPart = parts[4];
            
            // Verify no German formatting artifacts
            Assert.DoesNotContain(",", amountPart.TrimStart('-')); // No German decimal comma
            Assert.True(decimal.TryParse(amountPart, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, 
                CultureInfo.InvariantCulture, out _), $"Amount {amountPart} should parse as invariant culture decimal");
        }
        
        // Verify proper export logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exported 4 transactions to CSV format")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test edge case: Empty transaction list
    /// Verifies service handles empty input gracefully
    /// </summary>
    [Fact]
    public void ExportTransactionsToCsv_WithEmptyList_ReturnsHeaderOnly()
    {
        // Arrange: Empty transaction list
        var emptyTransactions = new List<TransactionLine>();

        // Act: Export empty list
        byte[] csvBytes = _csvExportService.ExportTransactionsToCsv(emptyTransactions);

        // Assert: Should contain UTF-8 BOM + header only
        var bomBytes = Encoding.UTF8.GetPreamble();
        string csvContent = Encoding.UTF8.GetString(csvBytes, bomBytes.Length, csvBytes.Length - bomBytes.Length);
        string[] lines = csvContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines); // Only header line
        Assert.Equal("Year,Month,Category,Description,Amount,Type", lines[0]);
    }

    /// <summary>
    /// Test error handling: Null input
    /// Verifies service throws appropriate exception for invalid input
    /// </summary>
    [Fact]
    public void ExportTransactionsToCsv_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert: Should throw ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => _csvExportService.ExportTransactionsToCsv(null!));
    }
}