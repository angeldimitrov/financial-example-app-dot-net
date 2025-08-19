/**
 * BWA Position Development Line Chart Manager
 * 
 * Advanced Chart.js component for visualizing BWA position trends over time.
 * Supports multiple line datasets, dynamic filtering, German formatting, and responsive design.
 * 
 * Features:
 * - Multiple BWA position line datasets with dynamic color assignment
 * - Interactive legend with show/hide functionality
 * - Time-series data with zoom and pan controls
 * - German number formatting (1.234,56 €)
 * - Date range filtering with debounced API calls
 * - URL state management for bookmarkable charts
 * - Responsive design with mobile support
 * - Performance optimized for 50+ positions
 * 
 * Business Context:
 * BWA positions represent standardized German accounting categories used in
 * business evaluation reports (Betriebswirtschaftliche Auswertung).
 * Each position tracks revenue or expense trends over monthly periods.
 * 
 * Performance Targets:
 * - Chart rendering: <500ms for 12 months of data
 * - Smooth filtering interactions with 100ms debounce
 * - Memory-efficient dataset management with cleanup
 */
class BwaLineChartManager {
    /**
     * Initialize BWA Line Chart Manager
     * 
     * @param {Object} config - Chart configuration
     * @param {string} config.containerId - DOM element ID for chart container
     * @param {string} config.canvasId - Canvas element ID for Chart.js
     * @param {string} [config.apiEndpoint='/api/bwa-positions'] - API endpoint for data
     * @param {number} [config.debounceMs=300] - API call debounce delay
     * @param {boolean} [config.enableZoom=true] - Enable zoom/pan functionality
     * @param {boolean} [config.enableUrlState=true] - Enable URL state management
     */
    constructor(config) {
        this.config = {
            apiEndpoint: '/api/bwa-positions',
            debounceMs: 300,
            enableZoom: true,
            enableUrlState: true,
            maxPositions: 50, // Performance limit
            ...config
        };

        // Core chart components
        this.chart = null;
        this.datasets = new Map(); // position name -> dataset config
        this.colorPalette = [];
        this.nextColorIndex = 0;
        
        // State management
        this.activePositions = new Set();
        this.dateRange = { start: null, end: null };
        this.isLoading = false;
        this.loadedData = new Map(); // position name -> time series data
        
        // Performance optimization
        this.debounceTimer = null;
        this.renderTimer = null;
        this.lastApiCall = 0;
        
        // DOM elements
        this.container = null;
        this.canvas = null;
        this.loadingIndicator = null;
        this.positionSelector = null;
        this.dateFilters = null;
        
        // Initialize color palette for consistent BWA position coloring
        this.initializeColorPalette();
        
        // Bind methods to preserve context
        this.handlePositionToggle = this.handlePositionToggle.bind(this);
        this.handleDateRangeChange = this.handleDateRangeChange.bind(this);
        this.handleResize = this.handleResize.bind(this);
        
        this.initialize();
    }

    /**
     * Initialize the chart manager and setup DOM elements
     */
    async initialize() {
        try {
            this.setupDOM();
            this.setupChart();
            this.attachEventListeners();
            
            // Load initial state from URL if enabled
            if (this.config.enableUrlState) {
                this.loadStateFromUrl();
            }
            
            // Load initial data
            await this.loadInitialData();
            
            console.log('BwaLineChartManager initialized successfully');
        } catch (error) {
            console.error('Failed to initialize BwaLineChartManager:', error);
            this.showError('Failed to initialize chart. Please refresh the page.');
        }
    }

