/**
 * JAS International - Navigation & Progress Module (ES2024+)
 * Manages dual progress indicators, animated mobile loading capsules,
 * form submit indicators with microtask queues, and smooth element scrolling.
 */
class NavigationController {
    #pendingForm = null;
    #progress = 0;
    #isActive = false;
    #tickTimer = null;
    #fadeTimer = null;
    #animFrame = null;
    #capsuleTimer = null;

    #indicator = () => document.getElementById("navigation-progress");
    #progressBar = () => this.#indicator()?.querySelector(".navigation-progress-bar");
    #capsule = () => document.getElementById("navigation-capsule");

    /**
     * Updates the CSS transform scale on the progress bar element
     */
    #paint() {
        const bar = this.#progressBar();
        if (bar) {
            bar.style.transform = `scaleX(${this.#progress})`;
        }
    }

    /**
     * Cancels any running animation timers or requestAnimationFrame callbacks
     */
    #stopTimers() {
        clearInterval(this.#tickTimer);
        clearTimeout(this.#fadeTimer);
        clearTimeout(this.#capsuleTimer);
        if (this.#animFrame !== null) {
            cancelAnimationFrame(this.#animFrame);
            this.#animFrame = null;
        }
    }

    /**
     * Starts progress animation during route transition
     */
    start() {
        this.#stopTimers();
        const track = this.#indicator();
        const bar = this.#progressBar();
        const capsule = this.#capsule();

        if (!track || !bar) return;

        if (!this.#isActive) {
            this.#progress = 0;
            bar.style.transition = "none";
            this.#paint();
        }

        this.#isActive = true;
        track.hidden = false;
        track.style.opacity = "1";

        this.#animFrame = requestAnimationFrame(() => {
            bar.style.transition = "";
            this.#progress = Math.max(this.#progress, 0.15);
            this.#paint();

            this.#tickTimer = setInterval(() => {
                this.#progress = Math.min(0.94, this.#progress + (1 - this.#progress) * 0.1);
                this.#paint();
            }, 300);
        });

        // Show floating mobile capsule after 90ms if navigation is still in flight
        this.#capsuleTimer = setTimeout(() => {
            if (this.#isActive && capsule) {
                capsule.hidden = false;
                requestAnimationFrame(() => capsule.classList.add("is-visible"));
            }
        }, 90);
    }

    /**
     * Completes and smoothly dismisses the progress indicators
     */
    finish() {
        this.#stopTimers();
        this.#isActive = false;
        this.#progress = 1;

        const bar = this.#progressBar();
        if (bar) {
            bar.style.transition = "";
        }
        this.#paint();
        this.restorePendingForm();

        const capsule = this.#capsule();
        if (capsule) {
            capsule.classList.remove("is-visible");
            setTimeout(() => {
                if (!this.#isActive) capsule.hidden = true;
            }, 200);
        }

        this.#fadeTimer = setTimeout(() => {
            const track = this.#indicator();
            if (track) track.style.opacity = "0";
            this.#fadeTimer = setTimeout(() => {
                if (track) track.hidden = true;
            }, 180);
        }, 250);
    }

    /**
     * Restores submit buttons and spinners to their original state
     */
    restorePendingForm() {
        if (!this.#pendingForm) return;

        const { form, button, label, originalLabelNodes, originalLabelText, originalDisabled, originalBusy, spinners } = this.#pendingForm;

        if (button) {
            if (label instanceof Element) {
                label.replaceChildren(...originalLabelNodes);
            } else if (label) {
                label.textContent = originalLabelText;
            }
            button.disabled = originalDisabled;
        }

        for (const { element, wasHidden } of spinners) {
            element.toggleAttribute("hidden", wasHidden);
        }

        if (originalBusy === null) {
            form.removeAttribute("aria-busy");
        } else {
            form.setAttribute("aria-busy", originalBusy);
        }

        this.#pendingForm = null;
    }

    /**
     * Intercepts form submissions for forms with data-submit-indication="true"
     */
    handleFormSubmit(event) {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.dataset.submitIndication !== "true") return;

        const button = event.submitter instanceof HTMLButtonElement
            ? event.submitter
            : form.querySelector('button[type="submit"]');

        queueMicrotask(() => {
            this.restorePendingForm();
            const spinnerElements = [...form.querySelectorAll(".form-submit-spinner")];
            const label = button?.hasAttribute("data-pending-text")
                ? button.querySelector("[data-submit-label]")
                    ?? [...button.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim())
                : null;

            this.#pendingForm = {
                form,
                button,
                label,
                originalLabelNodes: label instanceof Element ? [...label.childNodes] : [],
                originalLabelText: label?.textContent,
                originalDisabled: button?.disabled,
                originalBusy: form.getAttribute("aria-busy"),
                spinners: spinnerElements.map(element => ({ element, wasHidden: element.hasAttribute("hidden") }))
            };

            if (button) {
                if (label && !spinnerElements.some(spinner => label === spinner || label.contains?.(spinner))) {
                    label.textContent = button.dataset.pendingText ?? "";
                }
                button.disabled = true;
            }

            for (const spinner of spinnerElements) {
                spinner.removeAttribute("hidden");
            }
            form.setAttribute("aria-busy", "true");
        });
    }

    /**
     * Smoothly scrolls an element horizontally
     */
    static scrollElement(element, delta) {
        element?.scrollBy({ left: delta, behavior: "smooth" });
    }
}

// Global initialization
(function (global) {
    "use strict";

    const controller = new NavigationController();
    document.addEventListener("submit", event => controller.handleFormSubmit(event), true);

    const JAS = global.JAS ??= {};
    JAS.Navigation = {
        start: () => controller.start(),
        finish: () => controller.finish(),
        restoreForm: () => controller.restorePendingForm(),
        scrollElement: NavigationController.scrollElement
    };

    global.scrollElement = NavigationController.scrollElement;
})(window);
