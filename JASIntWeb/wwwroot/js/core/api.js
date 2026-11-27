/**
 * JAS International - Core Functional API Client (ES2024+)
 * Fetch wrapper supporting JSON and FormData payloads, per-endpoint
 * CSRF token caching with 400/403 refresh retry, and unified error mapping.
 */

const csrfTokenCache = new Map();

/**
 * Retrieves a cached CSRF token or fetches a fresh one from the given endpoint.
 * @param {boolean} [forceRefresh=false]
 * @param {string} [securityUrl='/api/cart/security']
 * @returns {Promise<string>}
 */
export async function getCsrfToken(forceRefresh = false, securityUrl = '/api/cart/security') {
    if (!forceRefresh && csrfTokenCache.has(securityUrl)) {
        return csrfTokenCache.get(securityUrl);
    }

    try {
        const res = await fetch(securityUrl, {
            method: 'GET',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'application/json' }
        });

        if (!res.ok) throw new Error('Failed to retrieve security token.');
        const data = await res.json();
        const token = data.token || '';
        if (token) {
            csrfTokenCache.set(securityUrl, token);
        } else {
            csrfTokenCache.delete(securityUrl);
        }
        return token;
    } catch (err) {
        console.warn('[JAS API] CSRF fetch error:', err);
        return '';
    }
}

/**
 * Clears cached CSRF token(s) so the next request fetches a fresh token.
 * @param {string} [securityUrl]
 */
export function clearCsrfToken(securityUrl) {
    if (securityUrl) {
        csrfTokenCache.delete(securityUrl);
    } else {
        csrfTokenCache.clear();
    }
}

/**
 * Sends an HTTP request (JSON or FormData) to an internal endpoint.
 * @param {string} url
 * @param {object} [options={}]
 * @param {'GET'|'POST'|'PUT'|'DELETE'} [options.method='GET']
 * @param {any} [options.body=null]
 * @param {boolean} [options.includeCsrf=false]
 * @param {string} [options.csrfUrl='/api/cart/security']
 * @param {Record<string, string>} [options.headers={}]
 * @param {AbortSignal} [options.signal]
 * @returns {Promise<{ok: boolean, status: number, data?: any, error?: string, headers?: Headers}>}
 */
export async function request(url, {
    method = 'GET',
    body = null,
    includeCsrf = false,
    csrfUrl = '/api/cart/security',
    headers: extraHeaders = {},
    signal
} = {}) {
    const isFormData = typeof FormData !== 'undefined' && body instanceof FormData;
    const headers = {
        'Accept': 'application/json',
        ...extraHeaders
    };

    if (body !== null && !isFormData && !headers['Content-Type']) {
        headers['Content-Type'] = 'application/json';
    }

    if (includeCsrf && method !== 'GET') {
        const token = await getCsrfToken(false, csrfUrl);
        if (token) {
            headers['X-CSRF-TOKEN'] = token;
        }
    }

    const requestBody = body === null
        ? undefined
        : (isFormData || typeof body === 'string' ? body : JSON.stringify(body));

    const config = {
        method,
        headers,
        credentials: 'same-origin',
        cache: method === 'GET' ? 'no-store' : undefined,
        body: requestBody,
        signal
    };

    try {
        let response = await fetch(url, config);

        // Auto-retry once on CSRF expiration (400 or 403)
        if (includeCsrf && method !== 'GET' && (response.status === 400 || response.status === 403)) {
            const freshToken = await getCsrfToken(true, csrfUrl);
            if (freshToken) {
                const retryHeaders = { ...headers, 'X-CSRF-TOKEN': freshToken };
                response = await fetch(url, { ...config, headers: retryHeaders });
            }
        }

        let data = null;
        try {
            data = await response.json();
        } catch {
            // Empty or non-JSON response body
        }

        if (response.ok) {
            return { ok: true, status: response.status, data, headers: response.headers };
        }

        const errorMessage = data?.message || data?.error || `Request failed with status ${response.status}`;
        return { ok: false, status: response.status, error: errorMessage, data, headers: response.headers };
    } catch (err) {
        if (err?.name !== 'AbortError') {
            console.error(`[JAS API] Error fetching ${url}:`, err);
        }
        return { ok: false, status: 0, error: 'Network connection interrupted. Please try again.' };
    }
}
