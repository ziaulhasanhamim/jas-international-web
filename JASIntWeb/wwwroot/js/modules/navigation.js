/**
 * JAS International - Navigation & Progress Module (ES2024+)
 * Single controller for top progress bar, floating mobile capsule,
 * pending form submit indicators, and horizontal track scrolling.
 */

let pendingForm = null;
let scrollTopFromPath = null;
let suppressNavIndicator = false;
let progress = 0;
let isActive = false;
let tickTimer = null;
let fadeTimer = null;
let animFrame = null;
let capsuleTimer = null;
let capsuleHideTimer = null;
let submitListenerBound = false;

const getIndicator = () => document.getElementById('navigation-progress');
const getProgressBar = () => getIndicator()?.querySelector('.navigation-progress-bar') ?? null;
const getCapsule = () => document.getElementById('navigation-capsule');

function paintProgress() {
    const bar = getProgressBar();
    if (bar) {
        bar.style.transform = `scaleX(${progress})`;
    }
}

function stopTimers() {
    clearInterval(tickTimer);
    clearTimeout(fadeTimer);
    clearTimeout(capsuleTimer);
    clearTimeout(capsuleHideTimer);
    tickTimer = null;
    fadeTimer = null;
    capsuleTimer = null;
    capsuleHideTimer = null;
    if (animFrame !== null) {
        cancelAnimationFrame(animFrame);
        animFrame = null;
    }
}

export function restorePendingForm() {
    if (!pendingForm) return;

    const {
        form,
        button,
        disabledButtons = [],
        label,
        originalLabelNodes,
        originalLabelText,
        originalDisabled,
        originalBusy,
        spinners
    } = pendingForm;

    if (button) {
        if (label instanceof Element) {
            label.replaceChildren(...originalLabelNodes);
        } else if (label) {
            label.textContent = originalLabelText;
        }
        button.disabled = originalDisabled;
    }

    for (const { element, wasDisabled } of disabledButtons) {
        element.disabled = wasDisabled;
    }

    for (const { element, wasHidden } of spinners) {
        element.toggleAttribute('hidden', wasHidden);
    }

    if (originalBusy === null) {
        form.removeAttribute('aria-busy');
    } else {
        form.setAttribute('aria-busy', originalBusy);
    }

    pendingForm = null;
}

export function startNavigation() {
    if (suppressNavIndicator) {
        suppressNavIndicator = false;
        return;
    }

    stopTimers();
    const track = getIndicator();
    const bar = getProgressBar();
    const capsule = getCapsule();

    if (!track || !bar) return;

    if (!isActive) {
        progress = 0;
        bar.style.transition = 'none';
        paintProgress();
    }

    isActive = true;
    track.hidden = false;
    track.style.opacity = '1';

    animFrame = requestAnimationFrame(() => {
        bar.style.transition = '';
        progress = Math.max(progress, 0.15);
        paintProgress();

        tickTimer = setInterval(() => {
            progress = Math.min(0.94, progress + (1 - progress) * 0.1);
            paintProgress();
        }, 300);
    });

    capsuleTimer = setTimeout(() => {
        if (isActive && capsule) {
            capsule.hidden = false;
            requestAnimationFrame(() => capsule.classList.add('is-visible'));
        }
    }, 90);
}

export function finishNavigation() {
    suppressNavIndicator = false;
    stopTimers();
    restorePendingForm();

    if (scrollTopFromPath !== null) {
        const fromPath = scrollTopFromPath;
        scrollTopFromPath = null;

        if (window.location?.pathname !== fromPath) {
            let hashId = '';
            if (window.location?.hash) {
                try {
                    hashId = decodeURIComponent(window.location.hash.slice(1));
                } catch {
                    hashId = window.location.hash.slice(1);
                }
            }
            const target = hashId ? document.getElementById(hashId) : null;
            if (target && typeof target.scrollIntoView === 'function') {
                target.scrollIntoView({ behavior: 'instant' });
            } else if (!window.location?.hash && typeof window.scrollTo === 'function') {
                window.scrollTo({ top: 0, behavior: 'instant' });
            }
        }
    }

    const wasActive = isActive;
    isActive = false;
    progress = 1;

    const capsule = getCapsule();
    if (capsule) {
        capsule.classList.remove('is-visible');
        capsuleHideTimer = setTimeout(() => {
            if (!isActive) capsule.hidden = true;
        }, 200);
    }

    const track = getIndicator();
    const bar = getProgressBar();
    if (!track || !bar) return;

    if (!wasActive && track.hidden) return;

    bar.style.transition = '';
    paintProgress();

    fadeTimer = setTimeout(() => {
        track.style.opacity = '0';
        fadeTimer = setTimeout(() => {
            if (!isActive) track.hidden = true;
        }, 180);
    }, 250);
}

