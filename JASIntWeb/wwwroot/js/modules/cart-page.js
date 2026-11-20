/**
 * JAS International - Full Cart Page Controller (ES2024+)
 * Manages in-page DOM synchronization, optimistic stepper reflection,
 * and empty state transitions for the static SSR /cart route.
 */

import { on } from '../core/events.js';

function formatCurrency(amount) {
    return '৳' + Number(amount || 0).toLocaleString('en-US');
}

/**
 * Updates DOM elements on the /cart page when cart:updated fires.
 * @param {object} cart
 */
function handleCartUpdated(cart) {
    const pageContainer = document.getElementById('jas-cart-page');
    if (!pageContainer) return; // Not on /cart page

    if (!cart || !Array.isArray(cart.items) || cart.items.length === 0) {
        // Transition to empty state
        pageContainer.innerHTML = `
            <div class="rounded-3xl border border-slate-200 bg-white p-12 text-center animate-fadeIn">
                <div class="mx-auto flex size-16 items-center justify-center rounded-2xl bg-blue-50 text-blue-600 mb-4">
                    <svg class="size-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z" />
                    </svg>
                </div>
                <h2 class="text-xl font-bold text-slate-900">Your cart is empty</h2>
                <p class="text-sm text-slate-500 mt-2 mb-6">Looks like you haven't added any authentic gadgets to your cart yet.</p>
                <a href="/products" class="btn-primary shadow-xs">Explore Products</a>
            </div>
        `;
        return;
    }

    // 1. Update subtotal & total
    const subtotalEls = document.querySelectorAll('[data-cart-subtotal]');
    subtotalEls.forEach(el => el.textContent = formatCurrency(cart.subtotal));

    const totalEls = document.querySelectorAll('[data-cart-total]');
    totalEls.forEach(el => el.textContent = formatCurrency(cart.subtotal));

    // 2. Update or remove item rows
    const presentVariantIds = new Set(cart.items.map(i => i.variantId));
    const existingCards = document.querySelectorAll('[data-cart-page-item]');

    existingCards.forEach(card => {
        const variantId = parseInt(card.getAttribute('data-variant-id'), 10);
        if (!presentVariantIds.has(variantId)) {
            // Smoothly remove item card from DOM
            card.classList.add('opacity-0', 'scale-95');
            setTimeout(() => card.remove(), 250);
        } else {
            const item = cart.items.find(i => i.variantId === variantId);
            if (item) {
                const qtyVal = card.querySelector(`[data-cart-qty-val="${variantId}"]`);
                if (qtyVal) qtyVal.textContent = item.quantity;

                const lineTotal = card.querySelector(`[data-cart-line-total="${variantId}"]`);
                if (lineTotal) lineTotal.textContent = formatCurrency(item.lineTotal || (item.quantity * item.price));

                // Update current-qty attributes on buttons
                const decBtn = card.querySelector('[data-cart-qty="dec"]');
                if (decBtn) decBtn.setAttribute('data-current-qty', item.quantity);
                const incBtn = card.querySelector('[data-cart-qty="inc"]');
                if (incBtn) incBtn.setAttribute('data-current-qty', item.quantity);
            }
        }
    });
}

/**
 * Initializes the full cart page controller.
 */
export function initCartPage() {
    on('cart:updated', handleCartUpdated);
}
