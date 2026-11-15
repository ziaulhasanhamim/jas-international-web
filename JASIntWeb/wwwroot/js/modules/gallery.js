/**
 * JAS International - Product Gallery Module (ES2024+)
 * Handles interactive product image thumbnail switching via clean event delegation,
 * with multi-format picture source (AVIF/WebP) synchronization.
 */

let isInitialized = false;

/**
 * Switches the main product image in the gallery.
 * @param {HTMLElement} btn
 */
export function switchMainImage(btn) {
    if (!btn) return;
    const src = btn.dataset.src;
    const avif = btn.dataset.avif;
    const webp = btn.dataset.webp;

    const picture = document.getElementById('main-product-picture');
    if (picture) {
        const sourceAvif = picture.querySelector('source[type="image/avif"]');
        if (sourceAvif && avif) sourceAvif.srcset = avif;

        const sourceWebp = picture.querySelector('source[type="image/webp"]');
        if (sourceWebp && webp) sourceWebp.srcset = webp;
    }

    const img = document.getElementById('main-product-img');
    if (img && src) {
        img.src = src;
    }

    document.querySelectorAll('.thumb-btn').forEach(b => {
        b.classList.remove('border-blue-600', 'shadow-xs', 'ring-2', 'ring-blue-500/20');
        b.classList.add('border-slate-200');
    });

    btn.classList.remove('border-slate-200');
    btn.classList.add('border-blue-600', 'shadow-xs', 'ring-2', 'ring-blue-500/20');
}

/**
 * Initializes document-level event delegation for gallery thumbnails.
 */
export function initGallery() {
    if (isInitialized) return;
    isInitialized = true;

    document.addEventListener('click', event => {
        const thumbBtn = event.target.closest('.thumb-btn');
        if (thumbBtn) {
            switchMainImage(thumbBtn);
        }
    });
}
