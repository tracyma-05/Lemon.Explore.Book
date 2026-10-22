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
| P2 | Lesson engine, object cards, click targets, narration player; port the **Seasons** chapter as the reference | done |
| P3 | Narration pipeline (see below) | can run alongside P2 |
| P4 | Port the other 33 chapters (`tools/porting` does the mechanical part), a few per change, comparing screenshots against the original | 12 of 34 in the book; the Sky volume is complete (Solar System, Eclipses, Seasons, Moon phases, Day and night, Tides, Kepler, Star trails, Retrograde, Star life, Black hole, Expansion) |
| P5 | Releases: Windows package, signed and notarised macOS app, web edition on static hosting | |

### Porting notes (P2/P4)

| WPF | Avalonia |
|---|---|
| `OnRender(DrawingContext)` | `Render(DrawingContext)` |
| `FormattedText`, `StreamGeometry` | same names, small API differences |
| `CompositionTarget.Rendering`, `DispatcherTimer` | `TopLevel.RequestAnimationFrame` |
| `DependencyProperty` | `StyledProperty` / `DirectProperty` |
| `MediaPlayer` | `INarrationAudio` (desktop audio library, Web Audio in the browser); none yet, so clips run as timed subtitles |
| MaterialDesign `PackIcon` | `PathIcon` with the project's own geometries (`Styles/Explore.axaml`) |
| `dc.PushOpacity(...)` … `dc.Pop()` | `using var dc = new PaintContext(context);` keeps Push/Pop as is (or `using (dc.PushOpacity(...)) { … }`) |
| `Point - Point` is a `Vector` | it is a `Point`: write `Vector v = a - b;` |
| `Rect.Inflate` changes the rect | it returns a new one |
| `StreamGeometryContext.BeginFigure(p, filled, closed)` | `BeginFigure(p, filled)` … `EndFigure(closed)` |
| `WriteableBitmap.WritePixels` | `SpaceDrawing.WritePixels` (locks the bitmap and copies rows) |
| `OnPropertyChanged("Item[]")` for an indexer | `OnPropertyChanged("Item")` (Avalonia listens for the indexer's name) |
| `DrawingVisual` + `RenderTargetBitmap.Render` | `SpaceDrawing.RenderToBitmap(width, height, scale, dc => …)` |
| `DrawImage(bitmap, rect)` of a bitmap at the screen's DPI | `SpaceDrawing.DrawWhole`: the source rectangle is in pixels, a bitmap's `Size` in DIPs |

### Porting a chapter (the Seasons pattern)

1. Drawing code and models go to `Chapters/<Topic>/`. Shared space drawing (sphere renderer, textures, stars,
   labels) is in `Controls/Space`; add bodies to `SpaceBodies` and surfaces to `PlanetSurfaces` as needed.
2. The chapter view model is ported almost as is; one instance serves both pages of the spread.
3. Add `<Topic>TextPageViewModel` / `<Topic>DiagramPageViewModel` (subclasses of the placeholder pages), their views,
   a case in `Contents/ChapterRegistry.cs`, and data templates at the top of `MainView.axaml`.
4. Left page: the text, controls and side figures (on dark plates). Right page: the diagram plate with the
   walkthrough panel and playback toolbar.
5. Copy the chapter's strings into both resx files, then add tests like `SeasonsChapterTests` (targets have words,
   walkthrough keys exist, the spread renders and responds).
6. The chapter view model implements `IChapterContent` (stop its walkthrough when the last of its views leaves the
   screen); page views derive from `ChapterPageView`. `ChapterTestKit` has the shared test helpers.
7. A chapter that depends on where you are takes `BookServices.Location` and exposes it for a `LocationChip` on its
   left page. A link to another chapter turns to `BookContents.ChapterPageOf(slug)`.
8. The original puts its controls in a side panel on the right; the book puts them on the left page, and the title
   there too (bind the diagram's `TitleHeight` to nothing, its `ToolbarHeight` to the toolbar's `Bounds.Height`).
   `tools/porting` flags strings that mention the old layout ("the panel on the right"): reword them for the left
   page, in both languages.
9. `OscillationStat` and `OscillationChoice` (a row of the "what is happening" card, one of a row of chips) are
   shared by most chapters and live in `Explore/Lessons` under those names.

### Simplifications so far

- The shared observer location (`Explore/Places`, saved in the reader's settings, chosen from `LocationChip`) is
  followed by Day and night, Tides, Star trails and Retrograde. Seasons and Moon phases still watch from Beijing (a
  preset), and "the other hemisphere" from Sydney; moving them onto the shared location is a follow-up.
- Pages are narrower than the original window, so a few wide diagrams crop at the edge (the eclipse seasons view,
  the inset panels of the star trails year view).

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
