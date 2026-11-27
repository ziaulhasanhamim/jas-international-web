/**
 * JAS International - Zero-Penalty Visitor Analytics Tracking Module (ES2024+)
 * Pure functional module using idle scheduling, cached identities, sendBeacon, and bfcache-safe unload hooks.
 */

const VID_KEY = 'jas_vid';
const SID_KEY = 'jas_sid';
const SID_TIME_KEY = 'jas_sid_time';
const SESSION_TIMEOUT_MS = 30 * 60 * 1000;

let cachedVid = null;
let cachedSid = null;
let cachedSidTime = 0;
let currentPath = '';
let startTime = Date.now();
let initialized = false;

function readStorage(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        const match = document.cookie.match(new RegExp(`(?:^|; )${key}=([^;]*)`));
        return match ? decodeURIComponent(match[1]) : null;
    }
}

function writeStorage(key, value, days = 365) {
    try {
        localStorage.setItem(key, value);
    } catch {
        // Storage quota or private browsing mode
    }
    let expires = '';
    if (days) {
        const d = new Date(Date.now() + days * 86_400_000);
        expires = `; expires=${d.toUTCString()}`;
    }
    const secure = location.protocol === 'https:' ? '; SameSite=Lax; Secure' : '; SameSite=Lax';
    document.cookie = `${key}=${encodeURIComponent(value)}${expires}; path=/${secure}`;
}

function generateId(prefix = 'v') {
    const rand = Math.random().toString(36).substring(2, 9);
    const time = Date.now().toString(36);
    return `${prefix}_${time}_${rand}`;
}

export function getVisitorId() {
    if (cachedVid) return cachedVid;

    let vid = readStorage(VID_KEY);
    if (!vid) {
        vid = generateId('vid');
        writeStorage(VID_KEY, vid, 365);
    }
    cachedVid = vid;
    return vid;
}

export function getSessionId() {
    const now = Date.now();
    if (cachedSid && (now - cachedSidTime < SESSION_TIMEOUT_MS)) {
        cachedSidTime = now;
        return cachedSid;
    }

    let sid = null;
    try {
        sid = sessionStorage.getItem(SID_KEY);
        const lastActive = parseInt(sessionStorage.getItem(SID_TIME_KEY) || '0', 10);
        if (!sid || (now - lastActive > SESSION_TIMEOUT_MS)) {
            sid = generateId('sid');
            sessionStorage.setItem(SID_KEY, sid);
        }
        sessionStorage.setItem(SID_TIME_KEY, now.toString());
    } catch {
        sid = readStorage(SID_KEY);
        if (!sid) {
            sid = generateId('sid');
            writeStorage(SID_KEY, sid, 1);
        }
    }

    cachedSid = sid;
    cachedSidTime = now;
    return sid;
}

function send(payload) {
    if (location.pathname.startsWith('/admin') || location.pathname.startsWith('/api')) {
        return;
    }

    const endpoint = '/api/analytics/collect';
    const json = JSON.stringify(payload);

    if (typeof navigator.sendBeacon === 'function') {
        try {
            if (navigator.sendBeacon(endpoint, json)) {
                return;
            }
        } catch {
            // Fallback to low-priority fetch
        }
    }

    if (typeof fetch === 'function') {
        try {
            fetch(endpoint, {
                method: 'POST',
                headers: { 'Content-Type': 'text/plain' },
                body: json,
                keepalive: true,
                priority: 'low'
            }).catch(() => {});
        } catch {
            // Silent fail
        }
    }
}

function doTrackPageView() {
    if (location.pathname.startsWith('/admin') || location.pathname.startsWith('/api')) {
        return;
    }

    startTime = Date.now();
    currentPath = location.pathname;

    const urlParams = new URLSearchParams(window.location.search);
    send({
        vid: getVisitorId(),
        sid: getSessionId(),
        path: location.pathname,
        title: document.title || '',
        referrer: document.referrer || '',
        utm_source: urlParams.get('utm_source') || null,
        utm_medium: urlParams.get('utm_medium') || null,
        utm_campaign: urlParams.get('utm_campaign') || null,
        fbclid: urlParams.get('fbclid') || readStorage('jas_fbclid') || null,
        duration: 0,
        is_unload: false
    });
}

export function trackPageView() {
    if (typeof window.requestIdleCallback === 'function') {
        window.requestIdleCallback(doTrackPageView, { timeout: 2000 });
    } else {
        setTimeout(doTrackPageView, 50);
    }
}

export function reportUnload() {
    if (!currentPath || currentPath.startsWith('/admin')) return;
    const duration = Math.round((Date.now() - startTime) / 1000);
    if (duration < 1) return;

    send({
        vid: getVisitorId(),
        sid: getSessionId(),
        path: currentPath,
        title: document.title || '',
        duration,
        is_unload: true
    });
}

export function initVisitorAnalytics() {
    if (navigator.webdriver) return;

    if (!initialized) {
        initialized = true;

        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'hidden') {
                reportUnload();
            }
        }, { passive: true });

        window.addEventListener('pagehide', reportUnload, { passive: true });
    }

    trackPageView();
}
