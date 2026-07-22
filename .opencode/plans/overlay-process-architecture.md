# Plan: Separate Overlay Process with FftSharp + MessagePack + R3

## Status: SAVED — Ready for implementation

---

## Responsibility Split

| | Main App (EQAPO-Configurator) | Overlay App (EQAPO-Overlay) |
|---|---|---|
| **Audio capture** | NONE | WASAPI loopback (NAudio) |
| **FFT** | NONE | FftSharp (60+fps) |
| **Category levels** | NONE | Computes from FFT + profile config |
| **Rendering** | WPF UI (editor, profiles) | DrawingVisual overlay (60fps) |
| **Profile management** | YES (create/edit/save/load) | Receives profile config via pipe |
| **Category management** | YES (slider adjustments) | Receives category updates via pipe |
| **Device detection** | YES (registry, config files) | Receives device info via pipe |
| **EqualizerAPO config** | YES (writes config.txt) | NONE |
| **Overlay display** | Launches overlay process | Owns the overlay window |

---

## Root Cause of FPS Degradation

1. WPF throttles `CompositionTarget.Rendering` when app loses focus
2. UI thread shared between main app and overlay — any stall kills overlay fps
3. WPF layout cascade compounds over time
4. `List<float>.RemoveAt(0)` on 441K elements causes audio thread stalls
5. Per-frame allocations (CategoryLevel[], SpectrumFrame, Hamming window) generate GC pressure
6. `SpectrumAnalyzer` recreates hundreds of WPF shapes every frame

---

## IPC Protocol (Main App → Overlay)

3 message types — config data, never audio:

```csharp
// Sent once on profile activation/switch
[MessagePackObject]
public class ProfileConfigMessage : OverlayMessage
{
    [Key(0)] public string ProfileName;
    [Key(1)] public string Genre;
    [Key(2)] public string[] CategoryNames;
    [Key(3)] public CategoryFilterConfig[] Categories;
    [Key(4)] public int SampleRate;      // for overlay's FFT config
    [Key(5)] public int FftSize;         // overlay's FFT size
}

[MessagePackObject]
public class CategoryFilterConfig
{
    [Key(0)] public string Name;
    [Key(1)] public FilterConfig[] Filters;
}

[MessagePackObject]
public class FilterConfig
{
    [Key(0)] public double CenterFrequency;
    [Key(1)] public double Q;
    [Key(2)] public double BaseGain;
    [Key(3)] public double UserOffset;
}

// Sent when status changes (profile activate/deactivate)
[MessagePackObject]
public class StatusMessage : OverlayMessage
{
    [Key(0)] public string Status;   // "EQ ACTIVE", "NO PROFILE", etc.
    [Key(1)] public string Color;    // "#00CC66", "#FF6644", etc.
    [Key(2)] public string ProfileName;
    [Key(3)] public string Genre;
}

// Sent on main app close
[MessagePackObject]
public class ShutdownMessage : OverlayMessage { }
```

No audio data crosses the pipe. The overlay captures its own audio independently.

---

## Architecture

```
EQAPO-Configurator.exe                      EQAPO-Overlay.exe
┌────────────────────────────┐              ┌──────────────────────────────┐
│                            │              │ AudioCaptureService          │
│ ProfileManager             │  Named Pipe  │  NAudio WasapiLoopback       │
│ ConfigWriter               │ ──────────>  │  Mix to mono                │
│ DeviceDetector             │  MessagePack │                              │
│ GameDetector               │              │ AudioProcessor              │
│                            │  Sends:      │  FftSharp FFT + windowing   │
│ OverlayProcessService      │  • Profile   │  Log-freq binning (256)     │
│  Pipe server               │  • Status    │  Smoothing                  │
│  R3 Subject outbound       │  • Shutdown  │  Peak/RMS compute           │
│  Process lifecycle         │              │                              │
│                            │              │ CategoryLevelComputer       │
│ Sends on profile change:   │              │  Uses profile filter config  │
│  ProfileConfigMessage      │              │  Computes raw/post-EQ dB    │
│                            │              │  on thread pool             │
│ Sends on status change:    │              │                              │
│  StatusMessage             │              │ RenderThread                │
│                            │              │  Stopwatch 60fps loop       │
│ Sends on close:            │              │  DrawingVisual bars         │
│  ShutdownMessage           │              │  No WPF layout              │
│                            │              │  WS_EX_TRANSPARENT          │
│ Writes EqualizerAPO:       │              │  Topmost, click-through     │
│  config.txt                │              │                              │
└────────────────────────────┘              └──────────────────────────────┘
```

