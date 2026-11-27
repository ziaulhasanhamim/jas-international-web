/**
 * JAS International - Admin Editor & Delegated Actions Module (ES2024+)
 * Pure functional ES module managing static SSR admin uploaders, entity drafts,
 * and declarative data-* admin interactions.
 */

import { request } from '../core/api.js';

const dirtyForms = new Set();
let leaving = false;
let delegationBound = false;
let lifecycleBound = false;
const debounceTimers = new WeakMap();

function isEligibleEntityForm(form) {
    if (!form || !(form instanceof HTMLFormElement)) return false;
    if (form.hasAttribute('data-admin-draft')) return false;
    if (form.method && form.method.toLowerCase() === 'get') return false;
    if (form.hasAttribute('data-no-draft')) return false;
    if (form.querySelector('input[type="search"], input[name="q"], input[name="search"], input[name="filter"]')) return false;
    if (form.dataset.formname && (form.dataset.formname.includes('table-action') || form.dataset.formname.includes('quick-action') || form.dataset.formname.startsWith('delete-'))) return false;

    const path = (location.pathname || '').toLowerCase();
    const isEditOrCreatePath = (path.endsWith('/new') || path.includes('/edit')) &&
        (path.startsWith('/admin/coupons') || path.startsWith('/admin/categories') || path.startsWith('/admin/brands') || path.startsWith('/admin/carousel'));
    if (!isEditOrCreatePath) return false;

    const hasEntityClass = form.classList.contains('admin-form') || form.hasAttribute('data-admin-entity-form');
    if (!hasEntityClass) return false;

    let editableCount = 0;
    for (const el of form.elements) {
        if (!el.name || el.disabled || el.type === 'submit' || el.type === 'button' || el.type === 'hidden') continue;
        if (el.name.startsWith('__RequestVerificationToken')) continue;
        editableCount++;
    }
    return editableCount >= 2;
}

function getFormDraftKey() {
    return 'jas-admin-form-draft:' + location.pathname;
}

function captureFormData(form) {
    const data = {};
    for (const el of form.elements) {
        if (!el.name || el.disabled || el.type === 'submit' || el.type === 'button' || el.type === 'file' || el.type === 'password') continue;
        if (el.name.startsWith('__RequestVerificationToken')) continue;
        if (el.type === 'checkbox') {
            data[el.name] = el.checked;
        } else if (el.type === 'radio') {
            if (el.checked) data[el.name] = el.value;
        } else {
            data[el.name] = el.value;
        }
    }
    return data;
}

function saveFormDraft(form) {
    if (!isEligibleEntityForm(form)) return;
    try {
        const data = captureFormData(form);
        const hasAny = Object.values(data).some(v => v !== '' && v !== false && v !== null && v !== undefined);
        if (hasAny) {
            localStorage.setItem(getFormDraftKey(), JSON.stringify(data));
        }
    } catch (e) {
        console.warn('[AdminEditor] Could not save form draft:', e);
    }
}

function clearFormDraft() {
    try {
        localStorage.removeItem(getFormDraftKey());
    } catch {}
}

function applyFormData(form, data) {
    for (const [name, val] of Object.entries(data)) {
        const el = form.elements[name];
        if (!el) continue;

        if (el instanceof RadioNodeList) {
            for (const radio of el) {
                radio.checked = (radio.value === String(val));
            }
        } else if (el.type === 'checkbox') {
            el.checked = Boolean(val);
        } else {
            const strVal = String(val ?? '');
            el.value = strVal;

            const uploader = el.closest('[data-admin-image-uploader]');
            if (uploader) {
                const previewImg = uploader.querySelector('.image-uploader-preview');
                const statusText = uploader.querySelector('.image-uploader-status');
                const btnClear = uploader.querySelector('.image-uploader-btn-clear');
                const drawer = uploader.querySelector('.image-uploader-drawer');
                const urlInput = uploader.querySelector('.image-uploader-url-input');
                const urlDisplay = uploader.querySelector('.image-uploader-url-display');
                if (strVal) {
                    if (previewImg) { previewImg.src = strVal; previewImg.classList.remove('opacity-25', 'grayscale'); }
                    if (statusText) statusText.textContent = 'Image attached';
                    if (btnClear) btnClear.classList.remove('hidden');
                    if (drawer) drawer.classList.remove('hidden');
                    if (urlInput) urlInput.value = strVal;
                    if (urlDisplay) urlDisplay.textContent = strVal;
                } else {
                    if (previewImg) { previewImg.src = '/jas-logo.png'; previewImg.classList.add('opacity-25', 'grayscale'); }
                    if (statusText) statusText.textContent = 'No image uploaded yet';
                    if (btnClear) btnClear.classList.add('hidden');
                    if (drawer) drawer.classList.add('hidden');
                    if (urlInput) urlInput.value = '';
                    if (urlDisplay) urlDisplay.textContent = '';
                }
            }
        }
    }
}

