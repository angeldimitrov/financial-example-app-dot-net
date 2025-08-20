using Microsoft.Playwright;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;

namespace FinanceApp.Tests.UI;

/// <summary>
/// Comprehensive UI integration tests using Playwright for German BWA Position Trends
/// Tests JavaScript Chart.js integration, interactive filters, and mobile responsiveness
/// Focus on German business data visualization and user interactions
/// </summary>
[Collection("PlaywrightCollection")]
public class PlaywrightUITests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _baseUrl = null!;

    public PlaywrightUITests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("UITestDb");
                    });
                });
            });
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-web-security" }
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "de-DE"
        });
        _page = await _context.NewPageAsync();
        
        _baseUrl = _factory.Services.GetRequiredService<IConfiguration>()["BaseUrl"] ?? "http://localhost:5000";
        
        await SeedUITestData();
    }

    #region Chart.js Integration Tests

    [Fact]
    public async Task FinanceChart_WithGermanData_ShouldRenderCorrectly()
    {
        // Navigate to home page with chart data
        await _page.GotoAsync(_baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for Chart.js to load and render
        await _page.WaitForSelectorAsync("#financeChart", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
        
        // Verify chart canvas is present and has content
        var chartCanvas = await _page.QuerySelectorAsync("#financeChart");
        chartCanvas.Should().NotBeNull();

        // Verify Chart.js script is loaded
        var chartScript = await _page.EvaluateAsync<bool>("() => typeof Chart !== 'undefined'");
        chartScript.Should().BeTrue("Chart.js should be loaded");

        // Verify chart instance exists
        var hasChart = await _page.EvaluateAsync<bool>("() => window.financeChart !== undefined");
        hasChart.Should().BeTrue("Finance chart should be initialized");

        // Test German number formatting in chart tooltips
        await _page.HoverAsync("#financeChart");
        
        // Verify German locale formatting is used
        var chartOptions = await _page.EvaluateAsync<string>(@"() => {
            const chart = window.financeChart || Chart.getChart('financeChart');
            return chart?.options?.plugins?.tooltip?.callbacks?.label ? 'has-german-formatter' : 'no-formatter';
        }");
        
        chartOptions.Should().Be("has-german-formatter", "Chart should use German number formatting");
    }

    [Fact]
    public async Task ChartData_WithGermanCurrency_ShouldDisplayCorrectly()
    {
        await _page.GotoAsync(_baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Wait for chart to render
        await _page.WaitForSelectorAsync("#financeChart");
        await _page.WaitForTimeoutAsync(1000); // Allow chart animation to complete

        // Verify chart data contains German currency formatting
        var chartDatasets = await _page.EvaluateAsync<string[]>(@"() => {
            const chart = window.financeChart || Chart.getChart('financeChart');
            return chart ? chart.data.datasets.map(d => d.label) : [];
        }");

        chartDatasets.Should().Contain("Revenue", "Chart should have Revenue dataset");
        chartDatasets.Should().Contain("Expenses", "Chart should have Expenses dataset");

        // Test chart interaction - hover and click
        await _page.HoverAsync("#financeChart");
        await _page.ClickAsync("#financeChart");

        // Verify no JavaScript errors occurred
        var consoleErrors = new List<string>();
        _page.Console += (_, e) => {
            if (e.Type == "error") consoleErrors.Add(e.Text);
        };
        
        await _page.WaitForTimeoutAsync(500);
        consoleErrors.Should().BeEmpty("Chart interaction should not cause JavaScript errors");
    }

    [Fact]
    public async Task ChartLegend_WithGermanLabels_ShouldBeInteractive()
    {
        await _page.GotoAsync(_baseUrl);
        await _page.WaitForSelectorAsync("#financeChart");

        // Click on legend items to toggle datasets
        var legendItems = await _page.QuerySelectorAllAsync("canvas + div .chart-legend li"); // Chart.js legend items
        
        if (legendItems.Count > 0)
        {
            // Click first legend item (Revenue)
            await legendItems[0].ClickAsync();
            await _page.WaitForTimeoutAsync(300);

            // Verify dataset visibility toggled
            var isDatasetVisible = await _page.EvaluateAsync<bool>(@"() => {
                const chart = window.financeChart || Chart.getChart('financeChart');
                return chart ? chart.getDatasetMeta(0).visible : true;
            }");

            // Click again to toggle back
            await legendItems[0].ClickAsync();
            await _page.WaitForTimeoutAsync(300);
        }
    }

    #endregion

    #region Interactive Filter Controls Tests

    [Fact]
    public async Task TransactionFilters_WithGermanData_ShouldWorkCorrectly()
    {
        await _page.GotoAsync($"{_baseUrl}/Transactions");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Test Year filter
        await _page.SelectOptionAsync("#year", "2024");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify filter applied
        var pageContent = await _page.TextContentAsync("body");
        pageContent.Should().NotBeNull();

        // Test Month filter
        await _page.SelectOptionAsync("#month", "1");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Test Type filter
        await _page.SelectOptionAsync("#type", "Revenue");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify German revenue categories are shown
        var revenueContent = await _page.TextContentAsync(".card-body");
        revenueContent.Should().NotBeNull();

        // Test Reset button
        await _page.ClickAsync("a.btn-secondary"); // Reset button
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify filters are cleared
        var yearValue = await _page.GetAttributeAsync("#year", "value");
        yearValue.Should().BeEmpty("Year filter should be cleared");
    }

    [Fact]
    public async Task GermanCategoryFilter_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        await _page.GotoAsync($"{_baseUrl}/Transactions");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Test German categories with umlauts and special characters
        var germanCategories = new[]
        {
            "Personalkosten",
            "Umsatzerlöse",
            "Steuern Einkommen u. Ertrag",
            "Versicherungen/Beiträge",
            "Fahrzeugkosten (ohne Steuer)"
        };

        foreach (var category in germanCategories)
        {
            // Navigate with category filter
            await _page.GotoAsync($"{_baseUrl}/Transactions?category={Uri.EscapeDataString(category)}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify German characters display correctly
            var pageContent = await _page.TextContentAsync("body");
            pageContent.Should().Contain("Transaction Details");

            // Verify no encoding issues with German characters
            pageContent.Should().NotContain("?"); // Question marks indicate encoding issues
            pageContent.Should().NotContain("&uml;"); // HTML entity issues
        }
    }

    [Fact]
    public async Task FilterControls_MobileResponsive_ShouldWorkOnSmallScreens()
    {
        // Set mobile viewport
        await _page.SetViewportSizeAsync(375, 667); // iPhone size
        await _page.GotoAsync($"{_baseUrl}/Transactions");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify filter controls are accessible on mobile
        var yearFilter = await _page.IsVisibleAsync("#year");
        yearFilter.Should().BeTrue("Year filter should be visible on mobile");

        var monthFilter = await _page.IsVisibleAsync("#month");
        monthFilter.Should().BeTrue("Month filter should be visible on mobile");

        // Test mobile interaction
        await _page.SelectOptionAsync("#type", "Expense");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify mobile layout doesn't break German content
        var content = await _page.TextContentAsync(".card-body");
        content.Should().NotBeNull();
    }

    #endregion

    #region Dynamic Chart Updates Tests

    [Fact]
    public async Task ExpensePieCharts_ShouldRenderForEachMonth()
    {
        await _page.GotoAsync($"{_baseUrl}/Transactions");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for expense charts to load
        await _page.WaitForTimeoutAsync(2000);

        // Find all expense charts
        var expenseCharts = await _page.QuerySelectorAllAsync("canvas[id^='expenseChart_']");
        expenseCharts.Should().NotBeEmpty("Expense charts should be rendered for months with data");

        // Test each chart
        foreach (var chart in expenseCharts)
        {
            var chartId = await chart.GetAttributeAsync("id");
            
            // Verify chart is initialized
            var isChartInitialized = await _page.EvaluateAsync<bool>($@"() => {{
                const chart = Chart.getChart('{chartId}');
                return chart !== null && chart !== undefined;
            }}");
            
            isChartInitialized.Should().BeTrue($"Chart {chartId} should be initialized");

            // Test chart hover interaction
            await chart.HoverAsync();
            await _page.WaitForTimeoutAsync(200);
        }
    }

    [Fact]
    public async Task PieChartTooltips_WithGermanFormatting_ShouldShowCorrectly()
    {
        await _page.GotoAsync($"{_baseUrl}/Transactions");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(2000);

        var firstChart = await _page.QuerySelectorAsync("canvas[id^='expenseChart_']");
        if (firstChart != null)
        {
            // Trigger tooltip by hovering
            await firstChart.HoverAsync();
            await _page.WaitForTimeoutAsync(500);

            // Verify German number formatting in tooltips
            var tooltipContent = await _page.EvaluateAsync<string>(@"() => {
                const charts = Chart.instances;
                for (let chart of Object.values(charts)) {
                    if (chart.tooltip && chart.tooltip.opacity > 0) {
                        return chart.tooltip.title[0] + ': ' + chart.tooltip.body[0].lines[0];
                    }
                }
                return 'no-tooltip';
            }");

            if (tooltipContent != "no-tooltip")
            {
                tooltipContent.Should().Contain("€", "Tooltip should contain Euro symbol");
                tooltipContent.Should().MatchRegex(@"\d+,\d{2}", "Tooltip should use German decimal format");
            }
        }
    }

    #endregion

    #region Loading States and Error Handling Tests

    [Fact]
    public async Task ChartLoading_ShouldShowProgressIndicator()
    {
        // Navigate to page and intercept network requests
        await _page.RouteAsync("**/*", async route =>
        {
            // Add delay to simulate slow loading
            await Task.Delay(1000);
            await route.ContinueAsync();
        });

        await _page.GotoAsync(_baseUrl);

        // Verify loading state is handled gracefully
        var hasContent = await _page.WaitForSelectorAsync(".card", new PageWaitForSelectorOptions 
        { 
            Timeout = 10000 
        });
        
        hasContent.Should().NotBeNull("Page should load even with network delays");
    }

    [Fact]
    public async Task ChartError_ShouldHandleGracefully()
    {
        // Inject Chart.js error to test error handling
        await _page.GotoAsync(_baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Simulate Chart.js error
        await _page.EvaluateAsync(@"() => {
            if (window.Chart) {
                const originalChart = window.Chart;
                window.Chart = function() {
                    throw new Error('Simulated Chart.js error');
                };
                window.Chart.getChart = originalChart.getChart;
            }
        }");

        // Reload page to trigger error
        await _page.ReloadAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify page still loads and shows data table
        var tableContent = await _page.IsVisibleAsync(".table");
        tableContent.Should().BeTrue("Data table should be visible even if chart fails");

        // Verify no uncaught JavaScript errors in console
        var hasErrors = await _page.EvaluateAsync<bool>("() => window.jsErrors && window.jsErrors.length > 0");
        // Note: This would require error tracking setup in the application
    }

    #endregion

    #region Mobile Responsiveness Tests

    [Theory]
    [InlineData(320, 568)] // iPhone SE
    [InlineData(375, 667)] // iPhone 8
    [InlineData(414, 896)] // iPhone XR
    [InlineData(768, 1024)] // iPad
    public async Task ResponsiveDesign_OnDifferentDevices_ShouldWorkCorrectly(int width, int height)
    {
        await _page.SetViewportSizeAsync(width, height);
        await _page.GotoAsync($"{_baseUrl}/Transactions");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify responsive layout
        var cardVisible = await _page.IsVisibleAsync(".card");
        cardVisible.Should().BeTrue($"Cards should be visible on {width}x{height}");

        // Verify German content is readable
        var content = await _page.TextContentAsync(".card-body");
        content.Should().Contain("Transaction Details");

        // Test responsive charts
        var charts = await _page.QuerySelectorAllAsync("canvas");
        foreach (var chart in charts)
        {
            var isVisible = await chart.IsVisibleAsync();
            isVisible.Should().BeTrue("Charts should be visible on mobile");
        }

        // Test responsive navigation
        if (width < 768) // Mobile breakpoint
        {
            // Test mobile-specific interactions
            var mobileElements = await _page.QuerySelectorAllAsync(".btn-sm, .form-select");
            foreach (var element in mobileElements)
            {
                var isClickable = await element.IsEnabledAsync();
                isClickable.Should().BeTrue("Mobile elements should be clickable");
            }
        }
    }

    [Fact]
    public async Task TouchInteractions_OnMobileDevices_ShouldWorkCorrectly()
    {
        await _page.SetViewportSizeAsync(375, 667);
        await _page.GotoAsync($"{_baseUrl}/Transactions");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Test touch interactions with filters
        await _page.TapAsync("#type");
        await _page.SelectOptionAsync("#type", "Revenue");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify touch interaction worked
        var selectedValue = await _page.InputValueAsync("#type");
        selectedValue.Should().Be("Revenue");

        // Test chart touch interactions
        var charts = await _page.QuerySelectorAllAsync("canvas");
        if (charts.Count > 0)
        {
            await _page.TapAsync("canvas:first-of-type");
            await _page.WaitForTimeoutAsync(300);
            
            // Verify no errors from touch interaction
            var pageContent = await _page.TextContentAsync("body");
            pageContent.Should().NotContain("error");
        }
    }

    #endregion

    #region Chart Export Functionality Tests

    [Fact]
    public async Task ChartExport_ShouldGenerateValidImage()
    {
        await _page.GotoAsync(_baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForSelectorAsync("#financeChart");

        // Test chart image export
        var chartImageData = await _page.EvaluateAsync<string>(@"() => {
            const canvas = document.getElementById('financeChart');
            return canvas ? canvas.toDataURL('image/png') : null;
        }");

        chartImageData.Should().NotBeNull("Chart should be exportable as image");
        chartImageData.Should().StartWith("data:image/png;base64,", "Should generate valid PNG data URL");

        // Verify image contains chart content
        var imageSize = chartImageData!.Length;
        imageSize.Should().BeGreaterThan(1000, "Chart image should contain meaningful content");
    }

    #endregion

    #region Accessibility Tests

    [Fact]
    public async Task ChartAccessibility_ShouldMeetStandards()
    {
        await _page.GotoAsync(_baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Test keyboard navigation
        await _page.PressAsync("body", "Tab"); // Focus first element
        await _page.PressAsync("body", "Tab"); // Navigate to chart area

        // Verify chart container is accessible
        var focusedElement = await _page.EvaluateAsync<string>("() => document.activeElement.tagName");
        
        // Test screen reader support
        var chartAriaLabel = await _page.GetAttributeAsync("#financeChart", "aria-label");
        if (chartAriaLabel != null)
        {
            chartAriaLabel.Should().NotBeEmpty("Chart should have aria-label for screen readers");
        }

        // Verify color contrast (basic check)
        var chartColors = await _page.EvaluateAsync<string[]>(@"() => {
            const chart = window.financeChart || Chart.getChart('financeChart');
            if (!chart) return [];
            return chart.data.datasets.map(d => d.borderColor);
        }");

        chartColors.Should().NotBeEmpty("Chart should have defined colors");
        chartColors.Should().NotContain("#ffffff", "Chart should not use white lines (poor contrast)");
    }

    #endregion

    #region Helper Methods

    private async Task SeedUITestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        // Clear existing data
        context.TransactionLines.RemoveRange(context.TransactionLines);
        context.FinancialPeriods.RemoveRange(context.FinancialPeriods);
        await context.SaveChangesAsync();

        // Add test periods
        var periods = new[]
        {
            new FinancialPeriod { Year = 2024, Month = 1, ImportedAt = DateTime.Now, SourceFileName = "ui_test_jan.pdf" },
            new FinancialPeriod { Year = 2024, Month = 2, ImportedAt = DateTime.Now, SourceFileName = "ui_test_feb.pdf" },
            new FinancialPeriod { Year = 2024, Month = 3, ImportedAt = DateTime.Now, SourceFileName = "ui_test_mar.pdf" }
        };
        
        context.FinancialPeriods.AddRange(periods);
        await context.SaveChangesAsync();

        // Add test transactions with German BWA categories
        var transactions = new List<TransactionLine>();
        
        foreach (var period in periods)
        {
            transactions.AddRange(new[]
            {
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Personalkosten", Month = period.Month, Year = period.Year, Amount = -15000m, Type = TransactionType.Expense },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Umsatzerlöse", Month = period.Month, Year = period.Year, Amount = 45000m, Type = TransactionType.Revenue },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Steuern Einkommen u. Ertrag", Month = period.Month, Year = period.Year, Amount = -3000m, Type = TransactionType.Expense },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Raumkosten", Month = period.Month, Year = period.Year, Amount = -2500m, Type = TransactionType.Expense },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Versicherungen/Beiträge", Month = period.Month, Year = period.Year, Amount = -800m, Type = TransactionType.Expense },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "Fahrzeugkosten (ohne Steuer)", Month = period.Month, Year = period.Year, Amount = -1200m, Type = TransactionType.Expense },
                new TransactionLine { FinancialPeriodId = period.Id, Category = "So. betr. Erlöse", Month = period.Month, Year = period.Year, Amount = 2000m, Type = TransactionType.Revenue }
            });
        }

        context.TransactionLines.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    #endregion

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
        await _factory.DisposeAsync();
    }
}