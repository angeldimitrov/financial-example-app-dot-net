/**
 * Chart Memory Management System for German Finance Application
 * 
 * Prevents Chart.js memory leaks by properly managing chart lifecycle
 * Essential for financial dashboard with multiple chart instances and updates
 * 
 * Features:
 * - Automatic chart cleanup and disposal
 * - Memory leak detection and prevention
 * - Chart instance registry for tracking
 * - Performance monitoring and logging
 * 
 * German BWA Context:
 * Manages chart instances showing German financial trends (Personalkosten, Raumkosten, etc.)
 * Ensures smooth performance during BWA data visualization updates
 */

class ChartMemoryManager {
    constructor() {
        // Registry of all active chart instances
        this.chartRegistry = new Map();
        
        // Memory monitoring configuration
        this.memoryCheckInterval = 30000; // 30 seconds
        this.maxMemoryUsage = 100 * 1024 * 1024; // 100MB threshold
        
        // Performance tracking
        this.performanceMetrics = {
            chartsCreated: 0,
            chartsDestroyed: 0,
            memoryLeaksDetected: 0,
            lastMemoryCheck: Date.now()
        };
        
        // Start memory monitoring
        this.startMemoryMonitoring();
        
        console.log('ChartMemoryManager initialized - monitoring Chart.js instances');
    }

    /**
     * Creates optimized Chart.js configuration with German financial formatting
     * Used by dashboard pages to create consistent chart configurations
     * 
     * @param {string} chartType - Type of chart (line, bar, pie, etc.)
     * @param {object} baseConfig - Base chart configuration
     * @returns {object} Optimized chart configuration
     */
    createOptimizedConfig(chartType, baseConfig) {
        return {
            type: chartType,
            ...baseConfig,
            options: {
                responsive: true,
                maintainAspectRatio: false,
                // Performance optimizations
                animation: {
                    duration: 750
                },
                elements: {
                    point: {
                        radius: 3,
                        hoverRadius: 5
                    }
                },
                // Merge with any existing options
                ...baseConfig.options
            }
        };
    }

    /**
     * Creates and registers a new Chart.js instance with automatic cleanup
     * 
     * @param {string} chartId - Unique identifier for the chart
     * @param {HTMLCanvasElement} canvas - Canvas element for the chart
     * @param {object} config - Chart.js configuration object
     * @returns {Chart} The created Chart.js instance
     */
    createChart(chartId, canvas, config) {
        try {
            // Destroy existing chart if present
            this.destroyChart(chartId);
            
            // Create new chart instance
            const chart = new Chart(canvas, config);
            
            // Register chart with metadata
            this.chartRegistry.set(chartId, {
                chart: chart,
                canvas: canvas,
                createdAt: Date.now(),
                lastUpdated: Date.now(),
                updateCount: 0
            });
            
            this.performanceMetrics.chartsCreated++;
            
            console.log(`Chart created and registered: ${chartId}`);
            return chart;
            
        } catch (error) {
            console.error(`Error creating chart ${chartId}:`, error);
            throw error;
        }
    }

    /**
     * Safely destroys a chart and cleans up resources
     * 
     * @param {string} chartId - ID of the chart to destroy
     */
    destroyChart(chartId) {
        try {
            const chartData = this.chartRegistry.get(chartId);
            
            if (chartData && chartData.chart) {
                // Properly destroy Chart.js instance
                chartData.chart.destroy();
                
                // Clear canvas context
                if (chartData.canvas) {
                    const ctx = chartData.canvas.getContext('2d');
                    if (ctx) {
                        ctx.clearRect(0, 0, chartData.canvas.width, chartData.canvas.height);
                    }
                }
                
                // Remove from registry
                this.chartRegistry.delete(chartId);
                this.performanceMetrics.chartsDestroyed++;
                
                console.log(`Chart destroyed and cleaned up: ${chartId}`);
            }
            
        } catch (error) {
            console.error(`Error destroying chart ${chartId}:`, error);
            // Force removal from registry even if destroy failed
            this.chartRegistry.delete(chartId);
        }
    }

    /**
     * Registers a chart for memory management without creating it
     * Useful when charts are created externally but need memory management
     * 
     * @param {string} chartId - Unique identifier for the chart
     * @param {Chart} chartInstance - Existing Chart.js instance
     * @param {HTMLCanvasElement} canvas - Canvas element for the chart
     */
    registerChart(chartId, chartInstance, canvas) {
        try {
            // Destroy existing chart if present
            this.destroyChart(chartId);
            
            // Register chart with metadata
            this.chartRegistry.set(chartId, {
                chart: chartInstance,
                canvas: canvas,
                createdAt: Date.now(),
                lastUpdated: Date.now(),
                updateCount: 0
            });
            
            this.performanceMetrics.chartsCreated++;
            console.log(`Chart registered for memory management: ${chartId}`);
            
        } catch (error) {
            console.error(`Error registering chart ${chartId}:`, error);
        }
    }

    /**
     * Updates chart data using chart ID (works with registered charts)
     * 
     * @param {string} chartId - ID of the chart to update
     * @param {object} newData - New chart data
     */
    updateChart(chartId, newData) {
        return this.updateChartData(chartId, newData);
    }

    /**
     * Updates existing chart data efficiently
     * Prevents memory leaks during frequent data updates
     * 
     * @param {string} chartId - ID of the chart to update
     * @param {object} newData - New chart data
     */
    updateChartData(chartId, newData) {
        try {
            const chartData = this.chartRegistry.get(chartId);
            
            if (!chartData || !chartData.chart) {
                console.warn(`Chart not found for update: ${chartId}`);
                return false;
            }
            
            // Update chart data efficiently
            const chart = chartData.chart;
            chart.data = newData;
            chart.update('none'); // Use 'none' animation mode for better performance
            
            // Update metadata
            chartData.lastUpdated = Date.now();
            chartData.updateCount++;
            
            console.log(`Chart data updated: ${chartId} (update #${chartData.updateCount})`);
            return true;
            
        } catch (error) {
            console.error(`Error updating chart ${chartId}:`, error);
            return false;
        }
    }

