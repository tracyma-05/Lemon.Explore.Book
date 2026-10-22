# Porting tools

`wpf2avalonia.py` does the mechanical part of porting a chapter from the original WPF app: the rewrites that are
the same in every file. The compiler, and a person, do the rest. The steps of a port are in
[docs/ROADMAP.md](../../docs/ROADMAP.md), "Porting a chapter".

The original app is not part of this repository. Pass its root namespace with `--source-prefix`, and its source and
resource folders as paths. They are never written into the repository.

```bash
python -I tools/porting/wpf2avalonia.py drawing SRC/Controls/Tides/TidesDiagram.cs src/Lemon.Explore.Book/Chapters/Tides/TidesDiagram.cs --namespace Lemon.Explore.Book.Chapters.Tides --source-prefix ROOT --map Controls.Tides=Chapters.Tides
```

```bash
python -I tools/porting/wpf2avalonia.py viewmodel SRC/ViewModels/Explore/TidesViewModel.cs src/Lemon.Explore.Book/Chapters/Tides/TidesViewModel.cs --namespace Lemon.Explore.Book.Chapters.Tides --source-prefix ROOT --map Controls.Tides=Chapters.Tides --chapter
```

```bash
python -I tools/porting/wpf2avalonia.py strings SRC/Resources src/Lemon.Explore.Book/Resources "Tides_*" "Narr_Tides_*"
```

| Command | For | What it does |
|---|---|---|
| `drawing` | diagrams, figures, models | namespaces, usings, brush/pen types, frozen brushes and gradients, transforms, single-figure geometries, dependency → styled properties (callbacks, `AffectsRender`), `OnRender` → `Render` with a `PaintContext`, mouse → pointer, cursors, the `CompositionTarget` loop → animation frames, `Point`/`Vector` idioms |
| `viewmodel` | view models, lessons | namespaces, DI and the original's own plumbing removed; with `--chapter`: localization from the singleton, `IChapterContent` view lifetimes, immutable brushes; `--replacements file.py` applies a chapter's own edits (`REPLACEMENTS = [(old, new), ...]`) |
| `strings` | both resx files | copies keys (exact, or prefixes ending in `*`) that the book does not have yet |

Each run prints what is left to do by hand. Build, fix what the compiler finds, then read the diff: every rewrite
is a plain text pattern.

Self-test:

```bash
python -I -m unittest discover tools/porting
```
