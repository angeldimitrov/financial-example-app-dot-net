using FinanceApp.Web.Models;
using FinanceApp.Web.Services;
using Bogus;
using System.Globalization;

namespace FinanceApp.Tests.TestData;

/// <summary>
/// Factory for generating realistic German BWA test data
/// Provides consistent, authentic German financial data for all test scenarios
/// Uses Bogus with German locale for realistic data generation
/// </summary>
public static class GermanBWATestDataFactory
{
    private static readonly CultureInfo GermanCulture = new("de-DE");
    private static readonly Faker GermanFaker = new("de");
    
    /// <summary>
    /// Authentic German BWA category mappings as used in real German accounting software
    /// Includes proper classification and typical German business terminology
    /// </summary>
    public static readonly Dictionary<string, (TransactionType Type, decimal TypicalMonthlyAmount, string Description)> AuthenticGermanCategories = new()
    {
        // Revenue categories (Erlöse)
        ["Umsatzerlöse"] = (TransactionType.Revenue, 50000m, "Primary business revenue from services"),
        ["So. betr. Erlöse"] = (TransactionType.Revenue, 2000m, "Other operational revenue"),
        ["Erlöse aus Verkauf"] = (TransactionType.Revenue, 15000m, "Sales revenue"),
        ["Provisionserlöse"] = (TransactionType.Revenue, 3000m, "Commission income"),
        
        // Personnel costs (Personalkosten)
        ["Personalkosten"] = (TransactionType.Expense, -20000m, "Total personnel expenses"),
        ["Löhne und Gehälter"] = (TransactionType.Expense, -15000m, "Wages and salaries"),
        ["Soziale Abgaben"] = (TransactionType.Expense, -3000m, "Social security contributions"),
        ["Aufwendungen für Altersversorgung"] = (TransactionType.Expense, -1500m, "Pension contributions"),
        
        // Operating costs (Betriebskosten)
        ["Raumkosten"] = (TransactionType.Expense, -3500m, "Office and facility costs"),
        ["Mieten"] = (TransactionType.Expense, -2500m, "Rent payments"),
        ["Nebenkosten"] = (TransactionType.Expense, -800m, "Utilities and ancillary costs"),
        
        // Vehicle costs (Fahrzeugkosten)
        ["Fahrzeugkosten (ohne Steuer)"] = (TransactionType.Expense, -1200m, "Vehicle expenses excluding tax"),
        ["KFZ-Steuer"] = (TransactionType.Expense, -150m, "Vehicle tax"),
        ["Benzin und Öl"] = (TransactionType.Expense, -600m, "Fuel and oil"),
        ["KFZ-Versicherung"] = (TransactionType.Expense, -300m, "Vehicle insurance"),
        
        // Marketing and travel (Werbe- und Reisekosten)
        ["Werbe-/Reisekosten"] = (TransactionType.Expense, -1500m, "Marketing and travel expenses"),
        ["Werbung"] = (TransactionType.Expense, -800m, "Advertising expenses"),
        ["Reisekosten"] = (TransactionType.Expense, -700m, "Travel expenses"),
        
        // Material costs (Materialkosten)
        ["Kosten Warenabgabe"] = (TransactionType.Expense, -5000m, "Cost of goods sold"),
        ["Wareneinkauf"] = (TransactionType.Expense, -4000m, "Inventory purchases"),
        ["Materialkosten"] = (TransactionType.Expense, -2000m, "Material costs"),
        
        // Depreciation (Abschreibungen)
        ["Abschreibungen"] = (TransactionType.Expense, -2500m, "Depreciation expenses"),
        ["AfA auf Sachanlagen"] = (TransactionType.Expense, -2000m, "Depreciation on fixed assets"),
        ["AfA auf immaterielle VG"] = (TransactionType.Expense, -500m, "Depreciation on intangible assets"),
        
        // Maintenance and repairs (Reparatur und Instandhaltung)
        ["Reparatur/Instandhaltung"] = (TransactionType.Expense, -800m, "Repair and maintenance"),
        ["Instandhaltung Gebäude"] = (TransactionType.Expense, -500m, "Building maintenance"),
        ["Reparaturen"] = (TransactionType.Expense, -300m, "Repair costs"),
        
        // Insurance and contributions (Versicherungen und Beiträge)
        ["Versicherungen/Beiträge"] = (TransactionType.Expense, -1000m, "Insurance and contributions"),
        ["Betriebshaftpflicht"] = (TransactionType.Expense, -400m, "Business liability insurance"),
        ["Berufshaftpflicht"] = (TransactionType.Expense, -300m, "Professional liability insurance"),
        ["Beiträge Berufsverbände"] = (TransactionType.Expense, -200m, "Professional association fees"),
        
        // Taxes (Steuern - always expenses in German BWA)
        ["Steuern Einkommen u. Ertrag"] = (TransactionType.Expense, -4500m, "Income and earnings tax"),
        ["Betriebliche Steuern"] = (TransactionType.Expense, -350m, "Business taxes"),
        ["Gewerbesteuer"] = (TransactionType.Expense, -2000m, "Trade tax"),
        ["Körperschaftsteuer"] = (TransactionType.Expense, -1800m, "Corporate tax"),
        ["Umsatzsteuer"] = (TransactionType.Expense, -800m, "VAT payments"),
        
        // Special costs (Besondere Kosten)
        ["Besondere Kosten"] = (TransactionType.Expense, -600m, "Special expenses"),
        ["Rechts- und Beratungskosten"] = (TransactionType.Expense, -1200m, "Legal and consulting fees"),
        ["Fortbildungskosten"] = (TransactionType.Expense, -500m, "Training and education costs"),
        
        // Other costs (Sonstige Kosten)
        ["Sonstige Kosten"] = (TransactionType.Expense, -1000m, "Other expenses"),
        ["Porto und Telefon"] = (TransactionType.Expense, -300m, "Postage and telephone"),
        ["Bürokosten"] = (TransactionType.Expense, -400m, "Office expenses"),
        ["IT-Kosten"] = (TransactionType.Expense, -600m, "IT costs"),
        ["Bankgebühren"] = (TransactionType.Expense, -150m, "Bank fees"),
        
        // Summary categories (should be excluded from import)
        ["Gesamtkosten"] = (TransactionType.Summary, -45000m, "Total costs"),
        ["Rohertrag"] = (TransactionType.Summary, 52000m, "Gross profit"),
        ["Betriebsergebnis"] = (TransactionType.Summary, 7000m, "Operating result"),
        ["Ergebnis vor Steuern"] = (TransactionType.Summary, 6500m, "Pre-tax result")
    };