    /**
     * Setup DOM elements and chart container structure
     */
    setupDOM() {
        this.container = document.getElementById(this.config.containerId);
        if (!this.container) {
            throw new Error(`Container element #${this.config.containerId} not found`);
        }

        // Create chart structure if it doesn't exist
        const existingCanvas = document.getElementById(this.config.canvasId);
        if (!existingCanvas) {
            this.container.innerHTML = `
                <div class="bwa-chart-controls mb-3">
                    <div class="row g-3">
                        <div class="col-md-3">
                            <label for="bwa-date-start" class="form-label">Start Date</label>
                            <input type="month" id="bwa-date-start" class="form-control form-control-sm" />
                        </div>
                        <div class="col-md-3">
                            <label for="bwa-date-end" class="form-label">End Date</label>
                            <input type="month" id="bwa-date-end" class="form-control form-control-sm" />
                        </div>
                        <div class="col-md-4">
                            <label for="bwa-position-selector" class="form-label">BWA Positions</label>
                            <select id="bwa-position-selector" class="form-select form-select-sm" multiple>
                                <option value="" disabled>Loading positions...</option>
                            </select>
                        </div>
                        <div class="col-md-2">
                            <label class="form-label">&nbsp;</label>
                            <div>
                                <button type="button" id="bwa-reset-zoom" class="btn btn-outline-secondary btn-sm me-1">
                                    <i class="bi bi-zoom-out"></i>
                                </button>
                                <button type="button" id="bwa-export" class="btn btn-outline-primary btn-sm">
                                    <i class="bi bi-download"></i>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
                
                <div class="position-relative">
                    <div id="bwa-loading" class="position-absolute top-50 start-50 translate-middle d-none">
                        <div class="spinner-border text-primary" role="status">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                    </div>
                    <canvas id="${this.config.canvasId}" style="height: 400px;"></canvas>
                </div>
                
                <div class="bwa-chart-info mt-2">
                    <small class="text-muted">
                        <i class="bi bi-info-circle"></i>
                        Tip: Click legend items to show/hide positions. Use mouse wheel to zoom, drag to pan.
                        Up to ${this.config.maxPositions} positions can be displayed simultaneously.
                    </small>
                </div>
            `;
        }

        // Cache DOM references
        this.canvas = document.getElementById(this.config.canvasId);
        this.loadingIndicator = document.getElementById('bwa-loading');
        this.positionSelector = document.getElementById('bwa-position-selector');
        this.dateFilters = {
            start: document.getElementById('bwa-date-start'),
            end: document.getElementById('bwa-date-end')
        };

        if (!this.canvas) {
            throw new Error(`Canvas element #${this.config.canvasId} not found`);
        }
    }

    /**
     * Initialize Chart.js instance with BWA-specific configuration
     */
    setupChart() {
        const ctx = this.canvas.getContext('2d');
        
        // Chart.js configuration optimized for BWA time-series data
        const chartConfig = {
            type: 'line',
            data: {
                labels: [], // Will be populated with time series dates
                datasets: [] // Will be populated dynamically
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false,
                    axis: 'x'
                },
                animation: {
                    duration: 300, // Fast animations for better UX
                    easing: 'easeOutQuart'
                },
                plugins: {
                    title: {
                        display: true,
                        text: 'BWA Position Development Over Time',
                        font: { size: 16, weight: 'bold' },
                        padding: 20
                    },
                    legend: {
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 12 },
                            // Custom legend click handler for smooth show/hide
                            onClick: (event, legendItem, legend) => {
                                this.handleLegendClick(legendItem, legend);
                            }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(33, 37, 41, 0.95)',
                        titleColor: '#fff',
                        bodyColor: '#fff',
                        borderColor: '#495057',
                        borderWidth: 1,
                        cornerRadius: 6,
                        displayColors: true,
                        callbacks: {
                            // German-formatted tooltip labels
                            title: (context) => {
                                if (context.length === 0) return '';
                                const date = new Date(context[0].label + '-01');
                                return date.toLocaleDateString('de-DE', { 
                                    year: 'numeric', 
                                    month: 'long' 
                                });
                            },
                            label: (context) => {
                                const value = context.parsed.y;
                                const formattedValue = this.formatCurrency(value);
                                return `${context.dataset.label}: ${formattedValue}`;
                            },
                            footer: (tooltipItems) => {
                                // Show trend information
                                const current = tooltipItems[0];
                                if (current.dataIndex > 0) {
                                    const previous = current.dataset.data[current.dataIndex - 1];
                                    if (previous && current.parsed.y !== null) {
                                        const change = current.parsed.y - previous.y;
                                        const changePercent = previous.y !== 0 ? 
                                            ((change / Math.abs(previous.y)) * 100).toFixed(1) : '∞';
                                        const trend = change >= 0 ? '↗' : '↘';
                                        return `${trend} ${this.formatCurrency(change)} (${changePercent}%)`;
                                    }
                                }
                                return '';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        type: 'time',
                        time: {
                            unit: 'month',
                            displayFormats: {
                                month: 'MMM yyyy'
                            },
                            tooltipFormat: 'MMM yyyy'
                        },
                        title: {
                            display: true,
                            text: 'Time Period',
                            font: { size: 12, weight: 'bold' }
                        },
                        grid: {
                            color: 'rgba(0,0,0,0.1)'
                        }
                    },
                    y: {
                        title: {
                            display: true,
                            text: 'Amount (EUR)',
                            font: { size: 12, weight: 'bold' }
                        },
                        ticks: {
                            // German number formatting for Y-axis
                            callback: (value) => this.formatCurrency(value, false)
                        },
                        grid: {
                            color: 'rgba(0,0,0,0.1)'
                        }
                    }
                },
                elements: {
                    line: {
                        tension: 0.2, // Slight curve for better visual appeal
                        borderWidth: 2
                    },
                    point: {
                        radius: 4,
                        hoverRadius: 6,
                        borderWidth: 2,
                        backgroundColor: '#fff'
                    }
                }
            }
        };

        // Add zoom plugin if enabled
        if (this.config.enableZoom && window.Chart.Zoom) {
            chartConfig.options.plugins.zoom = {
                zoom: {
                    wheel: { enabled: true, speed: 0.1 },
                    pinch: { enabled: true },
                    drag: { enabled: false }, // Disable drag zoom, use pan instead
                    mode: 'x' // Only zoom on X axis (time)
                },
                pan: {
                    enabled: true,
                    mode: 'x',
                    speed: 20,
                    threshold: 10
                }
            };
        }

        this.chart = new Chart(ctx, chartConfig);
    }

