let loading;
async function loadEditor() {
    if (window.tinymce) return;
    loading ??= new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/tinymce@6.8.3/tinymce.min.js';
        script.referrerPolicy = 'origin';
        script.onload = resolve;
        script.onerror = () => { loading = null; reject(new Error('Rich editor unavailable')); };
        document.head.append(script);
    });
    await loading;
}
export async function create(element, receiver) {
    await loadEditor();
    if (!element.isConnected) return { dispose() {} };
    let disposed = false;
    const editors = await window.tinymce.init({
        target: element, height: 480, plugins: 'table lists link code image',
        toolbar: 'undo redo | blocks | bold italic underline | bullist numlist | table techspecs | link image | code',
        menubar: false, branding: false,
        setup(editor) {
            editor.ui.registry.addButton('techspecs', { text: 'Tech Specs Template', onAction: () => editor.insertContent('<table><tbody><tr><th>Specification</th><th>Details</th></tr><tr><td>Model</td><td></td></tr><tr><td>Warranty</td><td></td></tr></tbody></table>') });
            editor.on('input change undo redo', () => {
                if (!disposed) receiver.invokeMethodAsync('Changed', editor.getContent()).catch(() => {});
            });
        }
    });
    return { dispose() { disposed = true; for (const editor of editors) editor.remove(); } };
}
