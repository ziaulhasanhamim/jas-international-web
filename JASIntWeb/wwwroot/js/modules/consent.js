export function initConsent() {
    const banner = document.getElementById('jas-cookie-consent');
    if (!banner) return;
    const preference = document.cookie.split('; ').find(x => x.startsWith('jas_cookie_consent='));
    banner.classList.toggle('hidden', !!preference);
    for (const [id, choice] of [['jas-consent-accept', 'accepted'], ['jas-consent-decline', 'essential']]) {
        const button = document.getElementById(id);
        if (!button || button.dataset.bound) continue;
        button.dataset.bound = 'true';
        button.addEventListener('click', async () => {
            document.cookie = `jas_cookie_consent=${choice}; max-age=31536000; path=/; SameSite=Lax${location.protocol === 'https:' ? '; Secure' : ''}`;
            banner.classList.add('hidden');
            const meta = await import('./meta-tracking.js');
            if (choice === 'accepted') {
                meta.initMetaTracking();
                meta.trackPageView();
            } else {
                meta.revokeMetaConsent();
            }
        });
    }
}
