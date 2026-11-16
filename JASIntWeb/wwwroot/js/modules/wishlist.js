/**
 * JAS International - Wishlist Module (ES2024+)
 * Manages client-side wishlist storage in localStorage, synchronizes header badges,
 * updates heart toggles across all catalog cards, and dynamically renders the /wishlist page.
 */

const STORAGE_KEY = 'jas.wishlist.v1';

/**
 * Retrieves the current wishlist items from localStorage.
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
 * Persists the wishlist items array to localStorage.
 * @param {Array} items
 */
export function saveWishlistItems(items) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
    } catch (e) {
        console.warn('Unable to persist wishlist to localStorage', e);
    }
}

/**
 * Checks if a specific product ID is currently in the wishlist.
 * @param {number} productId
 * @returns {boolean}
 */
export function isInWishlist(productId) {
    const items = getWishlistItems();
    return items.some(item => item.id === productId);
}

/**
 * Toggles a product in the wishlist (adds if absent, removes if present).
 * @param {{id: number, name: string, slug: string, image: string, price: number}} product
 * @returns {boolean} True if item was added, false if removed.
 */
export function toggleWishlistItem(product) {
    if (!product || !product.id) return false;
    const items = getWishlistItems();
    const index = items.findIndex(item => item.id === product.id);
    let added = false;

    if (index >= 0) {
        items.splice(index, 1);
        added = false;
    } else {
        items.unshift({
            id: product.id,
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
    renderWishlistPage();
    return added;
}

/**
 * Removes a product by ID from the wishlist.
 * @param {number} productId
 */
export function removeFromWishlist(productId) {
    const items = getWishlistItems().filter(item => item.id !== productId);
    saveWishlistItems(items);
    updateWishlistBadges();
    updateWishlistButtons();
    renderWishlistPage();
}

/**
 * Clears all items from the wishlist.
 */
export function clearWishlist() {
    saveWishlistItems([]);
    updateWishlistBadges();
    updateWishlistButtons();
    renderWishlistPage();
}

/**
 * Updates all wishlist badge count indicators in the header and navigation.
 */
export function updateWishlistBadges() {
    const items = getWishlistItems();
    const count = items.length;
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
 * Updates visual active states for all wishlist heart buttons currently in the DOM.
 */
export function updateWishlistButtons() {
    const items = getWishlistItems();
    const wishlistedIds = new Set(items.map(item => item.id));

    document.querySelectorAll('[data-wishlist-toggle]').forEach(button => {
        const id = parseInt(button.getAttribute('data-wishlist-toggle'), 10);
        const active = wishlistedIds.has(id);

        if (active) {
            button.classList.add('is-wishlisted');
        } else {
            button.classList.remove('is-wishlisted');
        }

        const svg = button.querySelector('svg');
        if (svg) {
            if (active) {
                svg.classList.add('text-rose-500', 'fill-rose-500');
                svg.classList.remove('text-slate-400');
            } else {
                svg.classList.remove('text-rose-500', 'fill-rose-500');
                svg.classList.add('text-slate-400');
            }
        }
    });
}

/**
 * Dynamically renders the /wishlist container if present on the active page.
 */
export function renderWishlistPage() {
    const container = document.getElementById('wishlist-container');
    if (!container) return;

    const items = getWishlistItems();

    if (items.length === 0) {
        container.innerHTML = `
            <div class="mx-auto max-w-md py-16 text-center animate-fadeIn">
                <div class="mx-auto flex size-20 items-center justify-center rounded-3xl bg-slate-100 text-slate-400 mb-5">
                    <svg class="size-10 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z" />
                    </svg>
                </div>
                <h2 class="text-xl font-bold text-slate-900">Your wishlist is currently empty</h2>
                <p class="mt-2 text-sm text-slate-500 leading-relaxed">
                    Explore our catalog of authentic smartwatches, audio gear, chargers, and gadget accessories. Click the heart icon on any product to save it here.
                </p>
                <div class="mt-6 flex justify-center">
                    <a href="/products" class="btn-primary shadow-xs">Explore Products</a>
                </div>
            </div>
        `;
        return;
    }

    const cardsHtml = items.map(item => `
        <article class="product-card group relative flex flex-col justify-between h-full bg-white rounded-2xl border border-slate-200/80 shadow-xs hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
            <button
                type="button"
                data-wishlist-remove="${item.id}"
                class="absolute top-2.5 right-2.5 z-20 flex size-8 items-center justify-center rounded-full bg-white/90 backdrop-blur-sm border border-slate-200/90 text-rose-500 hover:scale-110 shadow-xs transition cursor-pointer"
                title="Remove from wishlist"
                aria-label="Remove ${escapeHtml(item.name)} from wishlist"
            >
                <svg class="size-4 fill-rose-500 text-rose-500" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2">
                    <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                </svg>
            </button>
            <a href="/product/${encodeURIComponent(item.slug)}" class="block relative aspect-square p-5 bg-white overflow-hidden">
                <img
                    src="${escapeHtml(item.image)}"
                    alt="${escapeHtml(item.name)}"
                    loading="lazy"
                    class="h-full w-full object-contain transition-transform duration-500 group-hover:scale-105"
                />
            </a>
            <div class="px-4 pb-4 pt-1 flex flex-col flex-1 text-center">
                <a href="/product/${encodeURIComponent(item.slug)}" class="block font-bold text-slate-900 text-sm hover:text-orange-600 transition line-clamp-2 min-h-[2.5rem] leading-snug">
                    ${escapeHtml(item.name)}
                </a>
                <div class="flex items-baseline justify-center gap-1.5 my-2">
                    <span class="text-base sm:text-lg font-black text-[#ea580c]">Tk. ${Number(item.price).toLocaleString()}</span>
                </div>
                <div class="mt-auto pt-2 border-t border-slate-100">
                    <form method="post" action="/cart/buy-now" data-enhance class="w-full">
                        <input type="hidden" name="productId" value="${item.id}" />
                        <button
                            type="button"
                            data-buy-now
                            data-product-id="${item.id}"
                            class="btn-primary w-full py-2 text-xs font-bold transition flex items-center justify-center gap-1.5 shadow-xs hover:shadow-md cursor-pointer"
                        >
                            <span>⚡ Buy Now</span>
                        </button>
                    </form>
                </div>
            </div>
        </article>
    `).join('');

    container.innerHTML = `
        <div class="mb-6 flex items-center justify-between">
            <span class="rounded-full bg-slate-100 px-3 py-1 text-xs font-bold text-slate-700">
                ${items.length} ${items.length === 1 ? 'Item' : 'Items'}
            </span>
            <button
                type="button"
                data-wishlist-clear
                class="rounded-xl border border-slate-200 px-3.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-rose-50 hover:text-rose-600 hover:border-rose-200 transition cursor-pointer"
            >
                Clear All
            </button>
        </div>
        <div class="grid grid-cols-2 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 animate-fadeIn">
            ${cardsHtml}
        </div>
    `;
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

let isInitialized = false;

/**
 * Initializes wishlist event listeners and initial DOM synchronization.
 */
export function initWishlist() {
    updateWishlistBadges();
    updateWishlistButtons();
    renderWishlistPage();

    if (isInitialized) return;
    isInitialized = true;

    // Document-level event delegation for wishlist toggle buttons
    document.addEventListener('click', event => {
        const toggleBtn = event.target.closest('[data-wishlist-toggle]');
        if (toggleBtn) {
            event.preventDefault();
            event.stopPropagation();
            const id = parseInt(toggleBtn.getAttribute('data-wishlist-toggle'), 10);
            if (!id) return;
            const name = toggleBtn.getAttribute('data-product-name') || '';
            const slug = toggleBtn.getAttribute('data-product-slug') || '';
            const image = toggleBtn.getAttribute('data-product-image') || '';
            const price = parseFloat(toggleBtn.getAttribute('data-product-price') || '0');
            toggleWishlistItem({ id, name, slug, image, price });
            return;
        }

        const removeBtn = event.target.closest('[data-wishlist-remove]');
        if (removeBtn) {
            event.preventDefault();
            event.stopPropagation();
            const id = parseInt(removeBtn.getAttribute('data-wishlist-remove'), 10);
            if (id) removeFromWishlist(id);
            return;
        }

        const clearBtn = event.target.closest('[data-wishlist-clear]');
        if (clearBtn) {
            event.preventDefault();
            event.stopPropagation();
            if (confirm('Are you sure you want to clear your entire wishlist?')) {
                clearWishlist();
            }
        }
    });

    // Cross-tab synchronization
    window.addEventListener('storage', event => {
        if (event.key === STORAGE_KEY) {
            updateWishlistBadges();
            updateWishlistButtons();
            renderWishlistPage();
        }
    });
}