    /**
     * Attach event listeners for user interactions
     */
    attachEventListeners() {
        // Position selector change (multiple selection)
        if (this.positionSelector) {
            this.positionSelector.addEventListener('change', this.handlePositionToggle);
        }

        // Date range filters with debounced API calls
        if (this.dateFilters.start) {
            this.dateFilters.start.addEventListener('change', this.handleDateRangeChange);
        }
        if (this.dateFilters.end) {
            this.dateFilters.end.addEventListener('change', this.handleDateRangeChange);
        }

        // Zoom reset button
        const resetZoomBtn = document.getElementById('bwa-reset-zoom');
        if (resetZoomBtn) {
            resetZoomBtn.addEventListener('click', () => {
                if (this.chart && this.chart.resetZoom) {
                    this.chart.resetZoom();
                }
            });
        }

        // Export button
        const exportBtn = document.getElementById('bwa-export');
        if (exportBtn) {
            exportBtn.addEventListener('click', () => this.exportChart());
        }

        // Responsive resize handling
        window.addEventListener('resize', this.handleResize);

        // URL state change handling (for back/forward navigation)
        if (this.config.enableUrlState) {
            window.addEventListener('popstate', () => this.loadStateFromUrl());
        }
    }

    /**
     * Initialize color palette for consistent BWA position visualization
     */
    initializeColorPalette() {
        // Professional color palette optimized for financial data
        // Uses distinguishable colors that work well on white backgrounds
        this.colorPalette = [
            '#1f77b4', // Blue (primary revenue/income positions)
            '#ff7f0e', // Orange (operational expenses) 
            '#2ca02c', // Green (profits/positive trends)
            '#d62728', // Red (losses/negative trends)
            '#9467bd', // Purple (tax-related positions)
            '#8c564b', // Brown (personnel costs)
            '#e377c2', // Pink (administrative costs)
            '#7f7f7f', // Gray (miscellaneous)
            '#bcbd22', // Olive (depreciation)
            '#17becf', // Cyan (financial income)
            '#aec7e8', // Light blue (revenue subcategories)
            '#ffbb78', // Light orange (expense subcategories)
            '#98df8a', // Light green (other income)
            '#ff9896', // Light red (extraordinary expenses)
            '#c5b0d5', // Light purple (tax subcategories)
            '#c49c94', // Light brown (personnel subcategories)
            '#f7b6d3', // Light pink (admin subcategories)
            '#c7c7c7', // Light gray (other)
            '#dbdb8d', // Light olive (asset costs)
            '#9edae5'  // Light cyan (financial subcategories)
        ];
    }

