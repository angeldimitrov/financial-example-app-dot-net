using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FinanceApp.Web.Data;
using FinanceApp.Web.Models;
using FinanceApp.Web.Pages;
using FinanceApp.Web.Services;

namespace FinanceApp.Tests;

/**
 * Unit tests for Index page model CSV export functionality
 * 
 * Test Coverage:
 * - CSV export handler with no data
 * - CSV export handler with valid data
 * - Error handling during export
 * 
 * Business Context:
 * - Validates that users receive appropriate feedback when no data is available
 * - Ensures proper file download response for CSV exports
 */
public class IndexModelTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<PdfParserService> _pdfParserMock;
    private readonly Mock<DataImportService> _dataImportMock;
    private readonly Mock<IFileValidationService> _fileValidationMock;
    private readonly Mock<IInputSanitizationService> _inputSanitizationMock;
    private readonly Mock<CsvExportService> _csvExportMock;
    private readonly Mock<ILogger<IndexModel>> _loggerMock;
    private readonly IndexModel _pageModel;
    private readonly Mock<ITempDataDictionary> _tempDataMock;

    public IndexModelTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        // Create mocks for all dependencies
        _fileValidationMock = new Mock<IFileValidationService>();
        _inputSanitizationMock = new Mock<IInputSanitizationService>();
        _loggerMock = new Mock<ILogger<IndexModel>>();
        
        // Create service mocks with required constructor parameters
        var pdfLoggerMock = new Mock<ILogger<PdfParserService>>();
        _pdfParserMock = new Mock<PdfParserService>(pdfLoggerMock.Object, _inputSanitizationMock.Object);
        
        var dataImportLoggerMock = new Mock<ILogger<DataImportService>>();
        _dataImportMock = new Mock<DataImportService>(_context, dataImportLoggerMock.Object, _inputSanitizationMock.Object);
        
        var csvLoggerMock = new Mock<ILogger<CsvExportService>>();
        _csvExportMock = new Mock<CsvExportService>(csvLoggerMock.Object);

        // Setup TempData mock
        _tempDataMock = new Mock<ITempDataDictionary>();
        var httpContext = new DefaultHttpContext();
        var tempDataProvider = Mock.Of<ITempDataProvider>();
        var tempDataDictionaryFactory = new TempDataDictionaryFactory(tempDataProvider);
        
        // Create page model with all dependencies
        _pageModel = new IndexModel(
            _pdfParserMock.Object,
            _dataImportMock.Object,
            _fileValidationMock.Object,
            _inputSanitizationMock.Object,
            _csvExportMock.Object,
            _context,
            _loggerMock.Object
        )
        {
            TempData = _tempDataMock.Object,
            PageContext = new PageContext
            {
                HttpContext = httpContext
            }
        };
    }

    /**
     * Test that appropriate response is returned when no financial data exists
     * 
     * Business Rule:
     * - Users should receive clear feedback when attempting to export without data
     * - Should redirect back to the page with warning message
     * - No file download should occur
     */
    [Fact]
    public async Task OnGetExportCsvAsync_WithNoData_ReturnsAppropriateResponse()
    {
        // Arrange - Ensure database is empty
        _context.Database.EnsureCreated();
        // No data added to context, so it remains empty

        // Act - Call the export handler
        var result = await _pageModel.OnGetExportCsvAsync();

        // Assert - Verify appropriate response
        Assert.IsType<RedirectToPageResult>(result);
        
        // Verify warning message was set in TempData
        _tempDataMock.VerifySet(td => td["Warning"] = It.Is<string>(
            s => s != null && s.Contains("Keine Daten zum Exportieren")), 
            Times.Once);
        
        // Verify logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CSV export attempted with no data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        // Verify CSV export service was NOT called (no data to export)
        _csvExportMock.Verify(x => x.GenerateCsvAsync(It.IsAny<List<FinancialPeriod>>()), Times.Never);
    }

    /**
     * Test successful CSV export with data
     * 
     * Business Rule:
     * - Should return file download when data exists
     * - File should have correct MIME type and filename
     */
    [Fact]
    public async Task OnGetExportCsvAsync_WithValidData_ReturnsFileResult()
    {
        // Arrange - Add test data to database
        _context.Database.EnsureCreated();
        
        var period = new FinancialPeriod
        {
            Year = 2024,
            Month = 1,
            SourceFileName = "test.pdf",
            ImportedAt = DateTime.Now,
            TransactionLines = new List<TransactionLine>
            {
                new TransactionLine
                {
                    Category = "Umsatzerlöse",
                    Amount = 10000m,
                    Type = TransactionType.Revenue,
                    Month = 1,
                    Year = 2024
                }
            }
        };
        
        _context.FinancialPeriods.Add(period);
        await _context.SaveChangesAsync();
        
        // Setup CSV export mock
        var csvContent = new byte[] { 0x01, 0x02, 0x03 }; // Mock CSV content
        _csvExportMock.Setup(x => x.GenerateCsvAsync(It.IsAny<List<FinancialPeriod>>()))
            .ReturnsAsync(csvContent);
        _csvExportMock.Setup(x => x.GenerateFileName(It.IsAny<List<FinancialPeriod>>()))
            .Returns("BWA-Export_2024-01_2024-01.csv");

        // Act - Call the export handler
        var result = await _pageModel.OnGetExportCsvAsync();

        // Assert - Verify file result
        Assert.IsType<FileContentResult>(result);
        var fileResult = result as FileContentResult;
        
        Assert.NotNull(fileResult);
        Assert.Equal("text/csv; charset=utf-8", fileResult.ContentType);
        Assert.Equal("BWA-Export_2024-01_2024-01.csv", fileResult.FileDownloadName);
        Assert.Equal(csvContent, fileResult.FileContents);
        
        // Verify CSV export service was called
        _csvExportMock.Verify(x => x.GenerateCsvAsync(It.IsAny<List<FinancialPeriod>>()), Times.Once);
        _csvExportMock.Verify(x => x.GenerateFileName(It.IsAny<List<FinancialPeriod>>()), Times.Once);
        
        // Verify success logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CSV export successful")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /**
     * Test error handling during CSV export
     * 
     * Business Rule:
     * - Errors should be handled gracefully
     * - User should receive error feedback
     * - Should redirect back to page
     */
    [Fact]
    public async Task OnGetExportCsvAsync_WithException_HandlesErrorGracefully()
    {
        // Arrange - Add test data
        _context.Database.EnsureCreated();
        var period = new FinancialPeriod { Year = 2024, Month = 1 };
        _context.FinancialPeriods.Add(period);
        await _context.SaveChangesAsync();
        
        // Setup CSV export to throw exception
        _csvExportMock.Setup(x => x.GenerateCsvAsync(It.IsAny<List<FinancialPeriod>>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        var result = await _pageModel.OnGetExportCsvAsync();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        
        // Verify error message was set
        _tempDataMock.VerifySet(td => td["Error"] = It.Is<string>(
            s => s != null && s.Contains("Fehler beim Exportieren")), 
            Times.Once);
        
        // Verify error logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during CSV export")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}