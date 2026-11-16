/**
 * JAS International - Main Application Orchestrator (ES2024+)
 * Coordinates modular scripts with Blazor Static SSR enhanced navigation lifecycle
 * and optional native View Transitions API.
 */

import { initWishlist } from './modules/wishlist.js';
import { initCart, syncCartBadges } from './modules/cart.js';
import { initGallery, switchMainImage } from './modules/gallery.js';
import { initReviews } from './modules/reviews.js';

class AppOrchestrator {
    /**
     * Initializes all UI and tracking modules for the current DOM state
     */
    static onDomReady() {
        initWishlist();
        initCart();
        syncCartBadges();
        initGallery();
        initReviews();

        const JAS = window.JAS;
        JAS?.Carousel?.init();
        JAS?.OrderTracking?.init();
        JAS?.AdminEditor?.init();
        JAS?.MetaTracking?.init();
        JAS?.VisitorAnalytics?.init();
    }

    /**
     * Handles enhanced navigation start (instant non-blocking feedback)
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
            initWishlist();
            initCart();
            syncCartBadges();
            initGallery();
            initReviews();
            JAS?.Carousel?.init();
            JAS?.OrderTracking?.init();
            JAS?.AdminEditor?.init();
            JAS?.MetaTracking?.init();
            JAS?.MetaTracking?.trackPageView();
            JAS?.VisitorAnalytics?.trackPageView();
        };

        // Use modern native View Transitions API if supported by the browser
        if (typeof document.startViewTransition === "function") {
            document.startViewTransition(() => updateDom());
        } else {
            updateDom();
        }
    }

    /**
     * Handles cleanup on page dismissal (pagehide preserves bfcache)
     */
    static onPageHide() {
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

        // Use pagehide instead of beforeunload to guarantee bfcache (Back/Forward Cache) eligibility
        window.addEventListener("pagehide", this.onPageHide, { passive: true });
    }
}

// Backward-compatibility bridge for any legacy inline onclick calls
window.switchMainProductImage = switchMainImage;

// Bootstrap application
AppOrchestrator.boot();