    /**
     * Get next color from palette for new BWA position
     * 
     * @param {string} positionName - BWA position name for color assignment
     * @returns {string} Hex color code
     */
    getNextColor(positionName) {
        // Assign specific colors for common BWA positions
        const specialColors = {
            'Umsatzerlöse': '#2ca02c',           // Green for revenue
            'Personalkosten': '#8c564b',         // Brown for personnel
            'Raumkosten': '#ff7f0e',            // Orange for facilities
            'Abschreibungen': '#bcbd22',        // Olive for depreciation
            'Steuern': '#9467bd',               // Purple for taxes
            'Zinsen': '#17becf',                // Cyan for interest
            'Sonstige Kosten': '#7f7f7f'       // Gray for miscellaneous
        };

        // Check for special color assignment
        for (const [key, color] of Object.entries(specialColors)) {
            if (positionName.toLowerCase().includes(key.toLowerCase())) {
                return color;
            }
        }

        // Use next color from palette
        const color = this.colorPalette[this.nextColorIndex % this.colorPalette.length];
        this.nextColorIndex++;
        return color;
    }

    /**
     * Handle BWA position selection/deselection
     * 
     * @param {Event} event - Change event from position selector
     */
    async handlePositionToggle(event) {
        const selectedOptions = Array.from(event.target.selectedOptions);
        const newActivePositions = new Set(selectedOptions.map(opt => opt.value));

        // Enforce maximum positions limit for performance
        if (newActivePositions.size > this.config.maxPositions) {
            this.showWarning(`Maximum ${this.config.maxPositions} positions allowed. Please deselect some positions.`);
            // Revert selection
            event.target.value = Array.from(this.activePositions);
            return;
        }

        // Determine positions to add and remove
        const toAdd = [...newActivePositions].filter(pos => !this.activePositions.has(pos));
        const toRemove = [...this.activePositions].filter(pos => !newActivePositions.has(pos));

        // Update active positions
        this.activePositions = newActivePositions;

        // Update chart datasets
        await this.updateChartDatasets(toAdd, toRemove);

        // Update URL state
        if (this.config.enableUrlState) {
            this.updateUrlState();
        }
    }

    /**
     * Handle date range filter changes with debounced API calls
     * 
     * @param {Event} event - Change event from date input
     */
    handleDateRangeChange(event) {
        const startDate = this.dateFilters.start.value;
        const endDate = this.dateFilters.end.value;

        // Basic validation
        if (startDate && endDate && startDate > endDate) {
            this.showWarning('Start date cannot be after end date.');
            return;
        }

        // Update date range state
        this.dateRange = {
            start: startDate || null,
            end: endDate || null
        };

        // Debounce API call to avoid excessive requests
        clearTimeout(this.debounceTimer);
        this.debounceTimer = setTimeout(async () => {
            await this.refreshChartData();
            
            if (this.config.enableUrlState) {
                this.updateUrlState();
            }
        }, this.config.debounceMs);
    }

    /**
     * Handle responsive resize events
     */
    handleResize() {
        // Debounce resize handling to avoid excessive redraws
        clearTimeout(this.renderTimer);
        this.renderTimer = setTimeout(() => {
            if (this.chart) {
                this.chart.resize();
            }
        }, 100);
    }

    /**
     * Handle custom legend click for smooth show/hide animation
     * 
     * @param {Object} legendItem - Chart.js legend item
     * @param {Object} legend - Chart.js legend instance
     */
    handleLegendClick(legendItem, legend) {
        const datasetIndex = legendItem.datasetIndex;
        const dataset = this.chart.data.datasets[datasetIndex];
        
        // Toggle dataset visibility
        dataset.hidden = !dataset.hidden;
        
        // Animate the change
        this.chart.update('active');
        
        // Update position selector to reflect visibility state
        const positionName = dataset.label;
        const option = Array.from(this.positionSelector.options)
            .find(opt => opt.value === positionName);
        
        if (option) {
            option.selected = !dataset.hidden;
        }
    }

