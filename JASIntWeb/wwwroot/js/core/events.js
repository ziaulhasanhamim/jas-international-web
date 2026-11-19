/**
 * JAS International - Core Event Bus (ES2024+)
 * Functional pub/sub utility wrapping native DOM CustomEvents.
 * Decouples modules and provides event-driven architecture across the application.
 */

/**
 * Dispatches a custom event with optional payload data.
 * @param {string} eventName - Name of the event (e.g., 'cart:updated', 'drawer:opened')
 * @param {any} [detail=null] - Payload object or value attached to the event
 * @param {EventTarget} [target=document] - Target element to dispatch from
 */
export function emit(eventName, detail = null, target = document) {
    const event = new CustomEvent(eventName, {
        bubbles: true,
        cancelable: true,
        detail
    });
    target.dispatchEvent(event);
}

/**
 * Subscribes a listener to a custom event.
 * @param {string} eventName - Name of the event to listen for
 * @param {Function} handler - Callback receiving (event, event.detail)
 * @param {EventTarget} [target=document] - Target element to listen on
 * @param {AddEventListenerOptions} [options] - Optional listener options
 * @returns {Function} Unsubscribe function
 */
export function on(eventName, handler, target = document, options = {}) {
    const listener = (event) => handler(event.detail, event);
    target.addEventListener(eventName, listener, options);
    return () => target.removeEventListener(eventName, listener, options);
}

/**
 * Subscribes a listener to a custom event for a single invocation.
 * @param {string} eventName
 * @param {Function} handler
 * @param {EventTarget} [target=document]
 */
export function once(eventName, handler, target = document) {
    on(eventName, handler, target, { once: true });
}
