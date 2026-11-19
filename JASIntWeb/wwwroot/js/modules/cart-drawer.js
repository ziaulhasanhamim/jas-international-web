/**
 * JAS International - Animated Cart Slide-Over Drawer (ES2024+)
 * High-performance, 60fps sliding drawer modal with dynamic rendering,
 * optimistic steppers, and seamless checkout transition.
 */

import { on } from '../core/events.js';
import { openOverlay, closeOverlay, toggleOverlay } from '../core/overlay.js';
import { fetchCart } from './cart.js';

const DRAWER_ID = 'jas-cart-drawer';
let isDrawerInitialized = false;

function formatCurrency(amount) {
    return '৳' + Number(amount || 0).toLocaleString('en-US');
}

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

/**
 * Renders the cart drawer content based on the provided or fetched cart data.
 * @param {object} [cart=null]
 */
export function renderCartDrawer(cart = null) {
    const itemsContainer = document.getElementById('jas-cart-drawer-items');
    const subtotalEl = document.getElementById('jas-cart-drawer-subtotal');
    const deliveryTextEl = document.getElementById('jas-cart-drawer-delivery-text');
    const deliveryPctEl = document.getElementById('jas-cart-drawer-delivery-pct');
    const deliveryBarEl = document.getElementById('jas-cart-drawer-delivery-bar');
    const footerEl = document.getElementById('jas-cart-drawer-footer');

    if (!itemsContainer) return;

    if (!cart || !Array.isArray(cart.items) || cart.items.length === 0) {
        // Empty State
        itemsContainer.innerHTML = `
            <div class="py-16 text-center">
                <div class="mx-auto flex size-14 items-center justify-center rounded-2xl bg-blue-50 text-blue-600 mb-3">
                    <svg class="size-7" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z" />
                    </svg>
                </div>
                <p class="font-bold text-slate-800 text-sm">Your cart is currently empty</p>
                <p class="text-xs text-slate-500 mt-1">Browse our store and add authentic gadgets to get started.</p>
                <a href="/products" data-overlay-close class="btn-primary inline-flex mt-5 text-xs py-2 px-4 shadow-xs">
                    Start Shopping
                </a>
            </div>
        `;

        if (subtotalEl) subtotalEl.textContent = '৳0';
        if (deliveryPctEl) deliveryPctEl.textContent = '0%';
        if (deliveryBarEl) deliveryBarEl.style.width = '0%';
        if (deliveryTextEl) deliveryTextEl.innerHTML = 'Add <strong class="text-blue-600 font-bold">৳5,000</strong> for Free Delivery';
        if (footerEl) footerEl.classList.add('hidden');
        return;
    }

    if (footerEl) footerEl.classList.remove('hidden');

    // Free delivery progress calculation
    const freeDeliveryThreshold = 5000;
    const currentSubtotal = cart.subtotal || 0;
    const progress = Math.min(100, Math.round((currentSubtotal / freeDeliveryThreshold) * 100));
    const remaining = Math.max(0, freeDeliveryThreshold - currentSubtotal);

    if (deliveryPctEl) deliveryPctEl.textContent = `${progress}%`;
    if (deliveryBarEl) deliveryBarEl.style.width = `${progress}%`;
    if (deliveryTextEl) {
        if (remaining > 0) {
            deliveryTextEl.innerHTML = `Add <strong class="text-blue-600 font-bold">${formatCurrency(remaining)}</strong> for Free Delivery`;
        } else {
            deliveryTextEl.innerHTML = '<span class="text-emerald-700 font-bold">🎉 Free Delivery Unlocked!</span>';
        }
    }

    if (subtotalEl) subtotalEl.textContent = formatCurrency(currentSubtotal);

    // Items list
    const itemsHtml = cart.items.map(item => `
        <div data-cart-item data-variant-id="${item.variantId}" class="flex gap-3.5 rounded-2xl border border-slate-100 bg-white p-3 shadow-2xs hover:border-slate-200 transition">
            <img
                src="${escapeHtml(item.image || '/jas-logo.png')}"
                alt="${escapeHtml(item.name)}"
                class="size-16 shrink-0 rounded-xl object-contain bg-slate-50 p-1 border border-slate-100"
                width="64"
                height="64"
                loading="lazy"
            />
            <div class="flex-1 min-w-0 flex flex-col justify-between">
                <div class="flex items-start justify-between gap-2">
                    <div>
                        <a href="/product/${encodeURIComponent(item.slug || '')}" data-overlay-close class="font-semibold text-slate-900 text-xs hover:text-blue-600 transition line-clamp-1">
                            ${escapeHtml(item.name)}
                        </a>
                        ${item.variant ? `<p class="text-[11px] text-slate-500 font-medium">${escapeHtml(item.variant)}</p>` : ''}
                    </div>
                    <button
                        type="button"
                        data-cart-remove
                        data-variant-id="${item.variantId}"
                        class="text-slate-400 hover:text-rose-600 transition p-1 cursor-pointer"
                        title="Remove item"
                        aria-label="Remove item"
                    >
                        <svg class="size-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                        </svg>
                    </button>
                </div>
                <div class="flex items-center justify-between mt-2 pt-2 border-t border-slate-100">
                    <strong class="text-xs font-bold text-slate-900 tabular-nums">${formatCurrency(item.lineTotal || (item.quantity * item.price))}</strong>
                    
                    <!-- Stepper -->
                    <div class="flex items-center gap-1 rounded-lg border border-slate-200 bg-slate-50 p-0.5 shadow-2xs">
                        <button
                            type="button"
                            data-cart-qty="dec"
                            data-variant-id="${item.variantId}"
                            data-current-qty="${item.quantity}"
                            class="size-6 rounded bg-white text-slate-700 hover:bg-slate-100 text-xs font-bold flex items-center justify-center transition active:scale-90 cursor-pointer"
                            aria-label="Decrease quantity"
                        >
                            −
                        </button>
                        <span data-cart-qty-val="${item.variantId}" class="w-6 text-center text-xs font-bold text-slate-800 tabular-nums">
                            ${item.quantity}
                        </span>
                        <button
                            type="button"
                            data-cart-qty="inc"
                            data-variant-id="${item.variantId}"
                            data-current-qty="${item.quantity}"
                            class="size-6 rounded bg-white text-slate-700 hover:bg-slate-100 text-xs font-bold flex items-center justify-center transition active:scale-90 cursor-pointer"
                            aria-label="Increase quantity"
                        >
                            +
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `).join('');

    itemsContainer.innerHTML = itemsHtml;
}

/**
 * Opens the animated cart slide-over modal.
 */
export async function openCartDrawer() {
    openOverlay(DRAWER_ID);
    const cart = await fetchCart();
    if (cart) renderCartDrawer(cart);
}

/**
 * Closes the animated cart slide-over modal.
 */
export function closeCartDrawer() {
    closeOverlay(DRAWER_ID);
}

/**
 * Toggles the animated cart slide-over modal.
 */
export function toggleCartDrawer() {
    toggleOverlay(DRAWER_ID);
}

/**
 * Subscribes the cart drawer to application events.
 */
export function initCartDrawer() {
    if (isDrawerInitialized) return;
    isDrawerInitialized = true;

    // Open drawer automatically when an item is added to cart
    on('cart:item-added', (detail) => {
        openOverlay(DRAWER_ID);
        if (detail?.cart) {
            renderCartDrawer(detail.cart);
        } else {
            fetchCart().then(renderCartDrawer);
        }
    });

    // Re-render drawer whenever cart updates
    on('cart:updated', (cart) => {
        if (cart) renderCartDrawer(cart);
    });

    // Global toggle listener
    on('cart:toggle-drawer', () => {
        toggleOverlay(DRAWER_ID);
    });
}
