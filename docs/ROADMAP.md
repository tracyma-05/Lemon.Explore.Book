# Roadmap

The book's 34 chapters come from an earlier Windows-only (WPF) app. There, each topic was a page with an
animated, clickable diagram and a side panel of lessons with narration. This project turns them into a
cross-platform book.

## Decisions

- **One Avalonia code base** for Windows, macOS and the browser. The diagrams are hand-drawn with
  `DrawingContext`, and Avalonia's drawing API is close to WPF's, so they port almost line for line. A web
  rewrite would mean redrawing all 34. The cost: the web edition renders to a canvas, has a larger first
  download, and is weaker for search engines and screen readers.
- **Book layout**: two-page spreads on wide screens, single pages on phones. Each chapter is one spread: text
  and lessons on the left, the interactive diagram on the right.
- **Pages are live** at rest and captured to bitmaps only while a page turns (`Controls/BookView.cs`).

## Phases

| | Phase | State |
|---|---|---|
| P0 | Repository, CI, secret scanning, localization | done |
| P1 | Book shell: cover, contents, page turning, language switch, reading position | done |
| P2 | Lesson engine, object cards, click targets, narration player; port the **Seasons** chapter as the reference | next |
| P3 | Narration pipeline (see below) | can run alongside P2 |
| P4 | Port the other 33 chapters, a few per change, comparing screenshots against the original | |
| P5 | Releases: Windows package, signed and notarised macOS app, web edition on static hosting | |

### Porting notes (P2/P4)

| WPF | Avalonia |
|---|---|
| `OnRender(DrawingContext)` | `Render(DrawingContext)` |
| `FormattedText`, `StreamGeometry` | same names, small API differences |
| `CompositionTarget.Rendering`, `DispatcherTimer` | `TopLevel.RequestAnimationFrame` |
| `DependencyProperty` | `StyledProperty` / `DirectProperty` |
| `MediaPlayer` | `IAudioPlayer` (desktop audio library, `HTMLAudioElement` / Web Audio in the browser) |
| MaterialDesign `PackIcon` | an icon font or path icons (to be chosen) |

## Narration

- **Pre-rendered clips**. Every narrated line is a resource key; a script renders it to audio. A manifest records
  each clip's text hash, duration and sentence timings, so subtitles stay in sync and only changed lines are
  re-rendered.
- **Openly licensed voices only.** The clips are published, so the text-to-speech model and voices must allow
  redistribution. Candidates to compare on a short English and Chinese sample: CosyVoice, MeloTTS, Kokoro.
  Check each model's weight licence, not just its code licence.
- **Not in git.** Clips ship as per-chapter, per-language packs with each release, and are deployed next to the
  web edition.
- **Desktop**: a chapter's pack downloads the first time it is opened (checked with sha256), with an option to
  download everything.
- **Web**: audio is served from the same origin as the app, under hashed, immutable file names. The service worker
  caches each chapter's clips as they are played or saved for offline use, and the app asks for persistent
  storage. Playback decodes clips with Web Audio, which avoids Safari's range-request problems with cached
  audio. The first clip waits for a tap, as browsers require.
- **Fallback**: the system voice (Windows, macOS speech, Web Speech API) when a clip is missing, then silent
  timed subtitles.

## Later

- Subset the web edition's Chinese font to the characters the book uses (the full files are about 8 MB each).
- Settings page: page-turn animation on or off, narration voice and speed.
