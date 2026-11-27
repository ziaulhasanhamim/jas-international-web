/**
 * JAS International - Functional Cart Module (ES2024+)
 * Optimistic UI cart controller routed through core/api.js and core/events.js,
 * with declarative data-* delegation and Blazor JS interop aliases.
 */

import { emit } from '../core/events.js';
import { request } from '../core/api.js';

let currentCartState = null;

export function getCurrentCart() {
    return currentCartState;
}

export function getCartToken() {
    const match = typeof document !== 'undefined' && document.cookie ? document.cookie.match(/(?:^|;\s*)jas_cart_id=([^;]*)/) : null;
    return match ? decodeURIComponent(match[1]) : null;
}

export function setCartToken(token) {
    if (!token || typeof document === 'undefined') return;
    document.cookie = `jas_cart_id=${encodeURIComponent(token)}; path=/; max-age=2592000; SameSite=Lax`;
}

export function clearCartToken() {
    if (typeof document !== 'undefined') {
        document.cookie = 'jas_cart_id=; path=/; max-age=0; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    }
    currentCartState = null;
    updateCartBadges(0);
    emit('cart:cleared');
}

export function updateCartBadges(count) {
    const safeCount = Math.max(0, parseInt(count, 10) || 0);
    if (typeof document === 'undefined') return;
    const badges = document.querySelectorAll('[data-cart-count]');
    badges.forEach(badge => {
        badge.textContent = safeCount;
        if (typeof badge.setAttribute === 'function') {
            badge.setAttribute('data-cart-count', safeCount);
        }

        badge.classList.remove('animate-pulse');
        if ('offsetWidth' in badge) void badge.offsetWidth;
        badge.classList.add('animate-pulse');
    });
}

export function showCartToast(message, type = 'success', action = { text: 'View Cart →', href: '/cart' }) {
    if (typeof document === 'undefined') return;
    let container = document.getElementById('jas-toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'jas-toast-container';
        container.className = 'fixed top-20 right-4 sm:right-6 z-[120] flex flex-col gap-2 max-w-sm w-full pointer-events-none';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = `pointer-events-auto flex items-center justify-between gap-3 p-3.5 sm:p-4 rounded-2xl shadow-xl text-xs sm:text-sm font-semibold transition-all duration-300 transform translate-y-2 opacity-0 ${
        type === 'error'
            ? 'bg-rose-600 text-white shadow-rose-900/20'
            : 'bg-slate-900/95 text-white backdrop-blur-md shadow-slate-900/30'
    }`;

    const label = document.createElement('div');
    label.className = 'flex items-center gap-2.5 min-w-0';
    const icon = document.createElement('span');
    icon.className = 'text-base shrink-0';
    icon.textContent = type === 'error' ? '⚠️' : '🛒';
    const text = document.createElement('span');
    text.className = 'truncate';
    text.textContent = message;
    label.append(icon, text);
    toast.append(label);

    if (action) {
        const link = document.createElement('a');
        link.href = action.href;
        link.textContent = action.text;
        link.className = 'ml-auto inline-flex items-center gap-1 rounded-xl bg-blue-600 hover:bg-blue-500 px-3 py-1 text-xs font-bold text-white transition active:scale-95 shrink-0';
        if (typeof link.setAttribute === 'function') {
            link.setAttribute('data-enhance', 'true');
        }
        toast.append(link);
    }

    container.appendChild(toast);

    if (typeof requestAnimationFrame === 'function') {
        requestAnimationFrame(() => {
            toast.classList.remove('translate-y-2', 'opacity-0');
            toast.classList.add('translate-y-0', 'opacity-100');
        });
    }

    if (typeof setTimeout === 'function') {
        setTimeout(() => {
            toast.classList.add('opacity-0', '-translate-y-2');
            setTimeout(() => toast.remove(), 300);
        }, 3500);
    }
}

export async function fetchCart() {
    const res = await request('/api/cart');
    if (res.ok && res.data) {
        currentCartState = res.data;
        if (res.data?.token) setCartToken(res.data.token);
        updateCartBadges(res.data?.itemCount ?? 0);
        emit('cart:updated', res.data);
        return res.data;
    }
    return null;
}

export async function refreshCartBadge() {
    const res = await request('/api/cart/count');
    if (res.ok && res.data) {
        const count = typeof res.data?.count === 'number' ? res.data.count : 0;
        updateCartBadges(count);
        return count;
    }
    return 0;
}

export async function addToCart(variantId, quantity = 1, productId = 0, { showToast = true } = {}) {
    const payload = {
        variantId: Number(variantId) || 0,
        quantity: Math.max(1, Number(quantity) || 1),
        productId: Number(productId) || 0
    };

    const res = await request('/api/cart', {
        method: 'POST',
        body: payload,
        includeCsrf: true,
        csrfUrl: '/api/cart/security'
    });

    if (res.ok && res.data) {
        const cart = res.data;
        currentCartState = cart;
        if (cart?.token) setCartToken(cart.token);

        const totalCount = cart?.itemCount ?? (Array.isArray(cart?.items) ? cart.items.reduce((s, x) => s + (x.quantity || 0), 0) : 1);
        updateCartBadges(totalCount);

        emit('cart:updated', cart);

        try {
            const addedItem = Array.isArray(cart?.items) ? cart.items.find(i => i.variantId === Number(variantId)) : null;
            const sku = cart?.addedSku || addedItem?.sku || (variantId ? `V${variantId}` : null);
            const eventId = cart?.metaEventId || res.headers?.get('X-Meta-Event-Id') || `atc_${variantId}_${Date.now()}`;
            const price = addedItem?.price || 0;
            const name = addedItem?.name || 'Product';
            const qty = Math.max(1, Number(quantity) || 1);

            if (sku && typeof window.jasTrackMeta === 'function') {
                window.jasTrackMeta('AddToCart', {
                    content_ids: [String(sku).trim().toUpperCase()],
                    content_name: name,
                    content_type: 'product',
                    value: price * qty,
                    currency: 'BDT'
                }, eventId);
            }
        } catch (trackErr) {
            console.debug('[Cart] Meta AddToCart tracking error:', trackErr);
        }

        if (showToast) {
            showCartToast('Added to your shopping cart!', 'success');
        }
        return { success: true, cart };
    }

    const message = res.status === 0
        ? 'Network error. Please try again.'
        : (res.data?.message || res.error || 'Could not add item to cart.');
    showCartToast(message, 'error', null);
    emit('cart:error', { message });
    return { success: false, message };
}

export async function setQuantity(variantId, quantity) {
    const targetQty = Math.max(0, Math.min(99, Number(quantity) || 0));
    const previousState = currentCartState;

    if (currentCartState && Array.isArray(currentCartState.items)) {
        const updatedItems = targetQty === 0
            ? currentCartState.items.filter(i => i.variantId !== variantId)
            : currentCartState.items.map(i => i.variantId === variantId
                ? { ...i, quantity: targetQty, lineTotal: targetQty * (i.unitPrice || i.price || 0) }
                : i
            );

        const newSubtotal = updatedItems.reduce((acc, i) => acc + (i.lineTotal || (i.quantity * (i.unitPrice || i.price || 0))), 0);
        const newItemCount = updatedItems.reduce((acc, i) => acc + i.quantity, 0);

        const optimisticCart = {
            ...currentCartState,
            items: updatedItems,
            subtotal: newSubtotal,
            itemCount: newItemCount
        };

        currentCartState = optimisticCart;
        updateCartBadges(newItemCount);
        emit('cart:updated', optimisticCart);
    }

    const res = await request('/api/cart', {
        method: 'PUT',
        body: { variantId, quantity: targetQty },
        includeCsrf: true,
        csrfUrl: '/api/cart/security'
    });

    if (res.ok && res.data) {
        currentCartState = res.data;
        if (res.data?.token) setCartToken(res.data.token);
        updateCartBadges(res.data?.itemCount ?? 0);
        emit('cart:updated', res.data);
        return { success: true, cart: res.data };
    }

    if (previousState) {
        currentCartState = previousState;
        updateCartBadges(previousState.itemCount ?? 0);
        emit('cart:updated', previousState);
    }

    const message = res.status === 0
        ? 'Network error. Please try again.'
        : (res.data?.message || res.error || 'Could not update item quantity.');
    showCartToast(message, 'error', null);
    emit('cart:error', { message });
    return { success: false, message };
}

export function removeFromCart(variantId) {
    return setQuantity(variantId, 0);
}

export async function buyNow(variantId, quantity = 1, productId = 0) {
    const result = await addToCart(variantId, quantity, productId, { showToast: false });
    if (result.success) {
        if (typeof window.Blazor?.navigateTo === 'function') {
            window.Blazor.navigateTo('/cart', { forceLoad: false });
        } else {
            window.location.assign('/cart');
        }
    }
}

export function syncCartBadges() {
    refreshCartBadge();
}

function resolveButtonQuantity(button) {
    const qtyTarget = button.getAttribute('data-quantity-target');
    if (qtyTarget) {
        const input = document.querySelector(qtyTarget);
        if (input) return parseInt(input.value, 10) || 1;
    } else {
        const container = button.closest('form, div, article');
        const input = container?.querySelector('input[name="Quantity"], input[name="quantity"]');
        if (input) return parseInt(input.value, 10) || 1;
    }
    return 1;
}

export function initCartDelegation() {
    if (typeof document === 'undefined' || document.__jasCartDelegated) return;
    document.__jasCartDelegated = true;

    document.addEventListener('click', async (e) => {
        const addBtn = e.target.closest('[data-cart-add], [data-add-to-cart]');
        if (addBtn) {
            e.preventDefault();
            e.stopPropagation();

            const variantId = parseInt(addBtn.getAttribute('data-variant-id'), 10) || 0;
            const productId = parseInt(addBtn.getAttribute('data-product-id'), 10) || 0;
            const qty = resolveButtonQuantity(addBtn);

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

        const buyBtn = e.target.closest('[data-buy-now]');
        if (buyBtn) {
            const form = buyBtn.closest ? buyBtn.closest('form') : null;
            if (form && typeof form.getAttribute === 'function' && form.getAttribute('action')?.includes('/cart/buy-now')) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();

            const variantId = parseInt(buyBtn.getAttribute('data-variant-id'), 10) || 0;
            const productId = parseInt(buyBtn.getAttribute('data-product-id'), 10) || 0;
            const qty = resolveButtonQuantity(buyBtn);

            const origHtml = buyBtn.innerHTML;
            buyBtn.disabled = true;
            buyBtn.innerHTML = '<span>Processing…</span>';

            try {
                await buyNow(variantId, qty, productId);
            } finally {
                buyBtn.disabled = false;
                buyBtn.innerHTML = origHtml;
            }
            return;
        }

        const qtyBtn = e.target.closest('[data-cart-qty]');
        if (qtyBtn) {
            e.preventDefault();
            const action = qtyBtn.getAttribute('data-cart-qty');
            const variantId = parseInt(qtyBtn.getAttribute('data-variant-id'), 10);
            if (!variantId) return;

            let currentQty = parseInt(qtyBtn.getAttribute('data-current-qty'), 10);
            if (isNaN(currentQty)) {
                const valEl = qtyBtn.parentElement?.querySelector(`[data-cart-qty-val="${variantId}"]`);
                if (valEl) currentQty = parseInt(valEl.textContent, 10);
            }

            currentQty = currentQty || 1;
            const nextQty = action === 'inc' ? currentQty + 1 : currentQty - 1;
            await setQuantity(variantId, nextQty);
            return;
        }

        const removeBtn = e.target.closest('[data-cart-remove]');
        if (removeBtn) {
            e.preventDefault();
            const variantId = parseInt(removeBtn.getAttribute('data-variant-id'), 10);
            if (variantId) {
                await removeFromCart(variantId);
            }
        }
    });
}

export function initCart() {
    refreshCartBadge();
    initCartDelegation();
}

// Global window exposure for Blazor interop and existing callers
if (typeof window !== 'undefined') {
    window.jasGetCartToken = getCartToken;
    window.jasClearCartCookie = clearCartToken;
    window.jasUpdateCartBadge = updateCartBadges;
    window.jasUpdateCartCount = updateCartBadges;
    window.jasRefreshCart = refreshCartBadge;
    window.jasAddToCart = (variantId, qty, prodId) => addToCart(variantId, qty, prodId);
    window.jasBuyNow = (variantId, qty, prodId) => buyNow(variantId, qty, prodId);
    window.jasSyncCartBadges = syncCartBadges;
    window.jasGetCurrentCart = getCurrentCart;
    window.jasSetQuantity = setQuantity;
    window.jasRemoveFromCart = removeFromCart;
}
