/**
 * JAS International - Meta Ads & First-Party Attribution Module (ES2024+)
 * Handles client-side Meta Pixel tracking, first-party cookie management (_fbc, _fbp, jas_attribution),
 * event deduplication matching server CAPI, and Blazor Static SSR enhanced navigation triggers.
 */
class MetaTrackingController {
    /**
     * Checks if the user has marketing consent.
     * Default-on unless explicitly declined ('essential').
     * @returns {boolean}
     */
    static hasMarketingConsent() {
        return this.getCookie("jas_cookie_consent") !== "essential";
    }

    /**
     * Reads a cookie value by name
     * @param {string} name Cookie name
     * @returns {string|null} Cookie value or null
     */
    static getCookie(name) {
        const matches = document.cookie.match(
            new RegExp(`(?:^|; )${name.replaceAll(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, "\\$1")}=([^;]*)`)
        );
        return matches ? decodeURIComponent(matches[1]) : null;
    }

    /**
     * Sets a first-party cookie
     * @param {string} name Cookie name
     * @param {string} value Cookie value
     * @param {number} days Expiration in days
     */
    static setCookie(name, value, days) {
        let expires = "";
        if (days) {
            const date = new Date(Date.now() + days * 86_400_000);
            expires = `; expires=${date.toUTCString()}`;
        }
        const secure = location.protocol === "https:" ? "; SameSite=Lax; Secure" : "; SameSite=Lax";
        document.cookie = `${name}=${encodeURIComponent(value ?? "")}${expires}; path=/${secure}`;
    }

    /**
     * Captures ad click IDs (fbclid) and UTM parameters from current URL
     */
    static initAttribution() {
        if (!this.hasMarketingConsent()) return;
        try {
            const urlParams = new URLSearchParams(window.location.search);
            const now = Date.now();

            // 1. Capture fbclid -> _fbc cookie (valid for 90 days)
            const fbclid = urlParams.get("fbclid");
            if (fbclid) {
                this.setCookie("_fbc", `fb.1.${now}.${fbclid}`, 90);
                try {
                    localStorage.setItem("jas_fbclid", fbclid);
                } catch {
                    // Ignore storage quota / private mode errors
                }
            }

            // 2. Ensure first-party browser identifier _fbp exists (valid for 90 days)
            if (!this.getCookie("_fbp")) {
                const randomInt = Math.floor(Math.random() * 899_999_999 + 100_000_000);
                this.setCookie("_fbp", `fb.1.${now}.${randomInt}`, 90);
            }

            // 3. Capture UTM parameters
            const utmSource = urlParams.get("utm_source");
            const utmMedium = urlParams.get("utm_medium");
            const utmCampaign = urlParams.get("utm_campaign");
            const utmContent = urlParams.get("utm_content");
            const utmTerm = urlParams.get("utm_term");

            if (utmSource || utmMedium || utmCampaign || utmContent || utmTerm || fbclid) {
                const attribution = Object.freeze({
                    fbclid: fbclid ?? (this.getCookie("_fbc") ? this.getCookie("_fbc").split(".")[3] ?? null : null),
                    fbp: this.getCookie("_fbp"),
                    fbc: this.getCookie("_fbc"),
                    utmSource: utmSource ?? (fbclid ? "meta_ads" : null),
                    utmMedium: utmMedium ?? (fbclid ? "paid_social" : null),
                    utmCampaign: utmCampaign ?? null,
                    utmContent: utmContent ?? null,
                    utmTerm: utmTerm ?? null
                });

                const json = JSON.stringify(attribution);
                this.setCookie("jas_attribution", json, 30);
                try {
                    localStorage.setItem("jas_attribution", json);
                } catch {
                    // Ignore storage quota errors
                }
            }

            this.scanDeclarativeEvents();
        } catch (err) {
            console.debug("[MetaTracking] Attribution capture skipped:", err);
        }
    }

