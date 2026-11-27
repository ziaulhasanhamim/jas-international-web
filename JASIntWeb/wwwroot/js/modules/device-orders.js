/**
 * JAS International - Guest Device Orders Module (ES2024+)
 * Pure functional module managing guest orders in localStorage and status lookups.
 */

const STORAGE_KEY = 'jas_device_orders';
const MAX_SAVED_ORDERS = 50;

function readOrders() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        if (!raw) return [];
        const parsed = JSON.parse(raw);
        return Array.isArray(parsed) ? parsed : [];
    } catch {
        return [];
    }
}

export function recordGuestOrder(id, token, total) {
    try {
        const currentOrders = readOrders();
        const filtered = currentOrders.filter(o => o.id !== id && o.token !== token);
        const updated = [
            {
                id: Number(id),
                token: String(token),
                total: Number(total),
                date: new Date().toISOString()
            },
            ...filtered
        ].slice(0, MAX_SAVED_ORDERS);

        localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
    } catch (err) {
        console.warn('[DeviceOrders] Failed to save guest order:', err);
    }
}

export function getGuestOrders() {
    try {
        return localStorage.getItem(STORAGE_KEY) ?? '[]';
    } catch {
        return '[]';
    }
}

export async function fetchDeviceOrders(tokensJson) {
    try {
        const tokens = JSON.parse(tokensJson);
        if (!Array.isArray(tokens) || tokens.length === 0) return '[]';

        const response = await fetch('/api/orders/device-status', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ tokens }),
            signal: AbortSignal.timeout(8000)
        });

        if (!response.ok) return '[]';
        return await response.text();
    } catch (err) {
        console.debug('[DeviceOrders] Failed to fetch device order statuses:', err);
        return '[]';
    }
}

// Blazor JS interop aliases
if (typeof window !== 'undefined') {
    window.jasRecordGuestOrder = recordGuestOrder;
    window.jasGetGuestOrders = getGuestOrders;
    window.jasFetchDeviceOrders = fetchDeviceOrders;
}
