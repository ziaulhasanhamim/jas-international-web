import assert from 'node:assert/strict';
import test from 'node:test';

// Establish minimal global browser environment before importing real ES modules
const listeners = new Map();
const dispatchedEvents = [];
const requests = [];
const navigations = [];
const reloads = [];
const elements = [];

function createMockElement(initial = {}) {
    const classes = new Set();
    const attrs = new Map();
    const el = {
        textContent: '',
        innerHTML: '',
        disabled: false,
        dataset: {},
        classList: {
            add(...names) { names.forEach(n => classes.add(n)); },
            remove(...names) { names.forEach(n => classes.delete(n)); },
            contains(name) { return classes.has(name); },
            toggle(name, force) {
                if (force === undefined) {
                    if (classes.has(name)) { classes.delete(name); return false; }
                    classes.add(name); return true;
                }
                if (force) classes.add(name); else classes.delete(name);
                return force;
            }
        },
        setAttribute(k, v) { attrs.set(k, String(v)); },
        getAttribute(k) { return attrs.get(k) ?? null; },
        append() {},
        appendChild() {},
        remove() {},
        querySelector: () => null,
        querySelectorAll: () => [],
        focus() {},
        ...initial
    };
    elements.push(el);
    return el;
}

class MockCustomEvent {
    constructor(type, init = {}) {
        this.type = type;
        this.detail = init.detail;
        this.bubbles = init.bubbles ?? false;
        this.cancelable = init.cancelable ?? false;
    }
}

let fetchBehavior = { failure: false };

globalThis.CustomEvent = MockCustomEvent;
globalThis.window = {
    location: { assign: path => reloads.push(path) },
    Blazor: { navigateTo: (path, options) => navigations.push({ path, options }) }
};
globalThis.document = {
    cookie: '',
    body: { appendChild() {}, classList: createMockElement().classList },
    activeElement: null,
    createElement: () => createMockElement(),
    getElementById: () => null,
    querySelectorAll: () => [],
    addEventListener(name, listener) {
        if (!listeners.has(name)) listeners.set(name, []);
        listeners.get(name).push(listener);
    },
    removeEventListener(name, listener) {
        const list = listeners.get(name);
        if (!list) return;
        const idx = list.indexOf(listener);
        if (idx >= 0) list.splice(idx, 1);
    },
    dispatchEvent(event) {
        dispatchedEvents.push(event);
        return true;
    }
};
globalThis.requestAnimationFrame = callback => callback();
globalThis.fetch = async (url, options = {}) => {
    requests.push({ url, options });
    if (url === '/api/cart/security') {
        return { ok: true, status: 200, json: async () => ({ token: 'csrf' }), headers: new Headers() };
    }
    if (url === '/api/cart/count') {
        return { ok: true, status: 200, json: async () => ({ count: 0 }), headers: new Headers() };
    }
    assert.equal(url, '/api/cart');
    return {
        ok: !fetchBehavior.failure,
        status: fetchBehavior.failure ? 409 : 200,
        headers: new Headers(),
        json: async () => fetchBehavior.failure ? { message: 'Insufficient stock.' } : { itemCount: 3, items: [] }
    };
};

const cartModule = await import('../../JASIntWeb/wwwroot/js/modules/cart.js');
const apiModule = await import('../../JASIntWeb/wwwroot/js/core/api.js');

function resetState({ enhanced = true, failure = false } = {}) {
    requests.length = 0;
    navigations.length = 0;
    reloads.length = 0;
    elements.length = 0;
    dispatchedEvents.length = 0;
    fetchBehavior.failure = failure;
    globalThis.document.cookie = '';
    apiModule.clearCsrfToken();
    globalThis.window.Blazor = enhanced
        ? { navigateTo: (path, options) => navigations.push({ path, options }) }
        : undefined;
}

