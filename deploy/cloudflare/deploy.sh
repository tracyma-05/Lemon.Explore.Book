#!/usr/bin/env bash
# Publishes the web edition to Cloudflare: a Worker with static assets, plus an R2 bucket for the files that are too
# large for static assets (the AOT-compiled runtime). Narration clips go to the same bucket under narration/.
#
#   deploy/cloudflare/deploy.sh
#
# Needs Node.js (for wrangler) and, in the environment, CLOUDFLARE_ACCOUNT_ID and CLOUDFLARE_API_TOKEN (a token
# allowed to edit Workers and R2). The R2 bucket must exist:
# `npx wrangler r2 bucket create explore-book-apac --location apac`.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

bucket=explore-book-apac # Must match r2_buckets in wrangler.jsonc.
wrangler=(npx --yes wrangler@4)
out=publish/web/wwwroot

rm -rf publish/web
dotnet publish src/Lemon.Explore.Book.Browser -c Release -o publish/web
cp deploy/cloudflare/.assetsignore "$out/"

# Anything else over the limit would fail the deploy halfway; stop before uploading.
too_large=$(find "$out" -type f -size +25M ! -name '*.br' ! -name '*.gz' ! -name 'dotnet.native.*.wasm')
if [[ -n "$too_large" ]]; then
    echo "Over Cloudflare's 25 MiB per-file limit and not routed to R2:" >&2
    echo "$too_large" >&2
    exit 1
fi

# Upload to R2 before deploying, so the new boot manifest never points at a file that is not there yet.
# application/wasm is required for the Brotli copy too: browsers only stream-compile that type.
for file in "$out"/_framework/dotnet.native.*.wasm; do
    key="_framework/$(basename "$file")"
    "${wrangler[@]}" r2 object put "$bucket/$key" --file "$file" --content-type application/wasm --remote
    "${wrangler[@]}" r2 object put "$bucket/$key.br" --file "$file.br" --content-type application/wasm --remote
done

"${wrangler[@]}" deploy --config deploy/cloudflare/wrangler.jsonc
