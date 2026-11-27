function cleanupOnRemoval(root, cleanup) {
    let disposed = false;
    const observer = new MutationObserver(() => { if (!root.isConnected) dispose(); });
    function dispose() { if (disposed) return; disposed = true; observer.disconnect(); cleanup(); }
    observer.observe(document.body, { childList: true, subtree: true });
    return dispose;
}
function hasFiles(event) { return [...(event.dataTransfer?.types ?? [])].includes('Files'); }
export function dropZone(root) {
    if (!root) return { dispose() {} };
    const isBlocked = input => root.classList.contains('is-disabled') || (input?.disabled ?? false);
    const enter = event => {
        if (!hasFiles(event)) return;
        event.preventDefault();
        event.stopPropagation();
        const input = root.querySelector('input[type=file]');
        if (!isBlocked(input)) root.classList.add('is-dragging');
    };
    const over = event => {
        event.preventDefault();
        event.stopPropagation();
        const input = root.querySelector('input[type=file]');
        if (!hasFiles(event) || isBlocked(input)) {
            if (event.dataTransfer) event.dataTransfer.dropEffect = 'none';
            return;
        }
        root.classList.add('is-dragging');
        if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy';
    };
    const leave = event => { if (!root.contains(event.relatedTarget)) root.classList.remove('is-dragging'); };
    const drop = event => {
        root.classList.remove('is-dragging');
        event.preventDefault();
        event.stopPropagation();
        const input = root.querySelector('input[type=file]');
        const files = event.dataTransfer?.files;
        if (!files || files.length === 0 || !input || isBlocked(input)) return;
        try {
            const dt = new DataTransfer();
            for (const f of files) dt.items.add(f);
            input.files = dt.files;
        } catch {
            input.files = files;
        }
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };
    root.addEventListener('dragenter', enter);
    root.addEventListener('dragover', over);
    root.addEventListener('dragleave', leave);
    root.addEventListener('drop', drop);
    return {
        dispose: cleanupOnRemoval(root, () => {
            root.removeEventListener('dragenter', enter);
            root.removeEventListener('dragover', over);
            root.removeEventListener('dragleave', leave);
            root.removeEventListener('drop', drop);
        })
    };
}
export function gallery(root, receiver) {
    let dragged = null;
    const clear = () => { dragged = null; root.querySelectorAll('.is-drop-target').forEach(item => item.classList.remove('is-drop-target')); };
    const start = event => {
        const item = event.target.closest('[data-media-key]');
        if (!item || root.dataset.uploading === 'true') return;
        dragged = item.dataset.mediaKey;
        event.dataTransfer.setData('text/plain', dragged); event.dataTransfer.effectAllowed = 'move';
    };
    const over = event => {
        const item = event.target.closest('[data-media-index]');
        if (!dragged || !item) return;
        event.preventDefault(); event.dataTransfer.dropEffect = 'move';
        root.querySelectorAll('.is-drop-target').forEach(node => node.classList.remove('is-drop-target'));
        item.classList.add('is-drop-target');
    };
    const drop = event => {
        const item = event.target.closest('[data-media-index]');
        if (!dragged || !item) return;
        event.preventDefault();
        const key = dragged, target = Number(item.dataset.mediaIndex);
        clear();
        root.dispatchEvent(new Event('input', { bubbles: true }));
        receiver.invokeMethodAsync('Reorder', key, target).catch(() => {});
    };
    root.addEventListener('dragstart', start); root.addEventListener('dragover', over); root.addEventListener('drop', drop); root.addEventListener('dragend', clear);
    return { dispose: cleanupOnRemoval(root, () => { clear(); root.removeEventListener('dragstart', start); root.removeEventListener('dragover', over); root.removeEventListener('drop', drop); root.removeEventListener('dragend', clear); }) };
}
