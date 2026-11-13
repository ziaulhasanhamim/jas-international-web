/**
 * JAS International - Interactive Carousel Module (ES2024+)
 * Touch-enabled swipe carousel with auto-rotation, gesture thresholds,
 * and AbortController-managed event lifecycles.
 */
class CarouselInstance {
    static #AUTO_ROTATE_DELAY_MS = 5000;
    static #SWIPE_THRESHOLD_PX = 40;

    #root;
    #slides;
    #dots;
    #prevBtn;
    #nextBtn;
    #currentIndex = 0;
    #timer = null;
    #abortController = new AbortController();

    /**
     * @param {HTMLElement} root Carousel container element
     */
    constructor(root) {
        this.#root = root;
        this.#slides = [...root.querySelectorAll("[data-carousel-slide]")];
        this.#dots = [...root.querySelectorAll("[data-carousel-dot]")];
        this.#prevBtn = root.querySelector("[data-carousel-prev]");
        this.#nextBtn = root.querySelector("[data-carousel-next]");

        if (this.#slides.length <= 1) return;

        this.#bindEvents();
        this.#startTimer();
    }

    /**
     * Transitions to the specified slide index
     * @param {number} targetIndex Target slide index
     */
    showSlide(targetIndex) {
        const count = this.#slides.length;
        this.#currentIndex = ((targetIndex % count) + count) % count;

        this.#slides.forEach((slide, idx) => {
            const isTarget = idx === this.#currentIndex;
            slide.classList.toggle("opacity-100", isTarget);
            slide.classList.toggle("z-10", isTarget);
            slide.classList.toggle("opacity-0", !isTarget);
            slide.classList.toggle("pointer-events-none", !isTarget);
            slide.classList.toggle("z-0", !isTarget);
            slide.setAttribute("aria-hidden", String(!isTarget));
        });

        this.#dots.forEach((dot, idx) => {
            const isTarget = idx === this.#currentIndex;
            dot.classList.toggle("w-6", isTarget);
            dot.classList.toggle("sm:w-7", isTarget);
            dot.classList.toggle("bg-slate-900", isTarget);
            dot.classList.toggle("shadow-xs", isTarget);
            dot.classList.toggle("w-2", !isTarget);
            dot.classList.toggle("bg-slate-400/60", !isTarget);
            dot.setAttribute("aria-selected", String(isTarget));
        });
    }

    #startTimer() {
        this.#stopTimer();
        this.#timer = setInterval(() => {
            this.showSlide(this.#currentIndex + 1);
        }, CarouselInstance.#AUTO_ROTATE_DELAY_MS);
    }

    #stopTimer() {
        if (this.#timer !== null) {
            clearInterval(this.#timer);
            this.#timer = null;
        }
    }

    #bindEvents() {
        const signal = this.#abortController.signal;

        this.#prevBtn?.addEventListener("click", () => {
            this.showSlide(this.#currentIndex - 1);
            this.#startTimer();
        }, { signal });

        this.#nextBtn?.addEventListener("click", () => {
            this.showSlide(this.#currentIndex + 1);
            this.#startTimer();
        }, { signal });

        this.#dots.forEach((dot, idx) => {
            dot.addEventListener("click", () => {
                this.showSlide(idx);
                this.#startTimer();
            }, { signal });
        });

        // Pause on mouse hover
        this.#root.addEventListener("mouseenter", () => this.#stopTimer(), { signal });
        this.#root.addEventListener("mouseleave", () => this.#startTimer(), { signal });

        // Touch & swipe gestures for mobile
        let touchStartX = 0;
        let touchStartY = 0;

        this.#root.addEventListener("touchstart", event => {
            const touch = event.changedTouches?.[0];
            if (touch) {
                touchStartX = touch.screenX;
                touchStartY = touch.screenY;
            }
            this.#stopTimer();
        }, { passive: true, signal });

        this.#root.addEventListener("touchend", event => {
            const touch = event.changedTouches?.[0];
            if (touch) {
                const diffX = touch.screenX - touchStartX;
                const diffY = touch.screenY - touchStartY;

                if (Math.abs(diffX) > Math.abs(diffY) && Math.abs(diffX) > CarouselInstance.#SWIPE_THRESHOLD_PX) {
                    this.showSlide(this.#currentIndex + (diffX < 0 ? 1 : -1));
                }
            }
            this.#startTimer();
        }, { passive: true, signal });
    }

    /**
     * Destroys timers and aborts all attached event listeners
     */
    destroy() {
        this.#stopTimer();
        this.#abortController.abort();
    }
}

// Global initialization
(function (global) {
    "use strict";

    const activeCarousels = new WeakMap();

    function initCarousels() {
        const carouselElements = document.querySelectorAll("[data-carousel]");
        carouselElements.forEach(el => {
            if (!activeCarousels.has(el)) {
                activeCarousels.set(el, new CarouselInstance(el));
            }
        });
    }

    const JAS = global.JAS ??= {};
    JAS.Carousel = {
        init: initCarousels
    };
})(window);
