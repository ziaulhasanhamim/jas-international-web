export function remember(key, value) { sessionStorage.setItem(key, value); }
export function read(key) { return sessionStorage.getItem(key); }
export function forget(key) { sessionStorage.removeItem(key); }
export function saveLocalDraft(key, value) {
    try { localStorage.setItem(key, typeof value === 'string' ? value : JSON.stringify(value)); } catch (e) {}
}
export function getLocalDraft(key) {
    try { return localStorage.getItem(key); } catch (e) { return null; }
}
export function removeLocalDraft(key) {
    try { localStorage.removeItem(key); } catch (e) {}
}
export function exportDraft(value) {
    const url = URL.createObjectURL(new Blob([value], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'product-draft.json'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
