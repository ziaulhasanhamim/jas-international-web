let loading;
async function loadEditor() {
    if (window.tinymce) return;
    loading ??= new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/tinymce@6.8.3/tinymce.min.js';
        script.referrerPolicy = 'origin';
        script.onload = resolve;
        script.onerror = () => { loading = null; script.remove(); reject(new Error('Rich editor unavailable')); };
        document.head.append(script);
    });
    await loading;
}
export function create(element, receiver, options) {
    let disposed = false, starting = false, editor = null;
    let html = element.value, disabled = options.disabled, synchronizing = false, ready = false;
    function sync() {
        if (!editor) return;
        synchronizing = true;
        try {
            if (editor.getContent() !== html) editor.setContent(html);
            html = editor.getContent();
            if (editor.mode.isReadOnly() !== disabled) editor.mode.set(disabled ? 'readonly' : 'design');
        } finally { synchronizing = false; }
    }
    async function start() {
        if (disposed || starting || !element.isConnected || element.getClientRects().length === 0) return;
        starting = true;
        try {
            await loadEditor();
            if (disposed || !element.isConnected) return;
            html = element.value;
            const editors = await window.tinymce.init({
                target: element, height: options.height, resize: true, iframe_aria_text: options.label,
                plugins: 'table lists link code image',
                toolbar: `undo redo | blocks | bold italic underline | bullist numlist | table ${options.specifications ? 'techspecs' : ''} | link image | code`,
                block_formats: 'Paragraph=p; Heading 2=h2; Heading 3=h3; Heading 4=h4; Preformatted=pre',
                menubar: false, branding: false, readonly: disabled,
                formats: { underline: { inline: 'u' } },
                content_style: 'body{font-family:Arial,sans-serif;font-size:14px;line-height:1.7}img{max-width:100%;height:auto}table{border-collapse:collapse}td,th{border:1px solid #cbd5e1;padding:8px}',
                setup(instance) {
                    if (options.specifications) instance.ui.registry.addButton('techspecs', { text: 'Tech Specs Template', onAction: () => instance.insertContent('<table><tbody><tr><th>Specification</th><th>Details</th></tr><tr><td>Model</td><td></td></tr><tr><td>Warranty</td><td></td></tr></tbody></table>') });
                    instance.on('input change undo redo blur', () => {
                        if (ready && !disposed && !synchronizing && !disabled) {
                            const changed = instance.getContent();
                            if (changed === html) return;
                            html = changed;
                            receiver.invokeMethodAsync('Changed', html).catch(() => {});
                        }
                    });
                }
            });
            if (disposed) { for (const item of editors) item.remove(); return; }
            editor = editors[0];
            observer.disconnect();
            sync();
            ready = true;
        } catch {
            if (!disposed) receiver.invokeMethodAsync('Unavailable').catch(() => {});
        }
    }
    // Collapsed optional sections initialize only when opened, avoiding zero-width editors.
    const observer = new ResizeObserver(start);
    const removalObserver = new MutationObserver(() => { if (!element.isConnected) dispose(); });
    removalObserver.observe(document.body, { childList: true, subtree: true });
    function dispose() {
        if (disposed) return;
        disposed = true; observer.disconnect(); removalObserver.disconnect(); editor?.remove();
    }
    observer.observe(element);
    start();
    return {
        update(value, readOnly) { if (disposed) return; html = value ?? ''; disabled = readOnly; sync(); },
        dispose
    };
}
