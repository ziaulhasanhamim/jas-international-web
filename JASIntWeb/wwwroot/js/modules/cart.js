/**
 * JAS International - Cart Module (ES2024+)
 * Handles client-side cart operations (Add to Cart, Buy Now, Badge Synchronization)
 * with optimistic feedback, non-blocking fetch API calls, and zero page reloads.
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
    const safeCount = Math.max(0, parseInt(count, 10) || 0);
    const badges = document.querySelectorAll('[data-cart-count]');
    badges.forEach(badge => {
        badge.textContent = safeCount;
        badge.setAttribute('data-cart-count', safeCount);

        // Add micro-pulse animation
        badge.classList.remove('animate-pulse');
        void badge.offsetWidth; // Force reflow
        badge.classList.add('animate-pulse');
    });
}

/**
 * Fetches the current cart item count from the backend API.
 * @returns {Promise<number>}
 */
export async function refreshCartBadge() {
    try {
        const response = await fetch('/api/cart/count', {
            method: 'GET',
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin',
            cache: 'no-store'
        });
        if (response.ok) {
            const data = await response.json();
            const count = typeof data.count === 'number' ? data.count : 0;
            updateCartBadges(count);
            return count;
        }
    } catch (e) {
        console.warn('[JAS] Failed to refresh cart badge:', e);
    }
    return 0;
}

/**
 * Displays a non-intrusive floating toast notification.
 * @param {string} message
 * @param {'success'|'error'|'info'} [type='success']
 * @param {{ text: string, href: string }|null} [action=null]
 */