---

## Main App Keeps

| File | Why |
|------|-----|
| `Services/ConfigWriter.cs` | Core — writes EqualizerAPO config |
| `Services/DeviceDetector.cs` | Core — device detection from registry |
| `Services/EqualizerApoService.cs` | Core — resolves EqualizerAPO paths |
| `Services/ProfileManager.cs` | Core — save/load profiles |
| `Services/GameDetector.cs` | Core — detects running games |
| `Services/AppSettingsService.cs` | App settings |
| `Services/BiquadFilter.cs` | Pure math for EQ curve drawing in editor |
| `Models/GameProfile.cs` | Core model |
| `Models/SoundCategory.cs` + `FilterMapping` | Core model |
| `Models/EqProfile.cs` | EQ editor model |
| `Models/EqBand.cs` | EQ band model |
| `Models/GameGenre.cs` | Genre enum |
| `Controls/ParametricEqEditor.xaml(.cs)` | EQ editor (modified — no live spectrum) |
| `Controls/ClipReviewWindow.xaml(.cs)` | Clip review (modified — no spectrum) |
| `Converters/BandLevelConverters.cs` | Band level display |
| `MainWindow.xaml(.cs)` | Main window (heavily modified) |

---

## Main App Removes

| File | Why |
|------|-----|
| `Services/SpectrumService.cs` | WASAPI + FFT — audio analysis moves to overlay |
| `Services/CaptureService.cs` | Audio clip recording — not wired up, not core |
| `Services/OverlayService.cs` | Replaced by `OverlayProcessService` |
| `Controls/SpectrumAnalyzer.xaml(.cs)` | Live spectrum — no longer in editor |
| `Controls/CompactSpectrum.xaml(.cs)` | Spectrum bars — no longer in overlay |
| `Controls/OverlayWindow.xaml(.cs)` | Moved to overlay project |
| `Controls/BandLevelsBar.xaml(.cs)` | Moved to overlay project |
| `Services/NativeOverlay.cs` | Moved to overlay project |
| `Models/SpectrumData.cs` | `SpectrumFrame`, `SpectrumConfig`, `AudioClip` — all removed |

---

## Main App Modifies

| File | Change |
|------|--------|
| `MainWindow.xaml.cs` | Remove `SpectrumService`, `CaptureService`, `OverlayService`. Add `OverlayProcessService`. Remove `OnSpectrumUpdated()`, `OnClipCaptured()`. Wire `OverlayProcessService.Show(profile)` on activation, `Hide()` on deactivation. |
| `Controls/ParametricEqEditor.xaml` | Remove `<controls:SpectrumAnalyzer>` from Grid.Row="1" |
| `Controls/ParametricEqEditor.xaml.cs` | Remove `UpdateSpectrum()`, remove `LiveSpectrum?.SetEqBands()`. Keep `DrawCurve()`, `LoadProfile()` |
| `Models/EqBand.cs` | Remove `BandLevelDb`, `BandLevelDisplay` (was for SpectrumAnalyzer) |
| `EQAPO-Configurator.csproj` | Remove NAudio (no longer needed), add project reference to `EQAPO-Overlay` |

---

## New Files

