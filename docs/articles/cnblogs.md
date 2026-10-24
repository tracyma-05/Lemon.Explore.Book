# 用 Avalonia 做一本会动、会说话的科学书：一套代码跑 Windows、macOS 和浏览器

> 在线阅读：<https://book-explore.lemontree.asia>
> 源码（MIT）：<https://github.com/tracyma-05/Lemon.Explore.Book>

![四季一章：左页文字，右页轨道图，正在语音讲解](../images/zh-CN/walkthrough.jpg)

## 一、这是什么

**探索之书**是一本给孩子的互动科学书：封面、目录、34 个章节，覆盖天空、地球、物理和化学。每章占一个跨页——左页是文字，
右页是一张可以拖、可以点、会动的图；每一章都有中英双语的语音讲解，讲到哪里就高亮哪里，中途会出选择题，再把控制权交给孩子。

它是一个 .NET 10 + Avalonia 12 的项目：**同一份共享代码**，同时产出

- Windows（x64）和 macOS（Apple 芯片 / Intel）的 **Native AOT** 桌面应用，带原地自动更新；
- 浏览器里的 **WebAssembly PWA**，可以安装、可以离线，手机上一次显示一页。

| 天空 | 地球 | 物理 | 化学 |
|---|---|---|---|
| ![](../images/zh-CN/solar-system.jpg) | ![](../images/zh-CN/rainbow.jpg) | ![](../images/zh-CN/black-hole.jpg) | ![](../images/zh-CN/acids-bases.jpg) |

下面挑几个做的过程中觉得值得一说的技术点。

## 二、一份代码，三个外壳

```
src/
  Lemon.Explore.Book           共享：书本控件、34 章、本地化、设置、朗读、更新
  Lemon.Explore.Book.Desktop   Windows / macOS 外壳（Native AOT）
  Lemon.Explore.Book.Browser   WebAssembly 外壳（PWA 清单 + Service Worker）
tests/
  Lemon.Explore.Book.Tests     单元测试 + Avalonia Headless UI 测试（1000+ 个）
```

几乎所有东西都在共享库里，桌面和浏览器外壳只负责平台相关的部分：桌面端的音频播放（Windows 用 Media Foundation，
macOS 用系统 API）、窗口位置记忆和自动更新；浏览器端用 Web Audio 播放朗读、用 Service Worker 做离线缓存。

共享库标记了 `IsAotCompatible`，所以任何会破坏 AOT 或裁剪的写法（反射、动态生成代码）都会在编译时变成警告，
而 CI 把警告当错误。这一条约束让桌面版可以放心地 Native AOT 发布：不需要装 .NET 运行时，启动快。

## 三、翻页：一张纸的卷曲几何

书最重要的手感是翻页。`Controls/BookView.cs` 负责显示和翻页，卷曲的几何单独放在 `Controls/PageCurlGeometry.cs`，
是一个纯函数：输入翻到哪里（手指的位置），输出页面被折叠的直线、可见的正面和背面多边形、阴影的位置。

把几何和绘制分开的好处是**可以单元测试**（`PageCurlGeometryTests`）：卷曲的形状不用开窗口就能验证。
拖动页边、点击、键盘 ←/→ 最终都走同一套几何。

## 四、每一章的结构：页面 + 讲解

一章由三部分组成：

1. 一个 `PageViewModel` 子类和 `Views/Pages` 里的视图（左页文字、右页图表）；
2. 一个自绘的图表控件，比如四季一章的 `SeasonsDiagram`，在 `Render` 里画轨道、太阳、地轴；
3. 一份讲解脚本，一组 `LessonStep`：

```csharp
new("Narr_Seasons_Tilt_05")
{
    Enter = () => { page.Day = june; page.IsPlaying = false; }, // 先摆好场景
    Highlight = "earth",                                         // 高亮图中的地球
},
new("Narr_Seasons_Tilt_06")
{
    Highlight = "december",
    TryIt = () => DaysApart(page.Day, december) < Near,          // “轮到你了”：等孩子把地球拖到 12 月
},
new("Narr_Seasons_Tilt_08")
{
    Choices = ["..._Choice1", "..._Choice2", "..._Choice3"],     // 选择题
    Answer = 1,
},
```

`LessonPlayer` 负责按顺序执行：设置场景、播放这一步的朗读、同步字幕、等待孩子动手或答题；孩子在讲解中途去拖动图表，
讲解会自动暂停，停手几秒后再继续。

## 五、双语朗读：预生成音频 + 句子时间轴

每个 `Narr_*` 字符串就是一段朗读，中英文各一份。`scripts/narration/generate-narration.py` 用神经网络语音一次性生成 mp3，
同时写出一个清单 `narration.json`：每段的哈希、时长、**每一句的起止时间**。字幕就是按这个时间轴逐句显示的。

