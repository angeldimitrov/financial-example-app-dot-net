/**
 * Premium CSV Export Manager for BWA Financial Data
 * 
 * Handles CSV export functionality for German financial applications
 * with professional UI feedback, error handling, and localization support
 * 
 * Features:
 * - Secure AJAX-based export requests
 * - Loading state management with visual feedback
 * - Automatic file download with progress indication
 * - German localization and business context
 * - Error handling with user-friendly messages
 * - CSRF protection for security
 * - Responsive design considerations
 */

class CsvExportManager {
    constructor() {
        this.exportButton = null;
        this.isExporting = false;
        this.requestTimeoutId = null;
        
        // German localization strings
        this.messages = {
            exporting: 'Exportiere Daten...',
            success: 'CSV-Datei erfolgreich heruntergeladen',
            noData: 'Keine Daten zum Exportieren verfügbar',
            error: 'Fehler beim Exportieren der Daten',
            timeout: 'Export-Vorgang überschritten. Bitte versuchen Sie es erneut.',
            networkError: 'Netzwerkfehler. Bitte überprüfen Sie Ihre Internetverbindung.',
            serverError: 'Server-Fehler. Bitte kontaktieren Sie den Support.'
        };
        
        // Configuration
        this.config = {
            exportUrl: '/?handler=ExportCsv',
            timeout: 30000, // 30 seconds
            retryAttempts: 2,
            retryDelay: 1000
        };
        
        this.init();
    }
    
    /**
     * Initialize the CSV export functionality
     * Sets up event listeners and validates prerequisites
     */
    init() {
        this.exportButton = document.getElementById('csvExport');
        
        if (!this.exportButton) {
            console.warn('CSV Export: Button element not found');
            return;
        }
        
        // Check if we have data to export
        if (!this.hasExportableData()) {
            this.disableExportButton();
            return;
        }
        
        this.setupEventListeners();
        this.setupTooltips();
        
        console.log('CSV Export Manager initialized ✅');
    }
    
    /**
     * Check if there's data available for export
     * Based on the presence of monthly summaries data
     */
    hasExportableData() {
        // Check if we have chart data or monthly summaries
        const chartCanvas = document.getElementById('financeChart');
        const summaryTable = document.querySelector('.table-premium tbody tr');
        
        return chartCanvas && summaryTable;
    }
    
