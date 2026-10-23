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
| P3 | Narration pipeline (see below) | clips rendered and playing on Windows and the web (macOS player untested); open: voice licence, desktop packs, offline save |
| P4 | Port the other 33 chapters (`tools/porting` does the mechanical part), a few per change, comparing screenshots against the original | 32 of 34 in the book: the Sky volume (Solar System, Eclipses, Seasons, Moon phases, Day and night, Tides, Kepler, Star trails, Retrograde, Star life, Black hole, Expansion); the Earth volume (Water cycle, Sea breeze, Greenhouse, Typhoon, Lightning, Blue sky, Rainbow, Aurora, Earthquakes, Volcanoes, Ice ages); Physics: Oscillations, Waves, Sound, Projectile, Flight, Lenses, Spectra, Heat engines, Power grid |
| P5 | Releases: Windows package, signed and notarised macOS app, web edition on static hosting | |

### Porting notes (P2/P4)

| WPF | Avalonia |
|---|---|
| `OnRender(DrawingContext)` | `Render(DrawingContext)` |
| `FormattedText`, `StreamGeometry` | same names, small API differences |
| `CompositionTarget.Rendering`, `DispatcherTimer` | `TopLevel.RequestAnimationFrame` |
| `DependencyProperty` | `StyledProperty` / `DirectProperty` |
| `MediaPlayer` | `INarrationAudio`: winmm (MCI) on Windows, `AVAudioPlayer` on macOS, Web Audio in the browser |
| The Sound chapter's waveOut stream (`SoundOutput`) | `SoundEngine.OpenOutput`, set by each head: waveOut on Windows, an AudioToolbox queue on macOS (untested), and in the browser short chunks rendered on a timer and scheduled back to back with Web Audio |
| MaterialDesign `PackIcon` | `PathIcon` with the project's own geometries (`Styles/Explore.axaml`) |
| `dc.PushOpacity(...)` … `dc.Pop()` | `using var dc = new PaintContext(context);` keeps Push/Pop as is (or `using (dc.PushOpacity(...)) { … }`) |
| `Point - Point` is a `Vector` | it is a `Point`: write `Vector v = a - b;` |
| `Rect.Inflate` changes the rect | it returns a new one |
| `StreamGeometryContext.BeginFigure(p, filled, closed)` | `BeginFigure(p, filled)` … `EndFigure(closed)` |
| `WriteableBitmap.WritePixels` | `SpaceDrawing.WritePixels` (locks the bitmap and copies rows) |
| `OnPropertyChanged("Item[]")` for an indexer | `OnPropertyChanged("Item")` (Avalonia listens for the indexer's name) |
| `DrawingVisual` + `RenderTargetBitmap.Render` | `SpaceDrawing.RenderPixels(w, h, dpi, dc => …)` (or `RenderToBitmap` in DIPs) |
| `RenderTargetBitmap.CopyPixels(bytes, stride, 0)` into Pbgra32 | `SpaceDrawing.ReadPixels(bitmap)`: premultiplied BGRA bytes whatever the platform's own layout |
| `v.Normalize();`, `r.Offset(…)`, `r.Union(…)`, `r.Inflate(…)` change the struct | they return a new one: `v = v.Normalize();`, `r = r.Translate(…)`, `r = r.Union(…)` |
| `DrawImage(bitmap, rect)` of a bitmap at the screen's DPI | `SpaceDrawing.DrawWhole`: the source rectangle is in pixels, a bitmap's `Size` in DIPs |
| A coarse bitmap (a pixel for a few DIPs) stretched over the view | `SpaceDrawing.DrawSmooth`: Avalonia draws it nearest-neighbour, WPF smooths it |
| `Rect.Empty`, `r.IsEmpty`, `r.Union(point)` | `default(Rect)` (Union ignores it, like Empty); a box round points from their min and max: Union also ignores a rect of no size |
| `Point3D`, `Vector3D` (Media3D) | the book's own in `Controls/Space/Space3D.cs` (Avalonia's `Vector3D` has no cross product), with a `using Vector3D = …` alias |
| `RenderOptions.SetBitmapScalingMode(this, …)` | `RenderOptions.SetBitmapInterpolationMode(this, …)`: Linear is MediumQuality, NearestNeighbor is None |
| `ImageBrush` with `BitmapScalingMode.NearestNeighbor` | make the tile at its drawn size, each pixel repeated: Avalonia brushes have no scaling mode |

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
   there too (bind the diagram's `TitleHeight` to nothing, its `ToolbarHeight` to the toolbar's `Bounds.Height`), so
   the diagram keeps only a margin at the top instead of the original's title band (`tools/porting` rewrites a
   computed `TitleSpace`; a fixed one becomes 34 by hand). A picture limited by the page's width is then centred in
   the stage's height rather than hung from its top.
   `tools/porting` flags strings that mention the old layout ("the panel on the right"): reword them for the left
   page, in both languages.
