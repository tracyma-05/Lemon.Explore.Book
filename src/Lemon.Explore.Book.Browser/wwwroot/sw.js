// Service worker for the installable web edition.
//
// - The page shell (index.html, main.js, css, manifest, icons) is network-first, so a new release shows up on
//   the next visit, with the cached copy as the offline fallback.
// - Files under _framework/ whose names carry the .NET build's fingerprint (Name.abc123xyz0.wasm) are cache-first: such
//   a name never changes content. The ones without it (dotnet.js, which holds the boot manifest naming all the others,
//   avalonia.js, storage.js) change with every release, so they are network-first like the shell. Caching dotnet.js
//   cache-first kept readers on the release they first opened, whatever was deployed after.
// - Narration clips (narration/) carry their text's hash in the file name, so they are cache-first too: each clip
//   is kept once it has been played. Saving a whole chapter for offline use ahead of time is still to come.

// v2: the v1 framework cache held dotnet.js and the other unfingerprinted files for good; activating drops it.
const SHELL_CACHE = 'explore-book-shell-v2';
const FRAMEWORK_CACHE = 'explore-book-framework-v2';
const NARRATION_CACHE = 'explore-book-narration-v1';
const SHELL = ['./', './index.html', './main.js', './narration.js', './synth.js', './app.css', './manifest.webmanifest', './icons/icon-192.png'];

// The .NET build's fingerprint: ten lower-case letters and digits before the extension(s), e.g.
// dotnet.native.aiuw32zoda.wasm or Avalonia.Base.3episxfaie.wasm.
const FINGERPRINTED = /\.[a-z0-9]{10}\.[a-z]+(\.(br|gz))?$/;

self.addEventListener('install', event => {
    event.waitUntil(caches.open(SHELL_CACHE).then(cache => cache.addAll(SHELL)));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    const keep = new Set([SHELL_CACHE, FRAMEWORK_CACHE, NARRATION_CACHE]);
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(key => !keep.has(key)).map(key => caches.delete(key))))
            .then(() => self.clients.claim()));
});

self.addEventListener('fetch', event => {
    const request = event.request;
    if (request.method !== 'GET' || new URL(request.url).origin !== self.location.origin) {
        return;
    }

    const path = new URL(request.url).pathname;
    if (path.includes('/_framework/') && FINGERPRINTED.test(path)) {
        event.respondWith(cacheFirst(request, FRAMEWORK_CACHE));
    } else if (path.includes('/_framework/')) {
        event.respondWith(networkFirst(request, FRAMEWORK_CACHE));
    } else if (path.includes('/narration/')) {
        event.respondWith(cacheFirst(request, NARRATION_CACHE));
    } else {
        event.respondWith(networkFirst(request, SHELL_CACHE));
    }
});

async function cacheFirst(request, cacheName) {
    const cache = await caches.open(cacheName);
    const cached = await cache.match(request);
    if (cached) {
        return cached;
    }

    const response = await fetch(request);
    if (response.ok) {
        cache.put(request, response.clone());
    }
    return response;
}

async function networkFirst(request, cacheName) {
    const cache = await caches.open(cacheName);
    try {
        const response = await fetch(request);
        if (response.ok) {
            cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }
        throw error;
    }
}