    /**
     * Load initial BWA positions and set up the selector
     */
    async loadInitialData() {
        try {
            this.showLoading(true);
            
            // Fetch available BWA positions
            const positions = await this.fetchBwaPositions();
            this.populatePositionSelector(positions);
            
            // Load default positions if none selected
            if (this.activePositions.size === 0) {
                this.loadDefaultPositions(positions);
            }
            
            // Load data for active positions
            if (this.activePositions.size > 0) {
                await this.refreshChartData();
            }
            
        } catch (error) {
            console.error('Failed to load initial data:', error);
            this.showError('Failed to load BWA positions. Please refresh the page.');
        } finally {
            this.showLoading(false);
        }
    }

    /**
     * Fetch available BWA positions from API
     * 
     * @returns {Promise<Array>} Array of BWA position objects
     */
    async fetchBwaPositions() {
        const response = await fetch(`${this.config.apiEndpoint}/list`);
        if (!response.ok) {
            throw new Error(`Failed to fetch BWA positions: ${response.status}`);
        }
        return await response.json();
    }

    /**
     * Populate the position selector with available BWA positions
     * 
     * @param {Array} positions - Array of BWA position objects
     */
    populatePositionSelector(positions) {
        if (!this.positionSelector) return;
        
        // Clear loading state
        this.positionSelector.innerHTML = '';
        
        // Group positions by type for better organization
        const groupedPositions = this.groupPositionsByType(positions);
        
        Object.entries(groupedPositions).forEach(([type, typePositions]) => {
            if (typePositions.length === 0) return;
            
            const optgroup = document.createElement('optgroup');
            optgroup.label = type;
            
            typePositions.forEach(position => {
                const option = document.createElement('option');
                option.value = position.name;
                option.textContent = position.displayName || position.name;
                option.title = position.description || position.name;
                optgroup.appendChild(option);
            });
            
            this.positionSelector.appendChild(optgroup);
        });
    }

    /**
     * Group BWA positions by type for organized display
     * 
     * @param {Array} positions - Array of position objects
     * @returns {Object} Grouped positions by type
     */
    groupPositionsByType(positions) {
        const groups = {
            'Revenue (Umsätze)': [],
            'Expenses (Kosten)': [],
            'Personnel (Personal)': [],
            'Taxes (Steuern)': [],
            'Other (Sonstige)': []
        };
        
        positions.forEach(position => {
            const name = position.name.toLowerCase();
            
            if (name.includes('umsatz') || name.includes('erlös')) {
                groups['Revenue (Umsätze)'].push(position);
            } else if (name.includes('personal') || name.includes('lohn') || name.includes('gehalt')) {
                groups['Personnel (Personal)'].push(position);
            } else if (name.includes('steuer')) {
                groups['Taxes (Steuern)'].push(position);
            } else if (name.includes('kosten') || name.includes('aufwand')) {
                groups['Expenses (Kosten)'].push(position);
            } else {
                groups['Other (Sonstige)'].push(position);
            }
        });
        
        return groups;
    }

    /**
     * Load default BWA positions for initial display
     * 
     * @param {Array} positions - Available positions
     */
    loadDefaultPositions(positions) {
        // Select most common/important BWA positions by default
        const defaultPositions = [
            'Umsatzerlöse',
            'Personalkosten',
            'Raumkosten'
        ];
        
        const availableDefaults = positions
            .filter(pos => defaultPositions.some(def => 
                pos.name.toLowerCase().includes(def.toLowerCase())
            ))
            .slice(0, 3); // Limit to 3 for initial load performance
        
        this.activePositions = new Set(availableDefaults.map(pos => pos.name));
        
        // Update selector to reflect default selection
        if (this.positionSelector) {
            Array.from(this.positionSelector.options).forEach(option => {
                option.selected = this.activePositions.has(option.value);
            });
        }
    }

