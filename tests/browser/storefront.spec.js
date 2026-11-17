const { test, expect } = require('@playwright/test');

async function firstProduct(page) {
  await page.goto('/products');
  const link = page.locator('.product-card a[href^="/product/"]').first();
  const href = await link.getAttribute('href');
  expect(href).toBeTruthy();
  return href;
}

test('hot routes render without Blazor circuits or WASM downloads', async ({ page }, testInfo) => {
  const requests = [];
  page.on('request', request => requests.push(request.url()));
  const product = await firstProduct(page);
  for (const path of ['/', '/products', product]) {
    await page.goto(path); await page.waitForLoadState('networkidle');
    await expect(page.locator('h1').first()).toBeVisible();
    expect(requests.filter(url => /\/_blazor(?:[/?]|$)|\.wasm(?:[?]|$)|blazor\.boot\.json/.test(url))).toEqual([]);
  }
  await testInfo.attach('network-resources', { body: JSON.stringify(requests, null, 2), contentType: 'application/json' });
});

test('mobile and desktop product navigation preserves wishlist and adds one cart item', async ({ page }) => {
  const product = await firstProduct(page);
  await page.goto(product);
  await page.evaluate(() => { window.__enhancedNavigationMarker = 'kept'; });
  for (let i = 0; i < 2; i++) {
    await page.locator('a[href="/products"]:visible').first().click();
    await page.waitForURL('**/products');
    await page.locator(`a[href="${product}"]:visible`).first().click();
    await page.waitForURL(`**${product}`);
    expect(await page.evaluate(() => window.__enhancedNavigationMarker)).toBe('kept');
  }
  const wishlist = page.locator('[data-wishlist-toggle]:visible').first();
  await wishlist.click(); await expect(wishlist).toHaveClass(/is-wishlisted/);
  let additions = 0;
  page.on('request', request => { if (request.url().endsWith('/api/cart') && request.method() === 'POST') additions++; });
  const add = page.locator('[data-add-to-cart]:visible').first();
  await expect(add).toBeVisible();
  const response = page.waitForResponse(r => r.url().endsWith('/api/cart') && r.request().method() === 'POST');
  await add.click(); await expect((await response).ok()).toBeTruthy();
  const cart = await page.request.get('/api/cart'); expect(cart.ok()).toBeTruthy();
  const body = await cart.json(); expect(body.items.length).toBeGreaterThan(0);
  expect(additions).toBe(1);
});

test('review submits through protected POST and awaits moderation', async ({ page }) => {
  const product = await firstProduct(page); await page.goto(product);
  await page.getByRole('button', { name: /write a review/i }).click();
  const form = page.locator('#write-review-modal form');
  await form.locator('[name="Name"]').fill('Browser Test Shopper');
  await form.locator('[name="Comment"]').fill('A detailed review submitted through the real browser form.');
  await form.getByRole('button', { name: 'Submit Review' }).click();
  await expect(page.getByText('Thank you. Your review is awaiting moderation.')).toBeVisible();
  await expect(page.locator('#reviews').getByText('Browser Test Shopper', { exact: true })).toHaveCount(0);
});

test('missing consent prevents Meta script and marketing cookies', async ({ page }) => {
  await page.goto('/?fbclid=test-click&utm_source=meta');
  await page.waitForLoadState('networkidle');
  expect((await page.context().cookies()).filter(c => ['_fbp', '_fbc', 'jas_attribution'].includes(c.name))).toEqual([]);
  await expect(page.locator('script[src*="fbevents.js"]')).toHaveCount(0);
});

test('capture storefront and details for release review', async ({ page }, testInfo) => {
  const product = await firstProduct(page);
  for (const [name, path] of [['home', '/'], ['catalog', '/products'], ['details', product]]) {
    await page.goto(path); await page.waitForLoadState('networkidle');
    await page.screenshot({ path: testInfo.outputPath(`${name}.png`), fullPage: false, scale: 'css' });
  }
});


