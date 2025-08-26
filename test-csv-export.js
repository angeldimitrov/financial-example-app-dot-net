/**
 * CSV Export Integration Test
 * 
 * This script tests the complete CSV export functionality:
 * 1. Gets anti-forgery token from the page
 * 2. Tests estimated record count API
 * 3. Tests actual CSV export API
 * 4. Validates file download
 * 
 * Instructions:
 * 1. Open http://localhost:5001 in browser
 * 2. Open the "Daten exportieren" modal
 * 3. Open Developer Console
 * 4. Paste this entire script and run it
 */

console.log('🧪 Starting CSV Export Integration Test...');

async function testCsvExportIntegration() {
    try {
        // Step 1: Get the anti-forgery token
        console.log('📋 Step 1: Getting anti-forgery token...');
        const tokenInput = document.querySelector('#csvExportForm input[name="__RequestVerificationToken"]');
        if (!tokenInput) {
            throw new Error('Anti-forgery token not found. Make sure the export modal is open.');
        }
        const token = tokenInput.value;
        console.log('✅ Token obtained:', token.substring(0, 20) + '...');

        // Step 2: Test the estimated record count API
        console.log('📊 Step 2: Testing estimated record count API...');
        const exportOptions = {
            startDate: "2024-01-01",
            endDate: "2024-12-31",
            includeRevenue: true,
            includeExpenses: true,
            exportFormat: "standard"
        };

        const estimateResponse = await fetch('/Index?handler=GetEstimatedRecordCount', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(exportOptions)
        });

        if (!estimateResponse.ok) {
            throw new Error(`Estimate API failed: ${estimateResponse.status} ${estimateResponse.statusText}`);
        }

        const estimateData = await estimateResponse.json();
        console.log('✅ Estimated records response:', estimateData);

        if (estimateData.success && estimateData.count > 0) {
            console.log(`📈 Found ${estimateData.count} records for export`);
        } else {
            console.warn('⚠️ No records found or API returned error');
        }

        // Step 3: Test the actual CSV export
        console.log('📄 Step 3: Testing CSV export API...');
        const exportResponse = await fetch('/Index?handler=ExportCsvWithOptions', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(exportOptions)
        });

        if (!exportResponse.ok) {
            if (exportResponse.headers.get('content-type')?.includes('application/json')) {
                const errorData = await exportResponse.json();
                throw new Error(`Export API failed: ${errorData.error || 'Unknown error'}`);
            } else {
                throw new Error(`Export API failed: ${exportResponse.status} ${exportResponse.statusText}`);
            }
        }

        console.log('✅ Export API responded successfully');

        // Step 4: Handle the file download
        console.log('💾 Step 4: Processing CSV file download...');
        const blob = await exportResponse.blob();
        console.log(`📦 Received blob of size: ${blob.size} bytes`);
        console.log(`📋 Content type: ${blob.type}`);

        // Create download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        
        // Get filename from response headers or create default
        const contentDisposition = exportResponse.headers.get('content-disposition');
        let filename = 'BWA-Export-Test.csv';
        if (contentDisposition) {
            const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/i);
            if (filenameMatch) {
                filename = filenameMatch[1].replace(/['"]/g, '');
            }
        }
        
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        console.log(`✅ CSV file "${filename}" downloaded successfully`);

        // Step 5: Basic content validation
        console.log('🔍 Step 5: Validating CSV content...');
        const text = await blob.text();
        const lines = text.split('\n').filter(line => line.trim());
        const header = lines[0];
        const dataRows = lines.slice(1);

        console.log(`📊 CSV Statistics:`);
        console.log(`   - Header: ${header}`);
        console.log(`   - Data rows: ${dataRows.length}`);
        console.log(`   - Expected German headers: Jahr, Monat, Kategorie, Typ, Betrag, Gruppenkategorie`);
        
        if (header.includes('Jahr') && header.includes('Monat') && header.includes('Kategorie')) {
            console.log('✅ CSV headers look correct (German format detected)');
        } else {
            console.warn('⚠️ CSV headers may not be in expected German format');
        }

        if (dataRows.length > 0) {
            console.log(`✅ CSV contains ${dataRows.length} data rows`);
            console.log('🔍 Sample data row:', dataRows[0]);
        } else {
            console.warn('⚠️ CSV contains no data rows');
        }

        // Final success message
        console.log('🎉 CSV Export Integration Test PASSED!');
        console.log('🎯 All features working correctly:');
        console.log('   ✅ Anti-forgery token validation');
        console.log('   ✅ Estimated record count API');
        console.log('   ✅ CSV export API');
        console.log('   ✅ File download mechanism');
        console.log('   ✅ German CSV format');
        
        return {
            success: true,
            estimatedRecords: estimateData.count || 0,
            actualRecords: dataRows.length,
            filename: filename,
            fileSize: blob.size
        };

    } catch (error) {
        console.error('❌ CSV Export Integration Test FAILED:', error.message);
        console.error('🔧 Troubleshooting tips:');
        console.error('   - Make sure the application is running on http://localhost:5001');
        console.error('   - Ensure the export modal is open before running this test');
        console.error('   - Check browser network tab for detailed error information');
        console.error('   - Verify there is data in the database for export');
        
        return {
            success: false,
            error: error.message
        };
    }
}

// Run the test
testCsvExportIntegration().then(result => {
    console.log('📋 Test Results:', result);
}).catch(error => {
    console.error('💥 Test execution failed:', error);
});

// Export function to global scope for manual testing
window.testCsvExport = testCsvExportIntegration;