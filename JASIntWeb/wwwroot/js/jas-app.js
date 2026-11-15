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

/**
 * Handles switching the main product image in the gallery with next-gen AVIF/WebP srcset support
 */
window.switchMainProductImage = function (btn) {
    if (!btn) return;
    const src = btn.dataset.src;
    const avif = btn.dataset.avif;
    const webp = btn.dataset.webp;
    const pic = document.getElementById("main-product-picture");
    if (pic) {
        const sourceAvif = pic.querySelector('source[type="image/avif"]');
        if (sourceAvif && avif) sourceAvif.srcset = avif;
        const sourceWebp = pic.querySelector('source[type="image/webp"]');
        if (sourceWebp && webp) sourceWebp.srcset = webp;
    }
    const img = document.getElementById("main-product-img");
    if (img && src) img.src = src;

    document.querySelectorAll(".thumb-btn").forEach(b => {
        b.classList.remove("border-blue-600", "shadow-xs", "ring-2", "ring-blue-500/20");
        b.classList.add("border-slate-200");
    });
    btn.classList.remove("border-slate-200");
    btn.classList.add("border-blue-600", "shadow-xs", "ring-2", "ring-blue-500/20");
};

// Bootstrap application
AppOrchestrator.boot();
