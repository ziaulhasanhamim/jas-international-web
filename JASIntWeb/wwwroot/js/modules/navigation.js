/**
 * JAS International - Navigation & Progress Module (ES2024+)
 * Single controller for top progress bar, floating mobile capsule,
 * pending form submit indicators, and horizontal track scrolling.
 */

let pendingForm = null;
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
    stopTimers();
    restorePendingForm();

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

function handleFormSubmit(event) {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || form.dataset.submitIndication !== 'true') return;

    const button = event.submitter instanceof HTMLButtonElement
        ? event.submitter
        : form.querySelector('button[type="submit"]');

    queueMicrotask(() => {
        restorePendingForm();
        const spinnerElements = [...form.querySelectorAll('.form-submit-spinner')];
        const label = button?.hasAttribute('data-pending-text')
            ? button.querySelector('[data-submit-label]')
                ?? [...button.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim())
            : null;

        pendingForm = {
            form,
            button,
            label,
            originalLabelNodes: label instanceof Element ? [...label.childNodes] : [],
            originalLabelText: label?.textContent,
            originalDisabled: button?.disabled,
            originalBusy: form.getAttribute('aria-busy'),
            spinners: spinnerElements.map(element => ({ element, wasHidden: element.hasAttribute('hidden') }))
        };

        if (button) {
            if (label && !spinnerElements.some(spinner => label === spinner || label.contains?.(spinner))) {
                label.textContent = button.dataset.pendingText ?? '';
            }
            button.disabled = true;
        }

        for (const spinner of spinnerElements) {
            spinner.removeAttribute('hidden');
        }
        form.setAttribute('aria-busy', 'true');
    });
}

export function initNavigation() {
    if (submitListenerBound) return;
    submitListenerBound = true;
    document.addEventListener('submit', handleFormSubmit, true);
}

export function scrollElement(element, delta) {
    element?.scrollBy({ left: delta, behavior: 'smooth' });
}
