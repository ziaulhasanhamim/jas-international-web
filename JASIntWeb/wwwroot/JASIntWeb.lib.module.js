export function afterWebStarted(blazor) {
    let progress = 0;
    let active = false;
    let tick = null;
    let fade = null;
    let frame = null;

    const indicator = () => document.getElementById("navigation-progress");
    const bar = () => indicator()?.querySelector(".navigation-progress-bar");
    const paint = () => {
        const fill = bar();
        if (fill) fill.style.transform = `scaleX(${progress})`;
    };
    const stopTimers = () => {
        clearInterval(tick);
        clearTimeout(fade);
        cancelAnimationFrame(frame);
    };

    const startProgress = () => {
        stopTimers();
        const track = indicator();
        const fill = bar();
        if (!track || !fill) return;
        if (!active) {
            progress = 0;
            fill.style.transition = "none";
            paint();
        }
        active = true;
        track.hidden = false;
        track.style.opacity = "1";
        frame = requestAnimationFrame(() => {
            fill.style.transition = "";
            progress = Math.max(progress, 0.12);
            paint();
            tick = setInterval(() => {
                progress = Math.min(0.94, progress + (1 - progress) * 0.1);
                paint();
            }, 350);
        });
    };

    const finishProgress = () => {
        stopTimers();
        active = false;
        progress = 1;
        const fill = bar();
        if (fill) fill.style.transition = "";
        paint();
        fade = setTimeout(() => {
            const track = indicator();
            if (track) track.style.opacity = "0";
            fade = setTimeout(() => {
                if (track) track.hidden = true;
            }, 180);
        }, 250);
    };

    blazor.addEventListener("enhancednavigationstart", startProgress);
    blazor.addEventListener("enhancednavigationend", finishProgress);
}
