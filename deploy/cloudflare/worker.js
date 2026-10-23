// Serves the web edition on Cloudflare. Static assets hold everything except what they cannot: the AOT-compiled
// runtime (over the 25 MiB per-file limit) and the narration clips, which live in R2 under the same paths, so the
// app, its service worker and the .NET boot manifest see a single origin.
const FROM_R2 = [/^\/_framework\/dotnet\.native\.[a-z0-9]+\.wasm$/, /^\/narration\//];

export default {
    async fetch(request, env) {
        const url = new URL(request.url);
        if (!FROM_R2.some(pattern => pattern.test(url.pathname))) {
            return env.ASSETS.fetch(request);
        }
        if (request.method !== 'GET' && request.method !== 'HEAD') {
            return new Response(null, { status: 405, headers: { allow: 'GET, HEAD' } });
        }
        return serveFromR2(request, env.BOOK, url.pathname.slice(1));
    },
};

async function serveFromR2(request, bucket, key) {
    const ranged = request.headers.has('range');

    // The runtime is stored with a Brotli copy next to it (about 9 MB instead of 45 MB); audio is not.
    const wantsBrotli = key.endsWith('.wasm') && !ranged
        && /\bbr\b/.test(request.headers.get('accept-encoding') ?? '');
    let object = wantsBrotli ? await bucket.get(`${key}.br`, { onlyIf: request.headers }) : null;
    const encoded = object !== null;
    if (!encoded) {
        object = await bucket.get(key, { range: request.headers, onlyIf: request.headers });
    }
    if (object === null) {
        return new Response('Not found', { status: 404 });
    }

    const headers = new Headers();
    object.writeHttpMetadata(headers); // Content-Type is set when the object is uploaded.
    headers.set('etag', object.httpEtag);
    headers.set('accept-ranges', 'bytes');
    headers.set('vary', 'accept-encoding');
    // Every key carries a content hash, so its bytes never change.
    headers.set('cache-control', 'public, max-age=31536000, immutable');

    // A failed If-None-Match / If-Modified-Since returns the metadata without a body.
    if (!('body' in object)) {
        return new Response(null, { status: 304, headers });
    }

    let status = 200;
    if (ranged && object.range && !encoded) {
        const offset = object.range.offset ?? object.size - object.range.suffix;
        const length = object.range.length ?? object.size - offset;
        headers.set('content-range', `bytes ${offset}-${offset + length - 1}/${object.size}`);
        status = 206;
    }
    if (encoded) {
        headers.set('content-encoding', 'br');
    }

    return new Response(request.method === 'HEAD' ? null : object.body, {
        status,
        headers,
        // The Brotli copy is already compressed; keep the runtime from encoding it again.
        encodeBody: encoded ? 'manual' : 'automatic',
    });
}
