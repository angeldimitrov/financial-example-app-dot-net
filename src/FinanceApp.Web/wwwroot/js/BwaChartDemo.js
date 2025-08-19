/**
 * BWA Line Chart Demo and Integration Examples
 * 
 * Demonstrates how to integrate and use the BwaLineChartManager
 * with various configuration options and real-world scenarios.
 * 
 * Usage Examples:
 * - Basic chart initialization
 * - Advanced filtering and interactions
 * - Multiple chart instances
 * - API integration patterns
 * - Performance optimization techniques
 * 
 * This file serves as both documentation and functional demo code
 * for implementing BWA position development charts in the application.
 */

/**
 * Demo configuration for BWA Line Charts
 */
const BwaChartDemo = {
    
    /**
     * Initialize basic BWA line chart
     * Simple setup for displaying BWA position trends
     * 
     * @param {string} containerId - DOM container element ID
     */
    initBasicChart(containerId = 'bwa-chart-container') {
        const chartManager = new BwaLineChartManager({
            containerId: containerId,
            canvasId: 'bwa-chart-canvas',
            apiEndpoint: '/api/bwa-positions',
            enableZoom: true,
            enableUrlState: true
        });

        // Return instance for further customization
        return chartManager;
    },

    /**
     * Initialize advanced BWA chart with custom configuration
     * Demonstrates advanced features and customization options
     * 
     * @param {string} containerId - DOM container element ID
     * @param {Object} customConfig - Custom configuration options
     */
    initAdvancedChart(containerId, customConfig = {}) {
        const defaultConfig = {
            containerId: containerId,
            canvasId: `${containerId}-canvas`,
            apiEndpoint: '/api/bwa-positions',
            debounceMs: 200, // Faster response for advanced users
            enableZoom: true,
            enableUrlState: true,
            maxPositions: 25, // Reduced for better performance
            // Custom styling options
            colorScheme: 'professional', // professional, colorful, minimal
            showTrendIndicators: true,
            enableDataExport: true,
            // Performance optimizations
            lazyLoadData: true,
            cacheResults: true,
            updateThrottleMs: 16 // 60fps for smooth animations
        };

        const config = { ...defaultConfig, ...customConfig };
        const chartManager = new BwaLineChartManager(config);

        // Add custom event handlers for advanced features
        this.addAdvancedEventHandlers(chartManager);

        return chartManager;
    },

    /**
     * Create multiple coordinated BWA charts
     * Useful for dashboard layouts with different perspectives
     * 
     * @param {Array} chartConfigs - Array of chart configuration objects
     */
    initMultipleCharts(chartConfigs) {
        const chartInstances = new Map();

        chartConfigs.forEach((config, index) => {
            const chartId = config.id || `bwa-chart-${index}`;
            const chartManager = new BwaLineChartManager({
                containerId: config.containerId,
                canvasId: `${config.containerId}-canvas`,
                apiEndpoint: config.apiEndpoint || '/api/bwa-positions',
                ...config.options
            });

            chartInstances.set(chartId, chartManager);

            // Add cross-chart synchronization if enabled
            if (config.syncWithOthers) {
                this.addChartSynchronization(chartManager, chartInstances);
            }
        });

        return chartInstances;
    },

    /**
     * Add advanced event handlers for enhanced functionality
     * 
     * @param {BwaLineChartManager} chartManager - Chart manager instance
     */
    addAdvancedEventHandlers(chartManager) {
        // Custom keyboard shortcuts
        document.addEventListener('keydown', (event) => {
            if (event.target.closest(`#${chartManager.config.containerId}`)) {
                switch (event.key) {
                    case 'r':
                        if (event.ctrlKey || event.metaKey) {
                            event.preventDefault();
                            chartManager.refreshChartData();
                        }
                        break;
                    case 'e':
                        if (event.ctrlKey || event.metaKey) {
                            event.preventDefault();
                            chartManager.exportChart();
                        }
                        break;
                    case 'z':
                        if (event.ctrlKey || event.metaKey) {
                            event.preventDefault();
                            if (chartManager.chart && chartManager.chart.resetZoom) {
                                chartManager.chart.resetZoom();
                            }
                        }
                        break;
                }
            }
        });

        // Add fullscreen toggle
        const container = document.getElementById(chartManager.config.containerId);
        if (container) {
            const fullscreenBtn = document.createElement('button');
            fullscreenBtn.className = 'btn btn-outline-secondary btn-sm position-absolute';
            fullscreenBtn.style.top = '10px';
            fullscreenBtn.style.right = '10px';
            fullscreenBtn.style.zIndex = '1000';
            fullscreenBtn.innerHTML = '<i class="bi bi-fullscreen"></i>';
            fullscreenBtn.title = 'Toggle Fullscreen';
            
            fullscreenBtn.addEventListener('click', () => {
                this.toggleFullscreen(container);
            });
            
            container.appendChild(fullscreenBtn);
        }
    },

    /**
     * Add synchronization between multiple chart instances
     * 
     * @param {BwaLineChartManager} newChart - New chart to sync
     * @param {Map} existingCharts - Existing chart instances
     */
    addChartSynchronization(newChart, existingCharts) {
        // Sync date range changes
        const syncDateRange = (sourceChart) => {
            existingCharts.forEach((targetChart) => {
                if (targetChart !== sourceChart) {
                    targetChart.dateRange = { ...sourceChart.dateRange };
                    targetChart.refreshChartData();
                }
            });
        };

        // Override date change handler to include sync
        const originalHandler = newChart.handleDateRangeChange.bind(newChart);
        newChart.handleDateRangeChange = function(event) {
            originalHandler(event);
            syncDateRange(this);
        };
    },

    /**
     * Toggle fullscreen mode for chart container
     * 
     * @param {HTMLElement} container - Chart container element
     */
    toggleFullscreen(container) {
        if (!document.fullscreenElement) {
            container.requestFullscreen().then(() => {
                container.classList.add('chart-fullscreen');
                // Resize chart after fullscreen transition
                setTimeout(() => {
                    const chartCanvas = container.querySelector('canvas');
                    if (chartCanvas && chartCanvas.chart) {
                        chartCanvas.chart.resize();
                    }
                }, 100);
            }).catch(err => {
                console.error('Failed to enter fullscreen:', err);
            });
        } else {
            document.exitFullscreen();
            container.classList.remove('chart-fullscreen');
        }
    },

    /**
     * Create mock API for demonstration purposes
     * In production, replace with real API endpoints
     */
    createMockAPI() {
        // Mock BWA positions data
        const mockPositions = [
            { name: 'Umsatzerlöse', displayName: 'Umsatzerlöse', description: 'Gesamte Umsatzerlöse', category: 'revenue' },
            { name: 'Personalkosten', displayName: 'Personalkosten', description: 'Löhne und Gehälter', category: 'expense' },
            { name: 'Raumkosten', displayName: 'Raumkosten', description: 'Miete und Nebenkosten', category: 'expense' },
            { name: 'Abschreibungen', displayName: 'Abschreibungen', description: 'Abschreibungen auf Anlagevermögen', category: 'expense' },
            { name: 'Zinsen und ähnliche Aufwendungen', displayName: 'Zinsen', description: 'Zinsaufwendungen', category: 'expense' },
            { name: 'Steuern vom Einkommen und Ertrag', displayName: 'Steuern', description: 'Ertragssteuern', category: 'tax' }
        ];

        // Generate mock time-series data
        const generateMockData = (positionName, months = 12) => {
            const data = [];
            const baseValue = Math.random() * 50000 + 10000; // Random base value
            const trend = (Math.random() - 0.5) * 0.1; // Random trend
            const volatility = Math.random() * 0.3 + 0.1; // Random volatility

            for (let i = 0; i < months; i++) {
                const date = new Date();
                date.setMonth(date.getMonth() - (months - 1 - i));
                const dateString = date.toISOString().substring(0, 7); // YYYY-MM format

                const trendValue = baseValue * (1 + trend * i / months);
                const randomVariation = (Math.random() - 0.5) * volatility * baseValue;
                const amount = Math.round(trendValue + randomVariation);

                data.push({
                    date: dateString,
                    amount: amount,
                    metadata: {
                        position: positionName,
                        period: dateString
                    }
                });
            }

            return data;
        };

        // Mock API endpoints
        window.mockBwaAPI = {
            '/api/bwa-positions/list': () => Promise.resolve(mockPositions),
            '/api/bwa-positions/data': (params) => {
                const position = params.get('position') || mockPositions[0].name;
                const data = generateMockData(position);
                return Promise.resolve(data);
            }
        };

        // Override fetch for demo purposes
        const originalFetch = window.fetch;
        window.fetch = function(url, options) {
            const urlObj = new URL(url, window.location.origin);
            const apiPath = urlObj.pathname;
            
            if (window.mockBwaAPI[apiPath]) {
                return window.mockBwaAPI[apiPath](urlObj.searchParams);
            }
            
            return originalFetch.call(this, url, options);
        };
    },

    /**
     * Demo scenarios for different use cases
     */
    scenarios: {
        
        /**
         * Scenario: Revenue Analysis Dashboard
         * Focus on revenue-related BWA positions
         */
        revenueAnalysis(containerId) {
            const chartManager = new BwaLineChartManager({
                containerId: containerId,
                canvasId: `${containerId}-canvas`,
                apiEndpoint: '/api/bwa-positions',
                maxPositions: 10,
                defaultPositions: [
                    'Umsatzerlöse',
                    'Erlöse aus Vermietung',
                    'Sonstige betriebliche Erträge'
                ]
            });

            // Customize for revenue analysis
            chartManager.chart.options.plugins.title.text = 'Revenue Position Analysis';
            chartManager.chart.options.scales.y.title.text = 'Revenue (EUR)';
            
            return chartManager;
        },

        /**
         * Scenario: Cost Control Dashboard
         * Focus on expense management and cost trends
         */
        costControl(containerId) {
            const chartManager = new BwaLineChartManager({
                containerId: containerId,
                canvasId: `${containerId}-canvas`,
                apiEndpoint: '/api/bwa-positions',
                maxPositions: 15,
                defaultPositions: [
                    'Personalkosten',
                    'Raumkosten',
                    'Versicherungen',
                    'Kfz-Kosten',
                    'Werbekosten'
                ]
            });

            // Customize for cost analysis
            chartManager.chart.options.plugins.title.text = 'Cost Control Analysis';
            chartManager.chart.options.scales.y.title.text = 'Costs (EUR)';

            // Add cost threshold line
            chartManager.chart.options.plugins.annotation = {
                annotations: {
                    costThreshold: {
                        type: 'line',
                        yMin: 30000,
                        yMax: 30000,
                        borderColor: 'red',
                        borderWidth: 2,
                        borderDash: [6, 6],
                        label: {
                            content: 'Cost Threshold',
                            enabled: true,
                            position: 'end'
                        }
                    }
                }
            };
            
            return chartManager;
        },

        /**
         * Scenario: Executive Summary
         * High-level overview with key performance indicators
         */
        executiveSummary(containerId) {
            const chartManager = new BwaLineChartManager({
                containerId: containerId,
                canvasId: `${containerId}-canvas`,
                apiEndpoint: '/api/bwa-positions',
                maxPositions: 5,
                defaultPositions: [
                    'Umsatzerlöse',
                    'Personalkosten',
                    'Gesamtkosten',
                    'Betriebsergebnis'
                ]
            });

            // Customize for executive view
            chartManager.chart.options.plugins.title.text = 'Executive Summary - Key Financial Metrics';
            chartManager.chart.options.elements.line.borderWidth = 3; // Thicker lines for clarity
            
            return chartManager;
        }
    },

    /**
     * Integration helper for ASP.NET Core Razor Pages
     * Provides server-side integration patterns
     */
    razorPageIntegration: {
        
        /**
         * Generate script tag for Razor page integration
         * 
         * @param {string} containerId - Chart container ID
         * @param {Object} serverData - Server-provided configuration
         * @returns {string} Script tag HTML
         */
        generateScriptTag(containerId, serverData = {}) {
            return `
                <script>
                document.addEventListener('DOMContentLoaded', function() {
                    // Initialize BWA chart with server data
                    const chartManager = BwaChartDemo.initBasicChart('${containerId}');
                    
                    // Apply server-side configuration
                    ${serverData.selectedPositions ? 
                        `chartManager.activePositions = new Set(${JSON.stringify(serverData.selectedPositions)});` : ''
                    }
                    
                    ${serverData.dateRange ? 
                        `chartManager.dateRange = ${JSON.stringify(serverData.dateRange)};` : ''
                    }
                    
                    // Load initial data
                    chartManager.refreshChartData();
                });
                </script>
            `;
        },

        /**
         * Generate Razor page HTML template
         * 
         * @param {string} containerId - Chart container ID
         * @returns {string} HTML template
         */
        generateRazorTemplate(containerId = 'bwa-chart-container') {
            return `
                <div class="card">
                    <div class="card-header">
                        <h5 class="mb-0">
                            <i class="bi bi-graph-up"></i> BWA Position Development
                        </h5>
                    </div>
                    <div class="card-body">
                        <div id="${containerId}" style="height: 500px;">
                            <!-- Chart will be initialized here -->
                        </div>
                    </div>
                </div>

                @section Scripts {
                    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
                    <script src="https://cdn.jsdelivr.net/npm/chartjs-plugin-zoom"></script>
                    <script src="~/js/ChartUtils.js"></script>
                    <script src="~/js/BwaLineChartManager.js"></script>
                    <script>
                        document.addEventListener('DOMContentLoaded', function() {
                            const chartManager = BwaChartDemo.initBasicChart('${containerId}');
                        });
                    </script>
                }
            `;
        }
    },

    /**
     * Performance testing utilities
     */
    performance: {
        
        /**
         * Test chart rendering performance with various data sizes
         * 
         * @param {string} containerId - Test container ID
         */
        testRenderingPerformance(containerId) {
            const testSizes = [12, 24, 36, 60]; // months of data
            const testPositions = [5, 10, 25, 50]; // number of positions
            
            console.log('Starting BWA Chart Performance Tests...');
            
            testSizes.forEach(months => {
                testPositions.forEach(positions => {
                    const startTime = performance.now();
                    
                    // Create test chart
                    const chartManager = new BwaLineChartManager({
                        containerId: containerId,
                        canvasId: `test-canvas-${months}-${positions}`,
                        maxPositions: positions
                    });
                    
                    const endTime = performance.now();
                    const duration = endTime - startTime;
                    
                    console.log(`${months} months, ${positions} positions: ${duration.toFixed(2)}ms`);
                    
                    // Clean up
                    chartManager.destroy();
                });
            });
        },

        /**
         * Test memory usage and cleanup
         * 
         * @param {number} iterations - Number of create/destroy cycles
         */
        testMemoryUsage(iterations = 10) {
            console.log(`Testing memory usage over ${iterations} iterations...`);
            
            const initialMemory = performance.memory ? performance.memory.usedJSHeapSize : 0;
            
            for (let i = 0; i < iterations; i++) {
                const chartManager = new BwaLineChartManager({
                    containerId: 'memory-test-container',
                    canvasId: `memory-test-canvas-${i}`
                });
                
                // Simulate usage
                setTimeout(() => {
                    chartManager.destroy();
                    
                    if (i === iterations - 1) {
                        const finalMemory = performance.memory ? performance.memory.usedJSHeapSize : 0;
                        const memoryDiff = finalMemory - initialMemory;
                        console.log(`Memory difference: ${(memoryDiff / 1024 / 1024).toFixed(2)} MB`);
                    }
                }, 100);
            }
        }
    },

    /**
     * Utility functions for demo and testing
     */
    utils: {
        
        /**
         * Create demo container HTML
         * 
         * @param {string} containerId - Container ID
         * @param {string} title - Demo title
         * @returns {string} HTML string
         */
        createDemoContainer(containerId, title = 'BWA Line Chart Demo') {
            return `
                <div class="demo-section mb-5">
                    <h3>${title}</h3>
                    <div class="card">
                        <div class="card-body">
                            <div id="${containerId}" style="height: 400px;"></div>
                        </div>
                    </div>
                </div>
            `;
        },

        /**
         * Add demo container to page
         * 
         * @param {string} containerId - Container ID
         * @param {string} title - Demo title
         * @param {HTMLElement} parentElement - Parent element to append to
         */
        addDemoContainerToPage(containerId, title, parentElement = document.body) {
            const html = this.createDemoContainer(containerId, title);
            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = html;
            parentElement.appendChild(tempDiv.firstElementChild);
        }
    }
};

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = BwaChartDemo;
}

