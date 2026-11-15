/**
 * JAS International - Cart Module (ES2024+)
 * Handles client-side cart badge updates and cart session token resolution.
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
 * Initializes cart module listeners.
 */
export function initCart() {
    // Badges are SSR rendered by ASP.NET Core from database cart state.
}
