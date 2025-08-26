#!/bin/bash

# Run the CSV Export Service unit tests
cd /Users/angel/Sites/finance-example-app

echo "Building the solution..."
dotnet build

echo ""
echo "Running CSV Export Service tests..."
dotnet test tests/FinanceApp.Tests/FinanceApp.Tests.csproj --filter "CsvExportServiceTests" --verbosity normal

echo ""
echo "Test run completed!"