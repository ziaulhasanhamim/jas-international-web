/**
 * JAS International - Reviews Module (ES2024+)
 * Handles client-side rating star selection, modal dialogs, and non-reloading enhanced form submissions.
 */

let isDelegationInitialized = false;

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
 * Sets the star rating value in the form and updates visual stars.
 * @param {number} rating
 */
export function setStarRating(rating) {
    const input = document.getElementById('review-rating-input');
    const text = document.getElementById('review-rating-text');
    if (input) input.value = String(rating);
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
        const errorEl = document.getElementById('review-form-error');
        if (errorEl) {
            errorEl.classList.add('hidden');
            errorEl.textContent = '';
        }
        const nameInput = document.getElementById('review-name');
        if (nameInput) {
            setTimeout(() => nameInput.focus(), 50);
        }
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
 * Builds HTML for a review card.
 */
function createReviewCardHtml(review, isPending = false) {
    const rating = Math.min(5, Math.max(1, parseInt(review.rating, 10) || 5));
    const starsHtml = [1, 2, 3, 4, 5].map(s =>
        `<span class="${s <= rating ? 'text-amber-400' : 'text-slate-200'}">★</span>`
    ).join('');

    const titleText = escapeHtml(review.title || 'Customer Review');
    const authorText = escapeHtml(review.customerName || 'Customer');
    const commentText = escapeHtml(review.comment || '');
    const dateText = escapeHtml(review.createdAt || 'Just now');

    const pendingBadge = isPending
        ? `<span class="inline-flex items-center text-[10px] font-semibold text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full">⏳ Your Review (Awaiting Moderation)</span>`
        : '';

    const verifiedBadge = review.isVerifiedPurchase
        ? `<span>•</span><span class="text-emerald-700 font-semibold">✓ Verified Buyer</span>`
        : '';

    return `
        <div class="rounded-2xl border border-slate-200/90 bg-white p-5 shadow-xs transition-all duration-300 animate-fadeIn" data-review-pending="${isPending ? 'true' : 'false'}">
            <div class="flex flex-wrap items-center justify-between gap-2 mb-2">
                <div class="flex items-center gap-2">
                    <div class="flex items-center gap-0.5 text-xs" aria-label="${rating} out of 5 stars">
                        ${starsHtml}
                    </div>
                    <span class="inline-flex items-center px-1.5 py-0.5 rounded text-[11px] font-bold bg-amber-50 text-amber-800 border border-amber-200/60">
                        ${rating}.0 ★
                    </span>
                    <strong class="text-xs font-bold text-slate-900">${titleText}</strong>
                    ${pendingBadge}
                </div>
                <span class="text-[11px] text-slate-400">${dateText}</span>
            </div>
            <p class="text-xs text-slate-700 leading-relaxed">${commentText}</p>
            <div class="mt-2 flex items-center gap-2 text-[11px] text-slate-500">
                <span class="font-semibold text-slate-800">${authorText}</span>
                ${verifiedBadge}
            </div>
        </div>
    `;
}

/**
 * Inserts a review into the reviews section on the page.
 */
function insertReview(review, isPending = false) {
    const container = document.getElementById('reviews-container');
    if (!container) return;

    const placeholder = document.getElementById('no-reviews-placeholder');
    if (placeholder) {
        placeholder.classList.add('hidden');
    }

    let list = document.getElementById('reviews-list');
    if (!list) {
        list = document.createElement('div');
        list.id = 'reviews-list';
        list.className = 'space-y-4';
        container.appendChild(list);
    }

    const cardHtml = createReviewCardHtml(review, isPending);
    list.insertAdjacentHTML('afterbegin', cardHtml);
}

/**
 * Restores any locally stored pending reviews for this product on page load.
 */
function restorePendingReviews() {
    const section = document.getElementById('reviews');
    if (!section) return;
    const productId = section.getAttribute('data-product-id');
    if (!productId) return;

    try {
        const raw = localStorage.getItem(`jas_pending_review_${productId}`);
        if (!raw) return;
        const storedList = JSON.parse(raw);
        const reviews = Array.isArray(storedList) ? storedList : [storedList];

        const existingPending = document.querySelectorAll('[data-review-pending="true"]');
        if (existingPending.length > 0) return;

        reviews.forEach(rev => {
            if (rev && rev.comment) {
                insertReview(rev, true);
            }
        });
    } catch {
        // Ignore localStorage parsing errors
    }
}

/**
 * Handles review form submission asynchronously via fetch (no page reload).
 */
async function handleReviewSubmit(event) {
    const form = event.target.closest('#write-review-form');
    if (!form) return;

    event.preventDefault();

    const errorEl = document.getElementById('review-form-error');
    if (errorEl) {
        errorEl.classList.add('hidden');
        errorEl.textContent = '';
    }

    const nameInput = form.querySelector('[name="Name"]');
    const commentInput = form.querySelector('[name="Comment"]');
    const ratingInput = form.querySelector('[name="Rating"]');
    const submitBtn = document.getElementById('submit-review-btn') || form.querySelector('button[type="submit"]');

    const name = nameInput ? nameInput.value.trim() : '';
    const comment = commentInput ? commentInput.value.trim() : '';
    const rating = ratingInput ? parseInt(ratingInput.value, 10) : 5;

    if (name.length < 2) {
        if (errorEl) {
            errorEl.textContent = 'Please enter your name (at least 2 characters).';
            errorEl.classList.remove('hidden');
        }
        nameInput?.focus();
        return;
    }

    if (comment.length < 10) {
        if (errorEl) {
            errorEl.textContent = 'Please write at least 10 characters detailing your experience.';
            errorEl.classList.remove('hidden');
        }
        commentInput?.focus();
        return;
    }

    if (isNaN(rating) || rating < 1 || rating > 5) {
        if (errorEl) {
            errorEl.textContent = 'Please select a rating between 1 and 5 stars.';
            errorEl.classList.remove('hidden');
        }
        return;
    }

    const origButtonText = submitBtn ? submitBtn.innerHTML : 'Submit Review';
    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="inline-block animate-spin mr-1">⏳</span> Submitting...';
    }

    try {
        const formData = new FormData(form);
        const response = await fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            }
        });

        if (response.ok) {
            const data = await response.json();

            // 1. Close modal cleanly
            closeModal('write-review-modal');

            // 2. Reset form
            form.reset();
            setStarRating(5);

            // 3. Show success alert in the reviews section
            const alertContainer = document.getElementById('review-alert-container');
            if (alertContainer) {
                alertContainer.innerHTML = `
                    <div class="mb-4 rounded-xl border border-emerald-200 bg-emerald-50 p-3.5 text-xs text-emerald-800 font-semibold flex items-center justify-between animate-fadeIn">
                        <span>✓ ${escapeHtml(data.message || 'Thank you! Your review has been submitted and is awaiting moderation.')}</span>
                        <button type="button" onclick="this.parentElement.remove()" class="text-emerald-600 hover:text-emerald-950 font-bold p-1 cursor-pointer">✕</button>
                    </div>
                `;
            }

            // 4. Insert the new review card into the reviews list immediately
            const reviewData = data.review || {
                customerName: name,
                rating: rating,
                title: form.querySelector('[name="Title"]')?.value.trim() || 'Customer Review',
                comment: comment,
                createdAt: 'Just now',
                isApproved: false,
                isVerifiedPurchase: false
            };

            insertReview(reviewData, true);

            // 5. Save pending review into localStorage so it persists across refreshes
            const section = document.getElementById('reviews');
            const productId = section?.getAttribute('data-product-id');
            if (productId) {
                try {
                    localStorage.setItem(`jas_pending_review_${productId}`, JSON.stringify([reviewData]));
                } catch {
                    // Ignore localStorage quota errors
                }
            }

            // 6. Smoothly scroll to the reviews section
            section?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        } else {
            let errorMsg = 'Failed to submit review. Please try again.';
            try {
                const errData = await response.json();
                if (errData && errData.message) errorMsg = errData.message;
            } catch {
                const text = await response.text();
                if (text) errorMsg = text;
            }
            if (errorEl) {
                errorEl.textContent = errorMsg;
                errorEl.classList.remove('hidden');
            }
        }
    } catch {
        if (errorEl) {
            errorEl.textContent = 'Network error. Please check your connection and try again.';
            errorEl.classList.remove('hidden');
        }
    } finally {
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = origButtonText;
        }
    }
}

/**
 * Initializes document-level event delegation for review controls, modals, and AJAX form submit.
 */
export function initReviews() {
    // Restore any pending reviews stored in localStorage
    restorePendingReviews();

    if (isDelegationInitialized) return;
    isDelegationInitialized = true;

    // Document click delegation
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

    // Form submit delegation (enhanced AJAX submit, no full-page reload)
    document.addEventListener('submit', event => {
        if (event.target && event.target.id === 'write-review-form') {
            handleReviewSubmit(event);
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
