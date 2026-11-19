/**
 * JAS International - Functional Cart Module (ES2024+)
 * High-performance, functional cart controller with 0ms optimistic UI updates,
 * declarative data-* delegation, and native CustomEvents.
 */

let currentCartState = null;

/**
 * Dispatches a custom event from document.
 * @param {string} eventName
 * @param {any} [detail=null]
 */
export function emit(eventName, detail = null) {
    if (typeof document !== 'undefined' && typeof CustomEvent === 'function') {
        document.dispatchEvent(new CustomEvent(eventName, { bubbles: true, cancelable: true, detail }));
    }
}

/**
 * Reads the secure cart GUID token from the client cookie.
 * @returns {string|null}
 */
export function getCartToken() {
    const match = typeof document !== 'undefined' && document.cookie ? document.cookie.match(/(?:^|;\s*)jas_cart_id=([^;]*)/) : null;
    return match ? decodeURIComponent(match[1]) : null;
}

/**
 * Sets the cart GUID token in the browser cookie.
 * @param {string} token
 */
export function setCartToken(token) {
    if (!token || typeof document === 'undefined') return;
    document.cookie = `jas_cart_id=${encodeURIComponent(token)}; path=/; max-age=2592000; SameSite=Lax`;
}

/**
 * Clears the cart cookie and resets cart badges.
 */
export function clearCartToken() {
    if (typeof document !== 'undefined') {
        document.cookie = 'jas_cart_id=; path=/; max-age=0; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    }
    currentCartState = null;
    updateCartBadges(0);
    emit('cart:cleared');
}

/**
 * Updates all cart badge elements across the page with the latest count.
 * @param {number} count
 */
export function updateCartBadges(count) {
    const safeCount = Math.max(0, parseInt(count, 10) || 0);
    if (typeof document === 'undefined') return;
    const badges = document.querySelectorAll('[data-cart-count]');
    badges.forEach(badge => {
        badge.textContent = safeCount;
        if (typeof badge.setAttribute === 'function') {
            badge.setAttribute('data-cart-count', safeCount);
        }

        // Add micro-pulse animation
        badge.classList.remove('animate-pulse');
        if ('offsetWidth' in badge) void badge.offsetWidth; // Force reflow
        badge.classList.add('animate-pulse');
    });
}

/**
 * Displays a non-intrusive floating toast notification.
 * @param {string} message
 * @param {'success'|'error'|'info'} [type='success']
 * @param {{ text: string, href: string }|null} [action=null]
 */
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
            link.setAttribute('data-cart-toggle', 'true');
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

/**
 * Fetches the current cart from the backend API.
 * @returns {Promise<object|null>}
 */
export async function fetchCart() {
    try {
        const response = await fetch('/api/cart', {
            method: 'GET',
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin',
            cache: 'no-store'
        });
        if (response.ok) {
            const data = await response.json();
            currentCartState = data;
            if (data?.token) setCartToken(data.token);
            updateCartBadges(data?.itemCount ?? 0);
            emit('cart:updated', data);
            return data;
        }
    } catch (e) {
        console.warn('[JAS] Failed to fetch cart:', e);
    }
    return null;
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
            const count = typeof data?.count === 'number' ? data.count : 0;
            updateCartBadges(count);
            return count;
        }
    } catch (e) {
        console.warn('[JAS] Failed to refresh cart badge:', e);
    }
    return 0;
}

/**
 * Adds an item to the cart using the backend API.
 * @param {number} variantId
 * @param {number} [quantity=1]
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

        const securityResponse = await fetch('/api/cart/security', { credentials: 'same-origin', cache: 'no-store' });
        if (!securityResponse.ok) throw new Error('Could not secure the cart request.');
        const security = await securityResponse.json();

        const response = await fetch('/api/cart', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'X-CSRF-TOKEN': security.token
            },
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            const cart = await response.json();
            currentCartState = cart;
            if (cart?.token) setCartToken(cart.token);

            const totalCount = cart?.itemCount ?? (Array.isArray(cart?.items) ? cart.items.reduce((s, x) => s + (x.quantity || 0), 0) : 1);
            updateCartBadges(totalCount);

            emit('cart:updated', cart);
            emit('cart:item-added', { variantId, quantity: payload.quantity, cart });

            showCartToast('Added to your shopping cart!', 'success');
            return { success: true, cart };
        } else {
            let message = 'Could not add item to cart.';
            try {
                const data = await response.json();
                if (data?.message) message = data.message;
            } catch {}

            showCartToast(message, 'error', null);
            emit('cart:error', { message });
            return { success: false, message };
        }
    } catch (err) {
        console.error('[JAS] Add to cart error:', err);
        showCartToast('Network error. Please try again.', 'error', null);
        emit('cart:error', { message: 'Network error.' });
        return { success: false, message: 'Network error.' };
    }
}

/**
 * Sets the quantity of an item with optimistic updates and error rollback.
 * @param {number} variantId
 * @param {number} quantity
 * @returns {Promise<{success: boolean, cart?: object}>}
 */
