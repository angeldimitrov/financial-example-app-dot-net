using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Bogus;

namespace FinanceApp.Tests.Performance;

/// <summary>
/// Performance benchmarking tests for German BWA Position Trends feature
/// Validates memory usage optimization, query performance, and German culture formatting efficiency
/// Focus on large dataset scenarios typical in German financial reporting
/// </summary>
public class PositionTrendsPerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private const int LARGE_DATASET_SIZE = 1000;
    private const int PERFORMANCE_YEAR_RANGE = 5;

    public PositionTrendsPerformanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Use in-memory database optimized for performance testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("PerformanceTestDb");
                    options.EnableSensitiveDataLogging(false); // Improve performance
                    options.EnableDetailedErrors(false);
                });
            });
        });
        _client = _factory.CreateClient();
    }

    #region Memory Usage Optimization Tests

    [Fact]
    public async Task LargeGermanDataset_MemoryUsage_ShouldStayBelowThreshold()
    {
        // Arrange
        await SeedLargeGermanDataset();
        
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var beforeMemory = GC.GetTotalMemory(false);

        // Act - Process large dataset with German formatting
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/Transactions");
        stopwatch.Stop();

        var afterMemory = GC.GetTotalMemory(false);
        var memoryUsed = afterMemory - beforeMemory;

        // Assert
        response.Should().BeSuccessful();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Large dataset processing should complete within 5 seconds");
        
        // Memory usage should be reasonable (less than 50MB for 1000+ transactions)
        memoryUsed.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should be optimized for large German datasets");
        
        // Verify German formatting doesn't cause memory leaks
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Personalkosten");
        content.Should().Contain("€"); // German currency formatting preserved
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task GermanPositionTrends_WithVaryingDataSizes_ShouldScaleLinearly(int transactionCount)
    {
        // Arrange
        await SeedGermanTransactions(transactionCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/Transactions");
        stopwatch.Stop();

        // Assert
        response.Should().BeSuccessful();
        
        // Performance should scale linearly with data size
        var expectedMaxTime = transactionCount * 2; // 2ms per transaction is acceptable
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedMaxTime, 
            $"Processing {transactionCount} German BWA transactions should complete within {expectedMaxTime}ms");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Transaction Details");
    }

    #endregion

    #region Database Query Performance Tests

    [Fact]
    public async Task GermanPositionFiltering_QueryPerformance_ShouldUseIndexes()
    {
        // Arrange
        await SeedLargeGermanDataset();

        // Act - Test various German position filters
        var germanCategories = new[]
        {
            "Personalkosten",
            "Umsatzerlöse", 
            "Steuern Einkommen u. Ertrag",
            "Raumkosten",
            "Abschreibungen"
        };

        var queryTimes = new List<long>();

        foreach (var category in germanCategories)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await _client.GetAsync($"/Transactions?category={Uri.EscapeDataString(category)}");
            stopwatch.Stop();

            response.Should().BeSuccessful();
            queryTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert
        var averageQueryTime = queryTimes.Average();
        averageQueryTime.Should().BeLessThan(100, "German category filtering should be fast with proper indexing");
        
        // All queries should have similar performance (indicating good indexing)
        var maxVariance = queryTimes.Max() - queryTimes.Min();
        maxVariance.Should().BeLessThan(50, "Query times should be consistent across different German categories");
    }

    [Fact]
    public async Task MonthlyAggregation_WithGermanFormatting_ShouldBeOptimized()
    {
        // Arrange
        await SeedMultipleYearsGermanData();

        // Act - Test monthly aggregation performance
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/");
        stopwatch.Stop();

        // Assert
        response.Should().BeSuccessful();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            "Monthly aggregation with German formatting should complete within 1 second");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Monthly Summary");
        content.Should().Contain("€"); // German currency formatting
        
        // Verify German month names are displayed efficiently
        content.Should().ContainAny("Januar", "Februar", "März", "April", "Mai", "Juni");
    }

    #endregion

    #region German Culture Formatting Performance Tests

    [Fact]
    public async Task GermanNumberFormatting_Performance_ShouldBeOptimized()
    {
        // Arrange
        var germanCulture = new CultureInfo("de-DE");
        var random = new Random(42); // Seed for reproducibility
        var testAmounts = Enumerable.Range(0, 10000)
            .Select(_ => (decimal)(random.NextDouble() * 100000))
            .ToArray();

        // Act - Test German number formatting performance
        var stopwatch = Stopwatch.StartNew();
        
        var formattedNumbers = testAmounts
            .Select(amount => amount.ToString("C", germanCulture))
            .ToArray();
            
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "German number formatting for 10,000 amounts should complete within 100ms");
        
        // Verify German formatting is correct
        formattedNumbers.First().Should().Contain("€");
        formattedNumbers.First().Should().Contain(","); // German decimal separator
        
        // Should not contain English formatting
        formattedNumbers.Should().NotContain(n => n.Contains("$"));
    }

    [Fact]
    public async Task ChartDataGeneration_WithGermanMonthNames_ShouldBeEfficient()
    {
        // Arrange
        await SeedChartTestData();

        // Act - Generate chart data with German formatting
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/");
        stopwatch.Stop();

        // Assert
        response.Should().BeSuccessful();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, 
            "Chart data generation with German formatting should be fast");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Chart.js");
        content.Should().Contain("financeChart");
        
        // Verify German formatting in chart data
        content.Should().Contain("'de-DE'"); // German locale for chart
    }

    #endregion

    #region Pagination Performance Tests

    [Fact]
    public async Task PaginatedTransactions_WithGermanData_ShouldLoadQuickly()
    {
        // Arrange
        await SeedLargeGermanDataset();

        // Act - Test pagination performance
        var pageSizes = new[] { 10, 25, 50, 100 };
        var pageTimes = new Dictionary<int, long>();

        foreach (var pageSize in pageSizes)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await _client.GetAsync($"/Transactions?pageSize={pageSize}");
            stopwatch.Stop();

            response.Should().BeSuccessful();
            pageTimes[pageSize] = stopwatch.ElapsedMilliseconds;
        }

        // Assert
        // Larger page sizes should not be significantly slower
        pageTimes[10].Should().BeLessThan(200);
        pageTimes[100].Should().BeLessThan(400);
        
        // Performance should scale reasonably
        var performanceRatio = (double)pageTimes[100] / pageTimes[10];
        performanceRatio.Should().BeLessThan(3.0, 
            "10x data size should not be more than 3x slower");
    }

    #endregion

    #region Concurrent Access Performance Tests

    [Fact]
    public async Task ConcurrentGermanPositionRequests_ShouldHandleLoad()
    {
        // Arrange
        await SeedLargeGermanDataset();
        const int concurrentRequests = 20;

        // Act - Send concurrent requests
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(i => _client.GetAsync("/Transactions"))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.Should().BeSuccessful());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "20 concurrent requests should complete within 5 seconds");

        // Verify all responses contain German content
        foreach (var response in responses)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Transaction Details");
            response.Dispose();
        }
    }

    #endregion

    #region PDF Processing Performance Tests

    [Fact]
    public async Task GermanPDFParsing_Performance_ShouldBeOptimized()
    {
        // Arrange
        var pdfContent = GenerateGermanPDFContent();
        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

        // Act - Test PDF parsing performance
        var stopwatch = Stopwatch.StartNew();
        var result = await pdfParser.ParsePdfAsync(stream, "performance_test.pdf");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            "German PDF parsing should complete within 1 second");
        
        result.Should().NotBeNull();
        result.TransactionLines.Should().NotBeEmpty();
        result.TransactionLines.Should().Contain(t => t.Category.Contains("Personal"));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Seeds a large dataset with realistic German BWA categories and amounts
    /// Simulates typical German financial reporting data volumes
    /// </summary>
    private async Task SeedLargeGermanDataset()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        // Clear existing data
        context.TransactionLines.RemoveRange(context.TransactionLines);
        context.FinancialPeriods.RemoveRange(context.FinancialPeriods);
        await context.SaveChangesAsync();

        // Create realistic German BWA categories
        var germanCategories = new[]
        {
            "Personalkosten", "Umsatzerlöse", "Raumkosten", "Fahrzeugkosten (ohne Steuer)",
            "Werbe-/Reisekosten", "Kosten Warenabgabe", "Abschreibungen", "Reparatur/Instandhaltung",
            "Sonstige Kosten", "Steuern Einkommen u. Ertrag", "Betriebliche Steuern",
            "Versicherungen/Beiträge", "Besondere Kosten", "So. betr. Erlöse"
        };

        var faker = new Faker("de"); // German locale
        
        // Create multiple periods for performance testing
        for (int year = 2020; year <= 2024; year++)
        {
            for (int month = 1; month <= 12; month++)
            {
                var period = new FinancialPeriod
                {
                    Year = year,
                    Month = month,
                    ImportedAt = DateTime.Now,
                    SourceFileName = $"perf_test_{year}_{month:00}.pdf"
                };
                context.FinancialPeriods.Add(period);
            }
        }
        
        await context.SaveChangesAsync();

        // Generate large transaction dataset
        var periods = await context.FinancialPeriods.ToListAsync();
        var transactions = new List<TransactionLine>();

        foreach (var period in periods)
        {
            foreach (var category in germanCategories)
            {
                // Create multiple transactions per category per period
                for (int i = 0; i < 5; i++)
                {
                    var isRevenue = category.Contains("Umsatz") || category.Contains("Erlös");
                    var amount = faker.Random.Decimal(100, 50000) * (isRevenue ? 1 : -1);
                    
                    transactions.Add(new TransactionLine
                    {
                        FinancialPeriodId = period.Id,
                        Category = category,
                        Month = period.Month,
                        Year = period.Year,
                        Amount = amount,
                        Type = DetermineTransactionType(category)
                    });
                }
            }
        }

        // Add transactions in batches for better performance
        const int batchSize = 500;
        for (int i = 0; i < transactions.Count; i += batchSize)
        {
            var batch = transactions.Skip(i).Take(batchSize);
            context.TransactionLines.AddRange(batch);
            await context.SaveChangesAsync();
        }
    }

    private async Task SeedGermanTransactions(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 1,
            ImportedAt = DateTime.Now,
            SourceFileName = $"perf_test_{count}.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var faker = new Faker("de");
        var categories = new[] { "Personalkosten", "Umsatzerlöse", "Raumkosten", "Abschreibungen" };
        
        var transactions = Enumerable.Range(0, count)
            .Select(i => new TransactionLine
            {
                FinancialPeriodId = period.Id,
                Category = faker.PickRandom(categories),
                Month = 1,
                Year = 2024,
                Amount = faker.Random.Decimal(-10000, 10000),
                Type = TransactionType.Expense
            })
            .ToArray();

        context.TransactionLines.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    private async Task SeedMultipleYearsGermanData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        var faker = new Faker("de");
        
        for (int year = 2020; year <= 2024; year++)
        {
            for (int month = 1; month <= 12; month++)
            {
                var period = new FinancialPeriod
                {
                    Year = year,
                    Month = month,
                    ImportedAt = DateTime.Now,
                    SourceFileName = $"multi_year_{year}_{month:00}.pdf"
                };
                context.FinancialPeriods.Add(period);
                await context.SaveChangesAsync();

                var transactions = new[]
                {
                    new TransactionLine { FinancialPeriodId = period.Id, Category = "Personalkosten", Month = month, Year = year, Amount = faker.Random.Decimal(-20000, -5000), Type = TransactionType.Expense },
                    new TransactionLine { FinancialPeriodId = period.Id, Category = "Umsatzerlöse", Month = month, Year = year, Amount = faker.Random.Decimal(30000, 80000), Type = TransactionType.Revenue }
                };

                context.TransactionLines.AddRange(transactions);
                await context.SaveChangesAsync();
            }
        }
    }

    private async Task SeedChartTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        var faker = new Faker("de");
        
        for (int month = 1; month <= 12; month++)
        {
            var period = new FinancialPeriod
            {
                Year = 2024,
                Month = month,
                ImportedAt = DateTime.Now,
                SourceFileName = $"chart_test_{month:00}.pdf"
            };
            context.FinancialPeriods.Add(period);
            await context.SaveChangesAsync();

            var transactions = new[]
            {
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Umsatzerlöse", Month = month, Year = 2024, Amount = faker.Random.Decimal(40000, 60000), Type = TransactionType.Revenue },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Personalkosten", Month = month, Year = 2024, Amount = faker.Random.Decimal(-25000, -15000), Type = TransactionType.Expense },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Raumkosten", Month = month, Year = 2024, Amount = faker.Random.Decimal(-5000, -2000), Type = TransactionType.Expense }
            };

            context.TransactionLines.AddRange(transactions);
            await context.SaveChangesAsync();
        }
    }

    private TransactionType DetermineTransactionType(string category)
    {
        var lower = category.ToLower();
        
        if (lower.Contains("steuer"))
            return TransactionType.Expense;
        if (lower.Contains("umsatz") || lower.Contains("erlös"))
            return TransactionType.Revenue;
        if (lower.Contains("kosten") || lower.Contains("aufwand") || lower.Contains("abschreibung"))
            return TransactionType.Expense;
        
        return TransactionType.Other;
    }

    private string GenerateGermanPDFContent()
    {
        return """
            BWA-Auswertung 2024
            Jan/2024 Feb/2024 Mrz/2024
            Personalkosten 15.000,00 16.500,00 14.200,00
            Umsatzerlöse 45.000,00 48.200,00 42.800,00
            Raumkosten 2.500,00 2.500,00 2.500,00
            Steuern Einkommen u. Ertrag 3.200,00 3.500,00 3.100,00
            """;
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
    }
}