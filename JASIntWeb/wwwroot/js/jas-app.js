/**
 * JAS International - Main Functional Application Orchestrator (ES2024+)
 * Coordinates always-on core modules and lazily imports route-specific modules
 * on initial load and Blazor Static SSR enhanced navigation.
 */

import { initNavigation, startNavigation, finishNavigation, scrollElement } from './modules/navigation.js';
import { initConsent } from './modules/consent.js';
import { initWishlist } from './modules/wishlist.js';
import { initCart, clearCartToken } from './modules/cart.js';
import { initOverlayDelegation, closeAllOverlays } from './core/overlay.js';

let cleanupHandlers = [];
let uiDelegationBound = false;

function registerCleanup(fn) {
    if (typeof fn === 'function') {
        cleanupHandlers.push(fn);
    }
}

function runCleanups() {
    while (cleanupHandlers.length > 0) {
        const fn = cleanupHandlers.pop();
        try { fn(); } catch {}
    }
}

function syncStickyBottomBar() {
    const hasBuyBar = Boolean(document.querySelector('[data-mobile-buy-bar]'));
    document.body.classList.toggle('has-mobile-buy-bar', hasBuyBar);
}

function bindStorefrontDelegation() {
    if (uiDelegationBound) return;
    uiDelegationBound = true;

    document.addEventListener('click', event => {
        if (!(event.target instanceof Element)) return;

        // Horizontal product section slider buttons
        const sliderBtn = event.target.closest('[data-slider-scroll]');
        if (sliderBtn) {
            const delta = parseInt(sliderBtn.getAttribute('data-slider-scroll') || '0', 10);
            const track = sliderBtn.parentElement?.querySelector('.slider-track');
            if (track && delta) scrollElement(track, delta);
            return;
        }

        // Product detail quantity stepper buttons
        const qtyStepBtn = event.target.closest('[data-qty-step]');
        if (qtyStepBtn) {
            const step = parseInt(qtyStepBtn.getAttribute('data-qty-step') || '0', 10);
            const targetSelector = qtyStepBtn.getAttribute('data-qty-target') || '#product-qty-input';
            const input = document.querySelector(targetSelector);
            if (input instanceof HTMLInputElement && step) {
                const min = parseInt(input.min || '1', 10) || 1;
                const max = parseInt(input.max || '99', 10) || 99;
                const current = parseInt(input.value || String(min), 10) || min;
                input.value = String(Math.min(max, Math.max(min, current + step)));
            }
            return;
        }

        // Generic dismiss button for inline alerts
        const dismissBtn = event.target.closest('[data-dismiss-target]');
        if (dismissBtn) {
            const selector = dismissBtn.getAttribute('data-dismiss-target');
            const target = selector ? dismissBtn.closest(selector) : dismissBtn.parentElement;
            target?.remove();
        }
    });

    // Sync selected quantity input into Buy Now SSR form before submission
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        const syncSelector = form.getAttribute('data-qty-sync');
        if (!syncSelector) return;

        const source = document.querySelector(syncSelector);
        const target = form.querySelector('input[name="quantity"]');
        if (source instanceof HTMLInputElement && target instanceof HTMLInputElement) {
            target.value = source.value;
        }
    }, true);
}

async function initRouteModules() {
    const path = location.pathname || '/';

    if (document.getElementById('jas-cart-page')) {
        const { initCartPage } = await import('./modules/cart-page.js');
        initCartPage();
    }

    if (document.querySelector('.thumb-btn')) {
        const { initGallery } = await import('./modules/gallery.js');
        initGallery();
    }

    if (document.getElementById('reviews') || document.getElementById('write-review-modal')) {
        const { initReviews } = await import('./modules/reviews.js');
        initReviews();
    }

    if (document.querySelector('[data-campaign-countdown], [data-campaign-hub-search], [data-campaign-filter-bar], button[data-copy-text]')) {
        const { initCampaignPages, cleanupCampaignPages } = await import('./modules/campaign.js');
        initCampaignPages();
        registerCleanup(cleanupCampaignPages);
    }

    if (document.querySelector('[data-carousel]')) {
        const { initCarousel, cleanupCarousel } = await import('./modules/carousel.js');
        initCarousel();
        registerCleanup(cleanupCarousel);
    }

    if (document.querySelector('#live-order-tracker, #payment-verification-stream')) {
        const { initOrderTracking, cleanupOrderTracking } = await import('./modules/order-tracking.js');
        initOrderTracking();
        registerCleanup(cleanupOrderTracking);
    }

    const orderTracker = document.getElementById('live-order-tracker');
    const needsDeviceOrders = Boolean(
        orderTracker?.dataset.recordOrderToken ||
        document.querySelector('[data-device-orders]') ||
        path.startsWith('/checkout') ||
        path.startsWith('/orders') ||
        path.startsWith('/account')
    );
    if (needsDeviceOrders) {
        const { recordGuestOrder } = await import('./modules/device-orders.js');
        if (orderTracker && orderTracker.dataset.orderRecorded !== 'true') {
            orderTracker.dataset.orderRecorded = 'true';
            if (orderTracker.dataset.clearCart === 'true') {
                clearCartToken();
            }
            const id = Number(orderTracker.dataset.recordOrderId || 0);
            const token = orderTracker.dataset.recordOrderToken;
            const total = Number(orderTracker.dataset.recordOrderTotal || 0);
            if (id > 0 && token) {
                recordGuestOrder(id, token, total);
            }
        }
    }

    if (document.querySelector('meta[name="jas-meta-pixel"]') || document.querySelector('[data-meta-event]') || path.startsWith('/checkout')) {
        const { initMetaTracking, trackPageView } = await import('./modules/meta-tracking.js');
        initMetaTracking();
        trackPageView();
    }

    if (!navigator.webdriver && !path.startsWith('/admin') && !path.startsWith('/api')) {
        const { initVisitorAnalytics } = await import('./modules/analytics.js');
        initVisitorAnalytics();
    }
}

function initModules() {
    syncStickyBottomBar();
    initNavigation();
    initConsent();
    initOverlayDelegation();
    initWishlist();
    initCart();
    bindStorefrontDelegation();
    initRouteModules();
}

function onEnhancedNavigationStart() {
    closeAllOverlays();
    runCleanups();
    startNavigation();
}

function onEnhancedNavigationEnd() {
    finishNavigation();
    initModules();
}

function onPageHide() {
    runCleanups();
}

function connectBlazorNavigation() {
    if (!window.Blazor || window.__jasBlazorNavConnected) return;
    window.__jasBlazorNavConnected = true;
    window.Blazor.addEventListener('enhancednavigationstart', onEnhancedNavigationStart);
    window.Blazor.addEventListener('enhancedload', onEnhancedNavigationEnd);
}

function boot() {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            initModules();
            connectBlazorNavigation();
        }, { once: true });
    } else {
        initModules();
    }

    connectBlazorNavigation();
    window.addEventListener('pagehide', onPageHide);
}

boot();