function purgeStaleDrafts() {
    try {
        const toDelete = [];
        for (let i = 0; i < localStorage.length; i++) {
            const k = localStorage.key(i);
            if (!k) continue;
            if (k.startsWith('jas-admin-form-draft:')) {
                const p = k.substring('jas-admin-form-draft:'.length).toLowerCase();
                const isAllowed = (p.endsWith('/new') || p.includes('/edit')) &&
                    (p.startsWith('/admin/coupons') || p.startsWith('/admin/categories') || p.startsWith('/admin/brands') || p.startsWith('/admin/carousel'));
                if (!isAllowed) toDelete.push(k);
            }
        }
        toDelete.forEach(k => {
            try { localStorage.removeItem(k); } catch {}
        });
    } catch {}

    document.querySelectorAll('.admin-draft-restored-banner').forEach(b => {
        const form = b.closest('form');
        if (!isEligibleEntityForm(form)) b.remove();
    });
}

function restoreFormDrafts() {
    document.querySelectorAll('form').forEach(form => {
        if (!isEligibleEntityForm(form)) return;

        if (!form._originalValues) {
            form._originalValues = captureFormData(form);
        }

        const key = getFormDraftKey();
        let json = null;
        try { json = localStorage.getItem(key); } catch { return; }
        if (!json) return;

        try {
            const data = JSON.parse(json);
            if (!data || typeof data !== 'object') return;

            const current = captureFormData(form);
            const isDifferent = Object.entries(data).some(([k, v]) => String(current[k] ?? '') !== String(v ?? ''));
            if (!isDifferent) return;

            applyFormData(form, data);

            if (!form.querySelector('.admin-draft-restored-banner')) {
                const banner = document.createElement('div');
                banner.className = 'admin-draft-restored-banner flex items-center justify-between p-3.5 mb-5 bg-amber-50 border border-amber-200 rounded-2xl text-xs text-amber-900 shadow-xs';
                banner.setAttribute('role', 'status');
                banner.innerHTML = `
                    <span class="flex items-center gap-2 font-medium">
                        <svg class="size-4 text-amber-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                        <span>Restored unsaved draft from your previous session.</span>
                    </span>
                    <button type="button" class="admin-discard-draft-btn px-2.5 py-1 text-xs font-bold text-amber-900 bg-amber-100 hover:bg-amber-200 rounded-lg transition cursor-pointer">Discard Draft</button>
                `;

                banner.querySelector('.admin-discard-draft-btn')?.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    try { localStorage.removeItem(key); } catch {}
                    dirtyForms.delete(form);

                    if (form._originalValues) {
                        applyFormData(form, form._originalValues);
                    } else {
                        form.reset();
                    }

                    banner.style.transition = 'all 200ms ease';
                    banner.style.opacity = '0';
                    banner.style.transform = 'translateY(-4px)';
                    setTimeout(() => banner.remove(), 200);
                });

                form.prepend(banner);
            }
        } catch (e) {
            console.warn('[AdminEditor] Error restoring draft:', e);
        }
    });
}

