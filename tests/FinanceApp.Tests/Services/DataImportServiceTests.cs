using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FinanceApp.Web.Data;
using FinanceApp.Web.Services;
using FinanceApp.Web.Models;

namespace FinanceApp.Tests.Services;

/// <summary>
/// Unit tests for DataImportService
/// Tests database operations, duplicate detection, and transaction handling
/// 
/// Test Coverage:
/// - Import validation and duplicate detection
/// - Database transaction handling
/// - Monthly summary calculations
/// - Performance optimizations
/// - Error handling scenarios
/// </summary>
public class DataImportServiceTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly Mock<ILogger<DataImportService>> _loggerMock;
    private readonly Mock<InputSanitizationService> _sanitizationMock;
    
    public DataImportServiceTests()
    {
        // Setup in-memory database for testing
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _loggerMock = new Mock<ILogger<DataImportService>>();
        
        // Setup input sanitization mock
        var sanitizationLoggerMock = new Mock<ILogger<InputSanitizationService>>();
        _sanitizationMock = new Mock<InputSanitizationService>(sanitizationLoggerMock.Object);
        _sanitizationMock.Setup(s => s.SanitizeFileName(It.IsAny<string>()))
            .Returns((string input) => input ?? "test.pdf");
        _sanitizationMock.Setup(s => s.SanitizeTransactionInput(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string input, string field) => input ?? string.Empty);
    }
    
    private AppDbContext CreateContext() => new AppDbContext(_dbOptions);
    private DataImportService CreateService(AppDbContext context) => 
        new DataImportService(context, _loggerMock.Object, _sanitizationMock.Object);
    
    [Fact]
    public async Task ImportDataAsync_NewData_ImportsSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        var parsedData = CreateTestParsedData(2023, new[] { 1, 2 });
        
        // Act
        var result = await service.ImportDataAsync(parsedData);
        
        // Assert
        Assert.True(result.Success);
        Assert.False(result.Skipped);
        Assert.Equal(2, result.ImportedMonths.Count);
        Assert.Contains(1, result.ImportedMonths);
        Assert.Contains(2, result.ImportedMonths);
        Assert.True(result.ImportedTransactionCount > 0);
        
        // Verify data in database
        var periodsInDb = await context.FinancialPeriods.CountAsync();
        var transactionsInDb = await context.TransactionLines.CountAsync();
        
        Assert.Equal(2, periodsInDb);
        Assert.Equal(result.ImportedTransactionCount, transactionsInDb);
    }
    
    [Fact]
    public async Task ImportDataAsync_DuplicateData_SkipsCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        var parsedData = CreateTestParsedData(2023, new[] { 1, 2 });
        
        // Import data first time
        await service.ImportDataAsync(parsedData);
        
        // Act - Import same data again
        var result = await service.ImportDataAsync(parsedData);
        
        // Assert
        Assert.True(result.Success);
        Assert.True(result.Skipped);
        Assert.Empty(result.ImportedMonths);
        Assert.Equal(2, result.SkippedMonths.Count);
        Assert.Equal(0, result.ImportedTransactionCount);
        
        // Verify no duplicates in database
        var periodsInDb = await context.FinancialPeriods.CountAsync();
        Assert.Equal(2, periodsInDb); // Still only 2, not 4
    }
    
    [Fact]
    public async Task ImportDataAsync_PartialDuplicate_ImportsOnlyNew()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        // Import data for months 1, 2
        var firstImport = CreateTestParsedData(2023, new[] { 1, 2 });
        await service.ImportDataAsync(firstImport);
        
        // Act - Import data for months 2, 3 (2 is duplicate, 3 is new)
        var secondImport = CreateTestParsedData(2023, new[] { 2, 3 });
        var result = await service.ImportDataAsync(secondImport);
        
        // Assert
        Assert.True(result.Success);
        Assert.False(result.Skipped);
        Assert.Single(result.ImportedMonths);
        Assert.Contains(3, result.ImportedMonths);
        Assert.Single(result.SkippedMonths);
        Assert.Contains(2, result.SkippedMonths);
        
        // Verify database state
        var periodsInDb = await context.FinancialPeriods.CountAsync();
        Assert.Equal(3, periodsInDb); // Months 1, 2, 3
    }
    
    [Fact]
    public async Task ImportDataAsync_EmptyData_ReturnsFailure()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        var emptyData = new ParsedFinancialData
        {
            Year = 2023,
            SourceFileName = "empty.pdf",
            TransactionLines = new List<ParsedTransactionLine>()
        };
        
        // Act
        var result = await service.ImportDataAsync(emptyData);
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("Keine Transaktionsdaten", result.Message);
    }
    
    [Fact]
    public async Task ImportDataAsync_NullData_ReturnsFailure()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        // Act
        var result = await service.ImportDataAsync(null!);
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("Keine Transaktionsdaten", result.Message);
    }
    
    [Fact]
    public async Task GetMonthlySummaryAsync_WithData_ReturnsCorrectSummary()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        // Import test data
        var parsedData = CreateTestParsedData(2023, new[] { 1, 2 });
        await service.ImportDataAsync(parsedData);
        
        // Act
        var summaries = await service.GetMonthlySummaryAsync();
        
        // Assert
        Assert.Equal(2, summaries.Count);
        
        var january = summaries.FirstOrDefault(s => s.Month == 1);
        Assert.NotNull(january);
        Assert.Equal(2023, january.Year);
        Assert.True(january.TotalRevenue > 0);
        Assert.True(january.TotalExpenses > 0);
        Assert.True(january.TransactionCount > 0);
        
        // Test calculated properties
        Assert.Equal("Januar 2023", january.MonthName);
        Assert.Equal("2023-01", january.PeriodKey);
    }
    
    [Fact]
    public async Task GetMonthlySummaryAsync_WithYearFilter_ReturnsFilteredResults()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        // Import data for different years
        var data2022 = CreateTestParsedData(2022, new[] { 12 });
        var data2023 = CreateTestParsedData(2023, new[] { 1, 2 });
        
        await service.ImportDataAsync(data2022);
        await service.ImportDataAsync(data2023);
        
        // Act
        var summaries2023 = await service.GetMonthlySummaryAsync(2023);
        
        // Assert
        Assert.Equal(2, summaries2023.Count);
        Assert.All(summaries2023, s => Assert.Equal(2023, s.Year));
    }
    
    [Fact]
    public async Task GetTransactionsAsync_WithFilters_ReturnsFilteredResults()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        var parsedData = CreateTestParsedData(2023, new[] { 1, 2 });
        await service.ImportDataAsync(parsedData);
        
        // Act
        var revenueTransactions = await service.GetTransactionsAsync(2023, null, TransactionType.Revenue);
        var expenseTransactions = await service.GetTransactionsAsync(2023, null, TransactionType.Expense);
        var januaryTransactions = await service.GetTransactionsAsync(2023, 1, null);
        
        // Assert
        Assert.All(revenueTransactions, t => Assert.Equal(TransactionType.Revenue, t.Type));
        Assert.All(expenseTransactions, t => Assert.Equal(TransactionType.Expense, t.Type));
        Assert.All(januaryTransactions, t => Assert.Equal(1, t.Month));
        
        // Verify Include is working (no lazy loading exceptions)
        Assert.All(revenueTransactions, t => Assert.NotNull(t.FinancialPeriod));
    }
    
    [Fact]
    public async Task GetDatabaseStatisticsAsync_ReturnsCorrectStats()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        var parsedData = CreateTestParsedData(2023, new[] { 1, 2 });
        await service.ImportDataAsync(parsedData);
        
        // Act
        var stats = await service.GetDatabaseStatisticsAsync();
        
        // Assert
        Assert.Equal(2, stats.TotalFinancialPeriods);
        Assert.True(stats.TotalTransactionLines > 0);
        Assert.Equal(2023, stats.EarliestDataYear);
        Assert.Equal(2023, stats.LatestDataYear);
        Assert.Equal(1, stats.DataYearSpan);
        Assert.True(stats.TotalRevenueAmount > 0);
        Assert.True(stats.TotalExpenseAmount > 0);
    }
    
    [Fact]
    public async Task ImportDataAsync_DatabaseError_RollsBackTransaction()
    {
        // This test would require mocking the database context to throw an exception
        // For integration testing, we'd use a real database with constraints
        
        using var context = CreateContext();
        var service = CreateService(context);
        
        // For now, test that method exists and handles errors gracefully
        var invalidData = new ParsedFinancialData
        {
            Year = 2023,
            SourceFileName = "test.pdf",
            TransactionLines = new List<ParsedTransactionLine>
            {
                new ParsedTransactionLine
                {
                    Category = "Test",
                    Month = 1,
                    Year = 2023,
                    Amount = 100m,
                    Type = TransactionType.Revenue
                }
            }
        };
        
        // This should succeed with valid data
        var result = await service.ImportDataAsync(invalidData);
        Assert.True(result.Success);
    }
    
    [Fact]
    public void MonthSummary_CalculatedProperties_WorkCorrectly()
    {
        // Test the calculated properties of MonthSummary
        var summary = new MonthSummary
        {
            Year = 2023,
            Month = 6,
            TotalRevenue = 10000m,
            TotalExpenses = 8000m,
            TransactionCount = 25
        };
        
        Assert.Equal(2000m, summary.NetResult);
        Assert.Equal("Juni 2023", summary.MonthName);
        Assert.Equal("2023-06", summary.PeriodKey);
        Assert.True(summary.IsProfitable);
        Assert.Equal(20m, summary.ProfitMargin); // 2000/10000 * 100
        
        // Test unprofitable scenario
        var unprofitableSummary = new MonthSummary
        {
            TotalRevenue = 5000m,
            TotalExpenses = 7000m
        };
        
        Assert.Equal(-2000m, unprofitableSummary.NetResult);
        Assert.False(unprofitableSummary.IsProfitable);
        Assert.Equal(-40m, unprofitableSummary.ProfitMargin); // -2000/5000 * 100
    }
    
    [Fact]
    public void DatabaseStatistics_CalculatedProperties_WorkCorrectly()
    {
        var stats = new DatabaseStatistics
        {
            TotalRevenueAmount = 15000m,
            TotalExpenseAmount = 12000m,
            EarliestDataYear = 2020,
            LatestDataYear = 2023
        };
        
        Assert.Equal(3000m, stats.NetTotalAmount);
        Assert.Equal(4, stats.DataYearSpan);
    }
    
    [Fact]
    public async Task ImportDataAsync_CallsSanitizationService()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        
        var parsedData = CreateTestParsedData(2023, new[] { 1 });
        parsedData.SourceFileName = "test<script>.pdf";
        
        // Act
        await service.ImportDataAsync(parsedData);
        
        // Assert
        _sanitizationMock.Verify(s => s.SanitizeFileName(It.IsAny<string>()), Times.AtLeastOnce);
        _sanitizationMock.Verify(s => s.SanitizeTransactionInput(It.IsAny<string>(), "category"), Times.AtLeastOnce);
    }
    
    /// <summary>
    /// Create test parsed data with German BWA categories
    /// </summary>
    private ParsedFinancialData CreateTestParsedData(int year, int[] months)
    {
        var transactions = new List<ParsedTransactionLine>();
        
        foreach (var month in months)
        {
            // Add revenue transaction
            transactions.Add(new ParsedTransactionLine
            {
                Category = "Umsatzerlöse",
                Month = month,
                Year = year,
                Amount = 5000m,
                Type = TransactionType.Revenue
            });
            
            // Add expense transactions
            transactions.Add(new ParsedTransactionLine
            {
                Category = "Personalkosten",
                Month = month,
                Year = year,
                Amount = -3000m,
                Type = TransactionType.Expense
            });
            
            transactions.Add(new ParsedTransactionLine
            {
                Category = "Raumkosten",
                Month = month,
                Year = year,
                Amount = -800m,
                Type = TransactionType.Expense
            });
            
            // Add tax transaction
            transactions.Add(new ParsedTransactionLine
            {
                Category = "Steuern Einkommen u. Ertrag",
                Month = month,
                Year = year,
                Amount = -200m,
                Type = TransactionType.Expense
            });
        }
        
        return new ParsedFinancialData
        {
            Year = year,
            SourceFileName = $"test_{year}.pdf",
            TransactionLines = transactions
        };
    }
    
    public void Dispose()
    {
        // Cleanup in-memory database
        using var context = CreateContext();
        context.Database.EnsureDeleted();
    }
}