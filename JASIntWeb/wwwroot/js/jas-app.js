import { initConsent } from './modules/consent.js';
/**
 * JAS International - Main Application Orchestrator (ES2024+)
 * Coordinates modular scripts with Blazor Static SSR enhanced navigation lifecycle
 * without starting Blazor circuits on static storefront routes.
 */

import { initWishlist } from './modules/wishlist.js';
import { initCart } from './modules/cart.js';
import { initGallery, switchMainImage } from './modules/gallery.js';
import { initReviews } from './modules/reviews.js';

class AppOrchestrator {
    static async initOrderTracking() {
        if (!document.querySelector('#live-order-tracker, #payment-verification-stream')) return;
        await import('./modules/order-tracking.js');
        window.JAS?.OrderTracking?.init();
    }

    /**
     * Initializes all UI and tracking modules for the current DOM state
     */
    static onDomReady() {
        initConsent();
        initWishlist();
        initCart();
        initGallery();
        initReviews();

        const JAS = window.JAS;
        JAS?.Carousel?.init();
        AppOrchestrator.initOrderTracking();
        JAS?.AdminEditor?.init();
        JAS?.MetaTracking?.init();
        JAS?.MetaTracking?.trackPageView();
        JAS?.VisitorAnalytics?.init();
    }

    /**
     * Handles enhanced navigation start (instant non-blocking feedback)
     */
    static onEnhancedNavigationStart() {
        const JAS = window.JAS;
        JAS?.Navigation?.start();
        JAS?.OrderTracking?.cleanup();
        JAS?.Carousel?.cleanup();
    }

    /**
     * Handles enhanced navigation end
     */
    static onEnhancedNavigationEnd() {
        const JAS = window.JAS;

        const updateDom = () => {
            JAS?.Navigation?.finish();
            initConsent();
            initWishlist();
            initCart();
            initGallery();
            initReviews();
            JAS?.Carousel?.init();
            AppOrchestrator.initOrderTracking();
            JAS?.AdminEditor?.init();
            JAS?.MetaTracking?.init();
            JAS?.MetaTracking?.trackPageView();
            JAS?.VisitorAnalytics?.trackPageView();
        };

        // Blazor has already patched the DOM. Re-initialize directly; starting a
        // view transition here can leave an input-blocking snapshot over mobile pages.
        updateDom();
    }

    /**
     * Handles cleanup on page dismissal (pagehide preserves bfcache)
     */
    static onPageHide() {
        window.JAS?.OrderTracking?.cleanup();
        window.JAS?.Carousel?.cleanup();
    }

    /**
     * Bootstraps the orchestrator
     */
    static boot() {
        // Connect to Blazor enhanced navigation events
        if (window.Blazor) {
            window.Blazor.addEventListener("enhancednavigationstart", this.onEnhancedNavigationStart);
            window.Blazor.addEventListener("enhancedload", this.onEnhancedNavigationEnd);
        } else {
            document.addEventListener("DOMContentLoaded", () => {
                if (window.Blazor) {
                    window.Blazor.addEventListener("enhancednavigationstart", this.onEnhancedNavigationStart);
                    window.Blazor.addEventListener("enhancedload", this.onEnhancedNavigationEnd);
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
        window.addEventListener("pageshow", event => { if (event.persisted) this.onDomReady(); }, { passive: true });
    }
}

// Backward-compatibility bridge for any legacy inline onclick calls
window.switchMainProductImage = switchMainImage;

// Bootstrap application
AppOrchestrator.boot();