export function initUniversalImageUploaders() {
    document.querySelectorAll('[data-admin-image-uploader]').forEach(uploader => {
        if (uploader.dataset.initialized === 'true') return;
        uploader.dataset.initialized = 'true';

        const hiddenValInput = uploader.querySelector('.image-uploader-value');
        const fileInput = uploader.querySelector('.image-uploader-file-input');
        const previewImg = uploader.querySelector('.image-uploader-preview');
        const statusText = uploader.querySelector('.image-uploader-status');
        const dropzone = uploader.querySelector('.image-uploader-dropzone');
        const btnUrl = uploader.querySelector('.image-uploader-btn-url');
        const btnClear = uploader.querySelector('.image-uploader-btn-clear');
        const drawer = uploader.querySelector('.image-uploader-drawer');
        const urlInput = uploader.querySelector('.image-uploader-url-input');
        const urlDisplay = uploader.querySelector('.image-uploader-url-display');
        const progress = uploader.querySelector('.image-uploader-progress');

        if (!hiddenValInput || !previewImg) return;

        const setImage = url => {
            const trimmed = (url || '').trim();
            hiddenValInput.value = trimmed;
            if (urlInput) urlInput.value = trimmed;
            if (urlDisplay) urlDisplay.textContent = trimmed;

            if (trimmed) {
                previewImg.src = trimmed;
                previewImg.classList.remove('opacity-25', 'grayscale');
                if (statusText) statusText.textContent = 'Image attached';
                if (btnClear) btnClear.classList.remove('hidden');
                if (drawer) drawer.classList.remove('hidden');
            } else {
                previewImg.src = '/jas-logo.png';
                previewImg.classList.add('opacity-25', 'grayscale');
                if (statusText) statusText.textContent = 'No image uploaded yet';
                if (btnClear) btnClear.classList.add('hidden');
                if (drawer) drawer.classList.add('hidden');
            }
            const form = hiddenValInput.closest('form');
            if (form && isEligibleEntityForm(form)) {
                saveFormDraft(form);
            }
        };

        const handleUpload = async file => {
            if (!file) return;
            if (file.size > 10 * 1024 * 1024) {
                alert('Image exceeds 10MB file size limit.');
                return;
            }

            const formData = new FormData();
            formData.append('file', file);

            if (progress) progress.hidden = false;

            try {
                const res = await request('/api/admin/uploads/image', {
                    method: 'POST',
                    body: formData,
                    includeCsrf: true,
                    csrfUrl: '/api/admin/uploads/security',
                    signal: AbortSignal.timeout(30000)
                });

                if (res.ok && res.data?.url) {
                    setImage(res.data.url);
                } else {
                    alert(res.error || `Upload failed (HTTP ${res.status}). Please ensure file is valid SVG, PNG, WEBP, AVIF, or JPG.`);
                }
            } finally {
                if (progress) progress.hidden = true;
                if (fileInput) fileInput.value = '';
            }
        };

        fileInput?.addEventListener('change', e => {
            const file = e.target.files?.[0];
            if (file) handleUpload(file);
        });

        if (dropzone) {
            ['dragenter', 'dragover'].forEach(eventName => {
                dropzone.addEventListener(eventName, e => {
                    e.preventDefault();
                    e.stopPropagation();
                    dropzone.classList.add('border-blue-500', 'bg-blue-50/50');
                });
            });

            ['dragleave', 'drop'].forEach(eventName => {
                dropzone.addEventListener(eventName, e => {
                    e.preventDefault();
                    e.stopPropagation();
                    dropzone.classList.remove('border-blue-500', 'bg-blue-50/50');
                });
            });

            dropzone.addEventListener('drop', e => {
                e.preventDefault();
                e.stopPropagation();
                dropzone.classList.remove('border-blue-500', 'bg-blue-50/50');
                const file = e.dataTransfer?.files?.[0];
                if (file) handleUpload(file);
            });
        }

        urlInput?.addEventListener('input', () => setImage(urlInput.value));

        btnUrl?.addEventListener('click', () => {
            if (drawer) {
                drawer.classList.toggle('hidden');
                if (!drawer.classList.contains('hidden')) {
                    urlInput?.focus();
                }
            }
        });

        btnClear?.addEventListener('click', () => setImage(''));
    });
}

