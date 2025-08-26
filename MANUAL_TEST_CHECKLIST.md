# CSV Export - Manual Testing Checklist

## Prerequisites ✅
- [ ] Application is running at http://localhost:5001
- [ ] PostgreSQL database is running with financial data
- [ ] Browser is open and ready for testing

## Test Execution Steps

### 1. Application Access 🌐
- [ ] Navigate to http://localhost:5001
- [ ] Verify BWA Dashboard loads successfully
- [ ] Check that monthly summary data is displayed (if available)
- [ ] Verify no console errors in browser developer tools

**Expected Result**: Professional dashboard interface with financial data or empty state message

---

### 2. Export Button Visibility 🔍
- [ ] Scroll to the monthly summary table
- [ ] Locate the action buttons section at the bottom
- [ ] Verify "Daten exportieren" button is present
- [ ] Verify button has download icon and blue styling
- [ ] Verify button is clickable (not disabled)

**Expected Result**: Blue export button with download icon is visible and enabled

---

### 3. Modal Opening and Initialization 📋
- [ ] Click "Daten exportieren" button
- [ ] Verify modal opens with smooth animation
- [ ] Check modal title: "BWA-Daten exportieren"
- [ ] Verify all form sections are present:
  - [ ] Date range section (Von/Bis)
  - [ ] Transaction type checkboxes
  - [ ] Export format radio buttons
  - [ ] Preview section with estimated records

**Expected Result**: Professional modal with all export options visible

---

### 4. Date Range Functionality 📅
- [ ] Check that start/end dates are pre-populated
- [ ] Modify start date to 2024-01-01
- [ ] Modify end date to 2024-12-31
- [ ] Observe estimated record count updates
- [ ] Try invalid date range (start after end)
- [ ] Verify validation prevents invalid ranges

**Expected Result**: Dynamic record count updates, validation prevents invalid dates

---

### 5. Transaction Type Filtering 🎯
- [ ] Both checkboxes should be checked by default
- [ ] Uncheck "Kosten einbeziehen" (expenses)
- [ ] Observe record count change
- [ ] Uncheck "Erlöse einbeziehen" (revenue)  
- [ ] Check "Kosten einbeziehen" (now only expenses)
- [ ] Observe record count change
- [ ] Check both boxes again

**Expected Result**: Record count changes based on selected transaction types

---

### 6. Format Selection 📝
- [ ] Verify "Standard CSV" is selected by default
- [ ] Read the format description
- [ ] Click "German Excel" radio button
- [ ] Verify description updates to mention German format
- [ ] Switch back to "Standard CSV"

**Expected Result**: Format descriptions update to explain the differences

---

### 7. Standard CSV Export 💾
- [ ] Ensure "Standard CSV" format is selected
- [ ] Ensure both transaction types are checked
- [ ] Click "Export starten" button
- [ ] Verify progress overlay appears
- [ ] Watch progress animation
- [ ] Verify file downloads automatically
- [ ] Check filename includes date range and timestamp

**Expected Result**: CSV file downloads with proper naming convention

---

### 8. CSV Content Validation 📊
- [ ] Open the downloaded CSV file in a text editor
- [ ] Verify headers are in German: "Jahr,Monat,Kategorie,Typ,Betrag,Gruppenkategorie"
- [ ] Check that data rows contain financial transaction data
- [ ] Verify Year and Month columns have numeric values
- [ ] Verify Betrag (Amount) column has decimal numbers
- [ ] Count data rows and compare with estimated count

**Expected Result**: Valid CSV with German headers and transaction data

---

### 9. German Format Export 🇩🇪
- [ ] Reopen export modal (click "Daten exportieren" again)
- [ ] Select "German Excel" format
- [ ] Verify format description mentions German formatting
- [ ] Click "Export starten"
- [ ] Verify second file downloads
- [ ] Compare filename with previous export

**Expected Result**: Second CSV file downloads with German format

---

### 10. Error Handling Testing ⚠️
- [ ] Try to submit with no transaction types selected (if possible)
- [ ] Test with very long date ranges
- [ ] Test with future dates (if any validation)
- [ ] Close modal and reopen to test reset

**Expected Result**: Graceful error handling with German error messages

---

## Performance Verification ⏱️

### Timing Expectations
- [ ] Modal opens within 1 second
- [ ] Record count updates within 2 seconds
- [ ] Export completes within 10 seconds for typical datasets
- [ ] UI remains responsive during export

**Expected Result**: Fast, responsive user experience

---

## Browser Compatibility 🌍
Test the above steps in multiple browsers:
- [ ] Chrome/Chromium
- [ ] Firefox
- [ ] Safari (macOS)
- [ ] Edge (Windows)

**Expected Result**: Consistent behavior across all browsers

---

## Security Validation 🔒
- [ ] Check browser developer tools for any security warnings
- [ ] Verify HTTPS is used (if applicable)
- [ ] Test with disabled JavaScript (should degrade gracefully)
- [ ] Verify no sensitive data in URLs or console logs

**Expected Result**: No security warnings or data exposure

---

## Final Validation Checklist ✅

### Core Functionality
- [ ] All export scenarios work correctly
- [ ] Both CSV formats export successfully
- [ ] Data filtering works as expected
- [ ] File downloads complete successfully

### User Experience  
- [ ] Modal is professional and intuitive
- [ ] Progress indication is clear
- [ ] Error messages are helpful
- [ ] Overall experience is smooth

### Technical Quality
- [ ] No JavaScript errors in console
- [ ] CSV files are properly formatted
- [ ] German headers are correct
- [ ] File encoding supports German characters

---

## Test Results Summary

**Date Tested**: _______________
**Browser Used**: _______________
**Test Result**: ⭐ PASS / ❌ FAIL

### Issues Found:
_List any issues discovered during testing_

1. ________________________________
2. ________________________________
3. ________________________________

### Overall Assessment:
_Your overall assessment of the CSV export feature_

---

**Manual Testing Completed By**: _______________
**Date**: _______________
**Signature**: _______________