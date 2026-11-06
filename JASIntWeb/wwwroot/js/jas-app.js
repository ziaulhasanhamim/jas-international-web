(function () {
    let pending = null;
    let progress = 0;
    let active = false;
    let tick = null;
    let fade = null;
    let frame = null;
    let capsuleTimer = null;

    const indicator = () => document.getElementById("navigation-progress");
    const bar = () => indicator()?.querySelector(".navigation-progress-bar");
    const capsule = () => document.getElementById("navigation-capsule");

    const paint = () => {
        const fill = bar();
        if (fill) fill.style.transform = `scaleX(${progress})`;
    };
    const stopTimers = () => {
        clearInterval(tick);
        clearTimeout(fade);
        clearTimeout(capsuleTimer);
        cancelAnimationFrame(frame);
    };

    const startProgress = () => {
        stopTimers();
        const track = indicator();
        const fill = bar();
        const cap = capsule();
        if (!track || !fill) return;
        if (!active) {
            progress = 0;
            fill.style.transition = "none";
            paint();
        }
        active = true;
        track.hidden = false;
        track.style.opacity = "1";
        frame = requestAnimationFrame(() => {
            fill.style.transition = "";
            progress = Math.max(progress, 0.15);
            paint();
            tick = setInterval(() => {
                progress = Math.min(0.94, progress + (1 - progress) * 0.1);
                paint();
            }, 300);
        });

        // Floating Mobile Capsule: Shows after 90ms if navigation is in progress
        capsuleTimer = setTimeout(() => {
            if (active && cap) {
                cap.hidden = false;
                requestAnimationFrame(() => cap.classList.add("is-visible"));
            }
        }, 90);
    };

    const restoreForm = () => {
        if (!pending) return;
        const { form, button, label, originalLabelNodes, originalLabelText, originalDisabled, originalBusy, spinners } = pending;
        if (button) {
            if (label instanceof Element) label.replaceChildren(...originalLabelNodes);
            else if (label) label.textContent = originalLabelText;
            button.disabled = originalDisabled;
        }
        for (const { element, hidden } of spinners) {
            if (hidden === null) element.removeAttribute("hidden");
            else element.setAttribute("hidden", hidden);
        }
        if (originalBusy === null) form.removeAttribute("aria-busy");
        else form.setAttribute("aria-busy", originalBusy);
        pending = null;
    };

    const finishProgress = () => {
        stopTimers();
        active = false;
        progress = 1;
        const fill = bar();
        if (fill) fill.style.transition = "";
        paint();
        restoreForm();

        const cap = capsule();
        if (cap) {
            cap.classList.remove("is-visible");
            setTimeout(() => {
                if (!active) cap.hidden = true;
            }, 200);
        }

        fade = setTimeout(() => {
            const track = indicator();
            if (track) track.style.opacity = "0";
            fade = setTimeout(() => {
                if (track) track.hidden = true;
            }, 180);
        }, 250);
    };

    function attach() {
        window.scrollElement = function (el, delta) {
        if (!el) return;
        el.scrollBy({ left: delta, behavior: "smooth" });
    };

    if (window.Blazor) {
            window.Blazor.addEventListener("enhancednavigationstart", startProgress);
            window.Blazor.addEventListener("enhancednavigationend", finishProgress);
        }
    }

    window.scrollElement = function (el, delta) {
        if (!el) return;
        el.scrollBy({ left: delta, behavior: "smooth" });
    };

    if (window.Blazor) {
        attach();
    } else {
        document.addEventListener("DOMContentLoaded", attach);
    }

    document.addEventListener("submit", event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.dataset.submitIndication !== "true") return;

        const button = event.submitter instanceof HTMLButtonElement
            ? event.submitter : form.querySelector('button[type="submit"]');

        queueMicrotask(() => {
            restoreForm();
            const spinnerElements = [...form.querySelectorAll(".form-submit-spinner")];
            const label = button?.hasAttribute("data-pending-text")
                ? button.querySelector("[data-submit-label]")
                    ?? [...button.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim())
                : null;

            pending = {
                form,
                button,
                label,
                originalLabelNodes: label instanceof Element ? [...label.childNodes] : [],
                originalLabelText: label?.textContent,
                originalDisabled: button?.disabled,
                originalBusy: form.getAttribute("aria-busy"),
                spinners: spinnerElements.map(element => ({ element, hidden: element.getAttribute("hidden") }))
            };

            if (button) {
                if (label && !spinnerElements.some(spinner => label === spinner || label.contains?.(spinner)))
                    label.textContent = button.dataset.pendingText;
                button.disabled = true;
            }
            for (const spinner of spinnerElements) spinner.removeAttribute("hidden");
            form.setAttribute("aria-busy", "true");
        });
    }, true);

    window.jasRecordGuestOrder = function (id, token, total) {
        try {
            const key = "jas_device_orders";
            let orders = [];
            const raw = localStorage.getItem(key);
            if (raw) {
                try { orders = JSON.parse(raw); } catch (e) { orders = []; }
            }
            if (!Array.isArray(orders)) orders = [];
            orders = orders.filter(o => o.id !== id && o.token !== token);
            orders.unshift({
                id: id,
                token: token,
                total: total,
                date: new Date().toISOString()
            });
            if (orders.length > 50) orders = orders.slice(0, 50);
            localStorage.setItem(key, JSON.stringify(orders));
        } catch (e) {
            console.warn("Could not save order to local storage", e);
        }
    };

    window.jasGetGuestOrders = function () {
        try {
            const raw = localStorage.getItem("jas_device_orders");
            if (!raw) return "[]";
            return raw;
        } catch (e) {
            return "[]";
        }
    };

    window.jasFetchDeviceOrders = async function (tokensJson) {
        try {
            const tokens = JSON.parse(tokensJson);
            const res = await fetch("/api/orders/device-status", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ tokens: tokens })
            });
            if (!res.ok) return "[]";
            return await res.text();
        } catch (e) {
            return "[]";
        }
    };

    // Product Media Manager (Upload, Arrange, Set Cover)
    function initProductEditorMedia() {
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

        function syncInputs() {
            if (images.length > 0) {
                mainInput.value = images[0];
                galleryInput.value = images.slice(1).join("\n");
            } else {
                mainInput.value = "";
                galleryInput.value = "";
            }
        }

        function renderCards() {
            cardsContainer.innerHTML = "";
            if (images.length === 0) {
                cardsContainer.innerHTML = '<p class="text-xs text-slate-400 col-span-full py-4 text-center">No images added yet. Upload files or add an image URL above.</p>';
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
                        ${isCover ? '<span class="absolute top-2 left-2 bg-blue-600 text-white text-[10px] font-bold px-2 py-0.5 rounded-full shadow-sm">⭐ Cover</span>' : ''}
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
                        const tmp = images[idx];
                        images[idx] = images[idx - 1];
                        images[idx - 1] = tmp;
                        syncInputs();
                        renderCards();
                    }
                });

                card.querySelector(".btn-move-right")?.addEventListener("click", () => {
                    if (idx < images.length - 1) {
                        const tmp = images[idx];
                        images[idx] = images[idx + 1];
                        images[idx + 1] = tmp;
                        syncInputs();
                        renderCards();
                    }
                });

                card.querySelector(".btn-set-cover")?.addEventListener("click", () => {
                    const chosen = images.splice(idx, 1)[0];
                    images.unshift(chosen);
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
        }

        async function handleUploadFiles(files) {
            if (!files || files.length === 0) return;
            const formData = new FormData();
            for (const file of files) {
                formData.append("files", file);
            }

            if (uploadProgress) {
                uploadProgress.hidden = false;
            }

            try {
                const res = await fetch("/api/admin/uploads/product-images", {
                    method: "POST",
                    body: formData
                });
                if (res.ok) {
                    const data = await res.json();
                    if (data.urls && Array.isArray(data.urls)) {
                        for (const u of data.urls) {
                            if (!images.includes(u)) images.push(u);
                        }
                        syncInputs();
                        renderCards();
                    }
                } else {
                    alert("Upload failed. Make sure files are images under 10MB.");
                }
            } catch (err) {
                console.error("Upload error", err);
                alert("Upload failed due to network error.");
            } finally {
                if (uploadProgress) {
                    uploadProgress.hidden = true;
                }
                if (fileInput) fileInput.value = "";
            }
        }

        fileInput?.addEventListener("change", e => {
            handleUploadFiles(e.target.files);
        });

        if (dropzone) {
            ["dragenter", "dragover"].forEach(evt => {
                dropzone.addEventListener(evt, e => {
                    e.preventDefault();
                    e.stopPropagation();
                    dropzone.classList.add("border-blue-500", "bg-blue-50/50");
                });
            });

            ["dragleave", "drop"].forEach(evt => {
                dropzone.addEventListener(evt, e => {
                    e.preventDefault();
                    e.stopPropagation();
                    dropzone.classList.remove("border-blue-500", "bg-blue-50/50");
                });
            });

            dropzone.addEventListener("drop", e => {
                const dt = e.dataTransfer;
                if (dt && dt.files && dt.files.length > 0) {
                    handleUploadFiles(dt.files);
                }
            });
        }

        btnAddUrl?.addEventListener("click", () => {
            const url = prompt("Enter image HTTPS or site-relative URL (e.g. /jas-logo.png):");
            if (url && url.trim()) {
                const trimmed = url.trim();
                if (!images.includes(trimmed)) {
                    images.push(trimmed);
                    syncInputs();
                    renderCards();
                }
            }
        });

        renderCards();
    }

    // WYSIWYG Editor using TinyMCE Library with Table Tools & Specs Template (dynamically loaded on demand)
    function initProductEditorWysiwyg() {
        const textarea = document.getElementById("input-details-html");
        if (!textarea) return;

        if (!window.tinymce) {
            if (!document.getElementById("tinymce-dynamic-script")) {
                const script = document.createElement("script");
                script.id = "tinymce-dynamic-script";
                script.src = "https://cdn.jsdelivr.net/npm/tinymce@6.8.3/tinymce.min.js";
                script.referrerPolicy = "origin";
                script.onload = () => initProductEditorWysiwyg();
                document.head.appendChild(script);
            }
            return;
        }

        try {
            if (tinymce.get("input-details-html")) {
                tinymce.get("input-details-html").remove();
            }
        } catch (e) {
            // ignore
        }

            tinymce.init({
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
                setup: function (editor) {
                    editor.ui.registry.addButton("techspecs", {
                        text: "⚡ Tech Specs Template",
                        tooltip: "Insert Standard Electronics Specification Sheet Template",
                        onAction: function () {
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

                    editor.on("change input keyup", function () {
                        editor.save();
                    });
                }
            });

            const form = textarea.closest("form");
            form?.addEventListener("submit", function () {
                try {
                    tinymce.triggerSave();
                } catch (e) {
                    // ignore
                }
            });
        }

    function initAutoSlugGenerator() {
        document.querySelectorAll("[data-slug-source]").forEach(function (sourceEl) {
            const targetSelector = sourceEl.getAttribute("data-slug-source");
            const targetEl = document.querySelector(targetSelector);
            if (!targetEl) return;

            if (targetEl.value && targetEl.value.trim().length > 0) {
                targetEl.dataset.manual = "true";
            }

            targetEl.addEventListener("input", function () {
                if (targetEl.value.trim().length > 0) {
                    targetEl.dataset.manual = "true";
                } else {
                    delete targetEl.dataset.manual;
                }
            });

            sourceEl.addEventListener("input", function () {
                if (targetEl.dataset.manual === "true") return;
                const slug = sourceEl.value
                    .toLowerCase()
                    .trim()
                    .replace(/&/g, "and")
                    .replace(/\+/g, "-plus-")
                    .replace(/@/g, "-at-")
                    .replace(/[^a-z0-9\s-]/g, "")
                    .replace(/[\s-]+/g, "-")
                    .replace(/^-+|-+$/g, "");
                targetEl.value = slug;
                targetEl.dispatchEvent(new Event("change", { bubbles: true }));
            });
        });
    }

    function initCarousels() {
        const carousels = document.querySelectorAll("[data-carousel]");
        carousels.forEach(carousel => {
            if (carousel.__carouselInitialized) return;
            carousel.__carouselInitialized = true;

            const slides = carousel.querySelectorAll("[data-carousel-slide]");
            const dots = carousel.querySelectorAll("[data-carousel-dot]");
            const prevBtn = carousel.querySelector("[data-carousel-prev]");
            const nextBtn = carousel.querySelector("[data-carousel-next]");
            if (slides.length <= 1) return;

            let currentIndex = 0;
            let timer = null;
            let touchStartX = 0;
            let touchStartY = 0;
            let touchEndX = 0;
            let touchEndY = 0;

            function showSlide(index) {
                currentIndex = (index + slides.length) % slides.length;
                slides.forEach((slide, idx) => {
                    if (idx === currentIndex) {
                        slide.classList.remove("opacity-0", "pointer-events-none", "z-0");
                        slide.classList.add("opacity-100", "z-10");
                        slide.setAttribute("aria-hidden", "false");
                    } else {
                        slide.classList.remove("opacity-100", "z-10");
                        slide.classList.add("opacity-0", "pointer-events-none", "z-0");
                        slide.setAttribute("aria-hidden", "true");
                    }
                });

                dots.forEach((dot, idx) => {
                    if (idx === currentIndex) {
                        dot.classList.remove("w-2", "bg-slate-400/60");
                        dot.classList.add("w-6", "sm:w-7", "bg-slate-900", "shadow-xs");
                        dot.setAttribute("aria-selected", "true");
                    } else {
                        dot.classList.remove("w-6", "sm:w-7", "bg-slate-900", "shadow-xs");
                        dot.classList.add("w-2", "bg-slate-400/60");
                        dot.setAttribute("aria-selected", "false");
                    }
                });
            }

            function startTimer() {
                stopTimer();
                timer = setInterval(() => {
                    showSlide(currentIndex + 1);
                }, 5000);
            }

            function stopTimer() {
                if (timer) clearInterval(timer);
                timer = null;
            }

            prevBtn?.addEventListener("click", () => {
                showSlide(currentIndex - 1);
                startTimer();
            });

            nextBtn?.addEventListener("click", () => {
                showSlide(currentIndex + 1);
                startTimer();
            });

            dots.forEach((dot, idx) => {
                dot.addEventListener("click", () => {
                    showSlide(idx);
                    startTimer();
                });
            });

            carousel.addEventListener("mouseenter", stopTimer);
            carousel.addEventListener("mouseleave", startTimer);

            carousel.addEventListener("touchstart", e => {
                if (e.changedTouches && e.changedTouches[0]) {
                    touchStartX = e.changedTouches[0].screenX;
                    touchStartY = e.changedTouches[0].screenY;
                }
                stopTimer();
            }, { passive: true });

            carousel.addEventListener("touchend", e => {
                if (e.changedTouches && e.changedTouches[0]) {
                    touchEndX = e.changedTouches[0].screenX;
                    touchEndY = e.changedTouches[0].screenY;
                    const diffX = touchEndX - touchStartX;
                    const diffY = touchEndY - touchStartY;
                    if (Math.abs(diffX) > Math.abs(diffY) && Math.abs(diffX) > 40) {
                        if (diffX < 0) {
                            showSlide(currentIndex + 1);
                        } else {
                            showSlide(currentIndex - 1);
                        }
                    }
                }
                startTimer();
            }, { passive: true });

            startTimer();
        });
    }

    function initAdminExtensions() {
        initCarousels();
        initProductEditorMedia();
        initProductEditorWysiwyg();
        initAutoSlugGenerator();
        initPaymentVerificationStream();
        initLiveOrderTracker();
    }

    let orderTrackerSse = null;
    let orderTrackerTimer = null;

    function cleanupOrderTracker() {
        if (orderTrackerSse) {
            orderTrackerSse.close();
            orderTrackerSse = null;
        }
        if (orderTrackerTimer) {
            clearInterval(orderTrackerTimer);
            orderTrackerTimer = null;
        }
    }

    function initLiveOrderTracker() {
        const trackerEl = document.getElementById("live-order-tracker");
        if (!trackerEl) {
            cleanupOrderTracker();
            return;
        }

        const orderId = trackerEl.getAttribute("data-order-id");
        const currentStatus = trackerEl.getAttribute("data-current-status");
        if (!orderId || !currentStatus) return;

        cleanupOrderTracker();

        try {
            orderTrackerSse = new EventSource("/api/orders/" + encodeURIComponent(orderId) + "/sse");
            orderTrackerSse.onmessage = function (e) {
                try {
                    const data = JSON.parse(e.data);
                    if (data && data.status && data.status !== currentStatus) {
                        cleanupOrderTracker();
                        window.location.reload();
                    }
                } catch (err) {}
            };
        } catch (err) {}

        orderTrackerTimer = setInterval(async () => {
            try {
                const res = await fetch("/api/orders/" + encodeURIComponent(orderId) + "/status");
                if (res.ok) {
                    const data = await res.json();
                    if (data && data.status && data.status !== currentStatus) {
                        cleanupOrderTracker();
                        window.location.reload();
                    }
                }
            } catch (err) {}
        }, 2000);
    }

    let paymentSse = null;
    let paymentPollTimer = null;
    let isPaymentRedirecting = false;

    function cleanupPaymentVerification() {
        if (paymentSse) {
            paymentSse.close();
            paymentSse = null;
        }
        if (paymentPollTimer) {
            clearInterval(paymentPollTimer);
            paymentPollTimer = null;
        }
    }

    function initPaymentVerificationStream() {
        const streamEl = document.getElementById("payment-verification-stream");
        if (!streamEl) {
            cleanupPaymentVerification();
            return;
        }

        const orderId = streamEl.getAttribute("data-order-id");
        if (!orderId) return;

        cleanupPaymentVerification();
        isPaymentRedirecting = false;

        function redirectToOrder(targetId) {
            if (isPaymentRedirecting) return;
            isPaymentRedirecting = true;
            cleanupPaymentVerification();
            const label = document.getElementById("sse-status-label");
            if (label) {
                label.textContent = "Payment Verified! Redirecting to receipt...";
            }
            window.location.replace("/order/" + encodeURIComponent(targetId || orderId));
        }

        try {
            paymentSse = new EventSource("/api/orders/" + encodeURIComponent(orderId) + "/sse");
            paymentSse.onmessage = function (e) {
                try {
                    const data = JSON.parse(e.data);
                    if (data && (data.isPaymentVerified || data.isCancelled)) {
                        redirectToOrder(data.orderId || orderId);
                    }
                } catch (err) {}
            };
            paymentSse.onerror = function () {
                // If SSE is interrupted or unsupported, parallel polling below seamlessly takes over
            };
        } catch (err) {
            console.warn("[SSE] EventSource init failed, relying on polling fallback", err);
        }

        // Active 1.5s background polling fallback
        paymentPollTimer = setInterval(async () => {
            if (isPaymentRedirecting) return;
            try {
                const res = await fetch("/api/orders/" + encodeURIComponent(orderId) + "/status");
                if (res.ok) {
                    const data = await res.json();
                    if (data && (data.isPaymentVerified || data.isCancelled)) {
                        redirectToOrder(data.id || orderId);
                    }
                }
            } catch (err) {}
        }, 1500);
    }

    window.scrollElement = function (el, delta) {
        if (!el) return;
        el.scrollBy({ left: delta, behavior: "smooth" });
    };

    function cleanupAllSubscriptions() {
        cleanupPaymentVerification();
        cleanupOrderTracker();
    }

    if (window.Blazor) {
        window.Blazor.addEventListener("enhancednavigationstart", cleanupAllSubscriptions);
        window.Blazor.addEventListener("enhancednavigationend", initAdminExtensions);
    }
    document.addEventListener("DOMContentLoaded", initAdminExtensions);
    window.addEventListener("beforeunload", cleanupAllSubscriptions);
})();
