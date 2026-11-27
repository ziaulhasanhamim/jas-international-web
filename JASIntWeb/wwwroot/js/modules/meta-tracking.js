/**
 * JAS International - Meta Ads & First-Party Attribution Module (ES2024+)
 * Pure functional Meta Pixel tracking, attribution cookies, and declarative SSR event scanner.
 */

let lastTrackedUrl = null;

function getCookie(name) {
    const matches = document.cookie.match(
        new RegExp(`(?:^|; )${name.replaceAll(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1')}=([^;]*)`)
    );
    return matches ? decodeURIComponent(matches[1]) : null;
}

function setCookie(name, value, days) {
    let expires = '';
    if (days) {
        const date = new Date(Date.now() + days * 86_400_000);
        expires = `; expires=${date.toUTCString()}`;
    }
    const secure = location.protocol === 'https:' ? '; SameSite=Lax; Secure' : '; SameSite=Lax';
    document.cookie = `${name}=${encodeURIComponent(value ?? '')}${expires}; path=/${secure}`;
}

export function hasMarketingConsent() {
    return getCookie('jas_cookie_consent') !== 'essential';
}

export function trackMeta(eventName, customData = {}, eventId = null) {
    if (!hasMarketingConsent() || typeof window.fbq !== 'function') return;
    try {
        if (eventId) {
            window.fbq('track', eventName, customData, { eventID: String(eventId) });
        } else {
            window.fbq('track', eventName, customData);
        }
        console.debug('[MetaPixel] Tracked:', eventName, customData, eventId ? `(eventID: ${eventId})` : '');
    } catch (err) {
        console.debug('[MetaPixel] Tracking failed:', err);
    }
}

export function trackPageView() {
    if (!hasMarketingConsent() || typeof window.fbq !== 'function') return;
    const currentUrl = location.href;
    if (lastTrackedUrl === currentUrl) return;
    lastTrackedUrl = currentUrl;
    window.fbq('track', 'PageView');
}

function parseJsonArray(raw) {
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
}

export function scanDeclarativeEvents() {
    const elements = document.querySelectorAll('[data-meta-event]:not([data-meta-fired="true"])');

    elements.forEach(el => {
        const eventName = el.dataset.metaEvent;
        const eventId = el.dataset.eventId ?? null;
        el.dataset.metaFired = 'true';

        if (eventName === 'ViewContent' || eventName === 'AddToCart') {
            const contentId = el.dataset.contentId;
            trackMeta(eventName, {
                content_name: el.dataset.contentName,
                content_category: el.dataset.contentCategory,
                content_ids: contentId ? [contentId.trim().toUpperCase()] : [],
                content_type: 'product',
                value: Number.parseFloat(el.dataset.value ?? '0') || 0,
                currency: el.dataset.currency ?? 'BDT'
            }, eventId);
        } else if (eventName === 'Search') {
            const searchString = (el.dataset.searchString ?? '').trim();
            if (searchString) {
                trackMeta('Search', { search_string: searchString }, eventId);
            }
        } else if (eventName === 'ViewCategory') {
            const category = (el.dataset.contentCategory ?? '').trim();
            if (category) {
                trackMeta('ViewCategory', {
                    content_category: category,
                    content_type: 'product_group'
                }, eventId);
            }
        } else if (eventName === 'InitiateCheckout') {
            const icData = {
                value: Number.parseFloat(el.dataset.value ?? '0') || 0,
                currency: el.dataset.currency ?? 'BDT',
                content_type: 'product',
                num_items: Number.parseInt(el.dataset.numItems ?? '1', 10) || 1
            };
            try {
                const contentIds = parseJsonArray(el.dataset.contentIds).map(x => String(x).trim().toUpperCase());
                const contents = parseJsonArray(el.dataset.contents);
                if (contentIds.length > 0) icData.content_ids = contentIds;
                if (contents.length > 0) icData.contents = contents;
            } catch (e) {
                console.debug('[MetaTracking] Failed to parse initiate checkout contents:', e);
            }
            trackMeta('InitiateCheckout', icData, eventId);
        } else if (eventName === 'Purchase') {
            const purchaseData = {
                value: Number.parseFloat(el.dataset.value ?? '0') || 0,
                currency: el.dataset.currency ?? 'BDT',
                content_type: 'product',
                order_id: el.dataset.orderId
            };
            try {
                const contentIds = parseJsonArray(el.dataset.contentIds).map(x => String(x).trim().toUpperCase());
                const contents = parseJsonArray(el.dataset.contents);
                if (contentIds.length > 0) purchaseData.content_ids = contentIds;
                if (contents.length > 0) purchaseData.contents = contents;
            } catch (e) {
                console.debug('[MetaTracking] Failed to parse purchase contents:', e);
            }
            trackMeta('Purchase', purchaseData, eventId);
        }
    });
}