    /// <summary>
    /// Creates a realistic German BWA financial period with authentic data
    /// </summary>
    public static FinancialPeriod CreateGermanFinancialPeriod(int year, int month, string? sourceFileName = null)
    {
        return new FinancialPeriod
        {
            Year = year,
            Month = month,
            ImportedAt = DateTime.Now.AddDays(-GermanFaker.Random.Int(1, 30)),
            SourceFileName = sourceFileName ?? $"BWA_{year}_{month:00}_{GermanFaker.Company.CompanyName().Replace(" ", "_")}.pdf"
        };
    }

    /// <summary>
    /// Creates a set of realistic German BWA transaction lines for a specific period
    /// </summary>
    public static List<TransactionLine> CreateGermanBWATransactions(FinancialPeriod period, int categoryCount = 0)
    {
        var transactions = new List<TransactionLine>();
        var categoriesToUse = categoryCount > 0 
            ? AuthenticGermanCategories.Take(categoryCount)
            : AuthenticGermanCategories;

        foreach (var (category, (type, typicalAmount, _)) in categoriesToUse)
        {
            // Skip summary categories in regular transaction data
            if (type == TransactionType.Summary) continue;

            // Add realistic monthly variation (±20%)
            var variationFactor = 0.8m + (decimal)(GermanFaker.Random.Double() * 0.4);
            var amount = typicalAmount * variationFactor;
            
            // Round to realistic German business amounts (usually to nearest 10 or 50 EUR)
            amount = Math.Round(amount / 10m) * 10m;

            transactions.Add(new TransactionLine
            {
                FinancialPeriodId = period.Id,
                Category = category,
                Month = period.Month,
                Year = period.Year,
                Amount = amount,
                Type = type,
                GroupCategory = DetermineGroupCategory(category)
            });
        }

        return transactions;
    }