export async function setQuantity(variantId, quantity) {
    const targetQty = Math.max(0, Math.min(99, Number(quantity) || 0));
    const previousState = currentCartState;

    // 1. Optimistic in-memory update
    if (currentCartState && Array.isArray(currentCartState.items)) {
        let updatedItems;
        if (targetQty === 0) {
            updatedItems = currentCartState.items.filter(i => i.variantId !== variantId);
        } else {
            updatedItems = currentCartState.items.map(i => i.variantId === variantId
                ? { ...i, quantity: targetQty, lineTotal: targetQty * (i.unitPrice || i.price || 0) }
                : i
            );
        }

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

    // 2. Dispatch to backend
    try {
        const securityResponse = await fetch('/api/cart/security', { credentials: 'same-origin', cache: 'no-store' });
        const security = securityResponse.ok ? await securityResponse.json() : null;

        const headers = {
            'Content-Type': 'application/json',
            'Accept': 'application/json'
        };
        if (security?.token) {
            headers['X-CSRF-TOKEN'] = security.token;
        }

        const response = await fetch('/api/cart', {
            method: 'PUT',
            headers,
            credentials: 'same-origin',
            body: JSON.stringify({ variantId, quantity: targetQty })
        });

        if (response.ok) {
            const data = await response.json();
            currentCartState = data;
            if (data?.token) setCartToken(data.token);
            updateCartBadges(data?.itemCount ?? 0);
            emit('cart:updated', data);
            return { success: true, cart: data };
        }

        // Rollback on non-200
        if (previousState) {
            currentCartState = previousState;
            updateCartBadges(previousState.itemCount ?? 0);
            emit('cart:updated', previousState);
        }

        let message = 'Could not update item quantity.';
        try {
            const err = await response.json();
            if (err?.message) message = err.message;
        } catch {}

        showCartToast(message, 'error', null);
        emit('cart:error', { message });
        return { success: false, message };
    } catch (err) {
        if (previousState) {
            currentCartState = previousState;
            updateCartBadges(previousState.itemCount ?? 0);
            emit('cart:updated', previousState);
        }
        showCartToast('Network error. Please try again.', 'error', null);
        emit('cart:error', { message: 'Network error.' });
        return { success: false, message: 'Network error.' };
    }
}

/**
 * Removes an item from the cart.
 * @param {number} variantId
 */
export function removeFromCart(variantId) {
    return setQuantity(variantId, 0);
}

/**
 * Adds item to cart and navigates directly to /cart or /checkout.
 * @param {number} variantId
 * @param {number} [quantity=1]
 * @param {number} [productId=0]
 */
export async function buyNow(variantId, quantity = 1, productId = 0) {
    const result = await addToCart(variantId, quantity, productId);
    if (result.success) {
        if (typeof window.Blazor?.navigateTo === 'function') {
            window.Blazor.navigateTo('/cart', { forceLoad: false });
        } else {
            window.location.assign('/cart');
        }
    }
}

/**
 * Synchronizes cart badges from in-page sync marker elements if present.
 */
export function syncCartBadges() {
    refreshCartBadge();
}

/**
 * Wires global delegated click listeners for all cart actions.
 */
export function initCartDelegation() {
    if (typeof document === 'undefined' || document.__jasCartDelegated) return;
    document.__jasCartDelegated = true;

    document.addEventListener('click', async (e) => {
        // 1. Add to Cart button
        const addBtn = e.target.closest('[data-cart-add], [data-add-to-cart]');
        if (addBtn) {
            e.preventDefault();
            e.stopPropagation();

            const variantId = parseInt(addBtn.getAttribute('data-variant-id'), 10) || 0;
            const productId = parseInt(addBtn.getAttribute('data-product-id'), 10) || 0;

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

        // 2. Buy Now button
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
            } finally {
                buyBtn.disabled = false;
                buyBtn.innerHTML = origHtml;
            }
            return;
        }

        // 3. Cart Stepper Buttons (+ / -)
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

        // 4. Cart Remove Button
        const removeBtn = e.target.closest('[data-cart-remove]');
        if (removeBtn) {
            e.preventDefault();
            const variantId = parseInt(removeBtn.getAttribute('data-variant-id'), 10);
            if (variantId) {
                await removeFromCart(variantId);
            }
            return;
        }

        // 5. Cart Toggle Button
        const toggleBtn = e.target.closest('[data-cart-toggle]');
        if (toggleBtn) {
            e.preventDefault();
            emit('cart:toggle-drawer');
            return;
        }
    });
}

/**
 * Initializes cart module listeners and refreshes badge count.
 */
export function initCart() {
    refreshCartBadge();
    initCartDelegation();
}

// Global window exposure
if (typeof window !== 'undefined') {
    window.jasGetCartToken = getCartToken;
    window.jasClearCartCookie = clearCartToken;
    window.jasUpdateCartBadge = updateCartBadges;
    window.jasUpdateCartCount = updateCartBadges;
    window.jasRefreshCart = refreshCartBadge;
    window.jasAddToCart = (variantId, qty, prodId) => addToCart(variantId, qty, prodId);
    window.jasBuyNow = (variantId, qty, prodId) => buyNow(variantId, qty, prodId);
    window.jasSyncCartBadges = syncCartBadges;
    window.jasSetQuantity = setQuantity;
    window.jasRemoveFromCart = removeFromCart;
}
