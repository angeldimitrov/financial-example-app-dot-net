# FinanceApp.Tests - Comprehensive German BWA Position Trends Test Suite

## Overview

This comprehensive test suite validates the German BWA (Betriebswirtschaftliche Auswertung) Position Trends feature with focus on:

- **Security**: Input validation, XSS prevention, CSRF protection, rate limiting
- **Performance**: Memory optimization, query efficiency, German formatting performance  
- **UI Integration**: Chart.js functionality, interactive filters, mobile responsiveness
- **German Localization**: Authentic BWA categories, number formats, business logic

## Test Structure

### 🛡️ Security Tests (`/Security/`)
**`PositionTrendsSecurityTests.cs`**
- XSS prevention with German character sets (ä, ö, ü, ß)
- SQL injection protection for German position names
- Input validation for authentic BWA categories
- CSRF token validation for file uploads
- Rate limiting validation
- German business logic security (tax rules, summary exclusion)

### ⚡ Performance Tests (`/Performance/`)
**`PositionTrendsPerformanceTests.cs`**
- Memory usage optimization with large German datasets (1000+ positions)
- Database query performance with German culture formatting
- Chart data generation efficiency with German month names
- Pagination performance with BWA position data
- Concurrent access handling
- PDF parsing performance optimization

### 🖥️ UI Integration Tests (`/UI/`)
**`PlaywrightUITests.cs`**
- Chart.js rendering with German BWA data
- Interactive filter controls with German position names
- Dynamic chart updates with authentic German categories
- Mobile responsive design validation
- Chart export functionality
- Accessibility compliance testing
- Error handling and loading states

### 🇩🇪 German Localization Tests (`/German/`)
**`GermanLocalizationTests.cs`**
- German number formatting (1.234,56 €)
- German month names (Januar, Februar, März...)
- Authentic BWA category classification
- German umlaut handling (ä, ö, ü, ß)
- Tax compliance rules (Steuern always = Expense)
- German PDF parsing with correct date formats
- Business year handling

### 🔗 Integration Tests (`/Integration/`)
**`PositionTrendsIntegrationTests.cs`**
- End-to-end workflow from PDF upload to chart visualization
- Multi-month trend analysis with German data
- Data consistency between charts and tables
- Complete user journey validation
- Error handling with corrupted German data
- Performance integration with large datasets

### 📊 Test Data Factory (`/TestData/`)
**`GermanBWATestDataFactory.cs`**
- Authentic German BWA category library
- Realistic financial data generation
- Business scenario data (profitable practice, high tax burden, seasonal, startup, investment year)
- German PDF content generation
- Tax compliance test data
- Umlaut and special character test data

## Running the Tests

### Prerequisites
```bash
# Install .NET 9 SDK
# Install Playwright browsers
pwsh bin/Debug/net9.0/playwright.ps1 install

# Restore packages
dotnet restore
```

### Run All Tests
```bash
cd tests/FinanceApp.Tests
dotnet test
```

### Run Specific Test Categories
```bash
# Security tests only
dotnet test --filter "FullyQualifiedName~Security"

# Performance tests only
dotnet test --filter "FullyQualifiedName~Performance"

# UI tests only (requires Playwright)
dotnet test --filter "FullyQualifiedName~UI"

# German localization tests
dotnet test --filter "FullyQualifiedName~German"

# Integration tests
dotnet test --filter "FullyQualifiedName~Integration"
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Key German BWA Categories Tested

### Revenue Categories (Erlöse)
- **Umsatzerlöse** - Primary business revenue
- **So. betr. Erlöse** - Other operational revenue
- **Provisionserlöse** - Commission income

### Expense Categories (Kostenarten)
- **Personalkosten** - Personnel expenses
- **Raumkosten** - Facility costs
- **Fahrzeugkosten (ohne Steuer)** - Vehicle costs
- **Werbe-/Reisekosten** - Marketing and travel
- **Kosten Warenabgabe** - Cost of goods sold
- **Abschreibungen** - Depreciation
- **Reparatur/Instandhaltung** - Maintenance
- **Versicherungen/Beiträge** - Insurance
- **Besondere Kosten** - Special expenses
- **Sonstige Kosten** - Other expenses

### Tax Categories (Steuern - Always Expenses)
- **Steuern Einkommen u. Ertrag** - Income tax
- **Betriebliche Steuern** - Business taxes
- **Gewerbesteuer** - Trade tax
- **Umsatzsteuer** - VAT payments

## German Business Logic Rules Tested

1. **Tax Classification**: All "Steuer" categories must be classified as Expense
2. **Summary Exclusion**: "Gesamtkosten", "Betriebsergebnis" excluded from trends
3. **Number Formatting**: German format (1.234,56 €) throughout UI
4. **Month Names**: German months (Januar, Februar, März...) in displays
5. **Character Encoding**: Proper handling of ä, ö, ü, ß characters
6. **BWA Structure**: Authentic German accounting category hierarchy

## Performance Targets

- **Memory Usage**: < 50MB for 1000+ transactions
- **Query Performance**: < 100ms for category filtering
- **Chart Rendering**: < 1 second for multi-month data
- **Page Load**: < 5 seconds for large datasets
- **Concurrent Users**: Support 20+ concurrent requests

## Security Validation

- **XSS Prevention**: All German inputs sanitized
- **SQL Injection**: Parameterized queries for all German text
- **Input Validation**: BWA category whitelist enforcement
- **Rate Limiting**: Request throttling implementation
- **CSRF Protection**: Token validation for uploads

## Test Configuration

- **Database**: In-Memory for test isolation
- **Culture**: German (de-DE) for formatting tests
- **Browsers**: Chromium for Playwright UI tests
- **Parallel**: Disabled for UI tests to prevent conflicts

## Continuous Integration

Tests are designed to run in CI/CD pipelines with:
- Headless browser support
- In-memory database
- No external dependencies
- Consistent test data generation
- German locale configuration

## Contributing

When adding new tests:
1. Use `GermanBWATestDataFactory` for consistent test data
2. Follow German business logic rules
3. Include both positive and negative test cases
4. Add performance assertions for new features
5. Validate German character encoding
6. Test with authentic BWA categories

## German BWA Business Context

This test suite validates a German financial application that processes BWA reports - standardized business evaluation reports used by German companies and accountants. The tests ensure:

- Compliance with German accounting standards
- Proper handling of German tax regulations
- Authentic BWA category classification
- German number and date formatting
- Professional German business terminology

The comprehensive coverage ensures the application meets the high standards expected in German financial software.