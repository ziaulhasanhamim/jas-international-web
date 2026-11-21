// Enhancements for static SSR admin upload forms and entity drafts.
// Auto editors (Products, Guides) own their uploads, slugs and rich-text state in Blazor.
(() => {
"use strict";

const dirty = new Set();
let leaving = false;

function isEligibleEntityForm(form) {
    if (!form || !(form instanceof HTMLFormElement)) return false;
    // Interactive Auto islands manage their own drafts in Blazor state
    if (form.hasAttribute('data-admin-draft')) return false;
    // Skip GET forms (search, pagination, filters)
    if (form.method && form.method.toLowerCase() === 'get') return false;
    // Skip forms explicitly marked to not save drafts
    if (form.hasAttribute('data-no-draft')) return false;
    // Skip search forms or table filters
    if (form.querySelector('input[type="search"], input[name="q"], input[name="search"], input[name="filter"]')) return false;
    // Skip quick table action forms (e.g. status toggle, delete)
    if (form.dataset.formname && (form.dataset.formname.includes('table-action') || form.dataset.formname.includes('quick-action'))) return false;

    // Must be an entity edit/create form
    const isEditPage = location.pathname.endsWith('/new') || location.pathname.includes('/edit');
    const hasEntityClass = form.classList.contains('admin-form') || form.hasAttribute('data-admin-entity-form');
    if (!isEditPage && !hasEntityClass) return false;

    // Count meaningful input elements (text, number, textarea, select, checkbox)
    let editableCount = 0;
    for (const el of form.elements) {
        if (!el.name || el.disabled || el.type === 'submit' || el.type === 'button' || el.type === 'hidden') continue;
        if (el.name.startsWith('__RequestVerificationToken')) continue;
        editableCount++;
    }
    return editableCount >= 2;
}

if (!window.jasAdminDrafts) {
    const roots = () => { for (const root of dirty) if (!root.isConnected) dirty.delete(root); return [...dirty]; };
    const hasDraft = () => roots().length > 0 || document.querySelector('[data-admin-draft][data-admin-dirty="true"]') !== null;
    const clear = () => { dirty.clear(); document.querySelectorAll('[data-admin-draft]').forEach(root => root.dataset.adminDirty = 'false'); leaving = false; };
    const save = (key, val) => {
        try { localStorage.setItem(key, typeof val === 'string' ? val : JSON.stringify(val)); } catch (e) {}
    };
    const get = (key) => {
        try { return localStorage.getItem(key); } catch (e) { return null; }
    };
    const remove = (key) => {
        try { localStorage.removeItem(key); } catch (e) {}
    };
    window.jasAdminDrafts = { clear, save, get, remove };

    const changed = event => {
        if (!(event.target instanceof Element)) return;
        const form = event.target.closest('form');
        if (!isEligibleEntityForm(form)) return;

        dirty.add(form);
        leaving = false;
        AdminEditorController.saveFormDraft(form);
    };

    document.addEventListener('input', changed);
    document.addEventListener('change', changed);

    document.addEventListener('click', event => {
        if (!(event.target instanceof Element) || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest('a[href]');
        if (!link || link.target === '_blank' || link.hasAttribute('download')) return;
        // Only save eligible entity forms
        const form = document.querySelector('form.admin-form:not([data-admin-draft]), form[data-admin-entity-form]:not([data-admin-draft])');
        if (form && isEligibleEntityForm(form)) {
            AdminEditorController.saveFormDraft(form);
        }
    }, true);

    document.addEventListener('submit', event => {
        if (event.target instanceof HTMLFormElement) {
            AdminEditorController.clearFormDraft(event.target);
            dirty.delete(event.target);
            if (!event.target.hasAttribute('data-admin-draft')) clear();
        }
    });

    window.addEventListener('beforeunload', event => {
        if (!leaving && hasDraft()) {
            event.preventDefault();
            event.returnValue = '';
        }
    });
}

if (!window.__jasAdminMenu) {
    window.__jasAdminMenu = true;
    const close = () => {
        const toggle = document.getElementById('mobile-nav-toggle');
        if (toggle) toggle.checked = false;
        document.querySelectorAll('[data-admin-menu]').forEach(button => button.setAttribute('aria-expanded', 'false'));
    };
    document.addEventListener('click', event => {
        if (!(event.target instanceof Element)) return;
        const button = event.target.closest('[data-admin-menu]');
        const toggle = document.getElementById('mobile-nav-toggle');
        if (button && toggle) {
            toggle.checked = !toggle.checked;
            document.querySelectorAll('[data-admin-menu]').forEach(item => item.setAttribute('aria-expanded', String(toggle.checked)));
        }
        if (event.target.closest('#admin-sidebar a')) close();
    });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') close(); });
    document.addEventListener('change', event => {
        if (event.target instanceof HTMLInputElement && event.target.id === 'mobile-nav-toggle') {
            document.querySelectorAll('[data-admin-menu]').forEach(button => button.setAttribute('aria-expanded', String(event.target.checked)));
        }
    });
}

async function uploadSecurityHeaders() {
    const response = await fetch('/api/admin/uploads/security', { cache: 'no-store' });
    if (!response.ok) throw new Error('Please sign in as an administrator and try again.');
    return { 'X-CSRF-TOKEN': (await response.json()).token };
}

class AdminEditorController {
    static initAll() {
        this.initUniversalImageUploaders();
        this.restoreFormDrafts();
    }

    static getFormDraftKey(form) {
        return 'jas-admin-form-draft:' + location.pathname;
    }

    static captureFormData(form) {
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

    static saveFormDraft(form) {
        if (!isEligibleEntityForm(form)) return;
        try {
            const data = this.captureFormData(form);
            const hasAny = Object.values(data).some(v => v !== '' && v !== false && v !== null && v !== undefined);
            if (hasAny) {
                localStorage.setItem(this.getFormDraftKey(form), JSON.stringify(data));
            }
        } catch (e) {
            console.warn('[AdminEditor] Could not save form draft:', e);
        }
    }

    static clearFormDraft(form) {
        try {
            localStorage.removeItem(this.getFormDraftKey(form));
        } catch (e) {}
    }

    static applyFormData(form, data) {
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

    static restoreFormDrafts() {
        const forms = document.querySelectorAll('form');
        forms.forEach(form => {
            if (!isEligibleEntityForm(form)) return;

            // Snapshot baseline form values on first load for non-reloading discard
            if (!form._originalValues) {
                form._originalValues = this.captureFormData(form);
            }

            const key = this.getFormDraftKey(form);
            let json = null;
            try { json = localStorage.getItem(key); } catch (e) { return; }
            if (!json) return;

            try {
                const data = JSON.parse(json);
                if (!data || typeof data !== 'object') return;

                // Check if draft has any differences from initial baseline
                const current = this.captureFormData(form);
                const isDifferent = Object.entries(data).some(([k, v]) => String(current[k] ?? '') !== String(v ?? ''));
                if (!isDifferent) return;

                this.applyFormData(form, data);

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
                        try { localStorage.removeItem(key); } catch (err) {}
                        dirty.delete(form);

                        // Reset form values to baseline WITHOUT reloading the page!
                        if (form._originalValues) {
                            AdminEditorController.applyFormData(form, form._originalValues);
                        } else {
                            form.reset();
                        }

                        // Smoothly animate and remove banner
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

    static initUniversalImageUploaders() {
        const uploaders = document.querySelectorAll("[data-admin-image-uploader]");
        uploaders.forEach(uploader => {
            if (uploader.dataset.initialized === "true") return;
            uploader.dataset.initialized = "true";

            const hiddenValInput = uploader.querySelector(".image-uploader-value");
            const fileInput = uploader.querySelector(".image-uploader-file-input");
            const previewImg = uploader.querySelector(".image-uploader-preview");
            const statusText = uploader.querySelector(".image-uploader-status");
            const dropzone = uploader.querySelector(".image-uploader-dropzone");
            const btnUrl = uploader.querySelector(".image-uploader-btn-url");
            const btnClear = uploader.querySelector(".image-uploader-btn-clear");
            const drawer = uploader.querySelector(".image-uploader-drawer");
            const urlInput = uploader.querySelector(".image-uploader-url-input");
            const urlDisplay = uploader.querySelector(".image-uploader-url-display");
            const progress = uploader.querySelector(".image-uploader-progress");

            if (!hiddenValInput || !previewImg) return;

            const setImage = url => {
                const trimmed = (url || "").trim();
                hiddenValInput.value = trimmed;
                if (urlInput) urlInput.value = trimmed;
                if (urlDisplay) urlDisplay.textContent = trimmed;

                if (trimmed) {
                    previewImg.src = trimmed;
                    previewImg.classList.remove("opacity-25", "grayscale");
                    if (statusText) statusText.textContent = "Image attached";
                    if (btnClear) btnClear.classList.remove("hidden");
                    if (drawer) drawer.classList.remove("hidden");
                } else {
                    previewImg.src = "/jas-logo.png";
                    previewImg.classList.add("opacity-25", "grayscale");
                    if (statusText) statusText.textContent = "No image uploaded yet";
                    if (btnClear) btnClear.classList.add("hidden");
                    if (drawer) drawer.classList.add("hidden");
                }
                const form = hiddenValInput.closest('form');
                if (form && isEligibleEntityForm(form)) {
                    AdminEditorController.saveFormDraft(form);
                }
            };

            const handleUpload = async file => {
                if (!file) return;
                if (file.size > 10 * 1024 * 1024) {
                    alert("Image exceeds 10MB file size limit.");
                    return;
                }

                const formData = new FormData();
                formData.append("file", file);

                if (progress) progress.hidden = false;

                try {
                    const response = await fetch("/api/admin/uploads/image", {
                        headers: await uploadSecurityHeaders(),
                        method: "POST",
                        body: formData,
                        signal: AbortSignal.timeout(30000)
                    });

                    if (response.ok) {
                        const data = await response.json();
                        if (data?.url) {
                            setImage(data.url);
                        }
                    } else {
                        const err = await response.json().catch(() => ({}));
                        alert(err.error || err.message || `Upload failed (HTTP ${response.status}). Please ensure file is valid SVG, PNG, WEBP, AVIF, or JPG.`);
                    }
                } catch (err) {
                    console.error("[UniversalUploader] Upload error:", err);
                    alert(`Upload failed: ${err.message || "connection error or timeout"}`);
                } finally {
                    if (progress) progress.hidden = true;
                    if (fileInput) fileInput.value = "";
                }
            };

            fileInput?.addEventListener("change", e => {
                const file = e.target.files?.[0];
                if (file) handleUpload(file);
            });

            if (dropzone) {
                ["dragenter", "dragover"].forEach(eventName => {
                    dropzone.addEventListener(eventName, e => {
                        e.preventDefault();
                        e.stopPropagation();
                        dropzone.classList.add("border-blue-500", "bg-blue-50/50");
                    });
                });

                ["dragleave", "drop"].forEach(eventName => {
                    dropzone.addEventListener(eventName, e => {
                        e.preventDefault();
                        e.stopPropagation();
                        dropzone.classList.remove("border-blue-500", "bg-blue-50/50");
                    });
                });

                dropzone.addEventListener("drop", e => {
                    e.preventDefault();
                    e.stopPropagation();
                    dropzone.classList.remove("border-blue-500", "bg-blue-50/50");
                    const file = e.dataTransfer?.files?.[0];
                    if (file) handleUpload(file);
                });
            }

            urlInput?.addEventListener("input", () => {
                setImage(urlInput.value);
            });

            btnUrl?.addEventListener("click", () => {
                if (drawer) {
                    drawer.classList.toggle("hidden");
                    if (!drawer.classList.contains("hidden")) {
                        urlInput?.focus();
                    }
                }
            });

            btnClear?.addEventListener("click", () => {
                setImage("");
            });
        });
    }
}

const JAS = window.JAS ??= {};
JAS.AdminEditor = {
    init: () => AdminEditorController.initAll(),
    initImageUploaders: () => AdminEditorController.initUniversalImageUploaders()
};

function autoInit() {
    AdminEditorController.initAll();
}

// 1. Initial page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', autoInit);
} else {
    autoInit();
}
window.addEventListener('load', autoInit);

// 2. Enhanced Navigation listener (Blazor SSR / interactive navigation)
if (window.Blazor) {
    window.Blazor.addEventListener('enhancedload', autoInit);
}
document.addEventListener('DOMContentLoaded', () => {
    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', autoInit);
    }
});
window.addEventListener('popstate', autoInit);

if (!window.__jasAdminDismiss) {
    window.__jasAdminDismiss = true;
    document.addEventListener('click', event => {
        const button = event.target instanceof Element ? event.target.closest('[data-admin-dismiss]') : null;
        if (button) button.parentElement?.remove();
    });
}
})();
