/**
 * JAS International - Order Tracking & Payment Verification Module (ES2024+)
 * Live Server-Sent Events (SSE) stream with AbortSignal-controlled
 * background polling fallbacks for payment confirmation and fulfillment tracking.
 */
class OrderTrackingController {
    #orderTrackerSse = null;
    #orderTrackerTimer = null;
    #paymentSse = null;
    #paymentPollTimer = null;
    #isPaymentRedirecting = false;
    #abortController = new AbortController();

    /**
     * Cleans up live order tracker SSE and polling subscriptions
     */
    cleanupOrderTracker() {
        if (this.#orderTrackerSse !== null) {
            this.#orderTrackerSse.close();
            this.#orderTrackerSse = null;
        }
        if (this.#orderTrackerTimer !== null) {
            clearInterval(this.#orderTrackerTimer);
            this.#orderTrackerTimer = null;
        }
    }

    /**
     * Initializes the live order tracker on the order confirmation page
     */
    initLiveOrderTracker() {
        const trackerEl = document.getElementById("live-order-tracker");
        if (!trackerEl) {
            this.cleanupOrderTracker();
            return;
        }

        const orderId = trackerEl.dataset.orderId;
        const currentStatus = trackerEl.dataset.currentStatus;
        if (!orderId || !currentStatus) return;

        this.cleanupOrderTracker();

        // 1. Primary: Server-Sent Events stream
        try {
            this.#orderTrackerSse = new EventSource(`/api/orders/${encodeURIComponent(orderId)}/sse`);
            this.#orderTrackerSse.onmessage = event => {
                try {
                    const data = JSON.parse(event.data);
                    if (data?.status && data.status !== currentStatus) {
                        this.cleanupOrderTracker();
                        window.location.reload();
                    }
                } catch {
                    // Ignore non-JSON heartbeat signals
                }
            };
        } catch (err) {
            console.debug("[OrderTracking] SSE unavailable, falling back to polling:", err);
        }

        // 2. Secondary: 2s interval polling fallback with AbortSignal
        this.#orderTrackerTimer = setInterval(async () => {
            try {
                const response = await fetch(`/api/orders/${encodeURIComponent(orderId)}/status`, {
                    signal: AbortSignal.any([this.#abortController.signal, AbortSignal.timeout(3000)])
                });

                if (response.ok) {
                    const data = await response.json();
                    if (data?.status && data.status !== currentStatus) {
                        this.cleanupOrderTracker();
                        window.location.reload();
                    }
                }
            } catch {
                // Ignore transient network errors during background polling
            }
        }, 2000);
    }

    /**
     * Cleans up payment verification SSE and polling subscriptions
     */
    cleanupPaymentVerification() {
        if (this.#paymentSse !== null) {
            this.#paymentSse.close();
            this.#paymentSse = null;
        }
        if (this.#paymentPollTimer !== null) {
            clearInterval(this.#paymentPollTimer);
            this.#paymentPollTimer = null;
        }
    }

    /**
     * Initializes payment verification stream on the payment instruction page
     */
    initPaymentVerificationStream() {
        const streamEl = document.getElementById("payment-verification-stream");
        if (!streamEl) {
            this.cleanupPaymentVerification();
            return;
        }

        const orderId = streamEl.dataset.orderId;
        if (!orderId) return;

        this.cleanupPaymentVerification();
        this.#isPaymentRedirecting = false;

        const redirectToOrder = targetId => {
            if (this.#isPaymentRedirecting) return;
            this.#isPaymentRedirecting = true;
            this.cleanupPaymentVerification();

            const label = document.getElementById("sse-status-label");
            if (label) {
                label.textContent = "Payment Verified! Redirecting to receipt...";
            }
            window.location.replace(`/order/${encodeURIComponent(targetId || orderId)}`);
        };

        // 1. Primary: Server-Sent Events stream
        try {
            this.#paymentSse = new EventSource(`/api/orders/${encodeURIComponent(orderId)}/sse`);
            this.#paymentSse.onmessage = event => {
                try {
                    const data = JSON.parse(event.data);
                    if (data && (data.isPaymentVerified || data.isCancelled)) {
                        redirectToOrder(data.orderId || orderId);
                    }
                } catch {
                    // Ignore non-JSON heartbeat pings
                }
            };
        } catch (err) {
            console.debug("[PaymentStream] SSE init failed, relying on polling fallback:", err);
        }

        // 2. Secondary: 1.5s background polling fallback
        this.#paymentPollTimer = setInterval(async () => {
            if (this.#isPaymentRedirecting) return;
            try {
                const response = await fetch(`/api/orders/${encodeURIComponent(orderId)}/status`, {
                    signal: AbortSignal.any([this.#abortController.signal, AbortSignal.timeout(2500)])
                });

                if (response.ok) {
                    const data = await response.json();
                    if (data && (data.isPaymentVerified || data.isCancelled)) {
                        redirectToOrder(data.id || orderId);
                    }
                }
            } catch {
                // Ignore transient network errors
            }
        }, 1500);
    }

    /**
     * Cleans up all active tracking subscriptions and aborts pending fetch requests
     */
    cleanupAll() {
        this.cleanupOrderTracker();
        this.cleanupPaymentVerification();
        this.#abortController.abort();
        this.#abortController = new AbortController();
    }

    /**
     * Initializes all tracking modules present in the current DOM
     */
    initAll() {
        this.initLiveOrderTracker();
        this.initPaymentVerificationStream();
    }
}

// Global initialization
(function (global) {
    "use strict";

    const controller = new OrderTrackingController();

    const JAS = global.JAS ??= {};
    JAS.OrderTracking = {
        init: () => controller.initAll(),
        cleanup: () => controller.cleanupAll(),
        initOrderTracker: () => controller.initLiveOrderTracker(),
        initPaymentStream: () => controller.initPaymentVerificationStream()
    };
})(window);