export function showCartToast(message, type = 'success', action = { text: 'View Cart →', href: '/cart' }) {
    let container = document.getElementById('jas-toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'jas-toast-container';
        container.className = 'fixed top-20 right-4 sm:right-6 z-50 flex flex-col gap-2 max-w-sm w-full pointer-events-none';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = `pointer-events-auto flex items-center justify-between gap-3 p-3.5 sm:p-4 rounded-2xl shadow-xl text-xs sm:text-sm font-semibold transition-all duration-300 transform translate-y-2 opacity-0 ${
        type === 'error'
            ? 'bg-rose-600 text-white shadow-rose-900/20'
            : 'bg-slate-900/95 text-white backdrop-blur-md shadow-slate-900/30'
    }`;

    const icon = type === 'error' ? '⚠️' : '🛒';
    let actionHtml = '';
    if (action) {
        actionHtml = `<a href="${action.href}" class="ml-auto inline-flex items-center gap-1 rounded-xl bg-blue-600 hover:bg-blue-500 px-3 py-1 text-xs font-bold text-white transition active:scale-95 shrink-0">${action.text}</a>`;
    }

    toast.innerHTML = `
        <div class="flex items-center gap-2.5 min-w-0">
            <span class="text-base shrink-0">${icon}</span>
            <span class="truncate">${message}</span>
        </div>
        ${actionHtml}
    `;

    container.appendChild(toast);

    // Animate in
    requestAnimationFrame(() => {
        toast.classList.remove('translate-y-2', 'opacity-0');
        toast.classList.add('translate-y-0', 'opacity-100');
    });

    // Auto dismiss after 3.5 seconds
    setTimeout(() => {
        toast.classList.add('opacity-0', '-translate-y-2');
        setTimeout(() => toast.remove(), 300);
    }, 3500);
}

/**
 * Adds an item to the cart using the backend API.
 * @param {number} variantId
 * @param {number} quantity
 * @param {number} [productId=0]
 * @returns {Promise<{success: boolean, message?: string, cart?: object}>}
 */
export async function addToCart(variantId, quantity = 1, productId = 0) {
    try {
        const payload = {
            variantId: Number(variantId) || 0,
            quantity: Math.max(1, Number(quantity) || 1),
            productId: Number(productId) || 0
        };

        const headers = {
            'Content-Type': 'application/json',
            'Accept': 'application/json'
        };
        const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (csrfToken) {
            headers['X-CSRF-TOKEN'] = csrfToken;
        }

        const response = await fetch('/api/cart', {
            method: 'POST',
            headers,
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            const cart = await response.json();
            const totalCount = cart.itemCount ?? (Array.isArray(cart.items) ? cart.items.reduce((s, x) => s + (x.quantity || 0), 0) : 1);
            updateCartBadges(totalCount);
            showCartToast('Added to your shopping cart!', 'success');
            return { success: true, cart };
        } else {
            let errorMsg = 'Could not add item to cart.';
            try {
                const errData = await response.json();
                if (errData?.message) errorMsg = errData.message;
            } catch {}
            showCartToast(errorMsg, 'error', null);
            return { success: false, message: errorMsg };
        }
    } catch (err) {
        console.error('[JAS] Add to cart error:', err);
        showCartToast('Network error. Please try again.', 'error', null);
        return { success: false, message: 'Network error.' };
    }
}

/**
 * Adds item to cart and navigates directly to /cart.
 * Navigating via standard location.assign guarantees window scrolls to top (0,0).
 * @param {number} variantId
 * @param {number} quantity
 * @param {number} [productId=0]
 */
export async function buyNow(variantId, quantity = 1, productId = 0) {
    const result = await addToCart(variantId, quantity, productId);
    if (result.success) {
        window.location.assign('/cart');
    }
}

/**
 * Synchronizes cart badges from in-page sync marker elements if present.
 */
export function syncCartBadges() {
    refreshCartBadge();
}

/**
 * Initializes cart module listeners and performs initial badge sync.
 */
export function initCart() {
    refreshCartBadge();

    // Wire global delegation for click events (guaranteed single listener)
    if (!document.__jasCartDelegated) {
        document.__jasCartDelegated = true;

        document.addEventListener('click', async (e) => {
            // Check Add to Cart button
            const addBtn = e.target.closest('[data-add-to-cart]');
            if (addBtn) {
                e.preventDefault();
                e.stopPropagation();

                const variantId = parseInt(addBtn.getAttribute('data-variant-id'), 10) || 0;
                const productId = parseInt(addBtn.getAttribute('data-product-id'), 10) || 0;

                // Find quantity
                let qty = 1;
                const qtyTarget = addBtn.getAttribute('data-quantity-target');
                if (qtyTarget) {
                    const input = document.querySelector(qtyTarget);
                    if (input) qty = parseInt(input.value, 10) || 1;
                } else {
                    const container = addBtn.closest('form, div, article');
                    const input = container?.querySelector('input[name="Quantity"], input[name="quantity"]');
                    if (input) qty = parseInt(input.value, 10) || 1;
                }

                // Show button busy state
                const origHtml = addBtn.innerHTML;
                addBtn.disabled = true;
                addBtn.classList.add('opacity-75');

                try {
                    await addToCart(variantId, qty, productId);
                } finally {
                    addBtn.disabled = false;
                    addBtn.classList.remove('opacity-75');
                    addBtn.innerHTML = origHtml;
                }
                return;
            }

            // Check Buy Now button
            const buyBtn = e.target.closest('[data-buy-now]');
            if (buyBtn) {
                e.preventDefault();
                e.stopPropagation();

                const variantId = parseInt(buyBtn.getAttribute('data-variant-id'), 10) || 0;
                const productId = parseInt(buyBtn.getAttribute('data-product-id'), 10) || 0;

                let qty = 1;
                const qtyTarget = buyBtn.getAttribute('data-quantity-target');
                if (qtyTarget) {
                    const input = document.querySelector(qtyTarget);
                    if (input) qty = parseInt(input.value, 10) || 1;
                } else {
                    const container = buyBtn.closest('form, div, article');
                    const input = container?.querySelector('input[name="Quantity"], input[name="quantity"]');
                    if (input) qty = parseInt(input.value, 10) || 1;
                }

                const origHtml = buyBtn.innerHTML;
                buyBtn.disabled = true;
                buyBtn.innerHTML = '<span>Processing…</span>';

                try {
                    await buyNow(variantId, qty, productId);
                } catch {
                    buyBtn.disabled = false;
                    buyBtn.innerHTML = origHtml;
                }
                return;
            }
        });
    }
}

// Expose callbacks globally on window for Blazor Interop and inline handlers
if (typeof window !== 'undefined') {
    window.jasGetCartToken = getCartToken;
    window.jasUpdateCartBadge = updateCartBadges;
    window.jasUpdateCartCount = updateCartBadges;
    window.jasRefreshCart = refreshCartBadge;
    window.jasAddToCart = (variantId, qty, prodId) => addToCart(variantId, qty, prodId);
    window.jasBuyNow = (variantId, qty, prodId) => buyNow(variantId, qty, prodId);
    window.jasSyncCartBadges = syncCartBadges;
}
