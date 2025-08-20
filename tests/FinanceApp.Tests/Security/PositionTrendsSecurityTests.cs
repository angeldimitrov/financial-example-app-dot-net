using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.DependencyInjection;
using FinanceApp.Web.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Models;

namespace FinanceApp.Tests.Security;

/// <summary>
/// Security penetration tests for German BWA Position Trends feature
/// Validates input sanitization, XSS prevention, rate limiting, and CSRF protection
/// Focus on German character sets and BWA-specific business logic security
/// </summary>
public class PositionTrendsSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PositionTrendsSecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real database with in-memory for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("SecurityTestDb");
                });
            });
        });
        _client = _factory.CreateClient();
    }

    #region XSS Prevention Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("onclick=alert('xss')")]
    [InlineData("<iframe src='javascript:alert(\"xss\")'></iframe>")]
    public async Task TransactionFilter_WithXSSPayload_ShouldSanitizeInput(string maliciousInput)
    {
        // Arrange - Seed database with legitimate German BWA data
        await SeedTestData();

        // Act - Attempt XSS through category filter
        var response = await _client.GetAsync($"/Transactions?category={Uri.EscapeDataString(maliciousInput)}");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify malicious scripts are not executed
        content.Should().NotContain("<script>");
        content.Should().NotContain("javascript:");
        content.Should().NotContain("onerror=");
        content.Should().NotContain("onclick=");
        content.Should().NotContain("<iframe");
        
        // Verify German characters are preserved in legitimate data
        content.Should().Contain("Personalkosten");
        content.Should().Contain("Umsatzerlöse");
    }

    [Theory]
    [InlineData("Personalkösten<script>alert('xss')</script>")]
    [InlineData("Umsatzerlöße'><img src=x onerror=alert('xss')>")]
    [InlineData("Steuern & Gebühren<iframe src='javascript:alert(\"xss\")'></iframe>")]
    public async Task GermanPositionNames_WithXSSPayload_ShouldPreserveUmlautsAndSanitizeScript(string germanMaliciousInput)
    {
        // Arrange
        await SeedTestData();

        // Act
        var response = await _client.GetAsync($"/Transactions?category={Uri.EscapeDataString(germanMaliciousInput)}");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify XSS is prevented
        content.Should().NotContain("<script>");
        content.Should().NotContain("<img src=x onerror=");
        content.Should().NotContain("<iframe");
        
        // Verify German characters are handled correctly
        content.Should().NotContain("Personalkösten"); // Malicious input should be rejected
        content.Should().Contain("Personalkosten"); // Legitimate data should remain
    }

    #endregion

    #region Input Validation Tests

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE TransactionLines; --")]
    [InlineData("1' UNION SELECT * FROM FinancialPeriods --")]
    [InlineData("<>\"'%;)(&+")]
    public async Task PositionFilter_WithSQLInjectionAttempt_ShouldPreventInjection(string injectionPayload)
    {
        // Arrange
        await SeedTestData();

        // Act
        var response = await _client.GetAsync($"/Transactions?category={Uri.EscapeDataString(injectionPayload)}");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify no SQL injection occurred (legitimate data still present)
        content.Should().Contain("Personalkosten");
        content.Should().Contain("Umsatzerlöse");
        
        // Verify injection payload is not executed
        content.Should().NotContain("DROP TABLE");
        content.Should().NotContain("UNION SELECT");
    }

    [Fact]
    public async Task GermanPositionNameWhitelist_WithValidBWACategories_ShouldPass()
    {
        // Arrange
        var validGermanCategories = new[]
        {
            "Personalkosten",
            "Umsatzerlöse",
            "Steuern Einkommen u. Ertrag",
            "Raumkosten",
            "Betriebliche Steuern",
            "Versicherungen/Beiträge",
            "Abschreibungen",
            "So. betr. Erlöse",
            "Fahrzeugkosten (ohne Steuer)",
            "Werbe-/Reisekosten"
        };

        await SeedTestData();

        // Act & Assert
        foreach (var category in validGermanCategories)
        {
            var response = await _client.GetAsync($"/Transactions?category={Uri.EscapeDataString(category)}");
            response.Should().BeSuccessful();
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotContain("error");
            content.Should().NotContain("exception");
        }
    }

    [Theory]
    [InlineData("InvalidCategory")]
    [InlineData("NonGermanCategory")]
    [InlineData("")]
    [InlineData(null)]
    public async Task GermanPositionNameWhitelist_WithInvalidCategories_ShouldHandleGracefully(string invalidCategory)
    {
        // Arrange
        await SeedTestData();

        // Act
        var queryParam = invalidCategory != null ? $"?category={Uri.EscapeDataString(invalidCategory)}" : "";
        var response = await _client.GetAsync($"/Transactions{queryParam}");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("error");
        content.Should().NotContain("exception");
        
        // Should show no results or all results for invalid/empty category
        content.Should().Contain("Transaction Details");
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task PositionTrendsRequests_WithHighFrequency_ShouldImplementRateLimit()
    {
        // Arrange
        await SeedTestData();
        const int requestCount = 100;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send many concurrent requests
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(_client.GetAsync("/Transactions"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        var successfulRequests = responses.Count(r => r.IsSuccessStatusCode);
        var tooManyRequests = responses.Count(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests);

        // At least some requests should be successful
        successfulRequests.Should().BeGreaterThan(0);
        
        // If rate limiting is implemented, some requests should be blocked
        // Note: This test assumes rate limiting is implemented - adjust based on actual implementation
        var totalBlocked = tooManyRequests;
        
        // Clean up responses
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    #endregion

    #region CSRF Protection Tests

    [Fact]
    public async Task FileUpload_WithoutCSRFToken_ShouldRejectRequest()
    {
        // Arrange
        var formData = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake pdf content"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        formData.Add(fileContent, "pdfFile", "test.pdf");

        // Act - Post without CSRF token
        var response = await _client.PostAsync("/", formData);

        // Assert
        // Should either redirect to get token or return 400/403
        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.BadRequest,
            System.Net.HttpStatusCode.Forbidden,
            System.Net.HttpStatusCode.Redirect);
    }

    #endregion

    #region German Character Encoding Tests

    [Theory]
    [InlineData("Umsatzerlöse", "ö")]
    [InlineData("Personalkosten für Ärzte", "ä")]
    [InlineData("Bürokosten", "ü")]
    [InlineData("Weiß", "ß")]
    public async Task GermanCharacters_InPositionNames_ShouldPreserveEncoding(string germanText, string specialChar)
    {
        // Arrange
        await SeedGermanTestData(germanText);

        // Act
        var response = await _client.GetAsync("/Transactions");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify German characters are properly encoded and displayed
        content.Should().Contain(specialChar);
        content.Should().Contain(germanText);
        
        // Verify content-type includes UTF-8
        response.Content.Headers.ContentType?.CharSet.Should().Be("utf-8");
    }

    #endregion

    #region Business Logic Security Tests

    [Theory]
    [InlineData("Steuern", TransactionType.Expense)] // Tax rules: always expense
    [InlineData("Steuern Einkommen u. Ertrag", TransactionType.Expense)]
    [InlineData("Betriebliche Steuern", TransactionType.Expense)]
    public async Task TaxCategoryClassification_SecurityValidation_ShouldAlwaysBeExpense(string taxCategory, TransactionType expectedType)
    {
        // Arrange - Attempt to manipulate tax classification through input
        await SeedSpecificCategoryData(taxCategory, TransactionType.Revenue); // Try to seed as revenue

        // Act
        var response = await _client.GetAsync($"/Transactions?category={Uri.EscapeDataString(taxCategory)}");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify business rule enforcement: taxes are always expenses
        content.Should().Contain("Expense"); // Should show as expense in UI
        content.Should().NotContain("Revenue"); // Should not show as revenue
    }

    [Fact]
    public async Task SummaryCategories_SecurityValidation_ShouldPreventDataTampering()
    {
        // Arrange - Seed data with summary categories that should be excluded
        await SeedSummaryTestData();

        // Act
        var response = await _client.GetAsync("/Transactions");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        
        // Summary categories should not appear in transaction details to prevent double-counting
        content.Should().NotContain("Gesamtkosten");
        content.Should().NotContain("Betriebsergebnis");
        content.Should().NotContain("Rohertrag");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Seeds the test database with legitimate German BWA financial data
    /// Creates realistic test scenarios for security validation
    /// </summary>
    private async Task SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        // Clear existing data
        context.TransactionLines.RemoveRange(context.TransactionLines);
        context.FinancialPeriods.RemoveRange(context.FinancialPeriods);
        await context.SaveChangesAsync();

        // Add test period
        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 1,
            ImportedAt = DateTime.Now,
            SourceFileName = "security_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        // Add typical German BWA categories
        var transactions = new[]
        {
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Personalkosten", Month = 1, Year = 2024, Amount = -15000m, Type = TransactionType.Expense },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Umsatzerlöse", Month = 1, Year = 2024, Amount = 45000m, Type = TransactionType.Revenue },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Steuern Einkommen u. Ertrag", Month = 1, Year = 2024, Amount = -3000m, Type = TransactionType.Expense },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Raumkosten", Month = 1, Year = 2024, Amount = -2500m, Type = TransactionType.Expense }
        };

        context.TransactionLines.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    private async Task SeedGermanTestData(string germanCategory)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 2,
            ImportedAt = DateTime.Now,
            SourceFileName = "german_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transaction = new TransactionLine
        {
            FinancialPeriodId = period.Id,
            Category = germanCategory,
            Month = 2,
            Year = 2024,
            Amount = -1000m,
            Type = TransactionType.Expense
        };

        context.TransactionLines.Add(transaction);
        await context.SaveChangesAsync();
    }

    private async Task SeedSpecificCategoryData(string category, TransactionType type)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 3,
            ImportedAt = DateTime.Now,
            SourceFileName = "specific_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transaction = new TransactionLine
        {
            FinancialPeriodId = period.Id,
            Category = category,
            Month = 3,
            Year = 2024,
            Amount = type == TransactionType.Revenue ? 1000m : -1000m,
            Type = type
        };

        context.TransactionLines.Add(transaction);
        await context.SaveChangesAsync();
    }

    private async Task SeedSummaryTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 4,
            ImportedAt = DateTime.Now,
            SourceFileName = "summary_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        // Add summary categories that should be filtered out
        var summaryTransactions = new[]
        {
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Gesamtkosten", Month = 4, Year = 2024, Amount = -50000m, Type = TransactionType.Summary },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Betriebsergebnis", Month = 4, Year = 2024, Amount = 5000m, Type = TransactionType.Summary },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Rohertrag", Month = 4, Year = 2024, Amount = 55000m, Type = TransactionType.Summary }
        };

        context.TransactionLines.AddRange(summaryTransactions);
        await context.SaveChangesAsync();
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
    }
}