function setupDraftBridge() {
    if (window.jasAdminDrafts) return;

    const connectedDirtyRoots = () => {
        for (const root of dirtyForms) {
            if (!root.isConnected) dirtyForms.delete(root);
        }
        return [...dirtyForms];
    };
    const hasDraft = () => connectedDirtyRoots().length > 0 || document.querySelector('[data-admin-draft][data-admin-dirty="true"]') !== null;
    const clear = () => {
        dirtyForms.clear();
        document.querySelectorAll('[data-admin-draft]').forEach(root => { root.dataset.adminDirty = 'false'; });
        leaving = false;
    };
    const save = (key, val) => {
        try { localStorage.setItem(key, typeof val === 'string' ? val : JSON.stringify(val)); } catch {}
    };
    const get = (key) => {
        try { return localStorage.getItem(key); } catch { return null; }
    };
    const remove = (key) => {
        try { localStorage.removeItem(key); } catch {}
    };

    window.jasAdminDrafts = { clear, save, get, remove };
}

function bindAdminDelegation() {
    if (delegationBound) return;
    delegationBound = true;

    const closeSidebar = () => {
        const toggle = document.getElementById('mobile-nav-toggle');
        if (toggle) toggle.checked = false;
        document.querySelectorAll('[data-admin-menu]').forEach(button => button.setAttribute('aria-expanded', 'false'));
    };

    const onFormFieldChange = event => {
        if (!(event.target instanceof Element)) return;
        const form = event.target.closest('form');
        if (!isEligibleEntityForm(form)) return;
        dirtyForms.add(form);
        leaving = false;
        saveFormDraft(form);
    };

    document.addEventListener('input', event => {
        onFormFieldChange(event);

        const debounceInput = event.target instanceof HTMLElement ? event.target.closest('[data-debounce-submit]') : null;
        if (debounceInput instanceof HTMLInputElement || debounceInput instanceof HTMLTextAreaElement) {
            const delay = parseInt(debounceInput.dataset.debounceSubmit || '350', 10) || 350;
            clearTimeout(debounceTimers.get(debounceInput));
            const timer = setTimeout(() => debounceInput.form?.requestSubmit(), delay);
            debounceTimers.set(debounceInput, timer);
        }
    });

    document.addEventListener('change', event => {
        onFormFieldChange(event);

        if (event.target instanceof HTMLInputElement && event.target.id === 'mobile-nav-toggle') {
            document.querySelectorAll('[data-admin-menu]').forEach(button => button.setAttribute('aria-expanded', String(event.target.checked)));
        }

        const autoSubmit = event.target instanceof HTMLElement ? event.target.closest('[data-auto-submit]') : null;
        if (autoSubmit && 'form' in autoSubmit && autoSubmit.form) {
            autoSubmit.form.requestSubmit();
        }
    });

    document.addEventListener('click', async event => {
        if (!(event.target instanceof Element)) return;

        // Draft checkpoint before anchor navigation
        if (event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
            const link = event.target.closest('a[href]');
            if (link && link.target !== '_blank' && !link.hasAttribute('download')) {
                const form = document.querySelector('form.admin-form:not([data-admin-draft]), form[data-admin-entity-form]:not([data-admin-draft])');
                if (form && isEligibleEntityForm(form)) {
                    saveFormDraft(form);
                }
            }
        }

        // Mobile sidebar drawer toggle / close
        const menuBtn = event.target.closest('[data-admin-menu]');
        const toggle = document.getElementById('mobile-nav-toggle');
        if (menuBtn && toggle) {
            toggle.checked = !toggle.checked;
            document.querySelectorAll('[data-admin-menu]').forEach(item => item.setAttribute('aria-expanded', String(toggle.checked)));
            return;
        }
        if (event.target.closest('#admin-sidebar a')) {
            closeSidebar();
        }

        // Confirmation prompt for destructive form buttons
        const confirmBtn = event.target.closest('[data-confirm]');
        if (confirmBtn) {
            const message = confirmBtn.getAttribute('data-confirm');
            if (message && !window.confirm(message)) {
                event.preventDefault();
                event.stopPropagation();
                return;
            }
        }

        // Select-all checkboxes in admin QuickGrids
        const selectAll = event.target.closest('[data-select-all]');
        if (selectAll instanceof HTMLInputElement) {
            const selector = selectAll.getAttribute('data-select-all');
            if (selector) {
                document.querySelectorAll(selector).forEach(cb => {
                    if (cb instanceof HTMLInputElement) cb.checked = selectAll.checked;
                });
            }
            return;
        }

        // Dismiss banners / alerts
        const dismissBtn = event.target.closest('[data-admin-dismiss], [data-dismiss-target]');
        if (dismissBtn) {
            const selector = dismissBtn.getAttribute('data-dismiss-target');
            const target = selector ? dismissBtn.closest(selector) : dismissBtn.parentElement;
            target?.remove();
            return;
        }

        // Copy text from target input or data attribute
        const copyTargetBtn = event.target.closest('[data-copy-target]');
        if (copyTargetBtn) {
            const selector = copyTargetBtn.getAttribute('data-copy-target');
            const input = selector ? document.querySelector(selector) : null;
            const value = input && 'value' in input ? input.value : '';
            if (value) {
                try {
                    await navigator.clipboard.writeText(value);
                    const feedback = copyTargetBtn.getAttribute('data-copy-message');
                    if (feedback) alert(feedback);
                } catch {}
            }
            return;
        }

        const copyValueBtn = event.target.closest('[data-copy-value]');
        if (copyValueBtn) {
            const value = copyValueBtn.getAttribute('data-copy-value') || '';
            if (value) {
                try {
                    await navigator.clipboard.writeText(value);
                    const feedback = copyValueBtn.getAttribute('data-copy-message');
                    if (feedback) alert(feedback);
                } catch {}
            }
            return;
        }

        // Print page trigger
        if (event.target.closest('[data-print-page]')) {
            window.print();
            return;
        }

        // Close <details> dialog overlay via close button or backdrop click
        const closeDetailsBtn = event.target.closest('[data-close-details]');
        if (closeDetailsBtn) {
            closeDetailsBtn.closest('details')?.removeAttribute('open');
            return;
        }
        const detailsBackdrop = event.target.closest('[data-details-backdrop]');
        if (detailsBackdrop && event.target === detailsBackdrop) {
            detailsBackdrop.closest('details')?.removeAttribute('open');
        }
    });

    document.addEventListener('submit', event => {
        if (event.target instanceof HTMLFormElement) {
            clearFormDraft();
            dirtyForms.delete(event.target);
            if (!event.target.hasAttribute('data-admin-draft')) {
                window.jasAdminDrafts?.clear();
            }
        }
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') closeSidebar();
    });

    window.addEventListener('beforeunload', event => {
        const connectedDirty = [...dirtyForms].some(f => f.isConnected);
        const interactiveDirty = document.querySelector('[data-admin-draft][data-admin-dirty="true"]') !== null;
        if (!leaving && (connectedDirty || interactiveDirty)) {
            event.preventDefault();
            event.returnValue = '';
        }
    });
}

export function initAdminEditor() {
    setupDraftBridge();
    bindAdminDelegation();
    purgeStaleDrafts();
    initUniversalImageUploaders();
    restoreFormDrafts();
}

function bindLifecycle() {
    if (lifecycleBound) return;
    lifecycleBound = true;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAdminEditor);
    } else {
        initAdminEditor();
    }
    window.addEventListener('load', initAdminEditor);
    window.addEventListener('popstate', initAdminEditor);

    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', initAdminEditor);
    } else {
        document.addEventListener('DOMContentLoaded', () => {
            window.Blazor?.addEventListener('enhancedload', initAdminEditor);
        }, { once: true });
    }
}

bindLifecycle();
