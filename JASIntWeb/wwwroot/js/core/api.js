/**
 * JAS International - Core Functional API Client (ES2024+)
 * High-performance fetch wrapper enforcing JSON request bodies,
 * automated CSRF token caching, and unified error mapping.
 */

let cachedCsrfToken = null;

/**
 * Retrieves the cached CSRF token or fetches a fresh one from /api/cart/security.
 * @param {boolean} [forceRefresh=false]
 * @returns {Promise<string>}
 */
export async function getCsrfToken(forceRefresh = false) {
    if (cachedCsrfToken && !forceRefresh) {
        return cachedCsrfToken;
    }

    try {
        const res = await fetch('/api/cart/security', {
            method: 'GET',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'application/json' }
        });

        if (!res.ok) throw new Error('Failed to retrieve security token.');
        const data = await res.json();
        cachedCsrfToken = data.token || '';
        return cachedCsrfToken;
    } catch (err) {
        console.warn('[JAS API] CSRF fetch error:', err);
        return '';
    }
}

/**
 * Sends a JSON HTTP request to an internal endpoint.
 * @param {string} url
 * @param {object} [options={}]
 * @param {'GET'|'POST'|'PUT'|'DELETE'} [options.method='GET']
 * @param {any} [options.body=null]
 * @param {boolean} [options.includeCsrf=false]
 * @returns {Promise<{ok: boolean, status: number, data?: any, error?: string}>}
 */
export async function request(url, { method = 'GET', body = null, includeCsrf = false } = {}) {
    const headers = {
        'Accept': 'application/json'
    };

    if (body !== null) {
        headers['Content-Type'] = 'application/json';
    }

    if (includeCsrf && method !== 'GET') {
        const token = await getCsrfToken();
        if (token) {
            headers['X-CSRF-TOKEN'] = token;
        }
    }

    const config = {
        method,
        headers,
        credentials: 'same-origin',
        body: body !== null ? JSON.stringify(body) : undefined
    };

    try {
        let response = await fetch(url, config);

        // Auto-retry once on CSRF expiration (400 or 403)
        if (includeCsrf && (response.status === 400 || response.status === 403)) {
            const freshToken = await getCsrfToken(true);
            if (freshToken) {
                headers['X-CSRF-TOKEN'] = freshToken;
                response = await fetch(url, { ...config, headers });
            }
        }

        let data = null;
        try {
            data = await response.json();
        } catch {
            // Empty response body
        }

        if (response.ok) {
            return { ok: true, status: response.status, data };
        }

        const errorMessage = data?.message || data?.error || `Request failed with status ${response.status}`;
        return { ok: false, status: response.status, error: errorMessage, data };
    } catch (err) {
        console.error(`[JAS API] Error fetching ${url}:`, err);
        return { ok: false, status: 0, error: 'Network connection interrupted. Please try again.' };
    }
}
