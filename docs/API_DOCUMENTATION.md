# Finance App API Documentation

## Position Trends API

### GET /PositionTrends?handler=PositionTrends

**Purpose**: Retrieves aggregated position trend data for Chart.js visualization.

**Business Context**: 
- Processes German BWA (Betriebswirtschaftliche Auswertung) financial data
- Aggregates transaction amounts by position/category over time periods
- Excludes summary transactions to prevent double-counting
- Uses German date formatting for consistency with source documents

#### Request Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `positions` | string | No | Comma-separated list of position names to filter by |
| `year` | integer | No | Year to filter data by (e.g., 2024) |
| `type` | string | No | Transaction type filter: "all", "revenue", or "expenses" |

#### Example Requests

```http
# Get all position trends for all years and types
GET /PositionTrends?handler=PositionTrends

# Get trends for specific positions
GET /PositionTrends?handler=PositionTrends&positions=Personalkosten,Umsatzerl%C3%B6se

# Get only revenue trends for 2024
GET /PositionTrends?handler=PositionTrends&year=2024&type=revenue

# Get expenses for specific positions in 2023
GET /PositionTrends?handler=PositionTrends&positions=Personalkosten&year=2023&type=expenses
```

#### Response Format

```json
{
  "positions": [
    "Personalkosten",
    "Umsatzerlöse",
    "Materialkosten"
  ],
  "series": [
    {
      "positionName": "Personalkosten",
      "type": "Expense",
      "dataPoints": [
        {
          "period": "Jan 2024",
          "amount": -15234.56,
          "year": 2024,
          "month": 1
        },
        {
          "period": "Feb 2024",
          "amount": -15456.78,
          "year": 2024,
          "month": 2
        }
      ]
    },
    {
      "positionName": "Umsatzerlöse",
      "type": "Revenue",
      "dataPoints": [
        {
          "period": "Jan 2024",
          "amount": 45678.90,
          "year": 2024,
          "month": 1
        },
        {
          "period": "Feb 2024",
          "amount": 47890.12,
          "year": 2024,
          "month": 2
        }
      ]
    }
  ]
}
```

#### Response Fields

##### Root Object
| Field | Type | Description |
|-------|------|-------------|
| `positions` | string[] | Array of unique position names included in the response |
| `series` | PositionSeries[] | Array of time series data for each position |

##### PositionSeries Object
| Field | Type | Description |
|-------|------|-------------|
| `positionName` | string | German position name as it appears in BWA reports |
| `type` | string | Transaction classification: "Revenue", "Expense", "Other" |
| `dataPoints` | TrendDataPoint[] | Array of time-ordered data points for this position |

##### TrendDataPoint Object
| Field | Type | Description |
|-------|------|-------------|
| `period` | string | German-formatted period label (e.g., "Jan 2024") |
| `amount` | decimal | Aggregated amount for this position in this period |
| `year` | integer | Year component for sorting and internal processing |
| `month` | integer | Month component (1-12) for sorting and internal processing |

#### Data Processing Rules

##### Aggregation Logic
1. **Grouping**: Transactions grouped by `category` (position name) and time period (year/month)
2. **Summation**: Multiple transactions for the same position/period are summed
3. **Exclusions**: Summary-type transactions (like "Gesamtkosten") are excluded
4. **Ordering**: Data points ordered chronologically within each series

##### German Business Context
- **Position Names**: Preserved exactly as they appear in source BWA documents
  - "Personalkosten" = Personnel costs
  - "Umsatzerlöse" = Revenue from sales  
  - "Materialkosten" = Material costs
  - "Betriebskosten" = Operating costs

- **Period Formatting**: Uses German date conventions
  - Month abbreviations: Jan, Feb, Mär, Apr, Mai, Jun, Jul, Aug, Sep, Okt, Nov, Dez
  - Format: "MMM yyyy" (e.g., "Mär 2024")

##### Type Classification
- **Revenue**: Positions containing keywords like "umsatz", "erlös"
- **Expense**: Positions containing keywords like "kosten", "aufwand"
- **Summary**: Calculated totals like "gesamtkosten", "betriebsergebnis" (excluded from API)

#### Error Responses

##### 500 Internal Server Error
```json
{
  "error": "An error occurred while loading the data."
}
```

**Common Causes**:
- Database connection issues
- Invalid filter parameters
- Data processing errors

#### Performance Considerations

- **Optimization**: Database queries use projections and server-side filtering
- **Caching**: Consider implementing caching for frequently requested data
- **Limits**: No explicit limits, but performance may degrade with very large datasets
- **Timeout**: Standard ASP.NET Core request timeout applies

#### Integration Notes

##### Frontend Integration
- Response format optimized for Chart.js multi-line charts
- Period labels formatted for direct display in German UI
- Color coding recommendations: Green for Revenue, Red for Expense

##### Chart.js Configuration Example
```javascript
const chartData = {
    labels: getAllUniquePeriods(apiResponse.series),
    datasets: apiResponse.series.map((series, index) => ({
        label: series.positionName,
        data: mapDataForPeriods(series.dataPoints),
        borderColor: getColorForType(series.type),
        backgroundColor: getColorForType(series.type) + '20',
        fill: false
    }))
};
```

#### Security Considerations

- **Authentication**: Currently no authentication required (internal application)
- **Input Validation**: Server validates year ranges and position name formats
- **Data Sensitivity**: Contains financial data - ensure appropriate access controls
- **SQL Injection**: Protected via Entity Framework parameterized queries

#### Monitoring and Logging

- **Request Logging**: All API calls logged with parameters
- **Performance Metrics**: Query execution time and data point counts logged
- **Error Tracking**: Detailed error logging for troubleshooting
- **Usage Statistics**: Consider tracking which filters are most commonly used

---

*This API is designed specifically for German BWA financial data processing and follows German accounting conventions.*