// Global registration for direct script inclusion
if (typeof window !== 'undefined') {
    window.BwaChartDemo = BwaChartDemo;
}

// Auto-initialize demo if demo containers exist
document.addEventListener('DOMContentLoaded', function() {
    // Look for demo containers and initialize automatically
    const demoContainers = document.querySelectorAll('[data-bwa-chart-demo]');
    
    demoContainers.forEach(container => {
        const scenario = container.dataset.bwaChartDemo || 'basic';
        const containerId = container.id;
        
        if (BwaChartDemo.scenarios[scenario]) {
            BwaChartDemo.scenarios[scenario](containerId);
        } else {
            BwaChartDemo.initBasicChart(containerId);
        }
    });
});

/**
 * CSS styles for demo and fullscreen mode
 * Add this to your stylesheet or include as a <style> tag
 */
const demoStyles = `
    .chart-fullscreen {
        background: white;
        padding: 20px;
        z-index: 9999;
    }
    
    .chart-fullscreen canvas {
        max-height: calc(100vh - 100px) !important;
    }
    
    .demo-section {
        margin-bottom: 2rem;
        border: 1px solid #dee2e6;
        border-radius: 0.375rem;
        padding: 1rem;
    }
    
    .bwa-chart-controls .form-label {
        font-weight: 600;
        font-size: 0.875rem;
    }
    
    .bwa-chart-info {
        background-color: #f8f9fa;
        border-radius: 0.375rem;
        padding: 0.75rem;
        margin-top: 0.5rem;
    }
    
    .chart-loading-overlay {
        background: rgba(255, 255, 255, 0.9);
        border-radius: 0.375rem;
        padding: 1rem;
        box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
    }
    
    @media (max-width: 768px) {
        .bwa-chart-controls .row {
            --bs-gutter-x: 0.5rem;
        }
        
        .bwa-chart-controls .col-md-3,
        .bwa-chart-controls .col-md-4,
        .bwa-chart-controls .col-md-2 {
            margin-bottom: 0.5rem;
        }
    }
`;

// Inject demo styles if not already present
if (typeof document !== 'undefined' && !document.querySelector('#bwa-chart-demo-styles')) {
    const styleSheet = document.createElement('style');
    styleSheet.id = 'bwa-chart-demo-styles';
    styleSheet.textContent = demoStyles;
    document.head.appendChild(styleSheet);
}