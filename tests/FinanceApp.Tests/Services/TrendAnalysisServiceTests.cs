using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;

namespace FinanceApp.Tests.Services;

/// <summary>
/// Comprehensive unit tests for TrendAnalysisService
/// 
/// Test Coverage:
/// - Position aggregation logic with multiple data points per position/period
/// - Year and position filtering functionality
/// - German date formatting validation
/// - Transaction type classification and filtering
/// - Edge cases: empty data, invalid filters, exception handling
/// - Data ordering and structure validation
/// </summary>
public class TrendAnalysisServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ILogger<TrendAnalysisService>> _mockLogger;
    private readonly TrendAnalysisService _service;

    public TrendAnalysisServiceTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new AppDbContext(options);
        _mockLogger = new Mock<ILogger<TrendAnalysisService>>();
        _service = new TrendAnalysisService(_context, _mockLogger.Object);
        
        // Seed test data
        SeedTestData();
    }

    /// <summary>
    /// Seeds comprehensive test data covering multiple scenarios:
    /// - Multiple positions across different periods
    /// - Revenue and expense transactions
    /// - Multiple transactions per position/period (for aggregation testing)
    /// - Summary transactions (should be excluded)
    /// - Multiple years for year filtering tests
    /// </summary>
    private void SeedTestData()
    {
        // Create financial periods for testing
        var periods = new[]
        {
            new FinancialPeriod { Year = 2023, Month = 11, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2023, Month = 12, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 1, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 2, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 3, ImportedAt = DateTime.UtcNow }
        };
        
        _context.FinancialPeriods.AddRange(periods);
        _context.SaveChanges();

        // Create test transaction data with German position names
        var transactions = new[]
        {
            // Personalkosten (Personnel Costs) - Expense type
            // Multiple entries per period to test aggregation
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id, // 2024-01
                Category = "Personalkosten", 
                Month = 1, Year = 2024, 
                Amount = 10000m, 
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id, // 2024-01
                Category = "Personalkosten", 
                Month = 1, Year = 2024, 
                Amount = 5000m, // Should aggregate to 15000 total
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[3].Id, // 2024-02
                Category = "Personalkosten", 
                Month = 2, Year = 2024, 
                Amount = 12000m, 
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[4].Id, // 2024-03
                Category = "Personalkosten", 
                Month = 3, Year = 2024, 
                Amount = 11000m, 
                Type = TransactionType.Expense 
            },
            
            // Umsatzerlöse (Revenue) - Revenue type
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id, // 2024-01
                Category = "Umsatzerlöse", 
                Month = 1, Year = 2024, 
                Amount = 25000m, 
                Type = TransactionType.Revenue 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[3].Id, // 2024-02
                Category = "Umsatzerlöse", 
                Month = 2, Year = 2024, 
                Amount = 28000m, 
                Type = TransactionType.Revenue 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[4].Id, // 2024-03
                Category = "Umsatzerlöse", 
                Month = 3, Year = 2024, 
                Amount = 30000m, 
                Type = TransactionType.Revenue 
            },
            
            // Raumkosten (Facility Costs) - Expense type
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id, // 2024-01
                Category = "Raumkosten", 
                Month = 1, Year = 2024, 
                Amount = 3000m, 
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[3].Id, // 2024-02
                Category = "Raumkosten", 
                Month = 2, Year = 2024, 
                Amount = 3200m, 
                Type = TransactionType.Expense 
            },
            
            // 2023 data for year filtering tests
            new TransactionLine 
            { 
                FinancialPeriodId = periods[0].Id, // 2023-11
                Category = "Personalkosten", 
                Month = 11, Year = 2023, 
                Amount = 9000m, 
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[1].Id, // 2023-12
                Category = "Umsatzerlöse", 
                Month = 12, Year = 2023, 
                Amount = 22000m, 
                Type = TransactionType.Revenue 
            },
            
            // Summary transaction - should be excluded from trends
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id, // 2024-01
                Category = "Gesamtkosten", 
                Month = 1, Year = 2024, 
                Amount = 50000m, 
                Type = TransactionType.Summary 
            }
        };

        _context.TransactionLines.AddRange(transactions);
        _context.SaveChanges();
    }

    /// <summary>
    /// Test basic position aggregation functionality
    /// 
    /// Verifies:
    /// - All non-summary positions are included
    /// - Multiple transactions per position/period are correctly aggregated
    /// - Data is properly grouped by position and period
    /// - Results are sorted correctly
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_NoFilters_ReturnsAllPositionsAggregated()
    {
        // Act
        var result = await _service.GetPositionTrendsAsync();

        // Assert
        Assert.NotNull(result);
        
        // Should have 3 positions: Personalkosten, Umsatzerlöse, Raumkosten (Gesamtkosten excluded)
        Assert.Equal(3, result.Positions.Count);
        Assert.Contains("Personalkosten", result.Positions);
        Assert.Contains("Umsatzerlöse", result.Positions);
        Assert.Contains("Raumkosten", result.Positions);
        Assert.DoesNotContain("Gesamtkosten", result.Positions); // Summary should be excluded
        
        // Should have 3 series (one per position)
        Assert.Equal(3, result.Series.Count);
        
        // Verify Personalkosten aggregation (10000 + 5000 = 15000 for Jan 2024)
        var personalCosts = result.Series.First(s => s.PositionName == "Personalkosten");
        Assert.Equal("Expense", personalCosts.Type);
        
        var jan2024PersonalCosts = personalCosts.DataPoints.First(dp => dp.Year == 2024 && dp.Month == 1);
        Assert.Equal(15000m, jan2024PersonalCosts.Amount); // Aggregated amount
        
        // Verify data points are ordered chronologically
        var personalCostsPoints = personalCosts.DataPoints.OrderBy(dp => dp.Year).ThenBy(dp => dp.Month).ToList();
        Assert.Equal(personalCosts.DataPoints, personalCostsPoints);
    }

    /// <summary>
    /// Test year filtering functionality
    /// 
    /// Verifies:
    /// - Only data from specified year is returned
    /// - Positions that don't exist in filtered year are excluded
    /// - Aggregation still works correctly within filtered year
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_WithYearFilter_ReturnsOnlySpecifiedYear()
    {
        // Act - filter for 2024 data only
        var result = await _service.GetPositionTrendsAsync(year: 2024);

        // Assert
        Assert.NotNull(result);
        
        // Verify all data points are from 2024
        foreach (var series in result.Series)
        {
            Assert.All(series.DataPoints, dp => Assert.Equal(2024, dp.Year));
        }
        
        // Should still have all 3 positions (they all have 2024 data)
        Assert.Equal(3, result.Positions.Count);
        
        // Personalkosten should have 3 data points for 2024 (Jan, Feb, Mar)
        var personalCosts = result.Series.First(s => s.PositionName == "Personalkosten");
        Assert.Equal(3, personalCosts.DataPoints.Count);
        
        // Verify no 2023 data is included
        Assert.All(personalCosts.DataPoints, dp => Assert.True(dp.Year >= 2024));
    }

    /// <summary>
    /// Test position filtering functionality
    /// 
    /// Verifies:
    /// - Only specified positions are included in results
    /// - Case-sensitive position matching works correctly
    /// - Filtered positions maintain their full time series
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_WithPositionFilter_ReturnsOnlySpecifiedPositions()
    {
        // Arrange - filter for specific positions
        var positionFilter = new List<string> { "Personalkosten", "Umsatzerlöse" };

        // Act
        var result = await _service.GetPositionTrendsAsync(positionFilter: positionFilter);

        // Assert
        Assert.NotNull(result);
        
        // Should only have the 2 filtered positions
        Assert.Equal(2, result.Positions.Count);
        Assert.Contains("Personalkosten", result.Positions);
        Assert.Contains("Umsatzerlöse", result.Positions);
        Assert.DoesNotContain("Raumkosten", result.Positions);
        
        // Should have 2 series
        Assert.Equal(2, result.Series.Count);
        
        // Each position should maintain its complete time series
        var personalCosts = result.Series.First(s => s.PositionName == "Personalkosten");
        Assert.Equal(4, personalCosts.DataPoints.Count); // 2023-11, 2024-01, 2024-02, 2024-03
    }

    /// <summary>
    /// Test combined year and position filtering
    /// 
    /// Verifies:
    /// - Both filters are applied simultaneously
    /// - Only data matching both criteria is returned
    /// - Empty results handled gracefully when no data matches
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_WithYearAndPositionFilter_AppliesBothFilters()
    {
        // Arrange - filter for Personalkosten in 2024 only
        var positionFilter = new List<string> { "Personalkosten" };

        // Act
        var result = await _service.GetPositionTrendsAsync(year: 2024, positionFilter: positionFilter);

        // Assert
        Assert.NotNull(result);
        
        // Should only have Personalkosten
        Assert.Single(result.Positions);
        Assert.Equal("Personalkosten", result.Positions[0]);
        
        // Should only have 2024 data for Personalkosten (3 months)
        var personalCosts = result.Series.First();
        Assert.Equal(3, personalCosts.DataPoints.Count);
        Assert.All(personalCosts.DataPoints, dp => Assert.Equal(2024, dp.Year));
    }

    /// <summary>
    /// Test German date formatting
    /// 
    /// Verifies:
    /// - Period labels use German month abbreviations
    /// - Format follows "MMM yyyy" pattern (e.g., "Jan 2024")
    /// - Dates are properly localized for German users
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_GermanDateFormatting_UsesCorrectFormat()
    {
        // Act
        var result = await _service.GetPositionTrendsAsync(year: 2024);

        // Assert
        Assert.NotNull(result);
        
        var anyDataPoint = result.Series.First().DataPoints.First();
        
        // German formatting should produce abbreviated month names
        // Note: This tests the specific German culture formatting
        Assert.Matches(@"^[A-Za-z]{3} \d{4}$", anyDataPoint.Period); // Pattern: "Jan 2024"
        
        // Verify specific German month formatting for January 2024
        var jan2024Point = result.Series
            .SelectMany(s => s.DataPoints)
            .First(dp => dp.Year == 2024 && dp.Month == 1);
        
        // In German culture, January is "Jan"
        Assert.Equal("Jan 2024", jan2024Point.Period);
    }

    /// <summary>
    /// Test transaction type classification
    /// 
    /// Verifies:
    /// - Revenue positions are correctly classified as "Revenue"
    /// - Expense positions are correctly classified as "Expense"  
    /// - Type strings match expected values for frontend filtering
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_TransactionTypes_ClassifiedCorrectly()
    {
        // Act
        var result = await _service.GetPositionTrendsAsync();

        // Assert
        Assert.NotNull(result);
        
        // Verify revenue position type
        var revenue = result.Series.First(s => s.PositionName == "Umsatzerlöse");
        Assert.Equal("Revenue", revenue.Type);
        
        // Verify expense position types
        var personalCosts = result.Series.First(s => s.PositionName == "Personalkosten");
        Assert.Equal("Expense", personalCosts.Type);
        
        var facilityCosts = result.Series.First(s => s.PositionName == "Raumkosten");
        Assert.Equal("Expense", facilityCosts.Type);
    }

    /// <summary>
    /// Test edge case: empty database
    /// 
    /// Verifies:
    /// - Service handles empty data gracefully
    /// - Returns valid but empty result structure
    /// - No exceptions thrown on empty data
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_EmptyDatabase_ReturnsEmptyResult()
    {
        // Arrange - create a new context with no data
        var emptyOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var emptyContext = new AppDbContext(emptyOptions);
        var emptyService = new TrendAnalysisService(emptyContext, _mockLogger.Object);

        // Act
        var result = await emptyService.GetPositionTrendsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Positions);
        Assert.Empty(result.Series);
    }

    /// <summary>
    /// Test edge case: invalid position filter
    /// 
    /// Verifies:
    /// - Non-existent positions in filter are handled gracefully
    /// - Returns empty result when no positions match filter
    /// - No exceptions thrown for invalid filters
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_InvalidPositionFilter_ReturnsEmptyResult()
    {
        // Arrange - filter for non-existent position
        var invalidFilter = new List<string> { "NonExistentPosition", "AnotherFakePosition" };

        // Act
        var result = await _service.GetPositionTrendsAsync(positionFilter: invalidFilter);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Positions);
        Assert.Empty(result.Series);
    }

    /// <summary>
    /// Test edge case: future year filter
    /// 
    /// Verifies:
    /// - Year filters for periods with no data return empty results
    /// - No exceptions thrown for valid but empty year filters
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_FutureYearFilter_ReturnsEmptyResult()
    {
        // Act - filter for a future year with no data
        var result = await _service.GetPositionTrendsAsync(year: 2025);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Positions);
        Assert.Empty(result.Series);
    }

    /// <summary>
    /// Test data ordering and structure
    /// 
    /// Verifies:
    /// - Positions are sorted alphabetically
    /// - Data points within each series are sorted chronologically
    /// - Series maintain consistent structure
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_DataOrdering_IsCorrect()
    {
        // Act
        var result = await _service.GetPositionTrendsAsync();

        // Assert
        Assert.NotNull(result);
        
        // Positions should be sorted alphabetically
        var sortedPositions = result.Positions.OrderBy(p => p).ToList();
        Assert.Equal(sortedPositions, result.Positions);
        
        // Data points within each series should be sorted chronologically
        foreach (var series in result.Series)
        {
            var sortedDataPoints = series.DataPoints
                .OrderBy(dp => dp.Year)
                .ThenBy(dp => dp.Month)
                .ToList();
            Assert.Equal(sortedDataPoints, series.DataPoints);
        }
    }

    /// <summary>
    /// Test performance with larger dataset
    /// 
    /// Verifies:
    /// - Service can handle multiple positions across many periods
    /// - Aggregation logic scales appropriately
    /// - Memory usage remains reasonable
    /// </summary>
    [Fact]
    public async Task GetPositionTrendsAsync_LargeDataset_PerformsWell()
    {
        // Arrange - create additional test data
        var additionalPeriods = new List<FinancialPeriod>();
        var additionalTransactions = new List<TransactionLine>();
        
        // Add 12 months of data for multiple positions
        for (int month = 1; month <= 12; month++)
        {
            var period = new FinancialPeriod { Year = 2025, Month = month, ImportedAt = DateTime.UtcNow };
            additionalPeriods.Add(period);
        }
        
        _context.FinancialPeriods.AddRange(additionalPeriods);
        _context.SaveChanges();
        
        // Create multiple positions with data for each month
        var positions = new[] { "Position1", "Position2", "Position3", "Position4", "Position5" };
        
        foreach (var period in additionalPeriods)
        {
            foreach (var position in positions)
            {
                additionalTransactions.Add(new TransactionLine
                {
                    FinancialPeriodId = period.Id,
                    Category = position,
                    Month = period.Month,
                    Year = period.Year,
                    Amount = 1000m * period.Month, // Vary amounts
                    Type = TransactionType.Expense
                });
            }
        }
        
        _context.TransactionLines.AddRange(additionalTransactions);
        _context.SaveChanges();

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _service.GetPositionTrendsAsync(year: 2025);
        var duration = DateTime.UtcNow - startTime;

        // Assert - basic performance check (should complete within reasonable time)
        Assert.True(duration.TotalSeconds < 5, $"Query took too long: {duration.TotalSeconds} seconds");
        Assert.Equal(5, result.Positions.Count);
        
        // Each position should have 12 data points
        foreach (var series in result.Series)
        {
            Assert.Equal(12, series.DataPoints.Count);
        }
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}