| Project | File | Purpose |
|---------|------|---------|
| `EQAPO-Overlay` | `EQAPO-Overlay.csproj` | WPF + NAudio + FftSharp + MessagePack |
| `EQAPO-Overlay` | `App.xaml` + `.cs` | Entry point, reads `--pipe=<name>` arg |
| `EQAPO-Overlay` | `OverlayWindow.xaml` + `.cs` | Borderless topmost window (from main app) |
| `EQAPO-Overlay` | `Protocol/OverlayMessage.cs` | MessagePack types |
| `EQAPO-Overlay` | `Protocol/PipeReader.cs` | Named pipe client → R3 Observable |
| `EQAPO-Overlay` | `Audio/AudioCaptureService.cs` | WASAPI loopback + mono mix (from SpectrumService) |
| `EQAPO-Overlay` | `Audio/AudioProcessor.cs` | FftSharp FFT + windowing + binning + smoothing |
| `EQAPO-Overlay` | `Processing/CategoryLevelComputer.cs` | Category level compute (from OverlayService) |
| `EQAPO-Overlay` | `Rendering/RenderThread.cs` | 60fps Stopwatch + DrawingVisual |
| `EQAPO-Overlay` | `Rendering/BandLevelsBar.cs` | Category bars (from main app) |
| `EQAPO-Overlay` | `Services/NativeOverlay.cs` | Win32 interop (from main app) |
| Main App | `Services/OverlayProcessService.cs` | Pipe server + R3 Subject + process lifecycle |

---

## Implementation Order

| Step | Task | Depends On |
|------|------|------------|
| 1 | Create `EQAPO-Overlay` project + csproj | — |
| 2 | Create `OverlayMessage.cs` (shared MessagePack types) | Step 1 |
| 3 | Create `AudioCaptureService.cs` in overlay (WASAPI loopback, from SpectrumService lines 84-110) | Step 1 |
| 4 | Create `AudioProcessor.cs` in overlay (FftSharp FFT, windowing, binning, smoothing) | Step 1 |
| 5 | Create `CategoryLevelComputer.cs` in overlay (from OverlayService.ComputeCategoryLevels) | Step 2 |
| 6 | Move `OverlayWindow` XAML + code to overlay, remove CompactSpectrum | Step 1 |
| 7 | Move `BandLevelsBar` to overlay (DrawingVisual-based) | Step 6 |
| 8 | Move `NativeOverlay.cs` to overlay | Step 1 |
| 9 | Create `RenderThread.cs` (60fps Stopwatch + DrawingVisual) | Step 7 |
| 10 | Create `PipeReader.cs` (named pipe client → R3 Observable) | Step 2 |
| 11 | Create `App.xaml` entry point (wire PipeReader → AudioProcessor → CategoryComputer → RenderThread) | Steps 3-10 |
| 12 | Create `OverlayProcessService.cs` in main app (pipe server + process launch/stop) | Step 2 |
| 13 | Update `MainWindow.xaml.cs` (remove old services, wire OverlayProcessService) | Step 12 |
| 14 | Remove `SpectrumAnalyzer` from `ParametricEqEditor.xaml` + `.cs` | — |
| 15 | Remove `BandLevelDb` from `EqBand.cs` | Step 14 |
| 16 | Remove NAudio from main `csproj` (no longer needed) | Step 13 |
| 17 | Delete old files from main app (SpectrumService, CaptureService, OverlayService, SpectrumAnalyzer, CompactSpectrum, OverlayWindow, BandLevelsBar, NativeOverlay, SpectrumData) | Steps 12-16 |
| 18 | Add post-build copy of overlay exe to main output | Step 1 |
| 19 | Build both projects, fix all errors | All steps |

---

## Key Library Choices

| Component | Library | Why |
|-----------|---------|-----|
| FFT | **FftSharp** | 5-12x faster than NAudio FFT, built-in windowing, MIT license |
| Audio capture | **NAudio** (WasapiLoopbackCapture) | Proven, well-documented |
| Serialization | **MessagePack** (v3.1.8) | Already a dependency, ~5-10x faster than JSON |
| Reactive IPC | **R3** (1.3.1) | Already a dependency, same pattern as current codebase |
| Rendering | **DrawingVisual** | Bypasses WPF layout, proven 60fps |
| IPC transport | **Named Pipes** | ~10-50us latency, trivial bandwidth for config data |

---

## Research References

- **SpectrumNet** (github.com/diqezit/SpectrumNet) — C# WPF + NAudio + FftSharp + SkiaSharp, proven 60fps overlay
- **FftSharp** (github.com/swharden/FftSharp) — .NET FFT library, 11 window types, MIT license
- **FFTW benchmark** — NAudio FFT: 169us, FftSharp: ~50us, FFTW: ~14us for 4096-point
- **VB-Audio Spectralissime** — professional spectrum uses band-pass filter bank (not FFT) for constant precision
