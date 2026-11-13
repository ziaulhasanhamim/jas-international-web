/**
 * JAS International - Main Application Orchestrator (ES2024+)
 * Coordinates modular scripts with Blazor Static SSR enhanced navigation lifecycle
 * and optional native View Transitions API.
 */
class AppOrchestrator {
    /**
     * Initializes all UI and tracking modules for the current DOM state
     */
    static onDomReady() {
        const JAS = window.JAS;
        JAS?.Carousel?.init();
        JAS?.OrderTracking?.init();
        JAS?.AdminEditor?.init();
        JAS?.MetaTracking?.init();
    }

    /**
     * Handles enhanced navigation start
     */
    static onEnhancedNavigationStart() {
        const JAS = window.JAS;
        JAS?.Navigation?.start();
        JAS?.OrderTracking?.cleanup();
    }

    /**
     * Handles enhanced navigation end
     */
    static onEnhancedNavigationEnd() {
        const JAS = window.JAS;

        const updateDom = () => {
            JAS?.Navigation?.finish();
            JAS?.Carousel?.init();
            JAS?.OrderTracking?.init();
            JAS?.AdminEditor?.init();
            JAS?.MetaTracking?.init();
            JAS?.MetaTracking?.trackPageView();
        };

        // Use modern native View Transitions API if supported by the browser
        if (typeof document.startViewTransition === "function") {
            document.startViewTransition(() => updateDom());
        } else {
            updateDom();
        }
    }

    /**
     * Handles cleanup before window/tab unloads
     */
    static onBeforeUnload() {
        window.JAS?.OrderTracking?.cleanup();
    }

    /**
     * Bootstraps the orchestrator
     */
    static boot() {
        // Connect to Blazor enhanced navigation events
        if (window.Blazor) {
            window.Blazor.addEventListener("enhancednavigationstart", this.onEnhancedNavigationStart);
            window.Blazor.addEventListener("enhancednavigationend", this.onEnhancedNavigationEnd);
        } else {
            document.addEventListener("DOMContentLoaded", () => {
                if (window.Blazor) {
                    window.Blazor.addEventListener("enhancednavigationstart", this.onEnhancedNavigationStart);
                    window.Blazor.addEventListener("enhancednavigationend", this.onEnhancedNavigationEnd);
                }
            }, { once: true });
        }

        // Initial DOM load
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", this.onDomReady, { once: true });
        } else {
            this.onDomReady();
        }

        window.addEventListener("beforeunload", this.onBeforeUnload, { passive: true });
    }
}

// Bootstrap application
AppOrchestrator.boot();
