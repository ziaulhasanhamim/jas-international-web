// Lightweight Vanilla JS Cart & Wishlist Badge Synchronizer (Zero Blazor Interactivity Overhead)
(function() {
    'use strict';

    function getCartCount() {
        try {
            const raw = localStorage.getItem('jas.cart.v2');
            if (!raw) return 0;
            const data = JSON.parse(raw);
            if (!data || !Array.isArray(data.items)) return 0;
            return data.items.reduce((sum, item) => sum + (item.quantity || 0), 0);
        } catch {
            return 0;
        }
    }

    function getWishlistItems() {
        try {
            const raw = localStorage.getItem('jas.wishlist.v1');
            if (!raw) return [];
            const list = JSON.parse(raw);
            return Array.isArray(list) ? list : [];
        } catch {
            return [];
        }
    }

    function updateBadges() {
        const count = getCartCount();
        const badges = document.querySelectorAll('[data-cart-count]');
        badges.forEach(b => {
            b.textContent = count;
            b.setAttribute('data-cart-count', count);
        });

        const wishlist = getWishlistItems();
        const wCount = wishlist.length;
        const wBadges = document.querySelectorAll('[data-wishlist-count]');
        wBadges.forEach(b => {
            b.textContent = wCount;
            b.setAttribute('data-wishlist-count', wCount);
            if (wCount > 0) {
                b.classList.remove('hidden');
            } else {
                b.classList.add('hidden');
            }
        });

        // Update active state on wishlist buttons on cards
        const wishButtons = document.querySelectorAll('[data-wishlist-toggle]');
        wishButtons.forEach(btn => {
            const id = parseInt(btn.getAttribute('data-wishlist-toggle'), 10);
            const isWish = wishlist.some(x => x.id === id);
            const svg = btn.querySelector('svg');
            if (svg) {
                if (isWish) {
                    svg.classList.add('text-rose-500', 'fill-rose-500');
                    svg.classList.remove('text-slate-400');
                } else {
                    svg.classList.remove('text-rose-500', 'fill-rose-500');
                    svg.classList.add('text-slate-400');
                }
            }
        });
    }

    function initWishlistButtons() {
        document.querySelectorAll('[data-wishlist-toggle]').forEach(btn => {
            if (btn._wishlistBound) return;
            btn._wishlistBound = true;
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation();
                const id = parseInt(btn.getAttribute('data-wishlist-toggle'), 10);
                if (!id) return;
                const name = btn.getAttribute('data-product-name') || '';
                const slug = btn.getAttribute('data-product-slug') || '';
                const image = btn.getAttribute('data-product-image') || '';
                const price = parseFloat(btn.getAttribute('data-product-price') || '0');

                let items = getWishlistItems();
                const idx = items.findIndex(x => x.id === id);
                if (idx >= 0) {
                    items.splice(idx, 1);
                } else {
                    items.push({ id, name, slug, image, price, addedAt: new Date().toISOString() });
                }

                try {
                    localStorage.setItem('jas.wishlist.v1', JSON.stringify(items));
                } catch {}

                updateBadges();
            });
        });
    }

    // Expose global updater so Blazor or other scripts can notify when cart changes
    window.jasUpdateCartCount = function(overrideCount) {
        if (typeof overrideCount === 'number') {
            const badges = document.querySelectorAll('[data-cart-count]');
            badges.forEach(b => {
                b.textContent = overrideCount;
                b.setAttribute('data-cart-count', overrideCount);
            });
        } else {
            updateBadges();
        }
    };

    window.jasUpdateWishlistCount = function() {
        updateBadges();
    };

    function init() {
        updateBadges();
        initWishlistButtons();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.addEventListener('storage', updateBadges);
    document.addEventListener('enhancedload', init);
})();
