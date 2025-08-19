/**
 * Chart Utilities for BWA Financial Data Visualization
 * 
 * Utility functions for German number formatting, color management,
 * and common chart operations optimized for financial data.
 * 
 * Features:
 * - German locale formatting (1.234,56 €)
 * - Professional color palette management
 * - Performance optimization utilities
 * - Date/time formatting for BWA periods
 * - Chart.js configuration helpers
 * 
 * Business Context:
 * Provides standardized formatting and styling for German financial data
 * visualization, ensuring consistency across all chart components.
 */
class ChartUtils {
    /**
     * German locale currency formatter
     * Formats numbers according to German standards with Euro currency
     */
    static currencyFormatter = new Intl.NumberFormat('de-DE', {
        style: 'currency',
        currency: 'EUR',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });

    /**
     * German locale number formatter (without currency)
     * For axis labels and other non-currency numbers
     */
    static numberFormatter = new Intl.NumberFormat('de-DE', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });

    /**
     * Compact number formatter for large values
     * Converts large numbers to K/M format (e.g., 1.2K, 1.5M)
     */
    static compactFormatter = new Intl.NumberFormat('de-DE', {
        notation: 'compact',
        compactDisplay: 'short',
        minimumFractionDigits: 0,
        maximumFractionDigits: 1
    });

    /**
     * Date formatter for German BWA periods
     * Formats dates for chart labels and tooltips
     */
    static dateFormatter = new Intl.DateTimeFormat('de-DE', {
        year: 'numeric',
        month: 'long'
    });

    /**
     * Short date formatter for axis labels
     */
    static shortDateFormatter = new Intl.DateTimeFormat('de-DE', {
        year: 'numeric',
        month: 'short'
    });

    /**
     * Professional color palette for financial data visualization
     * Colors selected for accessibility and professional appearance
     */
    static colorPalette = {
        // Primary colors for main categories
        revenue: '#28a745',      // Green for revenue
        expense: '#dc3545',      // Red for expenses  
        profit: '#17a2b8',       // Blue for profit
        loss: '#ffc107',         // Yellow for loss
        
        // BWA-specific position colors
        personnel: '#6f42c1',    // Purple for personnel costs
        facilities: '#fd7e14',   // Orange for facility costs
        depreciation: '#20c997', // Teal for depreciation
        taxes: '#e83e8c',        // Pink for taxes
        interest: '#6c757d',     // Gray for interest
        
        // Extended palette for multiple positions
        extended: [
            '#1f77b4', '#ff7f0e', '#2ca02c', '#d62728', '#9467bd',
            '#8c564b', '#e377c2', '#7f7f7f', '#bcbd22', '#17becf',
            '#aec7e8', '#ffbb78', '#98df8a', '#ff9896', '#c5b0d5',
            '#c49c94', '#f7b6d3', '#c7c7c7', '#dbdb8d', '#9edae5'
        ]
    };

    /**
     * Format currency value with German locale
     * 
     * @param {number|null|undefined} value - Numeric value to format
     * @param {Object} options - Formatting options
     * @param {boolean} [options.includeCurrency=true] - Include € symbol
     * @param {boolean} [options.compact=false] - Use compact notation for large values
     * @param {number} [options.decimals=2] - Number of decimal places
     * @returns {string} Formatted currency string
     */
    static formatCurrency(value, options = {}) {
        const {
            includeCurrency = true,
            compact = false,
            decimals = 2
        } = options;

        // Handle null/undefined/NaN values
        if (value === null || value === undefined || isNaN(value)) {
            return includeCurrency ? '€0,00' : '0,00';
        }

        // Use compact formatter for large values
        if (compact && Math.abs(value) >= 1000) {
            const formatted = this.compactFormatter.format(value);
            return includeCurrency ? `${formatted}€` : formatted;
        }

        // Standard currency formatting
        if (includeCurrency) {
            return this.currencyFormatter.format(value);
        }

        // Number-only formatting
        return value.toLocaleString('de-DE', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        });
    }

    /**
     * Format percentage value with German locale
     * 
     * @param {number} value - Decimal percentage value (0.15 for 15%)
     * @param {number} [decimals=1] - Number of decimal places
     * @returns {string} Formatted percentage string
     */
    static formatPercentage(value, decimals = 1) {
        if (value === null || value === undefined || isNaN(value)) {
            return '0,0%';
        }

        return (value * 100).toLocaleString('de-DE', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        }) + '%';
    }

    /**
     * Format date for BWA period display
     * 
     * @param {string|Date} date - Date to format (ISO string or Date object)
     * @param {boolean} [short=false] - Use short format
     * @returns {string} Formatted date string
     */
    static formatBwaDate(date, short = false) {
        if (!date) return '';

        const dateObj = typeof date === 'string' ? new Date(date + '-01') : date;
        
        if (isNaN(dateObj.getTime())) {
            return '';
        }

        return short ? 
            this.shortDateFormatter.format(dateObj) : 
            this.dateFormatter.format(dateObj);
    }

    /**
     * Get color for BWA position based on category
     * 
     * @param {string} positionName - BWA position name
     * @param {number} [index=0] - Fallback color index
     * @returns {string} Hex color code
     */
    static getBwaPositionColor(positionName, index = 0) {
        if (!positionName) {
            return this.colorPalette.extended[index % this.colorPalette.extended.length];
        }

        const name = positionName.toLowerCase();
        
        // Specific color mapping for common BWA positions
        const colorMap = {
            'umsatz': this.colorPalette.revenue,
            'erlös': this.colorPalette.revenue,
            'personal': this.colorPalette.personnel,
            'lohn': this.colorPalette.personnel,
            'gehalt': this.colorPalette.personnel,
            'raum': this.colorPalette.facilities,
            'miete': this.colorPalette.facilities,
            'abschreibung': this.colorPalette.depreciation,
            'steuer': this.colorPalette.taxes,
            'zins': this.colorPalette.interest,
            'gewinn': this.colorPalette.profit,
            'verlust': this.colorPalette.loss
        };

        // Find matching color
        for (const [keyword, color] of Object.entries(colorMap)) {
            if (name.includes(keyword)) {
                return color;
            }
        }

        // Fallback to extended palette
        return this.colorPalette.extended[index % this.colorPalette.extended.length];
    }

    /**
     * Convert hex color to RGBA with specified alpha
     * 
     * @param {string} hex - Hex color code (#rrggbb or #rgb)
     * @param {number} alpha - Alpha value (0-1)
     * @returns {string} RGBA color string
     */
    static hexToRgba(hex, alpha) {
        // Remove # if present
        hex = hex.replace('#', '');
        
        // Handle 3-character hex codes
        if (hex.length === 3) {
            hex = hex.split('').map(char => char + char).join('');
        }
        
        // Validate hex format
        if (hex.length !== 6 || !/^[0-9A-Fa-f]+$/.test(hex)) {
            console.warn(`Invalid hex color: #${hex}`);
            return `rgba(128, 128, 128, ${alpha})`; // Gray fallback
        }

        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    /**
     * Calculate color brightness (0-255 scale)
     * Used to determine if text should be light or dark on colored backgrounds
     * 
     * @param {string} color - Hex color code
     * @returns {number} Brightness value (0-255)
     */
    static calculateBrightness(color) {
        const hex = color.replace('#', '');
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        
        // Use standard luminance formula
        return (r * 299 + g * 587 + b * 114) / 1000;
    }

    /**
     * Get contrasting text color (black or white) for background
     * 
     * @param {string} backgroundColor - Background color hex code
     * @returns {string} Either '#000000' or '#ffffff'
     */
    static getContrastingTextColor(backgroundColor) {
        const brightness = this.calculateBrightness(backgroundColor);
        return brightness > 128 ? '#000000' : '#ffffff';
    }

    /**
     * Generate Chart.js tooltip configuration for German locale
     * 
     * @param {Object} options - Tooltip configuration options
     * @param {boolean} [options.showTrend=true] - Show trend information
     * @param {boolean} [options.showPercentage=false] - Show percentage values
     * @returns {Object} Chart.js tooltip configuration
     */
    static getGermanTooltipConfig(options = {}) {
        const { showTrend = true, showPercentage = false } = options;
        
        return {
            backgroundColor: 'rgba(33, 37, 41, 0.95)',
            titleColor: '#fff',
            bodyColor: '#fff',
            borderColor: '#495057',
            borderWidth: 1,
            cornerRadius: 6,
            displayColors: true,
            callbacks: {
                title: (context) => {
                    if (context.length === 0) return '';
                    
                    // Format date title
                    const label = context[0].label;
                    if (label && (label.includes('-') || label.includes('/'))) {
                        return this.formatBwaDate(label);
                    }
                    return label;
                },
                label: (context) => {
                    const value = context.parsed.y;
                    const datasetLabel = context.dataset.label;
                    
                    let formattedValue = this.formatCurrency(value);
                    
                    if (showPercentage && context.dataset._percentageValues) {
                        const percentage = context.dataset._percentageValues[context.dataIndex];
                        if (percentage !== undefined) {
                            formattedValue += ` (${this.formatPercentage(percentage)})`;
                        }
                    }
                    
                    return `${datasetLabel}: ${formattedValue}`;
                },
                footer: showTrend ? (tooltipItems) => {
                    // Show trend information in footer
                    const current = tooltipItems[0];
                    if (current.dataIndex > 0) {
                        const dataset = current.dataset;
                        const currentValue = current.parsed.y;
                        const previousValue = dataset.data[current.dataIndex - 1]?.y;
                        
                        if (previousValue !== undefined && previousValue !== null && currentValue !== null) {
                            const change = currentValue - previousValue;
                            const changePercent = previousValue !== 0 ? 
                                (change / Math.abs(previousValue)) : 0;
                            
                            const trend = change >= 0 ? '↗' : '↘';
                            const trendClass = change >= 0 ? 'up' : 'down';
                            
                            return `${trend} ${this.formatCurrency(change)} (${this.formatPercentage(changePercent)})`;
                        }
                    }
                    return '';
                } : undefined
            }
        };
    }

    /**
     * Generate Chart.js scale configuration for German locale
     * 
     * @param {Object} options - Scale configuration options
     * @param {boolean} [options.currency=true] - Format as currency
     * @param {boolean} [options.compact=false] - Use compact notation
     * @returns {Object} Chart.js scale configuration
     */
    static getGermanScaleConfig(options = {}) {
        const { currency = true, compact = false } = options;
        
        return {
            ticks: {
                callback: (value) => {
                    return this.formatCurrency(value, { 
                        includeCurrency: currency,
                        compact: compact 
                    });
                }
            }
        };
    }

    /**
     * Debounce function to limit API calls
     * 
     * @param {Function} func - Function to debounce
     * @param {number} delay - Delay in milliseconds
     * @returns {Function} Debounced function
     */
    static debounce(func, delay) {
        let timeoutId;
        return function (...args) {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => func.apply(this, args), delay);
        };
    }

    /**
     * Throttle function to limit execution frequency
     * 
     * @param {Function} func - Function to throttle
     * @param {number} delay - Minimum delay between executions
     * @returns {Function} Throttled function
     */
    static throttle(func, delay) {
        let lastExecution = 0;
        return function (...args) {
            const now = Date.now();
            if (now - lastExecution >= delay) {
                func.apply(this, args);
                lastExecution = now;
            }
        };
    }

    /**
     * Create loading state overlay for chart containers
     * 
     * @param {HTMLElement} container - Chart container element
     * @param {boolean} show - Show or hide loading overlay
     * @param {string} [message='Loading...'] - Loading message
     */
    static toggleLoadingOverlay(container, show, message = 'Lädt...') {
        let overlay = container.querySelector('.chart-loading-overlay');
        
        if (show) {
            if (!overlay) {
                overlay = document.createElement('div');
                overlay.className = 'chart-loading-overlay position-absolute top-50 start-50 translate-middle';
                overlay.innerHTML = `
                    <div class="text-center">
                        <div class="spinner-border text-primary mb-2" role="status">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                        <div class="small text-muted">${message}</div>
                    </div>
                `;
                container.appendChild(overlay);
                container.style.position = 'relative';
            }
            overlay.classList.remove('d-none');
        } else if (overlay) {
            overlay.classList.add('d-none');
        }
    }

    /**
     * Validate and sanitize BWA position name
     * 
     * @param {string} positionName - Raw position name
     * @returns {string} Sanitized position name
     */
    static sanitizeBwaPositionName(positionName) {
        if (!positionName || typeof positionName !== 'string') {
            return 'Unbekannte Position';
        }
        
        // Remove potentially harmful characters while preserving German characters
        return positionName
            .trim()
            .replace(/[<>\"']/g, '') // Remove HTML/script injection characters
            .substring(0, 100); // Limit length
    }

    /**
     * Calculate summary statistics for BWA position data
     * 
     * @param {Array} data - Array of data points with {x, y} format
     * @returns {Object} Summary statistics
     */
    static calculateSummaryStats(data) {
        if (!Array.isArray(data) || data.length === 0) {
            return {
                count: 0,
                sum: 0,
                average: 0,
                min: 0,
                max: 0,
                trend: 'stable'
            };
        }

        const values = data.map(point => point.y).filter(v => v !== null && !isNaN(v));
        
        if (values.length === 0) {
            return { count: 0, sum: 0, average: 0, min: 0, max: 0, trend: 'stable' };
        }

        const sum = values.reduce((a, b) => a + b, 0);
        const average = sum / values.length;
        const min = Math.min(...values);
        const max = Math.max(...values);

        // Calculate trend (comparing first and last quarter)
        let trend = 'stable';
        if (values.length >= 4) {
            const firstQuarter = values.slice(0, Math.ceil(values.length / 4)).reduce((a, b) => a + b, 0);
            const lastQuarter = values.slice(-Math.ceil(values.length / 4)).reduce((a, b) => a + b, 0);
            const quarterAvgFirst = firstQuarter / Math.ceil(values.length / 4);
            const quarterAvgLast = lastQuarter / Math.ceil(values.length / 4);
            
            const change = (quarterAvgLast - quarterAvgFirst) / Math.abs(quarterAvgFirst);
            
            if (change > 0.05) trend = 'increasing';
            else if (change < -0.05) trend = 'decreasing';
        }

        return {
            count: values.length,
            sum,
            average,
            min,
            max,
            trend
        };
    }

    /**
     * Export chart data to CSV format with German locale
     * 
     * @param {Array} datasets - Chart.js datasets array
     * @param {Array} labels - Chart labels
     * @param {string} filename - CSV filename
     */
    static exportToCSV(datasets, labels, filename = 'bwa-data.csv') {
        if (!datasets || !Array.isArray(datasets) || datasets.length === 0) {
            console.warn('No data to export');
            return;
        }

        // Build CSV content
        let csv = 'Zeitraum;'; // German header
        
        // Add dataset headers
        datasets.forEach(dataset => {
            csv += `${dataset.label || 'Unbekannt'};`;
        });
        csv = csv.slice(0, -1) + '\n'; // Remove last semicolon and add newline
        
        // Add data rows
        if (labels && labels.length > 0) {
            labels.forEach((label, index) => {
                csv += `${this.formatBwaDate(label)};`;
                
                datasets.forEach(dataset => {
                    const dataPoint = dataset.data[index];
                    const value = dataPoint ? (dataPoint.y !== undefined ? dataPoint.y : dataPoint) : 0;
                    csv += `${this.formatCurrency(value, { includeCurrency: false })};`;
                });
                
                csv = csv.slice(0, -1) + '\n'; // Remove last semicolon and add newline
            });
        }

        // Create and trigger download
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
    }

    /**
     * Performance monitoring for chart operations
     */
    static performance = {
        timers: new Map(),
        
        start(label) {
            this.timers.set(label, performance.now());
        },
        
        end(label, logToConsole = true) {
            const start = this.timers.get(label);
            if (start) {
                const duration = performance.now() - start;
                this.timers.delete(label);
                
                if (logToConsole && duration > 100) {
                    console.log(`Performance: ${label} took ${duration.toFixed(2)}ms`);
                }
                
                return duration;
            }
            return 0;
        }
    };
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ChartUtils;
}

// Global registration for direct script inclusion
if (typeof window !== 'undefined') {
    window.ChartUtils = ChartUtils;
}