    /**
     * Scans the DOM for declarative [data-meta-event] elements rendered by Static SSR components
     */
    static scanDeclarativeEvents() {
        const elements = document.querySelectorAll('[data-meta-event]:not([data-meta-fired="true"])');

        elements.forEach(el => {
            const eventName = el.dataset.metaEvent;
            const eventId = el.dataset.eventId ?? null;
            el.dataset.metaFired = "true";

            if (eventName === "ViewContent") {
                const contentId = el.dataset.contentId;
                const contentName = el.dataset.contentName;
                const contentCategory = el.dataset.contentCategory;
                const value = Number.parseFloat(el.dataset.value ?? "0") || 0;
                const currency = el.dataset.currency ?? "BDT";

                this.track("ViewContent", {
                    content_name: contentName,
                    content_category: contentCategory,
                    content_ids: contentId ? [contentId.trim().toUpperCase()] : [],
                    content_type: "product",
                    value,
                    currency
                }, eventId);
            } else if (eventName === "Search") {
                const searchString = el.dataset.searchString ?? "";
                if (searchString.trim()) {
                    this.track("Search", {
                        search_string: searchString.trim()
                    }, eventId);
                }
            } else if (eventName === "ViewCategory") {
                const category = el.dataset.contentCategory ?? "";
                if (category.trim()) {
                    this.track("ViewCategory", {
                        content_category: category.trim(),
                        content_type: "product_group"
                    }, eventId);
                }
            } else if (eventName === "AddToCart") {
                const contentId = el.dataset.contentId;
                const contentName = el.dataset.contentName;
                const contentCategory = el.dataset.contentCategory;
                const value = Number.parseFloat(el.dataset.value ?? "0") || 0;
                const currency = el.dataset.currency ?? "BDT";
                this.track("AddToCart", {
                    content_name: contentName,
                    content_category: contentCategory,
                    content_ids: contentId ? [contentId.trim().toUpperCase()] : [],
                    content_type: "product",
                    value,
                    currency
                }, eventId);
            } else if (eventName === "InitiateCheckout") {
                const value = Number.parseFloat(el.dataset.value ?? "0") || 0;
                const currency = el.dataset.currency ?? "BDT";
                const numItems = Number.parseInt(el.dataset.numItems ?? "1", 10) || 1;
                let contentIds = [];
                let contents = [];
                try {
                    if (el.dataset.contentIds) {
                        const parsed = JSON.parse(el.dataset.contentIds);
                        if (Array.isArray(parsed)) contentIds = parsed.map(x => String(x).trim().toUpperCase());
                    }
                    if (el.dataset.contents) {
                        const parsed = JSON.parse(el.dataset.contents);
                        if (Array.isArray(parsed)) contents = parsed;
                    }
                } catch (e) {
                    console.debug("[MetaTracking] Failed to parse initiate checkout contents:", e);
                }

                const icData = {
                    value,
                    currency,
                    content_type: "product",
                    num_items: numItems
                };
                if (contentIds.length > 0) icData.content_ids = contentIds;
                if (contents.length > 0) icData.contents = contents;

                this.track("InitiateCheckout", icData, eventId);
            } else if (eventName === "Purchase") {
                const value = Number.parseFloat(el.dataset.value ?? "0") || 0;
                const currency = el.dataset.currency ?? "BDT";
                const orderId = el.dataset.orderId;
                let contentIds = [];
                let contents = [];
                try {
                    if (el.dataset.contentIds) {
                        const parsed = JSON.parse(el.dataset.contentIds);
                        if (Array.isArray(parsed)) contentIds = parsed.map(x => String(x).trim().toUpperCase());
                    }
                    if (el.dataset.contents) {
                        const parsed = JSON.parse(el.dataset.contents);
                        if (Array.isArray(parsed)) contents = parsed;
                    }
                } catch (e) {
                    console.debug("[MetaTracking] Failed to parse purchase contents:", e);
                }

                const purchaseData = {
                    value,
                    currency,
                    content_type: "product",
                    order_id: orderId
                };
                if (contentIds.length > 0) purchaseData.content_ids = contentIds;
                if (contents.length > 0) purchaseData.contents = contents;

                this.track("Purchase", purchaseData, eventId);
            }
        });
    }

