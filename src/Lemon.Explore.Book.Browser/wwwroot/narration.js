// Plays narration clips with Web Audio for WebNarrationAudio.cs. One clip at a time: it is fetched whole and decoded,
// which avoids Safari's range-request problems with audio from the service worker's cache. Pausing suspends the
// audio context, so its clock (and the clip's position) stops with it.

let context = null;
let gain = null;
let source = null;
let generation = 0; // A newer play() or stop() abandons a clip still loading.
let startedAt = 0;
let state = 'idle'; // idle | loading | playing | ended
let paused = false;

function ensureContext() {
    if (context === null) {
        context = new AudioContext();
        gain = context.createGain();
        gain.connect(context.destination);
    }
    return context;
}

// Browsers start audio only from a user gesture: the first tap or key press unlocks it, and a clip that was
// waiting for it starts then.
function unlock() {
    const audio = ensureContext();
    if (audio.state === 'suspended' && !paused) {
        audio.resume().catch(() => {});
    }
}
for (const type of ['pointerdown', 'keydown', 'touchend']) {
    globalThis.addEventListener(type, unlock, { capture: true, passive: true });
}

export function play(url, volume, onFailed) {
    stop();
    const mine = ++generation;
    const audio = ensureContext();
    gain.gain.value = volume;
    state = 'loading';
    unlock();

    fetch(new URL(url, document.baseURI))
        .then(response => {
            if (!response.ok) {
                throw new Error(`${response.status} ${url}`);
            }
            return response.arrayBuffer();
        })
        .then(data => audio.decodeAudioData(data))
        .then(buffer => {
            if (mine !== generation) {
                return;
            }
            source = audio.createBufferSource();
            source.buffer = buffer;
            source.connect(gain);
            source.onended = () => {
                if (mine === generation) {
                    state = 'ended';
                }
            };
            startedAt = audio.currentTime;
            source.start();
            state = 'playing';
        })
        .catch(error => {
            if (mine !== generation) {
                return;
            }
            console.warn('Narration clip not played', error);
            state = 'ended';
            onFailed();
        });
}

export function pause() {
    paused = true;
    context?.suspend().catch(() => {});
}

export function resume() {
    paused = false;
    context?.resume().catch(() => {});
}

export function stop() {
    generation++;
    if (source !== null) {
        source.onended = null;
        try {
            source.stop();
        } catch {
            // Never started.
        }
        source.disconnect();
        source = null;
    }
    state = 'idle';
    if (paused) {
        resume();
    }
}

export function setVolume(volume) {
    if (gain !== null) {
        gain.gain.value = volume;
    }
}

export function position() {
    return state === 'playing' ? Math.max(0, context.currentTime - startedAt) : 0;
}

export function isEnded() {
    return state === 'idle' || state === 'ended';
}
