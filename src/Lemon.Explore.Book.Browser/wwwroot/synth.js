// Plays the Sound chapter's synthesizer for WebSynthOutput.cs. The browser runs .NET on its one thread, so there is
// no audio thread to pull samples: .NET renders short chunks ahead of time and each is scheduled to start exactly
// where the previous one ends.

let context = null;
let output = null;
let next = 0;

function ensureContext() {
    if (context === null) {
        context = new AudioContext();
        output = context.createGain();
        output.connect(context.destination);
    }
    return context;
}

// Browsers start audio only from a user gesture; the chapter's sounds all follow one, and this catches the rest.
function unlock() {
    if (context !== null && context.state === 'suspended') {
        context.resume().catch(() => {});
    }
}
for (const type of ['pointerdown', 'keydown', 'touchend']) {
    globalThis.addEventListener(type, unlock, { capture: true, passive: true });
}

export function open() {
    ensureContext();
    unlock();
    output.gain.value = 1;
}

// Seconds of sound queued and not yet heard.
export function ahead() {
    return context === null ? 0 : Math.max(0, next - context.currentTime);
}

export function push(samples, sampleRate) {
    const audio = ensureContext();
    const buffer = audio.createBuffer(1, samples.length, sampleRate);
    buffer.getChannelData(0).set(samples);
    const source = audio.createBufferSource();
    source.buffer = buffer;
    source.connect(output);

    // After a gap (nothing queued, or the page was busy), start a little ahead rather than in the past.
    const now = audio.currentTime;
    if (next < now + 0.01) {
        next = now + 0.04;
    }
    source.start(next);
    next += buffer.duration;
}

export function close() {
    if (output !== null) {
        // Anything still queued fades out instead of playing on.
        output.gain.setTargetAtTime(0, context.currentTime, 0.02);
        output.disconnect();
        output = context.createGain();
        output.connect(context.destination);
    }
    next = 0;
}
