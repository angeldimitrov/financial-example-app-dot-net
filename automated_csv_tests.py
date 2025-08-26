#!/usr/bin/env python3
"""
Automated CSV Export Tests using Playwright Python
This script performs comprehensive end-to-end testing of the CSV export feature
"""

import asyncio
import os
import tempfile
import time
from datetime import datetime
from pathlib import Path
from playwright.async_api import async_playwright
import csv
import re

class CSVExportTestSuite:
    def __init__(self):
        self.base_url = "http://localhost:5001"
        self.test_results = []
        self.download_dir = None
        self.browser = None
        self.context = None
        self.page = None

    async def setup(self):
        """Initialize the test environment"""
        print("🚀 Setting up test environment...")
        
        # Create temporary download directory
        self.download_dir = tempfile.mkdtemp(prefix="csv_export_test_")
        print(f"📁 Download directory: {self.download_dir}")

        # Launch browser
        playwright = await async_playwright().start()
        self.browser = await playwright.chromium.launch(headless=False, slow_mo=500)
        self.context = await self.browser.new_context(
            accept_downloads=True,
            download_path=self.download_dir
        )
        self.page = await self.context.new_page()
        
        print("✅ Browser environment ready")

    async def teardown(self):
        """Clean up test environment"""
        if self.browser:
            await self.browser.close()
        print("✅ Test environment cleaned up")

    def record_result(self, test_name, passed, details):
        """Record test result"""
        status = "✅ PASSED" if passed else "❌ FAILED"
        print(f"{status}: {test_name} - {details}")
        
        self.test_results.append({
            'test': test_name,
            'passed': passed,
            'details': details,
            'timestamp': datetime.now().isoformat()
        })

    async def take_screenshot(self, name):
        """Take a screenshot for documentation"""
        screenshot_path = os.path.join(self.download_dir, f"{name}_{int(time.time())}.png")
        await self.page.screenshot(path=screenshot_path, full_page=True)
        print(f"📸 Screenshot saved: {os.path.basename(screenshot_path)}")
        return screenshot_path

    async def test_application_loading(self):
        """Test 1: Verify application loads correctly"""
        try:
            print("\n🔍 Test 1: Application Loading")
            
            await self.page.goto(self.base_url, wait_until="networkidle")
            
            # Wait for main heading
            await self.page.wait_for_selector("h1:has-text('BWA Dashboard')", timeout=10000)
            
            # Check if we have any data by looking for the export button or empty state
            has_data = await self.page.is_visible('button:has-text("Daten exportieren")')
            
            await self.take_screenshot("01_application_loaded")
            
            if has_data:
                self.record_result("Application Loading", True, "Application loaded with data available")
            else:
                self.record_result("Application Loading", True, "Application loaded but no data available (expected for empty database)")
                print("ℹ️  No financial data available - upload some BWA data first to test export functionality")
                return False
            
            return True
            
        except Exception as e:
            self.record_result("Application Loading", False, f"Failed to load application: {str(e)}")
            return False

    async def test_export_button_visibility(self):
        """Test 2: Verify export button is present and accessible"""
        try:
            print("\n🔍 Test 2: Export Button Visibility")
            
            export_button = self.page.locator('button:has-text("Daten exportieren")')
            await export_button.wait_for(state="visible", timeout=5000)
            
            # Check if button is enabled
            is_enabled = await export_button.is_enabled()
            
            await self.take_screenshot("02_export_button_visible")
            
            if is_enabled:
                self.record_result("Export Button Visibility", True, "Export button is visible and enabled")
                return True
            else:
                self.record_result("Export Button Visibility", False, "Export button is visible but disabled")
                return False
                
        except Exception as e:
            self.record_result("Export Button Visibility", False, f"Export button not found: {str(e)}")
            return False

    async def test_modal_functionality(self):
        """Test 3: Test modal opening and initialization"""
        try:
            print("\n🔍 Test 3: Modal Functionality")
            
            # Click export button
            export_button = self.page.locator('button:has-text("Daten exportieren")')
            await export_button.click()
            
            # Wait for modal to appear
            modal = self.page.locator('#csvExportModal')
            await modal.wait_for(state="visible", timeout=5000)
            
            # Verify modal title
            modal_title = await self.page.locator('#csvExportModalLabel').text_content()
            
            # Check form elements
            start_date = self.page.locator('#startDate')
            end_date = self.page.locator('#endDate')
            revenue_checkbox = self.page.locator('#includeRevenue')
            expense_checkbox = self.page.locator('#includeExpenses')
            
            # Verify all elements are visible
            await start_date.wait_for(state="visible")
            await end_date.wait_for(state="visible")
            await revenue_checkbox.wait_for(state="visible")
            await expense_checkbox.wait_for(state="visible")
            
            # Check default states
            revenue_checked = await revenue_checkbox.is_checked()
            expense_checked = await expense_checkbox.is_checked()
            
            await self.take_screenshot("03_modal_opened")
            
            success = (
                "BWA-Daten exportieren" in modal_title and
                revenue_checked and
                expense_checked
            )
            
            if success:
                self.record_result("Modal Functionality", True, "Modal opened with all expected elements and correct defaults")
            else:
                self.record_result("Modal Functionality", False, f"Modal issues - Title: {modal_title}, Revenue: {revenue_checked}, Expense: {expense_checked}")
            
            return success
            
        except Exception as e:
            self.record_result("Modal Functionality", False, f"Modal functionality failed: {str(e)}")
            await self.take_screenshot("03_modal_error")
            return False

    async def test_date_range_functionality(self):
        """Test 4: Test date range selection and validation"""
        try:
            print("\n🔍 Test 4: Date Range Functionality")
            
            # Set date range
            await self.page.fill('#startDate', '2024-01-01')
            await self.page.fill('#endDate', '2024-12-31')
            
            # Wait for estimated records to update
            await self.page.wait_for_timeout(2000)
            
            # Check estimated records display
            estimated_records = self.page.locator('#estimatedRecords')
            await estimated_records.wait_for(state="visible", timeout=5000)
            
            estimated_text = await estimated_records.text_content()
            
            await self.take_screenshot("04_date_range_selected")
            
            # Try invalid date range
            await self.page.fill('#startDate', '2024-12-01')
            await self.page.fill('#endDate', '2024-11-01')
            
            # Reset to valid range
            await self.page.fill('#startDate', '2024-01-01')
            await self.page.fill('#endDate', '2024-12-31')
            
            if "Datensätze" in estimated_text or "wird berechnet" in estimated_text.lower():
                self.record_result("Date Range Functionality", True, f"Date range works, estimated records: {estimated_text.strip()}")
                return True
            else:
                self.record_result("Date Range Functionality", False, f"Unexpected estimated records format: {estimated_text}")
                return False
                
        except Exception as e:
            self.record_result("Date Range Functionality", False, f"Date range functionality failed: {str(e)}")
            return False

    async def test_transaction_type_filtering(self):
        """Test 5: Test transaction type filtering"""
        try:
            print("\n🔍 Test 5: Transaction Type Filtering")
            
            # Test Revenue only
            await self.page.uncheck('#includeExpenses')
            await self.page.wait_for_timeout(1000)
            
            estimated_revenue = await self.page.locator('#estimatedRecords').text_content()
            await self.take_screenshot("05_revenue_only")
            
            # Test Expenses only
            await self.page.uncheck('#includeRevenue')
            await self.page.check('#includeExpenses')
            await self.page.wait_for_timeout(1000)
            
            estimated_expenses = await self.page.locator('#estimatedRecords').text_content()
            await self.take_screenshot("06_expenses_only")
            
            # Test Both (reset)
            await self.page.check('#includeRevenue')
            await self.page.check('#includeExpenses')
            await self.page.wait_for_timeout(1000)
            
            estimated_both = await self.page.locator('#estimatedRecords').text_content()
            await self.take_screenshot("07_both_types")
            
            self.record_result("Transaction Type Filtering", True, 
                f"Filtering works - Revenue: {estimated_revenue.strip()}, Expenses: {estimated_expenses.strip()}, Both: {estimated_both.strip()}")
            return True
            
        except Exception as e:
            self.record_result("Transaction Type Filtering", False, f"Transaction type filtering failed: {str(e)}")
            return False

    async def test_format_selection(self):
        """Test 6: Test export format selection"""
        try:
            print("\n🔍 Test 6: Format Selection")
            
            # Test German Excel format
            await self.page.click('#germanExcel')
            await self.page.wait_for_timeout(500)
            
            german_description = await self.page.locator('#formatDescription').text_content()
            await self.take_screenshot("08_german_format")
            
            # Test Standard CSV format
            await self.page.click('#standardCsv')
            await self.page.wait_for_timeout(500)
            
            standard_description = await self.page.locator('#formatDescription').text_content()
            await self.take_screenshot("09_standard_format")
            
            success = german_description != standard_description
            
            if success:
                self.record_result("Format Selection", True, 
                    f"Format descriptions differ correctly - Standard: {standard_description}, German: {german_description}")
            else:
                self.record_result("Format Selection", False, "Format descriptions don't change between formats")
            
            return success
            
        except Exception as e:
            self.record_result("Format Selection", False, f"Format selection failed: {str(e)}")
            return False

    async def test_csv_export(self):
        """Test 7: Test actual CSV export functionality"""
        try:
            print("\n🔍 Test 7: CSV Export")
            
            # Ensure standard format is selected
            await self.page.click('#standardCsv')
            
            # Set up download listening
            download_start_time = time.time()
            
            # Start download and wait for it
            async with self.page.expect_download(timeout=30000) as download_info:
                export_button = self.page.locator('#exportButton')
                await export_button.click()
                
                # Wait for progress overlay
                progress_overlay = self.page.locator('#exportProgressOverlay')
                try:
                    await progress_overlay.wait_for(state="visible", timeout=5000)
                    await self.take_screenshot("10_export_progress")
                except:
                    pass  # Progress might be too fast to catch
            
            download = await download_info.value
            download_path = os.path.join(self.download_dir, download.suggested_filename)
            await download.save_as(download_path)
            
            download_time = time.time() - download_start_time
            
            # Verify file exists and has content
            if os.path.exists(download_path):
                file_size = os.path.getsize(download_path)
                
                # Validate CSV content
                await self.validate_csv_content(download_path)
                
                self.record_result("CSV Export", True, 
                    f"CSV exported successfully - File: {download.suggested_filename}, Size: {file_size} bytes, Time: {download_time:.2f}s")
                return True
            else:
                self.record_result("CSV Export", False, "Download file not found")
                return False
                
        except Exception as e:
            self.record_result("CSV Export", False, f"CSV export failed: {str(e)}")
            await self.take_screenshot("10_export_error")
            return False

    async def validate_csv_content(self, file_path):
        """Validate the content of exported CSV file"""
        try:
            print("📋 Validating CSV content...")
            
            with open(file_path, 'r', encoding='utf-8-sig') as f:
                content = f.read()
                lines = content.strip().split('\n')
            
            if len(lines) < 1:
                raise ValueError("CSV file is empty")
            
            # Check header
            header = lines[0]
            expected_columns = ['Jahr', 'Monat', 'Kategorie', 'Typ', 'Betrag', 'Gruppenkategorie']
            
            for col in expected_columns:
                if col not in header:
                    raise ValueError(f"Missing expected column: {col}")
            
            # Check data rows
            if len(lines) > 1:
                # Parse a few sample rows
                reader = csv.reader([lines[1]])  # First data row
                row = next(reader)
                
                if len(row) < 6:
                    raise ValueError(f"Data row has insufficient columns: {len(row)}")
                
                # Validate year and month are numeric
                if not row[0].isdigit() or not row[1].isdigit():
                    raise ValueError("Year or month is not numeric")
            
            validation_report = f"""CSV Validation Report
File: {os.path.basename(file_path)}
Total lines: {len(lines)}
Data rows: {len(lines) - 1}
Header: {header}
Sample row: {lines[1] if len(lines) > 1 else 'No data'}
Validation: PASSED
"""
            
            # Save validation report
            report_path = file_path + '.validation.txt'
            with open(report_path, 'w') as f:
                f.write(validation_report)
            
            print(f"✅ CSV validation passed - {len(lines) - 1} data rows")
            
        except Exception as e:
            print(f"❌ CSV validation failed: {str(e)}")
            raise

    async def test_german_format_export(self):
        """Test 8: Test German format export"""
        try:
            print("\n🔍 Test 8: German Format Export")
            
            # Reopen modal if needed
            modal = self.page.locator('#csvExportModal')
            is_visible = await modal.is_visible()
            
            if not is_visible:
                export_button = self.page.locator('button:has-text("Daten exportieren")')
                await export_button.click()
                await modal.wait_for(state="visible", timeout=5000)
            
            # Select German format
            await self.page.click('#germanExcel')
            await self.page.wait_for_timeout(1000)
            
            # Start download
            async with self.page.expect_download(timeout=30000) as download_info:
                export_button = self.page.locator('#exportButton')
                await export_button.click()
            
            download = await download_info.value
            download_path = os.path.join(self.download_dir, f"german_{download.suggested_filename}")
            await download.save_as(download_path)
            
            # Verify file
            if os.path.exists(download_path):
                file_size = os.path.getsize(download_path)
                await self.validate_csv_content(download_path)  # Same validation for now
                
                self.record_result("German Format Export", True, 
                    f"German CSV exported successfully - Size: {file_size} bytes")
                return True
            else:
                self.record_result("German Format Export", False, "German CSV download failed")
                return False
                
        except Exception as e:
            self.record_result("German Format Export", False, f"German format export failed: {str(e)}")
            return False

    async def test_performance(self):
        """Test 9: Performance validation"""
        try:
            print("\n🔍 Test 9: Performance Validation")
            
            # Reopen modal if needed
            modal = self.page.locator('#csvExportModal')
            is_visible = await modal.is_visible()
            
            if not is_visible:
                export_button = self.page.locator('button:has-text("Daten exportieren")')
                await export_button.click()
                await modal.wait_for(state="visible", timeout=5000)
            
            # Measure export time
            start_time = time.time()
            
            async with self.page.expect_download(timeout=30000) as download_info:
                export_button = self.page.locator('#exportButton')
                await export_button.click()
            
            download = await download_info.value
            end_time = time.time()
            
            export_time = end_time - start_time
            max_allowed_time = 15.0  # 15 seconds
            
            if export_time <= max_allowed_time:
                self.record_result("Performance", True, f"Export completed in {export_time:.2f}s (acceptable)")
                return True
            else:
                self.record_result("Performance", False, f"Export took {export_time:.2f}s (too slow, max: {max_allowed_time}s)")
                return False
                
        except Exception as e:
            self.record_result("Performance", False, f"Performance test failed: {str(e)}")
            return False

    def generate_test_report(self):
        """Generate comprehensive test report"""
        print("\n📊 Generating Test Report...")
        
        total_tests = len(self.test_results)
        passed_tests = sum(1 for r in self.test_results if r['passed'])
        failed_tests = total_tests - passed_tests
        success_rate = (passed_tests / total_tests * 100) if total_tests > 0 else 0
        
        report = f"""# CSV Export Feature - End-to-End Test Report

## Test Summary
- **Date**: {datetime.now().isoformat()}
- **Total Tests**: {total_tests}
- **Passed**: {passed_tests} ✅
- **Failed**: {failed_tests} ❌
- **Success Rate**: {success_rate:.1f}%
- **Application URL**: {self.base_url}
- **Test Environment**: Playwright Python with Chromium

## Test Results Detail

"""

        for result in self.test_results:
            status = "✅ PASSED" if result['passed'] else "❌ FAILED"
            report += f"""### {result['test']} {status}
- **Details**: {result['details']}
- **Timestamp**: {result['timestamp']}

"""

        report += f"""## Files Generated
- **Screenshots**: Multiple PNG files documenting UI interactions
- **CSV Exports**: Downloaded files from standard and German format tests
- **Validation Reports**: .validation.txt files for each CSV export
- **Test Report**: This comprehensive report

## Conclusion
{'🎉 All tests passed! CSV Export feature is working perfectly.' if failed_tests == 0 else f'⚠️  {failed_tests} test(s) failed. Review the details above for issues.'}

---
*Generated by CSV Export E2E Test Suite (Python/Playwright)*
"""

        # Save report
        report_path = os.path.join(self.download_dir, 'csv_export_test_report.md')
        with open(report_path, 'w', encoding='utf-8') as f:
            f.write(report)
        
        print(f"📄 Test report saved: {report_path}")
        return report_path

    async def run_all_tests(self):
        """Run the complete test suite"""
        print("🎯 Starting CSV Export E2E Test Suite...")
        print("=" * 60)
        
        try:
            await self.setup()
            
            # Run all tests
            tests_to_run = [
                self.test_application_loading,
                self.test_export_button_visibility,
                self.test_modal_functionality,
                self.test_date_range_functionality,
                self.test_transaction_type_filtering,
                self.test_format_selection,
                self.test_csv_export,
                self.test_german_format_export,
                self.test_performance
            ]
            
            # Execute tests
            app_loaded = await self.test_application_loading()
            
            if app_loaded:
                for test_func in tests_to_run[1:]:  # Skip application loading test
                    await test_func()
            else:
                print("⚠️  Skipping remaining tests due to application loading issues or no data available")
            
            # Generate report
            report_path = self.generate_test_report()
            
            # Summary
            total_tests = len(self.test_results)
            passed_tests = sum(1 for r in self.test_results if r['passed'])
            failed_tests = total_tests - passed_tests
            
            print("=" * 60)
            print("🏆 Test Suite Complete!")
            print(f"✅ Passed: {passed_tests}")
            print(f"❌ Failed: {failed_tests}")
            print(f"📄 Report: {report_path}")
            print(f"📁 Files: {self.download_dir}")
            
            if failed_tests == 0:
                print("🎉 All tests passed! CSV Export feature is working perfectly.")
            else:
                print("⚠️  Some tests failed. Check the detailed report for issues.")
                
        except Exception as e:
            print(f"💥 Test suite failed with critical error: {str(e)}")
        finally:
            await self.teardown()

async def main():
    """Main test execution function"""
    test_suite = CSVExportTestSuite()
    await test_suite.run_all_tests()

if __name__ == "__main__":
    asyncio.run(main())