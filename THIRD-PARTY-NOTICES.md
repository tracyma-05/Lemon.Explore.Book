# Third-party notices

The Explore Book's own code is under the MIT licence (see `LICENSE`). It ships or uses the following.

| Component | Used for | Licence |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | UI framework (desktop and WebAssembly) | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | View-model helpers | MIT |
| [Noto Sans SC](https://github.com/notofonts/noto-cjk) | Chinese and Latin text in the web edition (`src/Lemon.Explore.Book.Browser/Assets/Fonts`) | SIL Open Font License 1.1 (text alongside the font files) |
| [Natural Earth](https://www.naturalearthdata.com/) | Coastlines in map-based chapters (`Assets/Maps/land-110m.txt`, 1:110m land) | Public domain |

## Narration audio

Narration clips are not stored in this repository; they ship with the desktop packages and the web edition. They
are rendered ahead of time by `scripts/narration/generate-narration.py` with Microsoft's neural voices
`en-US-AvaMultilingualNeural` and `zh-CN-XiaoxiaoNeural`, through [edge-tts](https://github.com/rany2/edge-tts) and
the Edge browser's online "Read aloud" service. Microsoft publishes no licence for redistributing audio from that
service; moving to openly licensed voices is still open (see `docs/ROADMAP.md`, "Narration").
