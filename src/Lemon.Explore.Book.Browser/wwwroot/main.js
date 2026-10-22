import { dotnet } from './_framework/dotnet.js'

if (typeof window === 'undefined') {
    throw new Error('Expected to be running in a browser');
}

// Installable and offline: the service worker caches the app shell (see sw.js). Registration failing, e.g. on
// plain http other than localhost, only costs offline use; the book itself still opens.
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').catch(error => console.warn('Service worker not registered', error));
}

const runtime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = runtime.getConfig();
await runtime.runMain(config.mainAssemblyName, [globalThis.location.href]);
