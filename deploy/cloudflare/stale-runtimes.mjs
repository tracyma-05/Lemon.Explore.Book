// Lists the runtimes in R2 that no deploy needs any more, one key per line, for deploy.sh to delete.
//
//   node deploy/cloudflare/stale-runtimes.mjs <bucket> <current runtime key>...
//
// A runtime is _framework/dotnet.native.<hash>.wasm plus its .br copy. Kept: the ones this deploy serves, and the most
// recently uploaded other one, which the previous deploy served. Readers whose service worker still holds the
// previous boot manifest ask for it, and `wrangler rollback` needs it. Nothing else in the bucket is touched.
// Reads CLOUDFLARE_ACCOUNT_ID and CLOUDFLARE_API_TOKEN from the environment, like wrangler.
import { pathToFileURL } from 'node:url';

const RUNTIME = /^_framework\/dotnet\.native\.[^/]+\.wasm(\.br)?$/;

export function staleRuntimes(objects, currentKeys) {
    const current = new Set(currentKeys.map(runtimeOf));
    const others = new Map(); // Runtime key -> its objects' keys and latest upload.
    for (const { key, last_modified } of objects) {
        if (!RUNTIME.test(key) || current.has(runtimeOf(key))) {
            continue;
        }
        // Without an upload time the previous runtime cannot be told apart, so delete nothing.
        const uploaded = Date.parse(last_modified);
        if (Number.isNaN(uploaded)) {
            throw new Error(`No upload time for ${key}: ${JSON.stringify(last_modified)}`);
        }
        const runtime = others.get(runtimeOf(key)) ?? { keys: [], uploaded: 0 };
        runtime.keys.push(key);
        runtime.uploaded = Math.max(runtime.uploaded, uploaded);
        others.set(runtimeOf(key), runtime);
    }
    return [...others.values()]
        .sort((a, b) => b.uploaded - a.uploaded)
        .slice(1)
        .flatMap(runtime => runtime.keys)
        .sort();
}

function runtimeOf(key) {
    return key.replace(/\.br$/, '');
}

async function listRuntimes(bucket) {
    const { CLOUDFLARE_ACCOUNT_ID: account, CLOUDFLARE_API_TOKEN: token } = process.env;
    if (!account || !token) {
        throw new Error('CLOUDFLARE_ACCOUNT_ID and CLOUDFLARE_API_TOKEN must be set.');
    }
    const objects = [];
    let cursor;
    do {
        const url = new URL(`https://api.cloudflare.com/client/v4/accounts/${account}/r2/buckets/${bucket}/objects`);
        url.searchParams.set('prefix', '_framework/dotnet.native.');
        url.searchParams.set('per_page', '1000');
        if (cursor) {
            url.searchParams.set('cursor', cursor);
        }
        const response = await fetch(url, { headers: { authorization: `Bearer ${token}` } });
        const body = await response.json().catch(() => null);
        if (!response.ok || !body?.success) {
            throw new Error(`Listing ${bucket} failed (${response.status}): ${JSON.stringify(body?.errors ?? body)}`);
        }
        objects.push(...body.result);
        cursor = body.result_info?.is_truncated ? body.result_info.cursor : undefined;
    } while (cursor);
    return objects;
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
    const [bucket, ...currentKeys] = process.argv.slice(2);
    if (!bucket || currentKeys.length === 0) {
        console.error('Usage: stale-runtimes.mjs <bucket> <current runtime key>...');
        process.exit(2);
    }
    try {
        for (const key of staleRuntimes(await listRuntimes(bucket), currentKeys)) {
            console.log(key);
        }
    } catch (error) {
        console.error(error.message);
        process.exit(1);
    }
}
