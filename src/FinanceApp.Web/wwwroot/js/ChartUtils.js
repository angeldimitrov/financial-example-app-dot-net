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
     * Supports both light and dark themes with appropriate contrast ratios
     */
    static colorPalette = {
        // Light theme colors
        light: {
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
            ],
            
            // Chart background and text colors
            chartBackground: '#ffffff',
            gridColor: 'rgba(0, 0, 0, 0.1)',
            textColor: '#333333',
            axisColor: '#666666'
        },
        
        // Dark theme colors - enhanced brightness for dark backgrounds
        dark: {
            // Primary colors for main categories (brighter for dark mode)
            revenue: '#4ade80',      // Bright green for revenue
            expense: '#f87171',      // Bright red for expenses
            profit: '#60a5fa',       // Bright blue for profit
            loss: '#facc15',         // Bright yellow for loss
            
            // BWA-specific position colors (enhanced for dark mode)
            personnel: '#a78bfa',    // Bright purple for personnel costs
            facilities: '#fb923c',   // Bright orange for facility costs
            depreciation: '#34d399', // Bright teal for depreciation
            taxes: '#f472b6',       // Bright pink for taxes
            interest: '#94a3b8',     // Light gray for interest
            
            // Extended palette for multiple positions (dark mode optimized)
            extended: [
                '#60a5fa', '#fb923c', '#4ade80', '#f87171', '#a78bfa',
                '#f59e0b', '#ec4899', '#94a3b8', '#eab308', '#06b6d4',
                '#93c5fd', '#fdba74', '#86efac', '#fca5a5', '#c4b5fd',
                '#fbbf24', '#f9a8d4', '#d1d5db', '#fde047', '#67e8f9'
            ],
            
            // Chart background and text colors
            chartBackground: '#1a1a1a',
            gridColor: 'rgba(255, 255, 255, 0.1)',
            textColor: '#e8e8e8',
            axisColor: '#b3b3b3'
        }
    };
    
    /**
     * Legacy color palette for backward compatibility
     * Uses light theme colors by default
     */
    static get colorPaletteCompat() {
        return {
            revenue: this.colorPalette.light.revenue,
            expense: this.colorPalette.light.expense,
            profit: this.colorPalette.light.profit,
            loss: this.colorPalette.light.loss,
            personnel: this.colorPalette.light.personnel,
            facilities: this.colorPalette.light.facilities,
            depreciation: this.colorPalette.light.depreciation,
            taxes: this.colorPalette.light.taxes,
            interest: this.colorPalette.light.interest,
            extended: this.colorPalette.light.extended
        };
    }

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
     * Get current theme from ThemeService or DOM
     * 
     * @returns {string} 'light' or 'dark'
     */
    static getCurrentTheme() {
        // Try to get theme from ThemeService
        if (window.themeService && window.themeService.getEffectiveTheme) {
            return window.themeService.getEffectiveTheme();
        }
        
        // Fallback to DOM attribute
        const htmlElement = document.documentElement;
        const dataTheme = htmlElement.getAttribute('data-theme');
        
        if (dataTheme === 'dark') return 'dark';
        if (dataTheme === 'light') return 'light';
        
        // Check system preference as last resort
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            return 'dark';
        }
        
        return 'light'; // Default fallback
    }
    
    /**
     * Get theme-appropriate color palette
     * 
     * @param {string} [theme] - Override theme ('light' or 'dark')
     * @returns {Object} Theme-appropriate color palette
     */
    static getThemePalette(theme = null) {
        const currentTheme = theme || this.getCurrentTheme();
        return this.colorPalette[currentTheme] || this.colorPalette.light;
    }

    /**
     * Get color for BWA position based on category with theme support
     * 
     * @param {string} positionName - BWA position name
     * @param {number} [index=0] - Fallback color index
     * @param {string} [theme=null] - Override theme ('light' or 'dark')
     * @returns {string} Hex color code
     */
    static getBwaPositionColor(positionName, index = 0, theme = null) {
        const palette = this.getThemePalette(theme);
        
        if (!positionName) {
            return palette.extended[index % palette.extended.length];
        }

        const name = positionName.toLowerCase();
        
        // Specific color mapping for common BWA positions
        const colorMap = {
            'umsatz': palette.revenue,
            'erlös': palette.revenue,
            'personal': palette.personnel,
            'lohn': palette.personnel,
            'gehalt': palette.personnel,
            'raum': palette.facilities,
            'miete': palette.facilities,
            'abschreibung': palette.depreciation,
            'steuer': palette.taxes,
            'zins': palette.interest,
            'gewinn': palette.profit,
            'verlust': palette.loss
        };

        // Find matching color
        for (const [keyword, color] of Object.entries(colorMap)) {
            if (name.includes(keyword)) {
                return color;
            }
        }

        // Fallback to extended palette
        return palette.extended[index % palette.extended.length];
    }
    
    /**
     * Get Chart.js configuration for current theme with premium animations
     * 
     * @param {Object} options - Configuration options
     * @param {string} [options.theme=null] - Override theme
     * @param {boolean} [options.responsive=true] - Responsive chart
     * @param {boolean} [options.maintainAspectRatio=false] - Maintain aspect ratio
     * @param {boolean} [options.premiumAnimations=true] - Enable premium animations
     * @returns {Object} Chart.js configuration object
     */
    static getThemeChartConfig(options = {}) {
        const {
            theme = null,
            responsive = true,
            maintainAspectRatio = false,
            premiumAnimations = true
        } = options;
        
        const palette = this.getThemePalette(theme);
        
        return {
            responsive,
            maintainAspectRatio,
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            },
            animation: premiumAnimations ? {
                duration: 750,
                easing: 'easeInOutQuart',
                delay: (context) => {
                    // Stagger animation for multiple datasets
                    const delay = context.dataIndex * 30 + context.datasetIndex * 100;
                    return delay;
                },
                onComplete: (animation) => {
                    // Trigger custom event when animation completes
                    if (animation.chart && animation.chart.canvas) {
                        const event = new CustomEvent('chart-animation-complete', {
                            detail: { chart: animation.chart }
                        });
                        animation.chart.canvas.dispatchEvent(event);
                    }
                }
            } : {
                duration: 400,
                easing: 'easeOutQuart'
            },
            transitions: {
                active: {
                    animation: {
                        duration: 200
                    }
                },
                resize: {
                    animation: {
                        duration: 400
                    }
                },
                show: {
                    animations: {
                        x: { from: 0 },
                        y: { from: 0 }
                    }
                },
                hide: {
                    animations: {
                        x: { to: 0 },
                        y: { to: 0 }
                    }
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    align: 'center',
                    labels: {
                        color: palette.textColor,
                        font: {
                            family: 'Inter, sans-serif',
                            size: 12,
                            weight: 500
                        },
                        usePointStyle: true,
                        pointStyle: 'circle',
                        padding: 20,
                        generateLabels: (chart) => {
                            const datasets = chart.data.datasets;
                            return datasets.map((dataset, i) => ({
                                text: dataset.label,
                                fillStyle: dataset.backgroundColor,
                                strokeStyle: dataset.borderColor,
                                lineWidth: 2,
                                hidden: !chart.isDatasetVisible(i),
                                index: i,
                                pointStyle: dataset.pointStyle || 'circle'
                            }));
                        }
                    },
                    onHover: (event, legendItem, legend) => {
                        if (legendItem) {
                            legend.chart.canvas.style.cursor = 'pointer';
                            // Highlight corresponding dataset
                            const ci = legend.chart;
                            ci.data.datasets.forEach((dataset, i) => {
                                dataset.borderWidth = i === legendItem.index ? 4 : 2;
                                dataset.pointRadius = i === legendItem.index ? 5 : 4;
                            });
                            ci.update('none');
                        }
                    },
                    onLeave: (event, legendItem, legend) => {
                        legend.chart.canvas.style.cursor = 'default';
                        // Reset dataset styles
                        const ci = legend.chart;
                        ci.data.datasets.forEach((dataset) => {
                            dataset.borderWidth = 2;
                            dataset.pointRadius = 4;
                        });
                        ci.update('none');
                    }
                },
                tooltip: this.getPremiumTooltipConfig({ theme, premiumAnimations })
            },
            scales: {
                x: {
                    ticks: {
                        color: palette.axisColor,
                        font: {
                            family: 'Inter, sans-serif',
                            size: 11,
                            weight: 400
                        },
                        maxRotation: 45,
                        minRotation: 0
                    },
                    grid: {
                        color: palette.gridColor,
                        drawBorder: false,
                        lineWidth: 0.5
                    }
                },
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: palette.axisColor,
                        font: {
                            family: 'JetBrains Mono, monospace',
                            size: 11,
                            weight: 400
                        },
                        callback: (value) => this.formatCurrency(value, { compact: true }),
                        padding: 8
                    },
                    grid: {
                        color: palette.gridColor,
                        drawBorder: false,
                        lineWidth: 0.5
                    }
                }
            },
            elements: {
                point: {
                    radius: 4,
                    hoverRadius: 7,
                    borderWidth: 2,
                    hitRadius: 10,
                    hoverBorderWidth: 3
                },
                line: {
                    borderWidth: 2,
                    tension: 0.15,
                    borderJoinStyle: 'round'
                },
                bar: {
                    borderRadius: 4,
                    borderSkipped: false
                }
            },
            onHover: (event, activeElements, chart) => {
                chart.canvas.style.cursor = activeElements.length > 0 ? 'pointer' : 'default';
            }
        };
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
     * Generate premium Chart.js tooltip configuration with animations
     * 
     * @param {Object} options - Tooltip configuration options
     * @param {boolean} [options.showTrend=true] - Show trend information
     * @param {boolean} [options.showPercentage=false] - Show percentage values
     * @param {string} [options.theme=null] - Override theme
     * @param {boolean} [options.premiumAnimations=true] - Enable premium animations
     * @returns {Object} Chart.js tooltip configuration
     */
    static getPremiumTooltipConfig(options = {}) {
        const { 
            showTrend = true, 
            showPercentage = false, 
            theme = null,
            premiumAnimations = true
        } = options;
        const palette = this.getThemePalette(theme);
        
        return {
            enabled: true,
            backgroundColor: theme === 'dark' ? 
                'rgba(26, 26, 26, 0.95)' : 
                'rgba(255, 255, 255, 0.98)',
            titleColor: theme === 'dark' ? '#e8e8e8' : '#1e293b',
            titleFont: {
                family: 'Inter, sans-serif',
                size: 13,
                weight: 600
            },
            bodyColor: theme === 'dark' ? '#d1d1d1' : '#475569',
            bodyFont: {
                family: 'JetBrains Mono, monospace',
                size: 12,
                weight: 400
            },
            footerColor: theme === 'dark' ? '#b3b3b3' : '#64748b',
            footerFont: {
                family: 'Inter, sans-serif',
                size: 11,
                weight: 500
            },
            borderColor: theme === 'dark' ? 
                'rgba(96, 165, 250, 0.3)' : 
                'rgba(26, 54, 93, 0.2)',
            borderWidth: 1,
            cornerRadius: 8,
            padding: 12,
            boxShadow: '0 8px 32px rgba(0, 0, 0, 0.15)',
            displayColors: true,
            usePointStyle: true,
            caretSize: 6,
            caretPadding: 8,
            animation: premiumAnimations ? {
                duration: 250,
                easing: 'easeOutCubic'
            } : {
                duration: 100,
                easing: 'linear'
            },
            callbacks: {
                title: (context) => {
                    if (context.length === 0) return '';
                    
                    // Format date title with icon
                    const label = context[0].label;
                    if (label && (label.includes('-') || label.includes('/'))) {
                        return `📅 ${this.formatBwaDate(label)}`;
                    }
                    return label;
                },
                label: (context) => {
                    const value = context.parsed.y;
                    const datasetLabel = context.dataset.label;
                    
                    let formattedValue = this.formatCurrency(value);
                    
                    // Add icon based on value
                    const icon = value >= 0 ? '📈' : '📉';
                    
                    if (showPercentage && context.dataset._percentageValues) {
                        const percentage = context.dataset._percentageValues[context.dataIndex];
                        if (percentage !== undefined) {
                            formattedValue += ` (${this.formatPercentage(percentage)})`;
                        }
                    }
                    
                    return `${icon} ${datasetLabel}: ${formattedValue}`;
                },
                footer: showTrend ? (tooltipItems) => {
                    // Show trend information in footer with enhanced formatting
                    const current = tooltipItems[0];
                    if (current.dataIndex > 0) {
                        const dataset = current.dataset;
                        const currentValue = current.parsed.y;
                        const previousValue = dataset.data[current.dataIndex - 1]?.y;
                        
                        if (previousValue !== undefined && previousValue !== null && currentValue !== null) {
                            const change = currentValue - previousValue;
                            const changePercent = previousValue !== 0 ? 
                                (change / Math.abs(previousValue)) : 0;
                            
                            const trend = change >= 0 ? '↗️' : '↘️';
                            const trendText = change >= 0 ? 'Anstieg' : 'Rückgang';
                            
                            return [
                                '',
                                `${trend} ${trendText}: ${this.formatCurrency(Math.abs(change))}`,
                                `Änderung: ${this.formatPercentage(changePercent)}`
                            ];
                        }
                    }
                    return '';
                } : undefined,
                beforeBody: (tooltipItems) => {
                    if (premiumAnimations && tooltipItems.length > 0) {
                        return '─'.repeat(25);
                    }
                    return '';
                },
                afterBody: (tooltipItems) => {
                    if (premiumAnimations && tooltipItems.length > 1) {
                        // Calculate total for multiple datasets
                        const total = tooltipItems.reduce((sum, item) => sum + item.parsed.y, 0);
                        return [
                            '─'.repeat(25),
                            `Gesamt: ${this.formatCurrency(total)}`
                        ];
                    }
                    return '';
                }
            }
        };
    }
    
    /**
     * Generate Chart.js tooltip configuration for German locale with theme support
     * Legacy method for backward compatibility
     */
    static getGermanTooltipConfig(options = {}) {
        return this.getPremiumTooltipConfig(options);
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
     * Update existing Chart.js instance with new theme
     * 
     * @param {Chart} chartInstance - Chart.js instance
     * @param {string} theme - Theme to apply ('light' or 'dark')
     */
    static updateChartTheme(chartInstance, theme = null) {
        if (!chartInstance || !chartInstance.config) {
            console.warn('ChartUtils.updateChartTheme: Invalid chart instance');
            return;
        }
        
        const palette = this.getThemePalette(theme);
        const config = chartInstance.config;
        
        // Update legend colors
        if (config.options.plugins && config.options.plugins.legend) {
            config.options.plugins.legend.labels.color = palette.textColor;
        }
        
        // Update tooltip colors
        if (config.options.plugins && config.options.plugins.tooltip) {
            const tooltip = config.options.plugins.tooltip;
            tooltip.backgroundColor = theme === 'dark' ? 'rgba(26, 26, 26, 0.95)' : 'rgba(33, 37, 41, 0.95)';
            tooltip.borderColor = theme === 'dark' ? 'rgba(64, 64, 64, 0.8)' : '#495057';
        }
        
        // Update scale colors
        if (config.options.scales) {
            Object.keys(config.options.scales).forEach(scaleKey => {
                const scale = config.options.scales[scaleKey];
                if (scale.ticks) {
                    scale.ticks.color = palette.axisColor;
                }
                if (scale.grid) {
                    scale.grid.color = palette.gridColor;
                }
            });
        }
        
        // Update dataset colors to match theme
        config.data.datasets.forEach((dataset, datasetIndex) => {
            if (dataset._originalColors) {
                // Restore from saved original colors and update for theme
                const originalBorderColor = dataset._originalColors.borderColor;
                const originalBackgroundColor = dataset._originalColors.backgroundColor;
                
                if (Array.isArray(originalBorderColor)) {
                    dataset.borderColor = originalBorderColor.map((color, index) => 
                        this.getBwaPositionColor(dataset._positionNames?.[index] || '', index, theme)
                    );
                } else {
                    dataset.borderColor = this.getBwaPositionColor(dataset._positionName || '', datasetIndex, theme);
                }
                
                if (Array.isArray(originalBackgroundColor)) {
                    dataset.backgroundColor = originalBackgroundColor.map((color, index) => {
                        const newColor = this.getBwaPositionColor(dataset._positionNames?.[index] || '', index, theme);
                        return this.hexToRgba(newColor, 0.2);
                    });
                } else {
                    const newColor = this.getBwaPositionColor(dataset._positionName || '', datasetIndex, theme);
                    dataset.backgroundColor = this.hexToRgba(newColor, 0.2);
                }
            }
        });
        
        // Trigger chart update
        chartInstance.update('none'); // Use 'none' for immediate update without animation
        
        console.log(`[ChartUtils] Updated chart theme to: ${theme || this.getCurrentTheme()}`);
    }
    
    /**
     * Register theme change listener for automatic chart updates
     * 
     * @param {Chart} chartInstance - Chart.js instance to keep updated
     */
    static registerThemeListener(chartInstance) {
        if (!chartInstance || !document) {
            return;
        }
        
        const updateHandler = (event) => {
            const newTheme = event.detail?.effectiveTheme || event.detail?.theme;
            if (newTheme) {
                this.updateChartTheme(chartInstance, newTheme);
            }
        };
        
        // Listen for theme change events
        document.addEventListener('theme-changed', updateHandler);
        
        // Store reference for cleanup
        if (!chartInstance._themeListener) {
            chartInstance._themeListener = updateHandler;
        }
        
        console.log('[ChartUtils] Registered theme change listener for chart');
    }
    
    /**
     * Unregister theme change listener
     * 
     * @param {Chart} chartInstance - Chart.js instance
     */
    static unregisterThemeListener(chartInstance) {
        if (chartInstance && chartInstance._themeListener && document) {
            document.removeEventListener('theme-changed', chartInstance._themeListener);
            delete chartInstance._themeListener;
            console.log('[ChartUtils] Unregistered theme change listener for chart');
        }
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