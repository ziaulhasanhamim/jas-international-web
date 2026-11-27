let countdownIntervals = [];
let delegatedInitialized = false;

function pad2(n) {
    return String(Math.max(0, Math.floor(n))).padStart(2, '0');
}

function stopCountdowns() {
    for (const id of countdownIntervals) {
        clearInterval(id);
    }
    countdownIntervals = [];
}

function initCountdowns() {
    stopCountdowns();
    const els = document.querySelectorAll('[data-campaign-countdown]');
    for (const el of els) {
        let targetMs = Date.parse(el.dataset.targetIso || '');
        const endMs = el.dataset.endIso ? Date.parse(el.dataset.endIso) : NaN;
        const isUpcomingInitially = el.dataset.upcoming === 'true';
        if (Number.isNaN(targetMs)) continue;

        const daysEl = el.querySelector('[data-cd-days]');
        const hoursEl = el.querySelector('[data-cd-hours]');
        const minsEl = el.querySelector('[data-cd-mins]');
        const secsEl = el.querySelector('[data-cd-secs]');
        const labelEl = el.querySelector('[data-cd-label]');

        const tick = () => {
            const now = Date.now();
            let diff = targetMs - now;
            if (diff <= 0) {
                if (isUpcomingInitially && !Number.isNaN(endMs) && endMs > now) {
                    // Refresh once right when the campaign window opens so live prices render.
                    if (!el.dataset.reloadedForStart) {
                        el.dataset.reloadedForStart = 'true';
                        window.location.reload();
                        return;
                    }
                    targetMs = endMs;
                    diff = endMs - now;
                    if (labelEl) labelEl.textContent = '⏳ Deals Expire In';
                } else {
                    if (daysEl) daysEl.textContent = '00';
                    if (hoursEl) hoursEl.textContent = '00';
                    if (minsEl) minsEl.textContent = '00';
                    if (secsEl) secsEl.textContent = '00';
                    return;
                }
            }

            const totalSeconds = Math.floor(diff / 1000);
            const days = Math.floor(totalSeconds / 86400);
            const hours = Math.floor((totalSeconds % 86400) / 3600);
            const mins = Math.floor((totalSeconds % 3600) / 60);
            const secs = totalSeconds % 60;

            if (daysEl) daysEl.textContent = pad2(days);
            if (hoursEl) hoursEl.textContent = pad2(hours);
            if (minsEl) minsEl.textContent = pad2(mins);
            if (secsEl) secsEl.textContent = pad2(secs);
        };

        tick();
        countdownIntervals.push(setInterval(tick, 1000));
    }
}

function applyHubSearch(term) {
    const input = document.querySelector('[data-campaign-hub-search]');
    if (input && typeof term === 'string' && input.value !== term) {
        input.value = term;
    }

    const q = (input?.value || '').trim().toLowerCase();
    const clearBtn = document.querySelector('[data-campaign-hub-clear]');
    if (clearBtn) {
        clearBtn.classList.toggle('hidden', q.length === 0);
    }

    const cards = document.querySelectorAll('[data-campaign-card]');
    if (cards.length === 0) return;

    let visible = 0;
    for (const card of cards) {
        const hay = (card.dataset.searchIndex || '').toLowerCase();
        const match = !q || hay.includes(q);
        card.classList.toggle('hidden', !match);
        if (match) visible++;
    }

    const countEl = document.querySelector('[data-campaign-count]');
    if (countEl) {
        countEl.textContent = `Showing ${visible} Campaign${visible === 1 ? '' : 's'}`;
    }

    const moreHeader = document.querySelector('[data-campaign-more-section]');
    if (moreHeader) {
        const moreCards = moreHeader.querySelectorAll('[data-campaign-card]:not(.hidden)');
        moreHeader.classList.toggle('hidden', moreCards.length === 0);
    }

    const emptyEl = document.querySelector('[data-campaign-hub-empty]');
    const emptyTermEl = document.querySelector('[data-campaign-hub-empty-term]');
    if (emptyEl) {
        emptyEl.classList.toggle('hidden', visible > 0);
        if (emptyTermEl) emptyTermEl.textContent = input?.value || '';
    }
}

function applyCategoryFilter(category) {
    const bar = document.querySelector('[data-campaign-filter-bar]');
    if (!bar) return;

    const selected = category || '';
    for (const btn of bar.querySelectorAll('[data-campaign-category]')) {
        const active = (btn.dataset.campaignCategory || '') === selected;
        btn.className = active
            ? 'px-3.5 py-1.5 rounded-xl text-xs font-bold transition whitespace-nowrap cursor-pointer bg-slate-900 text-white shadow-2xs'
            : 'px-3.5 py-1.5 rounded-xl text-xs font-bold transition whitespace-nowrap cursor-pointer bg-slate-100 text-slate-600 hover:bg-slate-200';
    }

    const cards = document.querySelectorAll('[data-campaign-product]');
    let visible = 0;
    for (const card of cards) {
        const match = !selected || card.dataset.category === selected;
        card.classList.toggle('hidden', !match);
        if (match) visible++;
    }

    const heading = document.querySelector('[data-campaign-grid-heading]');
    if (heading) {
        heading.textContent = selected ? `${selected} Deals` : 'Featured Campaign Deals';
    }

    const countEl = document.querySelector('[data-campaign-visible-count]');
    if (countEl) {
        countEl.textContent = `${visible} Product${visible === 1 ? '' : 's'}`;
    }

    const emptyCatEl = document.querySelector('[data-campaign-cat-empty]');
    if (emptyCatEl) {
        emptyCatEl.classList.toggle('hidden', visible > 0);
    }
}

function ensureDelegation() {
    if (delegatedInitialized) return;
    delegatedInitialized = true;

    document.addEventListener('click', async event => {
        const copyBtn = event.target.closest('button[data-copy-text]');
        if (copyBtn) {
            const text = copyBtn.dataset.copyText || '';
            if (!text) return;
            try {
                await navigator.clipboard.writeText(text);
                const labelEl = copyBtn.querySelector('[data-copy-label]');
                if (labelEl) {
                    const prev = labelEl.textContent;
                    labelEl.textContent = copyBtn.dataset.copiedLabel || '✓ Copied!';
                    setTimeout(() => {
                        if (labelEl.isConnected) labelEl.textContent = prev;
                    }, 2000);
                }
            } catch {
                // Clipboard unavailable
            }
            return;
        }

        const catBtn = event.target.closest('[data-campaign-category]');
        if (catBtn) {
            applyCategoryFilter(catBtn.dataset.campaignCategory || '');
            return;
        }

        const clearHubBtn = event.target.closest('[data-campaign-hub-clear], [data-campaign-hub-reset]');
        if (clearHubBtn) {
            applyHubSearch('');
            return;
        }

        const resetCatBtn = event.target.closest('[data-campaign-cat-reset]');
        if (resetCatBtn) {
            applyCategoryFilter('');
        }
    });

    document.addEventListener('input', event => {
        if (event.target.matches('[data-campaign-hub-search]')) {
            applyHubSearch();
        }
    });
}

export function initCampaignPages() {
    ensureDelegation();
    initCountdowns();
}

export function cleanupCampaignPages() {
    stopCountdowns();
}