- 清单进 git，音频（约 180 MB）不进 git，随桌面包和网页一起发布；
- 文件名里带内容哈希（`Narr_Seasons_Tilt_01.<hash>.mp3`），网页端可以永久缓存；
- 改了一句文案而没重新生成，`NarrationManifestTests` 会失败；
- 没有音频时，按文字长度估算时长，讲解以“定时字幕”方式照常进行。

## 六、本地化：用测试兜底

所有界面文字都是 resx 里的键，英文和中文两个文件必须同时添加。`LocalizationCoverageTests` 会检查：
任何一个文件里缺键、或代码里用了未定义的键，测试都会失败。切换语言时，由 ViewModel 拼出来的文字会重新计算，整本书即时切换。

## 七、Native AOT 桌面版的自动更新

桌面版打开时检查更新，有新版本就在顶部提示，**更新并重启**：下载对应平台的 zip、校验 SHA-256（GitHub 会为 Release 资产
记录哈希）、解压到旁边、替换安装目录、启动新版本；替换失败一定会回滚到旧版本。

更新源是一个地址，可以在三个层级覆盖：

1. 构建时内置：Release 构建默认是本仓库的 GitHub Releases（`github:owner/repo`），fork 发布的版本则自动指向 fork；
2. 程序旁边的 `update-feed.txt`：给不重新编译、只是重新打包分发的人用；
3. 用户在设置里填的地址。

地址既可以是 GitHub 仓库，也可以是任何提供一个小 JSON 清单的 HTTPS 服务器。

## 八、WebAssembly：AOT、R2 和 PWA

浏览器版用 Avalonia.Browser。Debug 构建是解释执行的，图表动画会卡；Release 构建会 AOT 编译成 WebAssembly，流畅得多，
代价是运行时文件很大（`dotnet.native.wasm` 约 43 MiB）。

部署在 Cloudflare 上：一个 Worker、同一个源，静态资源放应用本身，超过单文件 25 MiB 限制的运行时放在 R2 里，并预先存一份
Brotli 压缩版本。PWA 清单和 Service Worker 让它可以安装到桌面或手机主屏、离线阅读。

## 九、让书自己“拍”宣传视频：可步进的时钟

为了给每章做宣传视频，我希望**不录屏**，而是让程序直接把讲解渲染成视频。思路是用 Avalonia Headless 在内存里渲染窗口、
逐帧截图、通过管道喂给 ffmpeg。

第一版是“边播边录”，结果视频只有每秒不到 1 帧：Headless 用 CPU 渲染，1080×1920 的一帧要 1.2 秒，
而所有动画都是按真实时间（`Stopwatch`）走的。

解决办法是给整本书引入一个**共享时钟** `BookClock`，并用一个 API 与 `Stopwatch` 相同的 `BookStopwatch` 替换掉全部 43 处
`Stopwatch` 字段：

```csharp
public static class BookClock
{
    public static double Now => s_held ?? System + s_offset;   // 平时就是系统时钟
    internal static void BeginStepping() => s_held ??= Now;     // 录制时把时间“按住”
    internal static void Advance(double seconds) => s_held += seconds; // 每帧只前进 1/30 秒
    internal static void EndStepping() { /* 从按住的时刻继续走，不跳变 */ }
}
```

正常运行时行为完全不变；录制时，每一帧先把时钟拨快 1/30 秒、再渲染、再截图。**无论一帧画多久，书里的时间都只走 1/30 秒**，
所以视频永远是丝滑的 30fps。朗读也用这个时钟记录每段开始和结束的时间，最后用 ffmpeg 的 `adelay` + `amix` 把 mp3 按时间轴
铺上去，配音和画面严丝合缝。

```bash
python -I scripts/video/make-chapter-video.py --chapter Seasons --culture zh-CN
```

同样的方法也用来生成 README 里的截图和这篇文章的配图：动画停在半途，而不是静止的初始状态。

## 十、测试

1000 多个测试，大部分是纯逻辑（轨道、光学、波动、化学平衡等物理模型），另一部分是 Avalonia Headless 的 UI 测试：
真的打开窗口、翻页、点击图中的太阳，断言弹出的说明卡片是太阳；还会把 34 章的每一个视图都在手机宽度下渲染一遍，
确保窄屏不会因为布局约束冲突而崩溃。

## 结语

这是一个业余项目，目标很简单：让孩子愿意自己去拖一拖、点一点，然后问一句“为什么”。欢迎试用、提 Issue、提 PR。

- 在线阅读：<https://book-explore.lemontree.asia>
- 源码：<https://github.com/tracyma-05/Lemon.Explore.Book>
- 下载桌面版：<https://github.com/tracyma-05/Lemon.Explore.Book/releases/latest>
