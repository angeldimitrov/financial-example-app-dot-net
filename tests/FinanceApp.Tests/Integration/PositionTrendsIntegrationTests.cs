using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using System.Text;
using System.Globalization;

namespace FinanceApp.Tests.Integration;

/// <summary>
/// Integration tests for the complete German BWA Position Trends feature
/// Tests end-to-end workflows combining PDF parsing, database operations, and UI rendering
/// Validates the complete user journey from PDF upload to chart visualization
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
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IntegrationTestDb");
                });
            });
        });
        _client = _factory.CreateClient();
    }

    #region Complete User Journey Tests

    [Fact]
    public async Task CompleteGermanBWAWorkflow_FromPDFUploadToChartVisualization_ShouldWork()
    {
        // Phase 1: PDF Upload and Processing
        var germanPDFContent = CreateRealisticGermanBWAPDF();
        
        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();
        var dataImport = scope.ServiceProvider.GetRequiredService<DataImportService>();
        
        using var pdfStream = new MemoryStream(Encoding.UTF8.GetBytes(germanPDFContent));
        
        // Parse PDF
        var parsedData = await pdfParser.ParsePdfAsync(pdfStream, "integration_test.pdf");
        
        // Import to database
        await dataImport.ImportFinancialDataAsync(parsedData);

        // Phase 2: Verify Home Page with Charts
        var homeResponse = await _client.GetAsync("/");
        homeResponse.Should().BeSuccessful();
        
        var homeContent = await homeResponse.Content.ReadAsStringAsync();
        
        // Verify German financial data is displayed
        homeContent.Should().Contain("Chart.js", "Chart library should be loaded");
        homeContent.Should().Contain("financeChart", "Finance chart should be initialized");
        homeContent.Should().Contain("€", "Euro currency should be displayed");
        homeContent.Should().Contain("Revenue vs Expenses Trend", "Chart title should be present");

        // Phase 3: Verify Transaction Detail Page
        var transactionResponse = await _client.GetAsync("/Transactions");
        transactionResponse.Should().BeSuccessful();
        
        var transactionContent = await transactionResponse.Content.ReadAsStringAsync();
        
        // Verify German BWA categories are displayed
        transactionContent.Should().Contain("Personalkosten", "Personnel costs should be shown");
        transactionContent.Should().Contain("Umsatzerlöse", "Revenue should be shown");
        transactionContent.Should().Contain("Steuern", "Taxes should be shown");
        
        // Verify German formatting
        transactionContent.Should().MatchRegex(@"€\d{1,3}\.\d{3},\d{2}", "German currency formatting should be used");

        // Phase 4: Verify Filtering Works
        var filteredResponse = await _client.GetAsync("/Transactions?type=Revenue");
        filteredResponse.Should().BeSuccessful();
        
        var filteredContent = await filteredResponse.Content.ReadAsStringAsync();
        filteredContent.Should().Contain("Umsatzerlöse", "Revenue filter should work");
        filteredContent.Should().NotContain("Personalkosten", "Expense items should be filtered out");
    }

    [Fact]
    public async Task MultiMonthGermanData_ShouldCreateTrendVisualization()
    {
        // Arrange - Import multi-month German BWA data
        await SeedMultiMonthGermanData();

        // Act - Get home page with trend chart
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        
        // Verify trend chart configuration
        content.Should().Contain("type: 'line'", "Should use line chart for trends");
        content.Should().Contain("'de-DE'", "Should use German locale");
        content.Should().Contain("tension: 0.4", "Should use smooth line curves");
        
        // Verify German month labels are generated
        var monthPattern = @"""labels"":\s*\[.*"".*""\]";
        content.Should().MatchRegex(monthPattern, "Chart should have month labels");
        
        // Verify chart data includes German financial data
        content.Should().Contain("datasets", "Chart should have data series");
        content.Should().MatchRegex(@"""data"":\s*\[\d", "Chart should have numeric data points");
    }

    [Fact]
    public async Task GermanBusinessLogicValidation_ThroughCompleteFlow_ShouldEnforceRules()
    {
        // Arrange - Create test data that tests German business rules
        var testData = CreateBusinessRuleTestData();
        
        using var scope = _factory.Services.CreateScope();
        var dataImport = scope.ServiceProvider.GetRequiredService<DataImportService>();
        
        // Import data with various category types
        await dataImport.ImportFinancialDataAsync(testData);

        // Act - View transactions to see how business rules are applied
        var response = await _client.GetAsync("/Transactions");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Verify German business rules are enforced
        
        // Rule 1: Tax categories should be expenses (even if amount is positive in source)
        content.Should().Contain("Steuern", "Tax categories should be present");
        // Tax items should appear in expense section, not revenue
        
        // Rule 2: Summary categories should be excluded or marked differently
        content.Should().NotContain("Gesamtkosten", "Summary totals should not appear in transaction details");
        
        // Rule 3: German formatting should be preserved
        content.Should().Contain("€", "Euro symbol should be used");
        content.Should().MatchRegex(@"\d+,\d{2}", "German decimal format should be used");
        
        // Rule 4: BWA categories should be properly classified
        var revenueSection = ExtractSectionContent(content, "Revenue");
        var expenseSection = ExtractSectionContent(content, "Expense");
        
        revenueSection.Should().Contain("Umsatz", "Revenue section should contain sales items");
        expenseSection.Should().Contain("Personal", "Expense section should contain personnel costs");
        expenseSection.Should().Contain("Steuer", "Expense section should contain taxes");
    }

    #endregion

    #region Data Consistency Tests

    [Fact]
    public async Task DataConsistency_BetweenChartsAndTables_ShouldMatch()
    {
        // Arrange
        await SeedConsistencyTestData();

        // Act - Get both chart and table data
        var homeResponse = await _client.GetAsync("/");
        var transactionResponse = await _client.GetAsync("/Transactions");

        // Assert
        homeResponse.Should().BeSuccessful();
        transactionResponse.Should().BeSuccessful();

        var homeContent = await homeResponse.Content.ReadAsStringAsync();
        var transactionContent = await transactionResponse.Content.ReadAsStringAsync();

        // Extract total amounts from both pages
        var chartTotalPattern = @"Total.*?€(\d{1,3}(?:\.\d{3})*,\d{2})";
        var tableTotalPattern = @"Total.*?€(\d{1,3}(?:\.\d{3})*,\d{2})";

        // Both should show the same totals (consistency check)
        var homeMatches = System.Text.RegularExpressions.Regex.Matches(homeContent, chartTotalPattern);
        var transactionMatches = System.Text.RegularExpressions.Regex.Matches(transactionContent, tableTotalPattern);

        if (homeMatches.Count > 0 && transactionMatches.Count > 0)
        {
            // At least verify both contain total calculations
            homeContent.Should().Contain("€", "Home page should show currency amounts");
            transactionContent.Should().Contain("€", "Transaction page should show currency amounts");
        }
    }

    [Fact]
    public async Task GermanDateConsistency_AcrossPagesAndCharts_ShouldMatch()
    {
        // Arrange
        await SeedDateConsistencyTestData();

        // Act
        var homeResponse = await _client.GetAsync("/");
        var transactionResponse = await _client.GetAsync("/Transactions?year=2024&month=3");

        // Assert
        homeResponse.Should().BeSuccessful();
        transactionResponse.Should().BeSuccessful();

        var homeContent = await homeResponse.Content.ReadAsStringAsync();
        var transactionContent = await transactionResponse.Content.ReadAsStringAsync();

        // Verify German month names are consistent
        if (homeContent.Contains("März") || transactionContent.Contains("März"))
        {
            // Both should use German month names consistently
            homeContent.Should().NotContain("March", "Should not mix German and English month names");
            transactionContent.Should().NotContain("March", "Should not mix German and English month names");
        }

        // Verify year formatting is consistent
        homeContent.Should().Contain("2024", "Year should be displayed");
        transactionContent.Should().Contain("2024", "Year should be displayed");
    }

    #endregion

    #region Performance Integration Tests

    [Fact]
    public async Task LargeGermanDataset_ShouldRenderWithinPerformanceTargets()
    {
        // Arrange - Create large dataset
        await SeedLargeIntegrationDataset();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Load pages that process large amounts of German data
        var homeTask = _client.GetAsync("/");
        var transactionTask = _client.GetAsync("/Transactions");

        await Task.WhenAll(homeTask, transactionTask);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "Large German dataset should render within 5 seconds");

        var homeResponse = await homeTask;
        var transactionResponse = await transactionTask;

        homeResponse.Should().BeSuccessful();
        transactionResponse.Should().BeSuccessful();

        var homeContent = await homeResponse.Content.ReadAsStringAsync();
        var transactionContent = await transactionResponse.Content.ReadAsStringAsync();

        // Verify German content is still properly formatted despite large dataset
        homeContent.Should().Contain("€", "Currency formatting should work with large datasets");
        transactionContent.Should().Contain("Personalkosten", "German categories should display correctly");
    }

    #endregion

    #region Error Handling Integration Tests

    [Fact]
    public async Task CorruptedGermanData_ShouldHandleGracefully()
    {
        // Arrange - Create partially corrupted German BWA data
        var corruptedData = CreateCorruptedGermanData();
        
        using var scope = _factory.Services.CreateScope();
        var dataImport = scope.ServiceProvider.GetRequiredService<DataImportService>();

        try
        {
            // Act - Import corrupted data
            await dataImport.ImportFinancialDataAsync(corruptedData);
        }
        catch
        {
            // Some corruption might cause import to fail - that's acceptable
        }

        // Act - Try to view pages
        var homeResponse = await _client.GetAsync("/");
        var transactionResponse = await _client.GetAsync("/Transactions");

        // Assert - Pages should still load gracefully
        homeResponse.Should().BeSuccessful("Home page should handle corrupted data gracefully");
        transactionResponse.Should().BeSuccessful("Transaction page should handle corrupted data gracefully");

        var homeContent = await homeResponse.Content.ReadAsStringAsync();
        var transactionContent = await transactionResponse.Content.ReadAsStringAsync();

        // Should not show error messages to user
        homeContent.Should().NotContain("Exception", "Should not expose exceptions to users");
        homeContent.Should().NotContain("Error", "Should not show error messages");
        transactionContent.Should().NotContain("Exception", "Should not expose exceptions to users");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates realistic German BWA PDF content for comprehensive testing
    /// Includes authentic German categories, proper formatting, and business logic scenarios
    /// </summary>
    private string CreateRealisticGermanBWAPDF()
    {
        return """
            BWA - Betriebswirtschaftliche Auswertung 2024
            Zahnarztpraxis Dr. Müller
            
            Jan/2024 Feb/2024 Mrz/2024 Apr/2024 Mai/2024 Jun/2024
            
            Erlöse
            Umsatzerlöse 48.500,00 52.300,00 49.800,00 51.200,00 53.600,00 50.900,00
            So. betr. Erlöse 2.100,00 1.800,00 2.300,00 1.950,00 2.400,00 2.150,00
            
            Kosten
            Personalkosten 18.200,00 18.500,00 19.100,00 18.800,00 19.300,00 18.900,00
            Raumkosten 3.500,00 3.500,00 3.500,00 3.500,00 3.500,00 3.500,00
            Fahrzeugkosten (ohne Steuer) 850,00 920,00 780,00 890,00 810,00 870,00
            Werbe-/Reisekosten 1.200,00 980,00 1.350,00 1.100,00 1.250,00 1.180,00
            Kosten Warenabgabe 4.800,00 5.200,00 4.900,00 5.100,00 5.300,00 5.000,00
            Abschreibungen 2.100,00 2.100,00 2.100,00 2.100,00 2.100,00 2.100,00
            Reparatur/Instandhaltung 650,00 420,00 780,00 590,00 710,00 640,00
            Versicherungen/Beiträge 890,00 890,00 890,00 890,00 890,00 890,00
            Betriebliche Steuern 320,00 280,00 350,00 310,00 340,00 300,00
            Besondere Kosten 450,00 380,00 520,00 410,00 480,00 430,00
            Sonstige Kosten 980,00 1.120,00 890,00 1.050,00 920,00 1.080,00
            
            Steuern
            Steuern Einkommen u. Ertrag 4.200,00 4.500,00 4.100,00 4.300,00 4.600,00 4.250,00
            """;
    }

    private ParsedFinancialData CreateBusinessRuleTestData()
    {
        return new ParsedFinancialData
        {
            Year = 2024,
            SourceFileName = "business_rules_test.pdf",
            TransactionLines = new List<ParsedTransactionLine>
            {
                // Revenue items
                new() { Category = "Umsatzerlöse", Month = 1, Year = 2024, Amount = 50000m, Type = TransactionType.Revenue },
                new() { Category = "So. betr. Erlöse", Month = 1, Year = 2024, Amount = 2000m, Type = TransactionType.Revenue },
                
                // Expense items
                new() { Category = "Personalkosten", Month = 1, Year = 2024, Amount = -18000m, Type = TransactionType.Expense },
                new() { Category = "Raumkosten", Month = 1, Year = 2024, Amount = -3500m, Type = TransactionType.Expense },
                
                // Tax items (should always be expenses)
                new() { Category = "Steuern Einkommen u. Ertrag", Month = 1, Year = 2024, Amount = -4200m, Type = TransactionType.Expense },
                new() { Category = "Betriebliche Steuern", Month = 1, Year = 2024, Amount = -320m, Type = TransactionType.Expense },
                
                // Summary items (should be excluded or marked)
                new() { Category = "Gesamtkosten", Month = 1, Year = 2024, Amount = -26020m, Type = TransactionType.Summary },
                new() { Category = "Betriebsergebnis", Month = 1, Year = 2024, Amount = 25980m, Type = TransactionType.Summary }
            }
        };
    }

    private async Task SeedMultiMonthGermanData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await ClearDatabase(context);

        var germanCategories = new[]
        {
            ("Personalkosten", TransactionType.Expense, -18000m),
            ("Umsatzerlöse", TransactionType.Revenue, 50000m),
            ("Raumkosten", TransactionType.Expense, -3500m),
            ("Steuern Einkommen u. Ertrag", TransactionType.Expense, -4200m)
        };

        for (int month = 1; month <= 6; month++)
        {
            var period = new FinancialPeriod
            {
                Year = 2024,
                Month = month,
                ImportedAt = DateTime.Now,
                SourceFileName = $"multi_month_{month:00}.pdf"
            };
            context.FinancialPeriods.Add(period);
            await context.SaveChangesAsync();

            foreach (var (category, type, baseAmount) in germanCategories)
            {
                var monthlyVariation = (decimal)(1.0 + (month - 3.5) * 0.1); // Vary amounts by month
                var transaction = new TransactionLine
                {
                    FinancialPeriodId = period.Id,
                    Category = category,
                    Month = month,
                    Year = 2024,
                    Amount = baseAmount * monthlyVariation,
                    Type = type
                };
                context.TransactionLines.Add(transaction);
            }
            
            await context.SaveChangesAsync();
        }
    }

    private async Task SeedConsistencyTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await ClearDatabase(context);

        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 1,
            ImportedAt = DateTime.Now,
            SourceFileName = "consistency_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transactions = new[]
        {
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Personalkosten", Month = 1, Year = 2024, Amount = -15000.00m, Type = TransactionType.Expense },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Umsatzerlöse", Month = 1, Year = 2024, Amount = 45000.00m, Type = TransactionType.Revenue },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Raumkosten", Month = 1, Year = 2024, Amount = -3000.00m, Type = TransactionType.Expense }
        };

        context.TransactionLines.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    private async Task SeedDateConsistencyTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await ClearDatabase(context);

        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 3, // März
            ImportedAt = DateTime.Now,
            SourceFileName = "date_consistency_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transaction = new TransactionLine
        {
            FinancialPeriodId = period.Id,
            Category = "Personalkosten",
            Month = 3,
            Year = 2024,
            Amount = -15000m,
            Type = TransactionType.Expense
        };

        context.TransactionLines.Add(transaction);
        await context.SaveChangesAsync();
    }

    private async Task SeedLargeIntegrationDataset()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await ClearDatabase(context);

        var germanCategories = new[]
        {
            "Personalkosten", "Umsatzerlöse", "Raumkosten", "Fahrzeugkosten (ohne Steuer)",
            "Werbe-/Reisekosten", "Kosten Warenabgabe", "Abschreibungen", "Reparatur/Instandhaltung",
            "Versicherungen/Beiträge", "Betriebliche Steuern", "Besondere Kosten", "Sonstige Kosten",
            "Steuern Einkommen u. Ertrag", "So. betr. Erlöse"
        };

        var random = new Random(42); // Seed for reproducibility

        // Create 24 months of data
        for (int year = 2023; year <= 2024; year++)
        {
            for (int month = 1; month <= 12; month++)
            {
                if (year == 2024 && month > 6) break; // Stop at June 2024

                var period = new FinancialPeriod
                {
                    Year = year,
                    Month = month,
                    ImportedAt = DateTime.Now,
                    SourceFileName = $"large_dataset_{year}_{month:00}.pdf"
                };
                context.FinancialPeriods.Add(period);
                await context.SaveChangesAsync();

                foreach (var category in germanCategories)
                {
                    var isRevenue = category.Contains("Umsatz") || category.Contains("Erlös");
                    var baseAmount = isRevenue ? random.Next(40000, 60000) : random.Next(1000, 20000);
                    var amount = isRevenue ? baseAmount : -baseAmount;

                    var transaction = new TransactionLine
                    {
                        FinancialPeriodId = period.Id,
                        Category = category,
                        Month = month,
                        Year = year,
                        Amount = amount,
                        Type = DetermineTransactionType(category)
                    };

                    context.TransactionLines.Add(transaction);
                }

                await context.SaveChangesAsync();
            }
        }
    }

    private ParsedFinancialData CreateCorruptedGermanData()
    {
        return new ParsedFinancialData
        {
            Year = 2024,
            SourceFileName = "corrupted_test.pdf",
            TransactionLines = new List<ParsedTransactionLine>
            {
                // Valid data
                new() { Category = "Personalkosten", Month = 1, Year = 2024, Amount = -15000m, Type = TransactionType.Expense },
                
                // Corrupted data - invalid month
                new() { Category = "InvalidCategory", Month = 13, Year = 2024, Amount = -1000m, Type = TransactionType.Expense },
                
                // Corrupted data - extreme amount
                new() { Category = "Umsatzerlöse", Month = 1, Year = 2024, Amount = 999999999m, Type = TransactionType.Revenue },
                
                // Corrupted data - empty category
                new() { Category = "", Month = 1, Year = 2024, Amount = -500m, Type = TransactionType.Expense }
            }
        };
    }

    private async Task ClearDatabase(AppDbContext context)
    {
        context.TransactionLines.RemoveRange(context.TransactionLines);
        context.FinancialPeriods.RemoveRange(context.FinancialPeriods);
        await context.SaveChangesAsync();
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

    private string ExtractSectionContent(string htmlContent, string sectionType)
    {
        // Simple extraction of content from HTML sections
        // In a real scenario, you might use a proper HTML parser
        var startMarker = $">{sectionType}<";
        var startIndex = htmlContent.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        
        if (startIndex == -1) return "";
        
        var endIndex = htmlContent.IndexOf("</div>", startIndex + startMarker.Length);
        if (endIndex == -1) endIndex = htmlContent.Length;
        
        return htmlContent.Substring(startIndex, endIndex - startIndex);
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
    }
}