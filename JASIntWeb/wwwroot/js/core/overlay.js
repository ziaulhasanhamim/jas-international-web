/**
 * JAS International - Core Overlay & Modal Blueprint (ES2024+)
 * Functional controller for accessible dialogs, slide-over drawers, and bottom sheets.
 * Provides smooth 60fps animations, backdrop blur, ESC key dismiss, and focus restoration.
 */

import { emit } from './events.js';

let activeOverlayId = null;
let lastFocusedElement = null;
let escapeListenerBound = false;

/**
 * Global ESC key listener for closing active overlays.
 * @param {KeyboardEvent} e
 */
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

/**
 * Checks if an overlay is currently open.
 * @param {string} id - The container DOM element ID
 * @returns {boolean}
 */
export function isOverlayOpen(id) {
    return activeOverlayId === id;
}

/**
 * Opens an overlay modal or drawer with smooth transition and accessibility state.
 * @param {string} id - The container DOM element ID
 */
export function openOverlay(id) {
    const container = document.getElementById(id);
    if (!container) return;

    if (activeOverlayId && activeOverlayId !== id) {
        closeOverlay(activeOverlayId, false);
    }

    lastFocusedElement = document.activeElement;
    activeOverlayId = id;

    // 1. Enable interaction
    container.classList.remove('pointer-events-none');
    container.classList.add('pointer-events-auto');
    container.setAttribute('aria-hidden', 'false');

    // 2. Animate backdrop (fade in)
    const backdrop = container.querySelector('[data-overlay-backdrop]') || container.firstElementChild;
    if (backdrop) {
        backdrop.classList.remove('opacity-0', 'pointer-events-none');
        backdrop.classList.add('opacity-100', 'pointer-events-auto');
    }

    // 3. Animate panel (slide in / zoom in)
    const panel = container.querySelector('[data-overlay-panel]') || container.querySelector('aside, [role="dialog"]');
    if (panel) {
        panel.classList.remove('translate-x-full', 'translate-y-full', 'scale-95', 'opacity-0', 'pointer-events-none');
        panel.classList.add('translate-x-0', 'translate-y-0', 'scale-100', 'opacity-100', 'pointer-events-auto');

        // Focus first actionable element inside panel
        const focusTarget = panel.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        if (focusTarget) {
            setTimeout(() => focusTarget.focus(), 50);
        }
    }

    // 4. Lock background scroll
    document.body.classList.add('overflow-hidden');

    ensureEscapeListener();
    emit('overlay:opened', { id });
}

/**
 * Closes an overlay modal or drawer.
 * @param {string} id - The container DOM element ID
 * @param {boolean} [restoreFocus=true] - Whether to restore focus to trigger
 */
export function closeOverlay(id, restoreFocus = true) {
    const container = document.getElementById(id);
    if (!container) return;

    // 1. Animate backdrop (fade out)
    const backdrop = container.querySelector('[data-overlay-backdrop]') || container.firstElementChild;
    if (backdrop) {
        backdrop.classList.remove('opacity-100', 'pointer-events-auto');
        backdrop.classList.add('opacity-0', 'pointer-events-none');
    }

    // 2. Animate panel (slide out / zoom out)
    const panel = container.querySelector('[data-overlay-panel]') || container.querySelector('aside, [role="dialog"]');
    if (panel) {
        panel.classList.remove('translate-x-0', 'translate-y-0', 'scale-100', 'opacity-100');
        // Slide right if slide-over, otherwise fade/scale
        if (panel.classList.contains('transition-transform')) {
            panel.classList.add('translate-x-full');
        } else {
            panel.classList.add('opacity-0', 'scale-95');
        }
    }

    // 3. Disable pointer events after transition finishes
    setTimeout(() => {
        container.classList.remove('pointer-events-auto');
        container.classList.add('pointer-events-none');
        container.setAttribute('aria-hidden', 'true');
    }, 300);

    // 4. Restore background scroll if no other overlays are active
    activeOverlayId = null;
    document.body.classList.remove('overflow-hidden');

    if (restoreFocus && lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
        lastFocusedElement.focus();
        lastFocusedElement = null;
    }

    emit('overlay:closed', { id });
}

/**
 * Force-closes any active overlay immediately and unlocks scroll.
 * Used during page transitions to prevent overlay leakage.
 */
export function closeAllOverlays() {
    if (activeOverlayId) {
        closeOverlay(activeOverlayId, false);
    }
    document.body.classList.remove('overflow-hidden');
}

/**
 * Toggles an overlay between open and closed states.
 * @param {string} id
 */
export function toggleOverlay(id) {
    if (isOverlayOpen(id)) {
        closeOverlay(id);
    } else {
        openOverlay(id);
    }
}

/**
 * Wires global delegation for data-overlay-* attributes.
 * Works seamlessly with Blazor Static SSR and enhanced navigation.
 */
export function initOverlayDelegation() {
    if (document.__jasOverlayDelegated) return;
    document.__jasOverlayDelegated = true;

    document.addEventListener('click', (e) => {
        // Close trigger
        const closeBtn = e.target.closest('[data-overlay-close]');
        if (closeBtn) {
            e.preventDefault();
            const id = closeBtn.getAttribute('data-overlay-close') || activeOverlayId;
            if (id) closeOverlay(id);
            return;
        }

        // Open trigger
        const openBtn = e.target.closest('[data-overlay-open]');
        if (openBtn) {
            e.preventDefault();
            const id = openBtn.getAttribute('data-overlay-open');
            if (id) openOverlay(id);
            return;
        }

        // Toggle trigger
        const toggleBtn = e.target.closest('[data-overlay-toggle]');
        if (toggleBtn) {
            e.preventDefault();
            const id = toggleBtn.getAttribute('data-overlay-toggle');
            if (id) toggleOverlay(id);
            return;
        }
    });
}