function handleNavigationClick(event) {
    if (event.defaultPrevented || !(event.target instanceof Element)) return;
    const link = event.target.closest?.('a[href]');
    if (!link) return;
    suppressNavIndicator = Boolean(link.closest?.('[data-no-nav-indicator]') || link.hasAttribute?.('data-no-nav-indicator'));
}

function handleFormSubmit(event) {
    if (event.defaultPrevented) return;
    const form = event.target;
    const isFormElement = (typeof HTMLFormElement !== 'undefined' && form instanceof HTMLFormElement) || form?.tagName === 'FORM';
    if (!isFormElement) return;

    const isScrollTopForm = typeof form.matches === 'function'
        ? form.matches('form[data-scroll-top]')
        : Boolean(form.hasAttribute?.('data-scroll-top'));
    scrollTopFromPath = isScrollTopForm ? (window.location?.pathname ?? '') : null;

    suppressNavIndicator = Boolean(
        form.closest?.('[data-no-nav-indicator]') ||
        form.hasAttribute?.('data-no-nav-indicator') ||
        event.submitter?.closest?.('[data-no-nav-indicator]') ||
        event.submitter?.hasAttribute?.('data-no-nav-indicator')
    );

    if (form.dataset?.submitIndication !== 'true') return;

    const button = (typeof HTMLButtonElement !== 'undefined' && event.submitter instanceof HTMLButtonElement)
        || event.submitter?.tagName === 'BUTTON'
        ? event.submitter
        : form.querySelector?.('button[type="submit"]');
    const disableAllSubmits = form.dataset?.disableSubmits === 'all';
    const submitButtons = disableAllSubmits && typeof form.querySelectorAll === 'function'
        ? [...form.querySelectorAll('button[type="submit"]')]
        : [];

    queueMicrotask(() => {
        restorePendingForm();
        const spinnerElements = [...(form.querySelectorAll?.('.form-submit-spinner') ?? [])];
        const label = button?.hasAttribute?.('data-pending-text')
            ? button.querySelector('[data-submit-label]')
                ?? [...button.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim())
            : null;

        pendingForm = {
            form,
            button,
            disabledButtons: submitButtons.map(element => ({ element, wasDisabled: element.disabled })),
            label,
            originalLabelNodes: typeof Element !== 'undefined' && label instanceof Element ? [...label.childNodes] : [],
            originalLabelText: label?.textContent,
            originalDisabled: button?.disabled,
            originalBusy: form.getAttribute?.('aria-busy') ?? null,
            spinners: spinnerElements.map(element => ({ element, wasHidden: element.hasAttribute('hidden') }))
        };

        if (button) {
            if (label && !spinnerElements.some(spinner => label === spinner || label.contains?.(spinner))) {
                label.textContent = button.dataset.pendingText ?? '';
            }
            button.disabled = true;
        }

        for (const { element } of pendingForm.disabledButtons) {
            element.disabled = true;
        }

        for (const spinner of spinnerElements) {
            spinner.removeAttribute('hidden');
        }
        form.setAttribute?.('aria-busy', 'true');
    });
}

export function initNavigation() {
    if (submitListenerBound) return;
    submitListenerBound = true;
    document.addEventListener('click', handleNavigationClick, true);
    document.addEventListener('submit', handleFormSubmit, true);
}

export function scrollElement(element, delta) {
    element?.scrollBy({ left: delta, behavior: 'smooth' });
}
