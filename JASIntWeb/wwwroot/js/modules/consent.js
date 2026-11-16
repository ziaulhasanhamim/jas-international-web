export function initConsent() {
    const banner = document.getElementById('jas-cookie-consent');
    if (!banner) return;
    const preference = document.cookie.split('; ').find(x => x.startsWith('jas_cookie_consent='));
    banner.classList.toggle('hidden', !!preference);
    for (const [id, choice] of [['jas-consent-accept', 'accepted'], ['jas-consent-decline', 'essential']]) {
        const button = document.getElementById(id);
        if (!button || button.dataset.bound) continue;
        button.dataset.bound = 'true';
        button.addEventListener('click', () => {
            document.cookie = `jas_cookie_consent=${choice}; max-age=31536000; path=/; SameSite=Lax${location.protocol === 'https:' ? '; Secure' : ''}`;
            banner.classList.add('hidden');
            if (choice === 'accepted') { window.JAS?.MetaTracking?.init(); window.JAS?.MetaTracking?.trackPageView(); }
            else {
                window.fbq?.('consent', 'revoke');
                for (const name of ['_fbp', '_fbc', 'jas_attribution', 'jas_first_touch']) document.cookie = `${name}=; max-age=0; path=/; SameSite=Lax`;
                for (const name of ['jas_fbclid', 'jas_attribution']) { try { localStorage.removeItem(name); } catch {} }
            }
        });
    }
}