    /**
     * Update chart datasets by adding/removing BWA positions
     * 
     * @param {Array} toAdd - Position names to add
     * @param {Array} toRemove - Position names to remove
     */
    async updateChartDatasets(toAdd, toRemove) {
        try {
            // Remove datasets for deselected positions
            toRemove.forEach(positionName => {
                const datasetIndex = this.chart.data.datasets.findIndex(
                    dataset => dataset.label === positionName
                );
                
                if (datasetIndex !== -1) {
                    this.chart.data.datasets.splice(datasetIndex, 1);
                    this.datasets.delete(positionName);
                }
            });

            // Add datasets for newly selected positions
            if (toAdd.length > 0) {
                this.showLoading(true);
                
                for (const positionName of toAdd) {
                    const data = await this.fetchPositionData(positionName);
                    const dataset = this.createDataset(positionName, data);
                    
                    this.chart.data.datasets.push(dataset);
                    this.datasets.set(positionName, dataset);
                }
            }

            // Update chart
            this.chart.update('active');
            
        } catch (error) {
            console.error('Failed to update chart datasets:', error);
            this.showError('Failed to load position data.');
        } finally {
            this.showLoading(false);
        }
    }

    /**
     * Refresh all chart data with current filters
     */
    async refreshChartData() {
        if (this.activePositions.size === 0) {
            // Clear chart if no positions selected
            this.chart.data.datasets = [];
            this.chart.update();
            return;
        }

        try {
            this.showLoading(true);
            
            // Fetch fresh data for all active positions
            const dataPromises = Array.from(this.activePositions).map(async positionName => {
                const data = await this.fetchPositionData(positionName);
                return { positionName, data };
            });
            
            const results = await Promise.all(dataPromises);
            
            // Update all datasets
            this.chart.data.datasets = [];
            this.datasets.clear();
            
            results.forEach(({ positionName, data }) => {
                if (data && data.length > 0) {
                    const dataset = this.createDataset(positionName, data);
                    this.chart.data.datasets.push(dataset);
                    this.datasets.set(positionName, dataset);
                }
            });
            
            // Update time axis labels
            this.updateTimeLabels();
            
            // Animate chart update
            this.chart.update('active');
            
        } catch (error) {
            console.error('Failed to refresh chart data:', error);
            this.showError('Failed to refresh chart data.');
        } finally {
            this.showLoading(false);
        }
    }

    /**
     * Fetch time-series data for a specific BWA position
     * 
     * @param {string} positionName - BWA position name
     * @returns {Promise<Array>} Time-series data points
     */
    async fetchPositionData(positionName) {
        // Implement caching to improve performance
        const cacheKey = `${positionName}_${this.dateRange.start}_${this.dateRange.end}`;
        
        if (this.loadedData.has(cacheKey)) {
            const cached = this.loadedData.get(cacheKey);
            // Check if cache is still fresh (5 minutes)
            if (Date.now() - cached.timestamp < 300000) {
                return cached.data;
            }
        }

        // Build API URL with date range filters
        const url = new URL(`${this.config.apiEndpoint}/data`, window.location.origin);
        url.searchParams.set('position', positionName);
        
        if (this.dateRange.start) {
            url.searchParams.set('start', this.dateRange.start);
        }
        if (this.dateRange.end) {
            url.searchParams.set('end', this.dateRange.end);
        }

        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(`Failed to fetch data for ${positionName}: ${response.status}`);
        }

        const data = await response.json();
        
        // Cache the result
        this.loadedData.set(cacheKey, {
            data,
            timestamp: Date.now()
        });

        // Clean up old cache entries to prevent memory leaks
        if (this.loadedData.size > 100) {
            const oldestKey = this.loadedData.keys().next().value;
            this.loadedData.delete(oldestKey);
        }