    /**
     * Retrieves attribution properties from cookies or localStorage
     * @returns {object} Attribution object
     */
    static getAttribution() {
        let stored = null;
        try {
            const rawCookie = this.getCookie("jas_attribution");
            if (rawCookie) {
                stored = JSON.parse(rawCookie);
            } else {
                const rawLocal = localStorage.getItem("jas_attribution");
                if (rawLocal) stored = JSON.parse(rawLocal);
            }
        } catch {
            stored = null;
        }

        const fbc = this.getCookie("_fbc");
        const fbp = this.getCookie("_fbp");
        let fbclid = stored?.fbclid ?? null;
        if (!fbclid && fbc) {
            const parts = fbc.split(".");
            if (parts.length >= 4) fbclid = parts[3];
        }

        return Object.freeze({
            fbclid,
            fbp: fbp ?? stored?.fbp ?? null,
            fbc: fbc ?? stored?.fbc ?? null,
            utmSource: stored?.utmSource ?? null,
            utmMedium: stored?.utmMedium ?? null,
            utmCampaign: stored?.utmCampaign ?? null,
            utmContent: stored?.utmContent ?? null,
            utmTerm: stored?.utmTerm ?? null
        });
    }

    /**
     * Dispatches a client-side Meta Pixel event with eventID deduplication
     * @param {string} eventName Event name
     * @param {object} customData Additional event data
     * @param {string|null} eventId Deduplication identifier
     */
    static track(eventName, customData = {}, eventId = null) {
        if (!this.hasMarketingConsent() || typeof window.fbq !== "function") return;
        try {
            if (eventId) {
                window.fbq("track", eventName, customData, { eventID: String(eventId) });
            } else {
                window.fbq("track", eventName, customData);
            }
            console.debug("[MetaPixel] Tracked:", eventName, customData, eventId ? `(eventID: ${eventId})` : "");
        } catch (err) {
            console.debug("[MetaPixel] Tracking failed:", err);
        }
    }

    static _lastTrackedUrl = null;

    /**
     * Tracks a PageView event on route change during enhanced navigation with deduplication
     */
    static trackPageView() {
        if (!this.hasMarketingConsent() || typeof window.fbq !== "function") return;
        const currentUrl = location.href;
        if (this._lastTrackedUrl === currentUrl) return; // Prevent double firing on initial load or duplicate hook
        this._lastTrackedUrl = currentUrl;
        window.fbq("track", "PageView");
    }
}

// Global initialization
(function (global) {
    "use strict";

    const JAS = global.JAS ??= {};
    JAS.MetaTracking = {
        init: () => {
            if (!MetaTrackingController.hasMarketingConsent()) return;
            const pixel = document.querySelector('meta[name="jas-meta-pixel"]')?.content;
            if (!pixel) return;
            if (!window.fbq) {
                const fbq = window.fbq = function() { fbq.callMethod ? fbq.callMethod.apply(fbq, arguments) : fbq.queue.push(arguments); };
                fbq.queue = []; fbq.loaded = true; fbq.version = '2.0';
                const script = document.createElement('script'); script.async = true;
                script.src = 'https://connect.facebook.net/en_US/fbevents.js'; document.head.append(script);
                fbq('init', pixel);
            }
            window.fbq('consent', 'grant');
            MetaTrackingController.initAttribution();
        },
        scan: () => MetaTrackingController.scanDeclarativeEvents(),
        getAttribution: () => MetaTrackingController.getAttribution(),
        track: (eventName, customData, eventId) => MetaTrackingController.track(eventName, customData, eventId),
        trackPageView: () => MetaTrackingController.trackPageView()
    };

    // Global aliases for Blazor JS interop
    global.jasGetAttribution = () => MetaTrackingController.getAttribution();
    global.jasTrackMeta = (eventName, customData, eventId) => MetaTrackingController.track(eventName, customData, eventId);
})(window);
