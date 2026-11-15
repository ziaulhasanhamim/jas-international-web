/**
 * JAS International - Visitor Analytics Tracking Module (ES2024+)
 * Industry-standard first-party visitor and session tracking with zero third-party dependencies,
 * full Blazor Static SSR enhanced navigation support, and resilient sendBeacon/keepalive transport.
 */
class VisitorAnalyticsController {
    static _vidKey = "jas_vid";
    static _sidKey = "jas_sid";
    static _sidTimeKey = "jas_sid_time";
    static _currentPath = "";
    static _startTime = Date.now();
    static _sessionTimeoutMs = 30 * 60 * 1000; // 30 minutes inactivity timeout

    /**
     * Safe storage reader with cookie fallback
     */
    static getItem(key) {
        try {
            return localStorage.getItem(key);
        } catch {
            const match = document.cookie.match(new RegExp(`(?:^|; )${key}=([^;]*)`));
            return match ? decodeURIComponent(match[1]) : null;
        }
    }

    /**
     * Safe storage writer with cookie fallback
     */
    static setItem(key, value, days = 365) {
        try {
            localStorage.setItem(key, value);
        } catch {
            // Ignore quota errors
        }
        let expires = "";
        if (days) {
            const d = new Date(Date.now() + days * 86_400_000);
            expires = `; expires=${d.toUTCString()}`;
        }
        const secure = location.protocol === "https:" ? "; SameSite=Lax; Secure" : "; SameSite=Lax";
        document.cookie = `${key}=${encodeURIComponent(value)}${expires}; path=/${secure}`;
    }

    /**
     * Generates a unique identifier
     */
    static generateId(prefix = "v") {
        const rand = Math.random().toString(36).substring(2, 10);
        const time = Date.now().toString(36);
        return `${prefix}_${time}_${rand}`;
    }

    /**
     * Retrieves or initializes persistent Visitor ID
     */
    static getVisitorId() {
        let vid = this.getItem(this._vidKey);
        if (!vid) {
            vid = this.generateId("vid");
            this.setItem(this._vidKey, vid, 365);
        }
        return vid;
    }

    /**
     * Retrieves or initializes Session ID (resets after 30 min inactivity)
     */
    static getSessionId() {
        const now = Date.now();
        let sid = null;
        try {
            sid = sessionStorage.getItem(this._sidKey);
            const lastActive = parseInt(sessionStorage.getItem(this._sidTimeKey) || "0", 10);
            if (!sid || (now - lastActive > this._sessionTimeoutMs)) {
                sid = this.generateId("sid");
                sessionStorage.setItem(this._sidKey, sid);
            }
            sessionStorage.setItem(this._sidTimeKey, now.toString());
        } catch {
            sid = this.getItem(this._sidKey);
            if (!sid) {
                sid = this.generateId("sid");
                this.setItem(this._sidKey, sid, 1);
            }
        }
        return sid;
    }

    /**
     * Sends analytics payload to backend using fetch with keepalive or sendBeacon
     */
    static send(payload) {
        // Exclude admin pages from storefront visitor logs
        if (location.pathname.startsWith("/admin") || location.pathname.startsWith("/api")) {
            return;
        }

        const endpoint = "/api/analytics/collect";
        const json = JSON.stringify(payload);

        if (typeof navigator.sendBeacon === "function") {
            try {
                const blob = new Blob([json], { type: "application/json" });
                const success = navigator.sendBeacon(endpoint, blob);
                if (success) return;
            } catch {
                // Fallback to fetch
            }
        }

        if (typeof fetch === "function") {
            fetch(endpoint, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: json,
                keepalive: true
            }).catch(() => {});
        }
    }

    /**
     * Tracks a pageview
     */
    static trackPageView() {
        // Do not track admin portal
        if (location.pathname.startsWith("/admin") || location.pathname.startsWith("/api")) {
            return;
        }

        const now = Date.now();
        const durationFromPrevious = Math.round((now - this._startTime) / 1000);
        this._startTime = now;
        this._currentPath = location.pathname;

        const urlParams = new URLSearchParams(window.location.search);
        const payload = {
            vid: this.getVisitorId(),
            sid: this.getSessionId(),
            path: location.pathname,
            title: document.title || "",
            referrer: document.referrer || "",
            utm_source: urlParams.get("utm_source") || null,
            utm_medium: urlParams.get("utm_medium") || null,
            utm_campaign: urlParams.get("utm_campaign") || null,
            fbclid: urlParams.get("fbclid") || localStorage.getItem("jas_fbclid") || null,
            duration: 0,
            is_unload: false
        };

        // Use requestIdleCallback if available to ensure zero impact on first-paint and interactivity
        if (typeof window.requestIdleCallback === "function") {
            window.requestIdleCallback(() => this.send(payload));
        } else {
            setTimeout(() => this.send(payload), 100);
        }
    }

    /**
     * Reports duration when leaving a page or navigating away
     */
    static reportUnload() {
        if (!this._currentPath || this._currentPath.startsWith("/admin")) return;
        const duration = Math.round((Date.now() - this._startTime) / 1000);
        if (duration < 1) return;

        const payload = {
            vid: this.getVisitorId(),
            sid: this.getSessionId(),
            path: this._currentPath,
            title: document.title || "",
            duration: duration,
            is_unload: true
        };

        this.send(payload);
    }

    /**
     * Initializes analytics tracking
     */
    static init() {
        this.trackPageView();
    }
}

// Expose on window.JAS namespace
window.JAS = window.JAS || {};
window.JAS.VisitorAnalytics = VisitorAnalyticsController;
