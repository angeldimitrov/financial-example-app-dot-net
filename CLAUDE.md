# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 9 web application for importing and analyzing German financial PDF reports (Jahresübersicht). The application parses BWA (Betriebswirtschaftliche Auswertung) format PDFs, extracts financial data, and provides interactive analytics with charts and detailed transaction views.

## Essential Development Commands

### Database Operations
```bash
# Start PostgreSQL database
docker-compose up -d

# Apply database migrations
dotnet ef database update --project src/FinanceApp.Web

# Create new migration (if needed)
dotnet ef migrations add <MigrationName> --project src/FinanceApp.Web
```

### Application Development
```bash
# Run the application (from repository root or src/FinanceApp.Web/)
dotnet run --urls=http://localhost:5000

# Build the application
dotnet build

# Restore packages
dotnet restore
```

### Database Connection
- **Development Database**: PostgreSQL via Docker (credentials in docker-compose.yml)
- **Connection String**: Located in `appsettings.json`
- **Default URL**: http://localhost:5000

## Architecture Overview

### Core Components

**Data Layer (`Data/`, `Models/`)**
- `AppDbContext`: Entity Framework context with PostgreSQL provider
- `FinancialPeriod`: Represents monthly financial periods with unique Year/Month constraint
- `TransactionLine`: Individual expense/revenue line items with German BWA categories

**Business Logic (`Services/`)**
- `PdfParserService`: Extracts data from concatenated German PDF format using PdfPig
- `DataImportService`: Handles database imports with duplicate prevention

**Presentation Layer (`Pages/`)**
- `Index`: Upload interface with Chart.js trend visualization
- `Transactions`: Detailed view with separate Revenue/Expenses/Profit sections

### Key Technical Considerations

**German Financial Data Processing**
- Uses German culture (`de-DE`) for number parsing (1.234,56 format)
- Implements BWA accounting category mapping
- Excludes summary totals to prevent double-counting
- Classifies transactions as Revenue, Expense, Summary, or Other

**PDF Parsing Strategy**
- Handles concatenated single-line PDF format from German accounting software
- Dynamically extracts year and month columns without hardcoded positions
- Applies business rules for tax classification (Steuern always = Expense)
- Validates parsed totals against PDF source ("Gesamtkosten")

**Data Integrity**
- Unique constraint on FinancialPeriod (Year, Month) prevents duplicates
- Cascading deletes maintain referential integrity
- Database indexes optimize queries by period and transaction type
- Import process is atomic - either all data imports or none

### Business Logic Rules

**Transaction Classification Priority**
1. Tax categories ("steuer", "Steuern Einkommen u. Ertrag") → Always Expense
2. Revenue keywords ("umsatz", "erlös") → Revenue
3. Expense keywords ("kosten", "aufwand") → Expense
4. Summary categories ("gesamtkosten", "betriebsergebnis") → Excluded from import

**Duplicate Prevention**
- PDF uploads check existing Year/Month combinations before import
- Users receive clear feedback for duplicate attempts
- Existing data remains unchanged on duplicate import attempts

### German Accounting Context

The application processes BWA reports which follow standardized German accounting categories. Key German terms:
- **Jahresübersicht**: Annual overview report
- **BWA**: Betriebswirtschaftliche Auswertung (business evaluation)
- **Umsatzerlöse**: Revenue from sales
- **Personalkosten**: Personnel costs
- **Gesamtkosten**: Total costs (summary - excluded from import)

### File Structure Note
- Main application is in `src/FinanceApp.Web/`
- Test PDFs available in `import-example/`
- Complete PRD documentation in `PRD.md`
- Docker configuration for PostgreSQL in `docker-compose.yml`
- always use gh cli when possible