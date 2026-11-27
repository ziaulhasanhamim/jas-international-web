import { request } from '../core/api.js';

let loading;
async function uploadImageBlob(blob, filename) {
    const formData = new FormData();
    formData.append('file', blob, filename || 'image.png');
    const res = await request('/api/admin/uploads/image', {
        method: 'POST',
        body: formData,
        includeCsrf: true,
        csrfUrl: '/api/admin/uploads/security',
        signal: AbortSignal.timeout(60000)
    });
    if (!res.ok) {
        throw new Error(res.error || `Upload failed (HTTP ${res.status})`);
    }
    if (!res.data?.url) throw new Error('No image URL returned from storage');
    return res.data.url;
}
async function loadEditor() {
    if (window.tinymce) return;
    loading ??= new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/tinymce@6.8.3/tinymce.min.js';
        script.referrerPolicy = 'origin';
        script.onload = () => resolve();
        script.onerror = () => {
            loading = null;
            script.remove();
            reject(new Error('Failed to load TinyMCE editor from CDN'));
        };
        document.head.append(script);
    });
    await loading;
}
export function create(element, receiver, options) {
    options = options || {};
    let disposed = false, starting = false, editor = null;
    let html = element.value, disabled = options.disabled ?? options.Disabled ?? false, synchronizing = false, ready = false;
    let layoutRetries = 0;

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
        if (disposed || starting || !element.isConnected) return;
        if (ready && editor) return;

        // Check if element is inside a closed <details> or hidden container
        const isHiddenContainer = element.closest('details:not([open]), [hidden]');
        if (isHiddenContainer) {
            // Wait for user to open the container (ResizeObserver will trigger start)
            return;
        }

        // Handle momentary zero-geometry during Blazor enhanced navigation DOM settling
        if (element.getClientRects().length === 0) {
            if (layoutRetries < 5) {
                layoutRetries++;
                requestAnimationFrame(() => {
                    if (!disposed && element.isConnected) start();
                });
                return;
            }
        }
        starting = true;
        try {
            await loadEditor();
            if (disposed || !element.isConnected) return;
            html = element.value;

            // Always purge any stale/detached editor before mounting
            if (window.tinymce) {
                if (element.id && window.tinymce.get(element.id)) {
                    try { window.tinymce.get(element.id).remove(); } catch (e) {}
                }
                for (const ed of [...(window.tinymce.editors || [])]) {
                    if (ed.getElement() === element || !ed.getElement()?.isConnected) {
                        try { ed.remove(); } catch (e) {}
                    }
                }
            }

            // Assign a unique instance ID to avoid colliding with any lingering reference in TinyMCE cache
            const instanceId = 'tinymce_' + Math.random().toString(36).substring(2, 9) + '_' + Date.now();
            element.id = instanceId;
            const targetId = instanceId;

            const rawHeight = options.height || options.Height;
            const parsedHeight = parseInt(rawHeight, 10);
            const targetHeight = (!isNaN(parsedHeight) && parsedHeight >= 350) ? parsedHeight : 700;

            const editors = await window.tinymce.init({
                target: element,
                height: targetHeight,
                min_height: 500,
                max_height: 1600,
                resize: true,
                promotion: false,
                menubar: false,
                branding: false,
                readonly: disabled,
                iframe_aria_text: options.label || options.Label || 'Content editor',
                plugins: 'table lists link code image fullscreen',
                toolbar_mode: 'wrap',
                toolbar: `undo redo | blocks styles | bold italic underline strikethrough | forecolor backcolor | bullist numlist | table ${options.specifications || options.Specifications ? 'techspecs' : ''} productcard calloutbox | link image | removeformat | code fullscreen`,
                table_toolbar: 'tableprops tabledelete | tableinsertrowbefore tableinsertrowafter tabledeleterow | tableinsertcolbefore tableinsertcolafter tabledeletecol',
                contextmenu: 'link image table',
                block_formats: 'Paragraph=p; Heading 2=h2; Heading 3=h3; Heading 4=h4; Preformatted=pre',
                style_formats: [
                    {
                        title: 'Badges & Pricing',
                        items: [
                            { title: 'Discount Badge (Save ৳ Green)', inline: 'span', classes: 'price-badge' },
                            { title: 'Current Price (৳ Blue)', inline: 'span', classes: 'price-current' },
                            { title: 'Original Price (Strikethrough)', inline: 'span', classes: 'price-original' }
                        ]
                    },
                    {
                        title: 'Action Buttons',
                        items: [
                            { title: 'Order Button (Primary Blue)', selector: 'a', classes: 'guide-product-btn' }
                        ]
                    }
                ],
                formats: {
                    underline: { inline: 'u' },
                    strikethrough: { inline: 's' }
                },
                content_css: '/css/output.css',
                content_style: `
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                        font-size: 15px;
                        line-height: 1.7;
                        color: #334155;
                        padding: 20px 24px;
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

                    /* Product Cards in Guide/Product Rich Text - Horizontal Compact Layout */
                    .guide-product-card,
                    div[class*="rounded-2xl"][class*="border"],
                    div[class*="rounded-xl"][class*="border"],
                    div[class*="shadow"][class*="border"],
                    div[class*="sm:flex-row"] {
                        display: flex !important;
                        flex-direction: row !important;
                        align-items: center !important;
                        gap: 20px !important;
                        border: 1px solid #e2e8f0 !important;
                        border-radius: 16px !important;
                        background-color: #ffffff !important;
                        padding: 16px 20px !important;
                        margin: 20px 0 !important;
                        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05) !important;
                        box-sizing: border-box !important;
                        clear: both !important;
                    }

                    /* Card image - FORCE COMPACT FIXED SIZE so it NEVER expands or overflows */
                    .guide-product-card img,
                    .guide-product-img,
                    img[class*="size-"],
                    img[class*="size-36"],
                    img[class*="size-32"],
                    div[class*="rounded-2xl"][class*="border"] img,
                    div[class*="rounded-xl"][class*="border"] img,
                    div[class*="shadow"][class*="border"] img,
                    div[class*="sm:flex-row"] img {
                        width: 120px !important;
                        height: 120px !important;
                        min-width: 120px !important;
                        min-height: 120px !important;
                        max-width: 120px !important;
                        max-height: 120px !important;
                        object-fit: cover !important;
                        border-radius: 12px !important;
                        background-color: #f8fafc !important;
                        border: 1px solid #f1f5f9 !important;
                        flex-shrink: 0 !important;
                        display: block !important;
                        margin: 0 !important;
                        padding: 2px !important;
                    }

                    /* Card details column */
                    .guide-product-card > div,
                    .guide-product-info,
                    div[class*="rounded-2xl"][class*="border"] > div,
                    div[class*="shadow"][class*="border"] > div,
                    div[class*="sm:flex-row"] > div {
                        flex: 1 1 auto !important;
                        min-width: 0 !important;
                    }

                    /* Card Title */
                    .guide-product-card h3,
                    .guide-product-title,
                    div[class*="rounded-2xl"][class*="border"] h3 {
                        font-size: 16px !important;
                        font-weight: 700 !important;
                        color: #0f172a !important;
                        margin: 0 0 4px 0 !important;
                        line-height: 1.3 !important;
                    }

                    /* Card Specs */
                    .guide-product-card p,
                    .guide-product-specs,
                    div[class*="rounded-2xl"][class*="border"] p {
                        font-size: 12.5px !important;
                        color: #64748b !important;
                        margin: 0 0 6px 0 !important;
                        line-height: 1.4 !important;
                    }

                    /* Selling Price (Bold Blue) */
                    .price-current,
                    span[class*="text-blue"],
                    div[class*="rounded-2xl"][class*="border"] span[class*="text-blue"] {
                        font-size: 1.15rem !important;
                        font-weight: 800 !important;
                        color: #2563eb !important;
                        margin-right: 8px !important;
                    }

                    /* Original Struck-Through Price */
                    .price-original,
                    s, strike, del,
                    span[class*="line-through"],
                    div[class*="rounded-2xl"][class*="border"] span[class*="line-through"] {
                        font-size: 0.9rem !important;
                        color: #94a3b8 !important;
                        text-decoration: line-through !important;
                        margin-right: 8px !important;
                    }

                    /* Discount Badge */
                    .price-badge,
                    span[class*="bg-emerald"],
                    div[class*="rounded-2xl"][class*="border"] span[class*="bg-emerald"] {
                        display: inline-block !important;
                        font-size: 11px !important;
                        font-weight: 700 !important;
                        color: #047857 !important;
                        background-color: #ecfdf5 !important;
                        border: 1px solid #a7f3d0 !important;
                        padding: 2px 8px !important;
                        border-radius: 9999px !important;
                    }

                    /* Button Link inside Card - High contrast white text guaranteed */
                    a.guide-product-btn,
                    div[class*="rounded-2xl"][class*="border"] a[class*="bg-blue"],
                    div[class*="border"] a[class*="bg-blue"],
                    a[class*="bg-blue-600"] {
                        display: inline-flex !important;
                        align-items: center !important;
                        gap: 6px !important;
                        background-color: #2563eb !important;
                        color: #ffffff !important;
                        font-size: 12.5px !important;
                        font-weight: 700 !important;
                        padding: 7px 16px !important;
                        border-radius: 10px !important;
                        text-decoration: none !important;
                        margin-top: 6px !important;
                        box-shadow: 0 1px 2px rgba(37,99,235,0.2) !important;
                    }
                    a.guide-product-btn:hover,
                    div[class*="rounded-2xl"][class*="border"] a[class*="bg-blue"]:hover {
                        background-color: #1d4ed8 !important;
                        color: #ffffff !important;
                    }

                    /* Callout Box */
                    .callout-box,
                    div[class*="bg-slate-50"][class*="border"] {
                        background-color: #f8fafc !important;
                        border: 1px solid #e2e8f0 !important;
                        border-radius: 16px !important;
                        padding: 18px 22px !important;
                        margin: 20px 0 !important;
                        display: block !important;
                    }
                    div[class*="bg-blue-50"][class*="border"] {
                        background-color: #eff6ff !important;
                        border: 1px solid #bfdbfe !important;
                        border-radius: 16px !important;
                        padding: 18px 22px !important;
                        margin: 20px 0 !important;
                        display: block !important;
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
                    if (options.specifications || options.Specifications) {
                        instance.ui.registry.addButton('techspecs', {
                            text: 'Tech Specs Table',
                            onAction: () => instance.insertContent('<table><tbody><tr><th>Specification</th><th>Details</th></tr><tr><td>Model</td><td></td></tr><tr><td>Warranty</td><td></td></tr></tbody></table>')
                        });
                    }

                    // Interactive Product Card Dialog Button
                    instance.ui.registry.addButton('productcard', {
                        text: 'Product Card',
                        icon: 'embed',
                        tooltip: 'Insert a featured product recommendation card',
                        onAction: () => {
                            instance.windowManager.open({
                                title: 'Insert Product Recommendation Card',
                                size: 'medium',
                                body: {
                                    type: 'panel',
                                    items: [
                                        {
                                            type: 'input',
                                            name: 'title',
                                            label: 'Product Title',
                                            placeholder: 'e.g. SoundPEATS C30 ANC TWS Earbuds'
                                        },
                                        {
                                            type: 'input',
                                            name: 'imageUrl',
                                            label: 'Product Image URL',
                                            placeholder: 'https://media.jasinternational.com.bd/... (or paste image URL)'
                                        },
                                        {
                                            type: 'input',
                                            name: 'specs',
                                            label: 'Key Highlights / Specs (bullets separated by •)',
                                            placeholder: 'Hi-Res Audio LDAC • 12mm Driver • 52dB Hybrid ANC • 40H Battery'
                                        },
                                        {
                                            type: 'input',
                                            name: 'sellingPrice',
                                            label: 'Selling Price (Current)',
                                            placeholder: '৳2,650'
                                        },
                                        {
                                            type: 'input',
                                            name: 'originalPrice',
                                            label: 'Original Price (Will be struck through)',
                                            placeholder: '৳3,100 (optional)'
                                        },
                                        {
                                            type: 'input',
                                            name: 'savings',
                                            label: 'Discount / Savings Badge (Optional, e.g. Save ৳450)',
                                            placeholder: 'Save ৳450 (or leave empty to auto-calculate)'
                                        },
                                        {
                                            type: 'input',
                                            name: 'productLink',
                                            label: 'Storefront Product URL',
                                            placeholder: '/product/soundpeats-c30'
                                        }
                                    ]
                                },
                                buttons: [
                                    {
                                        type: 'cancel',
                                        text: 'Cancel'
                                    },
                                    {
                                        type: 'submit',
                                        text: 'Insert Card',
                                        primary: true
                                    }
                                ],
                                onSubmit: (api) => {
                                    const data = api.getData();
                                    const title = data.title?.trim() || 'Product Name';
                                    const img = data.imageUrl?.trim() || 'https://media.jasinternational.com.bd/images/static/06be0c161d334131b1c7e0d9848b117d.webp';
                                    const specs = data.specs?.trim() || '';
                                    const sellPrice = data.sellingPrice?.trim() || '৳0';
                                    const origPrice = data.originalPrice?.trim() || '';
                                    const link = data.productLink?.trim() || '#';

                                    let savingsText = data.savings?.trim() || '';
                                    if (!savingsText && origPrice && sellPrice) {
                                        const origNum = parseInt(origPrice.replace(/[^0-9]/g, ''), 10);
                                        const sellNum = parseInt(sellPrice.replace(/[^0-9]/g, ''), 10);
                                        if (!isNaN(origNum) && !isNaN(sellNum) && origNum > sellNum) {
                                            savingsText = `Save ৳${(origNum - sellNum).toLocaleString()}`;
                                        }
                                    }

                                    let priceHtml = `<span class="price-current text-lg font-black text-blue-600">${sellPrice}</span>`;
                                    if (origPrice) {
                                        priceHtml += `&nbsp;<span class="price-original text-xs text-slate-400 line-through">${origPrice}</span>`;
                                    }
                                    if (savingsText) {
                                        priceHtml += `&nbsp;<span class="price-badge rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-bold text-emerald-700 border border-emerald-200">${savingsText}</span>`;
                                    }

                                    const cardHtml = `
<div class="guide-product-card my-6 rounded-2xl border border-slate-200 bg-white p-4 sm:p-6 shadow-sm flex flex-col sm:flex-row items-center gap-6">
  <img class="guide-product-img size-36 rounded-2xl object-cover bg-slate-50 border border-slate-100 shrink-0" src="${img}" alt="${title}" loading="lazy" />
  <div class="guide-product-info space-y-2 text-sm flex-1">
    <h3 class="guide-product-title text-base font-bold text-slate-900 m-0">${title}</h3>
    ${specs ? `<p class="guide-product-specs text-xs text-slate-500 m-0">${specs}</p>` : ''}
    <div class="guide-product-prices flex items-center gap-3 pt-1">
      ${priceHtml}
    </div>
    <div class="pt-2">
      <a class="guide-product-btn inline-flex items-center gap-1.5 rounded-xl bg-blue-600 px-4 py-2 text-xs font-bold text-white hover:bg-blue-700 transition" href="${link}">
        View Product &amp; Order Online &rarr;
      </a>
    </div>
  </div>
</div><p></p>`;

                                    instance.insertContent(cardHtml);
                                    api.close();
                                }
                            });
                        }
                    });

                    // Callout Box Button
                    instance.ui.registry.addButton('calloutbox', {
                        text: 'Callout Box',
                        icon: 'info',
                        tooltip: 'Insert a highlighted recommendation or note box',
                        onAction: () => {
                            instance.insertContent(`
<div class="callout-box my-6 rounded-2xl bg-slate-50 border border-slate-200 p-6 space-y-2">
  <h4 class="text-base font-bold text-slate-900 m-0">💡 Editorial Recommendation:</h4>
  <p class="text-sm text-slate-600 m-0">Write your highlighted tip, pros/cons, or buying advice here...</p>
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

            if (disposed) {
                const ed = window.tinymce?.get(targetId);
                if (ed) try { ed.remove(); } catch (e) {}
                for (const item of (editors || [])) item.remove();
                return;
            }
            editor = (editors && editors.length > 0) ? editors[0] : (window.tinymce?.get(targetId) || null);
            if (!editor && window.tinymce?.activeEditor) {
                editor = window.tinymce.activeEditor;
            }
            observer.disconnect();
            sync();
            ready = true;
        } catch (err) {
            console.error('[AdminRichText] Editor initialization error:', err);
            if (!disposed) receiver.invokeMethodAsync('Unavailable').catch(() => {});
        } finally {
            starting = false;
        }
    }

    const observer = new ResizeObserver(start);

    function dispose() {
        if (disposed) return;
        disposed = true;
        observer.disconnect();
        if (editor) {
            try { editor.remove(); } catch (e) {}
            editor = null;
        } else if (element.id && window.tinymce?.get(element.id)) {
            try { window.tinymce.get(element.id).remove(); } catch (e) {}
        }
    }

    observer.observe(element);
    start();

    return {
        update(value, readOnly) { if (disposed) return; html = value ?? ''; disabled = readOnly; sync(); },
        dispose
    };
}

// Listen to Blazor enhanced load events to purge stale, detached editors
if (typeof window !== 'undefined' && window.Blazor) {
    try {
        window.Blazor.addEventListener('enhancedload', () => {
            if (window.tinymce) {
                for (const ed of [...(window.tinymce.editors || [])]) {
                    if (!ed.getElement()?.isConnected) {
                        try { ed.remove(); } catch (e) {}
                    }
                }
            }
        });
    } catch (e) {}
}
