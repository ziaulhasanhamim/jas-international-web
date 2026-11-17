// Enhancements for the remaining static SSR admin upload forms.
// Auto editors own their uploads, slugs and rich-text state in Blazor.
(() => {
"use strict";
async function uploadSecurityHeaders() {
    const response = await fetch('/api/admin/uploads/security', { cache: 'no-store' });
    if (!response.ok) throw new Error('Please sign in as an administrator and try again.');
    return { 'X-CSRF-TOKEN': (await response.json()).token };
}

class AdminEditorController {
    static initAll() { this.initUniversalImageUploaders(); }
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
