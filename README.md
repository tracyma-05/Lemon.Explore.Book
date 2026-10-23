# The Explore Book · 探索之书

An interactive science book for children, in English and Chinese: a cover, a table of contents, and 34
chapters on the sky, our Earth, physics and chemistry. Each chapter is one spread, with text on the left and an
interactive picture on the right. Pages turn like paper, and every lesson can be read aloud.

一本给孩子的互动科学书，中英双语：有封面、有目录，34 章覆盖天空、地球、物理和化学。每章占一个跨页，左页读、右页玩。
可以像纸书一样翻页，每一课都能朗读。

It runs on **Windows** and **macOS**, and in the browser as an installable **PWA**, all from one Avalonia code base.

> Status: the book shell (cover, contents, page turning, language switch) is in place. The chapters are being
> moved in one by one, and narration comes after that. See [docs/ROADMAP.md](docs/ROADMAP.md).

## Build and run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Lemon.Explore.Book.Desktop
```

```bash
dotnet test
```

The web edition also needs the WebAssembly build tools (once per machine):

```bash
dotnet workload install wasm-tools
```

```bash
dotnet run --project src/Lemon.Explore.Book.Browser
```

That Debug build runs interpreted and is only good for checking layout: the animated diagrams stutter. To judge how
the book feels, run the Release build, which is AOT-compiled to WebAssembly (the first build takes several minutes):

```bash
dotnet run --project src/Lemon.Explore.Book.Browser -c Release
```

### Native AOT

The book builds as a native AOT app (no .NET runtime needed, fast start). The shared library is marked
`IsAotCompatible`, so any change that would break AOT or trimming shows up as a build warning.
CI publishes the Windows (win-x64) and macOS (osx-arm64) AOT builds with warnings as errors, checks that each one
starts and keeps running, and keeps them as build artifacts.

```bash
dotnet publish src/Lemon.Explore.Book.Desktop -c Release -r win-x64 -p:PublishAot=true
```

Use `osx-arm64` / `osx-x64` on a Mac. On Windows this needs the Visual Studio C++ build tools (the linker). The web
edition's Release build is always AOT-compiled to WebAssembly (it needs the `wasm-tools` workload); it runs far
faster than the interpreter but downloads more.

## Reading

- Turn a page: drag a page's outer edge, click it, or press ← / →.
- **Contents** in the toolbar goes back to the table of contents.
- **Bookmarks** marks the page you are reading (or press Ctrl+D) and lists your bookmarks; a bookmarked page has a
  ribbon at the top. The book also reopens at the page you left.
- The table of contents shows your progress: chapters you have opened, and how many of a chapter's walkthroughs you
  have heard to the end.
- **Settings** (top right) has everything you can set: the language, light or dark paper and an accent colour, your
  observer location, narration (voice, volume, subtitles), page-turn animation, and clearing your reading progress.
  Changes apply at once and are saved on the device.
- The text on a chapter's left page can be selected and copied. Right-click it for **Copy**, **Select all** and
  **Add to notes**.
- **Notes**: write a note on the page you are reading, quote text from it, edit or delete notes, and **Export** them
  all as a Markdown file (or **Copy all**). Notes are saved on the device.
- On the desktop the toolbar is the window's title bar: drag it to move the window, double-click it to maximize.
  F11 reads full screen, and the window reopens where it was, at its size.

## Updates and releases

The desktop book checks for updates when it opens and installs them in place (**Update and restart**). By default a
release updates from the GitHub Releases of the repository that built it. The address can be replaced: at build
time, with `update-feed.txt` next to the program, or in Settings → Updates. It can point at any server that serves a
small JSON manifest. See [docs/UPDATES.md](docs/UPDATES.md), which also describes publishing a release (push a `v*`
tag; `.github/workflows/release.yml` builds and publishes everything).

`scripts/package-desktop.sh <rid> [update-address]` builds one desktop package locally, the same way the release does.

## Hosting the web edition

The web edition is static files, so any static host works. `deploy/cloudflare` hosts it on Cloudflare as one Worker on
one origin: static assets serve the app, and an R2 bucket serves what static assets cannot hold. That is the
AOT-compiled runtime (`dotnet.native.*.wasm`, about 43 MiB, over the 25 MiB per-file limit), stored with a Brotli
copy, and later the narration clips under `narration/`.

```bash
deploy/cloudflare/deploy.sh
```

It publishes the Release build, uploads the runtime to R2, then deploys the Worker. It needs Node.js, an existing
bucket (`npx wrangler r2 bucket create explore-book-apac --location apac`), and `CLOUDFLARE_ACCOUNT_ID` /
`CLOUDFLARE_API_TOKEN` in the environment. Neither is stored in the repository.

## Project layout

| Path | What |
|---|---|
| `src/Lemon.Explore.Book` | Everything shared: the book control (`Controls/BookView`), pages, localization, settings |
| `src/Lemon.Explore.Book.Desktop` | Windows / macOS head |
| `src/Lemon.Explore.Book.Browser` | WebAssembly head with PWA manifest and service worker (`wwwroot`) |
| `tests/Lemon.Explore.Book.Tests` | Unit and headless UI tests |
| `scripts/` | Repository checks (and, later, the narration pipeline) |
| `deploy/cloudflare` | Cloudflare Worker, R2 routing and deploy script for the web edition |

## Contributing

- Every user-visible string goes in both `Resources/AppStrings.resx` and `AppStrings.zh-CN.resx`;
  `LocalizationCoverageTests` enforces it.
- This repository is public. Never commit credentials, private host names or personal data. Enable the
  pre-commit check once with `git config core.hooksPath .githooks`; CI runs it again, with gitleaks.

## Licence

MIT, see [LICENSE](LICENSE). Bundled fonts and data keep their own licences, see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
