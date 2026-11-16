/**
 * JAS International - Cart Module (ES2024+)
 * Handles client-side cart badge updates, synchronizing count attributes,
 * and cart session token resolution for Blazor Server and WASM circuits.
 */

/**
 * Reads the secure cart GUID token from the client cookie.
 * @returns {string|null}
 */
export function getCartToken() {
    const match = document.cookie.match(/(?:^|;\s*)jas_cart_id=([^;]*)/);
    return match ? decodeURIComponent(match[1]) : null;
}

/**
 * Updates all cart badge elements across the page with the latest count.
 * @param {number} count
 */
export function updateCartBadges(count) {
    if (typeof count !== 'number' || isNaN(count)) return;
    const badges = document.querySelectorAll('[data-cart-count]');
    badges.forEach(badge => {
        badge.textContent = count;
        badge.setAttribute('data-cart-count', count);
    });
}

/**
 * Synchronizes cart badges from in-page sync marker elements.
 * Eliminates the 1-off lag when in-page SSR forms add items to the cart.
 */
export function syncCartBadges() {
    const syncEl = document.getElementById('cart-badge-sync');
    if (syncEl) {
        const count = parseInt(syncEl.getAttribute('data-cart-count-sync'), 10);
        if (!isNaN(count)) {
            updateCartBadges(count);
        }
    }
}

/**
 * Initializes cart module listeners and performs initial badge sync.
 */
export function initCart() {
    syncCartBadges();
}

// Expose functions globally on window for Blazor Interop (CartService & ServerCartBackend)
if (typeof window !== 'undefined') {
    window.jasGetCartToken = getCartToken;
    window.jasUpdateCartCount = updateCartBadges;
    window.jasSyncCartBadges = syncCartBadges;
}
