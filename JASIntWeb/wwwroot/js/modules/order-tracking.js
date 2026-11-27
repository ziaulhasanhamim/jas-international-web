/**
 * JAS International - Order Tracking & Payment Verification Module (ES2024+)
 * Pure functional SSE stream with AbortSignal-controlled polling fallback.
 */

let orderTrackerSse = null;
let orderTrackerTimer = null;
let paymentSse = null;
let paymentPollTimer = null;
let isPaymentRedirecting = false;
let abortController = new AbortController();

function cleanupOrderTracker() {
    if (orderTrackerSse !== null) {
        orderTrackerSse.close();
        orderTrackerSse = null;
    }
    if (orderTrackerTimer !== null) {
        clearInterval(orderTrackerTimer);
        orderTrackerTimer = null;
    }
}

function cleanupPaymentVerification() {
    if (paymentSse !== null) {
        paymentSse.close();
        paymentSse = null;
    }
    if (paymentPollTimer !== null) {
        clearInterval(paymentPollTimer);
        paymentPollTimer = null;
    }
}

export function initLiveOrderTracker() {
    const trackerEl = document.getElementById('live-order-tracker');
    if (!trackerEl) {
        cleanupOrderTracker();
        return;
    }

    const orderId = trackerEl.dataset.orderId;
    const currentStatus = trackerEl.dataset.currentStatus;
    if (!orderId || !currentStatus) return;

    cleanupOrderTracker();

    try {
        orderTrackerSse = new EventSource(`/api/orders/${encodeURIComponent(orderId)}/sse`);
        orderTrackerSse.onmessage = event => {
            try {
                const data = JSON.parse(event.data);
                if (data?.status && data.status !== currentStatus) {
                    cleanupOrderTracker();
                    window.location.reload();
                }
            } catch {
                // Ignore non-JSON heartbeat signals
            }
        };
    } catch (err) {
        console.debug('[OrderTracking] SSE unavailable, falling back to polling:', err);
    }

    orderTrackerTimer = setInterval(async () => {
        try {
            const response = await fetch(`/api/orders/${encodeURIComponent(orderId)}/status`, {
                signal: AbortSignal.any([abortController.signal, AbortSignal.timeout(3000)])
            });

            if (response.ok) {
                const data = await response.json();
                if (data?.status && data.status !== currentStatus) {
                    cleanupOrderTracker();
                    window.location.reload();
                }
            }
        } catch {
            // Ignore transient network errors during background polling
        }
    }, 2000);
}

export function initPaymentVerificationStream() {
    const streamEl = document.getElementById('payment-verification-stream');
    if (!streamEl) {
        cleanupPaymentVerification();
        return;
    }

    const orderId = streamEl.dataset.orderId;
    if (!orderId) return;

    cleanupPaymentVerification();
    isPaymentRedirecting = false;

    const redirectToOrder = targetId => {
        if (isPaymentRedirecting) return;
        isPaymentRedirecting = true;
        cleanupPaymentVerification();

        const label = document.getElementById('sse-status-label');
        if (label) {
            label.textContent = 'Payment Verified! Redirecting to receipt...';
        }
        window.location.replace(`/order/${encodeURIComponent(targetId || orderId)}`);
    };

    try {
        paymentSse = new EventSource(`/api/orders/${encodeURIComponent(orderId)}/sse`);
        paymentSse.onmessage = event => {
            try {
                const data = JSON.parse(event.data);
                if (data && (data.isPaymentVerified || data.isCancelled)) {
                    redirectToOrder(orderId);
                }
            } catch {
                // Ignore non-JSON heartbeat pings
            }
        };
    } catch (err) {
        console.debug('[PaymentStream] SSE init failed, relying on polling fallback:', err);
    }

    paymentPollTimer = setInterval(async () => {
        if (isPaymentRedirecting) return;
        try {
            const response = await fetch(`/api/orders/${encodeURIComponent(orderId)}/status`, {
                signal: AbortSignal.any([abortController.signal, AbortSignal.timeout(2500)])
            });

            if (response.ok) {
                const data = await response.json();
                if (data && (data.isPaymentVerified || data.isCancelled)) {
                    redirectToOrder(orderId);
                }
            }
        } catch {
            // Ignore transient network errors
        }
    }, 1500);
}

export function initOrderTracking() {
    initLiveOrderTracker();
    initPaymentVerificationStream();
}

export function cleanupOrderTracking() {
    cleanupOrderTracker();
    cleanupPaymentVerification();
    abortController.abort();
    abortController = new AbortController();
}