    /**
     * Destroys all registered charts
     * Useful for page cleanup or navigation
     */
    destroyAllCharts() {
        const chartIds = Array.from(this.chartRegistry.keys());
        
        chartIds.forEach(chartId => {
            this.destroyChart(chartId);
        });
        
        console.log(`All charts destroyed: ${chartIds.length} instances cleaned up`);
    }

    /**
     * Gets performance statistics for monitoring
     * 
     * @returns {object} Performance metrics and chart registry info
     */
    getPerformanceStats() {
        return {
            ...this.performanceMetrics,
            activeCharts: this.chartRegistry.size,
            chartDetails: Array.from(this.chartRegistry.entries()).map(([id, data]) => ({
                id,
                createdAt: data.createdAt,
                lastUpdated: data.lastUpdated,
                updateCount: data.updateCount,
                age: Date.now() - data.createdAt
            }))
        };
    }

    /**
     * Starts automatic memory monitoring
     * Detects potential memory leaks and logs warnings
     */
    startMemoryMonitoring() {
        if (typeof window.performance === 'undefined' || typeof window.performance.memory === 'undefined') {
            console.log('Memory monitoring not available in this browser');
            return;
        }

        setInterval(() => {
            try {
                const memoryInfo = window.performance.memory;
                const usedMemory = memoryInfo.usedJSHeapSize;
                
                // Check for memory leaks
                if (usedMemory > this.maxMemoryUsage) {
                    this.performanceMetrics.memoryLeaksDetected++;
                    console.warn('Potential memory leak detected:', {
                        usedMemory: `${(usedMemory / 1024 / 1024).toFixed(2)} MB`,
                        activeCharts: this.chartRegistry.size,
                        threshold: `${(this.maxMemoryUsage / 1024 / 1024).toFixed(2)} MB`
                    });
                }
                
                // Log periodic memory status (only in development)
                if (window.location.hostname === 'localhost') {
                    console.log('Memory Status:', {
                        used: `${(usedMemory / 1024 / 1024).toFixed(2)} MB`,
                        total: `${(memoryInfo.totalJSHeapSize / 1024 / 1024).toFixed(2)} MB`,
                        activeCharts: this.chartRegistry.size
                    });
                }
                
                this.performanceMetrics.lastMemoryCheck = Date.now();
                
            } catch (error) {
                console.error('Memory monitoring error:', error);
            }
        }, this.memoryCheckInterval);
    }

    /**
     * Checks for orphaned charts and cleans them up
     * Useful for detecting charts that weren't properly destroyed
     */
    cleanupOrphanedCharts() {
        let cleanedUp = 0;
        
        this.chartRegistry.forEach((chartData, chartId) => {
            // Check if canvas is still in DOM
            if (chartData.canvas && !document.body.contains(chartData.canvas)) {
                console.warn(`Orphaned chart detected: ${chartId} - cleaning up`);
                this.destroyChart(chartId);
                cleanedUp++;
            }
        });
        
        if (cleanedUp > 0) {
            console.log(`Cleaned up ${cleanedUp} orphaned charts`);
        }
        
        return cleanedUp;
    }
}

// Global chart memory manager instance
window.chartMemoryManager = new ChartMemoryManager();

// Helper functions for easy chart management

/**
 * Creates a financial trend chart with German BWA data
 * Optimized for German accounting terminology and formatting
 * 
 * @param {string} chartId - Unique chart identifier
 * @param {string} canvasSelector - CSS selector for canvas element
 * @param {object} bwaData - German BWA financial data
 * @param {string} chartType - Chart type (line, bar, pie)
 */
function createFinanceChart(chartId, canvasSelector, bwaData, chartType = 'line') {
    try {
        const canvas = document.querySelector(canvasSelector);
        if (!canvas) {
            throw new Error(`Canvas not found: ${canvasSelector}`);
        }

        const config = {
            type: chartType,
            data: bwaData,
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top'
                    },
                    title: {
                        display: true,
                        text: 'BWA Finanzanalyse'
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function(value) {
                                // Format as German currency
                                return '€' + value.toLocaleString('de-DE');
                            }
                        }
                    }
                },
                // Optimize performance
                animation: {
                    duration: 750
                },
                elements: {
                    point: {
                        radius: 3,
                        hoverRadius: 5
                    }
                }
            }
        };

        return window.chartMemoryManager.createChart(chartId, canvas, config);
        
    } catch (error) {
        console.error(`Error creating finance chart ${chartId}:`, error);
        throw error;
    }
}

/**
 * Updates chart with new BWA data
 * 
 * @param {string} chartId - Chart to update
 * @param {object} newBwaData - New German financial data
 */
function updateFinanceChart(chartId, newBwaData) {
    return window.chartMemoryManager.updateChartData(chartId, newBwaData);
}

/**
 * Cleanup function for page navigation
 * Should be called when leaving pages with charts
 */
function cleanupChartsOnPageLeave() {
    window.chartMemoryManager.destroyAllCharts();
    window.chartMemoryManager.cleanupOrphanedCharts();
}

// Automatic cleanup on page unload
window.addEventListener('beforeunload', cleanupChartsOnPageLeave);

// Automatic cleanup on page visibility change (tab switching)
document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
        window.chartMemoryManager.cleanupOrphanedCharts();
    }
});

// Log initialization
console.log('Chart Memory Manager loaded - ready for German BWA financial charts');