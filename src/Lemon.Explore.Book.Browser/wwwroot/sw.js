// Service worker for the installable web edition.
//
// - The page shell (index.html, main.js, css, manifest, icons) is network-first, so a new release shows up on
//   the next visit, with the cached copy as the offline fallback.
// - Everything under _framework/ is fingerprinted by the .NET build, so it is cache-first: a file name never
//   changes content.
// - Narration clips (narration/) carry their text's hash in the file name, so they are cache-first too: each clip
//   is kept once it has been played. Saving a whole chapter for offline use ahead of time is still to come.

const SHELL_CACHE = 'explore-book-shell-v1';
const FRAMEWORK_CACHE = 'explore-book-framework-v1';
const NARRATION_CACHE = 'explore-book-narration-v1';
const SHELL = ['./', './index.html', './main.js', './narration.js', './synth.js', './app.css', './manifest.webmanifest', './icons/icon-192.png'];

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
    if (path.includes('/_framework/')) {
        event.respondWith(cacheFirst(request, FRAMEWORK_CACHE));
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