test('Buy Now posts once with the selected variant and quantity, then requests enhanced navigation', async () => {
    resetState({ enhanced: true, failure: false });
    await cartModule.buyNow(17, 3, 42);
    const posts = requests.filter(r => r.url === '/api/cart');
    assert.equal(posts.length, 1);
    assert.equal(posts[0].options.method, 'POST');
    assert.deepEqual(JSON.parse(posts[0].options.body), { variantId: 17, quantity: 3, productId: 42 });
    assert.equal(posts[0].options.headers['X-CSRF-TOKEN'], 'csrf');
    assert.equal(navigations.length, 1);
    assert.equal(navigations[0].path, '/cart');
    assert.equal(navigations[0].options.forceLoad, false);
    assert.deepEqual(reloads, []);
});

test('Buy Now falls back to full navigation when the Blazor navigation API is unavailable', async () => {
    resetState({ enhanced: false, failure: false });
    await cartModule.buyNow(17, 1, 42);
    assert.deepEqual(reloads, ['/cart']);
    assert.deepEqual(navigations, []);
});

test('CSRF token is cached across consecutive cart mutations via core/api.js', async () => {
    resetState({ enhanced: true, failure: false });
    await cartModule.addToCart(17, 1, 42, { showToast: false });
    await cartModule.addToCart(18, 2, 42, { showToast: false });
    const csrfCalls = requests.filter(r => r.url === '/api/cart/security');
    assert.equal(csrfCalls.length, 1);
});

test('A rejected Buy Now stays on the source page and restores the shared button', async () => {
    resetState({ enhanced: true, failure: true });
    cartModule.initCart();
    const original = '<span>Buy Now</span>';
    const button = {
        disabled: false,
        innerHTML: original,
        getAttribute: name => ({ 'data-variant-id': '17', 'data-product-id': '42' })[name] ?? null,
        closest: () => ({ querySelector: () => ({ value: '3' }) })
    };
    const event = {
        target: { closest: selector => selector === '[data-buy-now]' ? button : null },
        preventDefault() {},
        stopPropagation() {}
    };
    const clickListener = listeners.get('click')[0];
    const clickPromise = clickListener(event);
    assert.equal(button.disabled, true);
    assert.match(button.innerHTML, /Processing/);
    await clickPromise;
    assert.equal(button.disabled, false);
    assert.equal(button.innerHTML, original);
    assert.equal(requests.filter(r => r.url === '/api/cart').length, 1);
    assert.ok(elements.some(element => element.textContent === 'Insufficient stock.'));
    assert.deepEqual(navigations, []);
    assert.deepEqual(reloads, []);
});

test('Successful shared Buy Now restores its button and does not reload the document', async () => {
    resetState({ enhanced: true, failure: false });
    cartModule.initCart();
    const button = {
        disabled: false,
        innerHTML: 'Buy Now',
        getAttribute: name => ({ 'data-variant-id': '17', 'data-product-id': '42' })[name] ?? null,
        closest: () => null
    };
    const clickListener = listeners.get('click')[0];
    await clickListener({
        target: { closest: selector => selector === '[data-buy-now]' ? button : null },
        preventDefault() {},
        stopPropagation() {}
    });
    assert.equal(button.disabled, false);
    assert.equal(button.innerHTML, 'Buy Now');
    assert.equal(navigations.length, 1);
    assert.deepEqual(reloads, []);
});

test('clearCartToken expires cookie and emits cart:cleared', () => {
    resetState();
    globalThis.document.cookie = 'jas_cart_id=abc-123';
    cartModule.clearCartToken();
    assert.ok(globalThis.document.cookie.includes('max-age=0'));
    assert.ok(dispatchedEvents.some(e => e.type === 'cart:cleared'));
});

test('Buy Now updates cart state and badges without showing toast before navigation', async () => {
    resetState();
    await cartModule.buyNow(101, 1, 55);
    const eventNames = dispatchedEvents.map(e => e.type);
    assert.ok(eventNames.includes('cart:updated'), 'Should update cart state and badges');
});
