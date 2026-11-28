import assert from 'node:assert/strict';
import test from 'node:test';

const docListeners = new Map();
const scrollCalls = [];
const scrolledElements = [];
let elementsById = new Map();

globalThis.window = {
    location: { pathname: '/products', search: '', hash: '' },
    scrollTo(options) {
        scrollCalls.push(options);
    }
};

globalThis.document = {
    getElementById: id => elementsById.get(id) ?? null,
    addEventListener(name, listener) {
        const list = docListeners.get(name) ?? [];
        list.push(listener);
        docListeners.set(name, list);
    }
};

const navModule = await import('../../JASIntWeb/wwwroot/js/modules/navigation.js');
navModule.initNavigation();

function submitForm({ scrollTop = false, prevented = false } = {}) {
    scrollCalls.length = 0;
    scrolledElements.length = 0;
    elementsById = new Map();
    const form = {
        tagName: 'FORM',
        dataset: {},
        matches: selector => scrollTop && selector === 'form[data-scroll-top]'
    };
    const submitListener = docListeners.get('submit')[0];
    submitListener({ target: form, defaultPrevented: prevented });
}

test('data-scroll-top form scrolls instantaneously to top when enhanced submission navigates to a new pathname', () => {
    globalThis.window.location = { pathname: '/products', search: '', hash: '' };
    submitForm({ scrollTop: true });

    globalThis.window.location = { pathname: '/cart', search: '', hash: '' };
    navModule.finishNavigation();

    assert.deepEqual(scrollCalls, [{ top: 0, behavior: 'instant' }]);
});

test('data-scroll-top form scrolls to target element when destination URL includes a #block id', () => {
    globalThis.window.location = { pathname: '/products', search: '', hash: '' };
    submitForm({ scrollTop: true });

    elementsById.set('block', {
        scrollIntoView(options) {
            scrolledElements.push({ id: 'block', options });
        }
    });
    globalThis.window.location = { pathname: '/cart', search: '', hash: '#block' };
    navModule.finishNavigation();

    assert.deepEqual(scrollCalls, []);
    assert.deepEqual(scrolledElements, [{ id: 'block', options: { behavior: 'instant' } }]);
});

test('data-scroll-top form skips top scroll if hash exists even when element is missing', () => {
    globalThis.window.location = { pathname: '/products', search: '', hash: '' };
    submitForm({ scrollTop: true });

    globalThis.window.location = { pathname: '/cart', search: '', hash: '#missing' };
    navModule.finishNavigation();

    assert.deepEqual(scrollCalls, []);
});

test('data-scroll-top form preserves scroll when pathname does not change', () => {
    globalThis.window.location = { pathname: '/products', search: '', hash: '' };
    submitForm({ scrollTop: true });

    navModule.finishNavigation();

    assert.deepEqual(scrollCalls, []);
});

test('regular enhanced forms without data-scroll-top preserve scroll position', () => {
    globalThis.window.location = { pathname: '/admin/orders', search: '', hash: '' };
    submitForm({ scrollTop: false });

    globalThis.window.location = { pathname: '/cart', search: '', hash: '' };
    navModule.finishNavigation();

    assert.deepEqual(scrollCalls, []);
});
