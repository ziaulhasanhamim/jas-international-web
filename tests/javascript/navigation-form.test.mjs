import assert from 'node:assert/strict';
import test from 'node:test';

const docListeners = new Map();
const scrollCalls = [];
const scrolledElements = [];
let elementsById = new Map();

function createMockClassList() {
    const set = new Set();
    return {
        add: (...names) => names.forEach(n => set.add(n)),
        remove: (...names) => names.forEach(n => set.delete(n)),
        contains: name => set.has(name)
    };
}

const navProgressBar = { style: {} };
const navProgress = { hidden: true, style: {}, querySelector: () => navProgressBar };
const navCapsule = { hidden: true, classList: createMockClassList() };

globalThis.Element = class Element {};
globalThis.requestAnimationFrame = cb => { cb(); return 1; };
globalThis.cancelAnimationFrame = () => {};
globalThis.window = {
    location: { pathname: '/products', search: '', hash: '' },
    scrollTo(options) {
        scrollCalls.push(options);
    }
};

globalThis.document = {
    getElementById(id) {
        if (id === 'navigation-progress') return navProgress;
        if (id === 'navigation-capsule') return navCapsule;
        return elementsById.get(id) ?? null;
    },
    addEventListener(name, listener) {
        const list = docListeners.get(name) ?? [];
        list.push(listener);
        docListeners.set(name, list);
    }
};

const navModule = await import('../../JASIntWeb/wwwroot/js/modules/navigation.js');
navModule.initNavigation();

function submitForm({ scrollTop = false, noNavIndicator = false, submitIndication = false, disableAll = false, spinner = null, buttons = [], prevented = false } = {}) {
    scrollCalls.length = 0;
    scrolledElements.length = 0;
    elementsById = new Map();
    const attrs = new Map();
    const form = {
        tagName: 'FORM',
        dataset: {
            ...(submitIndication ? { submitIndication: 'true' } : {}),
            ...(disableAll ? { disableSubmits: 'all' } : {})
        },
        matches: selector => scrollTop && selector === 'form[data-scroll-top]',
        closest: selector => (noNavIndicator && selector === '[data-no-nav-indicator]') ? form : null,
        hasAttribute: name => (scrollTop && name === 'data-scroll-top') || (noNavIndicator && name === 'data-no-nav-indicator') || attrs.has(name),
        setAttribute: (k, v) => attrs.set(k, v),
        getAttribute: k => attrs.get(k) ?? null,
        removeAttribute: k => attrs.delete(k),
        querySelectorAll: selector => selector === '.form-submit-spinner'
            ? (spinner ? [spinner] : [])
            : (selector === 'button[type="submit"]' ? buttons : []),
        querySelector: () => buttons[0] ?? null
    };
    const submitListener = docListeners.get('submit')[0];
    submitListener({ target: form, submitter: buttons[0] ?? null, defaultPrevented: prevented });
    return form;
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

test('data-no-nav-indicator disables global progress bar and capsule for forms and links', () => {
    navProgress.hidden = true;
    navCapsule.hidden = true;

    submitForm({ noNavIndicator: true });
    navModule.startNavigation();
    assert.equal(navProgress.hidden, true);
    assert.equal(navCapsule.hidden, true);
    navModule.finishNavigation();

    class MockLink extends globalThis.Element {
        closest(selector) {
            return selector === 'a[href]' || selector === '[data-no-nav-indicator]' ? this : null;
        }
        hasAttribute(name) {
            return name === 'data-no-nav-indicator';
        }
    }
    const clickListener = docListeners.get('click')[0];
    clickListener({ target: new MockLink(), defaultPrevented: false });
    navModule.startNavigation();
    assert.equal(navProgress.hidden, true);
    navModule.finishNavigation();

    // Subsequent regular navigation still shows the progress bar
    submitForm({ noNavIndicator: false });
    navModule.startNavigation();
    assert.equal(navProgress.hidden, false);
    navModule.finishNavigation();
});

test('cart stepper submit shows inline quantity spinner, disables all buttons, and restores on finish', async () => {
    const hiddenSet = new Set(['hidden']);
    const spinner = {
        hasAttribute: name => hiddenSet.has(name),
        removeAttribute: name => hiddenSet.delete(name),
        toggleAttribute: (name, force) => force ? hiddenSet.add(name) : hiddenSet.delete(name)
    };
    const decBtn = { tagName: 'BUTTON', disabled: false, hasAttribute: () => false, closest: () => null };
    const incBtn = { tagName: 'BUTTON', disabled: false, hasAttribute: () => false, closest: () => null };

    const form = submitForm({
        noNavIndicator: true,
        submitIndication: true,
        disableAll: true,
        spinner,
        buttons: [incBtn, decBtn]
    });

    await Promise.resolve();
    assert.equal(form.getAttribute('aria-busy'), 'true');
    assert.equal(spinner.hasAttribute('hidden'), false);
    assert.equal(incBtn.disabled, true);
    assert.equal(decBtn.disabled, true);

    navModule.finishNavigation();
    assert.equal(form.getAttribute('aria-busy'), null);
    assert.equal(spinner.hasAttribute('hidden'), true);
    assert.equal(incBtn.disabled, false);
    assert.equal(decBtn.disabled, false);
});
