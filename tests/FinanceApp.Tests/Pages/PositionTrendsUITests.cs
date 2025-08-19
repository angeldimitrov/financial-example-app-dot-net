using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;

namespace FinanceApp.Tests.Pages;

/// <summary>
/// UI-focused tests for the Position Trends page
/// 
/// Test Coverage:
/// - Page loads correctly with all elements present
/// - Filter controls are properly initialized with data
/// - Chart container and loading states work correctly
/// - JavaScript integration points function properly
/// - Error handling displays appropriate messages
/// - Responsive design elements are present
/// 
/// Note: These are "basic UI tests" that verify HTML structure and initial state.
/// For full browser automation (clicking, JavaScript execution), a tool like
/// Playwright or Selenium would be more appropriate.
/// </summary>
public class PositionTrendsUITests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PositionTrendsUITests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace database with in-memory for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("UITestDb");
                });
            });
        });

        _client = _factory.CreateClient();
        SeedTestDatabase();
    }

    /// <summary>
    /// Seeds test database with position and year data for UI testing
    /// </summary>
    private void SeedTestDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        // Create test periods
        var periods = new[]
        {
            new FinancialPeriod { Year = 2023, Month = 12, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 1, ImportedAt = DateTime.UtcNow },
            new FinancialPeriod { Year = 2024, Month = 2, ImportedAt = DateTime.UtcNow }
        };
        
        context.FinancialPeriods.AddRange(periods);
        context.SaveChanges();

        // Create test transactions with German position names
        var transactions = new[]
        {
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
                FinancialPeriodId = periods[1].Id,
                Category = "Umsatzerlöse", 
                Month = 1, Year = 2024, 
                Amount = 45000m, 
                Type = TransactionType.Revenue 
            },
            new TransactionLine 
            { 
                FinancialPeriodId = periods[2].Id,
                Category = "Raumkosten", 
                Month = 2, Year = 2024, 
                Amount = 3500m, 
                Type = TransactionType.Expense 
            }
        };

        context.TransactionLines.AddRange(transactions);
        context.SaveChanges();
    }

    /// <summary>
    /// Test that the Position Trends page loads successfully
    /// 
    /// Verifies:
    /// - Page returns HTTP 200 OK status
    /// - Content-Type is HTML
    /// - Basic page structure is present
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_LoadsSuccessfully()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(content));
        Assert.Contains("Position Development Over Time", content);
    }

    /// <summary>
    /// Test that page header elements are present and correct
    /// 
    /// Verifies:
    /// - Page title is displayed
    /// - Header icons and styling are present
    /// - Descriptive text is included
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_HeaderElementsPresent()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert page header elements
        Assert.Contains("Position Development Over Time", content);
        Assert.Contains("Track individual position trends", content);
        Assert.Contains("bi-graph-up", content); // Icon class
        Assert.Contains("bg-primary", content); // Header styling
    }

    /// <summary>
    /// Test that filter controls are properly rendered
    /// 
    /// Verifies:
    /// - Position dropdown is present with correct structure
    /// - Year dropdown is present with correct structure  
    /// - Transaction type radio buttons are present
    /// - Filter action buttons are present
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_FilterControlsRendered()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert position selector
        Assert.Contains("id=\"positionSelect\"", content);
        Assert.Contains("All Positions", content);
        Assert.Contains("<option value=\"\">All Positions</option>", content);

        // Assert year selector
        Assert.Contains("id=\"yearSelect\"", content);
        Assert.Contains("All Years", content);
        Assert.Contains("<option value=\"\">All Years</option>", content);

        // Assert transaction type filters
        Assert.Contains("name=\"typeFilter\"", content);
        Assert.Contains("id=\"typeAll\"", content);
        Assert.Contains("id=\"typeRevenue\"", content);
        Assert.Contains("id=\"typeExpenses\"", content);
        Assert.Contains("value=\"all\"", content);
        Assert.Contains("value=\"revenue\"", content);
        Assert.Contains("value=\"expenses\"", content);

        // Assert filter buttons
        Assert.Contains("id=\"applyFilters\"", content);
        Assert.Contains("id=\"resetFilters\"", content);
        Assert.Contains("Update Chart", content);
        Assert.Contains("Reset Filters", content);
    }

    /// <summary>
    /// Test that filter dropdowns are populated with actual data
    /// 
    /// Verifies:
    /// - Position dropdown contains positions from database
    /// - Year dropdown contains years from database
    /// - Options are properly formatted
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_FilterDropdownsPopulated()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert position options are populated from database
        Assert.Contains("Personalkosten", content);
        Assert.Contains("Umsatzerlöse", content);
        Assert.Contains("Raumkosten", content);

        // Assert year options are populated
        Assert.Contains("2023", content);
        Assert.Contains("2024", content);
        
        // Verify option formatting
        Assert.Contains("<option value=\"Personalkosten\">Personalkosten</option>", content);
        Assert.Contains("<option value=\"2024\">2024</option>", content);
    }

    /// <summary>
    /// Test that chart container and related elements are present
    /// 
    /// Verifies:
    /// - Chart.js canvas element is present
    /// - Chart container has proper dimensions
    /// - Loading spinner element exists
    /// - Error message container exists
    /// - No data message container exists
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_ChartContainerPresent()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert chart elements
        Assert.Contains("id=\"positionTrendsChart\"", content);
        Assert.Contains("<canvas id=\"positionTrendsChart\"></canvas>", content);
        Assert.Contains("id=\"chartContainer\"", content);
        Assert.Contains("height: 500px", content);

        // Assert loading spinner
        Assert.Contains("id=\"loadingSpinner\"", content);
        Assert.Contains("spinner-border", content);
        Assert.Contains("Loading position trend data", content);

        // Assert error message container
        Assert.Contains("id=\"errorMessage\"", content);
        Assert.Contains("alert-danger", content);
        Assert.Contains("id=\"errorText\"", content);

        // Assert no data message
        Assert.Contains("id=\"noDataMessage\"", content);
        Assert.Contains("No data available", content);
        Assert.Contains("Try adjusting your filters", content);
    }

    /// <summary>
    /// Test that Chart.js library and dependencies are included
    /// 
    /// Verifies:
    /// - Chart.js CDN link is present
    /// - JavaScript initialization code is included
    /// - Required CSS classes and structures are present
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_ChartLibraryIncluded()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert Chart.js library inclusion
        Assert.Contains("chart.js", content);
        Assert.Contains("cdn.jsdelivr.net/npm/chart.js", content);

        // Assert JavaScript initialization
        Assert.Contains("let positionChart = null", content);
        Assert.Contains("initChart", content);
        Assert.Contains("loadPositionTrends", content);
        Assert.Contains("updateChart", content);

        // Assert Chart.js configuration
        Assert.Contains("type: 'line'", content);
        Assert.Contains("responsive: true", content);
        Assert.Contains("maintainAspectRatio: false", content);
    }

    /// <summary>
    /// Test that JavaScript event handlers are properly set up
    /// 
    /// Verifies:
    /// - DOM content loaded event listener is present
    /// - Filter button event listeners are configured
    /// - Auto-update event listeners are configured
    /// - Proper API endpoint is referenced
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_JavaScriptEventHandlers()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert event handlers setup
        Assert.Contains("DOMContentLoaded", content);
        Assert.Contains("addEventListener('click', loadPositionTrends)", content);
        Assert.Contains("addEventListener('click', resetFilters)", content);
        Assert.Contains("addEventListener('change', loadPositionTrends)", content);

        // Assert API endpoint reference
        Assert.Contains("/PositionTrends?handler=PositionTrends", content);
        Assert.Contains("fetch(", content);

        // Assert filter handling
        Assert.Contains("positionSelect", content);
        Assert.Contains("yearSelect", content);
        Assert.Contains("typeFilter", content);
    }

    /// <summary>
    /// Test that German localization elements are present
    /// 
    /// Verifies:
    /// - German currency formatting code is included
    /// - German locale references are present
    /// - Euro currency symbol and formatting
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_GermanLocalizationPresent()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert German localization
        Assert.Contains("'de-DE'", content);
        Assert.Contains("currency: 'EUR'", content);
        Assert.Contains("formatCurrency", content);
        Assert.Contains("Intl.NumberFormat", content);

        // Assert German UI text
        Assert.Contains("EUR", content);
        Assert.Contains("Amount (EUR)", content);
        Assert.Contains("All amounts are displayed in EUR", content);
        Assert.Contains("German formatting", content);
    }

    /// <summary>
    /// Test that responsive design elements are included
    /// 
    /// Verifies:
    /// - Bootstrap grid classes are used
    /// - Responsive breakpoints are present
    /// - Mobile-friendly elements are included
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_ResponsiveDesignElements()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert Bootstrap grid system
        Assert.Contains("container", content);
        Assert.Contains("row", content);
        Assert.Contains("col-", content);
        Assert.Contains("col-md-", content);
        Assert.Contains("col-12", content);

        // Assert responsive utilities
        Assert.Contains("g-3", content); // Bootstrap gap utilities
        Assert.Contains("mt-", content); // Margin utilities
        Assert.Contains("mb-", content); // Margin utilities

        // Assert card-based layout
        Assert.Contains("card", content);
        Assert.Contains("card-header", content);
        Assert.Contains("card-body", content);
    }

    /// <summary>
    /// Test that accessibility elements are present
    /// 
    /// Verifies:
    /// - Form labels are properly associated
    /// - ARIA attributes are used where appropriate
    /// - Screen reader friendly elements are included
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_AccessibilityElementsPresent()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert form labels
        Assert.Contains("for=\"positionSelect\"", content);
        Assert.Contains("for=\"yearSelect\"", content);
        Assert.Contains("class=\"form-label\"", content);

        // Assert ARIA attributes
        Assert.Contains("role=\"status\"", content);
        Assert.Contains("role=\"group\"", content);
        Assert.Contains("aria-label=", content);

        // Assert screen reader elements
        Assert.Contains("visually-hidden", content);
        Assert.Contains("<span class=\"visually-hidden\">Loading...</span>", content);
    }

    /// <summary>
    /// Test that information and help text is included
    /// 
    /// Verifies:
    /// - Chart legend information is present
    /// - Usage instructions are included
    /// - Help text for German formatting is present
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_InformationTextPresent()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert legend information
        Assert.Contains("Legend:", content);
        Assert.Contains("Green", content);
        Assert.Contains("Revenue positions", content);
        Assert.Contains("Red", content);
        Assert.Contains("Expense positions", content);

        // Assert instructions
        Assert.Contains("Instructions:", content);
        Assert.Contains("Click on legend items", content);
        Assert.Contains("Hover over data points", content);
        Assert.Contains("Use filters above", content);
        Assert.Contains("German formatting", content);

        // Assert informational badges/indicators
        Assert.Contains("badge bg-success", content);
        Assert.Contains("badge bg-danger", content);
    }

    /// <summary>
    /// Test error handling UI elements
    /// 
    /// Verifies:
    /// - Error message container is properly structured
    /// - Error icons and styling are present
    /// - Error states are initially hidden
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_ErrorHandlingUIPresent()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert error message structure
        Assert.Contains("id=\"errorMessage\"", content);
        Assert.Contains("alert alert-danger", content);
        Assert.Contains("style=\"display: none;\"", content); // Initially hidden

        // Assert error icons
        Assert.Contains("bi-exclamation-triangle", content);
        Assert.Contains("id=\"errorText\"", content);

        // Assert error handling JavaScript
        Assert.Contains("showError", content);
        Assert.Contains("hideError", content);
        Assert.Contains("catch (error)", content);
        Assert.Contains("Failed to load data", content);
    }

    /// <summary>
    /// Test page metadata and SEO elements
    /// 
    /// Verifies:
    /// - Page title is properly set
    /// - ViewData title is configured
    /// - Page structure follows ASP.NET Core conventions
    /// </summary>
    [Fact]
    public async Task PositionTrendsPage_MetadataPresent()
    {
        // Act
        var response = await _client.GetAsync("/PositionTrends");
        var content = await response.Content.ReadAsStringAsync();

        // Assert page title setup
        Assert.Contains("ViewData[\"Title\"] = \"Position Trends\"", content);
        
        // Note: Full HTML document structure testing would require 
        // checking the layout file, which isn't included in this response

        // Assert Razor page structure
        Assert.Contains("@page", content);
        Assert.Contains("@model FinanceApp.Web.Pages.PositionTrendsModel", content);
        Assert.Contains("@section Scripts", content);
    }
}