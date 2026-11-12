/**
 * JAS International - Declarative Cart & Micro-Interaction Library
 * ---------------------------------------------------------------
 * Lightweight, dependency-free JavaScript library driven by HTML data- attributes.
 * Designed so designers and C#/.NET developers can bind and style cart interactions
 * purely in markup without writing custom JavaScript.
 *
 * Supported Attributes:
 *   [data-jas-cart-count]          - Sets inner text to the current cart item count.
 *   [data-jas-cart-badge]          - Shows element if count > 0, hides if count == 0.
 *   [data-jas-cart-show-if-items]  - Displayed only when cart has 1+ items.
 *   [data-jas-cart-hide-if-items]  - Hidden when cart has items (e.g. "Your cart is empty").
 *
 * Events:
 *   window.addEventListener('jas-cart-updated', ...) - Dispatched whenever cart changes.
 *   window.JasCart.updateBadges()                    - Manually triggers badge refresh.
 */

(function () {
    'use strict';

    const STORAGE_KEY = 'jas.cart.v2';

    function getLocalCart() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return { items: [], token: null };
            const parsed = JSON.parse(raw);
            if (!parsed || !Array.isArray(parsed.Items)) return { items: [], token: null };
            return {
                items: parsed.Items,
                token: parsed.Token || null
            };
        } catch (e) {
            return { items: [], token: null };
        }
    }

    function getCartCount() {
        const cart = getLocalCart();
        return cart.items.reduce((sum, item) => sum + (Number(item.Quantity) || 0), 0);
    }

    function updateBadges() {
        const count = getCartCount();
        const hasItems = count > 0;

        // 1. Update text for all count targets
        document.querySelectorAll('[data-jas-cart-count]').forEach(el => {
            el.textContent = count > 99 ? '99+' : count.toString();
        });

        // 2. Toggle badge visibility
        document.querySelectorAll('[data-jas-cart-badge]').forEach(el => {
            if (hasItems) {
                el.classList.remove('hidden');
                el.style.removeProperty('display');
            } else {
                el.classList.add('hidden');
            }
        });

        // 3. Conditional visibility containers
        document.querySelectorAll('[data-jas-cart-show-if-items]').forEach(el => {
            el.classList.toggle('hidden', !hasItems);
        });

        document.querySelectorAll('[data-jas-cart-hide-if-items]').forEach(el => {
            el.classList.toggle('hidden', hasItems);
        });
    }

    function emitChange() {
        window.dispatchEvent(new CustomEvent('jas-cart-updated', { detail: { count: getCartCount() } }));
    }

    // Public API for team members
    window.JasCart = {
        getCount: getCartCount,
        getCart: getLocalCart,
        updateBadges: updateBadges,
        emitChange: emitChange
    };

    // Auto-initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', updateBadges);
    } else {
        updateBadges();
    }

    // Update whenever cart updates in current window
    window.addEventListener('jas-cart-updated', updateBadges);

    // Update across multiple browser tabs
    window.addEventListener('storage', (e) => {
        if (e.key === STORAGE_KEY) {
            updateBadges();
        }
    });

    // Re-evaluate on Blazor Enhanced Navigation DOM swaps
    document.addEventListener('blazor:enhancedload', updateBadges);
    window.addEventListener('popstate', updateBadges);
})();
