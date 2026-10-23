import { dotnet } from './_framework/dotnet.js'

if (typeof window === 'undefined') {
    throw new Error('Expected to be running in a browser');
}

// Installable and offline: the service worker caches the app shell (see sw.js). Registration failing, e.g. on
// plain http other than localhost, only costs offline use; the book itself still opens.
if ('serviceWorker' in navigator) {
    // A new release's service worker takes over at once (skipWaiting in sw.js). If an older one was in charge, this
    // page started from the old files: load it again, once, so the reader gets the new release on this visit rather
    // than the next. Not on the first visit, when there was no worker before.
    const hadWorker = navigator.serviceWorker.controller !== null;
    let reloading = false;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (hadWorker && !reloading) {
            reloading = true;
            location.reload();
        }
    });
    navigator.serviceWorker.register('./sw.js')
        .then(registration => registration.update())
        .catch(error => console.warn('Service worker not registered', error));
}

// By default the runtime downloads resource satellites only for the browser's language at startup, so switching
// language in Settings would find no strings for it and fall back to English. The satellites hold only strings.
const runtime = await dotnet
    .withConfig({ loadAllSatelliteResources: true })
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = runtime.getConfig();
await runtime.runMain(config.mainAssemblyName, [globalThis.location.href]);
