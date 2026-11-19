# Modern Functional JavaScript Architecture Blueprint

This document outlines the architecture, design principles, and patterns for client-side JavaScript in JAS International (`JASIntWeb/wwwroot/js`).

---

## 1. Architectural Philosophy

### A. Functions Over Classes
- **No Class Hierarchies**: Avoid `class` declarations, prototypes, and `this` binding ambiguities.
- **Pure Functions & Closures**: Use modular ES functions, closures for encapsulated state, and pure calculation functions.
- **Single Responsibility**: Each module exports focused, composable functions.

### B. Event-Driven Decoupling
- Modules do **not** tightly couple or call each other's internal DOM manipulation routines directly.
- Communication flows through native DOM `CustomEvent`s using `core/events.js` (`emit`, `on`).
  - Example: When `addToCart(...)` succeeds in `modules/cart.js`, it dispatches `cart:item-added` and `cart:updated`.
  - `modules/cart-drawer.js` listens to `cart:item-added` and smoothly slides open the drawer.
  - `modules/cart-page.js` listens to `cart:updated` and recalculates line items and totals on `/cart`.
  - Header badges listen to `cart:updated` and pulse with the new count.

### C. Global Event Delegation (Blazor Enhanced Navigation Resilient)
- In Blazor Static SSR with Enhanced Navigation (`data-enhance="true"`), navigation swaps the DOM dynamically via `fetch` streaming.
- Attaching event listeners directly to individual DOM nodes (e.g. `button.addEventListener('click', ...)`) breaks when Blazor replaces the HTML.
- **The Rule**: All interactive triggers use declarative `data-*` attributes (`data-cart-add`, `data-cart-qty`, `data-overlay-open`, etc.). Listeners are bound **once** at the `document` level using `event.target.closest('[data-...]')`. They run perpetually across all page transitions without re-binding.

---

## 2. Directory Structure

```
JASIntWeb/wwwroot/js/
├── jas-app.js                 # Functional orchestrator & lifecycle bootstrap
├── core/
│   ├── events.js              # Native CustomEvent pub/sub bus (emit, on, once)
│   ├── api.js                 # Functional HTTP client (JSON, auto-CSRF, errors)
│   └── overlay.js             # Reusable modal/drawer controller blueprint
└── modules/
    ├── cart.js                # Core cart API, 0ms optimistic updates & delegation
    ├── cart-drawer.js         # Animated 60fps cart slide-over modal controller
    ├── cart-page.js           # Full /cart page in-place DOM sync
    ├── wishlist.js            # Wishlist localStorage & Blazor interop controller
    ├── gallery.js             # Product gallery image switching
    ├── reviews.js             # Review submission & star rating
    ├── carousel.js            # Hero banner carousel
    ├── navigation.js          # Progress bars & mobile indicators
    ├── consent.js             # Cookie consent banner
    ├── analytics.js           # Visitor analytics
    └── meta-tracking.js       # Meta pixel & CAPI integration
```

---

## 3. Core Modules Reference

### `core/events.js`
Lightweight event bus using native DOM events:
```javascript
import { emit, on } from '../core/events.js';

// Dispatching an event
emit('cart:updated', { itemCount: 3, subtotal: 4500 });

// Subscribing to an event
const unsubscribe = on('cart:updated', (cart) => {
    console.log('Cart updated:', cart);
});
```

### `core/api.js`
Unified JSON fetch client:
```javascript
import { request } from '../core/api.js';

const res = await request('/api/cart', {
    method: 'POST',
    body: { variantId: 10, quantity: 1 }
});

if (res.ok) {
    console.log('Server cart:', res.data);
} else {
    console.error('Error:', res.error);
}
```

---

## 4. Blueprint: How to Add a New Overlay / Modal

To add any new modal, drawer, or interactive overlay (e.g. a "Quick View" modal, "Size Guide", or "Filter Drawer"):

### Step 1: Write HTML with Declarative Attributes
Create your modal in a Razor component or static layout:
```html
<div id="quick-view-modal" class="fixed inset-0 z-[100] pointer-events-none transition-all duration-300" aria-hidden="true">
    <!-- Backdrop: closes on click -->
    <div data-overlay-backdrop
         data-overlay-close="quick-view-modal"
         class="fixed inset-0 bg-slate-950/60 backdrop-blur-xs opacity-0 transition-opacity duration-300 pointer-events-none cursor-pointer">
    </div>

    <!-- Modal Panel: scales in -->
    <div data-overlay-panel
         role="dialog"
         aria-modal="true"
         class="fixed inset-4 sm:inset-auto sm:top-1/2 sm:left-1/2 sm:-translate-x-1/2 sm:-translate-y-1/2 z-10 max-w-lg w-full bg-white rounded-3xl p-6 shadow-2xl opacity-0 scale-95 transition-all duration-300 pointer-events-none">
        <div class="flex justify-between items-center mb-4">
            <h3 class="font-bold text-lg text-slate-900">Quick View</h3>
            <button type="button" data-overlay-close="quick-view-modal" class="size-8 rounded-full hover:bg-slate-100 flex items-center justify-center font-bold">✕</button>
        </div>
        <div id="quick-view-content">...</div>
    </div>
</div>
```

### Step 2: Triggering the Overlay
Simply place `data-overlay-open="quick-view-modal"` on any button anywhere in the app:
```html
<button type="button" data-overlay-open="quick-view-modal" class="btn-primary">
    Quick View
</button>
```
`core/overlay.js` handles:
- Backdrop fade-in (`opacity-0` -> `opacity-100`).
- Panel animation (`scale-95` -> `scale-100`, `opacity-0` -> `opacity-100`).
- Locking body scroll (`document.body.classList.add('overflow-hidden')`).
- Closing when clicking the backdrop, close button, or pressing **Escape**.
- Restoring scroll and returning focus when closed.

### Step 3: Triggering from JavaScript (Optional)
If your feature needs programmatic control:
```javascript
import { openOverlay, closeOverlay } from '../core/overlay.js';

// Open
openOverlay('quick-view-modal');

// Close
closeOverlay('quick-view-modal');
```

---

## 5. Blueprint: How to Add a New Interactive Feature

1. **Create module**: e.g., `modules/coupon.js` using pure exported functions.
2. **Handle user input with delegation**:
   ```javascript
   export function initCoupon() {
       if (document.__jasCouponDelegated) return;
       document.__jasCouponDelegated = true;

       document.addEventListener('submit', async (e) => {
           const form = e.target.closest('[data-coupon-form]');
           if (!form) return;
           e.preventDefault();

           const input = form.querySelector('input[name="coupon"]');
           const code = input?.value?.trim();
           if (!code) return;

           // Call API & emit event
           const res = await request('/api/coupons/apply', { method: 'POST', body: { code } });
           if (res.ok) {
               emit('coupon:applied', res.data);
           }
       });
   }
   ```
3. **Register in `jas-app.js`**:
   Import `initCoupon` and call it inside `initModules()`. It will automatically re-run safely on initial load and on every Blazor enhanced navigation!
