/**
 * Theme Management Service for German Financial Application
 * 
 * Business Context:
 * - Manages theme preferences for German accounting professionals
 * - Provides smooth transitions between light/dark modes
 * - Integrates with Chart.js for financial data visualization theming
 * - Supports system preference detection and manual override
 * - German localization for accessibility and user experience
 * 
 * Features:
 * - localStorage persistence across sessions
 * - System theme detection (prefers-color-scheme)
 * - Event-driven architecture for chart updates
 * - Smooth transitions without layout shifts
 * - WCAG 2.1 AA compliance maintenance
 */

class ThemeService {
    /**
     * Initialize ThemeService with default configuration
     * 
     * Storage Strategy:
     * - localStorage for client-side persistence
     * - Cookie fallback for server-side rendering
     * - System preference detection as default
     */
    constructor() {
        this.STORAGE_KEY = 'bwa-theme-preference';
        this.COOKIE_NAME = 'theme_preference';
        this.THEME_ATTRIBUTE = 'data-theme';
        
        // Available themes: light, dark, auto (system)
        this.THEMES = {
            LIGHT: 'light',
            DARK: 'dark', 
            AUTO: 'auto'
        };
        
        // Theme cycle order for toggle button
        this.THEME_CYCLE = [this.THEMES.AUTO, this.THEMES.LIGHT, this.THEMES.DARK];
        
        this.currentTheme = null;
        this.systemPreference = null;
        this.mediaQuery = null;
        this.eventListeners = new Set();
        
        // German text for accessibility and UI
        this.germanText = {
            light: 'Heller Modus',
            dark: 'Dunkler Modus', 
            auto: 'System-Einstellung',
            toggle: 'Theme umschalten',
            activated: 'aktiviert'
        };
        
        // Bind methods to preserve context
        this.handleSystemThemeChange = this.handleSystemThemeChange.bind(this);
        this.toggle = this.toggle.bind(this);
    }

    /**
     * Initialize the theme service
     * 
     * Initialization Process:
     * 1. Detect system preference
     * 2. Load saved preference from storage
     * 3. Apply appropriate theme
     * 4. Set up event listeners
     * 5. Initialize UI components
     */
    async init() {
        try {
            console.log('[ThemeService] Initializing theme service...');
            
            // Set up system preference detection
            this.setupSystemPreferenceDetection();
            
            // Load and apply saved theme preference
            const savedTheme = this.getStoredTheme();
            await this.applyTheme(savedTheme || this.THEMES.AUTO);
            
            // Set up UI event listeners
            this.bindEventListeners();
            
            console.log(`[ThemeService] Theme service initialized with theme: ${this.currentTheme}`);
            
            // Emit ready event for other components (like charts)
            this.emitEvent('theme-service-ready', {
                theme: this.currentTheme,
                effectiveTheme: this.getEffectiveTheme()
            });
            
        } catch (error) {
            console.error('[ThemeService] Failed to initialize theme service:', error);
            // Fallback to light theme on initialization error
            await this.applyTheme(this.THEMES.LIGHT);
        }
    }

    /**
     * Set up system preference detection using media queries
     * 
     * Listens for changes to (prefers-color-scheme: dark) and updates
     * theme automatically when user is using 'auto' theme preference
     */
    setupSystemPreferenceDetection() {
        if (window.matchMedia) {
            this.mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
            this.systemPreference = this.mediaQuery.matches ? this.THEMES.DARK : this.THEMES.LIGHT;
            
            // Listen for system theme changes
            this.mediaQuery.addEventListener('change', this.handleSystemThemeChange);
            
            console.log(`[ThemeService] System preference detected: ${this.systemPreference}`);
        } else {
            console.warn('[ThemeService] matchMedia not supported, defaulting to light theme');
            this.systemPreference = this.THEMES.LIGHT;
        }
    }

    /**
     * Handle system theme preference changes
     * 
     * Updates theme automatically if user is using 'auto' preference
     */
    handleSystemThemeChange(event) {
        const newSystemPreference = event.matches ? this.THEMES.DARK : this.THEMES.LIGHT;
        console.log(`[ThemeService] System preference changed to: ${newSystemPreference}`);
        
        this.systemPreference = newSystemPreference;
        
        // Only update if current theme is 'auto'
        if (this.currentTheme === this.THEMES.AUTO) {
            this.applyEffectiveTheme();
        }
    }

    /**
     * Get theme preference from localStorage
     * 
     * @returns {string|null} Stored theme preference or null if not found
     */
    getStoredTheme() {
        try {
            // Try localStorage first
            if (typeof Storage !== 'undefined') {
                const stored = localStorage.getItem(this.STORAGE_KEY);
                if (stored && Object.values(this.THEMES).includes(stored)) {
                    return stored;
                }
            }
            
            // Fallback to cookie
            const cookieValue = this.getCookie(this.COOKIE_NAME);
            if (cookieValue && Object.values(this.THEMES).includes(cookieValue)) {
                return cookieValue;
            }
            
            return null;
        } catch (error) {
            console.warn('[ThemeService] Failed to read stored theme:', error);
            return null;
        }
    }

