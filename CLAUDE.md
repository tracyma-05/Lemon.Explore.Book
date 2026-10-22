# Lemon.Explore.Book

Cross-platform (Avalonia 12, .NET 10) interactive science book: Windows/macOS desktop and a WebAssembly PWA from
one shared project. Plan and phases: `docs/ROADMAP.md`.

## Public repository: nothing sensitive, ever

- No credentials, tokens, private host names, internal product names, e-mail addresses or personal data in
  code, comments, strings, docs or commit messages. Use obvious placeholders in examples.
- The book needs no back end and no sign-in. Do not add configuration files with URLs or keys (`appsettings*.json`
  is git-ignored on purpose).
- `scripts/check-sensitive.sh` runs as the pre-commit hook (`git config core.hooksPath .githooks`) and in CI,
  together with gitleaks. Keep it passing; never bypass the hook.

## Localization

- Every user-visible string is a key in both `src/Lemon.Explore.Book/Resources/AppStrings.resx` (English) and
  `AppStrings.zh-CN.resx` (Chinese), added in the same change. Name keys `<Area>_<What>`.
- XAML: `{loc:Localize Key}`. C#: `LocalizationService.Instance.GetString` / `.Format`. Text a view model builds
  must be raised again on a culture change (`PageViewModel.OnCultureChanged`).
- `LocalizationCoverageTests` fails on keys missing from either file or used in code but undefined.

## Book structure

- The running order is in `Contents/BookContents.cs`. Every chapter must open on a left page so it fills one
  spread; `BookContentsTests` checks this.
- A page is a `PageViewModel` subclass plus a view in `Views/Pages`, mapped in `MainView.axaml`'s data templates.
- `Controls/BookView.cs` shows and turns pages; `Controls/PageCurlGeometry.cs` is the turn's geometry, unit-tested.

## Before committing

```bash
dotnet test
```

Set `BOOK_SNAPSHOT_DIR` to save the headless tests' rendered frames as PNGs for a visual check.
