# Position Trends Feature Tests

This directory contains comprehensive tests for the Position Trends feature (issue #7), covering unit tests, integration tests, and basic UI tests.

## Test Structure

### Unit Tests
**File:** `Services/TrendAnalysisServiceTests.cs`
- **Coverage:** TrendAnalysisService business logic
- **Test Count:** 12 comprehensive test methods
- **Features Tested:**
  - Position aggregation logic with multiple data points per position/period
  - Year and position filtering functionality  
  - German date formatting validation (Jan 2024, Feb 2024, etc.)
  - Transaction type classification (Revenue/Expense)
  - Edge cases: empty data, invalid filters, future years
  - Data ordering and performance with larger datasets

### Integration Tests
**File:** `Pages/PositionTrendsIntegrationTests.cs`
- **Coverage:** API endpoint `/PositionTrends?handler=PositionTrends`
- **Test Count:** 10 comprehensive test methods
- **Features Tested:**
  - HTTP endpoint accessibility and response codes
  - JSON serialization/deserialization of trend data
  - Query parameter handling (positions, year, type filters)
  - Combined filter parameter scenarios
  - Error handling and validation
  - Performance and response time validation
  - Full integration with database and services

### UI Tests
**File:** `Pages/PositionTrendsUITests.cs` 
- **Coverage:** Razor page HTML structure and JavaScript integration
- **Test Count:** 12 comprehensive test methods
- **Features Tested:**
  - Page loads correctly with all elements present
  - Filter controls properly initialized with database data
  - Chart container and loading states work correctly
  - Chart.js library integration and configuration
  - German localization elements (EUR formatting, de-DE locale)
  - Responsive design and accessibility elements
  - Error handling UI states and messages

## Running the Tests

### Prerequisites
```bash
# Ensure .NET 9 SDK is installed
dotnet --version  # Should be 9.x

# Start PostgreSQL for integration tests (if needed)
docker-compose up -d

# Restore packages
dotnet restore
```

### Run All Tests
```bash
# From repository root
dotnet test tests/FinanceApp.Tests/

# From test directory
cd tests/FinanceApp.Tests
dotnet test
```

### Run Specific Test Categories

**Unit Tests Only:**
```bash
dotnet test --filter "FullyQualifiedName~TrendAnalysisServiceTests"
```

**Integration Tests Only:**
```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

**UI Tests Only:**
```bash
dotnet test --filter "FullyQualifiedName~UITests"
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory:./coverage
```

### Verbose Output
```bash
dotnet test --verbosity detailed
```

## Test Data

All tests use in-memory databases with seeded test data:

### Sample Test Data Structure:
- **Financial Periods:** 2023-11, 2023-12, 2024-01, 2024-02, 2024-03
- **German Position Names:** 
  - Personalkosten (Personnel Costs) - Expense
  - Umsatzerlöse (Revenue) - Revenue
  - Raumkosten (Facility Costs) - Expense
  - Gesamtkosten (Total Costs) - Summary (excluded from trends)

### Test Scenarios Covered:
- Multiple transactions per position/period (aggregation testing)
- Cross-year data for year filtering
- Revenue vs expense classification
- German date formatting validation
- Empty datasets and edge cases

## Test Coverage Goals

The tests aim to achieve:
- **Unit Test Coverage:** >90% for TrendAnalysisService
- **Integration Coverage:** 100% for API endpoint paths
- **UI Coverage:** Complete HTML structure and JavaScript integration points
- **Edge Case Coverage:** Empty data, invalid filters, error conditions
- **Performance Validation:** Response times under 5 seconds

## German Business Context Testing

Special attention is paid to German financial data requirements:

### German Date Formatting:
```csharp
// Tests verify German month abbreviations
Assert.Equal("Jan 2024", dataPoint.Period);
Assert.Equal("Mrz 2024", dataPoint.Period); // German "März" = "Mrz"
```

### German Position Names:
- Tests use authentic German BWA accounting terms
- Personalkosten, Umsatzerlöse, Raumkosten, etc.
- Summary positions (Gesamtkosten) correctly excluded

### Currency Formatting:
- EUR currency formatting in UI tests
- German locale (de-DE) validation
- Decimal comma vs period handling

## Continuous Integration

These tests are designed to run in CI/CD pipelines:

**GitHub Actions Example:**
```yaml
- name: Run Position Trends Tests
  run: |
    dotnet test tests/FinanceApp.Tests/ \
      --configuration Release \
      --no-restore \
      --verbosity minimal \
      --collect:"XPlat Code Coverage"
```

**Performance Benchmarks:**
- Unit tests: <1 second total
- Integration tests: <10 seconds total  
- UI tests: <5 seconds total
- Memory usage: <100MB peak

## Future Test Enhancements

Potential additions for even more comprehensive testing:

### Browser-Based Tests (Playwright/Selenium):
- Actual Chart.js rendering validation
- Filter interaction testing
- Real browser JavaScript execution
- Visual regression testing

### Load Testing:
- Large dataset performance (1000+ positions)
- Concurrent API request handling  
- Memory leak detection

### E2E Testing:
- PDF import → Position Trends workflow
- Multi-user scenario testing
- Cross-browser compatibility

## Troubleshooting

### Common Issues:

**In-Memory Database Conflicts:**
```bash
# Clear test artifacts
dotnet clean
rm -rf bin/ obj/
dotnet restore
```

**Test Isolation Problems:**
- Tests use unique database names (GUID-based)
- Each test class has its own database context
- Proper disposal ensures no database leaks

**German Localization Issues:**
- Tests require German culture support
- Verify system has de-DE locale available
- Month name formatting depends on system locale

### Debug Individual Tests:
```bash
# Run single test with detailed output
dotnet test --filter "GetPositionTrendsAsync_NoFilters_ReturnsAllPositionsAggregated" --verbosity detailed
```

This comprehensive test suite ensures the Position Trends feature works correctly across all layers of the application, from data access through UI presentation, with special attention to German financial reporting requirements.