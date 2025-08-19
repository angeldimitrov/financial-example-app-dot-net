# German Financial PDF Analysis Application

A .NET 9 web application for importing and analyzing German financial PDF reports (Jahresübersicht). The application parses BWA (Betriebswirtschaftliche Auswertung) format PDFs, extracts financial data, and provides interactive analytics with charts and detailed transaction views.

## Features

- **PDF Import**: Upload and parse German Jahresübersicht PDFs
- **Data Visualization**: Interactive Chart.js charts for financial trends
- **Position Trends**: Multi-line charts showing individual position development over time
- **Transaction Analysis**: Detailed breakdown of Revenue, Expenses, and Profit
- **German Localization**: Culture-aware parsing for German number formats (1.234,56)
- **Duplicate Prevention**: Automatic detection of duplicate imports by Year/Month
- **BWA Classification**: Intelligent categorization of German accounting categories
- **Advanced Filtering**: Filter trends by position, type (Revenue/Expense), and year

## Quick Start

### Prerequisites

- .NET 9 SDK
- Docker (for PostgreSQL database)

### Setup

1. **Start the database:**
   ```bash
   docker-compose up -d
   ```

2. **Apply database migrations:**
   ```bash
   dotnet ef database update --project src/FinanceApp.Web
   ```

3. **Run the application:**
   ```bash
   dotnet run --project src/FinanceApp.Web --urls=http://localhost:5000
   ```

4. **Access the application:**
   - Navigate to http://localhost:5000
   - Upload a German financial PDF from the `import-example/` folder

## Architecture

### Core Components

- **Data Layer**: Entity Framework Core with PostgreSQL
- **Business Logic**: PDF parsing with PdfPig, data import services
- **Presentation**: Razor Pages with Bootstrap UI and Chart.js

### German Business Context

The application processes BWA reports following standardized German accounting categories:
- **Jahresübersicht**: Annual overview report
- **BWA**: Betriebswirtschaftliche Auswertung (business evaluation)
- **Umsatzerlöse**: Revenue from sales
- **Personalkosten**: Personnel costs
- **Gesamtkosten**: Total costs (excluded from import to prevent double-counting)

## Development

### Essential Commands

```bash
# Database operations
docker-compose up -d                                    # Start PostgreSQL
dotnet ef database update --project src/FinanceApp.Web  # Apply migrations
dotnet ef migrations add <Name> --project src/FinanceApp.Web  # Create migration

# Application
dotnet run --project src/FinanceApp.Web --urls=http://localhost:5000  # Run app
dotnet build                                            # Build
dotnet restore                                          # Restore packages
```

### Project Structure

- `src/FinanceApp.Web/` - Main web application
- `import-example/` - Sample German financial PDFs
- `docker-compose.yml` - PostgreSQL database configuration
- `CLAUDE.md` - Claude Code instructions
- `PRD.md` - Complete product requirements

## Technical Details

### PDF Parsing Strategy

- Handles concatenated single-line PDF format from German accounting software
- Dynamically extracts year and month columns without hardcoded positions  
- Applies business rules for tax classification (Steuern always = Expense)
- Validates parsed totals against PDF source ("Gesamtkosten")

### Data Integrity

- Unique constraint on FinancialPeriod (Year, Month) prevents duplicates
- Cascading deletes maintain referential integrity
- Database indexes optimize queries by period and transaction type
- Import process is atomic - either all data imports or none

## License

This project is for demonstration purposes.