    /**
     * Set up event listeners for export functionality
     */
    setupEventListeners() {
        this.exportButton.addEventListener('click', (e) => {
            e.preventDefault();
            this.handleExportClick();
        });
        
        // Handle keyboard accessibility
        this.exportButton.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                this.handleExportClick();
            }
        });
    }
    
    /**
     * Set up tooltips and accessibility features
     */
    setupTooltips() {
        this.exportButton.setAttribute('aria-label', 'BWA-Daten als CSV-Datei exportieren');
        
        // Update tooltip based on data availability
        if (this.hasExportableData()) {
            this.exportButton.title = 'BWA-Daten als CSV exportieren';
        } else {
            this.exportButton.title = 'Keine Daten zum Exportieren verfügbar';
        }
    }
    
    /**
     * Handle export button click with comprehensive error handling
     */
    async handleExportClick() {
        if (this.isExporting) {
            return; // Prevent double-clicks during export
        }
        
        if (!this.hasExportableData()) {
            this.showMessage(this.messages.noData, 'warning');
            return;
        }
        
        try {
            await this.performExport();
        } catch (error) {
            console.error('CSV Export Error:', error);
            this.handleExportError(error);
        } finally {
            this.resetExportButton();
        }
    }
    
    /**
     * Perform the actual CSV export operation
     * Implements retry logic and comprehensive error handling
     */
    async performExport() {
        this.setLoadingState();
        
        let lastError = null;
        
        for (let attempt = 1; attempt <= this.config.retryAttempts; attempt++) {
            try {
                console.log(`CSV Export attempt ${attempt}/${this.config.retryAttempts}`);
                
                const response = await this.makeExportRequest();
                
                if (response.ok) {
                    await this.handleSuccessfulExport(response);
                    return; // Success, exit retry loop
                }
                
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                
            } catch (error) {
                lastError = error;
                console.warn(`Export attempt ${attempt} failed:`, error.message);
                
                // Wait before retry (except on last attempt)
                if (attempt < this.config.retryAttempts) {
                    await this.delay(this.config.retryDelay * attempt);
                    this.updateLoadingMessage(`Wiederhole Export-Versuch ${attempt + 1}...`);
                }
            }
        }
        
        // All attempts failed
        throw lastError;
    }
    
    /**
     * Make the CSRF-protected export request
     */
    async makeExportRequest() {
        // Get CSRF token for security
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        
        const controller = new AbortController();
        this.requestTimeoutId = setTimeout(() => {
            controller.abort();
        }, this.config.timeout);
        
        const requestOptions = {
            method: 'GET',
            headers: {
                'RequestVerificationToken': token
            },
            credentials: 'same-origin',
            signal: controller.signal
        };
        
        try {
            return await fetch(this.config.exportUrl, requestOptions);
        } finally {
            if (this.requestTimeoutId) {
                clearTimeout(this.requestTimeoutId);
                this.requestTimeoutId = null;
            }
        }
    }
    
    /**
     * Handle successful export response
     * Triggers file download and shows success feedback
     */
    async handleSuccessfulExport(response) {
        try {
            // Check if response contains file data
            const blob = await response.blob();
            
            if (blob.size === 0) {
                throw new Error('Empty file received from server');
            }
            
            // Generate filename with timestamp
            const timestamp = new Date().toISOString().slice(0, 19).replace(/[:-]/g, '');
            const filename = `bwa-export-${timestamp}.csv`;
            
            // Trigger download
            this.downloadFile(blob, filename);
            
            // Show success message
            this.showMessage(this.messages.success, 'success');
            
            console.log('CSV export completed successfully:', filename);
            
        } catch (error) {
            throw new Error(`Download processing failed: ${error.message}`);
        }
    }
    
    /**
     * Download file using blob and temporary anchor element
     */
    downloadFile(blob, filename) {
        const url = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        
        anchor.href = url;
        anchor.download = filename;
        anchor.style.display = 'none';
        
        // Trigger download
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        
        // Clean up blob URL
        setTimeout(() => window.URL.revokeObjectURL(url), 1000);
    }
    
    /**
     * Handle export errors with appropriate user feedback
     */
    handleExportError(error) {
        let message = this.messages.error;
        let type = 'danger';
        
        if (error.name === 'AbortError') {
            message = this.messages.timeout;
        } else if (error.message.includes('Failed to fetch') || error.message.includes('network')) {
            message = this.messages.networkError;
        } else if (error.message.includes('HTTP 5')) {
            message = this.messages.serverError;
        }
        
        this.showMessage(message, type);
    }
    
    /**
     * Set button to loading state with visual feedback
     */
    setLoadingState() {
        this.isExporting = true;
        this.exportButton.disabled = true;
        
        // Store original content
        this.originalButtonContent = this.exportButton.innerHTML;
        
        // Show loading state
        this.exportButton.innerHTML = `
            <div class="d-flex align-items-center">
                <div class="loading-spinner me-2"></div>
                <span>${this.messages.exporting}</span>
            </div>
        `;
        
        this.exportButton.classList.add('btn-loading');
        this.exportButton.setAttribute('aria-busy', 'true');
    }
    
    /**
     * Update loading message during retry attempts
     */
    updateLoadingMessage(message) {
        if (this.isExporting && this.exportButton) {
            const messageSpan = this.exportButton.querySelector('span');
            if (messageSpan) {
                messageSpan.textContent = message;
            }
        }
    }
    
    /**
     * Reset button to original state
     */
    resetExportButton() {
        this.isExporting = false;
        this.exportButton.disabled = false;
        
        if (this.originalButtonContent) {
            this.exportButton.innerHTML = this.originalButtonContent;
        }
        
        this.exportButton.classList.remove('btn-loading');
        this.exportButton.removeAttribute('aria-busy');
        
        if (this.requestTimeoutId) {
            clearTimeout(this.requestTimeoutId);
            this.requestTimeoutId = null;
        }
    }
    
    /**
     * Disable export button when no data is available
     */
    disableExportButton() {
        if (this.exportButton) {
            this.exportButton.disabled = true;
            this.exportButton.classList.add('btn-disabled');
            this.exportButton.title = this.messages.noData;
        }
    }
    
    /**
     * Show user feedback message using premium alert system
     */
    showMessage(message, type = 'info') {
        // Create alert element with premium styling
        const alertHtml = `
            <div class="alert-premium alert-premium-${type} animate-in mb-4" role="alert" id="csvExportAlert">
                <div class="d-flex align-items-start">
                    <i class="bi ${this.getAlertIcon(type)} me-3 mt-1 fs-5"></i>
                    <div class="flex-grow-1">
                        <div class="fw-semibold mb-1">${this.getAlertTitle(type)}</div>
                        <div>${message}</div>
                    </div>
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Schließen"></button>
                </div>
            </div>
        `;
        
        // Find insertion point (after dashboard header)
        const insertionPoint = document.querySelector('.dashboard-header');
        if (insertionPoint && insertionPoint.parentNode) {
            // Remove any existing alert
            const existingAlert = document.getElementById('csvExportAlert');
            if (existingAlert) {
                existingAlert.remove();
            }
            
            // Insert new alert
            insertionPoint.insertAdjacentHTML('afterend', alertHtml);
            
            // Auto-dismiss after 5 seconds for non-error messages
            if (type !== 'danger') {
                setTimeout(() => {
                    const alert = document.getElementById('csvExportAlert');
                    if (alert) {
                        alert.remove();
                    }
                }, 5000);
            }
        }
    }
    
    /**
     * Get appropriate icon for alert type
     */
    getAlertIcon(type) {
        const icons = {
            success: 'bi-check-circle-fill',
            danger: 'bi-x-circle-fill',
            warning: 'bi-exclamation-triangle-fill',
            info: 'bi-info-circle-fill'
        };
        return icons[type] || icons.info;
    }
    
    /**
     * Get appropriate title for alert type
     */
    getAlertTitle(type) {
        const titles = {
            success: 'Export erfolgreich',
            danger: 'Export-Fehler',
            warning: 'Hinweis',
            info: 'Information'
        };
        return titles[type] || titles.info;
    }
    
    /**
     * Utility function for delays
     */
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}

