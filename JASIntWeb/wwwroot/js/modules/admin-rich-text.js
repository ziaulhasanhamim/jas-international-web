let loading;
async function uploadImageBlob(blob, filename) {
    const tokenRes = await fetch('/api/admin/uploads/security', { cache: 'no-store' });
    if (!tokenRes.ok) throw new Error('Please sign in as an administrator to upload images.');
    const { token } = await tokenRes.json();
    const formData = new FormData();
    formData.append('file', blob, filename || 'image.png');
    const res = await fetch('/api/admin/uploads/image', {
        method: 'POST',
        headers: { 'X-CSRF-TOKEN': token },
        body: formData,
        signal: AbortSignal.timeout(60000)
    });
    if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || err.message || `Upload failed (HTTP ${res.status})`);
    }
    const data = await res.json();
    if (!data?.url) throw new Error('No image URL returned from storage');
    return data.url;
}
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
                target: element,
                height: options.height || 480,
                min_height: 350,
                max_height: 1200,
                resize: true,
                iframe_aria_text: options.label,
                plugins: 'table lists link code image fullscreen',
                toolbar: `undo redo | blocks | bold italic underline | bullist numlist | table ${options.specifications ? 'techspecs' : ''} productcard | link image | code | fullscreen`,
                table_toolbar: 'tableprops tabledelete | tableinsertrowbefore tableinsertrowafter tabledeleterow | tableinsertcolbefore tableinsertcolafter tabledeletecol',
                contextmenu: 'link image table',
                block_formats: 'Paragraph=p; Heading 2=h2; Heading 3=h3; Heading 4=h4; Preformatted=pre',
                menubar: false, branding: false, readonly: disabled,
                formats: { underline: { inline: 'u' } },
                content_style: `
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                        font-size: 15px;
                        line-height: 1.7;
                        color: #334155;
                        padding: 16px 20px;
                        margin: 0;
                        background-color: #ffffff;
                    }
                    h2 { font-size: 1.45rem; font-weight: 700; color: #0f172a; margin: 1.8rem 0 0.6rem 0; line-height: 1.35; }
                    h3 { font-size: 1.15rem; font-weight: 700; color: #1e293b; margin: 1.4rem 0 0.5rem 0; line-height: 1.4; }
                    h4 { font-size: 1rem; font-weight: 600; color: #334155; margin: 1.2rem 0 0.4rem 0; }
                    p { margin: 0 0 1rem 0; line-height: 1.7; }
                    ul, ol { padding-left: 1.5rem; margin: 0 0 1.25rem 0; }
                    li { margin-bottom: 0.35rem; line-height: 1.6; }
                    img { max-width: 100%; height: auto; border-radius: 8px; }

                    /* Inline links */
                    a {
                        color: #2563eb;
                        font-weight: 600;
                        text-decoration: underline;
                        text-underline-offset: 3px;
                    }
                    a:hover { color: #1d4ed8; }

                    /* Button links inside content */
                    a.btn-primary,
                    a[class*="bg-blue"],
                    a[class*="text-white"],
                    .guide-product-btn {
                        display: inline-flex !important;
                        align-items: center !important;
                        gap: 6px !important;
                        background-color: #2563eb !important;
                        color: #ffffff !important;
                        font-size: 13px !important;
                        font-weight: 700 !important;
                        padding: 8px 18px !important;
                        border-radius: 12px !important;
                        text-decoration: none !important;
                        margin-top: 8px !important;
                        box-shadow: 0 1px 2px rgba(37,99,235,0.2) !important;
                    }
                    a[class*="text-white"]:hover,
                    a[class*="bg-blue"]:hover {
                        background-color: #1d4ed8 !important;
                        color: #ffffff !important;
                    }

                    /* Product Cards in Guide/Product Rich Text */
                    div[class*="border"][class*="rounded"], .guide-product-card {
                        display: flex !important;
                        flex-direction: row !important;
                        align-items: center !important;
                        gap: 22px !important;
                        border: 1px solid #e2e8f0 !important;
                        border-radius: 16px !important;
                        background-color: #ffffff !important;
                        padding: 18px 22px !important;
                        margin: 22px 0 !important;
                        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05), 0 1px 2px rgba(0, 0, 0, 0.03) !important;
                    }
                    div[class*="border"][class*="rounded"] img, .guide-product-card img {
                        width: 130px !important;
                        height: 130px !important;
                        max-width: 130px !important;
                        max-height: 130px !important;
                        object-fit: contain !important;
                        border-radius: 14px !important;
                        background-color: #f8fafc !important;
                        border: 1px solid #f1f5f9 !important;
                        flex-shrink: 0 !important;
                        margin: 0 !important;
                        display: block !important;
                        padding: 4px !important;
                    }
                    div[class*="border"][class*="rounded"] > div, .guide-product-card > div {
                        flex: 1 1 auto !important;
                        min-width: 0 !important;
                    }
                    div[class*="border"][class*="rounded"] h3, .guide-product-card h3 {
                        font-size: 16px !important;
                        font-weight: 700 !important;
                        color: #0f172a !important;
                        margin: 0 0 4px 0 !important;
                        line-height: 1.3 !important;
                    }
                    div[class*="border"][class*="rounded"] p, .guide-product-card p {
                        font-size: 13px !important;
                        color: #64748b !important;
                        margin: 0 0 8px 0 !important;
                        line-height: 1.5 !important;
                    }
                    div[class*="border"][class*="rounded"] span[class*="text-blue"], .guide-product-card .price-current {
                        font-size: 18px !important;
                        font-weight: 800 !important;
                        color: #2563eb !important;
                        margin-right: 8px !important;
                    }
                    div[class*="border"][class*="rounded"] span[class*="line-through"], .guide-product-card .price-original {
                        font-size: 13px !important;
                        color: #94a3b8 !important;
                        text-decoration: line-through !important;
                        margin-right: 8px !important;
                    }
                    div[class*="border"][class*="rounded"] span[class*="bg-emerald"], .guide-product-card .price-badge {
                        display: inline-block !important;
                        font-size: 11px !important;
                        font-weight: 700 !important;
                        color: #047857 !important;
                        background-color: #ecfdf5 !important;
                        border: 1px solid #a7f3d0 !important;
                        padding: 2px 8px !important;
                        border-radius: 9999px !important;
                    }

                    /* Tables */
                    table {
                        border-collapse: collapse !important;
                        width: 100% !important;
                        margin: 16px 0 !important;
                        border: 1px solid #cbd5e1 !important;
                        border-radius: 8px !important;
                    }
                    th, td {
                        border: 1px solid #cbd5e1 !important;
                        padding: 8px 14px !important;
                        font-size: 13.5px !important;
                    }
                    th {
                        background-color: #f1f5f9 !important;
                        font-weight: 700 !important;
                        color: #0f172a !important;
                    }
                    tr:nth-child(even) td { background-color: #f8fafc !important; }
                    tr:hover td { background-color: rgba(59,130,246,0.04) !important; }

                    /* Blockquotes */
                    blockquote {
                        border-left: 4px solid #3b82f6 !important;
                        padding: 8px 14px !important;
                        background-color: #f8fafc !important;
                        border-radius: 0 8px 8px 0 !important;
                        margin: 16px 0 !important;
                        color: #475569 !important;
                        font-style: italic !important;
                    }
                `,
                automatic_uploads: true,
                paste_data_images: true,
                images_upload_handler: (blobInfo) => uploadImageBlob(blobInfo.blob(), blobInfo.filename()),
                file_picker_types: 'image',
                file_picker_callback: (callback, value, meta) => {
                    if (meta.filetype === 'image') {
                        const input = document.createElement('input');
                        input.type = 'file';
                        input.accept = 'image/png,image/jpeg,image/webp,image/avif,image/gif,image/svg+xml';
                        input.onchange = async () => {
                            const file = input.files?.[0];
                            if (!file) return;
                            try {
                                const url = await uploadImageBlob(file, file.name);
                                callback(url, { alt: file.name });
                            } catch (err) {
                                alert(err.message || 'Image upload failed');
                            }
                        };
                        input.click();
                    }
                },
                setup(instance) {
                    if (options.specifications) {
                        instance.ui.registry.addButton('techspecs', {
                            text: 'Tech Specs Template',
                            onAction: () => instance.insertContent('<table><tbody><tr><th>Specification</th><th>Details</th></tr><tr><td>Model</td><td></td></tr><tr><td>Warranty</td><td></td></tr></tbody></table>')
                        });
                    }
                    instance.ui.registry.addButton('productcard', {
                        text: 'Product Card',
                        icon: 'embed',
                        tooltip: 'Insert a featured product card template',
                        onAction: () => {
                            instance.insertContent(`
<div class="my-6 rounded-2xl border border-slate-200 bg-white p-4 sm:p-6 shadow-sm flex flex-col sm:flex-row items-center gap-6">
  <img class="size-36 rounded-2xl object-cover bg-slate-50 border border-slate-100 shrink-0" src="https://media.jasinternational.com.bd/images/static/06be0c161d334131b1c7e0d9848b117d.webp" alt="Product Name" loading="lazy" />
  <div class="space-y-2 text-sm">
    <h3 class="text-base font-bold text-slate-900 m-0">Product Name</h3>
    <p class="text-xs text-slate-500 m-0">Key specification highlights &bull; Bullet 2 &bull; Bullet 3</p>
    <div class="flex items-center gap-3 pt-1">
      <span class="text-lg font-black text-blue-600">৳2,999</span>
      <span class="text-xs text-slate-400 line-through">৳3,499</span>
      <span class="rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-bold text-emerald-700 border border-emerald-200">Save ৳500</span>
    </div>
    <div class="pt-2">
      <a class="inline-flex items-center gap-1.5 rounded-xl bg-blue-600 px-4 py-2 text-xs font-bold text-white hover:bg-blue-700 transition" href="/product/product-slug">
        View Product &amp; Order Online &rarr;
      </a>
    </div>
  </div>
</div><p></p>`);
                        }
                    });
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