    /**
     * Store theme preference in localStorage and cookie
     * 
     * @param {string} theme - Theme to store
     */
    storeTheme(theme) {
        try {
            // Store in localStorage
            if (typeof Storage !== 'undefined') {
                localStorage.setItem(this.STORAGE_KEY, theme);
            }
            
            // Store in cookie for server-side rendering
            this.setCookie(this.COOKIE_NAME, theme, 365); // 1 year expiry
            
            console.log(`[ThemeService] Theme preference stored: ${theme}`);
        } catch (error) {
            console.warn('[ThemeService] Failed to store theme preference:', error);
        }
    }

    /**
     * Apply theme to the document
     * 
     * @param {string} theme - Theme to apply ('light', 'dark', 'auto')
     */
    async applyTheme(theme) {
        if (!Object.values(this.THEMES).includes(theme)) {
            console.warn(`[ThemeService] Invalid theme: ${theme}, defaulting to auto`);
            theme = this.THEMES.AUTO;
        }
        
        console.log(`[ThemeService] Applying theme: ${theme}`);
        
        // Prevent transition flash during theme change
        document.body.classList.add('theme-transitioning');
        
        this.currentTheme = theme;
        this.storeTheme(theme);
        
        // Apply the effective theme to DOM
        this.applyEffectiveTheme();
        
        // Update UI controls
        this.updateThemeToggle();
        
        // Emit theme change event for other components
        this.emitEvent('theme-changed', {
            theme: this.currentTheme,
            effectiveTheme: this.getEffectiveTheme()
        });
        
        // Re-enable transitions after a brief delay
        setTimeout(() => {
            document.body.classList.remove('theme-transitioning');
        }, 50);
        
        console.log(`[ThemeService] Theme applied successfully: ${theme} (effective: ${this.getEffectiveTheme()})`);
    }

    /**
     * Apply the effective theme (resolving 'auto' to actual theme)
     */
    applyEffectiveTheme() {
        const effectiveTheme = this.getEffectiveTheme();
        document.documentElement.setAttribute(this.THEME_ATTRIBUTE, effectiveTheme);
        
        // Update meta theme-color for mobile browsers
        this.updateMetaThemeColor(effectiveTheme);
    }

    /**
     * Get the effective theme (resolves 'auto' to actual system preference)
     * 
     * @returns {string} 'light' or 'dark'
     */
    getEffectiveTheme() {
        if (this.currentTheme === this.THEMES.AUTO) {
            return this.systemPreference || this.THEMES.LIGHT;
        }
        return this.currentTheme;
    }

    /**
     * Update meta theme-color for mobile browser chrome
     * 
     * @param {string} theme - Effective theme ('light' or 'dark')
     */
    updateMetaThemeColor(theme) {
        let metaThemeColor = document.querySelector('meta[name="theme-color"]');
        
        if (!metaThemeColor) {
            metaThemeColor = document.createElement('meta');
            metaThemeColor.name = 'theme-color';
            document.head.appendChild(metaThemeColor);
        }
        
        // Use brand colors that match the CSS theme
        const colors = {
            light: '#f8fafc', // --color-neutral-50 in light mode
            dark: '#1a1a1a'   // --color-neutral-100 in dark mode
        };
        
        metaThemeColor.content = colors[theme] || colors.light;
    }

    /**
     * Toggle to next theme in cycle (auto -> light -> dark -> auto)
     */
    toggle() {
        const currentIndex = this.THEME_CYCLE.indexOf(this.currentTheme);
        const nextIndex = (currentIndex + 1) % this.THEME_CYCLE.length;
        const nextTheme = this.THEME_CYCLE[nextIndex];
        
        console.log(`[ThemeService] Toggling theme: ${this.currentTheme} -> ${nextTheme}`);
        this.applyTheme(nextTheme);
        
        // Announce theme change for screen readers
        this.announceThemeChange(nextTheme);
    }

    /**
     * Update theme toggle button UI
     */
    updateThemeToggle() {
        const toggleButton = document.querySelector('.theme-toggle');
        if (!toggleButton) return;
        
        // Update data attribute for CSS styling
        toggleButton.setAttribute('data-theme', this.currentTheme);
        
        // Update tooltip text
        const effectiveTheme = this.getEffectiveTheme();
        const tooltipText = `${this.germanText.toggle} (${this.germanText[this.currentTheme]})`;
        toggleButton.setAttribute('title', tooltipText);
        toggleButton.setAttribute('aria-label', tooltipText);
        
        console.log(`[ThemeService] Theme toggle updated for: ${this.currentTheme}`);
    }

