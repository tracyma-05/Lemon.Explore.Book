# Updates

The desktop book checks for a newer version when it opens (Settings → Updates can turn that off) and shows a banner
when there is one. **Update and restart** downloads the package, checks its SHA-256 when the release publishes one,
unpacks it next to the book, swaps the installation and starts the new version — a few seconds, and a failed swap
always puts the old version back. When the package cannot be installed in place (an installer, a folder the reader
cannot write to), the button downloads it in the browser instead.

The web edition updates through its service worker and has none of this.

## The update address

An address is one of:

| Address | Reads |
|---|---|
| `github:owner/repo` (or `https://github.com/owner/repo`) | that repository's latest GitHub Release (drafts and pre-releases are skipped) |
| any other `https://…` address | a JSON manifest at that address (below) |
| `off` | nothing: updates are off |

Three places can set it; each overrides the one before:

1. **Built into the build** — `dotnet publish … -p:UpdateFeed=<address>`. The release workflow sets
   `github:<the repository it runs in>`, so a fork that publishes releases gets builds that update from the fork.
   Set the repository variable `UPDATE_FEED` to build in another address. Without `-p:UpdateFeed`, a Release build
   (local ones too) updates from this repository's GitHub Releases, `github:tracyma-05/Lemon.Explore.Book`; a Debug
   build has no address. `-p:UpdateFeed=off` builds one that never looks.
2. **`update-feed.txt` next to the program** (inside `Explore Book.app/Contents/MacOS/` on macOS) — for whoever
   repackages a build for their own server without rebuilding it. The first line that is not empty or a `#` comment
   is the address.
3. **The reader's own address** — Settings → Updates → Update address. **Restore the default** goes back to 1 or 2.

## Packages

The book picks the package for its platform by file name: `…-win-x64.zip`, `…-osx-arm64.zip`, `…-osx-x64.zip`
(also `windows`, `macos`, `amd64`, `aarch64` and similar spellings). `scripts/package-desktop.sh <rid>` builds them:

- **Windows**: a zip of the app folder, `ExploreBook.exe` at its root.
- **macOS**: `Explore Book.app` zipped with `ditto -c -k --keepParent`, so links, permissions and the signature survive.

A package that runs on an Apple silicon Mac is preferred there; the Intel build is the fallback (Rosetta 2).

## Your own server: the manifest

Host the packages anywhere over https, next to a manifest such as `latest.json`:

```json
{
  "version": "0.2.0",
  "downloads": {
    "win-x64":   { "url": "explore-book-0.2.0-win-x64.zip", "sha256": "…" },
    "osx-arm64": { "url": "explore-book-0.2.0-osx-arm64.zip", "sha256": "…" },
    "osx-x64":   "https://downloads.example.com/explore-book/explore-book-0.2.0-osx-x64.zip"
  },
  "pageUrl": "https://example.com/explore-book",
  "releaseNotes": "- New chapters …",
  "publishedAt": "2026-10-22T08:00:00Z"
}
```

- `version` is required (`0.2.0`, `v0.2.0`; a `-beta` suffix is ignored), plus at least one of `downloads`,
  `downloadUrl` or `pageUrl`.
- Links may be relative to the manifest. Only http(s) links are followed.
- A `downloads` entry is a link, or `{ "url", "sha256" }`; with a checksum the download is verified before it is
  installed. A top-level `sha256` belongs to `downloadUrl` (or to the only `downloads` entry).
- A computer with no matching package is sent to `pageUrl`.
- Serve the manifest with a short cache time (or `no-cache`): the book asks with `Cache-Control: no-cache`, but a
  CDN may still hold an old copy.

Then point the builds at it: `-p:UpdateFeed=https://downloads.example.com/explore-book/latest.json`, the
`UPDATE_FEED` repository variable, or `update-feed.txt`.

## Publishing a release (GitHub)

1. If narration clips changed, render them and upload them:
   `python -I scripts/narration/narration-pack.py pack --upload` (the `narration-v1` pre-release the build reads).
2. Set `<Version>` in `Directory.Build.props` and commit (`Release 0.2.0`).
3. `git tag v0.2.0 && git push origin main v0.2.0`.

`.github/workflows/release.yml` checks the tag equals the version, runs the tests, builds the three desktop packages
and the web edition, and creates the GitHub Release with them (its notes are the commit subjects since the previous
tag). GitHub records each asset's SHA-256, which the book verifies. With the repository variable `DEPLOY_PAGES` set to
`true` it also deploys the web edition to GitHub Pages.

The macOS packages are only ad-hoc signed: the first start needs right-click → Open (or System Settings → Privacy &
Security → Open Anyway). Updates the book downloads itself are not quarantined and open normally.
