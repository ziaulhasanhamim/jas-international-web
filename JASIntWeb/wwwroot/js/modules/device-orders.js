/**
 * JAS International - Guest Device Orders Module (ES2024+)
 * Manages guest orders saved on this device with localStorage persistence
 * and resilient network synchronization using AbortSignal timeouts.
 */
class DeviceOrdersManager {
    static #STORAGE_KEY = "jas_device_orders";
    static #MAX_SAVED_ORDERS = 50;

    /**
     * Safely parses orders stored in localStorage
     * @returns {Array<{id: number, token: string, total: number, date: string}>}
     */
    static #readOrders() {
        try {
            const raw = localStorage.getItem(this.#STORAGE_KEY);
            if (!raw) return [];
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    }

    /**
     * Persists an order record to device localStorage
     * @param {number} id Order ID
     * @param {string} token Public token GUID
     * @param {number} total Order total in BDT
     */
    static record(id, token, total) {
        try {
            const currentOrders = this.#readOrders();
            // Remove previous entry if exists and prepend new one
            const filtered = currentOrders.filter(o => o.id !== id && o.token !== token);
            const updated = [
                {
                    id: Number(id),
                    token: String(token),
                    total: Number(total),
                    date: new Date().toISOString()
                },
                ...filtered
            ].slice(0, this.#MAX_SAVED_ORDERS);

            localStorage.setItem(this.#STORAGE_KEY, JSON.stringify(updated));
        } catch (err) {
            console.warn("[DeviceOrders] Failed to save guest order:", err);
        }
    }

    /**
     * Retrieves all saved device orders as a raw JSON string
     * @returns {string} JSON array string
     */
    static getAll() {
        try {
            return localStorage.getItem(this.#STORAGE_KEY) ?? "[]";
        } catch {
            return "[]";
        }
    }

    /**
     * Queries the backend API for live status of tokens using an AbortSignal timeout
     * @param {string} tokensJson JSON array string of token GUIDs
     * @returns {Promise<string>} JSON string of live order records
     */
    static async fetchStatuses(tokensJson) {
        try {
            const tokens = JSON.parse(tokensJson);
            if (!Array.isArray(tokens) || tokens.length === 0) return "[]";

            const response = await fetch("/api/orders/device-status", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ tokens }),
                signal: AbortSignal.timeout(8000) // Modern auto-timeout signal (ES2022+)
            });

            if (!response.ok) return "[]";
            return await response.text();
        } catch (err) {
            console.debug("[DeviceOrders] Failed to fetch device order statuses:", err);
            return "[]";
        }
    }
}

// Global initialization
(function (global) {
    "use strict";

    const JAS = global.JAS ??= {};
    JAS.DeviceOrders = {
        record: (id, token, total) => DeviceOrdersManager.record(id, token, total),
        getAll: () => DeviceOrdersManager.getAll(),
        fetchStatuses: tokensJson => DeviceOrdersManager.fetchStatuses(tokensJson)
    };

    // Global aliases for Blazor JS interop
    global.jasRecordGuestOrder = (id, token, total) => DeviceOrdersManager.record(id, token, total);
    global.jasGetGuestOrders = () => DeviceOrdersManager.getAll();
    global.jasFetchDeviceOrders = tokensJson => DeviceOrdersManager.fetchStatuses(tokensJson);
})(window);
