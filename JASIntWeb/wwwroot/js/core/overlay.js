/**
 * JAS International - Core Overlay & Modal Blueprint (ES2024+)
 * Functional controller for accessible dialogs, slide-over drawers, and bottom sheets.
 * Supports left/right/bottom slides and fade+scale dialogs, ESC dismiss, and focus restoration.
 */

import { emit } from './events.js';

let activeOverlayId = null;
let lastFocusedElement = null;
let escapeListenerBound = false;
const closedTransformMap = new WeakMap();

const SLIDE_CLASSES = ['translate-x-full', '-translate-x-full', 'translate-y-full', '-translate-y-full'];

function handleKeyDown(e) {
    if (e.key === 'Escape' && activeOverlayId) {
        e.preventDefault();
        closeOverlay(activeOverlayId);
    }
}

function ensureEscapeListener() {
    if (!escapeListenerBound) {
        document.addEventListener('keydown', handleKeyDown);
        escapeListenerBound = true;
    }
}

export function isOverlayOpen(id) {
    return activeOverlayId === id;
}

export function openOverlay(id) {
    const container = document.getElementById(id);
    if (!container) return;

    if (activeOverlayId && activeOverlayId !== id) {
        closeOverlay(activeOverlayId, false);
    }

    lastFocusedElement = document.activeElement;
    activeOverlayId = id;

    container.classList.remove('pointer-events-none', 'hidden');
    container.classList.add('pointer-events-auto');
    container.setAttribute('aria-hidden', 'false');

    const backdrop = container.querySelector('[data-overlay-backdrop]') || container.firstElementChild;
    if (backdrop) {
        backdrop.classList.remove('opacity-0', 'pointer-events-none');
        backdrop.classList.add('opacity-100', 'pointer-events-auto');
    }

    const panel = container.querySelector('[data-overlay-panel]') || container.querySelector('aside, [role="dialog"]');
    if (panel) {
        if (!closedTransformMap.has(panel)) {
            const matchedSlide = SLIDE_CLASSES.find(cls => panel.classList.contains(cls));
            if (matchedSlide) {
                closedTransformMap.set(panel, matchedSlide);
            }
        }

        panel.classList.remove(...SLIDE_CLASSES, 'scale-95', 'opacity-0', 'pointer-events-none');
        panel.classList.add('translate-x-0', 'translate-y-0', 'scale-100', 'opacity-100', 'pointer-events-auto');

        const focusTarget = panel.querySelector('button:not([data-overlay-close]), [href], input:not([type="hidden"]), select, textarea, [tabindex]:not([tabindex="-1"])')
            || panel.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        if (focusTarget) {
            setTimeout(() => focusTarget.focus(), 50);
        }
    }

    document.body.classList.add('overflow-hidden');

    ensureEscapeListener();
    emit('overlay:opened', { id });
}

export function closeOverlay(id, restoreFocus = true) {
    const container = document.getElementById(id);
    if (!container) return;

    const backdrop = container.querySelector('[data-overlay-backdrop]') || container.firstElementChild;
    if (backdrop) {
        backdrop.classList.remove('opacity-100', 'pointer-events-auto');
        backdrop.classList.add('opacity-0', 'pointer-events-none');
    }

    const panel = container.querySelector('[data-overlay-panel]') || container.querySelector('aside, [role="dialog"]');
    if (panel) {
        panel.classList.remove('translate-x-0', 'translate-y-0', 'scale-100', 'opacity-100', 'pointer-events-auto');
        const closedSlide = closedTransformMap.get(panel) || panel.dataset.overlaySlide;
        if (closedSlide) {
            panel.classList.add(closedSlide);
        } else if (panel.classList.contains('transition-transform')) {
            panel.classList.add('translate-x-full');
        } else {
            panel.classList.add('opacity-0', 'scale-95');
        }
    }

    setTimeout(() => {
        if (activeOverlayId === id) return;
        container.classList.remove('pointer-events-auto');
        container.classList.add('pointer-events-none');
        container.setAttribute('aria-hidden', 'true');
    }, 300);

    if (activeOverlayId === id) {
        activeOverlayId = null;
    }
    document.body.classList.remove('overflow-hidden');

    if (restoreFocus && lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
        lastFocusedElement.focus();
        lastFocusedElement = null;
    }

    emit('overlay:closed', { id });
}

export function closeAllOverlays() {
    if (activeOverlayId) {
        closeOverlay(activeOverlayId, false);
    }
    document.body.classList.remove('overflow-hidden');
}

export function toggleOverlay(id) {
    if (isOverlayOpen(id)) {
        closeOverlay(id);
    } else {
        openOverlay(id);
    }
}

export function initOverlayDelegation() {
    if (document.__jasOverlayDelegated) return;
    document.__jasOverlayDelegated = true;

    document.addEventListener('click', (e) => {
        const closeBtn = e.target.closest('[data-overlay-close]');
        if (closeBtn) {
            e.preventDefault();
            const id = closeBtn.getAttribute('data-overlay-close') || activeOverlayId;
            if (id) closeOverlay(id);
            return;
        }

        const openBtn = e.target.closest('[data-overlay-open]');
        if (openBtn) {
            e.preventDefault();
            const id = openBtn.getAttribute('data-overlay-open');
            if (id) openOverlay(id);
            return;
        }

        const toggleBtn = e.target.closest('[data-overlay-toggle]');
        if (toggleBtn) {
            e.preventDefault();
            const id = toggleBtn.getAttribute('data-overlay-toggle');
            if (id) toggleOverlay(id);
        }
    });
}