        return data;
    }

    /**
     * Create Chart.js dataset configuration for BWA position
     * 
     * @param {string} positionName - BWA position name
     * @param {Array} data - Time-series data points
     * @returns {Object} Chart.js dataset configuration
     */
    createDataset(positionName, data) {
        const color = this.getNextColor(positionName);
        
        // Transform data to Chart.js format
        const chartData = data.map(point => ({
            x: point.date, // ISO date string (YYYY-MM)
            y: point.amount
        }));

        return {
            label: positionName,
            data: chartData,
            borderColor: color,
            backgroundColor: this.hexToRgba(color, 0.1),
            borderWidth: 2,
            fill: false,
            tension: 0.2,
            pointBackgroundColor: '#fff',
            pointBorderColor: color,
            pointBorderWidth: 2,
            pointRadius: 4,
            pointHoverRadius: 6,
            pointHoverBackgroundColor: color,
            pointHoverBorderColor: '#fff',
            pointHoverBorderWidth: 2,
            // Performance optimization: skip null values
            spanGaps: false,
            // Custom properties for legend/tooltip
            originalColor: color,
            positionType: this.categorizePosition(positionName)
        };
    }

    /**
     * Update time axis labels based on available data
     */
    updateTimeLabels() {
        // Collect all unique time points from datasets
        const timePoints = new Set();
        
        this.chart.data.datasets.forEach(dataset => {
            dataset.data.forEach(point => {
                if (point.x) {
                    timePoints.add(point.x);
                }
            });
        });

        // Sort time points chronologically
        const sortedTimes = Array.from(timePoints).sort();
        
        // Update chart labels
        this.chart.data.labels = sortedTimes;
    }

    /**
     * Categorize BWA position by type for styling/grouping
     * 
     * @param {string} positionName - BWA position name
     * @returns {string} Position category
     */
    categorizePosition(positionName) {
        const name = positionName.toLowerCase();
        
        if (name.includes('umsatz') || name.includes('erlös')) {
            return 'revenue';
        } else if (name.includes('personal') || name.includes('lohn') || name.includes('gehalt')) {
            return 'personnel';
        } else if (name.includes('steuer')) {
            return 'tax';
        } else if (name.includes('kosten') || name.includes('aufwand')) {
            return 'expense';
        }
        
        return 'other';
    }

    /**
     * Convert hex color to RGBA with alpha
     * 
     * @param {string} hex - Hex color code
     * @param {number} alpha - Alpha value (0-1)
     * @returns {string} RGBA color string
     */
    hexToRgba(hex, alpha) {
        const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
        if (!result) return hex;
        
        const r = parseInt(result[1], 16);
        const g = parseInt(result[2], 16);
        const b = parseInt(result[3], 16);
        
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    /**
     * Format currency values with German locale
     * 
     * @param {number} value - Numeric value
     * @param {boolean} [includeCurrency=true] - Include € symbol
     * @returns {string} Formatted currency string
     */
    formatCurrency(value, includeCurrency = true) {
        if (value === null || value === undefined || isNaN(value)) {
            return includeCurrency ? '€0,00' : '0,00';
        }

        const options = {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        };

        if (includeCurrency) {
            options.style = 'currency';
            options.currency = 'EUR';
        }

        return value.toLocaleString('de-DE', options);
    }

    /**
     * Export chart as PNG image
     */
    exportChart() {
        if (!this.chart) return;

        try {
            // Get chart as base64 image
            const chartImage = this.chart.toBase64Image('image/png', 1.0);
            
            // Create download link
            const link = document.createElement('a');
            link.download = `bwa-positions-${new Date().toISOString().split('T')[0]}.png`;
            link.href = chartImage;
            link.click();
            
        } catch (error) {
            console.error('Failed to export chart:', error);
            this.showError('Failed to export chart.');
        }
    }

    /**
     * Load chart state from URL parameters
     */
    loadStateFromUrl() {
        const params = new URLSearchParams(window.location.search);
        
        // Load selected positions
        const positions = params.get('positions');
        if (positions) {
            this.activePositions = new Set(positions.split(',').filter(p => p.length > 0));
            
            // Update position selector
            if (this.positionSelector) {
                Array.from(this.positionSelector.options).forEach(option => {
                    option.selected = this.activePositions.has(option.value);
                });
            }
        }

        // Load date range
        const startDate = params.get('start');
        const endDate = params.get('end');
        
        if (startDate && this.dateFilters.start) {
            this.dateFilters.start.value = startDate;
            this.dateRange.start = startDate;
        }
        
        if (endDate && this.dateFilters.end) {
            this.dateFilters.end.value = endDate;
            this.dateRange.end = endDate;
        }
    }

    /**
     * Update URL with current chart state for bookmarking
     */
    updateUrlState() {
        if (!this.config.enableUrlState) return;

        const params = new URLSearchParams();
        
        // Add selected positions
        if (this.activePositions.size > 0) {
            params.set('positions', Array.from(this.activePositions).join(','));
        }
        
        // Add date range
        if (this.dateRange.start) {
            params.set('start', this.dateRange.start);
        }
        if (this.dateRange.end) {
            params.set('end', this.dateRange.end);
        }

        // Update URL without page reload
        const newUrl = `${window.location.pathname}${params.toString() ? '?' + params.toString() : ''}`;
        window.history.replaceState(null, '', newUrl);
    }

    /**
     * Show/hide loading indicator
     * 
     * @param {boolean} show - Show or hide loading indicator
     */
    showLoading(show) {
        this.isLoading = show;
        
        if (this.loadingIndicator) {
            if (show) {
                this.loadingIndicator.classList.remove('d-none');
            } else {
                this.loadingIndicator.classList.add('d-none');
            }
        }
        
        // Disable interactions during loading
        if (this.positionSelector) {
            this.positionSelector.disabled = show;
        }
        
        Object.values(this.dateFilters).forEach(filter => {
            if (filter) filter.disabled = show;
        });
    }

    /**
     * Show error message to user
     * 
     * @param {string} message - Error message
     */
    showError(message) {
        // Create or update error alert
        let alert = document.querySelector('.bwa-chart-error');
        if (!alert) {
            alert = document.createElement('div');
            alert.className = 'alert alert-danger alert-dismissible bwa-chart-error';
            alert.innerHTML = `
                <i class="bi bi-exclamation-triangle"></i>
                <span class="error-message"></span>
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;
            this.container.insertBefore(alert, this.container.firstChild);
        }
        
        alert.querySelector('.error-message').textContent = message;
        alert.classList.remove('d-none');
        
        // Auto-hide after 5 seconds
        setTimeout(() => {
            if (alert && alert.parentNode) {
                alert.classList.add('d-none');
            }
        }, 5000);
    }

    /**
     * Show warning message to user
     * 
     * @param {string} message - Warning message
     */
    showWarning(message) {
        // Create or update warning alert
        let alert = document.querySelector('.bwa-chart-warning');
        if (!alert) {
            alert = document.createElement('div');
            alert.className = 'alert alert-warning alert-dismissible bwa-chart-warning';
            alert.innerHTML = `
                <i class="bi bi-exclamation-triangle"></i>
                <span class="warning-message"></span>
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;
            this.container.insertBefore(alert, this.container.firstChild);
        }
        
        alert.querySelector('.warning-message').textContent = message;
        alert.classList.remove('d-none');
        
        // Auto-hide after 3 seconds
        setTimeout(() => {
            if (alert && alert.parentNode) {
                alert.classList.add('d-none');
            }
        }, 3000);
    }

    /**
     * Clean up resources and event listeners
     */
    destroy() {
        // Clear timers
        clearTimeout(this.debounceTimer);
        clearTimeout(this.renderTimer);
        
        // Remove event listeners
        window.removeEventListener('resize', this.handleResize);
        window.removeEventListener('popstate', this.loadStateFromUrl);
        
        if (this.positionSelector) {
            this.positionSelector.removeEventListener('change', this.handlePositionToggle);
        }
        
        Object.values(this.dateFilters).forEach(filter => {
            if (filter) filter.removeEventListener('change', this.handleDateRangeChange);
        });
        
        // Destroy Chart.js instance
        if (this.chart) {
            this.chart.destroy();
            this.chart = null;
        }
        
        // Clear cached data
        this.loadedData.clear();
        this.datasets.clear();
        this.activePositions.clear();
        
        console.log('BwaLineChartManager destroyed successfully');
    }
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = BwaLineChartManager;
}

// Global registration for direct script inclusion
if (typeof window !== 'undefined') {
    window.BwaLineChartManager = BwaLineChartManager;
}