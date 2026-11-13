/**
 * JAS International - Admin Product Editor Enhancements Module (ES2024+)
 * Product image gallery manager, TinyMCE WYSIWYG specification builder,
 * and real-time SEO slug generation.
 */
class AdminEditorController {
    /**
     * Initializes the Product Media Gallery Manager
     */
    static initProductMediaManager() {
        const container = document.getElementById("product-media-manager");
        if (!container) return;

        const mainInput = document.getElementById("input-main-image");
        const galleryInput = document.getElementById("input-gallery-images");
        const cardsContainer = document.getElementById("media-cards-container");
        const fileInput = document.getElementById("file-uploader-input");
        const dropzone = document.getElementById("media-dropzone");
        const btnAddUrl = document.getElementById("btn-add-url");
        const uploadProgress = document.getElementById("media-upload-progress");

        if (!mainInput || !galleryInput || !cardsContainer) return;

        let images = [];
        const mainVal = (mainInput.value || "").trim();
        const galleryVals = (galleryInput.value || "")
            .split("\n")
            .map(s => s.trim())
            .filter(Boolean);

        if (mainVal) {
            images.push(mainVal);
        }
        for (const g of galleryVals) {
            if (!images.includes(g)) images.push(g);
        }

        const syncInputs = () => {
            if (images.length > 0) {
                mainInput.value = images[0];
                galleryInput.value = images.slice(1).join("\n");
            } else {
                mainInput.value = "";
                galleryInput.value = "";
            }
        };

        const renderCards = () => {
            cardsContainer.replaceChildren();

            if (images.length === 0) {
                const emptyNotice = document.createElement("p");
                emptyNotice.className = "text-xs text-slate-400 col-span-full py-4 text-center";
                emptyNotice.textContent = "No images added yet. Upload files or add an image URL above.";
                cardsContainer.appendChild(emptyNotice);
                return;
            }

            images.forEach((url, idx) => {
                const card = document.createElement("div");
                const isCover = idx === 0;
                card.className = `relative group rounded-2xl border p-2 bg-white flex flex-col gap-2 transition-all ${
                    isCover ? "border-blue-500 ring-2 ring-blue-500/20 shadow-md" : "border-slate-200 hover:border-slate-300"
                }`;

                card.innerHTML = `
                    <div class="relative aspect-square w-full rounded-xl overflow-hidden bg-slate-100 flex items-center justify-center">
                        <img src="${url}" alt="Product image ${idx + 1}" class="w-full h-full object-contain" onerror="this.src='/jas-logo.png'" />
                        ${isCover ? '<span class="absolute top-2 left-2 bg-blue-600 text-white text-[10px] font-bold px-2 py-0.5 rounded-full shadow-sm">⭐ Cover</span>' : ""}
                        <span class="absolute bottom-1 right-2 bg-black/60 text-white text-[10px] font-mono px-1.5 py-0.5 rounded">#${idx + 1}</span>
                    </div>
                    <div class="flex items-center justify-between gap-1 pt-1">
                        <button type="button" class="btn-move-left size-7 rounded-lg border border-slate-200 hover:bg-slate-100 flex items-center justify-center text-xs text-slate-700 disabled:opacity-30" ${idx === 0 ? "disabled" : ""} title="Move left">◀</button>
                        ${!isCover ? '<button type="button" class="btn-set-cover flex-1 text-[11px] font-semibold py-1 rounded-lg bg-blue-50 text-blue-700 hover:bg-blue-100 transition">Set Cover</button>' : '<span class="flex-1 text-center text-[11px] font-bold text-blue-600">Main</span>'}
                        <button type="button" class="btn-move-right size-7 rounded-lg border border-slate-200 hover:bg-slate-100 flex items-center justify-center text-xs text-slate-700 disabled:opacity-30" ${idx === images.length - 1 ? "disabled" : ""} title="Move right">▶</button>
                        <button type="button" class="btn-delete-img size-7 rounded-lg border border-red-200 text-red-600 hover:bg-red-50 flex items-center justify-center text-xs" title="Delete image">✕</button>
                    </div>
                `;

                card.querySelector(".btn-move-left")?.addEventListener("click", () => {
                    if (idx > 0) {
                        const [item] = images.splice(idx, 1);
                        images.splice(idx - 1, 0, item);
                        syncInputs();
                        renderCards();
                    }
                });

                card.querySelector(".btn-move-right")?.addEventListener("click", () => {
                    if (idx < images.length - 1) {
                        const [item] = images.splice(idx, 1);
                        images.splice(idx + 1, 0, item);
                        syncInputs();
                        renderCards();
                    }
                });

                card.querySelector(".btn-set-cover")?.addEventListener("click", () => {
                    const [item] = images.splice(idx, 1);
                    images.unshift(item);
                    syncInputs();
                    renderCards();
                });

                card.querySelector(".btn-delete-img")?.addEventListener("click", () => {
                    images.splice(idx, 1);
                    syncInputs();
                    renderCards();
                });

                cardsContainer.appendChild(card);
            });
        };

        const handleUploadFiles = async files => {
            if (!files?.length) return;
            const formData = new FormData();
            for (const file of files) {
                formData.append("files", file);
            }

            if (uploadProgress) {
                uploadProgress.hidden = false;
            }

            try {
                const response = await fetch("/api/admin/uploads/product-images", {
                    method: "POST",
                    body: formData,
                    signal: AbortSignal.timeout(30000)
                });

                if (response.ok) {
                    const data = await response.json();
                    if (Array.isArray(data?.urls)) {
                        for (const u of data.urls) {
                            if (!images.includes(u)) images.push(u);
                        }
                        syncInputs();
                        renderCards();
                    }
                } else {
                    alert("Upload failed. Make sure files are valid images under 10MB.");
                }
            } catch (err) {
                console.error("[MediaManager] Upload error:", err);
                alert("Upload failed due to network timeout or connection error.");
            } finally {
                if (uploadProgress) uploadProgress.hidden = true;
                if (fileInput) fileInput.value = "";
            }
        };

        fileInput?.addEventListener("change", event => {
            handleUploadFiles(event.target.files);
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

            dropzone.addEventListener("drop", event => {
                const files = event.dataTransfer?.files;
                if (files?.length) {
                    handleUploadFiles(files);
                }
            });
        }

        btnAddUrl?.addEventListener("click", () => {
            const rawUrl = prompt("Enter image HTTPS or site-relative URL (e.g. /jas-logo.png):");
            const trimmed = rawUrl?.trim();
            if (trimmed) {
                // Modern URL validation
                const isValid = trimmed.startsWith("/") || URL.canParse?.(trimmed) || trimmed.startsWith("http");
                if (isValid && !images.includes(trimmed)) {
                    images.push(trimmed);
                    syncInputs();
                    renderCards();
                } else if (!isValid) {
                    alert("Please enter a valid URL.");
                }
            }
        });

        renderCards();
    }

    /**
     * Dynamically initializes TinyMCE Rich Text Editor with electronics tech specs template
     */
    static initWysiwygEditor() {
        const textarea = document.getElementById("input-details-html");
        if (!textarea) return;

        if (!window.tinymce) {
            if (!document.getElementById("tinymce-dynamic-script")) {
                const script = document.createElement("script");
                script.id = "tinymce-dynamic-script";
                script.src = "https://cdn.jsdelivr.net/npm/tinymce@6.8.3/tinymce.min.js";
                script.referrerPolicy = "origin";
                script.onload = () => AdminEditorController.initWysiwygEditor();
                document.head.appendChild(script);
            }
            return;
        }

        try {
            window.tinymce.get("input-details-html")?.remove();
        } catch {
            // Ignore removal errors
        }

        window.tinymce.init({
            selector: "#input-details-html",
            height: 480,
            plugins: "table lists link code image",
            toolbar: "undo redo | blocks | bold italic underline | bullist numlist | table techspecs | code",
            menubar: false,
            statusbar: true,
            branding: false,
            content_style: `
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                    font-size: 14px;
                    line-height: 1.65;
                    color: #334155;
                    padding: 16px;
                }
                h2 { font-size: 18px; font-weight: bold; color: #0f172a; margin-top: 20px; margin-bottom: 8px; border-bottom: 1px solid #e2e8f0; padding-bottom: 4px; }
                h3 { font-size: 15px; font-weight: 600; color: #1e293b; margin-top: 16px; margin-bottom: 6px; }
                ul, ol { padding-left: 20px; margin-bottom: 16px; }
                li { margin-bottom: 4px; }
                table { width: 100%; border-collapse: collapse; margin: 16px 0; border: 1px solid #cbd5e1; border-radius: 8px; overflow: hidden; }
                table td, table th { border: 1px solid #e2e8f0; padding: 10px 14px; }
                table tr:nth-child(even) { background-color: #f8fafc; }
                table tr td:first-child { font-weight: 600; color: #1e293b; width: 30%; background-color: #f1f5f9; }
            `,
            setup: editor => {
                editor.ui.registry.addButton("techspecs", {
                    text: "⚡ Tech Specs Template",
                    tooltip: "Insert Standard Electronics Specification Sheet Template",
                    onAction: () => {
                        editor.insertContent(`
<h2>Key Features</h2>
<ul>
    <li>High-performance authentic gadget hardware with official warranty</li>
    <li>Fast charging, low latency and durable premium build quality</li>
    <li>Intelligent thermal management and certified safety protection</li>
</ul>
<h2>Specification Table</h2>
<table class="spec-table" style="width: 100%; border-collapse: collapse; border: 1px solid #cbd5e1;">
    <tbody>
        <tr><td style="border: 1px solid #cbd5e1; padding: 10px 14px; font-weight: 600; background: #f8fafc; width: 30%;">Brand</td><td style="border: 1px solid #cbd5e1; padding: 10px 14px;">Anker / Official Brand</td></tr>
        <tr><td style="border: 1px solid #cbd5e1; padding: 10px 14px; font-weight: 600; background: #f8fafc;">Model</td><td style="border: 1px solid #cbd5e1; padding: 10px 14px;">Product Model Number</td></tr>
        <tr><td style="border: 1px solid #cbd5e1; padding: 10px 14px; font-weight: 600; background: #f8fafc;">Output Power</td><td style="border: 1px solid #cbd5e1; padding: 10px 14px;">30W Fast Charging / PD 3.0</td></tr>
        <tr><td style="border: 1px solid #cbd5e1; padding: 10px 14px; font-weight: 600; background: #f8fafc;">Warranty</td><td style="border: 1px solid #cbd5e1; padding: 10px 14px;">7-Day Replacement + 18 Months Official Warranty</td></tr>
    </tbody>
</table>
<p>&nbsp;</p>
`);
                    }
                });

                editor.on("change input keyup", () => editor.save());
            }
        });

        textarea.closest("form")?.addEventListener("submit", () => {
            try {
                window.tinymce?.triggerSave();
            } catch {
                // Ignore save errors
            }
        });
    }

    /**
     * Initializes automatic slug generation from product name inputs
     */
    static initAutoSlugGenerator() {
        document.querySelectorAll("[data-slug-source]").forEach(sourceEl => {
            const targetSelector = sourceEl.getAttribute("data-slug-source");
            const targetEl = document.querySelector(targetSelector);
            if (!targetEl) return;

            if (targetEl.value?.trim().length > 0) {
                targetEl.dataset.manual = "true";
            }

            targetEl.addEventListener("input", () => {
                if (targetEl.value.trim().length > 0) {
                    targetEl.dataset.manual = "true";
                } else {
                    delete targetEl.dataset.manual;
                }
            });

            sourceEl.addEventListener("input", () => {
                if (targetEl.dataset.manual === "true") return;
                const slug = sourceEl.value
                    .toLowerCase()
                    .trim()
                    .replaceAll("&", "and")
                    .replaceAll("+", "-plus-")
                    .replaceAll("@", "-at-")
                    .replace(/[^a-z0-9\s-]/g, "")
                    .replace(/[\s-]+/g, "-")
                    .replace(/^-+|-+$/g, "");

                targetEl.value = slug;
                targetEl.dispatchEvent(new Event("change", { bubbles: true }));
            });
        });
    }

    /**
     * Boots all admin editor enhancements present in current DOM
     */
    static initAll() {
        this.initProductMediaManager();
        this.initWysiwygEditor();
        this.initAutoSlugGenerator();
    }
}

// Global initialization
(function (global) {
    "use strict";

    const JAS = global.JAS ??= {};
    JAS.AdminEditor = {
        init: () => AdminEditorController.initAll(),
        initMedia: () => AdminEditorController.initProductMediaManager(),
        initWysiwyg: () => AdminEditorController.initWysiwygEditor(),
        initSlugGenerator: () => AdminEditorController.initAutoSlugGenerator()
    };
})(window);
