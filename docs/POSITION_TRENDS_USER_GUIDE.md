# Position Trends Feature - User Guide

## Overview

The Position Trends feature provides interactive visualization of how individual financial positions develop over time. This powerful tool helps you track the performance of specific expense categories and revenue sources from your German BWA (Betriebswirtschaftliche Auswertung) reports.

## Accessing Position Trends

1. Navigate to the main application at http://localhost:5000
2. Click on **"Position Trends"** in the top navigation menu
3. The page will load showing filter controls and an empty chart initially

## Understanding the Interface

### Filter Controls

The Position Trends page provides three main filtering options:

#### Position Filter
- **Dropdown**: "All Positions" or select a specific position
- **Purpose**: Focus on a single position like "Personalkosten" or "Umsatzerlöse"
- **German Terms**: All position names are preserved exactly as they appear in your BWA reports

#### Transaction Type Filter
- **All**: Shows both revenue and expense positions
- **Revenue**: Shows only revenue positions (green lines)
- **Expenses**: Shows only expense positions (red lines)
- **Purpose**: Separate revenue trends from expense trends for clearer analysis

#### Year Filter
- **Dropdown**: "All Years" or select a specific year
- **Purpose**: Focus analysis on a particular fiscal year
- **Data Source**: Automatically populated based on your imported financial data

### Chart Display

The main chart shows:
- **Multi-line visualization**: Each position appears as a separate colored line
- **Time axis (X)**: Months and years in German format ("Jan 2024", "Feb 2024")
- **Amount axis (Y)**: Values in EUR with German number formatting (1.234,56 €)
- **Color coding**:
  - Green lines: Revenue positions
  - Red lines: Expense positions

### Interactive Features

#### Legend Interaction
- **Click** on any legend item to show/hide that position's line
- **Multiple selections**: Hide/show multiple positions to focus your analysis
- **Type indicators**: Each legend item shows "(Revenue)" or "(Expense)" for clarity

#### Tooltip Information
- **Hover** over any data point to see:
  - Position name
  - Exact period (month/year)
  - Formatted amount in EUR

## Using the Filters

### Basic Workflow

1. **Start broad**: Begin with "All Positions", "All Types", "All Years"
2. **Apply filters**: Use the filter controls to narrow down your view
3. **Click "Update Chart"**: Apply your selected filters
4. **Analyze trends**: Look for patterns, seasonal variations, growth/decline
5. **Reset if needed**: Use "Reset Filters" to return to the full view

### Common Analysis Scenarios

#### Comparing Revenue Streams
1. Set Type Filter to "Revenue"
2. Leave Position as "All Positions"
3. Compare different revenue sources over time

#### Tracking Major Expenses
1. Set Type Filter to "Expenses"
2. Select a specific year if desired
3. Identify which expense categories are growing or stable

#### Seasonal Analysis
1. Select "All Years" to see multi-year patterns
2. Choose a specific position (e.g., "Personalkosten")
3. Look for seasonal fluctuations or consistent trends

#### Year-over-Year Comparison
1. Set Position to a specific category
2. Use "All Years" to see the complete timeline
3. Identify growth trends and anomalies

## Understanding German Financial Terms

### Common Revenue Positions
- **Umsatzerlöse**: Revenue from sales (primary income)
- **Sonstige betriebliche Erlöse**: Other operating income
- **Erträge**: General earnings/income

### Common Expense Positions
- **Personalkosten**: Personnel costs (salaries, benefits)
- **Materialkosten**: Material costs (direct expenses)
- **Raumkosten**: Facility costs (rent, utilities)
- **Betriebskosten**: Operating costs (general expenses)
- **Abschreibungen**: Depreciation
- **Steuer**: Taxes

### Summary Positions (Excluded from Trends)
- **Gesamtkosten**: Total costs
- **Betriebsergebnis**: Operating result
- **Rohertrag**: Gross profit

*Note: Summary positions are automatically excluded from trend analysis to prevent double-counting.*

## Chart Interpretation

### Positive vs. Negative Values
- **Revenue positions**: Always shown as positive values
- **Expense positions**: May appear as negative values depending on your data source
- **Trend direction**: Focus on the slope of lines rather than absolute values

### Pattern Recognition
- **Upward trending lines**: Growing revenues or increasing expenses
- **Downward trending lines**: Declining revenues or decreasing expenses
- **Flat lines**: Stable amounts over time
- **Seasonal patterns**: Regular peaks and valleys throughout the year
- **Irregular spikes**: One-time events or data anomalies

## Troubleshooting

### No Data Displayed
- **Check filters**: Ensure your filter combination includes available data
- **Verify imports**: Confirm that financial data has been imported for the selected period
- **Try "Reset Filters"**: Return to default view to see all available data

### Chart Not Loading
- **Refresh the page**: Browser refresh often resolves display issues
- **Check console**: Open browser developer tools for error messages
- **Network connectivity**: Ensure stable connection to the application

### Missing Positions
- **Summary exclusion**: Remember that total/summary positions don't appear in trends
- **Type filtering**: Check if position is hidden by Revenue/Expense filter
- **Data availability**: Position may not have data in the selected time period

## Technical Notes

### Data Processing
- Amounts are aggregated by month for positions that appear multiple times
- German number formatting is used throughout (1.234,56 €)
- All dates use German month abbreviations (Jan, Feb, Mär, etc.)

### Performance
- Charts are optimized for up to several hundred data points
- Filtering is performed server-side for optimal performance
- Large datasets may take a few seconds to load

### Browser Compatibility
- Modern browsers required for Chart.js functionality
- Mobile responsive design for tablet and phone access
- JavaScript must be enabled

## Best Practices

### Effective Analysis
1. **Start with overview**: View all positions first to get the big picture
2. **Focus progressively**: Use filters to drill down into specific areas
3. **Compare similar items**: Group related positions for meaningful comparisons
4. **Consider seasonality**: Look for patterns that repeat annually
5. **Note anomalies**: Investigate unusual spikes or drops in the data

### Data Quality
- Ensure consistent BWA imports for accurate trending
- Verify that position names are standardized across periods
- Check for data gaps that might affect trend interpretation

---

*This feature is designed specifically for German financial data and BWA report formats. All terms and formatting follow German accounting standards.*