    /// <summary>
    /// Creates parsed transaction lines (before database import) with German BWA data
    /// </summary>
    public static List<ParsedTransactionLine> CreateParsedGermanBWATransactions(int year, int month, int categoryCount = 10)
    {
        var transactions = new List<ParsedTransactionLine>();
        var selectedCategories = AuthenticGermanCategories
            .Where(kvp => kvp.Value.Type != TransactionType.Summary)
            .Take(categoryCount);

        foreach (var (category, (type, typicalAmount, _)) in selectedCategories)
        {
            var variationFactor = 0.8m + (decimal)(GermanFaker.Random.Double() * 0.4);
            var amount = Math.Round(typicalAmount * variationFactor / 10m) * 10m;

            transactions.Add(new ParsedTransactionLine
            {
                Category = category,
                Month = month,
                Year = year,
                Amount = amount,
                Type = type
            });
        }

        return transactions;
    }

    /// <summary>
    /// Creates a complete ParsedFinancialData object with realistic German BWA content
    /// </summary>
    public static ParsedFinancialData CreateGermanBWAParsedData(int year, int month, string? fileName = null)
    {
        return new ParsedFinancialData
        {
            Year = year,
            SourceFileName = fileName ?? $"BWA_Jahresübersicht_{year}_{month:00}.pdf",
            TransactionLines = CreateParsedGermanBWATransactions(year, month, 15)
        };
    }

    /// <summary>
    /// Creates multi-month German BWA data for trend analysis testing
    /// </summary>
    public static List<ParsedFinancialData> CreateMultiMonthGermanBWAData(int year, int startMonth, int endMonth)
    {
        var dataList = new List<ParsedFinancialData>();
        
        for (int month = startMonth; month <= endMonth; month++)
        {
            dataList.Add(CreateGermanBWAParsedData(year, month));
        }

        return dataList;
    }

    /// <summary>
    /// Creates German BWA test data with specific business scenarios
    /// </summary>
    public static ParsedFinancialData CreateGermanBusinessScenarioData(string scenario, int year = 2024, int month = 1)
    {
        var baseData = CreateGermanBWAParsedData(year, month);
        
        return scenario.ToLower() switch
        {
            "profitable_practice" => CreateProfitablePracticeData(baseData),
            "high_tax_burden" => CreateHighTaxBurdenData(baseData),
            "seasonal_business" => CreateSeasonalBusinessData(baseData, month),
            "startup_phase" => CreateStartupPhaseData(baseData),
            "investment_year" => CreateInvestmentYearData(baseData),
            _ => baseData
        };
    }

    /// <summary>
    /// Creates test data for German tax compliance scenarios
    /// Ensures all tax-related categories are properly classified as expenses
    /// </summary>
    public static ParsedFinancialData CreateGermanTaxComplianceData(int year, int month)
    {
        var taxCategories = AuthenticGermanCategories
            .Where(kvp => kvp.Key.ToLower().Contains("steuer"))
            .ToList();

        var transactions = new List<ParsedTransactionLine>();
        
        // Add regular business data
        transactions.AddRange(CreateParsedGermanBWATransactions(year, month, 8));
        
        // Add all tax categories to test compliance
        foreach (var (category, (_, typicalAmount, _)) in taxCategories)
        {
            transactions.Add(new ParsedTransactionLine
            {
                Category = category,
                Month = month,
                Year = year,
                Amount = typicalAmount * 0.9m, // Slightly varied
                Type = TransactionType.Expense // Must be expense for tax compliance
            });
        }

        return new ParsedFinancialData
        {
            Year = year,
            SourceFileName = $"Tax_Compliance_Test_{year}_{month:00}.pdf",
            TransactionLines = transactions
        };
    }

