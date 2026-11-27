/**
 * JAS International - Reviews Module (ES2024+)
 * Handles client-side rating star selection, pending review persistence,
 * and review submissions routed through core/api.js and core/overlay.js.
 */

import { request } from '../core/api.js';
import { on } from '../core/events.js';
import { closeOverlay } from '../core/overlay.js';

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

    const idAttr = review.id ? `data-review-id="${escapeHtml(review.id)}"` : '';

    return `
        <div class="rounded-2xl border border-slate-200/90 bg-white p-5 shadow-xs transition-all duration-300 animate-fadeIn" data-review-pending="${isPending ? 'true' : 'false'}" ${idAttr}>
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

function insertReview(review, isPending = false) {
    const container = document.getElementById('reviews-container');
    if (!container) return;

    if (isPending) {
        container.querySelectorAll('[data-review-pending="true"]').forEach(el => el.remove());
    }

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

    list.insertAdjacentHTML('afterbegin', createReviewCardHtml(review, isPending));
}

function restorePendingReviews() {
    const section = document.getElementById('reviews');
    if (!section) return;
    const productId = section.getAttribute('data-product-id');
    if (!productId) return;

    try {
        const raw = localStorage.getItem(`jas_pending_review_${productId}`);
        if (!raw) return;
        const stored = JSON.parse(raw);
        const rev = Array.isArray(stored) ? stored[stored.length - 1] : stored;
        if (!rev || !rev.comment) return;

        const list = document.getElementById('reviews-list');
        if (list) {
            let alreadyApproved = false;
            if (rev.id && list.querySelector(`[data-review-id="${rev.id}"]`)) {
                alreadyApproved = true;
            }
            if (!alreadyApproved) {
                const cleanComment = (rev.comment || '').trim().toLowerCase();
                const cleanAuthor = (rev.customerName || '').trim().toLowerCase();
                list.querySelectorAll('[data-review-id]').forEach(el => {
                    const author = el.querySelector('.font-semibold.text-slate-800')?.textContent?.trim().toLowerCase();
                    const comment = el.querySelector('p.text-slate-700')?.textContent?.trim().toLowerCase();
                    if (author === cleanAuthor && comment === cleanComment) {
                        alreadyApproved = true;
                    }
                });
            }
            if (alreadyApproved) {
                localStorage.removeItem(`jas_pending_review_${productId}`);
                return;
            }
        }

        if (document.querySelectorAll('[data-review-pending="true"]').length > 0) return;

        insertReview(rev, true);
    } catch {
        // Ignore localStorage parsing errors
    }
}

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
        const res = await request(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (res.ok) {
            const data = res.data || {};
            closeOverlay('write-review-modal');

            form.reset();
            setStarRating(5);

            const alertContainer = document.getElementById('review-alert-container');
            if (alertContainer) {
                alertContainer.innerHTML = `
                    <div class="mb-4 rounded-xl border border-emerald-200 bg-emerald-50 p-3.5 text-xs text-emerald-800 font-semibold flex items-center justify-between animate-fadeIn">
                        <span>✓ ${escapeHtml(data.message || 'Thank you! Your review has been submitted and is awaiting moderation.')}</span>
                        <button type="button" data-dismiss-parent class="text-emerald-600 hover:text-emerald-950 font-bold p-1 cursor-pointer" aria-label="Dismiss">✕</button>
                    </div>
                `;
            }

            const reviewData = data.review || {
                customerName: name,
                rating,
                title: form.querySelector('[name="Title"]')?.value.trim() || 'Customer Review',
                comment,
                createdAt: 'Just now',
                isApproved: false,
                isVerifiedPurchase: false
            };

            insertReview(reviewData, true);

            const section = document.getElementById('reviews');
            const productId = section?.getAttribute('data-product-id');
            if (productId) {
                try {
                    localStorage.setItem(`jas_pending_review_${productId}`, JSON.stringify(reviewData));
                } catch {}
            }

            section?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        } else if (errorEl) {
            errorEl.textContent = res.status === 0
                ? 'Network error. Please check your connection and try again.'
                : (res.error || 'Failed to submit review. Please try again.');
            errorEl.classList.remove('hidden');
        }
    } finally {
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = origButtonText;
        }
    }
}

export function initReviews() {
    restorePendingReviews();

    if (isDelegationInitialized) return;
    isDelegationInitialized = true;

    on('overlay:opened', detail => {
        if (detail?.id !== 'write-review-modal') return;
        const errorEl = document.getElementById('review-form-error');
        if (errorEl) {
            errorEl.classList.add('hidden');
            errorEl.textContent = '';
        }
        const nameInput = document.getElementById('review-name');
        if (nameInput) {
            setTimeout(() => nameInput.focus(), 50);
        }
    });

    document.addEventListener('click', event => {
        const starBtn = event.target.closest('[data-star-rating]');
        if (starBtn) {
            const rating = parseInt(starBtn.getAttribute('data-star-rating'), 10);
            if (rating >= 1 && rating <= 5) {
                setStarRating(rating);
            }
        }
    });

    document.addEventListener('submit', event => {
        if (event.target && event.target.id === 'write-review-form') {
            handleReviewSubmit(event);
        }
    });
}
