/**
 * JAS International - Functional Wishlist Module (ES2024+)
 * Manages customer wishlist in localStorage, synchronizes header badges,
 * updates heart toggles across catalog cards, and provides JS interop for Blazor.
 */

import { emit } from '../core/events.js';

const STORAGE_KEY = 'jas.wishlist.v1';

/**
 * Retrieves the saved wishlist items from localStorage.
 * @returns {Array<{id: number, name: string, slug: string, image: string, price: number, addedAt: string}>}
 */
export function getWishlistItems() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        if (!raw) return [];
        const parsed = JSON.parse(raw);
        return Array.isArray(parsed) ? parsed : [];
    } catch {
        return [];
    }
}

/**
 * Retrieves an array of unique product IDs in the wishlist.
 * Used directly by Blazor InteractiveServer JS interop for /wishlist.
 * @returns {number[]}
 */
export function getWishlistIds() {
    return getWishlistItems().map(item => Number(item.id)).filter(id => id > 0);
}

/**
 * Persists the wishlist items array to localStorage.
 * @param {Array} items
 */
export function saveWishlistItems(items) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
    } catch (e) {
        console.warn('[JAS Wishlist] Unable to persist to localStorage:', e);
    }
}

/**
 * Checks if a specific product ID is currently in the wishlist.
 * @param {number} productId
 * @returns {boolean}
 */
export function isInWishlist(productId) {
    const id = Number(productId);
    return getWishlistItems().some(item => Number(item.id) === id);
}

/**
 * Updates all wishlist badge elements across the page.
 */
export function updateWishlistBadges() {
    const count = getWishlistItems().length;
    const badges = document.querySelectorAll('[data-wishlist-count]');
    badges.forEach(badge => {
        badge.textContent = count;
        badge.setAttribute('data-wishlist-count', count);
        if (count > 0) {
            badge.classList.remove('hidden');
        } else {
            badge.classList.add('hidden');
        }
    });
}

/**
 * Updates all wishlist button toggle states (active/inactive SVG) on the current page.
 */
export function updateWishlistButtons() {
    const items = getWishlistItems();
    const idSet = new Set(items.map(i => Number(i.id)));

    document.querySelectorAll('[data-wishlist-toggle]').forEach(button => {
        const id = parseInt(button.getAttribute('data-wishlist-toggle'), 10);
        const isActive = idSet.has(id);
        const svg = button.querySelector('svg');

        if (isActive) {
            button.classList.add('text-rose-500');
            button.classList.remove('text-slate-400');
            button.setAttribute('aria-label', 'Remove from Wishlist');
            if (svg) {
                svg.classList.add('fill-rose-500', 'text-rose-500');
                svg.classList.remove('fill-none');
            }
        } else {
            button.classList.remove('text-rose-500');
            button.classList.add('text-slate-400');
            button.setAttribute('aria-label', 'Save to Wishlist');
            if (svg) {
                svg.classList.remove('fill-rose-500', 'text-rose-500');
                svg.classList.add('fill-none');
            }
        }
    });
}

/**
 * Toggles a product in the wishlist (adds if absent, removes if present).
 * @param {{id: number, name?: string, slug?: string, image?: string, price?: number}} product
 * @returns {boolean} True if added, false if removed.
 */
export function toggleWishlistItem(product) {
    if (!product || !product.id) return false;
    const prodId = Number(product.id);
    const items = getWishlistItems();
    const index = items.findIndex(item => Number(item.id) === prodId);
    let added = false;

    if (index >= 0) {
        items.splice(index, 1);
        added = false;
    } else {
        items.unshift({
            id: prodId,
            name: product.name || '',
            slug: product.slug || '',
            image: product.image || '',
            price: Number(product.price) || 0,
            addedAt: new Date().toISOString()
        });
        added = true;
    }

    saveWishlistItems(items);
    updateWishlistBadges();
    updateWishlistButtons();

    emit('wishlist:updated', { productId: prodId, added, count: items.length });
    return added;
}

/**
 * Removes a product by ID from the wishlist.
 * @param {number} productId
 */
export function removeFromWishlist(productId) {
    const prodId = Number(productId);
    const items = getWishlistItems().filter(item => Number(item.id) !== prodId);
    saveWishlistItems(items);
    updateWishlistBadges();
    updateWishlistButtons();
    emit('wishlist:updated', { productId: prodId, added: false, count: items.length });
}

/**
 * Clears all items from the wishlist.
 */
export function clearWishlist() {
    saveWishlistItems([]);
    updateWishlistBadges();
    updateWishlistButtons();
    emit('wishlist:cleared');
}

/**
 * Wires global delegated click listeners for wishlist toggles.
 */
export function initWishlistDelegation() {
    if (document.__jasWishlistDelegated) return;
    document.__jasWishlistDelegated = true;

    document.addEventListener('click', (event) => {
        const toggleBtn = event.target.closest('[data-wishlist-toggle]');
        if (toggleBtn) {
            event.preventDefault();
            event.stopPropagation();

            const id = parseInt(toggleBtn.getAttribute('data-wishlist-toggle'), 10);
            if (!id) return;

            const product = {
                id,
                name: toggleBtn.getAttribute('data-product-name') || '',
                slug: toggleBtn.getAttribute('data-product-slug') || '',
                image: toggleBtn.getAttribute('data-product-image') || '',
                price: parseFloat(toggleBtn.getAttribute('data-product-price') || '0')
            };

            const added = toggleWishlistItem(product);

            // Pop animation on button
            toggleBtn.classList.remove('scale-125');
            void toggleBtn.offsetWidth;
            toggleBtn.classList.add('scale-125');
            setTimeout(() => toggleBtn.classList.remove('scale-125'), 200);

            return;
        }

        const removeBtn = event.target.closest('[data-wishlist-remove]');
        if (removeBtn) {
            event.preventDefault();
            event.stopPropagation();
            const id = parseInt(removeBtn.getAttribute('data-wishlist-remove'), 10);
            if (id) {
                removeFromWishlist(id);
                const card = removeBtn.closest('article, .product-card');
                if (card) {
                    card.classList.add('opacity-0', 'scale-95');
                    setTimeout(() => card.remove(), 250);
                }
            }
        }
    });
}

/**
 * Initializes wishlist state on page load or after enhanced navigation.
 */
export function initWishlist() {
    updateWishlistBadges();
    updateWishlistButtons();
    initWishlistDelegation();
}

// Global window exposure for Blazor interop
if (typeof window !== 'undefined') {
    window.jasGetWishlistIds = getWishlistIds;
    window.jasRemoveFromWishlist = removeFromWishlist;
    window.jasClearWishlist = clearWishlist;
    window.jasIsInWishlist = isInWishlist;
    window.jasToggleWishlist = (id, name, slug, img, price) => toggleWishlistItem({ id, name, slug, image: img, price });
    window.jasUpdateWishlistCount = updateWishlistBadges;
}
