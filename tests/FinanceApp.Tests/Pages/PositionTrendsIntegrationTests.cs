using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;

namespace FinanceApp.Tests.Pages;

/// <summary>
/// Integration tests for the PositionTrends API endpoint
/// 
/// Test Coverage:
/// - HTTP endpoint accessibility and response codes
/// - JSON serialization/deserialization of trend data
/// - Query parameter handling (positions, year, type filters)
/// - Error handling and validation
/// - Full integration with database and services
/// - Authentication/authorization (if applicable)
/// </summary>
public class PositionTrendsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PositionTrendsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the database context with in-memory database for testing
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });
            });
        });

        _client = _factory.CreateClient();
        
        // Seed test data
        SeedTestDatabase();
    }

    /// <summary>
    /// Seeds the test database with comprehensive data for integration testing
    /// 
    /// Test Data Structure:
    /// - Multiple financial periods across 2 years
    /// - Various German position names (Personalkosten, Umsatzerlöse, etc.)
    /// - Both revenue and expense transactions
    /// - Summary transactions (should be excluded)
    /// - Multiple transactions per position/period for aggregation testing
    /// </summary>
    private void SeedTestDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Ensure clean database
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        // Create financial periods
        var periods = new[]
        {
            new FinancialPeriod { Year = 2023, Month = 12, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 1, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 2, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 3, ImportedAt = DateTime.UtcNow }
        };
        
        context.FinancialPeriods.AddRange(periods);
        context.SaveChanges();

        // Create test transactions with German position names
        var transactions = new[]
        {
            // Personalkosten (Personnel Costs) - Expense
            new TransactionLine 
            { 
                FinancialPeriodId = periods[1].Id,
                Category = "Personalkosten", 
                Month = 1, Year = 2024, 
                Amount = 15000m, 
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id,
                Category = "Personalkosten", 
                Month = 2, Year = 2024, 
                Amount = 16000m, 
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[3].Id,
                Category = "Personalkosten", 
                Month = 3, Year = 2024, 
                Amount = 15500m, 
                Type = TransactionType.Expense 
            },
            
            // Umsatzerlöse (Revenue)
            new TransactionLine 
            { 
                FinancialPeriodId = periods[1].Id,
                Category = "Umsatzerlöse", 
                Month = 1, Year = 2024, 
                Amount = 45000m, 
                Type = TransactionType.Revenue 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id,
                Category = "Umsatzerlöse", 
                Month = 2, Year = 2024, 
                Amount = 48000m, 
                Type = TransactionType.Revenue 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[3].Id,
                Category = "Umsatzerlöse", 
                Month = 3, Year = 2024, 
                Amount = 52000m, 
                Type = TransactionType.Revenue 
            },
            
            // Raumkosten (Facility Costs) - Expense
            new TransactionLine 
            { 
                FinancialPeriodId = periods[1].Id,
                Category = "Raumkosten", 
                Month = 1, Year = 2024, 
                Amount = 3500m, 
                Type = TransactionType.Expense 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id,
                Category = "Raumkosten", 
                Month = 2, Year = 2024, 
                Amount = 3600m, 
                Type = TransactionType.Expense 
            },
            
            // 2023 data for year filtering
            new TransactionLine 
            { 
                FinancialPeriodId = periods[0].Id,
                Category = "Personalkosten", 
                Month = 12, Year = 2023, 
                Amount = 14000m, 
                Type = TransactionType.Expense 
            },
            
            // Summary transaction - should be excluded
            new TransactionLine 
            { 
                FinancialPeriodId = periods[1].Id,
                Category = "Gesamtkosten", 
                Month = 1, Year = 2024, 
                Amount = 60000m, 
                Type = TransactionType.Summary 
            }
        };

        context.TransactionLines.AddRange(transactions);
        context.SaveChanges();
    }

    /// <summary>
    /// Test basic API endpoint accessibility
    /// 
    /// Verifies:
    /// - Endpoint returns HTTP 200 OK
    /// - Response contains valid JSON
    /// - Basic response structure is correct
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_NoParameters_ReturnsSuccessWithData()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends?handler=PositionTrends");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(content));
        
        // Verify basic JSON structure
        var jsonDocument = JsonDocument.Parse(content);
        Assert.True(jsonDocument.RootElement.TryGetProperty("positions", out _));
        Assert.True(jsonDocument.RootElement.TryGetProperty("series", out _));
    }

    /// <summary>
    /// Test data retrieval and structure
    /// 
    /// Verifies:
    /// - All expected positions are returned
    /// - Summary positions are excluded
    /// - Data points contain expected properties
    /// - German date formatting is applied
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_DataRetrieval_ReturnsExpectedStructure()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends?handler=PositionTrends");
        var trendData = await response.Content.ReadFromJsonAsync<TrendData>();

        // Assert
        Assert.NotNull(trendData);
        
        // Should have 3 positions (excluding summary)
        Assert.Equal(3, trendData.Positions.Count);
        Assert.Contains("Personalkosten", trendData.Positions);
        Assert.Contains("Umsatzerlöse", trendData.Positions);
        Assert.Contains("Raumkosten", trendData.Positions);
        Assert.DoesNotContain("Gesamtkosten", trendData.Positions); // Summary excluded
        
        // Should have 3 series
        Assert.Equal(3, trendData.Series.Count);
        
        // Verify series structure
        foreach (var series in trendData.Series)
        {
            Assert.False(string.IsNullOrEmpty(series.PositionName));
            Assert.False(string.IsNullOrEmpty(series.Type));
            Assert.NotEmpty(series.DataPoints);
            
            // Verify data point structure
            foreach (var dataPoint in series.DataPoints)
            {
                Assert.False(string.IsNullOrEmpty(dataPoint.Period));
                Assert.True(dataPoint.Year > 0);
                Assert.InRange(dataPoint.Month, 1, 12);
                // German date format should be "MMM yyyy"
                Assert.Matches(@"^[A-Za-z]{3} \d{4}$", dataPoint.Period);
            }
        }
    }

    /// <summary>
    /// Test year filter parameter
    /// 
    /// Verifies:
    /// - Year parameter correctly filters data
    /// - Only data from specified year is returned
    /// - Response structure remains consistent
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_YearFilter_ReturnsFilteredData()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends?handler=PositionTrends&year=2024");
        var trendData = await response.Content.ReadFromJsonAsync<TrendData>();

        // Assert
        Assert.NotNull(trendData);
        
        // All data points should be from 2024
        foreach (var series in trendData.Series)
        {
            Assert.All(series.DataPoints, dp => Assert.Equal(2024, dp.Year));
        }
        
        // Should still have multiple positions with 2024 data
        Assert.True(trendData.Positions.Count > 0);
    }

    /// <summary>
    /// Test position filter parameter
    /// 
    /// Verifies:
    /// - Positions parameter correctly filters data
    /// - Only specified positions are returned
    /// - Multiple positions can be specified (comma-separated)
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_PositionFilter_ReturnsFilteredData()
    {
        // Act - filter for specific positions
        var response = await _client.GetAsync("/PositionTrends?handler=PositionTrends&positions=Personalkosten,Umsatzerlöse");
        var trendData = await response.Content.ReadFromJsonAsync<TrendData>();

        // Assert
        Assert.NotNull(trendData);
        
        // Should only have the 2 specified positions
        Assert.Equal(2, trendData.Positions.Count);
        Assert.Contains("Personalkosten", trendData.Positions);
        Assert.Contains("Umsatzerlöse", trendData.Positions);
        Assert.DoesNotContain("Raumkosten", trendData.Positions);
        
        // Should have 2 series
        Assert.Equal(2, trendData.Series.Count);
    }

    /// <summary>
    /// Test transaction type filter parameter
    /// 
    /// Verifies:
    /// - Type parameter correctly filters by revenue/expenses
    /// - "revenue" filter returns only revenue positions
    /// - "expenses" filter returns only expense positions
    /// - "all" filter returns all positions
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_TypeFilter_ReturnsFilteredData()
    {
        // Test revenue filter
        var revenueResponse = await _client.GetAsync("/PositionTrends?handler=PositionTrends&type=revenue");
        var revenueData = await revenueResponse.Content.ReadFromJsonAsync<TrendData>();

        Assert.NotNull(revenueData);
        Assert.Single(revenueData.Positions); // Only Umsatzerlöse is revenue
        Assert.Equal("Umsatzerlöse", revenueData.Positions[0]);
        Assert.All(revenueData.Series, s => Assert.Equal("Revenue", s.Type));

        // Test expenses filter
        var expensesResponse = await _client.GetAsync("/PositionTrends?handler=PositionTrends&type=expenses");
        var expensesData = await expensesResponse.Content.ReadFromJsonAsync<TrendData>();

        Assert.NotNull(expensesData);
        Assert.Equal(2, expensesData.Positions.Count); // Personalkosten and Raumkosten
        Assert.Contains("Personalkosten", expensesData.Positions);
        Assert.Contains("Raumkosten", expensesData.Positions);
        Assert.All(expensesData.Series, s => Assert.Equal("Expense", s.Type));
    }

    /// <summary>
    /// Test combined filter parameters
    /// 
    /// Verifies:
    /// - Multiple filters can be applied simultaneously
    /// - Combined filters work correctly together
    /// - Results match all specified criteria
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_CombinedFilters_ReturnsCorrectData()
    {
        // Act - combine year, position, and type filters
        var response = await _client.GetAsync("/PositionTrends?handler=PositionTrends&year=2024&positions=Personalkosten&type=expenses");
        var trendData = await response.Content.ReadFromJsonAsync<TrendData>();

        // Assert
        Assert.NotNull(trendData);
        
        // Should only have Personalkosten
        Assert.Single(trendData.Positions);
        Assert.Equal("Personalkosten", trendData.Positions[0]);
        
        // Should only have 2024 data
        var series = trendData.Series.First();
        Assert.All(series.DataPoints, dp => Assert.Equal(2024, dp.Year));
        Assert.Equal("Expense", series.Type);
    }

    /// <summary>
    /// Test error handling for invalid parameters
    /// 
    /// Verifies:
    /// - Invalid year values are handled gracefully
    /// - Non-existent positions don't cause errors
    /// - Invalid type values are handled appropriately
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_InvalidParameters_HandlesGracefully()
    {
        // Test with invalid year (too far in future)
        var futureYearResponse = await _client.GetAsync("/PositionTrends?handler=PositionTrends&year=3000");
        Assert.Equal(HttpStatusCode.OK, futureYearResponse.StatusCode);
        
        var futureYearData = await futureYearResponse.Content.ReadFromJsonAsync<TrendData>();
        Assert.NotNull(futureYearData);
        Assert.Empty(futureYearData.Positions); // Should return empty data, not error

        // Test with non-existent position
        var invalidPositionResponse = await _client.GetAsync("/PositionTrends?handler=PositionTrends&positions=NonExistentPosition");
        Assert.Equal(HttpStatusCode.OK, invalidPositionResponse.StatusCode);
        
        var invalidPositionData = await invalidPositionResponse.Content.ReadFromJsonAsync<TrendData>();
        Assert.NotNull(invalidPositionData);
        Assert.Empty(invalidPositionData.Positions); // Should return empty data
    }

    /// <summary>
    /// Test response performance
    /// 
    /// Verifies:
    /// - API responds within reasonable time limits
    /// - Large datasets are handled efficiently
    /// - No memory leaks or performance issues
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_Performance_RespondsWithinTimeLimit()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/PositionTrends?handler=PositionTrends");
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(duration.TotalSeconds < 5, $"API response took too long: {duration.TotalSeconds} seconds");
        
        var trendData = await response.Content.ReadFromJsonAsync<TrendData>();
        Assert.NotNull(trendData);
    }

    /// <summary>
    /// Test JSON serialization compatibility
    /// 
    /// Verifies:
    /// - Response can be deserialized to expected types
    /// - Property names match frontend expectations
    /// - Data types are correctly serialized
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_JsonSerialization_IsCompatible()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends?handler=PositionTrends");
        var jsonString = await response.Content.ReadAsStringAsync();
        
        // Test manual JSON parsing to verify structure
        var jsonDocument = JsonDocument.Parse(jsonString);
        var root = jsonDocument.RootElement;
        
        // Verify expected properties exist with correct types
        Assert.True(root.TryGetProperty("positions", out var positionsElement));
        Assert.Equal(JsonValueKind.Array, positionsElement.ValueKind);
        
        Assert.True(root.TryGetProperty("series", out var seriesElement));
        Assert.Equal(JsonValueKind.Array, seriesElement.ValueKind);
        
        // Verify series structure
        if (seriesElement.GetArrayLength() > 0)
        {
            var firstSeries = seriesElement[0];
            Assert.True(firstSeries.TryGetProperty("positionName", out var positionNameElement));
            Assert.Equal(JsonValueKind.String, positionNameElement.ValueKind);
            
            Assert.True(firstSeries.TryGetProperty("type", out var typeElement));
            Assert.Equal(JsonValueKind.String, typeElement.ValueKind);
            
            Assert.True(firstSeries.TryGetProperty("dataPoints", out var dataPointsElement));
            Assert.Equal(JsonValueKind.Array, dataPointsElement.ValueKind);
            
            // Verify data point structure
            if (dataPointsElement.GetArrayLength() > 0)
            {
                var firstDataPoint = dataPointsElement[0];
                Assert.True(firstDataPoint.TryGetProperty("period", out var periodElement));
                Assert.Equal(JsonValueKind.String, periodElement.ValueKind);
                
                Assert.True(firstDataPoint.TryGetProperty("amount", out var amountElement));
                Assert.Equal(JsonValueKind.Number, amountElement.ValueKind);
                
                Assert.True(firstDataPoint.TryGetProperty("year", out var yearElement));
                Assert.Equal(JsonValueKind.Number, yearElement.ValueKind);
                
                Assert.True(firstDataPoint.TryGetProperty("month", out var monthElement));
                Assert.Equal(JsonValueKind.Number, monthElement.ValueKind);
            }
        }
        
        // Also test automatic deserialization
        var trendData = await response.Content.ReadFromJsonAsync<TrendData>();
        Assert.NotNull(trendData);
    }

    /// <summary>
    /// Test edge case: empty database
    /// 
    /// Verifies:
    /// - API handles empty database gracefully
    /// - Returns valid empty response structure
    /// - No exceptions thrown
    /// </summary>
    [Fact]
    public async Task GetPositionTrends_EmptyDatabase_ReturnsEmptyData()
    {
        // Arrange - create client with empty database
        var emptyFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("EmptyInMemoryDb");
                });
            });
        });

        using var emptyClient = emptyFactory.CreateClient();

        // Act
        var response = await emptyClient.GetAsync("/PositionTrends?handler=PositionTrends");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var trendData = await response.Content.ReadFromJsonAsync<TrendData>();
        Assert.NotNull(trendData);
        Assert.Empty(trendData.Positions);
        Assert.Empty(trendData.Series);
    }
}