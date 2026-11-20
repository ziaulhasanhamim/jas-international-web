// Enhancements for the remaining static SSR admin upload forms.
// Auto editors own their uploads, slugs and rich-text state in Blazor.
(() => {
"use strict";
// A single delegated guard works with static enhanced navigation and Auto islands.
if (!window.jasAdminDrafts) {
    const dirty = new Set();
    let leaving = false;
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
        const root = event.target.closest('[data-admin-draft], form.admin-form, main.admin-main form');
        if (root) {
            dirty.add(root);
            leaving = false;
            if (root instanceof HTMLFormElement && !root.hasAttribute('data-admin-draft')) {
                AdminEditorController.saveFormDraft(root);
            }
        }
    };
    document.addEventListener('input', changed);
    document.addEventListener('change', changed);
    document.addEventListener('click', event => {
        if (!(event.target instanceof Element) || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest('a[href]');
        if (!link || link.target === '_blank' || link.hasAttribute('download')) return;
        const form = document.querySelector('form.admin-form:not([data-admin-draft]), main.admin-main form:not([data-admin-draft])');
        if (form) AdminEditorController.saveFormDraft(form);
    }, true);
    document.addEventListener('submit', event => {
        if (event.target instanceof HTMLFormElement) {
            AdminEditorController.clearFormDraft(event.target);
            if (!event.target.hasAttribute('data-admin-draft')) clear();
        }
    });
    window.addEventListener('beforeunload', event => { if (!leaving && hasDraft()) { event.preventDefault(); event.returnValue = ''; } });
}
if (!window.__jasAdminMenu) {
    window.__jasAdminMenu = true;
    const close = () => { const toggle = document.getElementById('mobile-nav-toggle'); if (toggle) toggle.checked = false; document.querySelectorAll('[data-admin-menu]').forEach(button => button.setAttribute('aria-expanded', 'false')); };
    document.addEventListener('click', event => {
        if (!(event.target instanceof Element)) return;
        const button = event.target.closest('[data-admin-menu]');
        const toggle = document.getElementById('mobile-nav-toggle');
        if (button && toggle) { toggle.checked = !toggle.checked; document.querySelectorAll('[data-admin-menu]').forEach(item => item.setAttribute('aria-expanded', String(toggle.checked))); }
        if (event.target.closest('#admin-sidebar a')) close();
    });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') close(); });
    document.addEventListener('change', event => { if (event.target instanceof HTMLInputElement && event.target.id === 'mobile-nav-toggle') document.querySelectorAll('[data-admin-menu]').forEach(button => button.setAttribute('aria-expanded', String(event.target.checked))); });
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
    static saveFormDraft(form) {
        if (!form || form.hasAttribute('data-admin-draft')) return;
        try {
            const data = {};
            let hasAny = false;
            for (const el of form.elements) {
                if (!el.name || el.disabled || el.type === 'submit' || el.type === 'button' || el.type === 'file' || el.type === 'password') continue;
                if (el.name.startsWith('__RequestVerificationToken')) continue;
                if (el.type === 'checkbox') {
                    data[el.name] = el.checked;
                    hasAny = true;
                } else if (el.type === 'radio') {
                    if (el.checked) { data[el.name] = el.value; hasAny = true; }
                } else {
                    data[el.name] = el.value;
                    if (el.value) hasAny = true;
                }
            }
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
    static restoreFormDrafts() {
        const forms = document.querySelectorAll('form.admin-form:not([data-admin-draft]), main.admin-main form:not([data-admin-draft])');
        forms.forEach(form => {
            const key = this.getFormDraftKey(form);
            let json = null;
            try { json = localStorage.getItem(key); } catch (e) { return; }
            if (!json) return;

            try {
                const data = JSON.parse(json);
                if (!data || typeof data !== 'object') return;
                let restoredCount = 0;

                for (const [name, val] of Object.entries(data)) {
                    const el = form.elements[name];
                    if (!el) continue;

                    if (el instanceof RadioNodeList) {
                        for (const radio of el) {
                            if (radio.value === String(val)) {
                                if (!radio.checked) { radio.checked = true; restoredCount++; }
                            }
                        }
                    } else if (el.type === 'checkbox') {
                        const boolVal = Boolean(val);
                        if (el.checked !== boolVal) { el.checked = boolVal; restoredCount++; }
                    } else {
                        const strVal = String(val ?? '');
                        if (el.value !== strVal) {
                            el.value = strVal;
                            restoredCount++;

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
                                }
                            }
                        }
                    }
                }

                if (restoredCount > 0 && !form.querySelector('.admin-draft-restored-banner')) {
                    const banner = document.createElement('div');
                    banner.className = 'admin-draft-restored-banner flex items-center justify-between p-3 mb-4 bg-amber-50 border border-amber-200 rounded-xl text-xs text-amber-800';
                    banner.setAttribute('role', 'status');
                    banner.innerHTML = `
                        <span class="flex items-center gap-1.5 font-medium">
                            <svg class="size-4 text-amber-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                            Restored unsaved draft from local storage.
                        </span>
                        <button type="button" class="admin-discard-draft-btn text-xs font-semibold text-amber-900 underline hover:text-amber-950 ml-3 cursor-pointer">Discard draft</button>
                    `;
                    banner.querySelector('.admin-discard-draft-btn')?.addEventListener('click', () => {
                        try { localStorage.removeItem(key); } catch (e) {}
                        banner.remove();
                        location.reload();
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
                if (form && !form.hasAttribute('data-admin-draft')) {
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
JAS.AdminEditor = { init: () => AdminEditorController.initAll(), initImageUploaders: () => AdminEditorController.initUniversalImageUploaders() };
if (!window.__jasAdminDismiss) {
    window.__jasAdminDismiss = true;
    document.addEventListener('click', event => {
        const button = event.target instanceof Element ? event.target.closest('[data-admin-dismiss]') : null;
        if (button) button.parentElement?.remove();
    });
}
})();
