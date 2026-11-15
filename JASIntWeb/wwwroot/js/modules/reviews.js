/**
 * JAS International - Reviews Module (ES2024+)
 * Handles client-side rating star selection and modal opening/closing via event delegation.
 */

let isInitialized = false;

/**
 * Sets the star rating value in the form and updates visual stars.
 * @param {number} rating
 */
export function setStarRating(rating) {
    const input = document.getElementById('review-rating-input');
    const text = document.getElementById('review-rating-text');
    if (input) input.value = rating;
    if (text) text.textContent = `${rating} / 5 Stars`;

    document.querySelectorAll('.star-btn').forEach((btn, idx) => {
        const starNum = idx + 1;
        if (starNum <= rating) {
            btn.classList.add('text-amber-400');
            btn.classList.remove('text-slate-200');
        } else {
            btn.classList.remove('text-amber-400');
            btn.classList.add('text-slate-200');
        }
    });
}

/**
 * Opens a modal dialog by element ID.
 * @param {string} modalId
 */
export function openModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.remove('hidden');
    }
}

/**
 * Closes a modal dialog by element ID.
 * @param {string} modalId
 */
export function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.add('hidden');
    }
}

/**
 * Initializes document-level event delegation for review controls and modals.
 */
export function initReviews() {
    if (isInitialized) return;
    isInitialized = true;

    document.addEventListener('click', event => {
        // Modal opener
        const openBtn = event.target.closest('[data-open-modal]');
        if (openBtn) {
            const modalId = openBtn.getAttribute('data-open-modal');
            openModal(modalId);
            return;
        }

        // Modal closer
        const closeBtn = event.target.closest('[data-close-modal]');
        if (closeBtn) {
            const modalId = closeBtn.getAttribute('data-close-modal');
            closeModal(modalId);
            return;
        }

        // Close when clicking directly on the backdrop outside the modal card
        const modal = event.target;
        if (modal && modal.id === 'write-review-modal' && !modal.classList.contains('hidden')) {
            const dialog = modal.querySelector('.max-w-lg');
            if (dialog && !dialog.contains(event.target)) {
                closeModal(modal.id);
                return;
            }
        }

        // Star rating button
        const starBtn = event.target.closest('[data-star-rating]');
        if (starBtn) {
            const rating = parseInt(starBtn.getAttribute('data-star-rating'), 10);
            if (rating >= 1 && rating <= 5) {
                setStarRating(rating);
            }
        }
    });

    // Close modal on Escape key
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            const openModalEl = document.querySelector('#write-review-modal:not(.hidden)');
            if (openModalEl) {
                closeModal(openModalEl.id);
            }
        }
    });
}