    /**
     * Announce theme change for screen readers (German accessibility)
     * 
     * @param {string} theme - New theme that was activated
     */
    announceThemeChange(theme) {
        const announcement = `${this.germanText[theme]} ${this.germanText.activated}`;
        
        // Create temporary announcement element
        const announcer = document.createElement('div');
        announcer.setAttribute('aria-live', 'polite');
        announcer.setAttribute('aria-atomic', 'true');
        announcer.className = 'sr-only';
        announcer.textContent = announcement;
        
        document.body.appendChild(announcer);
        
        // Remove after announcement
        setTimeout(() => {
            document.body.removeChild(announcer);
        }, 1000);
    }

    /**
     * Set up event listeners for theme toggle button
     */
    bindEventListeners() {
        // Theme toggle button
        const toggleButton = document.querySelector('.theme-toggle');
        if (toggleButton) {
            toggleButton.addEventListener('click', this.toggle);
            
            // Keyboard navigation support
            toggleButton.addEventListener('keydown', (event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    this.toggle();
                }
            });
        }
        
        // Listen for page visibility changes to sync theme
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden && this.currentTheme === this.THEMES.AUTO) {
                // Re-check system preference when page becomes visible
                this.setupSystemPreferenceDetection();
                this.applyEffectiveTheme();
            }
        });
        
        console.log('[ThemeService] Event listeners bound successfully');
    }

    /**
     * Emit custom event for theme changes
     * 
     * @param {string} eventName - Event name
     * @param {object} detail - Event detail data
     */
    emitEvent(eventName, detail) {
        const event = new CustomEvent(eventName, { detail });
        document.dispatchEvent(event);
        console.log(`[ThemeService] Event emitted: ${eventName}`, detail);
    }

    /**
     * Add event listener for theme changes
     * 
     * @param {string} eventName - Event name to listen for
     * @param {function} callback - Callback function
     */
    addEventListener(eventName, callback) {
        document.addEventListener(eventName, callback);
        this.eventListeners.add({ eventName, callback });
    }

    /**
     * Remove event listener
     * 
     * @param {string} eventName - Event name
     * @param {function} callback - Callback function
     */
    removeEventListener(eventName, callback) {
        document.removeEventListener(eventName, callback);
        this.eventListeners.delete({ eventName, callback });
    }

    /**
     * Get cookie value by name
     * 
     * @param {string} name - Cookie name
     * @returns {string|null} Cookie value or null if not found
     */
    getCookie(name) {
        const nameEQ = name + "=";
        const ca = document.cookie.split(';');
        for (let i = 0; i < ca.length; i++) {
            let c = ca[i];
            while (c.charAt(0) === ' ') c = c.substring(1, c.length);
            if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length, c.length);
        }
        return null;
    }

    /**
     * Set cookie with expiration
     * 
     * @param {string} name - Cookie name
     * @param {string} value - Cookie value
     * @param {number} days - Days until expiration
     */
    setCookie(name, value, days) {
        let expires = "";
        if (days) {
            const date = new Date();
            date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
            expires = "; expires=" + date.toUTCString();
        }
        document.cookie = name + "=" + (value || "") + expires + "; path=/; SameSite=Lax";
    }

    /**
     * Get current theme information
     * 
     * @returns {object} Theme information
     */
    getThemeInfo() {
        return {
            current: this.currentTheme,
            effective: this.getEffectiveTheme(),
            system: this.systemPreference,
            available: Object.values(this.THEMES)
        };
    }

    /**
     * Clean up resources when service is destroyed
     */
    destroy() {
        // Remove system preference listener
        if (this.mediaQuery) {
            this.mediaQuery.removeEventListener('change', this.handleSystemThemeChange);
        }
        
        // Remove event listeners
        this.eventListeners.forEach(({ eventName, callback }) => {
            document.removeEventListener(eventName, callback);
        });
        this.eventListeners.clear();
        
        // Remove theme toggle listener
        const toggleButton = document.querySelector('.theme-toggle');
        if (toggleButton) {
            toggleButton.removeEventListener('click', this.toggle);
        }
        
        console.log('[ThemeService] Service destroyed and cleaned up');
    }
}

// Global theme service instance
window.themeService = null;

/**
 * Initialize theme service when DOM is ready
 * 
 * Business Context:
 * - Ensures theme is applied before page content is visible
 * - Prevents flash of unstyled content (FOUC)
 * - Sets up proper accessibility and German localization
 */
document.addEventListener('DOMContentLoaded', async () => {
    console.log('[ThemeService] DOM ready, initializing theme service...');
    
    try {
        window.themeService = new ThemeService();
        await window.themeService.init();
        
        // Make service globally available for debugging and integration
        if (typeof window !== 'undefined') {
            window.FinanceApp = window.FinanceApp || {};
            window.FinanceApp.themeService = window.themeService;
        }
        
    } catch (error) {
        console.error('[ThemeService] Critical error during initialization:', error);
    }
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ThemeService;
}