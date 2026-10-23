import { dotnet } from './_framework/dotnet.js'

if (typeof window === 'undefined') {
    throw new Error('Expected to be running in a browser');
}

// Installable and offline: the service worker caches the app shell (see sw.js). Registration failing, e.g. on
// plain http other than localhost, only costs offline use; the book itself still opens.
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').catch(error => console.warn('Service worker not registered', error));
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
