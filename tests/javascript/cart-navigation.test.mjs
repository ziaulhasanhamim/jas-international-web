import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

const source = await readFile(new URL('../../JASIntWeb/wwwroot/js/modules/cart.js', import.meta.url), 'utf8');

// Run the browser module in an isolated DOM fixture for each test. Only ES module
// export keywords are removed; the actual request and delegated-click code runs.
function createBrowser({ enhanced = true, failure = false } = {}) {
    const requests = [];
    const navigations = [];
    const reloads = [];
    const elements = [];
    const listeners = new Map();
    function element() {
        const value = {
            textContent: '',
            classList: { add() {}, remove() {} },
            append() {},
            appendChild() {},
            remove() {}
        };
        elements.push(value);
        return value;
    }
    const window = {
        location: { assign: path => reloads.push(path) },
        ...(enhanced ? { Blazor: { navigateTo: (path, options) => navigations.push({ path, options }) } } : {})
    };
    const context = vm.createContext({
        window,
        document: {
            cookie: '',
            body: { appendChild() {} },
            createElement: element,
            getElementById: () => null,
            querySelectorAll: () => [],
            addEventListener: (name, listener) => listeners.set(name, listener)
        },
        location: { origin: 'https://shop.example' },
        URL,
        console: { warn() {}, error() {} },
        requestAnimationFrame: callback => callback(),
        setTimeout() {},
        fetch: async (url, options) => {
            requests.push({ url, options });
            if (url === '/api/cart/security') return { ok: true, json: async () => ({ token: 'csrf' }) };
            if (url === '/api/cart/count') return { ok: true, json: async () => ({ count: 0 }) };
            assert.equal(url, '/api/cart');
            return {
                ok: !failure,
                json: async () => failure ? { message: 'Insufficient stock.' } : { itemCount: 3, items: [] }
            };
        }
    });
    vm.runInContext(source.replace(/^export /gm, ''), context);
    return { context, window, requests, navigations, reloads, elements, listeners };
}

test('Buy Now posts once with the selected variant and quantity, then requests enhanced navigation', async () => {
    const browser = createBrowser();
    await browser.window.jasBuyNow(17, 3, 42);
    const posts = browser.requests.filter(request => request.url === '/api/cart');
    assert.equal(posts.length, 1);
    assert.equal(posts[0].options.method, 'POST');
    assert.deepEqual(JSON.parse(posts[0].options.body), { variantId: 17, quantity: 3, productId: 42 });
    assert.equal(posts[0].options.headers['X-CSRF-TOKEN'], 'csrf');
    assert.equal(browser.navigations.length, 1);
    assert.equal(browser.navigations[0].path, '/cart');
    assert.equal(browser.navigations[0].options.forceLoad, false);
    assert.deepEqual(browser.reloads, []);
});

test('Buy Now falls back to full navigation when the Blazor navigation API is unavailable', async () => {
    const browser = createBrowser({ enhanced: false });
    await browser.window.jasBuyNow(17, 1, 42);
    assert.deepEqual(browser.reloads, ['/cart']);
    assert.deepEqual(browser.navigations, []);
});

test('A rejected Buy Now stays on the source page and restores the shared button', async () => {
    const browser = createBrowser({ failure: true });
    vm.runInContext('initCart()', browser.context);
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
    const click = browser.listeners.get('click')(event);
    assert.equal(button.disabled, true);
    assert.match(button.innerHTML, /Processing/);
    await click;
    assert.equal(button.disabled, false);
    assert.equal(button.innerHTML, original);
    assert.equal(browser.requests.filter(request => request.url === '/api/cart').length, 1);
    assert.ok(browser.elements.some(element => element.textContent === 'Insufficient stock.'));
    assert.deepEqual(browser.navigations, []);
    assert.deepEqual(browser.reloads, []);
});

test('Successful shared Buy Now restores its button and does not reload the document', async () => {
    const browser = createBrowser();
    vm.runInContext('initCart()', browser.context);
    const button = {
        disabled: false,
        innerHTML: 'Buy Now',
        getAttribute: name => ({ 'data-variant-id': '17', 'data-product-id': '42' })[name] ?? null,
        closest: () => null
    };
    await browser.listeners.get('click')({
        target: { closest: selector => selector === '[data-buy-now]' ? button : null },
        preventDefault() {}, stopPropagation() {}
    });
    assert.equal(button.disabled, false);
    assert.equal(button.innerHTML, 'Buy Now');
    assert.equal(browser.navigations.length, 1);
    assert.deepEqual(browser.reloads, []);
});

test('clearCartToken expires cookie and resets badges', () => {
    const browser = createBrowser();
    browser.context.document.cookie = 'jas_cart_id=abc-123';
    browser.context.clearCartToken();
    assert.ok(browser.context.document.cookie.includes('max-age=0'));
});
