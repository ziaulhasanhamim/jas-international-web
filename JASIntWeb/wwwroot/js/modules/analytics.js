/**
 * JAS International - Zero-Penalty Visitor Analytics Tracking Module (ES2024+)
 * High-performance, zero main-thread blocking telemetry:
 * - 100% asynchronous and idle-scheduled (requestIdleCallback)
 * - In-memory cached visitor & session identities (zero repeated storage I/O)
 * - Fetch Priority "low" & out-of-process sendBeacon transport
 * - 100% bfcache (Back/Forward Cache) preserved via visibilitychange / pagehide
 */
class VisitorAnalyticsController {
    static _vidKey = "jas_vid";
    static _sidKey = "jas_sid";
    static _sidTimeKey = "jas_sid_time";
    static _sessionTimeoutMs = 30 * 60 * 1000; // 30 min inactivity timeout

    // In-memory identity caches to avoid disk/localStorage reads during user navigation
    static _cachedVid = null;
    static _cachedSid = null;
    static _cachedSidTime = 0;
    static _currentPath = "";
    static _startTime = Date.now();
    static _initialized = false;

    /**
     * Fast in-memory / storage reader with cookie fallback
     */
    static _getItem(key) {
        try {
            return localStorage.getItem(key);
        } catch {
            const match = document.cookie.match(new RegExp(`(?:^|; )${key}=([^;]*)`));
            return match ? decodeURIComponent(match[1]) : null;
        }
    }

    /**
     * Storage writer
     */
    static _setItem(key, value, days = 365) {
        try {
            localStorage.setItem(key, value);
        } catch {
            // Storage quota or private browsing mode
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
     * Generates a lightweight random identifier
     */
    static _generateId(prefix = "v") {
        const rand = Math.random().toString(36).substring(2, 9);
        const time = Date.now().toString(36);
        return `${prefix}_${time}_${rand}`;
    }

    /**
     * Resolves Visitor ID from memory cache or storage
     */
    static getVisitorId() {
        if (this._cachedVid) return this._cachedVid;

        let vid = this._getItem(this._vidKey);
        if (!vid) {
            vid = this._generateId("vid");
            this._setItem(this._vidKey, vid, 365);
        }
        this._cachedVid = vid;
        return vid;
    }

    /**
     * Resolves Session ID from memory cache or storage
     */
    static getSessionId() {
        const now = Date.now();
        if (this._cachedSid && (now - this._cachedSidTime < this._sessionTimeoutMs)) {
            this._cachedSidTime = now;
            return this._cachedSid;
        }

        let sid = null;
        try {
            sid = sessionStorage.getItem(this._sidKey);
            const lastActive = parseInt(sessionStorage.getItem(this._sidTimeKey) || "0", 10);
            if (!sid || (now - lastActive > this._sessionTimeoutMs)) {
                sid = this._generateId("sid");
                sessionStorage.setItem(this._sidKey, sid);
            }
            sessionStorage.setItem(this._sidTimeKey, now.toString());
        } catch {
            sid = this._getItem(this._sidKey);
            if (!sid) {
                sid = this._generateId("sid");
                this._setItem(this._sidKey, sid, 1);
            }
        }

        this._cachedSid = sid;
        this._cachedSidTime = now;
        return sid;
    }

    /**
     * Non-blocking background transport:
     * - Uses native sendBeacon with text/plain (zero Blob allocation, runs in browser background thread)
     * - Fallback to fetch with priority: "low" and keepalive: true (never blocks UI or critical assets)
     */
    static send(payload) {
        if (location.pathname.startsWith("/admin") || location.pathname.startsWith("/api")) {
            return;
        }

        const endpoint = "/api/analytics/collect";
        const json = JSON.stringify(payload);

        // 1. sendBeacon transmits out-of-process without touching the main thread
        if (typeof navigator.sendBeacon === "function") {
            try {
                if (navigator.sendBeacon(endpoint, json)) {
                    return;
                }
            } catch {
                // Fallback to fetch
            }
        }

        // 2. Fetch with low priority ensures zero contention with images, css, or checkout requests
        if (typeof fetch === "function") {
            try {
                fetch(endpoint, {
                    method: "POST",
                    headers: { "Content-Type": "text/plain" },
                    body: json,
                    keepalive: true,
                    priority: "low"
                }).catch(() => {});
            } catch {
                // Silent fail
            }
        }
    }

    /**
     * Executes pageview collection in the browser's idle period
     */
    static _doTrackPageView() {
        if (location.pathname.startsWith("/admin") || location.pathname.startsWith("/api")) {
            return;
        }

        const now = Date.now();
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
            fbclid: urlParams.get("fbclid") || this._getItem("jas_fbclid") || null,
            duration: 0,
            is_unload: false
        };

        this.send(payload);
    }

    /**
     * Public pageview trigger scheduled safely during idle time
     */
    static trackPageView() {
        if (typeof window.requestIdleCallback === "function") {
            window.requestIdleCallback(() => this._doTrackPageView(), { timeout: 2000 });
        } else {
            setTimeout(() => this._doTrackPageView(), 50);
        }
    }

    /**
     * Reports duration when tab or page is hidden/dismissed
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
     * Initializes analytics tracking with zero performance impact
     */
    static init() {
        // Skip automated test scrapers
        if (navigator.webdriver) return;

        if (!this._initialized) {
            this._initialized = true;

            // Use visibilitychange and pagehide instead of beforeunload to preserve 100% bfcache (Back/Forward Cache)
            document.addEventListener("visibilitychange", () => {
                if (document.visibilityState === "hidden") {
                    VisitorAnalyticsController.reportUnload();
                }
            }, { passive: true });

            window.addEventListener("pagehide", () => {
                VisitorAnalyticsController.reportUnload();
            }, { passive: true });
        }

        this.trackPageView();
    }
}

// Expose on window.JAS namespace
window.JAS = window.JAS || {};
window.JAS.VisitorAnalytics = VisitorAnalyticsController;