9. `OscillationStat` and `OscillationChoice` (a row of the "what is happening" card, one of a row of chips) are
   shared by most chapters and live in `Explore/Lessons` under those names.

### Simplifications so far

- The shared observer location (`Explore/Places`, saved in the reader's settings, chosen from `LocationChip`) is
  followed by Day and night, Tides, Star trails, Retrograde, the Sea breeze monsoon, the Greenhouse glass house, the
  Typhoon track, the Lightning storm cloud, Blue sky, the Rainbow sky, the Aurora sky and the Earthquakes map and Earth's inside, the engine's air in Heat engines and the Power grid's sunshine. Seasons and Moon phases still watch from Beijing (a
  preset), and "the other hemisphere" from Sydney; moving them onto the shared location is a follow-up.
- Pages are narrower than the original window, so a few wide diagrams crop at the edge (the eclipse seasons view,
  the inset panels of the star trails year view); the water cycle's stores view draws Earth smaller to keep its enlarged balls).

## Narration

- **Pre-rendered clips** (done). Every narrated line is a resource key; `scripts/narration/generate-narration.py`
  renders it to `assets/narration/audio/<culture>/<key>.<hash>.mp3`. The manifest, `assets/narration/narration.json`,
  is committed and embedded in the app: each clip's text hash, duration and sentence timings, so subtitles stay in
  sync and only changed lines are re-rendered. `NarrationManifestTests` fails when a `Narr_*` string changes without
  rendering again.
- **Voices** (open). The clips are the original app's, rendered with Microsoft's Edge "Read aloud" neural voices
  through edge-tts (`--seed-from` copied the ones whose text matched; the rest were rendered with the same voices).
  Microsoft publishes no licence for redistributing that audio. Replacing it with openly licensed voices is still
  the goal: compare CosyVoice, MeloTTS and Kokoro on a short English and Chinese sample, and check each model's
  weight licence, not just its code licence.
- **Not in git.** Clips are about 180 MB for both languages. They must be rendered (or copied) on the machine that
  builds a release; the release workflow does not have them yet, so its packages and web edition fall back to
  subtitles.
- **Desktop** (Windows done; macOS written, untested): the clips are copied next to the program (`narration/`; in a
  macOS bundle, `Contents/Resources/narration`) and played with winmm (MCI) on Windows and `AVAudioPlayer` on macOS,
  so no audio library ships. Still to do: per-chapter packs downloaded the first time a chapter is opened (checked
  with sha256), so the package and every update do not carry all the audio.
- **Web** (done): clips are static assets under `narration/`, next to the app; their hashed names never change
  content, so the service worker caches each clip cache-first once it has been played. Playback decodes clips with
  Web Audio, which avoids Safari's range-request problems with cached audio. The first clip waits for a tap, as
  browsers require. Still to do: save a chapter for offline use ahead of time, and ask for persistent storage.
- **Fallback**: a clip that is missing or fails to play runs as silent timed subtitles. The system voice (Windows,
  macOS speech, Web Speech API) as an intermediate fallback is not planned now that the clips exist.

## Later

- Subset the web edition's Chinese font to the characters the book uses (the full files are about 8 MB each).
- Settings page: page-turn animation on or off, narration voice and speed.