test('payment updates redirect with private token and polling survives SSE failure', async ({ page }) => {
  const token = 'a24341e8-62d7-5e88-98b3-f480664fc33d'; // checked-in development fixture
  await page.route(`**/api/orders/${token}/sse`, route => route.abort());
  await page.route(`**/api/orders/${token}/status`, route => route.fulfill({
    contentType: 'application/json', body: JSON.stringify({ id: 1, publicToken: token, status: 'Delivered', isPaymentVerified: true })
  }));
  await page.goto(`/payment-verification?token=${token}`);
  await page.waitForURL(`**/order/${token}`);
  await expect(page.locator('#fulfillment-title')).toBeVisible();
});

test('cart feedback treats server messages as text and rejects external action links', async ({ page }) => {
  await page.goto('/');
  await page.evaluate(async () => {
    const { showCartToast } = await import('/js/modules/cart.js');
    showCartToast('<img src=x onerror="window.toastInjected=true">', 'error', { text: 'Unsafe', href: 'javascript:alert(1)' });
  });
  const toast = page.locator('#jas-toast-container');
  await expect(toast).toContainText('<img src=x');
  await expect(toast.locator('img, a')).toHaveCount(0);
  expect(await page.evaluate(() => window.toastInjected)).toBeUndefined();
});

test('static signup confirms email before signing in', async ({ page }) => {
  await page.goto('/account/signup');
  await page.locator('input[autocomplete="name"]').fill('Browser Signup');
  await page.locator('input[type="email"]').fill(`browser-${Date.now()}@example.test`);
  await page.locator('input[type="password"]').fill('BrowserSignup123!');
  await page.getByRole('button', { name: 'Create Account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Verify Your Email' })).toBeVisible();
  const code = await page.locator('code').innerText();
  expect(code).toMatch(/^\d{6}$/);
  await page.locator('input[autocomplete="one-time-code"]').fill(code);
  await page.getByRole('button', { name: 'Verify Code & Continue' }).click();
  await page.waitForURL('**/cart');
  await expect(page.locator('h1').first()).toBeVisible();
});

test('hero preserves the entire image and Chromium selects the AVIF picture source', async ({ page }) => {
  // Serve a checked-in image so source selection and geometry need no external CDN.
  await page.route('https://images.unsplash.com/**', route => route.fulfill({
    path: require('path').resolve('JASIntWeb/wwwroot/jas-og-card.avif'), contentType: 'image/avif'
  }));
  await page.goto('/');
  const carousel = page.locator('[data-carousel]');
  const images = carousel.locator('img');
  expect(await images.count()).toBeGreaterThan(1);
  for (const image of await images.all()) {
    await expect.poll(() => image.evaluate(img => img.complete && img.naturalWidth > 0)).toBe(true);
    const dimensions = await image.evaluate(img => ({
      width: img.getBoundingClientRect().width, height: img.getBoundingClientRect().height,
      naturalWidth: img.naturalWidth, naturalHeight: img.naturalHeight,
      currentSrc: img.currentSrc, avif: img.closest('picture').querySelector('source[type="image/avif"]').srcset
    }));
    expect(Math.abs(dimensions.height - dimensions.width * dimensions.naturalHeight / dimensions.naturalWidth)).toBeLessThan(1);
    expect(dimensions.currentSrc).toBe(dimensions.avif);
    expect(new URL(dimensions.currentSrc).searchParams.get('fm')).toBe('avif');
  }
  const height = await carousel.evaluate(el => el.getBoundingClientRect().height);
  await carousel.locator('[data-carousel-dot]').nth(1).click();
  await expect(carousel.locator('[data-carousel-slide]').nth(1)).toHaveAttribute('aria-hidden', 'false');
  expect(Math.abs(await carousel.evaluate(el => el.getBoundingClientRect().height) - height)).toBeLessThan(1);
});
