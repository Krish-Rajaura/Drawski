# Drawski

Draw on top of anything on your screen. Drawski puts a transparent layer over your whole desktop, so you can circle, underline and sketch over a PDF, a website, slides or code while you explain it, without switching to a whiteboard app.

It can also open PDFs itself. When you do that, your drawings stick to the pages: they scroll and zoom with the document, and they come back when you open the same PDF later.

---

## Contents

- [Requirements](#requirements)
- [Install and run](#install-and-run)
- [Quick start](#quick-start)
- [The two ways to draw](#the-two-ways-to-draw)
- [Toolbar](#toolbar)
- [Tools](#tools)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [Saving and opening](#saving-and-opening)
- [Whiteboard](#whiteboard)
- [Troubleshooting](#troubleshooting)
- [Performance](#performance)
- [Known limitations](#known-limitations)
- [Project structure](#project-structure)

---

## Requirements

- Windows 10 (version 2004 / build 19041) or newer, or Windows 11
- [.NET 11 SDK](https://dotnet.microsoft.com/download) to build it. If you only run a published exe, you need just the .NET 11 Desktop Runtime.

## Install and run

**Run from source**

```powershell
cd C:\projects\Drawski
dotnet run
```

**Build, then run the exe**

```powershell
dotnet build
.\bin\Debug\net11.0-windows10.0.19041.0\Drawski.exe
```

**Make a single exe you can copy anywhere**

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

This makes `publish\Drawski.exe` (about 25 MB). It still needs the .NET 11 Desktop Runtime installed on the PC. To get an exe that runs without .NET installed, use `--self-contained true` instead. The file will be much bigger.

Use a published Release build for everyday use. It's pre-compiled (ReadyToRun), so it starts faster than `dotnet run` or a Debug build.

**Open a PDF straight away**

```powershell
Drawski.exe "C:\path\to\notes.pdf"
```

You can also right-click a PDF in Explorer → **Open with** → **Choose another app** → browse to `Drawski.exe`.

**Startup options**

| Option | What it does |
|---|---|
| `"path\to\file.pdf"` | Opens that PDF straight away |
| `--gpu` | Draws using the graphics card instead of the CPU. Smoother scrolling on slow CPUs or 4K screens, but uses 3× more memory. See [Performance](#performance). |

```powershell
Drawski.exe --gpu "C:\path\to\notes.pdf"
```

## Quick start

1. Start Drawski. A dark toolbar appears on the left edge of the screen. Nothing else changes: you're in **Mouse** mode, so clicks go through to your apps as normal.
2. Click the **pen** (or press `Ctrl+Alt+D`). A thin blue border appears around the screen, which means you're drawing.
3. Draw over whatever is on screen.
4. Press `Esc` (or `Ctrl+Alt+D` again) to go back to Mouse mode. Your drawing stays visible, and you can click and scroll the app underneath again.
5. Press `Ctrl+Alt+C` to wipe it, or `Ctrl+Alt+H` to hide it for a moment.

## The two ways to draw

### 1. Screen overlay (any app)

The default. Drawski covers every monitor with an invisible layer.

| Mode | What happens |
|---|---|
| **Mouse** | The overlay ignores the mouse. Clicks, scrolling and typing go to the app underneath. Your ink stays on screen. |
| **Drawing** (any tool picked) | The overlay takes the mouse and pen input. A blue border shows you're in this mode. |

Overlay ink is fixed to the **screen**, not to the app. If you scroll the PDF underneath, the drawing stays where it was. Clear it, or hide it, before you scroll.

### 2. Drawski's PDF viewer (ink stays on the page)

Open a PDF with the **Open** button or `Ctrl+O`.

- Each page has its own ink, so drawings scroll and zoom with the page.
- Scroll with the mouse wheel, the scroll bar, or `Page Up` / `Page Down`. This works with any tool selected, so you don't have to switch to Mouse mode to scroll.
- `Ctrl + mouse wheel` zooms, centred on the cursor.
- The bottom-right corner shows the page number and the zoom level.
- A `•` in the window title means there are unsaved drawings.

While a PDF is open, the tools draw **on the PDF**, and the overlay stays out of the way. Close the PDF window to draw over other apps again.

## Toolbar

Drag the toolbar by the grip at the top. Hover over any button to see its name and shortcut. Clicking the toolbar never takes focus away from the app you're using.

| Section | Buttons |
|---|---|
| Top | **Mouse**: stop drawing and click through to apps |
| Tools | Pen, Highlighter, Laser, Eraser, Line, Arrow, Rectangle, Ellipse |
| Colours | Red, Yellow, Green, Blue, Black, White |
| Sizes | Thin, Medium, Thick |
| Actions | Undo, Redo, Whiteboard, Hide ink, Save, Open, Clear, Quit |

## Tools

| Tool | Key | Notes |
|---|---|---|
| Pen | `P` | Smooth freehand ink. Uses pressure if you draw with a pen or stylus. |
| Highlighter | `H` | Wide and see-through. Text underneath stays readable. |
| Laser | `X` | Fades out about 1 second after you lift the pen. Good for pointing while you talk. Never saved, never in undo. |
| Eraser | `E` | Removes a **whole stroke** you touch. |
| Line | `L` | Drag from start to end. Hold `Shift` to snap to 45°. |
| Arrow | `A` | Like Line, with an arrowhead at the end. `Shift` snaps to 45°. |
| Rectangle | `R` | Drag corner to corner. `Shift` makes a square. |
| Ellipse | `O` | Drag corner to corner. `Shift` makes a circle. |

Picking a colour while in Mouse or Eraser mode switches back to your last drawing tool.

## Keyboard shortcuts

**Global**: these work from any app, even when Drawski isn't focused.

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+D` | Switch between Mouse mode and your last drawing tool |
| `Ctrl+Alt+C` | Clear all ink |
| `Ctrl+Alt+H` | Hide / show ink |

**While drawing**: these work when the overlay or the PDF window has focus.

| Shortcut | Action |
|---|---|
| `Esc` | Back to Mouse mode |
| `P` `H` `X` `E` | Pen, Highlighter, Laser, Eraser |
| `L` `A` `R` `O` | Line, Arrow, Rectangle, Ellipse |
| `1` – `6` | Red, Yellow, Green, Blue, Black, White |
| `[` / `]` | Thinner / thicker |
| `B` | Whiteboard on / off |
| `Del` | Clear |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` or `Ctrl+Shift+Z` | Redo |
| `Ctrl+S` | Save |
| `Ctrl+O` | Open a PDF or ink file |
| `Shift` (hold while dragging a shape) | Square, circle, or 45° snapping |

## Saving and opening

Drawski never saves on its own. You save when you want to. If you close a PDF or quit with unsaved drawings, it asks: **Yes** saves, **No** throws them away, **Cancel** goes back.

### PDF ink

`Ctrl+S` in a PDF saves straight away, with no dialog. The ink goes to a file next to the PDF:

```
notes.pdf
notes.pdf.drawski   ← your drawings
```

Next time you open `notes.pdf` in Drawski, the drawings load automatically. The PDF file itself is **never changed**.

- If the PDF's folder isn't writable (a read-only share, a protected folder), the ink goes to `%LocalAppData%\Drawski\ink\` instead, and still loads automatically next time.
- If every drawing has been erased, saving deletes the `.drawski` file.
- To share annotated notes, send **both** files and keep their names matching.

### Screen overlay ink

`Ctrl+S` opens a Save dialog. Pick the format from the "Save as type" list:

| Format | What you get |
|---|---|
| **PNG** | A screenshot of the screen **with** your ink on top. The toolbar and blue border are left out. Good for sharing. |
| **ISF** (`.isf`) | Only the ink, with nothing underneath. Open it again later with `Ctrl+O` to keep working on it. |

### Opening

`Ctrl+O` opens either:
- a **`.pdf`**, which opens in Drawski's viewer with its saved ink, or
- a **`.isf`**, which adds that ink to the overlay or whiteboard. One undo removes it again.

## Whiteboard

Press `B` or click the whiteboard button. The screen turns plain white, which is good for a quick sketch or diagram in the middle of an explanation.

- The whiteboard has its **own** ink and its own undo history. Switching back shows your overlay or PDF ink untouched.
- Whiteboard ink is kept until you quit. Save it as a PNG or ISF if you want to keep it.

---

## Troubleshooting

### I can't click anything / my apps stopped responding to the mouse
You're in drawing mode, so the overlay is catching your clicks. Look for the blue border around the screen. Press `Esc` or `Ctrl+Alt+D`, or click **Mouse** on the toolbar.

### I'm drawing but nothing appears
- If the PDF viewer is open, the tools draw on the PDF and **not** on the screen. Close the PDF window to draw over other apps.
- The ink may be hidden. Press `Ctrl+Alt+H`, or check the eye button on the toolbar.
- You may have the **Eraser** or **Mouse** selected. Pick the pen (`P`).
- Black ink on a dark app, or white ink on a light one, can be hard to see. Change the colour.

### The global shortcuts (`Ctrl+Alt+D/C/H`) do nothing
- Another app may already use that shortcut. Windows gives each shortcut to only one app, and Drawski gets no warning when it misses out. Close the other app, or change the keys in `OverlayWindow.xaml.cs` (`OnLoaded`, the `RegisterHotKey` lines).
- On some keyboard layouts `Ctrl+Alt` acts as `AltGr` and types a character instead.
- Shortcuts don't reach programs running **as administrator** unless Drawski is also run as administrator.

### The letter shortcuts (`P`, `H`, `1`…) do nothing
These only work when Drawski has keyboard focus, which it gets when you start drawing. After clicking in another app, click once on the overlay in drawing mode (or in the PDF window) and try again. Global shortcuts always work.

### The toolbar has gone off-screen or behind something
- It's always on top, but a full-screen game or video player in exclusive mode can cover it. Leave full screen.
- If you dragged it onto a monitor that's now unplugged, quit (Task Manager → Drawski → End task) and start again. It comes back on the left of the main screen.

### Drawski doesn't start when I already have it open
Only one copy runs at a time. A second launch, including **Open with → Drawski** on a PDF, quietly exits. Use the copy that's already open and its **Open** button. If no toolbar is visible, end `Drawski.exe` in Task Manager and start it again.

### The overlay doesn't cover my second monitor, or ink is offset
The overlay resizes when monitors or scaling change. If it's still wrong, restart Drawski. When monitors use different scale settings (for example 100% and 150%), ink may look thicker or thinner on one of them.

### A PDF won't open
- **Password-protected** PDFs aren't supported.
- Damaged or unusual PDFs may fail in the Windows PDF engine Drawski uses. Try opening and re-saving the file in Edge or another viewer ("Print → Save as PDF").
- Make sure the file isn't still downloading or open with an exclusive lock in another app.

### My PDF drawings didn't come back
- The `.drawski` file must sit **next to** the PDF with the matching name: `notes.pdf` → `notes.pdf.drawski`. If you renamed or moved the PDF, rename or move the `.drawski` file too.
- If the ink was saved to `%LocalAppData%\Drawski\ink\` (read-only folder case), it's linked to the PDF's **full path**. Moving the PDF breaks that link. Put the PDF back where it was, open it, and save again from a writable folder.
- You may have clicked **No** when asked to save.
- "Found saved drawings but couldn't read them" means the `.drawski` file is damaged or from an incompatible version.

### Ink lands on the wrong page
The PDF was edited (pages added, removed or reordered) after the ink was saved. Ink is saved per page **number**, so it follows the numbers, not the content.

### The PDF looks blurry for a moment
Pages are shown quickly at the old size while zooming, then redrawn sharp about 0.2 s after you stop zooming. Pages coming onto the screen are drawn as you scroll.

### Scrolling a PDF feels choppy
Drawski draws on the CPU by default to save memory. On a slow CPU or a 4K screen, start it with `--gpu` instead (see [Startup options](#install-and-run)). It uses more memory but scrolls more smoothly.

### High memory use
Normal use is about 55 MB on its own and 170–240 MB with a PDF open at full screen. If you see 3× more, you're probably running with `--gpu`. See [Performance](#performance).

### The PNG screenshot is very wide
With several monitors, the PNG covers all of them in one image. Crop it afterwards if you need one screen.

### Build errors
- `NETSDK1045` / target framework not supported: install the .NET 11 SDK and check with `dotnet --list-sdks`.
- The first build downloads the Windows SDK reference package from NuGet, so it needs an internet connection once.
- "File is locked" / "being used by another process": Drawski is still running. Quit it (toolbar ✕ or Task Manager) and build again.

---

## Performance

Measured on a 1920×1200 screen at 150% scaling, using a 200-page PDF in a full-screen window (Release build, private memory):

| | Default (CPU) | `--gpu` |
|---|---|---|
| Idle, toolbar only | ~55 MB | ~160 MB |
| PDF open | ~170 MB | ~370 MB |
| After fast scrolling through the PDF | ~240 MB | ~520–680 MB |
| CPU while scrolling fast | ~2× higher | lower |
| CPU while drawing | about the same | about the same |
| Startup | ~0.5 s | ~0.5 s |

How memory is kept low:
- **Only pages near the screen are drawn.** Pages further away free their image. Their ink is kept as lightweight point data. Memory doesn't grow with the length of the PDF.
- **Pages are drawn at the exact screen resolution**, no bigger. The Windows PDF engine adds its own display scaling, so Drawski measures that once when a PDF opens and compensates. This halves the size of each page image.
- **Pages are stored in a format WPF can draw directly**, so WPF doesn't keep a second converted copy of every page.
- **CPU rendering by default.** WPF's graphics-card mode makes the graphics driver keep its own copies of every page image (and they build up while scrolling). For flat ink and page images, the CPU is fast enough on any recent PC.
- **ReadyToRun and lighter runtime settings** in the published build, for faster startup.

## Known limitations

- Overlay ink is fixed to the screen. Only Drawski's own PDF viewer keeps ink attached to the content.
- Ink saved for a PDF only shows in Drawski. Other PDF viewers won't show it.
- PDF pages are images, so you can't select or copy text.
- The eraser removes whole strokes, not parts of them.
- The overlay doesn't appear over apps in exclusive full-screen mode (some games and video players).
- **Clear** in the PDF viewer wipes ink on every page. One undo brings it all back.

## Project structure

| File | What it does |
|---|---|
| `App.xaml(.cs)` | Startup, single-instance check, passes a PDF path from the command line |
| `OverlayWindow.xaml(.cs)` | The full-screen transparent layer. Holds the current tool, colour and size, handles shortcuts, the whiteboard, laser fading, saving and opening |
| `ToolbarWindow.xaml(.cs)` | The floating toolbar |
| `PdfWindow.xaml(.cs)` | PDF viewer: page virtualization, zoom, per-page ink, `.drawski` files |
| `InkSurface.cs` | Tool behaviour for one drawing canvas (pen settings, shapes, laser tagging), used by both the overlay and every PDF page |
| `InkHistory.cs` | Undo / redo, which can cover many pages at once |
| `Native.cs` | Windows API calls: click-through, global shortcuts, covering all monitors, screen capture |
