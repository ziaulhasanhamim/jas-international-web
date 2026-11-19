/**
 * JAS International - Main Functional Application Orchestrator (ES2024+)
 * Coordinates modular scripts with Blazor Static SSR enhanced navigation lifecycle
 * without starting Blazor circuits on static storefront routes.
 */

import { initConsent } from './modules/consent.js';
import { initWishlist } from './modules/wishlist.js';
import { initCart } from './modules/cart.js';
import { initCartDrawer } from './modules/cart-drawer.js';
import { initCartPage } from './modules/cart-page.js';
import { initOverlayDelegation } from './core/overlay.js';
import { initGallery } from './modules/gallery.js';
import { initReviews } from './modules/reviews.js';

async function initOrderTracking() {
    if (!document.querySelector('#live-order-tracker, #payment-verification-stream')) return;
    await import('./modules/order-tracking.js');
    window.JAS?.OrderTracking?.init();
}

/**
 * Initializes all functional modules for the active DOM state.
 */
function initModules() {
    initConsent();
    initOverlayDelegation();
    initWishlist();
    initCart();
    initCartDrawer();
    initCartPage();
    initGallery();
    initReviews();

    const JAS = window.JAS;
    JAS?.Carousel?.init();
    initOrderTracking();
    JAS?.AdminEditor?.init();
    JAS?.MetaTracking?.init();
    JAS?.MetaTracking?.trackPageView();
    JAS?.VisitorAnalytics?.init();
}

/**
 * Handles Blazor enhanced navigation start (instant non-blocking feedback).
 */
function onEnhancedNavigationStart() {
    const JAS = window.JAS;
    JAS?.Navigation?.start();
    JAS?.OrderTracking?.cleanup();
    JAS?.Carousel?.cleanup();
}

/**
 * Handles Blazor enhanced navigation end (DOM patched).
 */
function onEnhancedNavigationEnd() {
    const JAS = window.JAS;
    JAS?.Navigation?.finish();
    initModules();
}

/**
 * Handles page dismissal (preserves bfcache).
 */
function onPageHide() {
    window.JAS?.OrderTracking?.cleanup();
    window.JAS?.Carousel?.cleanup();
}

/**
 * Bootstraps the application.
 */
function boot() {
    // 1. Initial DOM load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initModules, { once: true });
    } else {
        initModules();
    }

    // 2. Connect to Blazor enhanced navigation events
    if (window.Blazor) {
        window.Blazor.addEventListener('enhancednavigationstart', onEnhancedNavigationStart);
        window.Blazor.addEventListener('enhancedload', onEnhancedNavigationEnd);
    } else {
        document.addEventListener('DOMContentLoaded', () => {
            if (window.Blazor) {
                window.Blazor.addEventListener('enhancednavigationstart', onEnhancedNavigationStart);
                window.Blazor.addEventListener('enhancedload', onEnhancedNavigationEnd);
            }
        }, { once: true });
    }

    // 3. bfcache preservation
    window.addEventListener('pagehide', onPageHide);
}

// Start application
boot();
