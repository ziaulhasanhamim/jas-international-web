(function () {
    let pending = null;
    let progress = 0;
    let active = false;
    let tick = null;
    let fade = null;
    let frame = null;

    const indicator = () => document.getElementById("navigation-progress");
    const bar = () => indicator()?.querySelector(".navigation-progress-bar");
    const paint = () => {
        const fill = bar();
        if (fill) fill.style.transform = `scaleX(${progress})`;
    };
    const stopTimers = () => {
        clearInterval(tick);
        clearTimeout(fade);
        cancelAnimationFrame(frame);
    };

    const startProgress = () => {
        stopTimers();
        const track = indicator();
        const fill = bar();
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
            progress = Math.max(progress, 0.12);
            paint();
            tick = setInterval(() => {
                progress = Math.min(0.94, progress + (1 - progress) * 0.1);
                paint();
            }, 350);
        });
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
        fade = setTimeout(() => {
            const track = indicator();
            if (track) track.style.opacity = "0";
            fade = setTimeout(() => {
                if (track) track.hidden = true;
            }, 180);
        }, 250);
    };

    function attach() {
        if (window.Blazor) {
            window.Blazor.addEventListener("enhancednavigationstart", startProgress);
            window.Blazor.addEventListener("enhancednavigationend", finishProgress);
        }
    }

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
})();
