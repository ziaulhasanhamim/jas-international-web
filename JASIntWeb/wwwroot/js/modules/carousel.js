/**
 * JAS International - Interactive Carousel Module (ES2024+)
 * Pure functional swipe carousel with auto-rotation and AbortController-managed cleanup.
 */

const AUTO_ROTATE_DELAY_MS = 5000;
const SWIPE_THRESHOLD_PX = 40;
const activeCarousels = new Map();

function createCarousel(root) {
    const slides = [...root.querySelectorAll('[data-carousel-slide]')];
    const dots = [...root.querySelectorAll('[data-carousel-dot]')];
    const prevBtn = root.querySelector('[data-carousel-prev]');
    const nextBtn = root.querySelector('[data-carousel-next]');

    if (slides.length <= 1) {
        return { destroy() {} };
    }

    let currentIndex = 0;
    let timer = null;
    const abortController = new AbortController();
    const { signal } = abortController;

    function showSlide(targetIndex) {
        const count = slides.length;
        currentIndex = ((targetIndex % count) + count) % count;

        slides.forEach((slide, idx) => {
            const isTarget = idx === currentIndex;
            slide.classList.toggle('opacity-100', isTarget);
            slide.classList.toggle('z-10', isTarget);
            slide.classList.toggle('opacity-0', !isTarget);
            slide.classList.toggle('pointer-events-none', !isTarget);
            slide.classList.toggle('z-0', !isTarget);
            slide.setAttribute('aria-hidden', String(!isTarget));
        });

        dots.forEach((dot, idx) => {
            const isTarget = idx === currentIndex;
            dot.classList.toggle('w-6', isTarget);
            dot.classList.toggle('sm:w-7', isTarget);
            dot.classList.toggle('bg-slate-900', isTarget);
            dot.classList.toggle('shadow-xs', isTarget);
            dot.classList.toggle('w-2', !isTarget);
            dot.classList.toggle('bg-slate-400/60', !isTarget);
            dot.setAttribute('aria-selected', String(isTarget));
        });
    }

    function stopTimer() {
        if (timer !== null) {
            clearInterval(timer);
            timer = null;
        }
    }

    function startTimer() {
        stopTimer();
        timer = setInterval(() => {
            showSlide(currentIndex + 1);
        }, AUTO_ROTATE_DELAY_MS);
    }

    prevBtn?.addEventListener('click', () => {
        showSlide(currentIndex - 1);
        startTimer();
    }, { signal });

    nextBtn?.addEventListener('click', () => {
        showSlide(currentIndex + 1);
        startTimer();
    }, { signal });

    dots.forEach((dot, idx) => {
        dot.addEventListener('click', () => {
            showSlide(idx);
            startTimer();
        }, { signal });
    });

    root.addEventListener('mouseenter', stopTimer, { signal });
    root.addEventListener('mouseleave', startTimer, { signal });

    let touchStartX = 0;
    let touchStartY = 0;

    root.addEventListener('touchstart', event => {
        const touch = event.changedTouches?.[0];
        if (touch) {
            touchStartX = touch.screenX;
            touchStartY = touch.screenY;
        }
        stopTimer();
    }, { passive: true, signal });

    root.addEventListener('touchend', event => {
        const touch = event.changedTouches?.[0];
        if (touch) {
            const diffX = touch.screenX - touchStartX;
            const diffY = touch.screenY - touchStartY;
            if (Math.abs(diffX) > Math.abs(diffY) && Math.abs(diffX) > SWIPE_THRESHOLD_PX) {
                showSlide(currentIndex + (diffX < 0 ? 1 : -1));
            }
        }
        startTimer();
    }, { passive: true, signal });

    startTimer();

    return {
        destroy() {
            stopTimer();
            abortController.abort();
        }
    };
}

export function initCarousel() {
    document.querySelectorAll('[data-carousel]').forEach(el => {
        if (!activeCarousels.has(el)) {
            activeCarousels.set(el, createCarousel(el));
        }
    });
}

export function cleanupCarousel() {
    for (const carousel of activeCarousels.values()) {
        carousel.destroy();
    }
    activeCarousels.clear();
}