    /// <summary>
    /// Creates German test data with special characters and umlauts
    /// Tests character encoding and display issues
    /// </summary>
    public static ParsedFinancialData CreateGermanUmlautTestData(int year, int month)
    {
        var umlautCategories = new[]
        {
            "Personalkösten für Ärzte",
            "Büroausstattung",
            "Weiterbildungsmaßnahmen",
            "Werbemaßnahmen",
            "Büromiete",
            "Geschäftsführung",
            "Fortbildungskosten"
        };

        var transactions = umlautCategories.Select((category, index) => new ParsedTransactionLine
        {
            Category = category,
            Month = month,
            Year = year,
            Amount = -(1000m + index * 500m),
            Type = TransactionType.Expense
        }).ToList();

        return new ParsedFinancialData
        {
            Year = year,
            SourceFileName = $"Umlaut_Test_{year}_{month:00}.pdf",
            TransactionLines = transactions
        };
    }

    /// <summary>
    /// Generates realistic German PDF content for parser testing
    /// </summary>
    public static string CreateGermanBWAPDFContent(int year, params int[] months)
    {
        var monthAbbreviations = new[] { "Jan", "Feb", "Mrz", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez" };
        var header = $"BWA - Betriebswirtschaftliche Auswertung {year}\n";
        header += $"Praxis Dr. {GermanFaker.Name.LastName()}\n\n";
        
        // Create month headers
        var monthHeaders = months.Select(m => $"{monthAbbreviations[m-1]}/{year}").ToArray();
        header += string.Join(" ", monthHeaders) + "\n\n";

        var content = new StringBuilder(header);
        
        // Add revenue section
        content.AppendLine("Erlöse");
        foreach (var (category, (type, amount, _)) in AuthenticGermanCategories.Where(c => c.Value.Type == TransactionType.Revenue))
        {
            content.Append($"{category} ");
            foreach (var month in months)
            {
                var monthlyAmount = amount * (0.9m + (decimal)GermanFaker.Random.Double() * 0.2m);
                content.Append($"{monthlyAmount.ToString("N2", GermanCulture)} ");
            }
            content.AppendLine();
        }

        content.AppendLine();
        
        // Add cost section
        content.AppendLine("Kosten");
        foreach (var (category, (type, amount, _)) in AuthenticGermanCategories.Where(c => c.Value.Type == TransactionType.Expense && !c.Key.ToLower().Contains("steuer")))
        {
            content.Append($"{category} ");
            foreach (var month in months)
            {
                var monthlyAmount = Math.Abs(amount) * (0.9m + (decimal)GermanFaker.Random.Double() * 0.2m);
                content.Append($"{monthlyAmount.ToString("N2", GermanCulture)} ");
            }
            content.AppendLine();
        }

        content.AppendLine();
        
        // Add tax section
        content.AppendLine("Steuern");
        foreach (var (category, (type, amount, _)) in AuthenticGermanCategories.Where(c => c.Key.ToLower().Contains("steuer")))
        {
            content.Append($"{category} ");
            foreach (var month in months)
            {
                var monthlyAmount = Math.Abs(amount) * (0.9m + (decimal)GermanFaker.Random.Double() * 0.2m);
                content.Append($"{monthlyAmount.ToString("N2", GermanCulture)} ");
            }
            content.AppendLine();
        }

        return content.ToString();
    }

    #region Private Helper Methods

    private static string DetermineGroupCategory(string category)
    {
        var lowerCategory = category.ToLower();
        
        if (lowerCategory.Contains("personal") || lowerCategory.Contains("löhne") || lowerCategory.Contains("gehälter"))
            return "Personalkosten";
        if (lowerCategory.Contains("raum") || lowerCategory.Contains("miete") || lowerCategory.Contains("nebenkosten"))
            return "Raumkosten";
        if (lowerCategory.Contains("fahrzeug") || lowerCategory.Contains("kfz"))
            return "Fahrzeugkosten";
        if (lowerCategory.Contains("steuer"))
            return "Steuern";
        if (lowerCategory.Contains("versicherung") || lowerCategory.Contains("beiträge"))
            return "Versicherungen";
        if (lowerCategory.Contains("umsatz") || lowerCategory.Contains("erlös"))
            return "Erlöse";
        
        return "Sonstige";
    }

    private static ParsedFinancialData CreateProfitablePracticeData(ParsedFinancialData baseData)
    {
        // Increase revenue by 30%, reduce some costs
        foreach (var transaction in baseData.TransactionLines)
        {
            if (transaction.Type == TransactionType.Revenue)
            {
                transaction.Amount *= 1.3m;
            }
            else if (transaction.Type == TransactionType.Expense && !transaction.Category.ToLower().Contains("personal"))
            {
                transaction.Amount *= 0.8m; // Reduce non-personnel costs
            }
        }

        baseData.SourceFileName = baseData.SourceFileName.Replace(".pdf", "_profitable.pdf");
        return baseData;
    }

    private static ParsedFinancialData CreateHighTaxBurdenData(ParsedFinancialData baseData)
    {
        // Increase all tax-related amounts
        foreach (var transaction in baseData.TransactionLines)
        {
            if (transaction.Category.ToLower().Contains("steuer"))
            {
                transaction.Amount *= 1.5m; // 50% higher taxes
            }
        }

        baseData.SourceFileName = baseData.SourceFileName.Replace(".pdf", "_high_tax.pdf");
        return baseData;
    }

    private static ParsedFinancialData CreateSeasonalBusinessData(ParsedFinancialData baseData, int month)
    {
        // Adjust revenue based on seasonal patterns (higher in certain months)
        var seasonalFactor = month switch
        {
            1 or 2 or 7 or 8 => 0.7m, // Lower in winter/summer vacation
            3 or 4 or 9 or 10 => 1.3m, // Higher in spring/fall
            _ => 1.0m
        };

        foreach (var transaction in baseData.TransactionLines)
        {
            if (transaction.Type == TransactionType.Revenue)
            {
                transaction.Amount *= seasonalFactor;
            }
        }

        baseData.SourceFileName = baseData.SourceFileName.Replace(".pdf", "_seasonal.pdf");
        return baseData;
    }

    private static ParsedFinancialData CreateStartupPhaseData(ParsedFinancialData baseData)
    {
        // Lower revenue, higher setup costs
        foreach (var transaction in baseData.TransactionLines)
        {
            if (transaction.Type == TransactionType.Revenue)
            {
                transaction.Amount *= 0.4m; // Much lower revenue in startup phase
            }
            else if (transaction.Category.Contains("Abschreibungen") || transaction.Category.Contains("Besondere Kosten"))
            {
                transaction.Amount *= 2.0m; // Higher depreciation and special costs
            }
        }

        baseData.SourceFileName = baseData.SourceFileName.Replace(".pdf", "_startup.pdf");
        return baseData;
    }

    private static ParsedFinancialData CreateInvestmentYearData(ParsedFinancialData baseData)
    {
        // Higher depreciation, special investment costs
        foreach (var transaction in baseData.TransactionLines)
        {
            if (transaction.Category.Contains("Abschreibungen"))
            {
                transaction.Amount *= 3.0m; // Much higher depreciation
            }
        }

        // Add special investment category
        baseData.TransactionLines.Add(new ParsedTransactionLine
        {
            Category = "Investitionen Praxisausstattung",
            Month = baseData.TransactionLines.First().Month,
            Year = baseData.TransactionLines.First().Year,
            Amount = -25000m,
            Type = TransactionType.Expense
        });

        baseData.SourceFileName = baseData.SourceFileName.Replace(".pdf", "_investment.pdf");
        return baseData;
    }

    #endregion
}