#!/usr/bin/env bash
# Publishes the web edition to Cloudflare: a Worker with static assets, plus an R2 bucket for the files that are too
# large for static assets (the AOT-compiled runtime). Narration clips are static assets under narration/, copied in
# from assets/narration/audio (render them first: scripts/narration/generate-narration.py).
#
#   deploy/cloudflare/deploy.sh               # publish, then deploy
#   deploy/cloudflare/deploy.sh --skip-build  # deploy the existing publish/web (built earlier, same commit)
#   deploy/cloudflare/deploy.sh --dry-run     # build and check, upload nothing; list the runtimes it would delete
#
# After deploying, it deletes the runtimes in R2 that no deploy needs any more (stale-runtimes.mjs says which).
#
# Needs Node.js (for wrangler) and, in the environment, CLOUDFLARE_ACCOUNT_ID and CLOUDFLARE_API_TOKEN (a token
# allowed to edit Workers and R2). The R2 bucket must exist:
# `npx wrangler r2 bucket create explore-book-apac --location apac`.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

bucket=explore-book-apac # Must match r2_buckets in wrangler.jsonc.
wrangler=(npx --yes wrangler@4)
out=publish/web/wwwroot

skip_build=false
dry_run=false
for arg in "$@"; do
    case "$arg" in
        --skip-build) skip_build=true ;;
        --dry-run) dry_run=true ;;
        *) echo "Unknown option: $arg" >&2; exit 2 ;;
    esac
done

if $skip_build; then
    [[ -f "$out/index.html" ]] || { echo "No build in $out; run without --skip-build." >&2; exit 1; }
else
    rm -rf publish/web
    dotnet publish src/Lemon.Explore.Book.Browser -c Release -o publish/web
fi
cp deploy/cloudflare/.assetsignore "$out/"

# The clips are not in git. Static assets upload only files Cloudflare has not seen, so a deploy sends just the clips
# whose text changed (their names carry the text's hash).
if [[ -d assets/narration/audio ]]; then
    mkdir -p "$out/narration"
    cp -R assets/narration/audio/. "$out/narration/"
else
    echo "No clips in assets/narration/audio: the web edition's narration will be subtitles only." >&2
fi

# Anything else over the limit would fail the deploy halfway; stop before uploading.
too_large=$(find "$out" -type f -size +25M ! -name '*.br' ! -name '*.gz' ! -name 'dotnet.native.*.wasm')
if [[ -n "$too_large" ]]; then
    echo "Over Cloudflare's 25 MiB per-file limit and not routed to R2:" >&2
    echo "$too_large" >&2
    exit 1
fi

# Upload to R2 before deploying, so the new boot manifest never points at a file that is not there yet.
# application/wasm is required for the Brotli copy too: browsers only stream-compile that type.
runtime_keys=()
for file in "$out"/_framework/dotnet.native.*.wasm; do
    key="_framework/$(basename "$file")"
    runtime_keys+=("$key")
    if $dry_run; then
        echo "Would upload $key and $key.br to R2."
        continue
    fi
    "${wrangler[@]}" r2 object put "$bucket/$key" --file "$file" --content-type application/wasm --remote
    "${wrangler[@]}" r2 object put "$bucket/$key.br" --file "$file.br" --content-type application/wasm --remote
done

if $dry_run; then
    "${wrangler[@]}" deploy --config deploy/cloudflare/wrangler.jsonc --dry-run
else
    "${wrangler[@]}" deploy --config deploy/cloudflare/wrangler.jsonc
fi

# Only once the new version is live: the runtimes before the previous one. A deploy that failed after its upload
# counts as the previous one, so rerun the failed deploy rather than build another first.
stale=$(node deploy/cloudflare/stale-runtimes.mjs "$bucket" "${runtime_keys[@]}")
for key in $stale; do
    if $dry_run; then
        echo "Would delete $key from R2."
    else
        "${wrangler[@]}" r2 object delete "$bucket/$key" --remote
    fi
done
[[ -n "$stale" ]] || echo "No old runtimes in R2 to delete."