export function initAttribution() {
    if (!hasMarketingConsent()) return;
    try {
        const urlParams = new URLSearchParams(window.location.search);
        const now = Date.now();

        const fbclid = urlParams.get('fbclid');
        if (fbclid) {
            setCookie('_fbc', `fb.1.${now}.${fbclid}`, 90);
            try { localStorage.setItem('jas_fbclid', fbclid); } catch {}
        }

        if (!getCookie('_fbp')) {
            const randomInt = Math.floor(Math.random() * 899_999_999 + 100_000_000);
            setCookie('_fbp', `fb.1.${now}.${randomInt}`, 90);
        }

        const utmSource = urlParams.get('utm_source');
        const utmMedium = urlParams.get('utm_medium');
        const utmCampaign = urlParams.get('utm_campaign');
        const utmContent = urlParams.get('utm_content');
        const utmTerm = urlParams.get('utm_term');

        if (utmSource || utmMedium || utmCampaign || utmContent || utmTerm || fbclid) {
            const attribution = Object.freeze({
                fbclid: fbclid ?? (getCookie('_fbc') ? getCookie('_fbc').split('.')[3] ?? null : null),
                fbp: getCookie('_fbp'),
                fbc: getCookie('_fbc'),
                utmSource: utmSource ?? (fbclid ? 'meta_ads' : null),
                utmMedium: utmMedium ?? (fbclid ? 'paid_social' : null),
                utmCampaign: utmCampaign ?? null,
                utmContent: utmContent ?? null,
                utmTerm: utmTerm ?? null
            });

            const json = JSON.stringify(attribution);
            setCookie('jas_attribution', json, 30);
            try { localStorage.setItem('jas_attribution', json); } catch {}
        }

        scanDeclarativeEvents();
    } catch (err) {
        console.debug('[MetaTracking] Attribution capture skipped:', err);
    }
}

export function getAttribution() {
    let stored = null;
    try {
        const rawCookie = getCookie('jas_attribution');
        if (rawCookie) {
            stored = JSON.parse(rawCookie);
        } else {
            const rawLocal = localStorage.getItem('jas_attribution');
            if (rawLocal) stored = JSON.parse(rawLocal);
        }
    } catch {
        stored = null;
    }

    const fbc = getCookie('_fbc');
    const fbp = getCookie('_fbp');
    let fbclid = stored?.fbclid ?? null;
    if (!fbclid && fbc) {
        const parts = fbc.split('.');
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

export function initMetaTracking() {
    if (!hasMarketingConsent()) return;
    const pixel = document.querySelector('meta[name="jas-meta-pixel"]')?.content;
    if (!pixel) return;
    if (!window.fbq) {
        const fbq = window.fbq = function () {
            fbq.callMethod ? fbq.callMethod.apply(fbq, arguments) : fbq.queue.push(arguments);
        };
        fbq.queue = [];
        fbq.loaded = true;
        fbq.version = '2.0';
        const script = document.createElement('script');
        script.async = true;
        script.src = 'https://connect.facebook.net/en_US/fbevents.js';
        document.head.append(script);
        fbq('init', pixel);
    }
    window.fbq('consent', 'grant');
    initAttribution();
}

export function revokeMetaConsent() {
    window.fbq?.('consent', 'revoke');
    for (const name of ['_fbp', '_fbc', 'jas_attribution', 'jas_first_touch']) {
        document.cookie = `${name}=; max-age=0; path=/; SameSite=Lax`;
    }
    for (const name of ['jas_fbclid', 'jas_attribution']) {
        try { localStorage.removeItem(name); } catch {}
    }
}

// Blazor JS interop aliases
if (typeof window !== 'undefined') {
    window.jasGetAttribution = getAttribution;
    window.jasTrackMeta = trackMeta;
}
