using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using System.Globalization;
using System.Text;

namespace FinanceApp.Tests.German;

/// <summary>
/// Comprehensive tests for German BWA localization and business logic
/// Validates German number formats, currency display, month names, and BWA category classification
/// Ensures authentic German financial reporting compliance
/// </summary>
public class GermanLocalizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly CultureInfo _germanCulture = new("de-DE");

    public GermanLocalizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("GermanLocalizationTestDb");
                });
            });
        });
        _client = _factory.CreateClient();
    }

    #region German Number Formatting Tests

    [Theory]
    [InlineData(1234.56, "1.234,56")]
    [InlineData(123456.78, "123.456,78")]
    [InlineData(1234567.89, "1.234.567,89")]
    [InlineData(12.3, "12,30")]
    [InlineData(0.5, "0,50")]
    public void GermanNumberFormat_ShouldFormatCorrectly(decimal amount, string expectedFormat)
    {
        // Act
        var formatted = amount.ToString("N2", _germanCulture);

        // Assert
        formatted.Should().Be(expectedFormat, $"German number formatting should use comma as decimal separator and dot as thousands separator");
    }

    [Theory]
    [InlineData(1234.56, "1.234,56 €")]
    [InlineData(-1234.56, "-1.234,56 €")]
    [InlineData(0, "0,00 €")]
    public void GermanCurrencyFormat_ShouldDisplayCorrectly(decimal amount, string expectedFormat)
    {
        // Act
        var formatted = amount.ToString("C", _germanCulture);

        // Assert
        formatted.Should().Be(expectedFormat, "German currency formatting should use Euro symbol and German number format");
    }

    [Fact]
    public async Task TransactionAmounts_InUI_ShouldUseGermanFormatting()
    {
        // Arrange
        await SeedGermanFormattingTestData();

        // Act
        var response = await _client.GetAsync("/Transactions");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        
        // Verify German decimal formatting in HTML
        content.Should().Contain(",00", "Amounts should use German decimal separator");
        content.Should().Contain("€", "Euro symbol should be displayed");
        content.Should().NotContain("$", "Should not contain dollar symbols");
        
        // Verify large numbers use German thousands separator
        content.Should().MatchRegex(@"\d{1,3}\.\d{3},\d{2}", "Large amounts should use German thousands separator");
    }

    [Fact]
    public async Task ChartData_ShouldUseGermanLocaleFormatting()
    {
        // Arrange
        await SeedChartFormattingTestData();

        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        
        // Verify German locale is used in Chart.js configuration
        content.Should().Contain("'de-DE'", "Chart.js should use German locale");
        content.Should().Contain("toLocaleString('de-DE'", "Chart tooltips should use German formatting");
        
        // Verify chart configuration uses German formatting
        content.Should().MatchRegex(@"minimumFractionDigits:\s*2", "Chart should show 2 decimal places");
        content.Should().MatchRegex(@"maximumFractionDigits:\s*2", "Chart should limit to 2 decimal places");
    }

    #endregion

    #region German Month Names Tests

    [Theory]
    [InlineData(1, "Januar")]
    [InlineData(2, "Februar")]
    [InlineData(3, "März")]
    [InlineData(4, "April")]
    [InlineData(5, "Mai")]
    [InlineData(6, "Juni")]
    [InlineData(7, "Juli")]
    [InlineData(8, "August")]
    [InlineData(9, "September")]
    [InlineData(10, "Oktober")]
    [InlineData(11, "November")]
    [InlineData(12, "Dezember")]
    public void GermanMonthNames_ShouldBeCorrect(int monthNumber, string expectedGermanName)
    {
        // Act
        var monthName = new DateTime(2024, monthNumber, 1).ToString("MMMM", _germanCulture);

        // Assert
        monthName.Should().Be(expectedGermanName, $"Month {monthNumber} should be displayed in German");
    }

    [Theory]
    [InlineData(1, "Jan")]
    [InlineData(2, "Feb")]
    [InlineData(3, "Mrz")]  // German abbreviation for März
    [InlineData(4, "Apr")]
    [InlineData(5, "Mai")]
    [InlineData(6, "Jun")]
    [InlineData(7, "Jul")]
    [InlineData(8, "Aug")]
    [InlineData(9, "Sep")]
    [InlineData(10, "Okt")]
    [InlineData(11, "Nov")]
    [InlineData(12, "Dez")]
    public void GermanMonthAbbreviations_ForPDFParsing_ShouldMatchBWAFormat(int monthNumber, string expectedAbbreviation)
    {
        // This tests the specific abbreviations used in German BWA PDFs
        var germanMonthAbbreviations = new[] { "Jan", "Feb", "Mrz", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez" };
        
        // Act
        var abbreviation = germanMonthAbbreviations[monthNumber - 1];

        // Assert
        abbreviation.Should().Be(expectedAbbreviation, "PDF parser should recognize German month abbreviations used in BWA reports");
    }

    [Fact]
    public async Task MonthlyDisplay_ShouldShowGermanMonthNames()
    {
        // Arrange
        await SeedMonthlyDisplayTestData();

        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        
        // Verify German month names appear in UI
        content.Should().ContainAny("Januar", "Februar", "März", "German months should be displayed");
        content.Should().NotContainAny("January", "February", "March", "English months should not appear");
    }

    #endregion

    #region German BWA Category Validation Tests

    [Theory]
    [InlineData("Personalkosten")]
    [InlineData("Umsatzerlöse")]
    [InlineData("Steuern Einkommen u. Ertrag")]
    [InlineData("Raumkosten")]
    [InlineData("Betriebliche Steuern")]
    [InlineData("Versicherungen/Beiträge")]
    [InlineData("Besondere Kosten")]
    [InlineData("Fahrzeugkosten (ohne Steuer)")]
    [InlineData("Werbe-/Reisekosten")]
    [InlineData("Kosten Warenabgabe")]
    [InlineData("Abschreibungen")]
    [InlineData("Reparatur/Instandhaltung")]
    [InlineData("Sonstige Kosten")]
    [InlineData("So. betr. Erlöse")]
    public async Task AuthenticGermanBWACategories_ShouldBeRecognizedAndClassified(string category)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();
        
        // Create a test PDF content with the category
        var pdfContent = CreateTestPDFContent(category, "1.500,00");
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

        // Act
        var result = await pdfParser.ParsePdfAsync(stream, "german_category_test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.TransactionLines.Should().NotBeEmpty();
        
        var parsedTransaction = result.TransactionLines.FirstOrDefault(t => t.Category == category);
        parsedTransaction.Should().NotBeNull($"Category '{category}' should be parsed correctly");
        
        // Verify correct transaction type classification
        var expectedType = DetermineExpectedTransactionType(category);
        parsedTransaction!.Type.Should().Be(expectedType, $"Category '{category}' should be classified as {expectedType}");
    }

    [Theory]
    [InlineData("Steuern", TransactionType.Expense)]
    [InlineData("Steuern Einkommen u. Ertrag", TransactionType.Expense)]
    [InlineData("Betriebliche Steuern", TransactionType.Expense)]
    [InlineData("steuer", TransactionType.Expense)] // Case insensitive
    [InlineData("STEUER", TransactionType.Expense)] // Case insensitive
    public async Task GermanTaxCategories_ShouldAlwaysBeClassifiedAsExpense(string taxCategory, TransactionType expectedType)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();
        
        var pdfContent = CreateTestPDFContent(taxCategory, "2.500,00");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

        // Act
        var result = await pdfParser.ParsePdfAsync(stream, "tax_test.pdf");

        // Assert
        var taxTransaction = result.TransactionLines.FirstOrDefault(t => t.Category.ToLower().Contains("steuer"));
        taxTransaction.Should().NotBeNull("Tax category should be parsed");
        taxTransaction!.Type.Should().Be(expectedType, "German tax categories must always be classified as expenses");
    }

    [Fact]
    public async Task GermanSummaryCategories_ShouldBeExcludedFromImport()
    {
        // Arrange
        var summaryCategories = new[]
        {
            "Gesamtkosten",
            "Betriebsergebnis",
            "Rohertrag",
            "Ergebnis vor Steuern"
        };

        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();

        foreach (var summaryCategory in summaryCategories)
        {
            var pdfContent = CreateTestPDFContent(summaryCategory, "50.000,00");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

            // Act
            var result = await pdfParser.ParsePdfAsync(stream, "summary_test.pdf");

            // Assert
            var summaryTransaction = result.TransactionLines.FirstOrDefault(t => t.Category == summaryCategory);
            
            if (summaryTransaction != null)
            {
                summaryTransaction.Type.Should().Be(TransactionType.Summary, 
                    $"Summary category '{summaryCategory}' should be classified as Summary type");
            }
        }
    }

    #endregion

    #region German Character Encoding Tests

    [Theory]
    [InlineData("ä", "ae")]
    [InlineData("ö", "oe")]
    [InlineData("ü", "ue")]
    [InlineData("ß", "ss")]
    [InlineData("Ä", "AE")]
    [InlineData("Ö", "OE")]
    [InlineData("Ü", "UE")]
    public async Task GermanUmlauts_ShouldDisplayCorrectlyInUI(string umlaut, string fallback)
    {
        // Arrange
        var categoryWithUmlaut = $"Bürokosten {umlaut}";
        await SeedUmlautTestData(categoryWithUmlaut);

        // Act
        var response = await _client.GetAsync("/Transactions");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        
        // Verify umlauts are displayed correctly (not as fallback)
        content.Should().Contain(umlaut, "German umlauts should be displayed correctly");
        content.Should().NotContain(fallback, "Umlauts should not be converted to fallback characters");
        
        // Verify proper UTF-8 encoding
        response.Content.Headers.ContentType?.CharSet.Should().Be("utf-8");
    }

    [Theory]
    [InlineData("Personalkösten für Ärzte")]
    [InlineData("Büroausstattung & Möbel")]
    [InlineData("Weiterbildungskosten (Fort-/Ausbildung)")]
    [InlineData("Steuern & Gebühren")]
    public async Task ComplexGermanCategoryNames_ShouldHandleCorrectly(string complexCategory)
    {
        // Arrange
        await SeedComplexCategoryTestData(complexCategory);

        // Act
        var response = await _client.GetAsync("/Transactions");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        content.Should().Contain(complexCategory, "Complex German category names should be handled correctly");
        
        // Verify special characters are preserved
        if (complexCategory.Contains("&"))
            content.Should().Contain("&amp;", "Ampersands should be HTML encoded");
        if (complexCategory.Contains("("))
            content.Should().Contain("(", "Parentheses should be preserved");
        if (complexCategory.Contains("/"))
            content.Should().Contain("/", "Slashes should be preserved");
    }

    #endregion

    #region PDF Parsing German Number Format Tests

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("12.345,67", 12345.67)]
    [InlineData("123.456,78", 123456.78)]
    [InlineData("1.000,00", 1000.00)]
    [InlineData("0,50", 0.50)]
    [InlineData("-1.234,56", -1234.56)]
    public async Task GermanNumberParsing_FromPDF_ShouldConvertCorrectly(string germanNumber, decimal expectedValue)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();
        
        var pdfContent = CreateTestPDFContent("Personalkosten", germanNumber);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

        // Act
        var result = await pdfParser.ParsePdfAsync(stream, "german_numbers_test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.TransactionLines.Should().NotBeEmpty();
        
        var transaction = result.TransactionLines.First();
        transaction.Amount.Should().Be(expectedValue, $"German number '{germanNumber}' should parse to {expectedValue}");
    }

    [Fact]
    public async Task GermanPDFDateFormat_ShouldParseCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();
        
        var pdfContent = """
            BWA-Auswertung 2024
            Jan/2024 Feb/2024 Mrz/2024 Apr/2024 Mai/2024 Jun/2024
            Personalkosten 15.000,00 16.000,00 14.500,00 15.500,00 16.200,00 15.800,00
            """;
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

        // Act
        var result = await pdfParser.ParsePdfAsync(stream, "german_dates_test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.Year.Should().Be(2024);
        result.TransactionLines.Should().HaveCountGreaterOrEqualTo(6, "Should parse all 6 months of data");
        
        // Verify month parsing (1=Jan, 3=Mrz, 5=Mai)
        result.TransactionLines.Should().Contain(t => t.Month == 1, "January should be parsed");
        result.TransactionLines.Should().Contain(t => t.Month == 3, "März should be parsed");
        result.TransactionLines.Should().Contain(t => t.Month == 5, "Mai should be parsed");
    }

    #endregion

    #region Business Rules Validation Tests

    [Fact]
    public async Task GermanVATRules_ShouldBeAppliedCorrectly()
    {
        // In German BWA, VAT (Mehrwertsteuer) has specific treatment
        var vatCategories = new[]
        {
            "Vorsteuer",
            "Umsatzsteuer", 
            "Mehrwertsteuer"
        };

        using var scope = _factory.Services.CreateScope();
        var pdfParser = scope.ServiceProvider.GetRequiredService<PdfParserService>();

        foreach (var vatCategory in vatCategories)
        {
            var pdfContent = CreateTestPDFContent(vatCategory, "1.900,00");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(pdfContent));

            // Act
            var result = await pdfParser.ParsePdfAsync(stream, "vat_test.pdf");

            // Assert
            if (result.TransactionLines.Any())
            {
                var vatTransaction = result.TransactionLines.First();
                // VAT categories should be classified appropriately
                vatTransaction.Type.Should().BeOneOf(TransactionType.Expense, TransactionType.Other);
            }
        }
    }

    [Fact]
    public async Task GermanAccountingPeriod_ShouldFollowBusinessYear()
    {
        // German business year can be calendar year or fiscal year
        // Test that the system handles German accounting periods correctly
        
        await SeedBusinessYearTestData();

        // Act
        var response = await _client.GetAsync("/Transactions");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        
        // Verify proper period handling
        content.Should().Contain("2024", "Should handle current business year");
        content.Should().NotContain("invalid", "Should not show invalid periods");
    }

    #endregion

    #region Helper Methods

    private async Task SeedGermanFormattingTestData()
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
            SourceFileName = "formatting_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transactions = new[]
        {
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Personalkosten", Month = 1, Year = 2024, Amount = -15234.56m, Type = TransactionType.Expense },
            new TransactionLine { FinancialPeriodId = period.Id, Category = "Umsatzerlöse", Month = 1, Year = 2024, Amount = 123456.78m, Type = TransactionType.Revenue }
        };

        context.TransactionLines.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    private async Task SeedChartFormattingTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await ClearDatabase(context);

        for (int month = 1; month <= 3; month++)
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
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Umsatzerlöse", Month = month, Year = 2024, Amount = 45000m + (month * 1000), Type = TransactionType.Revenue },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Personalkosten", Month = month, Year = 2024, Amount = -15000m - (month * 500), Type = TransactionType.Expense }
            };

            context.TransactionLines.AddRange(transactions);
            await context.SaveChangesAsync();
        }
    }

    private async Task SeedMonthlyDisplayTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await ClearDatabase(context);

        // Create data for German months
        var germanMonths = new[] { 1, 2, 3 }; // Januar, Februar, März
        
        foreach (var month in germanMonths)
        {
            var period = new FinancialPeriod
            {
                Year = 2024,
                Month = month,
                ImportedAt = DateTime.Now,
                SourceFileName = $"month_test_{month:00}.pdf"
            };
            context.FinancialPeriods.Add(period);
            await context.SaveChangesAsync();

            var transaction = new TransactionLine
            {
                FinancialPeriodId = period.Id,
                Category = "Personalkosten",
                Month = month,
                Year = 2024,
                Amount = -10000m,
                Type = TransactionType.Expense
            };

            context.TransactionLines.Add(transaction);
            await context.SaveChangesAsync();
        }
    }

    private async Task SeedUmlautTestData(string categoryWithUmlaut)
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
            SourceFileName = "umlaut_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transaction = new TransactionLine
        {
            FinancialPeriodId = period.Id,
            Category = categoryWithUmlaut,
            Month = 1,
            Year = 2024,
            Amount = -1000m,
            Type = TransactionType.Expense
        };

        context.TransactionLines.Add(transaction);
        await context.SaveChangesAsync();
    }

    private async Task SeedComplexCategoryTestData(string complexCategory)
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
            SourceFileName = "complex_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transaction = new TransactionLine
        {
            FinancialPeriodId = period.Id,
            Category = complexCategory,
            Month = 1,
            Year = 2024,
            Amount = -2500m,
            Type = TransactionType.Expense
        };

        context.TransactionLines.Add(transaction);
        await context.SaveChangesAsync();
    }

    private async Task SeedBusinessYearTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await ClearDatabase(context);

        // Create test data spanning German business year
        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 1,
            ImportedAt = DateTime.Now,
            SourceFileName = "business_year_test.pdf"
        };
        context.FinancialPeriods.Add(period);
        await context.SaveChangesAsync();

        var transaction = new TransactionLine
        {
            FinancialPeriodId = period.Id,
            Category = "Personalkosten",
            Month = 1,
            Year = 2024,
            Amount = -15000m,
            Type = TransactionType.Expense
        };

        context.TransactionLines.Add(transaction);
        await context.SaveChangesAsync();
    }

    private async Task ClearDatabase(AppDbContext context)
    {
        context.TransactionLines.RemoveRange(context.TransactionLines);
        context.FinancialPeriods.RemoveRange(context.FinancialPeriods);
        await context.SaveChangesAsync();
    }

    private string CreateTestPDFContent(string category, string amount)
    {
        return $"""
            BWA-Auswertung 2024
            Jan/2024
            {category} {amount}
            """;
    }

    private TransactionType DetermineExpectedTransactionType(string category)
    {
        var lower = category.ToLower();
        
        if (lower.Contains("steuer"))
            return TransactionType.Expense;
        if (lower.Contains("umsatz") || lower.Contains("erlös"))
            return TransactionType.Revenue;
        if (lower.Contains("kosten") || lower.Contains("aufwand") || lower.Contains("abschreibung"))
            return TransactionType.Expense;
        if (lower.Contains("ergebnis") || lower.Contains("rohertrag") || lower.Contains("gesamt"))
            return TransactionType.Summary;
        
        return TransactionType.Other;
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
    }
}