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

## Reading

- Turn a page: drag a page's outer edge, click it, or press ← / →.
- **Contents** in the toolbar goes back to the table of contents.
- **Settings** (top right) has everything you can set: the language, your observer location, narration (voice,
  volume, subtitles) and page-turn animation. Changes apply at once and are saved on the device.

## Project layout

| Path | What |
|---|---|
| `src/Lemon.Explore.Book` | Everything shared: the book control (`Controls/BookView`), pages, localization, settings |
| `src/Lemon.Explore.Book.Desktop` | Windows / macOS head |
| `src/Lemon.Explore.Book.Browser` | WebAssembly head with PWA manifest and service worker (`wwwroot`) |
| `tests/Lemon.Explore.Book.Tests` | Unit and headless UI tests |
| `scripts/` | Repository checks (and, later, the narration pipeline) |

## Contributing

- Every user-visible string goes in both `Resources/AppStrings.resx` and `AppStrings.zh-CN.resx`;
  `LocalizationCoverageTests` enforces it.
- This repository is public. Never commit credentials, private host names or personal data. Enable the
  pre-commit check once with `git config core.hooksPath .githooks`; CI runs it again, with gitleaks.

## Licence

MIT, see [LICENSE](LICENSE). Bundled fonts and data keep their own licences, see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