// CSS for loading spinner and button states
const csvExportStyles = `
    <style id="csv-export-styles">
        /* Loading spinner animation */
        .loading-spinner {
            width: 14px;
            height: 14px;
            border: 2px solid transparent;
            border-top: 2px solid currentColor;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }
        
        @keyframes spin {
            0% { transform: rotate(0deg); }
            100% { transform: rotate(360deg); }
        }
        
        /* Loading button state */
        .btn-loading {
            position: relative;
            pointer-events: none;
            opacity: 0.8;
        }
        
        /* Disabled button state */
        .btn-disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }
        
        /* CSV export button specific styles */
        #csvExport {
            min-width: 70px;
            transition: all var(--timing-normal) var(--easing-ease-out);
        }
        
        #csvExport:hover:not(:disabled) {
            transform: translateY(-1px);
            box-shadow: var(--shadow-md);
        }
        
        #csvExport:active:not(:disabled) {
            transform: translateY(0);
        }
        
        /* Responsive adjustments */
        @media (max-width: 768px) {
            #csvExport .d-none.d-md-inline {
                display: none !important;
            }
        }
    </style>
`;

// Initialize CSV Export Manager when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    // Inject CSS styles
    document.head.insertAdjacentHTML('beforeend', csvExportStyles);
    
    // Initialize CSV Export Manager
    window.csvExportManager = new CsvExportManager();
    
    console.log('CSV Export functionality loaded ✨');
});

// Export for potential external use
if (typeof module !== 'undefined' && module.exports) {
    module.exports = CsvExportManager;
}