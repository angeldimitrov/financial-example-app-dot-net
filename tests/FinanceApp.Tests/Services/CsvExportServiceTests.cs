using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using Bogus;

namespace FinanceApp.Tests.Services;

/// <summary>
/// Comprehensive unit tests for CsvExportService focusing on filtering capabilities
/// 
/// Test Coverage:
/// - Priority Test 1: Export with date range filter (inclusive boundaries, edge cases)
/// - Priority Test 2: Export with transaction type filter (Revenue, Expense, Both)
/// - German number formatting validation (1.234,56 format)
/// - CSV structure and encoding verification
/// - Error handling and validation scenarios
/// </summary>
public class CsvExportServiceTests
{
    private readonly Mock<DataImportService> _mockDataImportService;
    private readonly Mock<IInputSanitizationService> _mockInputSanitization;
    private readonly Mock<ILogger<CsvExportService>> _mockLogger;
    private readonly CsvExportService _csvExportService;

    public CsvExportServiceTests()
    {
        // Create mock for DataImportService (virtual method approach)
        _mockDataImportService = new Mock<DataImportService>();
        _mockInputSanitization = new Mock<IInputSanitizationService>();
        _mockLogger = new Mock<ILogger<CsvExportService>>();
        
        _csvExportService = new CsvExportService(
            _mockDataImportService.Object,
            _mockLogger.Object,
            _mockInputSanitization.Object);

        // Setup default mock behaviors
        _mockInputSanitization.Setup(x => x.SanitizeTransactionInput(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((input, field) => input ?? "");
        _mockInputSanitization.Setup(x => x.CreateSafeDisplayText(It.IsAny<string>()))
            .Returns<string>(input => input ?? "");
    }

    #region Priority Test 1: Export with Date Range Filter

    /// <summary>
    /// Priority Test 1: Verify that CSV export correctly applies date range filtering
    /// 
    /// Test Scenarios:
    /// - Inclusive date boundaries (both start and end dates included)
    /// - Transactions exactly on boundary dates are included
    /// - Transactions outside range are excluded
    /// - Proper handling of year/month comparison logic
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithDateRange_FiltersTransactionsCorrectly()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Revenue Item", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 6, "Mid Year Revenue", TransactionType.Revenue, 2000),  // Should be included
            CreateTransaction(2023, 12, "End Year Revenue", TransactionType.Revenue, 3000), // Should be included
            CreateTransaction(2024, 1, "Next Year Revenue", TransactionType.Revenue, 4000), // Should be excluded
            CreateTransaction(2022, 12, "Previous Year", TransactionType.Revenue, 500)      // Should be excluded
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        var startDate = new DateTime(2023, 6, 1);  // June 2023 - inclusive
        var endDate = new DateTime(2023, 12, 31);  // December 2023 - inclusive

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(startDate, endDate, "Both");
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert - Verify filtering worked correctly
        csvContent.Should().NotBeNullOrEmpty("CSV content should be generated");
        
        // Verify included transactions (within date range)
        csvContent.Should().Contain("2023,6,Mid Year Revenue", "June 2023 transaction should be included");
        csvContent.Should().Contain("2023,12,End Year Revenue", "December 2023 transaction should be included");
        
        // Verify excluded transactions (outside date range)
        csvContent.Should().NotContain("2023,1,Revenue Item", "January 2023 transaction should be excluded (before start date)");
        csvContent.Should().NotContain("2024,1,Next Year Revenue", "2024 transaction should be excluded (after end date)");
        csvContent.Should().NotContain("2022,12,Previous Year", "2022 transaction should be excluded (before start date)");

        // Verify CSV structure
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Jahr,Monat,Kategorie,Typ,Betrag,Gruppenkategorie", "CSV header should be in German");
        lines.Length.Should().Be(3, "Should contain header + 2 filtered transactions"); // Header + 2 transactions
    }

    /// <summary>
    /// Test edge case: Exact boundary date matching
    /// Verifies that transactions on the exact start and end dates are included
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithExactBoundaryDates_IncludesBoundaryTransactions()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 6, "Start Boundary", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 12, "End Boundary", TransactionType.Revenue, 2000),
            CreateTransaction(2023, 5, "Before Start", TransactionType.Revenue, 3000),
            CreateTransaction(2024, 1, "After End", TransactionType.Revenue, 4000)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        var startDate = new DateTime(2023, 6, 15);  // Mid-June (should include June transactions)
        var endDate = new DateTime(2023, 12, 5);    // Early December (should include December transactions)

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(startDate, endDate, "Both");
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        csvContent.Should().Contain("2023,6,Start Boundary", "Start boundary month should be included");
        csvContent.Should().Contain("2023,12,End Boundary", "End boundary month should be included");
        csvContent.Should().NotContain("2023,5,Before Start", "Month before start should be excluded");
        csvContent.Should().NotContain("2024,1,After End", "Month after end should be excluded");
    }

    /// <summary>
    /// Test edge case: Empty date range
    /// When startDate > endDate, should throw ArgumentException
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithInvalidDateRange_ThrowsArgumentException()
    {
        // Arrange
        var startDate = new DateTime(2023, 12, 1);
        var endDate = new DateTime(2023, 6, 1);  // End before start

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _csvExportService.ExportToCsvAsync(startDate, endDate, "Both"));
        
        exception.Message.Should().Contain("Start date cannot be after end date");
        exception.ParamName.Should().Be("startDate");
    }

    /// <summary>
    /// Test edge case: No transactions in date range
    /// Should return CSV with header only
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithNoTransactionsInRange_ReturnsHeaderOnly()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2020, 1, "Old Transaction", TransactionType.Revenue, 1000),
            CreateTransaction(2025, 1, "Future Transaction", TransactionType.Revenue, 2000)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        var startDate = new DateTime(2023, 1, 1);
        var endDate = new DateTime(2023, 12, 31);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(startDate, endDate, "Both");
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(1, "Should contain only header when no transactions match");
        lines[0].Should().Be("Jahr,Monat,Kategorie,Typ,Betrag,Gruppenkategorie");
    }

    #endregion

    #region Priority Test 2: Export with Transaction Type Filter

    /// <summary>
    /// Priority Test 2: Verify that CSV export correctly applies transaction type filtering
    /// 
    /// Test Scenarios:
    /// - "Revenue" filter includes only Revenue transactions
    /// - "Expense" filter includes only Expense transactions  
    /// - "Both" filter includes all transaction types
    /// - Null/empty filter defaults to "Both"
    /// - Case-insensitive filtering
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithRevenueFilter_ExportsOnlyRevenueTransactions()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Revenue Item 1", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 1, "Revenue Item 2", TransactionType.Revenue, 2000),
            CreateTransaction(2023, 1, "Expense Item 1", TransactionType.Expense, 500),
            CreateTransaction(2023, 1, "Other Item 1", TransactionType.Other, 300),
            CreateTransaction(2023, 1, "Summary Item 1", TransactionType.Summary, 100)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(null, null, "Revenue");
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        csvContent.Should().NotBeNullOrEmpty();
        
        // Verify only Revenue transactions are included
        csvContent.Should().Contain("Revenue Item 1,Revenue,", "Revenue transaction 1 should be included");
        csvContent.Should().Contain("Revenue Item 2,Revenue,", "Revenue transaction 2 should be included");
        
        // Verify other transaction types are excluded
        csvContent.Should().NotContain("Expense Item 1,Expense,", "Expense transaction should be excluded");
        csvContent.Should().NotContain("Other Item 1,Other,", "Other transaction should be excluded");
        csvContent.Should().NotContain("Summary Item 1,Summary,", "Summary transaction should be excluded");

        // Verify count
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3, "Should contain header + 2 revenue transactions");
    }

    /// <summary>
    /// Test Expense-only filtering
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithExpenseFilter_ExportsOnlyExpenseTransactions()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Revenue Item", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 1, "Expense Item 1", TransactionType.Expense, 500),
            CreateTransaction(2023, 1, "Expense Item 2", TransactionType.Expense, 750),
            CreateTransaction(2023, 1, "Other Item", TransactionType.Other, 300)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(null, null, "Expense");
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        csvContent.Should().Contain("Expense Item 1,Expense,", "Expense transaction 1 should be included");
        csvContent.Should().Contain("Expense Item 2,Expense,", "Expense transaction 2 should be included");
        csvContent.Should().NotContain("Revenue Item,Revenue,", "Revenue transaction should be excluded");
        csvContent.Should().NotContain("Other Item,Other,", "Other transaction should be excluded");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3, "Should contain header + 2 expense transactions");
    }

    /// <summary>
    /// Test "Both" filter includes all transaction types
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithBothFilter_ExportsAllTransactionTypes()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Revenue Item", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 1, "Expense Item", TransactionType.Expense, 500),
            CreateTransaction(2023, 1, "Other Item", TransactionType.Other, 300),
            CreateTransaction(2023, 1, "Summary Item", TransactionType.Summary, 100)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(null, null, "Both");
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        csvContent.Should().Contain("Revenue Item,Revenue,", "Revenue transaction should be included");
        csvContent.Should().Contain("Expense Item,Expense,", "Expense transaction should be included");
        csvContent.Should().Contain("Other Item,Other,", "Other transaction should be included");
        csvContent.Should().Contain("Summary Item,Summary,", "Summary transaction should be included");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(5, "Should contain header + 4 transactions of all types");
    }

    /// <summary>
    /// Test null transaction type defaults to "Both"
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithNullTransactionType_DefaultsToBoth()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Revenue Item", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 1, "Expense Item", TransactionType.Expense, 500)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(null, null, null);
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        csvContent.Should().Contain("Revenue Item,Revenue,", "Revenue transaction should be included");
        csvContent.Should().Contain("Expense Item,Expense,", "Expense transaction should be included");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3, "Should contain header + both transactions");
    }

    /// <summary>
    /// Test case-insensitive transaction type filtering
    /// </summary>
    [Theory]
    [InlineData("revenue")]
    [InlineData("REVENUE")]
    [InlineData("Revenue")]
    [InlineData("expense")]
    [InlineData("EXPENSE")]
    [InlineData("Expense")]
    public async Task ExportToCsvAsync_WithCaseInsensitiveTransactionType_FiltersCorrectly(string transactionType)
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Revenue Item", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 1, "Expense Item", TransactionType.Expense, 500)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(null, null, transactionType);
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        if (transactionType.ToLower() == "revenue")
        {
            csvContent.Should().Contain("Revenue Item,Revenue,", "Revenue should be included for revenue filter");
            csvContent.Should().NotContain("Expense Item,Expense,", "Expense should be excluded for revenue filter");
        }
        else if (transactionType.ToLower() == "expense")
        {
            csvContent.Should().Contain("Expense Item,Expense,", "Expense should be included for expense filter");
            csvContent.Should().NotContain("Revenue Item,Revenue,", "Revenue should be excluded for expense filter");
        }
    }

    #endregion

    #region German Number Formatting Tests

    /// <summary>
    /// Test that amounts are formatted according to German culture (1.234,56)
    /// This ensures Excel compatibility for German users
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_FormatsAmountsInGermanFormat()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Large Amount", TransactionType.Revenue, 12345.67m),
            CreateTransaction(2023, 1, "Small Amount", TransactionType.Revenue, 123.45m),
            CreateTransaction(2023, 1, "Zero Amount", TransactionType.Revenue, 0.00m)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync();
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert - German number formatting uses comma as decimal separator and dot as thousand separator
        csvContent.Should().Contain("12.345,67", "Large amount should use German formatting (12.345,67)");
        csvContent.Should().Contain("123,45", "Small amount should use German formatting (123,45)");
        csvContent.Should().Contain("0,00", "Zero amount should use German formatting (0,00)");
        
        // Verify English formatting is not used
        csvContent.Should().NotContain("12,345.67", "Should not use English/US formatting");
        csvContent.Should().NotContain("123.45", "Should not use English/US decimal format");
    }

    #endregion

    #region CSV Structure and Encoding Tests

    /// <summary>
    /// Test CSV structure, headers, and UTF-8 BOM encoding
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_ReturnsCorrectCsvStructureWithUtf8Bom()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Test Category", TransactionType.Revenue, 1000.50m)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync();

        // Assert
        // Check UTF-8 BOM is present
        var bom = Encoding.UTF8.GetPreamble();
        csvBytes.Length.Should().BeGreaterThan(bom.Length, "CSV should contain BOM and content");
        
        for (int i = 0; i < bom.Length; i++)
        {
            csvBytes[i].Should().Be(bom[i], $"Byte {i} should match UTF-8 BOM");
        }

        // Check CSV content
        var csvContent = Encoding.UTF8.GetString(csvBytes);
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        lines[0].Should().Be("Jahr,Monat,Kategorie,Typ,Betrag,Gruppenkategorie", "Header should be in German");
        lines.Length.Should().Be(2, "Should contain header and one data row");
        
        // Verify data row format
        lines[1].Should().Contain("2023,1,Test Category,Revenue,1.000,50,");
    }

    /// <summary>
    /// Test CSV field escaping for special characters
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_EscapesSpecialCharactersInFields()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Category, with comma", TransactionType.Revenue, 1000),
            CreateTransaction(2023, 1, "Category \"with quotes\"", TransactionType.Revenue, 2000),
            CreateTransaction(2023, 1, "Category\nwith\nnewlines", TransactionType.Revenue, 3000)
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync();
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        csvContent.Should().Contain("\"Category, with comma\"", "Categories with commas should be quoted");
        csvContent.Should().Contain("\"Category \"\"with quotes\"\"\"", "Quotes should be escaped by doubling");
        csvContent.Should().Contain("\"Category\nwith\nnewlines\"", "Newlines should cause field to be quoted");
    }

    #endregion

    #region Combined Filtering Tests

    /// <summary>
    /// Test combined date range and transaction type filtering
    /// </summary>
    [Fact]
    public async Task ExportToCsvAsync_WithCombinedFilters_AppliesBothFiltersCorrectly()
    {
        // Arrange
        var testTransactions = new List<TransactionLine>
        {
            CreateTransaction(2023, 1, "Early Revenue", TransactionType.Revenue, 1000),     // Outside date range
            CreateTransaction(2023, 6, "Mid Revenue", TransactionType.Revenue, 2000),      // Should be included
            CreateTransaction(2023, 6, "Mid Expense", TransactionType.Expense, 500),       // Wrong type
            CreateTransaction(2023, 12, "Late Revenue", TransactionType.Revenue, 3000),    // Should be included
            CreateTransaction(2024, 1, "Future Revenue", TransactionType.Revenue, 4000),   // Outside date range
        };

        _mockDataImportService.Setup(x => x.GetTransactionsAsync(null, null, null))
            .ReturnsAsync(testTransactions);

        var startDate = new DateTime(2023, 6, 1);
        var endDate = new DateTime(2023, 12, 31);

        // Act
        var csvBytes = await _csvExportService.ExportToCsvAsync(startDate, endDate, "Revenue");
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Assert
        csvContent.Should().Contain("2023,6,Mid Revenue,Revenue,", "Mid Revenue should be included (correct date and type)");
        csvContent.Should().Contain("2023,12,Late Revenue,Revenue,", "Late Revenue should be included (correct date and type)");
        
        csvContent.Should().NotContain("2023,1,Early Revenue", "Early Revenue should be excluded (outside date range)");
        csvContent.Should().NotContain("2023,6,Mid Expense", "Mid Expense should be excluded (wrong type)");
        csvContent.Should().NotContain("2024,1,Future Revenue", "Future Revenue should be excluded (outside date range)");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3, "Should contain header + 2 filtered transactions");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper method to create test transaction data
    /// </summary>
    private static TransactionLine CreateTransaction(int year, int month, string category, TransactionType type, decimal amount)
    {
        return new TransactionLine
        {
            Id = Random.Shared.Next(1, 1000),
            Year = year,
            Month = month,
            Category = category,
            Type = type,
            Amount = amount,
            GroupCategory = type == TransactionType.Revenue ? "Erlöse" : "Kosten",
            FinancialPeriodId = Random.Shared.Next(1, 100)
        };
    }

    #endregion
}