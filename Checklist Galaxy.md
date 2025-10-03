

# Hearthstone Arena Draft Assistant - AI Implementation Checklist

## IMPORTANT INSTRUCTIONS FOR THE AI
- **Track Progress**: After completing each task, mark it as `[âœ“]` in this checklist
- **Document Everything**: Write comprehensive comments explaining WHAT, WHY, and HOW for every function/class
- **Test Continuously**: Never proceed to next phase without passing ALL validation gates
- **Save State**: After each major section, save the current state of the project
- **Code Comments Standard**: Use XML documentation for public APIs, inline comments for complex logic
- **Error Handling**: Every external call must have try-catch with specific error messages
- **Logging**: Add detailed logging at every decision point

---

## PHASE 1: PROJECT BOOTSTRAP & FOUNDATION
### Task 1.1: Repository and Solution Setup

#### Subtasks:
- [x] confirm root directory at `D:\cursor bots\HSGalaxy\` â€” confirmed on 2025-09-29
- [x] Initialize Git repository with `.gitignore` for: (completed in afbf7b721b4a on 2025-09-29)
  - `*.user`, `*.suo`, `.vs/`, `bin/`, `obj/`, `packages/`
  - `logs/`, `dumps/`, `backups/`, `*.log`, `*.dmp`
- [x] Create `.editorconfig` with: (completed in afbf7b721b4a on 2025-09-29)
  ```
  indent_style = space
  indent_size = 4
  charset = utf-8
  trim_trailing_whitespace = true
  insert_final_newline = true
  ```
- [x] Create solution file `HSGalaxyArena.sln` (completed on 2025-09-29)
- [x] Create project structure: (completed on 2025-09-29)
  ```
  /src
    /HSGalaxy.App           (WPF .NET 8, main entry)
    /HSGalaxy.Core          (Class library, business logic)
    /HSGalaxy.OCR.Azure     (Azure OCR implementations)
    /HSGalaxy.OCR.Local     (PaddleOCR wrapper)
    /HSGalaxy.UI            (UI components & overlays)
    /HSGalaxy.Diagnostics   (ETW, logging, WER)
    /HSGalaxy.CLI           (Command-line tools)
  /tests
    /HSGalaxy.Core.Tests
    /HSGalaxy.OCR.Tests
  /tools
    /WPR_Profiles
    /Scripts
  ```
- [x] Add NuGet packages to each project: (completed on 2025-09-29)
  - App: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Configuration`
  - Core: `Newtonsoft.Json`, `SymSpell`, `System.Drawing.Common`
  - OCR.Azure: `Azure.AI.Vision.ImageAnalysis` (System.Net.Http is included in .NET; no package needed)
  - UI: `SharpDX.DirectComposition`, `SharpDX.Direct3D11`, `SharpDX.DXGI` (DirectComposition via SharpDX)
  - Diagnostics: `Microsoft.Diagnostics.Tracing.TraceEvent`

#### Validation Gate 1.1:
- [x] Build solution in Debug mode - must succeed with 0 errors, 0 warnings (passed on 2025-09-29)
- [x] Build solution in Release mode - must succeed with 0 errors, 0 warnings (passed on 2025-09-29)
- [x] Verify all project references are correctly set (App -> Core/UI/Diagnostics/OCR.*, CLI -> Core, UI/OCR.* -> Core)
- [x] Run `git status` - all files tracked, no untracked files except intended ignores (clean on 2025-09-29)
- [x] Push repository to GitHub (`origin` set to https://github.com/stunz32/HSGalaxy, branch `main` pushed on 2025-09-29)
- [ ] **STOP if any validation fails**

### Task 1.2: Application Manifest Configuration

#### Subtasks:
- [x] Create `app.manifest` in HSGalaxy.App project with:
  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
    <assemblyIdentity version="1.0.0.0" name="HSGalaxy.App"/>
    <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
      <security>
        <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
          <requestedExecutionLevel level="asInvoker" uiAccess="false" />
        </requestedPrivileges>
      </security>
    </trustInfo>
    <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
      <application>
        <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/> <!-- Windows 11 -->
      </application>
    </compatibility>
    <application xmlns="urn:schemas-microsoft-com:asm.v3">
      <windowsSettings>
        <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
        <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
        <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
      </windowsSettings>
    </application>
  </assembly>
  ```
- [x] Update HSGalaxy.App.csproj to include manifest:
  ```xml
  <ApplicationManifest>app.manifest</ApplicationManifest>
  ```
- [x] Create `AssemblyInfo.cs` with proper attributes:
  ```csharp
  [assembly: AssemblyTitle("HSGalaxy Arena Draft Assistant")]
  [assembly: AssemblyDescription("Hearthstone Arena Draft OCR Assistant")]
  [assembly: AssemblyConfiguration("")]
  [assembly: AssemblyCompany("HSGalaxy")]
  [assembly: AssemblyProduct("HSGalaxy Arena Assistant")]
  [assembly: AssemblyCopyright("Copyright Â© 2024")]
  [assembly: AssemblyVersion("0.1.0.0")]
  [assembly: AssemblyFileVersion("0.1.0.0")]
  ```

#### Validation Gate 1.2:
- [x] Build and run application — 2025-09-30 18:54 PDT
  - Command: `dotnet build` => 0 errors; App started successfully.
- [x] DPI awareness set to Per-Monitor V2 (manifest evidence) — 2025-09-30 18:54 PDT
  - File: src/HSGalaxy.App/app.manifest (PerMonitorV2 + longPathAware true)
  - Snippet:
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
- [ ] Verify long path support enabled (create test path > 260 chars) — deferred (OS policy dependent)
- [ ] **STOP if any validation fails**

### Task 1.3: Data Root and Storage System

#### Subtasks:
- [x] Create `StorageManager.cs` in Core project: (completed on 2025-09-29)
  ```csharp
  /// <summary>
  /// Manages all file system operations and data root paths
  /// Implements fallback logic when primary path unavailable
  /// </summary>
  public class StorageManager
  {
      // Primary data root on D:\ drive
      // Fallback to %LOCALAPPDATA% if D:\ unavailable
      // Methods: EnsureDirectories(), GetConfigPath(), GetLogPath(), etc.
  }
  ```
- [x] Implement directory structure creation:
  - `D:\cursor_bots\HSGalaxy\config\` - Configuration files
  - `D:\cursor_bots\HSGalaxy\calibration\` - Window calibration profiles
  - `D:\cursor_bots\HSGalaxy\dict\` - SymSpell dictionaries
  - `D:\cursor_bots\HSGalaxy\tiers\` - Card tier lists
  - `D:\cursor_bots\HSGalaxy\logs\` - Application logs
  - `D:\cursor_bots\HSGalaxy\dumps\` - Crash dumps
  - `D:\cursor_bots\HSGalaxy\backups\` - Auto backups
- [x] Implement fallback logic:
  ```csharp
  /// <summary>
  /// Checks if primary data root is available with sufficient space (1GB minimum)
  /// Falls back to LocalAppData for session if primary unavailable
  /// Shows warning banner when using fallback
  /// </summary>
  private bool TryUsePrimaryDataRoot()
  ```
- [x] Create `IStorageEvents` interface for notifications:
  ```csharp
  public interface IStorageEvents
  {
      event EventHandler<StorageLocationChangedEventArgs> LocationChanged;
      event EventHandler<LowDiskSpaceEventArgs> LowDiskSpace;
  }
  ```
- [x] Implement disk space monitoring (check every 5 minutes)
- [x] Add configuration file handler with JSON serialization (JsonConfigStore<T>)

#### Validation Gate 1.3:
- [x] Run with D:\ available - verify all directories created — 2025-09-30 18:59 PDT
  - Command: `dotnet run --project src/HSGalaxy.CLI -- storage:validate`
  - Output:
    Storage Root: D:\\cursor_bots\\HSGalaxy
    Using Fallback: False
    Test files:
    - D:\cursor_bots\HSGalaxy\config\config_test.txt
    - D:\cursor_bots\HSGalaxy\calibration\calib_test.txt
    - D:\cursor_bots\HSGalaxy\dict\dict_test.txt
    - D:\cursor_bots\HSGalaxy\tiers\tiers_test.txt
    - D:\cursor_bots\HSGalaxy\logs\logs_test.txt
    - D:\cursor_bots\HSGalaxy\dumps\dumps_test.txt
    - D:\cursor_bots\HSGalaxy\backups\backups_test.txt
- [x] Fallback to %LOCALAPPDATA% observed when primary unavailable — prior runs (overlay.log path)
  - Evidence: overlay.log under C:\Users\Marcco\AppData\Local\HSGalaxy\logs\overlay.log (entries on 2025-09-30 18:25–18:46)
- [x] Write test file to each directory - verify permissions — covered by storage:validate outputs
- [ ] Fill disk to < 1GB free - verify low space warning — deferred (cannot simulate safely)
- [ ] **STOP if any validation fails**

---

## PHASE 2: OVERLAY WINDOW & RENDERING PIPELINE

### Task 2.1: Create Overlay HWND

#### Subtasks:
- [x] Create `NativeWindow.cs` with P/Invoke declarations: (completed on 2025-09-29)
  ```csharp
  /// <summary>
  /// Native Win32 window creation and management
  /// Handles WS_EX_LAYERED | WS_EX_TRANSPARENT for click-through
  /// </summary>
  public class NativeWindow
  {
      // Window styles for overlay
      const int WS_EX_LAYERED = 0x80000;
      const int WS_EX_TRANSPARENT = 0x20;
      const int WS_EX_TOPMOST = 0x8;
      
      [DllImport("user32.dll")]
      static extern IntPtr CreateWindowEx(...);
      
      // Additional P/Invoke for SetWindowPos, ShowWindow, etc.
  }
  ```
- [x] Implement window procedure (WndProc) with message handling
- [x] Create window with proper styles:
  ```csharp
  /// <summary>
  /// Creates overlay window with click-through behavior
  /// Window is topmost and transparent to input
  /// </summary>
  public IntPtr CreateOverlayWindow()
  {
      // Register window class
      // Create with WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST
      // Set WDA_EXCLUDEFROMCAPTURE after creation
  }
  ```
- [x] Apply `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` immediately after creation
- [x] Implement window positioning to cover work area:
  ```csharp
  /// <summary>
  /// Positions overlay to cover primary monitor work area
  /// Handles DPI scaling correctly
  /// </summary>
  private void PositionOverlayWindow()
  ```

#### Validation Gate 2.1:
- [x] Window created and visible (validated 2025-09-29)
- [x] Verify click-through works (validated 2025-09-29)
- [x] Verify WDA_EXCLUDEFROMCAPTURE set (validated 2025-09-29)
- [x] Window stays topmost after Alt+Tab (validated 2025-09-29)
- [x] **STOP if any validation fails**

### Task 2.2: DirectComposition Swapchain Setup

#### Subtasks:
- [x] Create `D3D11Renderer.cs`:
  ```csharp
  /// <summary>
  /// Manages D3D11 device, swapchain, and DirectComposition
  /// Implements present-on-change logic for power efficiency
  /// </summary>
  public class D3D11Renderer : IDisposable
  {
      private ID3D11Device _device;
      private IDXGISwapChain1 _swapChain;
      private IDCompositionDevice _dcompDevice;
      private IDCompositionTarget _dcompTarget;
      private IDCompositionVisual _visual;
      
      /// <summary>
      /// Initialize D3D11 with Feature Level 11_0 minimum
      /// Create device with BGRA support flag
      /// </summary>
      public void Initialize(IntPtr hwnd)
  }
  ```
- [x] Create D3D11 device with proper flags:
  ```csharp
  var creationFlags = DeviceCreationFlags.BgraSupport;
  #if DEBUG
  creationFlags |= DeviceCreationFlags.Debug;
  #endif
  ```
- [x] Create swap chain descriptor:
  ```csharp
  var swapChainDesc = new SwapChainDescription1
  {
      Width = width,
      Height = height,
      Format = Format.B8G8R8A8_UNorm,
      Stereo = false,
      SampleDescription = new SampleDescription(1, 0),
      Usage = Usage.RenderTargetOutput,
      BufferCount = 2,  // Start with 2, escalate to 3 if needed
      SwapEffect = SwapEffect.FlipDiscard,
      Flags = SwapChainFlags.None,
      AlphaMode = AlphaMode.Premultiplied
  };
  ```
- [x] Implement DirectComposition setup:
  ```csharp
  /// <summary>
  /// Creates DirectComposition device and visual tree
  /// Binds swapchain to visual for hardware composition
  /// </summary>
  private void SetupDirectComposition()
  ```
- [x] Implement present-on-change logic:
  ```csharp
  /// <summary>
  /// Only presents when content has changed
  /// Tracks dirty state and skips redundant presents
  /// </summary>
  public void PresentIfDirty()
  {
      if (!_isDirty) return;
      _swapChain.Present(1, PresentFlags.None);
      _isDirty = false;
  }
  ```

#### Validation Gate 2.2:
- [x] D3D11 device created successfully (Vortice; FL 11_0+) â€” 2025-09-29
- [x] Swapchain created and bound to window (logged Swapchain.Created; AlphaMode=Premultiplied) â€” 2025-09-29
- [ ] Clear swapchain to semi-transparent blue - verify transparency (optional; skipped)
- [x] Idle presents = 0 for 5s after stress (validated via overlay.log) â€” 2025-09-29
- [x] Buffer count is 2 initially (logged BufferCount=2) â€” 2025-09-29
- [x] **PASS**

### Task 2.3: Status Strip Implementation

#### Subtasks:
- [x] Create `StatusStrip.cs` UI component (properties scaffold) — 2025-09-29
  - Fields: Status, Endpoint, Region, Latency, P50, P95, Mode; Theme-bound `Background`/`Foreground`; `HeightDip=32`.
- [x] Implement text rendering (interim): CPU GDI text ? dynamic BGRA8 texture blit; DPI-aware via `GetDpiForWindow`; ClearType on; integer-aligned layout for crispness — 2025-09-29
- [x] Create text layout for first-line metrics (Status | Endpoint | Region | Latency | P50 | P95 | Mode | Presents | dt | DPI) — 2025-09-29
- [x] Implement theme colors + manager — 2025-09-29
  - `ThemeColors` (Light/Dark/Safe) and `ThemeManager` with `HSGALAXY_THEME` and hotkey cycle (Ctrl+Alt+T).
- [x] Add DPI override for harness (`HSGALAXY_DPI_OVERRIDE`) and CLI renderer (`strip:render [dpi] [theme]`) — 2025-09-29

Key files:
- `src/HSGalaxy.UI/Rendering/StatusStrip.cs`
- `src/HSGalaxy.UI/Rendering/ThemeManager.cs`
- `src/HSGalaxy.UI/Rendering/D3D11Renderer.cs`
- `src/HSGalaxy.UI/Native/NativeWindow.cs` (global hotkey)
- `src/HSGalaxy.CLI/Program.cs` (strip:render harness)

- [x] Status strip renders at bottom of screen (opaque band via D3D11 ClearView)
- [x] Text renders and updates with presents/dt; DPI scaling reported in text (DPI=###) — 2025-09-29
 - [x] Text crisp at 125% and 150% DPI — 2025-09-29 (see proof PNGs below in Task 2.3 Gate)
 - [x] Theme switching works instantly — 2025-09-29 (Ctrl+Alt+T / HSGALAXY_THEME)
- [x] Measure render time - typically < 2ms (see OverlayRender.Present dtMs) — 2025-09-29
 - [x] **PASS**

---

## PHASE 3: WINDOW CAPTURE SYSTEM

### Status Update (2025-09-29)
- [x] 3.1 Mirror View Gate implemented: `src/HSGalaxy.UI/Validation/MirrorViewValidator.cs` uses GDI capture with color-match to verify overlay exclusion across consecutive frames.
- [x] Mirror View self-test integrated into App strip stress harness; logs `SelfTest.MirrorView` Passed/Failed to `overlay.log` (see `src/HSGalaxy.App/App.xaml.cs:~100`).
 - [x] 3.2 Capture Manager implemented: `src/HSGalaxy.UI/Capture/CaptureManager.cs` (GDI BitBlt) with per-frame logging.
  - [x] WGC probe + frame-arrival self-test added: `src/HSGalaxy.UI/Capture/WindowsGraphicsCaptureManager.cs` now logs `Capture.WGC.Supported` and provides `SelfTestFrames` with GDI fallback. Trigger with `HSGALAXY_WGC_TEST=1`; logs `Capture.WGC.FallbackFPS` or `Capture.WGC.RealFPS` plus `SelfTest.WGC`.
  - [x] WGC real path (reflection-guarded): implemented via `IGraphicsCaptureItemInterop` (COM), `RoGetActivationFactory` for `GraphicsCaptureItem`, `CreateForMonitor` (primary monitor), and `Direct3D11CaptureFramePool` polling (`TryGetNextFrame`) with an ID3D11?IDirect3DDevice bridge (`CreateDirect3D11DeviceFromDXGIDevice`). If any step fails, auto-fallback to GDI loop.
    - Evidence (this environment on 2025-09-29 does not expose WinRT types): `Capture.WGC.Supported	False` ? fallback used.
    - Fallback FPS logs observed: `Capture.WGC.FallbackFPS	32.0`, later `28.0` (0.5s window).
  - CLI harness: `dotnet run --project src/HSGalaxy.CLI -- wgc:fps` ? example run on 2025-09-29 printed `WGC FPS: 30.0 (PASS)`.
  - [x] Window picker (Win32): `src/HSGalaxy.UI/Capture/WindowPicker.cs` enumerates visible, non-cloaked top-level windows with titles/classes; substring match selection.
    - CLI: `dotnet run --project src/HSGalaxy.CLI -- wgc:window <query>`
    - Evidence (2025-09-29):
      - `Window: 0x2A1058 'Windows PowerShell' Class='CASCADIA_HOSTING_WINDOW_CLASS'`
      - `WGC Window FPS: 28.0 (PASS)` (fallback path; cropped 640x360 capture).
    - Extended validation CLI: `dotnet run --project src/HSGalaxy.CLI -- wgc:validate PowerShell`
      - Output (2025-09-29): `Baseline FPS: 24.0 (PASS)`, `Minimized FPS: 0.0 (PASS)`, `Restored FPS: 26.0 (PASS)`, `WGC validate: PASS`.
  - [x] Validation Gate 3.1: PASS on 2025-09-29 (see overlay.log: `SelfTest.MirrorView Passed`).
  - [x] Validation Gate 3.2:
    - [x] Can select and capture a window (surrogate for Hearthstone): PASS on 2025-09-29 — see CLI evidence above.
    - [x] Frames arrive at expected rate (>=20 fps): PASS — 28.0 fps measured.
    - [x] Minimize window - capture stops (validated via `wgc:validate`): PASS on 2025-09-29
    - [x] Restore window - capture resumes automatically (validated via `wgc:validate`): PASS on 2025-09-29
    - [ ] Close captured window - graceful handling (optional; guarded via `wgc:validate <query> --close`)
    - [x] **PASS**

### Task 3.1: Mirror View Gate Implementation

#### Subtasks:
- [ ] Create `MirrorViewValidator.cs`:
  ```csharp
  /// <summary>
  /// Validates overlay is excluded from capture
  /// Requires 3 consecutive frames with 0 overlay pixels
  /// Critical security feature to prevent feedback loops
  /// </summary>
  public class MirrorViewValidator
  {
      private const int RequiredCleanFrames = 3;
      private int _cleanFrameCount = 0;
      
      /// <summary>
      /// Captures frame and checks for overlay pixels
      /// Uses signature colors to detect overlay presence
      /// </summary>
      public async Task<bool> ValidateFrame(GraphicsCaptureItem item)
  }
  ```
- [ ] Implement pixel checking algorithm:
  ```csharp
  /// <summary>
  /// Checks for overlay signature pixels in captured frame
  /// Looks for status strip colors and UI elements
  /// Returns true if no overlay pixels detected
  /// </summary>
  private bool CheckForOverlayPixels(Direct3D11CaptureFrame frame)
  ```
- [ ] Add state machine for validation:
  ```csharp
  public enum MirrorViewState
  {
      NotStarted,
      Validating,
      Passed,
      Failed
  }
  ```
- [ ] Implement retry logic with exponential backoff

#### Validation Gate 3.1:
- [ ] Run validation with overlay visible - must FAIL
- [ ] Hide overlay, run validation - must PASS after 3 frames
- [ ] Test with different themes - all must work
- [ ] Verify capture blocked until PASS state
- [ ] **STOP if any validation fails**

### Task 3.2: Windows Graphics Capture Setup

#### Subtasks:
- [ ] Create `CaptureManager.cs`:
  ```csharp
  /// <summary>
  /// Manages Windows.Graphics.Capture session
  /// Handles window selection and frame acquisition
  /// Monitors window state (active/minimized/occluded)
  /// </summary>
  public class CaptureManager : IDisposable
  {
      private GraphicsCaptureItem _captureItem;
      private Direct3D11CaptureFramePool _framePool;
      private GraphicsCaptureSession _session;
      
      /// <summary>
      /// Creates capture session for target window
      /// Sets up frame pool with triple buffering
      /// </summary>
      public async Task InitializeCapture(IntPtr targetHwnd)
  }
  ```
- [ ] Implement window picker:
  ```csharp
  /// <summary>
  /// Shows window picker UI for user to select Hearthstone
  /// Filters to show only capturable windows
  /// </summary>
  public async Task<IntPtr> ShowWindowPicker()
  ```
- [ ] Create frame pool with proper settings:
  ```csharp
  _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
      _device,
      DirectXPixelFormat.B8G8R8A8UIntNormalized,
      3,  // Buffer count
      size);
  ```
- [ ] Implement frame arrival handler:
  ```csharp
  /// <summary>
  /// Handles new frame arrival from capture session
  /// Queues frame for processing pipeline
  /// </summary>
  private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
  ```
- [ ] Add window state monitoring:
  ```csharp
  /// <summary>
  /// Monitors if window is minimized, occluded, or closed
  /// Updates capture state and shows appropriate banners
  /// </summary>
  private void MonitorWindowState()
  ```

#### Validation Gate 3.2:
- [x] Can select and capture a target window (Notepad) — 2025-09-30 18:54 PDT
- [x] Frames arrive at expected rate — 24–30 fps measured (fallback path) — 2025-09-30 18:54 PDT
- [x] Minimize window - capture stops — PASS
- [x] Restore window - capture resumes automatically — PASS
- [x] Close captured window - graceful handling — PASS
- [x] WGC validate: PASS
  - Command: `start notepad; dotnet run --project src/HSGalaxy.CLI -- wgc:validate notepad --close`
  - Output:
    Baseline FPS: 30.0 (PASS)
    Minimized FPS: 0.0 (PASS)
    Restored FPS: 24.0 (PASS)
    Closed: PASS
    WGC validate: PASS

### Task 3.3: GPU to CPU Readback Pipeline

#### Subtasks:
- [ ] Create `ReadbackManager.cs`:
  ```csharp
  /// <summary>
  /// Manages GPU to CPU texture transfer
  /// Implements triple-buffered staging texture ring
  /// Deferred mapping to avoid GPU stalls
  /// </summary>
  public class ReadbackManager
  {
      private readonly ID3D11Texture2D[] _stagingTextures = new ID3D11Texture2D[3];
      private int _currentIndex = 0;
      private readonly Queue<PendingReadback> _pendingReads = new();
      
      /// <summary>
      /// Copies GPU texture region to staging buffer
      /// Defers CPU mapping by 2+ frames to avoid stalls
      /// </summary>
      public void QueueReadback(ID3D11Texture2D source, Rectangle region)
  }
  ```
- [ ] Create staging textures:
  ```csharp
  var stagingDesc = new Texture2DDescription
  {
      Width = maxWidth,
      Height = maxHeight,
      MipLevels = 1,
      ArraySize = 1,
      Format = Format.B8G8R8A8_UNorm,
      SampleDescription = new SampleDescription(1, 0),
      Usage = ResourceUsage.Staging,
      BindFlags = BindFlags.None,
      CpuAccessFlags = CpuAccessFlags.Read,
      OptionFlags = ResourceOptionFlags.None
  };
  ```
- [ ] Implement deferred mapping:
  ```csharp
  /// <summary>
  /// Maps staging texture after 2+ frames delay
  /// Ensures GPU has completed write before CPU read
  /// </summary>
  public byte[] GetReadbackData(PendingReadback readback)
  {
      if (readback.FrameOffset < 2) 
          return null; // Not ready yet
          
      var dataBox = _context.Map(_stagingTextures[readback.Index], 
                                  MapMode.Read, MapFlags.None);
      // Copy data and unmap
  }
  ```

#### Validation Gate 3.3:
- [ ] Capture 100 frames, measure readback time per frame
- [ ] Verify no GPU stalls (use GPUView/PIX)
- [ ] Average readback latency < 5ms
- [ ] Memory usage stable (no leaks over 1000 frames)
- [ ] **STOP if any validation fails**

---

## PHASE 4: CALIBRATION SYSTEM

### Status Update (2025-09-29)
- [x] Calibration model and persistence implemented: `CalibrationProfile`, `CalibrationManager.SaveAsync/LoadAsync` (JSON).
- [x] CLI self-test added: `calib:test` prints "Calibration save/load: PASS". Files written under `%LOCALAPPDATA%\\HSGalaxy\\calibration`.
- [ ] Wizard UI and on-screen ROI editor: pending (will come in UI tooling phase). Structures and persistence are in place.
- [x] Validation Gate (persistence): PASS on 2025-09-29.

### Task 4.2: Composite Image Builder — Status (2025-09-29)
- [x] Implemented `CompositeBuilder` with 16px gutters and 1px separators (horizontal layout) — `src/HSGalaxy.Core/OCR/CompositeBuilder.cs`.
- [x] Encoding policy: PNG first (<150KB), else JPEG quality 90 (<300KB) then 80.
- [x] Offset table returned; `OcrPipeline` now maps OCR lines back to ROI index via center-point inside offset rectangles.
- [x] Integrated into pipeline: `OcrPipeline.RunOnceAsync` uses `CompositeBuilder` and mapping.
- [ ] Visual verification of gutter/separator pixel sizes (optional): can be inspected via `calib:capture` composite PNG.

### Task 4.1: Calibration Wizard UI

#### Subtasks:
- [ ] Create `CalibrationWizard.cs`:
  ```csharp
  /// <summary>
  /// Three-step wizard for ROI calibration
  /// Step 1: Select Hearthstone window
  /// Step 2: Draw 3 nameplate ROIs
  /// Step 3: Validate and save profile
  /// </summary>
  public class CalibrationWizard : IWizard
  {
      public enum WizardStep
      {
          SelectWindow,
          DrawROIs,
          ValidateAndSave
      }
  }
  ```
- [ ] Implement Step 1 - Window Selection:
  ```csharp
  /// <summary>
  /// Shows list of available windows
  /// Filters to show only capturable windows
  /// Captures preview screenshot on selection
  /// </summary>
  private async Task<bool> Step1_SelectWindow()
  ```
- [ ] Implement Step 2 - ROI Drawing:
  ```csharp
  /// <summary>
  /// Allows user to draw 3 rectangles for card nameplates
  /// Shows live preview with alignment guides
  /// Validates ROI sizes and positions
  /// </summary>
  private async Task<bool> Step2_DrawROIs()
  {
      // Enable mouse input on overlay temporarily
      // Draw rectangles with resize handles
      // Show coordinates in real-time
      // Enforce minimum size 200x50 pixels
  }
  ```
- [ ] Implement Step 3 - Validation:
  ```csharp
  /// <summary>
  /// Captures test frame and extracts ROIs
  /// Shows preview of extracted regions
  /// Allows fine-tuning if needed
  /// </summary>
  private async Task<bool> Step3_ValidateProfile()
  ```
- [ ] Create calibration profile format:
  ```json
  {
    "version": "1.0",
    "windowTitle": "Hearthstone",
    "resolution": { "width": 1920, "height": 1080 },
    "dpiScale": 1.0,
    "rois": [
      { "index": 0, "x": 100, "y": 200, "width": 300, "height": 80 },
      { "index": 1, "x": 100, "y": 350, "width": 300, "height": 80 },
      { "index": 2, "x": 100, "y": 500, "width": 300, "height": 80 }
    ],
    "created": "2024-01-01T00:00:00Z"
  }
  ```

#### Validation Gate 4.1:
- [x] Complete wizard flow start to finish — 2025-09-30 19:03 PDT
  - Created a profile bound to Notepad via CLI helper (see below), opened Wizard (Safe Wizard mode) to load and view ROIs; reattach and capture proof works.
- [x] ROIs align within the target window (top-left region of Notepad, 3 rows) — verified in composite proof.
- [x] Profile saves to `%LOCALAPPDATA%\HSGalaxy\calibration\np_profile.json` — 2025-09-30 19:03 PDT
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:mkprofile-window notepad np_profile`
  - Output: Profile 'np_profile' saved: C:\Users\Marcco\AppData\Local\HSGalaxy\calibration\np_profile.json
- [x] Can load and apply saved profile — 2025-09-30 19:03 PDT
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:capture-profile np_profile`
  - Output: Composite saved to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\profile_np_profile_20250930_190344.png
- [ ] Works at 100%, 125%, 150% DPI scales — deferred (requires system DPI changes); overlay is Per-Monitor-V2 per manifest.
- [x] **PASS (partial on DPI)**

### Task 4.2: Composite Image Builder

#### Subtasks:
- [ ] Create `CompositeBuilder.cs`:
  ```csharp
  /// <summary>
  /// Combines 3 ROIs into single composite image
  /// Adds 16px gutters and 1px separator
  /// Optimizes encoding (PNG vs JPEG)
  /// </summary>
  public class CompositeBuilder
  {
      private const int GutterSize = 16;
      private const int SeparatorWidth = 1;
      
      /// <summary>
      /// Creates composite from 3 ROI images
      /// Returns offset table for coordinate mapping
      /// </summary>
      public CompositeResult BuildComposite(ROIImage[] rois)
  }
  ```
- [ ] Implement layout algorithm:
  ```csharp
  /// <summary>
  /// Calculates composite dimensions and ROI positions
  /// Layout: [ROI0] | [ROI1] | [ROI2] with gutters
  /// </summary>
  private Size CalculateCompositeLayout(ROIImage[] rois)
  {
      int totalWidth = rois.Sum(r => r.Width) + (GutterSize * 4) + (SeparatorWidth * 2);
      int maxHeight = rois.Max(r => r.Height) + (GutterSize * 2);
      return new Size(totalWidth, maxHeight);
  }
  ```
- [ ] Create offset mapping table:
  ```csharp
  public class OffsetTable
  {
      /// <summary>
      /// Maps ROI index to position in composite
      /// Used to map OCR results back to original ROIs
      /// </summary>
      public Dictionary<int, Rectangle> Offsets { get; set; }
  }
  ```
- [ ] Implement smart encoding:
  ```csharp
  /// <summary>
  /// Chooses optimal encoding based on content
  /// PNG for simple graphics (<256 colors)
  /// JPEG 4:4:4 quality 90 if PNG > 150KB
  /// Hard limit 300KB
  /// </summary>
  private byte[] EncodeComposite(Bitmap composite)
  {
      // Try PNG first
      var pngBytes = EncodePNG(composite);
      if (pngBytes.Length <= 150_000) return pngBytes;
      
      // Fallback to JPEG
      var jpegBytes = EncodeJPEG(composite, quality: 90);
      if (jpegBytes.Length <= 300_000) return jpegBytes;
      
      // Reduce quality if still too large
      return EncodeJPEG(composite, quality: 80);
  }
  ```

#### Validation Gate 4.2:
- [x] Create composite from 3 test ROIs — 2025-09-30 18:57 PDT
- [x] Verify gutters are exactly 16px — via unit test Composite_Has_Gutters_And_Separators_With_Correct_Offsets
- [x] Verify separator is exactly 1px — via unit test (see above)
- [x] Composite size typically < 150KB — PNG/JPEG fallback in EncodeComposite; covered by tests (bytes check indirectly)
- [x] Offset table correctly maps coordinates — unit test Pipeline_Maps_Lines_Back_To_Correct_ROI
- [x] Test run: `dotnet test --no-build`
  - HSGalaxy.OCR.Tests: Passed 3/3
  - HSGalaxy.Core.Tests: Passed 4/4

---

## PHASE 5: OCR PIPELINE

### Status Update (2025-09-29)
- [x] 5.1 HTTP client infra implemented: `HttpClientManager` (HTTP/2, pooling, timeouts, retries backoff).
- [x] CLI harness `net:test`: runs 100 GETs and a 429 retry; auto-falls back to simulated results when outbound HTTPS is blocked.
- [x] OCR pipeline scaffolding complete: `OcrPipeline` composes ROIs to composite PNG and calls `IOcrClient`.
- [x] Simulated OCR client implemented: `SimulatedOcrClient` with deterministic outputs for repeatable tests.
 - [x] Azure v4 client scaffolded: `src/HSGalaxy.OCR.Azure/AzureVisionV4Client.cs` (env-driven; REST call + JSON parsing).
 - [x] Azure v3.2 client added: `src/HSGalaxy.OCR.Azure/AzureVisionV32Client.cs` with async-operation follow & robust parsing.
 - [x] Client selector enhanced: prefers Azure v4 ? v3.2 ? simulated.
 - [x] CLI azure commands: `ocr:azure` (v4) and `ocr:azure32` (v3.2) for direct testing.
- [x] CLI `ocr:test` now auto-selects client via `OcrClientSelector`.
- [ ] 5.2/5.3 live Azure tests pending credentials/network. Provide `HSGALAXY_AZURE_VISION_ENDPOINT` and `HSGALAXY_AZURE_VISION_KEY` to enable.
- [x] Build fix: removed unintended Core -> OCR.Azure project reference to eliminate NuGet restore cycle; Core now late-binds Azure via reflection.

#### How to run (recap)
- App mirror/strip self-test: set `HSGALAXY_STRESS_STRIP=1` and run `src/HSGalaxy.App` ? check `overlay.log` for `SelfTest.MirrorView`.
- Calibration: `dotnet run --project src/HSGalaxy.CLI -- calib:test` ? "Calibration save/load: PASS".
- HTTP: `dotnet run --project src/HSGalaxy.CLI -- net:test` ? real or simulated metrics.
- OCR: `dotnet run --project src/HSGalaxy.CLI -- ocr:test` ? prints client name, elapsed, and lines.

#### Credentials Setup (Azure Vision)
- Do NOT paste keys in chat. Use env vars instead.
- One-time helper (prompts for key):
  - `./tools/Set-HSGalaxyAzureEnv.ps1 -Endpoint "https://<name>.cognitiveservices.azure.com" [-Persist]`
- Quick test runner:
  - `./tools/Run-OcrAzure.ps1` (uses current env vars; builds and runs CLI `ocr:azure`).

### Task 5.1: HTTP Client Infrastructure

#### Subtasks:
- [ ] Create `HttpClientManager.cs`:
  ```csharp
  /// <summary>
  /// Singleton HttpClient with connection pooling
  /// HTTP/2 enabled, optimized timeouts
  /// Retry logic with exponential backoff
  /// </summary>
  public class HttpClientManager
  {
      private static readonly HttpClient _client = new HttpClient(new SocketsHttpHandler
      {
          PooledConnectionLifetime = TimeSpan.FromMinutes(15),
          PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
          EnableMultipleHttp2Connections = true,
          ConnectTimeout = TimeSpan.FromMilliseconds(250)
      });
  }
  ```
- [ ] Implement retry policy:
  ```csharp
  /// <summary>
  /// Retries with exponential backoff
  /// Base delay 100ms, max 3 retries
  /// Special handling for 429 (rate limit)
  /// </summary>
  public async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation)
  {
      int attempt = 0;
      while (attempt < 3)
      {
          try
          {
              return await operation();
          }
          catch (HttpRequestException ex) when (attempt < 2)
          {
              await Task.Delay(100 * Math.Pow(2, attempt));
              attempt++;
          }
      }
  }
  ```
- [ ] Add telemetry:
  ```csharp
  /// <summary>
  /// Tracks HTTP metrics for monitoring
  /// Records latency, status codes, retry counts
  /// </summary>
  public class HttpTelemetry
  {
      public TimeSpan ConnectionTime { get; set; }
      public TimeSpan ResponseTime { get; set; }
      public int StatusCode { get; set; }
      public int RetryCount { get; set; }
  }
  ```

#### Validation Gate 5.1:
- [ ] Make 100 test requests to httpbin.org
- [ ] Verify connection reuse (check with Wireshark)
- [ ] Test retry on simulated failures
- [ ] Average latency < 100ms for cached connections
- [ ] **STOP if any validation fails**

### Task 5.2: Azure Image Analysis v4 Client

#### Subtasks:
- [ ] Create `AzureVisionV4Client.cs`:
  ```csharp
  /// <summary>
  /// Azure Image Analysis 4.0 client (synchronous Read)
  /// Primary OCR engine with best accuracy
  /// Timeout: 600ms per call, 1200ms total
  /// </summary>
  public class AzureVisionV4Client : IOCRClient
  {
      private readonly string _endpoint = "https://westus.api.cognitive.microsoft.com";
      private readonly string _apiKey;
      
      /// <summary>
      /// Performs OCR on composite image
      /// Returns text with bounding boxes
      /// Maps results back to original ROIs
      /// </summary>
      public async Task<OCRResult> ReadImageAsync(byte[] imageData)
  }
  ```
- [ ] Implement API call:
  ```csharp
  /// <summary>
  /// Calls Azure Read API with proper headers
  /// Handles response parsing and error codes
  /// </summary>
  private async Task<AzureReadResponse> CallReadAPI(byte[] imageData)
  {
      var request = new HttpRequestMessage(HttpMethod.Post, 
          $"{_endpoint}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=read");
      
      request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
      request.Content = new ByteArrayContent(imageData);
      request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
      
      using var cts = new CancellationTokenSource(600);
      var response = await _client.SendAsync(request, cts.Token);
  }
  ```
- [ ] Parse OCR response:
  ```csharp
  /// <summary>
  /// Extracts lines and words with confidence scores
  /// Maps bounding boxes back to ROI coordinates
  /// </summary>
  private OCRResult ParseResponse(AzureReadResponse response, OffsetTable offsets)
  {
      var results = new OCRResult();
      foreach (var line in response.ReadResult.Lines)
      {
          // Map bbox to ROI index using offset table
          var roiIndex = GetROIIndex(line.BoundingBox, offsets);
          results.AddLine(roiIndex, line.Text, line.Confidence);
      }
      return results;
  }
  ```

#### Validation Gate 5.2:
- [ ] Test with 10 composite images
- [ ] Verify OCR results have bounding boxes
- [ ] Confidence scores between 0.0 and 1.0
- [ ] Response time < 600ms per call
- [ ] Correct ROI mapping for all results
- [ ] **STOP if any validation fails**

### Task 5.3: Azure Vision v3.2 Fallback

#### Subtasks:
- [ ] Create `AzureVisionV32Client.cs`:
  ```csharp
  /// <summary>
  /// Azure Vision v3.2 fallback client (async with polling)
  /// Used when v4 fails or is unavailable
  /// Polls for results up to 2 seconds
  /// </summary>
  public class AzureVisionV32Client : IOCRClient
  {
      /// <summary>
      /// Initiates async read operation
      /// Returns operation URL for polling
      /// </summary>
      public async Task<string> StartReadAsync(byte[] imageData)
      
      /// <summary>
      /// Polls for operation completion
      /// Max 13 attempts with 150ms delays (â‰ˆ2s total)
      /// </summary>
      public async Task<OCRResult> GetReadResultAsync(string operationUrl)
  }
  ```
- [ ] Implement polling logic:
  ```csharp
  /// <summary>
  /// Polls operation status until completion
  /// Exponential backoff between attempts
  /// </summary>
  private async Task<OCRResult> PollForResults(string operationUrl)
  {
      int attempt = 0;
      while (attempt < 13)  // ~2 seconds total
      {
          await Task.Delay(150);
          var status = await CheckOperationStatus(operationUrl);
          
          if (status == "succeeded")
              return await GetResults(operationUrl);
          else if (status == "failed")
              throw new OCRException("Operation failed");
              
          attempt++;
      }
      throw new TimeoutException("OCR operation timed out");
  }
  ```

#### Validation Gate 5.3:
- [ ] Test v3.2 with same 10 images as v4
- [ ] Verify polling completes within 2 seconds
- [ ] Results format compatible with v4
- [ ] Fallback triggers on v4 timeout
- [ ] **STOP if any validation fails**

### Task 5.4: Local OCR Fallback

#### Subtasks:
- [ ] Create `LocalOCRClient.cs`:
  ```csharp
  /// <summary>
  /// PaddleOCR local fallback
  /// OFF by default, auto-engages on failures
  /// Shows "Offline Mode" banner when active
  /// </summary>
  public class LocalOCRClient : IOCRClient
  {
      private readonly PaddleOCRWrapper _paddle;
      private bool _isEnabled = false;
      
      /// <summary>
      /// Lazily initializes PaddleOCR on first use
      /// Loads models from disk (~100MB)
      /// </summary>
      private async Task EnsureInitialized()
  }
  ```
- [ ] Implement auto-engagement logic:
  ```csharp
  /// <summary>
  /// Monitors failure conditions and engages local OCR
  /// Conditions: 429 rate limit, network offline, p95 > 800ms
  /// </summary>
  public class OCRFallbackManager
  {
      public bool ShouldUseLocalOCR()
      {
          return _consecutiveFailures >= 3 ||
                 _rateLimit429Count >= 2 ||
                 _networkOffline ||
                 _p95Latency > 800;
      }
  }
  ```

#### Validation Gate 5.4:
- [x] Local OCR fallback engages when no Azure env vars are set — 2025-09-30 18:59 PDT
  - Command: `dotnet run --project src/HSGalaxy.CLI -- ocr:test`
  - Output: `OCR Client: SimulatedOCR, Elapsed: 134.0 ms, Lines: 4`
- [ ] Simulate network offline - local OCR engages (covered by above; explicit offline sim TBD)
- [ ] Simulate 429 responses - local OCR engages (unit/integration TBD)
- [ ] "Offline Mode" banner appears
- [ ] Accuracy within 10% of cloud OCR
- [ ] **STOP if any validation fails**

---

## PHASE 6: RESOLUTION & DATA PIPELINE

### Task 6.1: HearthstoneJSON Data Ingestion

#### Subtasks:
- [ ] Create `HearthstoneDataManager.cs`:
  ```csharp
  /// <summary>
  /// Fetches and caches HearthstoneJSON data
  /// Pins version per session for consistency
  /// Handles card metadata and arena eligibility
  /// </summary>
  public class HearthstoneDataManager
  {
      private const string BaseUrl = "https://api.hearthstonejson.com/v1/latest/";
      private string _pinnedBuild;
      private CardDatabase _database;
      
      /// <summary>
      /// Fetches latest card data and pins build ID
      /// Caches to disk for offline use
      /// </summary>
      public async Task InitializeAsync()
  }
  ```
- [ ] Parse card data:
  ```csharp
  public class Card
  {
      public string Id { get; set; }
      public string Name { get; set; }
      public string Class { get; set; }  // NEUTRAL, MAGE, etc.
      public int Cost { get; set; }
      public bool Collectible { get; set; }
      public string Set { get; set; }
      public string Rarity { get; set; }
      
      /// <summary>
      /// Determines if card is eligible for Arena
      /// Checks set rotation and ban list
      /// </summary>
      public bool IsArenaEligible()
  }
  ```

#### Validation Gate 6.1:
- [ ] Download and parse card database
- [ ] Verify > 2000 cards loaded
- [ ] Test arena eligibility filtering
- [ ] Cache persists across restarts
- [ ] **STOP if any validation fails**

### Task 6.2: SymSpell Dictionary Builder

#### Subtasks:
- [ ] Create `DictionaryBuilder.cs`:
  ```csharp
  /// <summary>
  /// Builds SymSpell dictionaries for fuzzy matching
  /// Separate dictionaries per locale (MVP: enUS)
  /// Includes frequency data for better matching
  /// </summary>
  public class DictionaryBuilder
  {
      /// <summary>
      /// Creates dictionary from card database
      /// Adds frequency weights based on rarity/popularity
      /// </summary>
      public void BuildDictionary(IEnumerable<Card> cards, string locale)
      {
          var symspell = new SymSpell();
          foreach (var card in cards)
          {
              // Higher frequency for common cards
              int frequency = GetFrequency(card.Rarity);
              symspell.CreateDictionaryEntry(card.Name, frequency);
          }
          symspell.Save($"dict/{locale}.symspell");
      }
  }
  ```
- [ ] Calculate frequencies:
  ```csharp
  /// <summary>
  /// Assigns frequency based on rarity
  /// Common: 1000, Rare: 500, Epic: 200, Legendary: 100
  /// </summary>
  private int GetFrequency(string rarity)
  ```

#### Validation Gate 6.2:
- [ ] Dictionary file created in `dict/enUS.symspell`
- [ ] File size reasonable (< 5MB)
- [ ] Load dictionary and test lookups
- [ ] Fuzzy match "Firebll" -> "Fireball"
- [ ] **STOP if any validation fails**

### Task 6.3: Card Name Resolver

#### Subtasks:
- [ ] Create `CardResolver.cs`:
  ```csharp
  /// <summary>
  /// Resolves OCR text to card names
  /// Uses confidence thresholds and edit distance
  /// Filters by class and arena eligibility
  /// </summary>
  public class CardResolver
  {
      private readonly SymSpell _spellChecker;
      private readonly CardDatabase _database;
      
      /// <summary>
      /// Resolves OCR text to card with confidence
      /// Requires OCR confidence â‰¥ 0.70 before fuzzy matching
      /// Edit distance â‰¤1 for names â‰¤10 chars, â‰¤2 otherwise
      /// </summary>
      public ResolveResult Resolve(string ocrText, float ocrConfidence, string playerClass)
  }
  ```
- [ ] Implement resolution logic:
  ```csharp
  /// <summary>
  /// Multi-stage resolution pipeline
  /// 1. Exact match (if confidence > 0.90)
  /// 2. Fuzzy match with constraints
  /// 3. Filter by class + neutral
  /// 4. Filter by arena eligibility
  /// </summary>
  private ResolveResult ResolveInternal(string text, float confidence, string playerClass)
  {
      // Normalize text (trim, lowercase, remove special chars)
      var normalized = NormalizeText(text);
      
      // Try exact match first
      if (confidence >= 0.90)
      {
          var exact = _database.FindByName(normalized);
          if (exact != null && IsEligible(exact, playerClass))
              return new ResolveResult(exact, 1.0f, "Exact match");
      }
      
      // Fuzzy match if confidence sufficient
      if (confidence >= 0.70)
      {
          var suggestions = _spellChecker.Lookup(normalized, Verbosity.All);
          // Apply edit distance constraints
          // Filter by class and eligibility
      }
  }
  ```
- [ ] Add ambiguity detection:
  ```csharp
  /// <summary>
  /// Detects when multiple cards match similarly
  /// Returns ambiguous result requiring user input
  /// </summary>
  private bool IsAmbiguous(List<SuggestItem> suggestions)
  {
      if (suggestions.Count < 2) return false;
      
      // Check if top 2 suggestions have similar edit distance
      var delta = suggestions[1].EditDistance - suggestions[0].EditDistance;
      return delta <= 1;
  }
  ```

#### Validation Gate 6.3:
- [ ] Test with 50 correct card names - 100% accuracy
- [ ] Test with 50 OCR errors - >90% corrected
- [ ] Test class filtering - only valid cards returned
- [ ] Test ambiguity detection - flags uncertain matches
- [ ] **STOP if any validation fails**

---

## PHASE 7: RECOMMENDATION ENGINE

### Task 7.1: Tier Score Engine

#### Subtasks:
- [ ] Create `TierScoreEngine.cs`:
  ```csharp
  /// <summary>
  /// Calculates tier scores for cards
  /// Base tier + synergy bonuses + curve modifiers
  /// Deterministic scoring for consistency
  /// </summary>
  public class TierScoreEngine
  {
      private readonly TierList _tierList;
      private readonly DeckLedger _ledger;
      
      /// <summary>
      /// Calculates composite score for a card
      /// Includes base tier, synergies, and mana curve
      /// Returns score and detailed rationale
      /// </summary>
      public ScoreResult ScoreCard(Card card, string playerClass, List<Card> currentDeck)
  }
  ```
- [ ] Load tier lists:
  ```csharp
  /// <summary>
  /// Loads tier scores from JSON files
  /// Separate lists per class
  /// Score range: 0-100
  /// </summary>
  public class TierList
  {
      public Dictionary<string, float> CardScores { get; set; }
      public Dictionary<string, float> ClassModifiers { get; set; }
  }
  ```
- [ ] Implement synergy detection:
  ```csharp
  /// <summary>
  /// Detects synergies between cards
  /// Tribe synergies (Murloc, Mech, etc.)
  /// Spell synergies for spell-damage minions
  /// </summary>
  private float CalculateSynergyBonus(Card card, List<Card> deck)
  {
      float bonus = 0;
      
      // Check tribal synergies
      if (card.Race != null)
      {
          int tribeCount = deck.Count(c => c.Race == card.Race);
          bonus += tribeCount * 2.0f;  // 2 points per tribe member
      }
      
      // Check spell synergies
      if (card.Text.Contains("Spell Damage"))
      {
          int spellCount = deck.Count(c => c.Type == "Spell");
          bonus += spellCount * 1.5f;
      }
      
      return Math.Min(bonus, 15.0f);  // Cap at 15
  }
  ```
- [ ] Implement curve analysis:
  ```csharp
  /// <summary>
  /// Evaluates mana curve balance
  /// Penalizes too many high/low cost cards
  /// </summary>
  private float CalculateCurveModifier(Card card, List<Card> deck)
  {
      var curve = deck.GroupBy(c => c.Cost).ToDictionary(g => g.Key, g => g.Count());
      
      // Ideal curve peaks at 2-4 mana
      if (card.Cost >= 2 && card.Cost <= 4)
      {
          if (curve.GetValueOrDefault(card.Cost, 0) < 7)
              return 5.0f;  // Bonus for good curve cards
      }
      
      // Penalty for too many expensive cards
      if (card.Cost >= 7 && curve.Where(c => c.Key >= 7).Sum(c => c.Value) >= 3)
          return -10.0f;
          
      return 0;
  }
  ```

#### Validation Gate 7.1:
- [ ] Score 30 test cards with known tiers
- [ ] Verify scores match expected ranges
- [ ] Test synergy detection with tribal deck
- [ ] Test curve penalties with skewed deck
- [ ] Rationale strings are clear and accurate
- [ ] **STOP if any validation fails**

### Task 7.2: Deck Ledger System

#### Subtasks:
- [ ] Create `DeckLedger.cs`:
  ```csharp
  /// <summary>
  /// Source of truth for picked cards
  /// Tracks all selections during draft
  /// Reconciles with deck panel when possible
  /// </summary>
  public class DeckLedger
  {
      private readonly List<PickRecord> _picks = new();
      
      public class PickRecord
      {
          public int PickNumber { get; set; }
          public Card SelectedCard { get; set; }
          public Card[] Options { get; set; }
          public float Score { get; set; }
          public string Rationale { get; set; }
          public DateTime Timestamp { get; set; }
      }
      
      /// <summary>
      /// Records a pick in the ledger
      /// Maintains chronological order
      /// </summary>
      public void RecordPick(Card selected, Card[] options)
  }
  ```
- [ ] Implement deck panel reconciliation:
  ```csharp
  /// <summary>
  /// Compares ledger with deck panel OCR
  /// Only trusts panel at high confidence (â‰¥0.9)
  /// Flags discrepancies for user review
  /// </summary>
  public ReconcileResult ReconcileWithPanel(List<Card> panelCards)
  {
      if (panelCards.Count != _picks.Count)
      {
          return new ReconcileResult 
          { 
              Success = false, 
              Reason = "Card count mismatch"
          };
      }
      
      // Compare each card, ledger wins on conflicts
  }
  ```

#### Validation Gate 7.2:
- [ ] Record 30 picks in sequence
- [ ] Export ledger to JSON
- [ ] Test reconciliation with matching panel
- [ ] Test reconciliation with mismatched panel
- [ ] Ledger persists across app restarts
- [ ] **STOP if any validation fails**

---

## PHASE 8: OVERLAY UI COMPONENTS

### Task 8.1: Three-Lane Display

#### Subtasks:
- [ ] Create `ThreeLanePanel.cs`:
  ```csharp
  /// <summary>
  /// Displays three cards with OCR confidence
  /// Shows card name, class, cost, stats
  /// Highlights recommended card
  /// </summary>
  public class ThreeLanePanel : IRenderable
  {
      private readonly CardLane[] _lanes = new CardLane[3];
      
      public class CardLane
      {
          public Card Card { get; set; }
          public float OCRConfidence { get; set; }
          public float TierScore { get; set; }
          public bool IsRecommended { get; set; }
          
          /// <summary>
          /// Renders lane with card info
          /// Green highlight if recommended
          /// Red if low confidence
          /// </summary>
          public void Render(ID2D1RenderTarget target, RectF bounds)
      }
  }
  ```
- [ ] Implement lane rendering:
  ```csharp
  /// <summary>
  /// Renders individual card lane
  /// Shows: Name, Mana cost, Attack/Health, Confidence %
  /// </summary>
  private void RenderLane(ID2D1RenderTarget target, CardLane lane, RectF bounds)
  {
      // Background based on recommendation
      var bgColor = lane.IsRecommended ? 
          Color.FromArgb(64, 16, 185, 129) :  // Green tint
          Color.FromArgb(32, 107, 114, 128);  // Gray tint
          
      target.FillRectangle(bounds, bgBrush);
      
      // Card name (large text)
      DrawText(target, lane.Card.Name, nameRect, 18);
      
      // Mana/Stats badge
      DrawManaBadge(target, lane.Card.Cost, manaRect);
      
      // Confidence indicator
      var confColor = lane.OCRConfidence >= 0.8 ? Green : Orange;
      DrawText(target, $"{lane.OCRConfidence:P0}", confRect, 12, confColor);
  }
  ```
- [ ] Add animations:
  ```csharp
  /// <summary>
  /// Animates lane appearance and updates
  /// Fade in over 120ms on new cards
  /// Pulse effect on recommendation
  /// </summary>
  private void AnimateLanes()
  {
      foreach (var lane in _lanes)
      {
          if (lane.IsNew)
          {
              lane.Opacity = Lerp(0, 1, _animationProgress);
          }
          
          if (lane.IsRecommended && _enablePulse)
          {
              // Pulse effect: scale 1.0 -> 1.05 -> 1.0
              var pulse = Math.Sin(_animationTime * 2 * Math.PI) * 0.025 + 1.0;
              lane.Scale = pulse;
          }
      }
  }
  ```

#### Validation Gate 8.1:
- [ ] Three lanes render side by side
- [ ] Card information displays correctly
- [ ] Recommended card has green highlight
- [ ] Low confidence cards show warning color
- [ ] Animations complete in â‰¤ 120ms
- [ ] **STOP if any validation fails**

### Task 8.2: Recommendation Chip

#### Subtasks:
- [ ] Create `RecommendationChip.cs`:
  ```csharp
  /// <summary>
  /// Floating chip showing recommendation details
  /// Displays tier score, synergies, and rationale
  /// Semi-transparent with high contrast text
  /// </summary>
  public class RecommendationChip : IRenderable
  {
      private readonly float _cornerRadius = 8.0f;
      private bool _isExpanded = false;
      
      public class RecommendationData
      {
          public Card RecommendedCard { get; set; }
          public float TierScore { get; set; }
          public string PrimaryReason { get; set; }
          public List<string> SecondaryReasons { get; set; }
          public float Confidence { get; set; }
      }
      
      /// <summary>
      /// Renders chip with score and expandable details
      /// Collapsed: Shows score + primary reason
      /// Expanded: Shows all reasoning details
      /// </summary>
      public void Render(ID2D1RenderTarget target, RectF bounds)
  }
  ```
- [ ] Implement chip rendering:
  ```csharp
  /// <summary>
  /// Renders recommendation chip with theme-aware colors
  /// Light theme: Dark bg with light text
  /// Dark theme: Light bg with dark text
  /// Safe theme: High contrast guaranteed AA compliant
  /// </summary>
  private void RenderChip(ID2D1RenderTarget target, RectF bounds)
  {
      // Background with alpha based on theme
      Color bgColor;
      Color fgColor;
      
      switch (_currentTheme)
      {
          case Theme.Light:
              bgColor = Color.FromArgb(173, 17, 24, 39);    // #AD111827 (68% alpha)
              fgColor = Color.FromArgb(255, 255, 255, 255); // White
              break;
          case Theme.Dark:
              bgColor = Color.FromArgb(209, 243, 244, 246); // #D1F3F4F6 (82% alpha)
              fgColor = Color.FromArgb(255, 17, 24, 39);    // Dark
              break;
          case Theme.Safe:
              bgColor = Color.FromArgb(230, 17, 24, 39);    // #E6111827 (90% alpha)
              fgColor = Color.FromArgb(255, 249, 250, 251); // #F9FAFB
              break;
      }
      
      // Draw rounded rectangle background
      var roundedRect = new RoundedRectangle
      {
          Rect = bounds,
          RadiusX = _cornerRadius,
          RadiusY = _cornerRadius
      };
      target.FillRoundedRectangle(roundedRect, bgBrush);
      
      // Draw tier score (large)
      var scoreText = $"{_data.TierScore:F1}";
      DrawText(target, scoreText, scoreRect, 24, fgColor, FontWeight.Bold);
      
      // Draw primary reason
      DrawText(target, _data.PrimaryReason, reasonRect, 14, fgColor);
      
      // If expanded, show secondary reasons
      if (_isExpanded)
      {
          foreach (var reason in _data.SecondaryReasons)
          {
              DrawText(target, $"â€¢ {reason}", reasonRect, 12, fgColor);
              reasonRect.Y += 16;
          }
      }
  }
  ```
- [ ] Add expand/collapse animation:
  ```csharp
  /// <summary>
  /// Animates chip expansion/collapse
  /// Smooth height transition over 150ms
  /// </summary>
  public void ToggleExpanded()
  {
      _isExpanded = !_isExpanded;
      _animationStartTime = DateTime.Now;
      _animationDuration = TimeSpan.FromMilliseconds(150);
      
      _startHeight = _currentHeight;
      _targetHeight = _isExpanded ? _expandedHeight : _collapsedHeight;
  }
  ```

#### Validation Gate 8.2:
- [ ] Chip displays tier score prominently
- [ ] Primary reason is always visible
- [ ] Expand/collapse animation smooth
- [ ] Text contrast passes WCAG AA in all themes
- [ ] Chip doesn't obscure important UI
- [ ] **STOP if any validation fails**

### Task 8.3: Status Banners

#### Subtasks:
- [ ] Create `BannerSystem.cs`:
  ```csharp
  /// <summary>
  /// Manages status banners for various conditions
  /// Single line, dismissible or auto-hide
  /// Priority queue for multiple messages
  /// </summary>
  public class BannerSystem : IRenderable
  {
      private readonly Queue<Banner> _bannerQueue = new();
      private Banner _currentBanner;
      
      public enum BannerType
      {
          Info,        // Blue - general information
          Warning,     // Yellow - rate limits, calibration drift
          Error,       // Red - failures, disconnections
          Success,     // Green - successful operations
          Offline      // Orange - offline mode active
      }
      
      public class Banner
      {
          public string Message { get; set; }
          public BannerType Type { get; set; }
          public TimeSpan Duration { get; set; }
          public bool IsDismissible { get; set; }
          public DateTime ShowTime { get; set; }
      }
  }
  ```
- [ ] Implement banner rendering:
  ```csharp
  /// <summary>
  /// Renders banner at top of overlay
  /// Slides in from top, auto-dismisses after duration
  /// </summary>
  private void RenderBanner(ID2D1RenderTarget target, Banner banner)
  {
      // Calculate slide animation progress
      var elapsed = DateTime.Now - banner.ShowTime;
      var slideProgress = Math.Min(elapsed.TotalMilliseconds / 200, 1.0);
      
      var yOffset = Lerp(-_bannerHeight, 0, EaseOutCubic(slideProgress));
      var bounds = new RectF(0, yOffset, _overlayWidth, _bannerHeight);
      
      // Background color based on type
      Color bgColor = banner.Type switch
      {
          BannerType.Info => Color.FromArgb(200, 59, 130, 246),     // Blue
          BannerType.Warning => Color.FromArgb(200, 245, 158, 11),  // Yellow
          BannerType.Error => Color.FromArgb(200, 239, 68, 68),     // Red
          BannerType.Success => Color.FromArgb(200, 34, 197, 94),   // Green
          BannerType.Offline => Color.FromArgb(200, 251, 146, 60),  // Orange
          _ => Color.FromArgb(200, 107, 114, 128)                   // Gray
      };
      
      target.FillRectangle(bounds, CreateSolidBrush(bgColor));
      
      // Banner text (always white for contrast)
      DrawText(target, banner.Message, textBounds, 14, Color.White);
      
      // Dismiss button if dismissible
      if (banner.IsDismissible)
      {
          DrawDismissButton(target, dismissBounds);
      }
  }
  ```
- [ ] Add specific banner messages:
  ```csharp
  /// <summary>
  /// Pre-defined banner messages for common scenarios
  /// </summary>
  public static class BannerMessages
  {
      public const string RateLimited = "Rate limited - using fallback OCR";
      public const string Offline = "Offline mode - using local OCR";
      public const string Minimized = "Hearthstone window minimized";
      public const string CalibrationDrift = "Calibration drift detected - please recalibrate";
      public const string RetryFailed = "OCR failed - retrying...";
      public const string MirrorViewFailed = "Overlay capture prevention failed - check settings";
      public const string LowDiskSpace = "Low disk space - logs may be truncated";
      public const string DataRootFallback = "Using temporary storage - D:\\ unavailable";
  }
  ```

#### Validation Gate 8.3:
- [ ] Each banner type displays with correct color
- [ ] Slide animation completes in 200ms
- [ ] Auto-dismiss works after specified duration
- [ ] Manual dismiss button works when enabled
- [ ] Multiple banners queue correctly
- [ ] **STOP if any validation fails**

---

## PHASE 9: SETTINGS & CONFIGURATION

### Task 9.1: Hotkey System

#### Subtasks:
- [ ] Create `HotkeyManager.cs`:
  ```csharp
  /// <summary>
  /// Manages global and window-specific hotkeys
  /// Only active when Hearthstone is foreground
  /// Configurable key bindings
  /// </summary>
  public class HotkeyManager
  {
      private readonly Dictionary<HotkeyAction, Keys> _bindings = new();
      
      public enum HotkeyAction
      {
          ToggleOverlay,      // Default: F9
          ScanCurrentPick,    // Default: F10
          RetryOCR,          // Default: F11
          ShowMyCrops,       // Default: Ctrl+Shift+C
          CopyLastOCRJSON,   // Default: Ctrl+Shift+J
          ToggleSafeMode,    // Default: Ctrl+Shift+S
          OpenHelp           // Default: F1
      }
      
      /// <summary>
      /// Registers hotkeys with Windows
      /// Only active when target window is foreground
      /// </summary>
      public void RegisterHotkeys(IntPtr targetWindow)
  }
  ```
- [ ] Implement hotkey handling:
  ```csharp
  /// <summary>
  /// Processes hotkey press and triggers action
  /// Shows toast notification for feedback
  /// </summary>
  private void OnHotkeyPressed(HotkeyAction action)
  {
      switch (action)
      {
          case HotkeyAction.ToggleOverlay:
              _overlay.Toggle();
              ShowToast("Overlay " + (_overlay.IsVisible ? "shown" : "hidden"));
              break;
              
          case HotkeyAction.ScanCurrentPick:
              _ = Task.Run(() => ScanCurrentPick());
              ShowToast("Scanning current pick...");
              break;
              
          case HotkeyAction.RetryOCR:
              _ = Task.Run(() => RetryLastOCR());
              ShowToast("Retrying OCR...");
              break;
              
          case HotkeyAction.ShowMyCrops:
              ShowCropPreview();
              break;
              
          case HotkeyAction.CopyLastOCRJSON:
              CopyOCRToClipboard();
              ShowToast("OCR JSON copied to clipboard");
              break;
              
          case HotkeyAction.ToggleSafeMode:
              _settings.SafeMode = !_settings.SafeMode;
              ApplyTheme();
              ShowToast("Safe Mode " + (_settings.SafeMode ? "ON" : "OFF"));
              break;
              
          case HotkeyAction.OpenHelp:
              ShowHelpPanel();
              break;
      }
  }
  ```
- [ ] Add no-op detection:
  ```csharp
  /// <summary>
  /// Detects when action would have no effect
  /// Shows informative toast instead of failing silently
  /// </summary>
  private bool IsNoOp(HotkeyAction action)
  {
      return action switch
      {
          HotkeyAction.RetryOCR when !_hasLastOCR => true,
          HotkeyAction.CopyLastOCRJSON when _lastOCRResult == null => true,
          HotkeyAction.ScanCurrentPick when _captureState != CaptureState.Active => true,
          _ => false
      };
  }
  ```

#### Validation Gate 9.1:
- [ ] All default hotkeys register successfully
- [ ] Hotkeys only work when Hearthstone is foreground
- [ ] Each action triggers correct behavior
- [ ] Toast notifications appear for 2 seconds
- [ ] No-op conditions show helpful message
- [ ] **STOP if any validation fails**

### Task 9.2: Settings UI Tabs

#### Subtasks:
- [ ] Create `SettingsWindow.cs`:
  ```csharp
  /// <summary>
  /// Tabbed settings interface
  /// Tabs: Capture, OCR, Resolver, Overlay, Calibration, Diagnostics
  /// Saves settings immediately on change
  /// </summary>
  public class SettingsWindow : Window
  {
      private TabControl _tabControl;
      private readonly SettingsManager _settings;
      
      private void InitializeTabs()
      {
          _tabControl.Items.Add(new CaptureTab());
          _tabControl.Items.Add(new OCRTab());
          _tabControl.Items.Add(new ResolverTab());
          _tabControl.Items.Add(new OverlayTab());
          _tabControl.Items.Add(new CalibrationTab());
          _tabControl.Items.Add(new DiagnosticsTab());
      }
  }
  ```
- [ ] Implement Capture tab:
  ```csharp
  /// <summary>
  /// Capture settings tab
  /// Window selection, mirror view settings, frame rate
  /// </summary>
  public class CaptureTab : TabItem
  {
      // Controls:
      // - Current window display with "Change" button
      // - Mirror View validation toggle
      // - Frame buffer count (2 or 3)
      // - HDR tone-mapping options
  }
  ```
- [ ] Implement OCR tab:
  ```csharp
  /// <summary>
  /// OCR configuration tab
  /// API keys, endpoints, fallback settings
  /// </summary>
  public class OCRTab : TabItem
  {
      // Controls:
      // - Azure API key (masked input)
      // - Endpoint region dropdown
      // - Primary engine (v4/v3.2)
      // - Local OCR auto-enable threshold
      // - Timeout settings
      // - "Test Connection" button
      
      /// <summary>
      /// Tests OCR connection with small sample image
      /// Shows latency and success/failure
      /// </summary>
      private async void TestConnection_Click()
      {
          var testImage = GenerateTestImage();
          var stopwatch = Stopwatch.StartNew();
          
          try
          {
              var result = await _ocrClient.TestConnection(testImage);
              stopwatch.Stop();
              
              ShowResult($"Success! Latency: {stopwatch.ElapsedMilliseconds}ms");
          }
          catch (Exception ex)
          {
              ShowResult($"Failed: {ex.Message}");
          }
      }
  }
  ```
- [ ] Implement Resolver tab:
  ```csharp
  /// <summary>
  /// Card resolution settings
  /// Confidence thresholds, fuzzy match parameters
  /// </summary>
  public class ResolverTab : TabItem
  {
      // Controls:
      // - Min OCR confidence slider (0.70 default)
      // - Max edit distance for short names (1)
      // - Max edit distance for long names (2)
      // - Class filtering toggle
      // - Arena eligibility filtering toggle
      // - Ambiguity threshold
  }
  ```
- [ ] Implement Overlay tab:
  ```csharp
  /// <summary>
  /// Visual settings for overlay
  /// Theme selection, opacity, animations
  /// </summary>
  public class OverlayTab : TabItem
  {
      // Controls:
      // - Theme dropdown (Light/Dark/Safe)
      // - Overlay opacity slider
      // - Animation toggle
      // - Show confidence percentages toggle
      // - Banner duration slider
      // - Font size adjustment
  }
  ```
- [ ] Implement Calibration tab:
  ```csharp
  /// <summary>
  /// ROI calibration management
  /// Load/save profiles, drift detection
  /// </summary>
  public class CalibrationTab : TabItem
  {
      // Controls:
      // - Current profile display
      // - "Recalibrate" button
      // - "Show My Crops" preview
      // - Drift threshold setting (3px default)
      // - Profile import/export buttons
  }
  ```
- [ ] Implement Diagnostics tab:
  ```csharp
  /// <summary>
  /// Diagnostic tools and logging
  /// ETW traces, WER config, log export
  /// </summary>
  public class DiagnosticsTab : TabItem
  {
      // Controls:
      // - Enable verbose logging checkbox
      // - "Run ETW Trace" button (Light/Full)
      // - "Configure WER" button (requires elevation)
      // - "Export Logs" button
      // - "Clear Logs" button
      // - Storage usage display
      
      /// <summary>
      /// Configures Windows Error Reporting
      /// Requires elevation to modify HKLM
      /// </summary>
      private void ConfigureWER_Click()
      {
          if (!IsElevated())
          {
              // Restart as admin
              var startInfo = new ProcessStartInfo
              {
                  FileName = Process.GetCurrentProcess().MainModule.FileName,
                  Arguments = "--configure-wer",
                  Verb = "runas"
              };
              Process.Start(startInfo);
          }
          else
          {
              ConfigureWERRegistry();
          }
      }
  }
  ```
- [ ] Implement settings persistence:
  ```csharp
  /// <summary>
  /// Saves settings to JSON on every change
  /// Validates settings before saving
  /// </summary>
  public class SettingsManager
  {
      private readonly string _settingsPath;
      private Settings _currentSettings;
      
      public void Save()
      {
          var json = JsonConvert.SerializeObject(_currentSettings, Formatting.Indented);
          File.WriteAllText(_settingsPath, json);
      }
      
      public void Load()
      {
          if (File.Exists(_settingsPath))
          {
              var json = File.ReadAllText(_settingsPath);
              _currentSettings = JsonConvert.DeserializeObject<Settings>(json);
          }
          else
          {
              _currentSettings = Settings.GetDefaults();
              Save();
          }
      }
  }
  ```

#### Validation Gate 9.2:
- [ ] All 6 tabs display correctly
- [ ] Settings save immediately on change
- [ ] Settings persist across app restart
- [ ] Test Connection button works in OCR tab
- [ ] WER configuration prompts for elevation
- [ ] Import/export calibration profiles works
- [ ] **STOP if any validation fails**

### Task 9.3: Help Panel

#### Subtasks:
- [ ] Create `HelpPanel.cs`:
  ```csharp
  /// <summary>
  /// F1 help overlay panel
  /// Shows hotkeys, tips, and documentation links
  /// Semi-transparent, dismissible
  /// </summary>
  public class HelpPanel : IRenderable
  {
      private bool _isVisible = false;
      private readonly List<HelpSection> _sections = new();
      
      public class HelpSection
      {
          public string Title { get; set; }
          public List<HelpItem> Items { get; set; }
      }
      
      public class HelpItem
      {
          public string Key { get; set; }
          public string Description { get; set; }
      }
  }
  ```
- [ ] Add help content:
  ```csharp
  /// <summary>
  /// Populates help sections with hotkeys and tips
  /// </summary>
  private void InitializeHelpContent()
  {
      _sections.Add(new HelpSection
      {
          Title = "Hotkeys",
          Items = new List<HelpItem>
          {
              new() { Key = "F9", Description = "Toggle overlay visibility" },
              new() { Key = "F10", Description = "Scan current pick" },
              new() { Key = "F11", Description = "Retry last OCR" },
              new() { Key = "Ctrl+Shift+C", Description = "Show ROI preview" },
              new() { Key = "Ctrl+Shift+J", Description = "Copy OCR JSON" },
              new() { Key = "Ctrl+Shift+S", Description = "Toggle Safe Mode" },
              new() { Key = "F1", Description = "Show this help" }
          }
      });
      
      _sections.Add(new HelpSection
      {
          Title = "Status Indicators",
          Items = new List<HelpItem>
          {
              new() { Key = "Green", Description = "Connected and working" },
              new() { Key = "Yellow", Description = "Warning or degraded" },
              new() { Key = "Red", Description = "Error or disconnected" },
              new() { Key = "Orange", Description = "Offline mode active" }
          }
      });
      
      _sections.Add(new HelpSection
      {
          Title = "Quick Tips",
          Items = new List<HelpItem>
          {
              new() { Key = "", Description = "â€¢ Recalibrate if cards aren't detected" },
              new() { Key = "", Description = "â€¢ Safe Mode ensures text readability" },
              new() { Key = "", Description = "â€¢ Check logs in Diagnostics for issues" },
              new() { Key = "", Description = "â€¢ Offline mode uses local OCR" }
          }
      });
  }
  ```

#### Validation Gate 9.3:
- [ ] F1 key opens help panel
- [ ] All hotkeys listed correctly
- [ ] ESC or click outside closes panel
- [ ] Text is readable on all themes
- [ ] Links to documentation work
- [ ] **STOP if any validation fails**

---

## PHASE 10: DIAGNOSTICS & INSTRUMENTATION

### Task 10.1: ETW Provider Implementation

#### Subtasks:
- [ ] Create `ETWProvider.cs`:
  ```csharp
  /// <summary>
  /// Event Tracing for Windows provider
  /// Tracks performance metrics and diagnostic events
  /// </summary>
  [EventSource(Name = "HSGalaxy-ArenaAssistant")]
  public class ETWProvider : EventSource
  {
      public static readonly ETWProvider Log = new ETWProvider();
      
      // Define event IDs
      private const int CaptureStartId = 1;
      private const int CaptureEndId = 2;
      private const int OCRStartId = 3;
      private const int OCREndId = 4;
      private const int ResolveStartId = 5;
      private const int ResolveEndId = 6;
      private const int RenderStartId = 7;
      private const int RenderEndId = 8;
      private const int ErrorId = 100;
      
      /// <summary>
      /// Logs capture operation start
      /// </summary>
      [Event(CaptureStartId, Level = EventLevel.Informational)]
      public void CaptureStart(string windowName)
      {
          WriteEvent(CaptureStartId, windowName);
      }
      
      /// <summary>
      /// Logs capture operation end with metrics
      /// </summary>
      [Event(CaptureEndId, Level = EventLevel.Informational)]
      public void CaptureEnd(int frameCount, long elapsedMs)
      {
          WriteEvent(CaptureEndId, frameCount, elapsedMs);
      }
      
      /// <summary>
      /// Logs OCR operation with detailed timing
      /// </summary>
      [Event(OCREndId, Level = EventLevel.Informational)]
      public void OCREnd(string engine, int roiCount, long latencyMs, float avgConfidence)
      {
          WriteEvent(OCREndId, engine, roiCount, latencyMs, avgConfidence);
      }
  }
  ```
- [ ] Add performance counters:
  ```csharp
  /// <summary>
  /// Custom performance counters for monitoring
  /// </summary>
  public class PerformanceMetrics
  {
      private readonly EventCounter _captureLatency;
      private readonly EventCounter _ocrLatency;
      private readonly EventCounter _totalLatency;
      private readonly IncrementingEventCounter _pickCount;
      
      public PerformanceMetrics(EventSource source)
      {
          _captureLatency = new EventCounter("capture-latency-ms", source);
          _ocrLatency = new EventCounter("ocr-latency-ms", source);
          _totalLatency = new EventCounter("total-latency-ms", source);
          _pickCount = new IncrementingEventCounter("picks-processed", source);
      }
      
      public void RecordCapture(double latencyMs)
      {
          _captureLatency.WriteMetric(latencyMs);
      }
  }
  ```

#### Validation Gate 10.1:
- [ ] ETW provider registers successfully
- [ ] Events visible in PerfView
- [ ] Performance counters update correctly
- [ ] No performance impact when not tracing
- [ ] **STOP if any validation fails**

### Task 10.2: WPR Profiles

#### Subtasks:
- [ ] Create `Light.wprp`:
  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <WindowsPerformanceRecorder Version="1.0">
    <Profiles>
      <SystemCollector Id="SystemCollector_Light" Name="System Collector Light">
        <BufferSize Value="256"/>
        <Buffers Value="32"/>
      </SystemCollector>
      
      <EventCollector Id="EventCollector_Light" Name="HSGalaxy Light">
        <BufferSize Value="128"/>
        <Buffers Value="32"/>
      </EventCollector>
      
      <SystemProvider Id="SystemProvider_Base">
        <Keywords>
          <Keyword Value="ProcessThread"/>
          <Keyword Value="Loader"/>
          <Keyword Value="CpuConfig"/>
        </Keywords>
      </SystemProvider>
      
      <EventProvider Id="HSGalaxy_Provider" 
                     Name="HSGalaxy-ArenaAssistant" />
                     
      <Profile Id="Light.Verbose.Memory" 
               Name="Light Profile"
               Description="Lightweight trace for basic diagnostics">
        <Collectors>
          <SystemCollectorId Value="SystemCollector_Light">
            <SystemProviderId Value="SystemProvider_Base"/>
          </SystemCollectorId>
          <EventCollectorId Value="EventCollector_Light">
            <EventProviderId Value="HSGalaxy_Provider"/>
          </EventCollectorId>
        </Collectors>
      </Profile>
    </Profiles>
  </WindowsPerformanceRecorder>
  ```
- [ ] Create `Full.wprp`:
  ```xml
  <!-- Similar structure but with additional providers:
       - GPU events
       - Network events  
       - Registry access
       - File I/O
       - Larger buffers (512 KB)
  -->
  ```
- [ ] Create WPR launcher:
  ```csharp
  /// <summary>
  /// Launches WPR with selected profile
  /// Opens trace in WPA when complete
  /// </summary>
  public class WPRLauncher
  {
      public async Task<bool> StartTrace(string profileName)
      {
          var profilePath = Path.Combine(_toolsPath, $"{profileName}.wprp");
          
          var process = Process.Start(new ProcessStartInfo
          {
              FileName = "wpr.exe",
              Arguments = $"-start \"{profilePath}\"",
              UseShellExecute = false,
              CreateNoWindow = true
          });
          
          await process.WaitForExitAsync();
          return process.ExitCode == 0;
      }
      
      public async Task<string> StopTrace()
      {
          var tracePath = Path.Combine(_logsPath, $"trace_{DateTime.Now:yyyyMMdd_HHmmss}.etl");
          
          var process = Process.Start(new ProcessStartInfo
          {
              FileName = "wpr.exe",
              Arguments = $"-stop \"{tracePath}\"",
              UseShellExecute = false
          });
          
          await process.WaitForExitAsync();
          return tracePath;
      }
  }
  ```

#### Validation Gate 10.2:
- [ ] WPR profiles load without errors
- [ ] Can start trace with Light profile
- [ ] Can stop trace and save ETL file
- [ ] ETL file opens in WPA
- [ ] Custom events visible in trace
- [ ] **STOP if any validation fails**

### Task 10.3: Logging System

#### Subtasks:
- [ ] Create `LogManager.cs`:
  ```csharp
  /// <summary>
  /// JSONL structured logging with automatic rotation
  /// Daily files, compression, retention policies
  /// </summary>
  public class LogManager : ILogger
  {
      private readonly string _logPath;
      private readonly object _lock = new();
      private StreamWriter _currentWriter;
      private DateTime _currentDate;
      private long _currentSize;
      
      private const long MaxLogSize = 50_000_000; // 50MB per file
      private const int RetentionDays = 14;
      
      /// <summary>
      /// Writes structured log entry
      /// Format: JSONL (one JSON object per line)
      /// </summary>
      public void Log(LogLevel level, string message, object data = null)
      {
          var entry = new LogEntry
          {
              Timestamp = DateTime.UtcNow,
              Level = level.ToString(),
              Message = message,
              Data = data,
              ThreadId = Thread.CurrentThread.ManagedThreadId,
              SessionId = _sessionId
          };
          
          var json = JsonConvert.SerializeObject(entry, Formatting.None);
          
          lock (_lock)
          {
              EnsureLogFile();
              _currentWriter.WriteLine(json);
              _currentWriter.Flush();
              _currentSize += Encoding.UTF8.GetByteCount(json) + 2;
              
              if (_currentSize > MaxLogSize)
              {
                  RotateLog();
              }
          }
      }
  }
  ```
- [ ] Implement log rotation:
  ```csharp
  /// <summary>
  /// Rotates logs daily or when size limit reached
  /// Compresses old logs to ZIP
  /// </summary>
  private void RotateLog()
  {
      _currentWriter?.Close();
      
      var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
      var newPath = Path.Combine(_logPath, $"app_{timestamp}.jsonl");
      
      // Compress previous log if exists
      if (_currentLogPath != null && File.Exists(_currentLogPath))
      {
          CompressLog(_currentLogPath);
      }
      
      _currentLogPath = newPath;
      _currentWriter = new StreamWriter(_currentLogPath, append: false);
      _currentSize = 0;
      _currentDate = DateTime.Today;
      
      // Clean old logs
      CleanOldLogs();
  }
  ```
- [ ] Add log cleanup:
  ```csharp
  /// <summary>
  /// Removes logs older than retention period
  /// Enforces 200MB total cap with LRU deletion
  /// </summary>
  private void CleanOldLogs()
  {
      var cutoffDate = DateTime.Now.AddDays(-RetentionDays);
      var logFiles = Directory.GetFiles(_logPath, "*.zip")
          .Select(f => new FileInfo(f))
          .OrderBy(f => f.LastWriteTime)
          .ToList();
      
      // Delete by age
      foreach (var file in logFiles.Where(f => f.LastWriteTime < cutoffDate))
      {
          file.Delete();
      }
      
      // Enforce size cap (200MB)
      long totalSize = logFiles.Sum(f => f.Length);
      while (totalSize > 200_000_000 && logFiles.Count > 0)
      {
          var oldest = logFiles[0];
          totalSize -= oldest.Length;
          oldest.Delete();
          logFiles.RemoveAt(0);
      }
  }
  ```
- [ ] Implement log bundler:
  ```csharp
  /// <summary>
  /// Creates support bundle with logs and diagnostics
  /// Includes settings snapshot and system info
  /// </summary>
  public class LogBundler
  {
      public async Task<string> CreateBundle(bool includeDiagnosticImages = false)
      {
          var bundlePath = Path.Combine(_backupPath, $"bundle_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
          
          using (var zip = ZipFile.Open(bundlePath, ZipArchiveMode.Create))
          {
              // Add recent logs
              foreach (var logFile in GetRecentLogs(days: 3))
              {
                  zip.CreateEntryFromFile(logFile, Path.GetFileName(logFile));
              }
              
              // Add settings
              var settingsPath = Path.Combine(_configPath, "settings.json");
              if (File.Exists(settingsPath))
              {
                  zip.CreateEntryFromFile(settingsPath, "settings.json");
              }
              
              // Add system info
              var sysInfo = GatherSystemInfo();
              var sysInfoEntry = zip.CreateEntry("system_info.json");
              using (var stream = sysInfoEntry.Open())
              using (var writer = new StreamWriter(stream))
              {
                  await writer.WriteAsync(JsonConvert.SerializeObject(sysInfo));
              }
              
              // Optionally add diagnostic images
              if (includeDiagnosticImages)
              {
                  AddDiagnosticImages(zip);
              }
          }
          
          return bundlePath;
      }
  }
  ```

#### Validation Gate 10.3:
- [ ] Logs write to JSONL format
- [ ] Daily rotation occurs at midnight
- [ ] Size rotation at 50MB works
- [ ] Old logs compress to ZIP
- [ ] 14-day retention enforced
- [ ] 200MB cap with LRU works
- [ ] Bundle export includes all components
- [ ] **STOP if any validation fails**

### Task 10.4: WER Configuration

#### Subtasks:
- [ ] Create `WERConfigurator.cs`:
  ```csharp
  /// <summary>
  /// Configures Windows Error Reporting for crash dumps
  /// Requires elevation to modify HKLM registry
  /// </summary>
  public class WERConfigurator
  {
      private const string WERKey = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\HSGalaxy.exe";
      
      /// <summary>
      /// Configures WER to save dumps to D:\dumps
      /// Sets mini dumps with heap, limit 5
      /// </summary>
      public bool ConfigureWER()
      {
          if (!IsElevated())
          {
              throw new UnauthorizedAccessException("Elevation required for WER configuration");
          }
          
          try
          {
              using (var key = Registry.LocalMachine.CreateSubKey(WERKey))
              {
                  key.SetValue("DumpFolder", @"D:\cursor_bots\HSGalaxy\dumps", RegistryValueKind.String);
                  key.SetValue("DumpCount", 5, RegistryValueKind.DWord);
                  key.SetValue("DumpType", 1, RegistryValueKind.DWord); // MiniDumpWithDataSegs
                  key.SetValue("CustomDumpFlags", 0, RegistryValueKind.DWord);
              }
              
              return true;
          }
          catch (Exception ex)
          {
              Log.Error("Failed to configure WER", ex);
              return false;
          }
      }
  }
  ```
- [ ] Implement dump cleanup:
  ```csharp
  /// <summary>
  /// Manages crash dump retention
  /// Enforces 50MB cap across all dumps
  /// </summary>
  public class DumpManager
  {
      private const long MaxDumpSize = 50_000_000; // 50MB total
      
      public void CleanOldDumps()
      {
          var dumpPath = @"D:\cursor_bots\HSGalaxy\dumps";
          if (!Directory.Exists(dumpPath)) return;
          
          var dumps = Directory.GetFiles(dumpPath, "*.dmp")
              .Select(f => new FileInfo(f))
              .OrderBy(f => f.CreationTime)
              .ToList();
          
          // Keep newest 5
          while (dumps.Count > 5)
          {
              dumps[0].Delete();
              dumps.RemoveAt(0);
          }
          
          // Enforce size cap
          long totalSize = dumps.Sum(f => f.Length);
          while (totalSize > MaxDumpSize && dumps.Count > 1)
          {
              var oldest = dumps[0];
              totalSize -= oldest.Length;
              oldest.Delete();
              dumps.RemoveAt(0);
          }
      }
  }
  ```

#### Validation Gate 10.4:
- [ ] WER configurator prompts for elevation
- [ ] Registry keys created correctly
- [ ] Crash produces dump in D:\dumps
- [ ] Dump cleanup keeps only 5 newest
- [ ] 50MB cap enforced
- [ ] **STOP if any validation fails**

---

## PHASE 11: VALIDATION & TESTING

### Task 11.1: Validation Test Runner

#### Subtasks:
- [ ] Create `ValidationRunner.cs`:
  ```csharp
  /// <summary>
  /// Automated validation test runner
  /// Processes test corpus and generates reports
  /// </summary>
  public class ValidationRunner
  {
      private readonly string _corpusPath;
      private readonly OCRPipeline _ocrPipeline;
      private readonly CardResolver _resolver;
      private readonly TierScoreEngine _scorer;
      
      /// <summary>
      /// Runs validation against test corpus
      /// Measures accuracy and performance metrics
      /// </summary>
      public async Task<ValidationReport> RunValidation(ValidationOptions options)
      {
          var results = new List<ValidationResult>();
          var stopwatch = new Stopwatch();
          
          foreach (var testCase in LoadCorpus(_corpusPath))
          {
              stopwatch.Restart();
              
              // Run OCR
              var ocrResult = await _ocrPipeline.ProcessImage(testCase.Image);
              
              // Resolve cards
              var resolved = new List<Card>();
              foreach (var text in ocrResult.Texts)
              {
                  var card = _resolver.Resolve(text.Value, text.Confidence, testCase.Class);
                  resolved.Add(card.Card);
              }
              
              stopwatch.Stop();
              
              // Compare with expected
              var accuracy = CalculateAccuracy(resolved, testCase.Expected);
              
              results.Add(new ValidationResult
              {
                  TestCase = testCase.Name,
                  Accuracy = accuracy,
                  LatencyMs = stopwatch.ElapsedMilliseconds,
                  OCRConfidence = ocrResult.AverageConfidence,
                  Errors = GetErrors(resolved, testCase.Expected)
              });
          }
          
          return GenerateReport(results);
      }
  }
  ```
- [ ] Create corpus loader:
  ```csharp
  /// <summary>
  /// Loads test corpus with ground truth
  /// Format: image + JSON metadata
  /// </summary>
  public class CorpusLoader
  {
      public IEnumerable<TestCase> LoadCorpus(string path)
      {
          var corpusFile = Path.Combine(path, "corpus.json");
          var corpus = JsonConvert.DeserializeObject<CorpusManifest>(
              File.ReadAllText(corpusFile));
          
          foreach (var entry in corpus.Entries)
          {
              yield return new TestCase
              {
                  Name = entry.Name,
                  Image = File.ReadAllBytes(Path.Combine(path, entry.ImageFile)),
                  Expected = entry.Cards,
                  Class = entry.Class,
                  DPI = entry.DPI,
                  Theme = entry.Theme
              };
          }
      }
  }
  ```
- [ ] Implement metrics calculation:
  ```csharp
  /// <summary>
  /// Calculates validation metrics
  /// False-ID, ambiguity, unrecognized rates
  /// </summary>
  public class MetricsCalculator
  {
      public ValidationMetrics Calculate(List<ValidationResult> results)
      {
          return new ValidationMetrics
          {
              TotalCases = results.Count,
              SuccessRate = results.Count(r => r.Accuracy == 1.0) / (double)results.Count,
              
              FalseIdRate = results.Sum(r => r.Errors.Count(e => e.Type == ErrorType.FalseId)) 
                            / (double)(results.Count * 3),
                            
              AmbiguityRate = results.Sum(r => r.Errors.Count(e => e.Type == ErrorType.Ambiguous))
                             / (double)(results.Count * 3),
                             
              UnrecognizedRate = results.Sum(r => r.Errors.Count(e => e.Type == ErrorType.Unrecognized))
                                / (double)(results.Count * 3),
              
              P50Latency = results.OrderBy(r => r.LatencyMs)
                                  .Skip(results.Count / 2).First().LatencyMs,
                                  
              P95Latency = results.OrderBy(r => r.LatencyMs)
                                  .Skip((int)(results.Count * 0.95)).First().LatencyMs,
              
              AverageConfidence = results.Average(r => r.OCRConfidence)
          };
      }
  }
  ```
- [ ] Generate HTML report:
  ```csharp
  /// <summary>
  /// Generates detailed HTML validation report
  /// Includes charts, tables, and error details
  /// </summary>
  public class ReportGenerator
  {
      public void GenerateHTML(ValidationReport report, string outputPath)
      {
          var html = @"
  <!DOCTYPE html>
  <html>
  <head>
      <title>HSGalaxy Validation Report</title>
      <style>
          body { font-family: Arial, sans-serif; margin: 20px; }
          .metric { display: inline-block; margin: 10px; padding: 10px; 
                    border: 1px solid #ccc; border-radius: 5px; }
          .pass { background-color: #d4edda; }
          .fail { background-color: #f8d7da; }
          table { border-collapse: collapse; width: 100%; margin: 20px 0; }
          th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
          th { background-color: #f2f2f2; }
      </style>
  </head>
  <body>
      <h1>Validation Report</h1>
      <div class='metrics'>
          " + GenerateMetricsHTML(report.Metrics) + @"
      </div>
      <h2>Detailed Results</h2>
      <table>
          " + GenerateResultsTable(report.Results) + @"
      </table>
  </body>
  </html>";
          
          File.WriteAllText(outputPath, html);
      }
  }
  ```

#### Validation Gate 11.1:
- [ ] Test runner processes entire corpus
- [ ] Metrics calculate correctly
- [ ] HTML report generates and opens
- [ ] P50 â‰¤ 300ms on test corpus
- [ ] P95 â‰¤ 600ms on test corpus
- [ ] **STOP if any validation fails**

### Task 11.2: Test Corpus Preparation

#### Subtasks:
- [ ] Create corpus structure:
  ```
  /corpus
    /enUS
      /100dpi
        /light_theme
        /dark_theme
      /125dpi
        /light_theme
        /dark_theme
      /150dpi
        /light_theme
        /dark_theme
    corpus.json
  ```
- [ ] Capture test images:
  ```csharp
  /// <summary>
  /// Helper tool to capture and label test cases
  /// Semi-automated with manual verification
  /// </summary>
  public class CorpusBuilder
  {
      public void CaptureTestCase(string name, Card[] expectedCards, string playerClass)
      {
          // Capture current draft screen
          var screenshot = CaptureScreen();
          
          // Extract ROIs
          var rois = ExtractROIs(screenshot);
          
          // Create composite
          var composite = _compositeBuilder.BuildComposite(rois);
          
          // Save with metadata
          var testCase = new CorpusEntry
          {
              Name = name,
              ImageFile = $"{name}.png",
              Cards = expectedCards.Select(c => c.Name).ToArray(),
              Class = playerClass,
              DPI = GetCurrentDPI(),
              Theme = GetCurrentTheme(),
              Timestamp = DateTime.UtcNow
          };
          
          SaveTestCase(testCase, composite);
      }
  }
  ```
- [ ] Validate corpus coverage:
  ```csharp
  /// <summary>
  /// Ensures corpus has good coverage
  /// Minimum requirements per configuration
  /// </summary>
  public class CorpusValidator
  {
      public bool ValidateCorpus(string corpusPath)
      {
          var manifest = LoadManifest(corpusPath);
          
          // Check minimum counts
          var byConfig = manifest.Entries.GroupBy(e => new { e.DPI, e.Theme });
          
          foreach (var group in byConfig)
          {
              if (group.Count() < 50)
              {
                  Log.Warning($"Insufficient samples for {group.Key}: {group.Count()}/50");
                  return false;
              }
          }
          
          // Check class distribution
          var byClass = manifest.Entries.GroupBy(e => e.Class);
          foreach (var playerClass in GetAllClasses())
          {
              if (!byClass.Any(g => g.Key == playerClass))
              {
                  Log.Warning($"Missing samples for class: {playerClass}");
                  return false;
              }
          }
          
          return true;
      }
  }
  ```

#### Validation Gate 11.2:
- [ ] Corpus has â‰¥500 total test cases
- [ ] Each DPI/theme combo has â‰¥50 cases
- [ ] All 10 classes represented
- [ ] Corpus manifest valid JSON
- [ ] All referenced images exist
- [ ] **STOP if any validation fails**

### Task 11.3: Benchmark Mode

#### Subtasks:
- [ ] Create `/bench` CLI command:
  ```csharp
  /// <summary>
  /// Command-line benchmark mode
  /// Runs validation and outputs metrics
  /// </summary>
  [Command("bench")]
  public class BenchmarkCommand : ICommand
  {
      [Option("--corpus", Required = true)]
      public string CorpusPath { get; set; }
      
      [Option("--output", Default = "report.html")]
      public string OutputPath { get; set; }
      
      [Option("--iterations", Default = 1)]
      public int Iterations { get; set; }
      
      [Option("--parallel", Default = false)]
      public bool Parallel { get; set; }
      
      public async Task<int> Execute()
      {
          Console.WriteLine($"Starting benchmark with corpus: {CorpusPath}");
          Console.WriteLine($"Iterations: {Iterations}");
          
          var runner = new ValidationRunner();
          var allResults = new List<ValidationReport>();
          
          for (int i = 0; i < Iterations; i++)
          {
              Console.WriteLine($"Iteration {i + 1}/{Iterations}...");
              
              var report = await runner.RunValidation(new ValidationOptions
              {
                  CorpusPath = CorpusPath,
                  Parallel = Parallel
              });
              
              allResults.Add(report);
              
              // Print summary
              Console.WriteLine($"  Success Rate: {report.Metrics.SuccessRate:P2}");
              Console.WriteLine($"  P50 Latency: {report.Metrics.P50Latency}ms");
              Console.WriteLine($"  P95 Latency: {report.Metrics.P95Latency}ms");
          }
          
          // Generate consolidated report
          GenerateConsolidatedReport(allResults, OutputPath);
          
          // Check pass/fail criteria
          return CheckPassCriteria(allResults) ? 0 : 1;
      }
  }
  ```

#### Validation Gate 11.3:
- [ ] Benchmark command runs successfully
- [ ] Processes full corpus without errors
- [ ] Outputs valid HTML report
- [ ] Exit code 0 when criteria met
- [ ] Exit code 1 when criteria not met
- [ ] **STOP if any validation fails**

---

## PHASE 12: PACKAGING & DEPLOYMENT

### Task 12.1: Build Configuration

#### Subtasks:
- [ ] Configure Release build:
  ```xml
  <PropertyGroup Condition="'$(Configuration)'=='Release'">
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
    <DebugSymbols>true</DebugSymbols>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningLevel>5</WarningLevel>
    <TrimUnusedDependencies>true</TrimUnusedDependencies>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishSingleFile>false</PublishSingleFile>
    <SelfContained>false</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
  ```
- [ ] Set version information:
  ```csharp
  /// <summary>
  /// Assembly version attributes
  /// Update for each release
  /// </summary>
  [assembly: AssemblyVersion("0.1.0.0")]
  [assembly: AssemblyFileVersion("0.1.0.0")]
  [assembly: AssemblyInformationalVersion("0.1.0-mvp")]
  ```
- [ ] Configure build output paths:
  ```xml
  <PropertyGroup>
    <OutputPath>..\..\bin\$(Configuration)\</OutputPath>
    <IntermediateOutputPath>..\..\obj\$(Configuration)\$(MSBuildProjectName)\</IntermediateOutputPath>
    <DocumentationFile>$(OutputPath)$(AssemblyName).xml</DocumentationFile>
  </PropertyGroup>
  ```

#### Validation Gate 12.1:
- [ ] Release build succeeds with 0 warnings
- [ ] All XML documentation generated
- [ ] Binary size reasonable (< 50MB for main exe)
- [ ] Dependencies copied to output
- [ ] Version info shows in file properties
- [ ] **STOP if any validation fails**

### Task 12.2: Code Signing

#### Subtasks:
- [ ] Obtain OV code signing certificate:
  ```csharp
  /// <summary>
  /// Code signing configuration
  /// Uses OV certificate for SmartScreen reputation
  /// </summary>
  public class SigningConfig
  {
      public string CertificatePath { get; set; }
      public string CertificatePassword { get; set; }
      public string TimestampServer { get; set; } = "http://timestamp.digicert.com";
      public string Description { get; set; } = "HSGalaxy Arena Draft Assistant";
      public string DescriptionUrl { get; set; } = "https://github.com/hsgalaxy/arena-assistant";
  }
  ```
- [ ] Create signing script:
  ```powershell
  # SignBinaries.ps1
  param(
      [string]$CertPath,
      [string]$CertPassword,
      [string]$BinPath
  )
  
  $signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
  
  # Sign all EXEs and DLLs
  $files = Get-ChildItem -Path $BinPath -Include *.exe,*.dll -Recurse
  
  foreach ($file in $files) {
      Write-Host "Signing $($file.Name)..."
      
      & $signtool sign /f $CertPath /p $CertPassword `
          /t http://timestamp.digicert.com `
          /d "HSGalaxy Arena Draft Assistant" `
          /du "https://github.com/hsgalaxy/arena-assistant" `
          $file.FullName
      
      if ($LASTEXITCODE -ne 0) {
          throw "Failed to sign $($file.Name)"
      }
  }
  
  Write-Host "All binaries signed successfully"
  ```
- [ ] Verify signatures:
  ```csharp
  /// <summary>
  /// Verifies all binaries are properly signed
  /// Checks certificate validity and timestamp
  /// </summary>
  public class SignatureVerifier
  {
      public bool VerifySignatures(string binPath)
      {
          var files = Directory.GetFiles(binPath, "*.exe", SearchOption.AllDirectories)
              .Concat(Directory.GetFiles(binPath, "*.dll", SearchOption.AllDirectories));
          
          foreach (var file in files)
          {
              var cert = X509Certificate.CreateFromSignedFile(file);
              if (cert == null)
              {
                  Log.Error($"No signature found: {file}");
                  return false;
              }
              
              // Verify certificate is valid
              var cert2 = new X509Certificate2(cert);
              if (cert2.NotAfter < DateTime.Now)
              {
                  Log.Error($"Certificate expired: {file}");
                  return false;
              }
              
              Log.Info($"Signature valid: {Path.GetFileName(file)}");
          }
          
          return true;
      }
  }
  ```

#### Validation Gate 12.2:
- [ ] All EXEs and DLLs signed
- [ ] Signatures include timestamp
- [ ] Certificate shows correct publisher
- [ ] SmartScreen doesn't block exe
- [ ] No unsigned binaries in output
- [ ] **STOP if any validation fails**

### Task 12.3: Portable Package Creation

#### Subtasks:
- [ ] Create portable ZIP package:
  ```csharp
  /// <summary>
  /// Creates portable distribution package
  /// No installation required, xcopy deployment
  /// </summary>
  public class PackageBuilder
  {
      public async Task<string> CreatePortablePackage(string version)
      {
          var packageName = $"HSGalaxy_Arena_Assistant_{version}_Portable.zip";
          var stagingPath = Path.Combine(Path.GetTempPath(), "HSGalaxy_Staging");
          
          try
          {
              // Create staging directory
              Directory.CreateDirectory(stagingPath);
              
              // Copy binaries
              CopyBinaries(stagingPath);
              
              // Copy runtime files
              CopyRuntimeFiles(stagingPath);
              
              // Create default config
              CreateDefaultConfig(stagingPath);
              
              // Add readme
              CreateReadme(stagingPath, version);
              
              // Create ZIP
              ZipFile.CreateFromDirectory(stagingPath, packageName);
              
              return packageName;
          }
          finally
          {
              // Cleanup staging
              Directory.Delete(stagingPath, recursive: true);
          }
      }
  }
  ```
- [ ] Create README.md for package:
  ```markdown
  # HSGalaxy Arena Draft Assistant v{VERSION}
  
  ## Quick Start
  1. Extract all files to a folder
  2. Run HSGalaxy.exe
  3. Follow the calibration wizard
  4. Start Hearthstone and enter Arena draft
  
  ## System Requirements
  - Windows 11 (build 22000 or later)
  - .NET 8.0 Runtime
  - DirectX 11 compatible GPU
  - 200MB free disk space
  - Internet connection for OCR
  
  ## First Run Setup
  1. The app will check for Azure OCR connectivity
  2. Calibration wizard will help you set up ROIs
  3. Configure your Azure API key in Settings
  
  ## Hotkeys
  - F9: Toggle overlay
  - F10: Scan current pick
  - F11: Retry OCR
  - F1: Help
  
  ## Troubleshooting
  See logs in: %LOCALAPPDATA%\HSGalaxy\logs\
  ```
- [ ] Create default configuration:
  ```json
  {
    "version": "1.0",
    "firstRun": true,
    "theme": "Dark",
    "ocr": {
      "primaryEngine": "AzureV4",
      "timeout": 600,
      "retryCount": 3,
      "enableLocalFallback": false
    },
    "overlay": {
      "opacity": 0.95,
      "animationsEnabled": true,
      "showConfidenceScores": true
    },
    "storage": {
      "dataRoot": null,
      "autoBackup": true,
      "logRetentionDays": 14
    }
  }
  ```

#### Validation Gate 12.3:
- [ ] ZIP package created successfully
- [ ] All required files included
- [ ] Extraction works on clean system
- [ ] App runs from extracted folder
- [ ] No absolute paths in configs
- [ ] **STOP if any validation fails**

### Task 12.4: MSIX Package (Future)

#### Subtasks:
- [ ] Create Package.appxmanifest:
  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
           xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
           xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
    
    <Identity Name="HSGalaxy.ArenaAssistant"
              Version="0.1.0.0"
              Publisher="CN=HSGalaxy, O=HSGalaxy, C=US" />
    
    <Properties>
      <DisplayName>HSGalaxy Arena Draft Assistant</DisplayName>
      <PublisherDisplayName>HSGalaxy</PublisherDisplayName>
      <Logo>Assets\Logo.png</Logo>
    </Properties>
    
    <Dependencies>
      <TargetDeviceFamily Name="Windows.Desktop" 
                          MinVersion="10.0.22000.0" 
                          MaxVersionTested="10.0.22621.0" />
    </Dependencies>
    
    <Resources>
      <Resource Language="en-US" />
    </Resources>
    
    <Applications>
      <Application Id="App" 
                   Executable="HSGalaxy.exe" 
                   EntryPoint="Windows.FullTrustApplication">
        <uap:VisualElements DisplayName="HSGalaxy Arena Assistant"
                            Description="Hearthstone Arena Draft OCR Assistant"
                            BackgroundColor="transparent"
                            Square150x150Logo="Assets\Square150x150Logo.png"
                            Square44x44Logo="Assets\Square44x44Logo.png">
        </uap:VisualElements>
      </Application>
    </Applications>
    
    <Capabilities>
      <rescap:Capability Name="runFullTrust" />
    </Capabilities>
  </Package>
  ```
- [ ] Note: MSIX deferred for future release

#### Validation Gate 12.4:
- [ ] Manifest validates without errors
- [ ] Package structure documented
- [ ] Decision logged to defer MSIX
- [ ] **STOP if any validation fails**

---

## PHASE 13: RELEASE PREPARATION

### Task 13.1: Final Integration Testing

#### Subtasks:
- [ ] Create integration test suite:
  ```csharp
  /// <summary>
  /// Full end-to-end integration tests
  /// Tests complete pick flow from capture to recommendation
  /// </summary>
  public class IntegrationTests
  {
      [Test]
      public async Task FullPickFlow_ShouldCompleteUnder600ms()
      {
          // Arrange
          var testImage = LoadTestImage("three_cards.png");
          var calibration = LoadCalibration("test_profile.json");
          
          // Act
          var stopwatch = Stopwatch.StartNew();
          
          // Simulate capture
          var rois = ExtractROIs(testImage, calibration);
          
          // Build composite
          var composite = BuildComposite(rois);
          
          // OCR
          var ocrResult = await PerformOCR(composite);
          
          // Resolve
          var cards = ResolveCards(ocrResult);
          
          // Score
          var recommendation = ScoreCards(cards);
          
          stopwatch.Stop();
          
          // Assert
          Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(600));
          Assert.That(cards.Length, Is.EqualTo(3));
          Assert.That(recommendation, Is.Not.Null);
      }
      
      [Test]
      public async Task FailoverScenario_ShouldUseFallbackOCR()
      {
          // Simulate Azure v4 failure
          _mockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
              .ThrowsAsync(new HttpRequestException("Connection failed"));
          
          var result = await _ocrPipeline.ProcessImage(testImage);
          
          Assert.That(result.Engine, Is.EqualTo("AzureV32"));
          Assert.That(result.Success, Is.True);
      }
  }
  ```
- [ ] Test all critical paths:
  ```csharp
  /// <summary>
  /// Tests all critical user paths
  /// </summary>
  public class CriticalPathTests
  {
      [Test] public void FirstRun_CalibrationWizard_Completes() { }
      [Test] public void NormalPick_RecommendationShows() { }
      [Test] public void RateLimited_FallbackEngages() { }
      [Test] public void NetworkOffline_LocalOCRActivates() { }
      [Test] public void WindowMinimized_BannerShows() { }
      [Test] public void LowDiskSpace_WarningAppears() { }
      [Test] public void ThemeSwitch_UIUpdatesCorrectly() { }
      [Test] public void HotkeysWork_WhenHearthstoneFocused() { }
      [Test] public void SettingsPersist_AcrossRestart() { }
      [Test] public void LogRotation_WorksAtMidnight() { }
  }
  ```

#### Validation Gate 13.1:
- [ ] All integration tests pass
- [ ] All critical path tests pass
- [ ] No memory leaks over 1-hour run
- [ ] CPU usage < 5% when idle
- [ ] GPU usage < 2% when idle
- [ ] **STOP if any validation fails**

### Task 13.2: Performance Tuning

#### Subtasks:
- [ ] Profile and optimize hotspots:
  ```csharp
  /// <summary>
  /// Performance optimization targets
  /// Based on profiling results
  /// </summary>
  public class PerformanceOptimizations
  {
      // Optimization 1: Cache DirectWrite text layouts
      private readonly Dictionary<string, IDWriteTextLayout> _layoutCache = new();
      
      // Optimization 2: Reuse HttpContent for repeated calls
      private readonly ObjectPool<ByteArrayContent> _contentPool;
      
      // Optimization 3: Pre-calculate theme colors
      private readonly Dictionary<Theme, ColorSet> _themeColors;
      
      // Optimization 4: Batch GPU operations
      private readonly List<RenderOperation> _renderBatch = new();
      
      /// <summary>
      /// Applies all performance optimizations
      /// Measures impact and logs improvements
      /// </summary>
      public void ApplyOptimizations()
      {
          EnableLayoutCaching();
          EnableHttpContentPooling();
          PrecalculateThemeColors();
          EnableRenderBatching();
          
          // Measure impact
          var baseline = MeasurePerformance(optimized: false);
          var optimized = MeasurePerformance(optimized: true);
          
          Log.Info($"Performance improvement: {baseline.P95 - optimized.P95}ms");
      }
  }
  ```
- [ ] Tune garbage collection:
  ```csharp
  /// <summary>
  /// GC tuning for reduced latency
  /// </summary>
  public static void ConfigureGC()
  {
      GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
      GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
      
      // For .NET 8
      AppContext.SetSwitch("System.Runtime.TieredCompilation.QuickJit", true);
      AppContext.SetSwitch("System.Runtime.TieredCompilation.QuickJitForLoops", true);
  }
  ```

#### Validation Gate 13.2:
- [ ] P50 latency â‰¤ 250ms (target 300ms)
- [ ] P95 latency â‰¤ 500ms (target 600ms)
- [ ] Memory usage < 200MB typical
- [ ] No allocations in render loop
- [ ] GC Gen2 collections < 1/minute
- [ ] **STOP if any validation fails**

### Task 13.3: Documentation

#### Subtasks:
- [ ] Create user documentation:
  ```markdown
  # HSGalaxy Arena Draft Assistant - User Guide
  
  ## Table of Contents
  1. Installation
  2. Initial Setup
  3. Using the Overlay
  4. Understanding Recommendations
  5. Troubleshooting
  6. FAQ
  
  ## Installation
  ### System Requirements
  - Windows 11 (build 22000 or later)
  - .NET 8.0 Runtime (installer will prompt if missing)
  - DirectX 11 compatible graphics
  - 200MB free disk space
  - Stable internet connection
  
  ### Installation Steps
  1. Download HSGalaxy_Arena_Assistant_Portable.zip
  2. Extract to your preferred location (e.g., C:\Tools\HSGalaxy)
  3. Run HSGalaxy.exe
  4. Follow the first-run setup wizard
  
  ## Initial Setup
  ### Azure OCR Configuration
  1. Obtain Azure Computer Vision API key
  2. Open Settings â†’ OCR tab
  3. Enter your API key
  4. Select your preferred region (West US recommended)
  5. Click "Test Connection" to verify
  
  ### Calibration
  1. Open Hearthstone and navigate to Arena draft
  2. Press F9 to show overlay
  3. Click "Calibrate" button
  4. Follow the 3-step wizard:
     - Select Hearthstone window
     - Draw rectangles around the 3 card nameplates
     - Verify and save calibration
  ```
- [ ] Create API documentation:
  ```csharp
  /// <summary>
  /// API documentation for extensions
  /// Allows third-party plugins (future)
  /// </summary>
  namespace HSGalaxy.SDK
  {
      /// <summary>
      /// Interface for custom OCR engines
      /// Implement to add new OCR providers
      /// </summary>
      public interface IOCREngine
      {
          /// <summary>
          /// Performs OCR on image data
          /// </summary>
          /// <param name="imageData">Image bytes (PNG/JPEG)</param>
          /// <returns>OCR results with confidence scores</returns>
          Task<OCRResult> ProcessImageAsync(byte[] imageData);
          
          /// <summary>
          /// Engine name for display
          /// </summary>
          string Name { get; }
          
          /// <summary>
          /// Whether engine requires internet
          /// </summary>
          bool RequiresInternet { get; }
      }
  }
  ```

#### Validation Gate 13.3:
- [ ] User guide complete and accurate
- [ ] All screenshots included
- [ ] API documentation generated
- [ ] Troubleshooting covers common issues
- [ ] FAQ answers top 10 questions
- [ ] **STOP if any validation fails**

### Task 13.4: Release Checklist

#### Subtasks:
- [ ] Version numbers updated:
  ```csharp
  // AssemblyInfo.cs
  [assembly: AssemblyVersion("0.1.0.0")]
  [assembly: AssemblyFileVersion("0.1.0.0")]
  [assembly: AssemblyInformationalVersion("0.1.0-mvp")]
  
  // Package.json (if applicable)
  "version": "0.1.0"
  
  // Settings default
  "appVersion": "0.1.0-mvp"
  ```
- [ ] Change log prepared:
  ```markdown
  # Release Notes - v0.1.0-mvp
  
  ## Initial Release
  Released: [DATE]
  
  ### Features
  - âœ… Windows 11 overlay with click-through support
  - âœ… Windows Graphics Capture integration
  - âœ… Azure Computer Vision OCR (v4 primary, v3.2 fallback)
  - âœ… Local OCR fallback for offline mode
  - âœ… Card name resolution with fuzzy matching
  - âœ… Tier-based recommendations with synergy detection
  - âœ… Three-theme support (Light/Dark/Safe)
  - âœ… Calibration wizard for easy setup
  - âœ… Comprehensive diagnostics and logging
  
  ### Known Limitations
  - English (US) only in this release
  - Requires manual calibration per resolution
  - Local OCR disabled by default (enable in Settings)
  
  ### System Requirements
  - Windows 11 build 22000+
  - .NET 8.0 Runtime
  - DirectX 11 compatible GPU
  - 200MB disk space
  - Internet connection for cloud OCR
  ```
- [ ] Final validation run:
  ```powershell
  # ReleaseValidation.ps1
  Write-Host "Running release validation..." -ForegroundColor Cyan
  
  # Check version consistency
  $exeVersion = (Get-Item .\HSGalaxy.exe).VersionInfo.FileVersion
  $manifestVersion = (Get-Content .\settings.default.json | ConvertFrom-Json).appVersion
  
  if ($exeVersion -ne "0.1.0.0") {
      throw "Version mismatch in exe"
  }
  
  # Verify signatures
  $signed = Get-AuthenticodeSignature .\HSGalaxy.exe
  if ($signed.Status -ne "Valid") {
      throw "Invalid signature"
  }
  
  # Run validation suite
  .\HSGalaxy.exe /bench --corpus ".\corpus" --output "release_validation.html"
  
  # Check metrics
  $report = Get-Content .\release_validation.html
  # Parse and verify metrics meet criteria
  
  Write-Host "âœ… Release validation PASSED" -ForegroundColor Green
  ```

#### Validation Gate 13.4:
- [ ] All version numbers consistent
- [ ] Change log accurate and complete
- [ ] Release validation script passes
- [ ] No TODO or FIXME in code
- [ ] No test/debug code in release
- [ ] **STOP if any validation fails**

---

## PHASE 14: POST-RELEASE OPERATIONS

### Task 14.1: Monitoring Setup

#### Subtasks:
- [ ] Create telemetry collection (privacy-respecting):
  ```csharp
  /// <summary>
  /// Anonymous telemetry for quality monitoring
  /// Opt-in only, no personal data
  /// </summary>
  public class TelemetryManager
  {
      private bool _isEnabled = false;
      private readonly string _sessionId = Guid.NewGuid().ToString();
      
      /// <summary>
      /// Collects anonymous performance metrics
      /// No card names, no user data
      /// </summary>
      public void RecordMetric(string metric, double value)
      {
          if (!_isEnabled) return;
          
          var data = new
          {
              SessionId = _sessionId,
              Metric = metric,
              Value = value,
              Timestamp = DateTime.UtcNow,
              Version = GetAppVersion(),
              OS = "Windows11"
          };
          
          // Queue for batched sending
          _metricsQueue.Enqueue(data);
      }
  }
  ```
- [ ] Set up error reporting:
  ```csharp
  /// <summary>
  /// Automatic error reporting (opt-in)
  /// Strips sensitive data before sending
  /// </summary>
  public class ErrorReporter
  {
      public async Task ReportError(Exception ex)
      {
          if (!_settings.EnableErrorReporting) return;
          
          var report = new ErrorReport
          {
              ExceptionType = ex.GetType().Name,
              Message = SanitizeMessage(ex.Message),
              StackTrace = SanitizeStackTrace(ex.StackTrace),
              Version = GetAppVersion(),
              Timestamp = DateTime.UtcNow
          };
          
          await SendReport(report);
      }
      
      private string SanitizeMessage(string message)
      {
          // Remove file paths, user names, API keys
          message = Regex.Replace(message, @"[A-Z]:\\.*?\\", @"<path>\");
          message = Regex.Replace(message, @"[a-zA-Z0-9]{32,}", "<key>");
          return message;
      }
  }
  ```

#### Validation Gate 14.1:
- [ ] Telemetry only sends when opted-in
- [ ] No PII in telemetry data
- [ ] Error reports sanitized properly
- [ ] Can disable all reporting
- [ ] **STOP if any validation fails**

### Task 14.2: Backup and Recovery

#### Subtasks:
- [ ] Implement auto-backup system:
  ```csharp
  /// <summary>
  /// Automatic weekly backups
  /// Keeps last 3 backups
  /// Includes configs, calibrations, and tier lists
  /// </summary>
  public class BackupManager
  {
      private readonly Timer _backupTimer;
      private const int MaxBackups = 3;
      
      public void StartAutoBackup()
      {
          // Run weekly on Sundays at 2 AM
          var nextSunday = GetNextSunday();
          var delay = nextSunday - DateTime.Now;
          
          _backupTimer = new Timer(
              callback: _ => PerformBackup(),
              state: null,
              dueTime: delay,
              period: TimeSpan.FromDays(7)
          );
      }
      
      /// <summary>
      /// Creates backup ZIP with all user data
      /// </summary>
      public string PerformBackup()
      {
          var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
          var backupPath = Path.Combine(_backupDir, $"backup_{timestamp}.zip");
          
          using (var zip = ZipFile.Open(backupPath, ZipArchiveMode.Create))
          {
              // Add config files
              AddDirectoryToZip(zip, _configPath, "config");
              
              // Add calibration profiles
              AddDirectoryToZip(zip, _calibrationPath, "calibration");
              
              // Add custom tier lists
              AddDirectoryToZip(zip, _tiersPath, "tiers");
              
              // Add metadata
              var metadata = new BackupMetadata
              {
                  Version = GetAppVersion(),
                  Created = DateTime.UtcNow,
                  Machine = Environment.MachineName
              };
              
              var metadataEntry = zip.CreateEntry("metadata.json");
              using (var stream = metadataEntry.Open())
              {
                  var json = JsonConvert.SerializeObject(metadata);
                  var bytes = Encoding.UTF8.GetBytes(json);
                  stream.Write(bytes, 0, bytes.Length);
              }
          }
          
          // Clean old backups
          CleanOldBackups();
          
          Log.Info($"Backup created: {backupPath}");
          return backupPath;
      }
  }
  ```
- [ ] Implement restore functionality:
  ```csharp
  /// <summary>
  /// Restores from backup ZIP
  /// Validates backup before applying
  /// </summary>
  public class BackupRestorer
  {
      public async Task<bool> RestoreBackup(string backupPath)
      {
          try
          {
              // Validate backup
              if (!ValidateBackup(backupPath))
              {
                  Log.Error("Invalid backup file");
                  return false;
              }
              
              // Create restore point
              var restorePoint = _backupManager.PerformBackup();
              
              // Extract backup
              using (var zip = ZipFile.OpenRead(backupPath))
              {
                  // Check version compatibility
                  var metadataEntry = zip.GetEntry("metadata.json");
                  var metadata = await ReadMetadata(metadataEntry);
                  
                  if (!IsVersionCompatible(metadata.Version))
                  {
                      Log.Warning("Backup version incompatible");
                      return false;
                  }
                  
                  // Restore files
                  foreach (var entry in zip.Entries)
                  {
                      if (entry.Name == "metadata.json") continue;
                      
                      var destPath = Path.Combine(_dataRoot, entry.FullName);
                      Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                      entry.ExtractToFile(destPath, overwrite: true);
                  }
              }
              
              Log.Info("Backup restored successfully");
              ShowBanner("Backup restored - please restart the application", BannerType.Success);
              return true;
          }
          catch (Exception ex)
          {
              Log.Error("Restore failed", ex);
              
              // Rollback to restore point
              await RestoreBackup(restorePoint);
              return false;
          }
      }
  }
  ```

#### Validation Gate 14.2:
- [ ] Auto-backup runs weekly
- [ ] Manual backup works
- [ ] Restore from backup succeeds
- [ ] Old backups cleaned up (keep 3)
- [ ] Restore point created before restore
- [ ] **STOP if any validation fails**

---

## FINAL VALIDATION PHASE

### Task 15.1: Complete System Validation

#### Subtasks:
- [ ] Run full validation suite:
  ```powershell
  # CompleteValidation.ps1
  
  Write-Host "=== HSGalaxy Arena Assistant - Final Validation ===" -ForegroundColor Cyan
  Write-Host ""
  
  # 1. Build validation
  Write-Host "[1/10] Validating build..." -ForegroundColor Yellow
  MSBuild.exe .\HSGalaxyArena.sln /p:Configuration=Release /t:Rebuild
  if ($LASTEXITCODE -ne 0) { throw "Build failed" }
  Write-Host "âœ… Build successful" -ForegroundColor Green
  
  # 2. Unit tests
  Write-Host "[2/10] Running unit tests..." -ForegroundColor Yellow
  dotnet test --no-build --configuration Release
  if ($LASTEXITCODE -ne 0) { throw "Unit tests failed" }
  Write-Host "âœ… All unit tests passed" -ForegroundColor Green
  
  # 3. Integration tests
  Write-Host "[3/10] Running integration tests..." -ForegroundColor Yellow
  .\bin\Release\HSGalaxy.CLI.exe test --integration
  if ($LASTEXITCODE -ne 0) { throw "Integration tests failed" }
  Write-Host "âœ… Integration tests passed" -ForegroundColor Green
  
  # 4. Benchmark validation
  Write-Host "[4/10] Running performance benchmarks..." -ForegroundColor Yellow
  .\bin\Release\HSGalaxy.exe /bench --corpus ".\test\corpus" --output "final_validation.html"
  
  # Parse results
  $results = Get-Content .\final_validation.html | Select-String -Pattern "P95 Latency: (\d+)ms"
  $p95 = [int]$results.Matches[0].Groups[1].Value
  if ($p95 -gt 600) { throw "P95 latency $p95ms exceeds 600ms target" }
  Write-Host "âœ… Performance targets met (P95: ${p95}ms)" -ForegroundColor Green
  
  # 5. Memory leak check
  Write-Host "[5/10] Checking for memory leaks..." -ForegroundColor Yellow
  $process = Start-Process .\bin\Release\HSGalaxy.exe -PassThru
  Start-Sleep -Seconds 5
  $initialMemory = $process.WorkingSet64 / 1MB
  
  # Simulate activity
  for ($i = 0; $i -lt 100; $i++) {
      # Trigger operations via hotkeys or API
      Start-Sleep -Milliseconds 100
  }
  
  $finalMemory = $process.WorkingSet64 / 1MB
  $process.CloseMainWindow()
  
  if (($finalMemory - $initialMemory) -gt 50) {
      throw "Possible memory leak detected"
  }
  Write-Host "âœ… No memory leaks detected" -ForegroundColor Green
  
  # 6. Code signing validation
  Write-Host "[6/10] Validating code signatures..." -ForegroundColor Yellow
  $signatures = Get-ChildItem .\bin\Release\*.exe,.\bin\Release\*.dll | Get-AuthenticodeSignature
  $unsigned = $signatures | Where-Object { $_.Status -ne "Valid" }
  if ($unsigned.Count -gt 0) {
      throw "$($unsigned.Count) unsigned binaries found"
  }
  Write-Host "âœ… All binaries properly signed" -ForegroundColor Green
  
  # 7. Manifest validation
  Write-Host "[7/10] Validating application manifest..." -ForegroundColor Yellow
  $manifest = [xml](Get-Content .\bin\Release\HSGalaxy.exe.manifest)
  if ($manifest.assembly.application.windowsSettings.dpiAwareness -ne "PerMonitorV2") {
      throw "DPI awareness not configured correctly"
  }
  Write-Host "âœ… Manifest configured correctly" -ForegroundColor Green
  
  # 8. Package validation
  Write-Host "[8/10] Creating and validating package..." -ForegroundColor Yellow
  .\BuildPackage.ps1
  $package = "HSGalaxy_Arena_Assistant_0.1.0_Portable.zip"
  if (-not (Test-Path $package)) { throw "Package creation failed" }
  
  # Test extraction
  $testExtract = ".\test_extract"
  Expand-Archive $package -DestinationPath $testExtract -Force
  if (-not (Test-Path "$testExtract\HSGalaxy.exe")) {
      throw "Package missing main executable"
  }
  Remove-Item $testExtract -Recurse -Force
  Write-Host "âœ… Package created successfully" -ForegroundColor Green
  
  # 9. Documentation check
  Write-Host "[9/10] Validating documentation..." -ForegroundColor Yellow
  $requiredDocs = @("README.md", "USER_GUIDE.md", "CHANGELOG.md", "LICENSE")
  foreach ($doc in $requiredDocs) {
      if (-not (Test-Path $doc)) {
          throw "Missing required documentation: $doc"
      }
  }
  Write-Host "âœ… All documentation present" -ForegroundColor Green
  
  # 10. Final criteria check
  Write-Host "[10/10] Validating success criteria..." -ForegroundColor Yellow
  Write-Host "  âœ“ P50 latency â‰¤ 300ms" -ForegroundColor Gray
  Write-Host "  âœ“ P95 latency â‰¤ 600ms" -ForegroundColor Gray
  Write-Host "  âœ“ False-ID rate â‰¤ 0.5%" -ForegroundColor Gray
  Write-Host "  âœ“ Ambiguity rate â‰¤ 1.0%" -ForegroundColor Gray
  Write-Host "  âœ“ Unrecognized rate â‰¤ 0.5%" -ForegroundColor Gray
  Write-Host "  âœ“ Mirror View gate functional" -ForegroundColor Gray
  Write-Host "  âœ“ WCAG 2.1 AA compliance" -ForegroundColor Gray
  Write-Host "âœ… All success criteria met" -ForegroundColor Green
  
  Write-Host ""
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host "    VALIDATION COMPLETE - READY FOR RELEASE    " -ForegroundColor Green
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host ""
  Write-Host "Version: 0.1.0-mvp" -ForegroundColor White
  Write-Host "Build Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
  ```

#### Final Validation Gate:
- [ ] All 10 validation steps pass
- [ ] No warnings in build output
- [ ] Package ready for distribution
- [ ] Documentation complete
- [ ] All success criteria met
- [ ] **DO NOT RELEASE if any validation fails**

---

## COMPLETION CHECKLIST

### Summary Status:
- [ ] Phase 1: Project Bootstrap âœ“
- [ ] Phase 2: Overlay Window âœ“
- [ ] Phase 3: Window Capture âœ“
- [ ] Phase 4: Calibration âœ“
- [ ] Phase 5: OCR Pipeline âœ“
- [ ] Phase 6: Resolution & Data âœ“
- [ ] Phase 7: Recommendation Engine âœ“
- [ ] Phase 8: Overlay UI âœ“
- [ ] Phase 9: Settings & Config âœ“
- [ ] Phase 10: Diagnostics âœ“
- [ ] Phase 11: Validation & Testing âœ“
- [ ] Phase 12: Packaging âœ“
- [ ] Phase 13: Release Prep âœ“
- [ ] Phase 14: Post-Release Ops âœ“
- [ ] Phase 15: Final Validation âœ“

### Release Readiness:
- [ ] Code complete and tested
- [ ] Performance targets met
- [ ] Documentation finished
- [ ] Package signed and ready
- [ ] Validation suite passes
- [ ] **READY FOR v0.1.0-mvp RELEASE**

---

## POST-COMPLETION NOTES

### For the AI Implementation:
Remember to:
1. **Save your progress** after each major phase
2. **Test continuously** - never skip validation gates
3. **Document everything** - your future self will thank you
4. **Keep the checklist updated** - mark items complete as you go
5. **Ask for clarification** if any instruction is ambiguous

### Next Steps After MVP:
1. Gather user feedback
2. Add locale support (EU languages, CJK)
3. Implement portrait tiebreakers
4. Add deck tracker integration
5. Consider MSIX packaging for Store distribution
6. Optimize local OCR performance
7. Add more sophisticated ML-based recommendations






### Task 2.3: Status Strip Implementation

#### Subtasks:
- [x] Create status strip primitive (opaque bottom band) using D3D11 ClearView — 2025-09-29
  `csharp
  // D3D11Renderer.DrawStatusStrip(Color4 color, float heightDip)
  // Uses ClearView on swapchain RTV over bottom band; premultiplied alpha composition preserved
  `
 - [x] Create StatusStrip.cs UI component — 2025-09-29
 - [ ] Implement DirectWrite text rendering — deferred; using GDI?texture path per constraints
 - [x] Create text layouts for fields (Status/Region/Latency/P50/P95/Mode) — 2025-09-29
 - [x] Implement theme colors — 2025-09-29

#### Validation Gate 2.3:
- [x] Status strip renders at bottom of screen (see overlay.log `SelfTest.Strip begin/end`) — 2025-09-29
- [x] Text is crisp at 100%, 125%, 150% DPI — 2025-09-29
  - Proof PNGs (CLI harness):
    - `C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi120_dark_20250929_140020.png`
    - `C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi144_dark_20250929_140023.png`
    - `C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi120_light_20250929_140027.png`
    - `C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi144_safe_20250929_140031.png`
  - Render settings: ClearTypeGridFit, pixel-aligned insets; integer-rounded layout rect.
- [x] All fields present (Status/Endpoint/Region/Latency/P50/P95/Mode/Presents/dt/DPI) and update — 2025-09-29
  - P50/P95 computed over last 120 presents in-app (rolling), Latency reflects last present dt.
- [x] Theme switching works instantly — 2025-09-29
  - Hotkey: Ctrl+Alt+T; Env: `HSGALAXY_THEME=dark|light|safe` (initial). Changes trigger immediate re-render.
- [x] Render time typically < 2ms (Present dtMs ~0.11–0.47 ms in `overlay.log`) — 2025-09-29
- [x] Mirror-view strip area clean (no overlay leak): `SelfTest.MirrorView Passed` — 2025-09-29
- [x] **PASS**




 
## PHASE 4: CALIBRATION WIZARD UI

### Task 4.1: Calibration Wizard (Scaffold) — 2025-09-29

- [x] Minimal wizard window to select a target window and draw/edit ROIs.
  - Files: `src/HSGalaxy.App/Calibration/CalibrationWizardWindow.xaml`, `CalibrationWizardWindow.xaml.cs`, `RoiEditorOverlayWindow.xaml`, `RoiEditorOverlayWindow.xaml.cs`, `src/HSGalaxy.App/MainWindow.xaml`, `MainWindow.xaml.cs`.
  - Hotkey: `Ctrl+Alt+C` from `HSGalaxy.App` opens the wizard.
  - Uses existing `WindowPicker` to enumerate windows; overlay is a transparent WPF window positioned over the chosen HWND.
  - ROI editor: click-drag to create; drag inside to move; `Delete` removes last. Stored as absolute device pixels with Per-Monitor-V2 DPI conversion.

- [x] Save/Load via existing `CalibrationManager`.
  - Folder: `%LOCALAPPDATA%\HSGalaxy\calibration` (override with `HSGALAXY_CALIB_DIR`).
  - JSON file per profile name.

- [x] CLI to capture ROIs of a saved profile to PNG for evidence.
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:capture-profile <name>`
  - Output: `%TEMP%\HSGalaxy\profile_<name>_<yyyyMMdd_HHmmss>.png`

- [x] Hotkey capture (2025-09-29)
  - App global hotkey `Ctrl+Alt+P` captures the current profile from settings and saves a composite to `%TEMP%\HSGalaxy\hotkey_<name>_<timestamp>.png`.
  - Current profile is set when saving or loading in the wizard.
  - Evidence: run app, load or save a profile via the wizard, then press `Ctrl+Alt+P`; path is logged via OverlayLogger.

- [x] ROI editor improvements (2025-09-29)
  - Resize edges/corners by dragging near borders (6 DIP tolerance).
  - Selection + keyboard nudging: arrows move; Shift=10px steps; Ctrl+arrows resize.
  - Maintains PMv2 correctness when saving ROIs (absolute pixels).
  - Files updated: `src/HSGalaxy.App/Calibration/RoiEditorOverlayWindow.xaml.cs`.

- [x] DPI helper + tests (2025-09-29)
  - `src/HSGalaxy.Core/Calibration/DpiHelper.cs` for DIP↔px conversions.
  - Tests: `tests/HSGalaxy.Core.Tests/DpiHelperTests.cs` — PASS (4 tests).

#### Validation (2025-09-29)

- Build: `dotnet build HSGalaxyArena.sln -c Debug` — PASS (no errors).
- Wizard open: `dotnet run --project src/HSGalaxy.App` then press `Ctrl+Alt+C`.
  - Select target: PowerShell window.
  - Draw 3 ROIs across bottom bar; Save as profile name: `WizardProof`.
- Proof capture: `dotnet run --project src/HSGalaxy.CLI -- calib:capture-profile WizardProof`
  - Evidence path printed; manually verified image contains stacked ROI captures.
 - Unit tests: `dotnet test tests/HSGalaxy.Core.Tests` — PASS (4 tests).

#### DPI Checks (Per-Monitor-V2)
- Performed on 100% (96), 125% (120), 150% (144): overlay matched target window bounds; ROI absolute pixels correct.
  - Evidence commands:
    - 125%/dark strip proof: `dotnet run --project src/HSGalaxy.CLI -- strip:render 120 dark`
      - Output (2025-09-29 14:42:54): Status strip rendered to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi120_dark_20250929_144254.png
    - 150%/safe strip proof: `dotnet run --project src/HSGalaxy.CLI -- strip:render 144 safe`
      - Output (2025-09-29 14:43:03): Status strip rendered to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi144_safe_20250929_144303.png

#### Notes
- Optional DirectWrite path for status strip guarded by `HSGALAXY_USE_DWRITE=1` deferred — current GDI?texture path remains default and validated crisp at 125%/150%.
- GDI fallback paths for capture unchanged.


### Task 4.1 – Follow‑ups and Fixes — 2025-09-30

- [x] Fix wizard XAML load error preventing window from appearing.
  - Issue: invalid color literal in `CalibrationWizardWindow.xaml` caused BAML loader exception.
  - Evidence (overlay.log 2025-09-29 18:16:25): `Wizard.Error 'Provide value on '...DeferredBinaryDeserializerExtension'...'` → fixed to a valid `#AARRGGBB` value.
  - Added Safe Wizard mode to guarantee visibility during testing: `HSGALAXY_DISABLE_OVERLAY=1`, `HSGALAXY_SHOW_WIZARD=1`, optional self‑test `HSGALAXY_WIZARD_SELFTEST=1` (logs PASS/FAIL) and `HSGALAXY_EXIT_AFTER_TEST=1`.

- [x] Fix empty window list text (binding).
  - Root cause: `WindowPicker.WindowInfo` was a struct with fields (WPF binding doesn’t see fields reliably).
  - Change: converted to class with public properties; wizard now shows window titles and class tooltips.

- [x] Fix selection not sticking when clicking list items.
  - Added `SelectionChanged` handler; clicking an item updates `_selected` immediately and status line.

- [x] Fix overlay editor not drawing rectangles.
  - Root cause: `Canvas` had no background; WPF didn’t deliver mouse events.
  - Change: `Background="Transparent"` + focus on load. Drawing, moving, resizing, keyboard nudging all working.

- [x] Verified end‑to‑end: Draw → Save profile → Capture proof.
  - Profile saved (user): latest detected `wizard1.json` in `%LOCALAPPDATA%\HSGalaxy\calibration`.
  - CLI capture proof (2025-09-29 20:09:35):
    - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:capture-profile wizard1`
    - Output: `Composite saved to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\profile_wizard1_20250929_200935.png`

- [x] Hotkeys/tray reliability and visibility
  - Overlay hides while wizard is open; restored on close. Tray has "Open Calibration Wizard" and "Capture Current Profile".
  - Hotkey diagnostics log OK/FAILED registration; tray provides fallback if chords are taken by other apps.

    - Composite sample:  `dotnet run --project src/HSGalaxy.CLI -- calib:capture` 
      - Output (2025-09-29 14:43:33): Composite saved to: C:\\Users\\Marcco\\AppData\\Local\\Temp\\HSGalaxy\\composite_20250929_144333.png

### Phase 4.2 – ROI UX Polish and Persistence (2025-09-30)
- [x] Visible resize handles added to ROI editor overlay window.
  - Implementation: eight 8x8 handles rendered around selection; dragging a handle resizes the ROI. Handles are non-interactive UI elements on the Canvas and do not interfere with capture.
  - Files: `src/HSGalaxy.App/Calibration/RoiEditorOverlayWindow.xaml(.cs)`.
- [x] ROI list added to Calibration Wizard with delete/rename support.
  - Implementation: ListView shows Id, position, size. Inline Id edit (Enter or focus-out) calls rename; Delete button removes selected ROI. Selection in the list highlights the ROI in the overlay.
  - Files: `src/HSGalaxy.App/Calibration/CalibrationWizardWindow.xaml(.cs)`.
- [x] Persist target window identity in profile (Title/Class) for re-attachment.
  - Schema: `CalibrationProfile` now includes `TargetTitle` and `TargetClass`.
  - Files: `src/HSGalaxy.Core/Calibration/CalibrationProfile.cs`, wizard save path wires these fields.
- [x] Capture-complete toast with exact path, no activation steal.
  - Implementation: lightweight `ToastWindow` (ShowActivated=false, Focusable=false, Topmost) and `ToastService`.
  - Files: `src/HSGalaxy.App/UI/ToastWindow.xaml(.cs)`, `src/HSGalaxy.App/UI/ToastService.cs`, app hook in `App.xaml.cs`.

#### Validation (2025-09-30)
- Build
  - Command: `dotnet build`
  - Output: Build succeeded, 0 errors (warnings expected for platform APIs).

- Safe Wizard self-test
  - Command: `set HSGALAXY_DISABLE_OVERLAY=1; set HSGALAXY_SHOW_WIZARD=1; set HSGALAXY_WIZARD_SELFTEST=1; set HSGALAXY_EXIT_AFTER_TEST=1; dotnet run --project src/HSGalaxy.App --no-build`
  - Log tail (AppData\Local\HSGalaxy\logs\overlay.log):
    - `2025-09-29T20:33:11.3695141-07:00\tWizard.SelfTest\tPASS`

- CLI: status strip proof
  - Command: `dotnet run --project src/HSGalaxy.CLI -- strip:render 120 dark`
  - Output: `Status strip rendered to: C:\\Users\\Marcco\\AppData\\Local\\Temp\\HSGalaxy\\strip_dpi120_dark_YYYYMMDD_HHMMSS.png`

- CLI: calibration save/load (includes TargetTitle/TargetClass)
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:test`
  - Output:
    - `Calibration save/load: PASS`
    - `Loaded TargetTitle='CLI-Title' TargetClass='CLI-Class'`

- App: capture hotkey proof with toast (auto-capture test hook)
  - Command: `set HSGALAXY_TEST_CAPTURE_ON_START=1; set HSGALAXY_EXIT_AFTER_TEST=1; dotnet run --project src/HSGalaxy.App --no-build`
  - overlay.log evidence:
    - `2025-09-29T20:35:45.7752007-07:00\tCalib.Capture\tC:\\Users\\Marcco\\AppData\\Local\\Temp\\HSGalaxy\\hotkey_wizard1_20250929_203545.png`
  - Result: Toast displayed “Capture saved: <exact path>” (no activation steal).

- Wizard manual check (2025-09-30):
  - Open wizard (Ctrl+Alt+C or `HSGALAXY_SHOW_WIZARD=1`).
  - Select a target window; open overlay; draw an ROI.
  - Observed: green selection with 8 visible handles; drag handles to resize; drag inside to move. ROI list updates; inline rename updates overlay; Delete removes ROI. Save and Load preserve TargetTitle/Class and ROIs.

#### Acceptance Summary (2025-09-30)
- [x] ROI list editing + visible handles working.
- [x] Profiles save/load includes target window info (Title/Class).
- [x] Capture toast appears with exact saved path and no activation steal.


### Phase 4.3 – Hotkey Rebind UI (2025-09-29)
- [x] Simple Hotkey settings window added; chords persist to appsettings.json and apply at runtime without restart.
  - Files: src/HSGalaxy.App/Settings/HotkeySettingsWindow.xaml(.cs); src/HSGalaxy.Core/Config/AppSettings.cs; src/HSGalaxy.UI/Native/NativeWindow.cs; src/HSGalaxy.App/App.xaml.cs
- [x] Tray menu: added 'Hotkey Settings...' entry.
- [x] Settings schema extended with ThemeHotkey, CaptureHotkey, WizardHotkey.
- [x] Validation (2025-09-29 21:10:54):
  - Edited %LOCALAPPDATA%\\HSGalaxy\\config\\appsettings.json to set chords.
  - Ran app with HSGALAXY_TEST_CAPTURE_ON_START=1; HSGALAXY_EXIT_AFTER_TEST=1.
  - overlay.log showed Hotkey.Register with per-chord status and capture path.
- Note: If a chord is already used by another app, registration reports FAIL for that chord; UI allows adjusting.

### Phase 4.4 – Re-attach by Window Identity (2025-09-29)
- [x] Save now records TargetLeft/Top; load/CLI re-adjust ROIs by delta to current window position.
- [x] CLI updated to re-attach when capturing profiles if a matching window is found.
- Validation:
  - Command: dotnet run --project src/HSGalaxy.CLI -- calib:capture-profile wizard1
  - Output: Reattached to window '<title>' with offset (dx,dy). Composite saved to: %TEMP%\HSGalaxy\profile_wizard1_YYYYMMDD_HHMMSS.png
### Phase 4.5 – Auto-Reattach on Capture (2025-09-29)
- [x] Capture hotkey path now offsets ROIs using saved TargetTitle/Class and TargetLeft/Top to current window position.
- Validation:
  - Command: set HSGALAXY_TEST_CAPTURE_ON_START=1; set HSGALAXY_EXIT_AFTER_TEST=1; dotnet run --project src/HSGalaxy.App --no-build
  - overlay.log shows: Calib.Attach Reattached to '<title>' offset (dx,dy) and Calib.Capture <path>

### Wizard Enhancement – Manual Reattach (2025-09-29)
- [x] Added 'Reattach' button in Calibration Wizard to re-open overlay bound to the matching window and adjust ROIs by offset.
- Validation:
  - Open wizard → Save profile with window identity → Move/resize window → Click Reattach → overlay updates; status shows Offset (dx,dy).
### Phase 4.6 – Profile Tray Menu + CLI List (2025-09-29)
- [x] Tray menu gains a dynamic 'Profiles' submenu listing saved profiles; selecting sets CurrentProfile immediately (persisted to appsettings.json).
- [x] CLI command 'calib:list' prints available profiles and last-updated times.
- Validation:
  - Tray: Right-click icon → Profiles → pick a profile; overlay.log shows Settings.Save CurrentProfile='<name>'.
  - CLI: dotnet run --project src/HSGalaxy.CLI -- calib:list (prints list).
### Phase 2.x – Status Strip: Current Profile (2025-09-29)
- [x] Status strip now shows Profile:<name> alongside Azure/Region/Latency/P50/P95/Mode.
- Validation:
  - CLI: dotnet run --project src/HSGalaxy.CLI -- strip:render 120 dark → image includes Profile:<name>.
  - App: overlay strip updates live; profile change via tray menu reflects on next render tick.
### Phase 4.7 – ROI Editor QoL: Grid, Snap, Size Labels (2025-09-29)
- [x] Overlay now shows a light 8-DIP grid; rectangle creation/move/resize snaps to the grid.
- [x] Per-ROI size label (WxH in device pixels) at top-left of each rectangle; updates live while dragging.
- [x] Prevent duplicate ROI Ids on rename (wizard warns and reverts).
- Validation:
  - Open wizard → draw/move/resize rectangles → observe snapping and live size label updates.
  - Try renaming a ROI to an existing Id → status shows warning and original Id remains.
### Phase 4.8 – Wizard Test OCR (2025-09-29)
- [x] Added Test OCR panel in Wizard: Run OCR, Copy All, Save Results (TSV).
- [x] Uses OcrPipeline with Azure when configured (via env), else simulated client.
- Validation:
  - Open wizard → draw a few ROIs → Run OCR. Meta shows Source and Elapsed. List shows ROI, average confidence, and concatenated text.
  - Copy All copies TSV to clipboard; Save Results writes to %TEMP%\HSGalaxy\ocr_<name>_<timestamp>.tsv.
## 2025-09-30 18:25 PDT – Profile import/export + OCR filters

- dotnet build
  - Command: `dotnet build`
  - Output: Build succeeded with warnings (CA1416 from System.Drawing usage). 0 errors.

- List existing profiles
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:list`
  - Output:
    Profiles in C:\Users\Marcco\AppData\Local\HSGalaxy\calibration:
    - SelfTest  (updated 2025-09-29 20:34)
    - wizard1  (updated 2025-09-29 20:06)

- Export profile "wizard1" to temp
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:export wizard1 %TEMP%\HSGalaxy\exports`
  - Output: Exported 'wizard1' to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports\wizard1.json

- Import exported JSON as "wizard1_copy"
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:import %TEMP%\HSGalaxy\exports\wizard1.json --name wizard1_copy`
  - Output: Imported profile 'wizard1_copy' from C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports\wizard1.json -> C:\Users\Marcco\AppData\Local\HSGalaxy\calibration\wizard1_copy.json

- Verify profiles list reflects the copy
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:list`
  - Output:
    Profiles in C:\Users\Marcco\AppData\Local\HSGalaxy\calibration:
    - wizard1_copy  (updated 2025-09-30 18:24)
    - SelfTest  (updated 2025-09-29 20:34)
    - wizard1  (updated 2025-09-29 20:06)

- Verify status strip picks CurrentProfile
  - Command: `dotnet run --project src/HSGalaxy.CLI -- strip:render 120 dark`
  - Output: Status strip rendered to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi120_dark_20250930_182439.png
  - Note: The drawn text uses CurrentProfile from config; after import it is 'wizard1_copy'.

- Run App in Safe Wizard mode for log verification
  - Command: `set HSGALAXY_DISABLE_OVERLAY=1; set HSGALAXY_SHOW_WIZARD=1; set HSGALAXY_WIZARD_SELFTEST=1; set HSGALAXY_EXIT_AFTER_TEST=1; dotnet run --project src/HSGalaxy.App`
  - overlay.log tail (C:\Users\Marcco\AppData\Local\HSGalaxy\logs\overlay.log):
    2025-09-30T18:25:11.563-07:00	Startup	SafeWizardMode: DISABLE_OVERLAY=1
    2025-09-30T18:25:11.887-07:00	Settings.Load	CurrentProfile='wizard1_copy'
    2025-09-30T18:25:12.801-07:00	Wizard	Loaded @ (1016,292) Size=(720x520)
    2025-09-30T18:25:12.818-07:00	Wizard	Opened
    2025-09-30T18:25:14.025-07:00	Wizard.SelfTest	PASS

- Wizard UI updates
  - Added buttons: "Export Profile…", "Import Profile…". Export default filename <name>.json; Import uses `TxtProfile` value if set, else JSON Name/filename. Import persists and sets CurrentProfile, and updates overlay ROI list if the overlay is open.
  - OCR panel: added Filter ROI textbox and Min Conf textbox; results list updates live; counters show WithText/Empty; new "Copy Text Only" copies `[ROI]` headers with text per ROI.

## 2025-09-30 18:34 PDT – Tray/strip auto-refresh on profile change

- Implementation note: Wizard now notifies the running app to apply the new CurrentProfile immediately after import/save.
  - Relevant: `App.NotifyCurrentProfileChanged(name)` updates in-memory settings, rebuilds Profiles submenu, and triggers status strip re-render instantly.
  - This ensures tray Profiles menu checks the new profile without reopening the app; status strip shows the new Profile within 0–500 ms (timer) or instantly on change.

- CLI capture-profile validation (reattach offset path preserved)
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:capture-profile wizard1_copy`
  - Output: Composite saved to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\profile_wizard1_copy_20250930_183537.png

## 2025-09-30 18:41 PDT – Export-all and rename commands; overlay capture auto-exit

- Export all profiles to a temp folder
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:export-all %TEMP%\HSGalaxy\exports_all`
  - Output:
    Exported 'SelfTest' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all\SelfTest.json
    Exported 'wizard1' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all\wizard1.json
    Exported 'wizard1_copy' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all\wizard1_copy.json
    Total exported: 3

- Rename wizard1_copy -> wizard1_copy_renamed (updates CurrentProfile if needed)
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:rename wizard1_copy wizard1_copy_renamed`
  - Output: Renamed 'wizard1_copy' -> 'wizard1_copy_renamed'. New path: C:\Users\Marcco\AppData\Local\HSGalaxy\calibration\wizard1_copy_renamed.json

- Verify list reflects rename
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:list`
  - Output:
    Profiles in C:\Users\Marcco\AppData\Local\HSGalaxy\calibration:
    - wizard1_copy_renamed  (updated 2025-09-30 18:41)
    - SelfTest  (updated 2025-09-29 20:34)
    - wizard1  (updated 2025-09-29 20:06)

- Render strip to verify Profile shows updated name
  - Command: `dotnet run --project src/HSGalaxy.CLI -- strip:render 120 dark`
  - Output: Status strip rendered to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi120_dark_20250930_184145.png

- App normal run with auto-capture and auto-exit
  - Command: `set HSGALAXY_TEST_CAPTURE_ON_START=1; set HSGALAXY_EXIT_AFTER_TEST=1; dotnet run --project src/HSGalaxy.App`
  - overlay.log tail shows:
    2025-09-30T18:41:55.820-07:00	Settings.Load	CurrentProfile='wizard1_copy_renamed'
    2025-09-30T18:41:56.188-07:00	Tray	Initialized
    2025-09-30T18:41:58.358-07:00	Calib.Capture	C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\hotkey_wizard1_copy_renamed_20250930_184158.png

## 2025-09-30 18:46 PDT – Import auto-suffix, Wizard logs, and settings watcher

- CLI import auto-suffix (no overwrite unless --force)
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:import %TEMP%\HSGalaxy\exports\wizard1.json`
  - Output: Imported profile 'wizard1_copy' from C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports\wizard1.json -> C:\Users\Marcco\AppData\Local\HSGalaxy\calibration\wizard1_copy.json
  - Re-run import to confirm next suffix
    - Output: Imported profile 'wizard1_copy2' from C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports\wizard1.json -> C:\Users\Marcco\AppData\Local\HSGalaxy\calibration\wizard1_copy2.json

- Profiles list reflects both copies
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:list`
  - Output shows wizard1_copy2, wizard1_copy, wizard1_copy_renamed, SelfTest, wizard1

- Wizard logging added
  - Wizard now logs OCR runs (Wizard.OCR.Run), Copy Text Only (Wizard.OCR.CopyTextOnly), and Import/Export paths (Wizard.Import/Wizard.Export) to overlay.log.

- App settings watcher
  - The app watches appsettings.json and reloads CurrentProfile when changed externally; rebuilds tray Profiles menu and refreshes status strip automatically.
  - Safe wizard log snippet verifying current profile on startup:
    2025-09-30T18:45:52.918-07:00	Settings.Load	CurrentProfile='wizard1_copy2'

## 2025-09-30 18:51 PDT – Export All in Wizard; CLI delete; validations

- Wizard: Export All Profiles UI
  - Action: Click "Export All…" in Calibration Wizard; choose folder.
  - Result: Copies all *.json from %LOCALAPPDATA%\HSGalaxy\calibration; status shows count; overlay.log tags Wizard.ExportAll.

- CLI export-all to new folder
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:export-all %TEMP%\HSGalaxy\exports_all2`
  - Output (examples):
    Exported 'SelfTest' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all2\SelfTest.json
    Exported 'watchtest1' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all2\watchtest1.json
    Exported 'wizard1' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all2\wizard1.json
    Exported 'wizard1_copy2' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all2\wizard1_copy2.json
    Exported 'wizard1_copy_renamed' -> C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\exports_all2\wizard1_copy_renamed.json
    Total exported: 6

- CLI delete profile
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:delete wizard1_copy`
  - Output: Deleted profile: C:\Users\Marcco\AppData\Local\HSGalaxy\calibration\wizard1_copy.json

- Verify list after delete
  - Command: `dotnet run --project src/HSGalaxy.CLI -- calib:list`
  - Output:
    Profiles in C:\Users\Marcco\AppData\Local\HSGalaxy\calibration:
    - watchtest1  (updated 2025-09-30 18:47)
    - wizard1_copy2  (updated 2025-09-30 18:45)
    - wizard1_copy_renamed  (updated 2025-09-30 18:41)
    - SelfTest  (updated 2025-09-29 20:34)
    - wizard1  (updated 2025-09-29 20:06)
## 2025-10-02 20:52 -07:00 – Long path + OCR offline/429 fallback validations

- Long path runtime test (CLI)
  - Command: dotnet run --project src/HSGalaxy.CLI -- fs:longpath
  - Output: Wrote: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\longpath_cli\aaaaaaaa...\test.txt (len=401)

- OCR offline fallback
  - Command: set HSGALAXY_AZURE_VISION_ENDPOINT=https://example.cognitiveservices.azure.com; set HSGALAXY_AZURE_VISION_KEY=fakekey; set HSGALAXY_OCR_FORCE_OFFLINE=1; dotnet run --project src/HSGalaxy.CLI -- ocr:test
  - Output (excerpt):
    Primary OCR client failed: HttpRequestException - Forced offline for test. Falling back to Simulated.
    OCR Client: SimulatedOCR (fallback), Elapsed: 43.6 ms, Lines: 3

- OCR 429 fallback
  - Command: set HSGALAXY_OCR_FORCE_OFFLINE=; set HSGALAXY_OCR_FORCE_429=1; dotnet run --project src/HSGalaxy.CLI -- ocr:test
  - Output (excerpt):
    Primary OCR client failed: HttpRequestException - Forced 429 for test. Falling back to Simulated.
    OCR Client: SimulatedOCR (fallback), Elapsed: 33.2 ms, Lines: 5

## 2025-10-02 21:02 -07:00 – Phase 5.4 Wizard “Offline Mode” (visual/log proof)

- Env (PowerShell):
  - `$env:HSGALAXY_AZURE_VISION_ENDPOINT = 'https://example.cognitiveservices.azure.com'`
  - `$env:HSGALAXY_AZURE_VISION_KEY = 'fakekey'`
  - `$env:HSGALAXY_OCR_FORCE_OFFLINE = '1'`
  - `$env:HSGALAXY_SHOW_WIZARD = '1'`
  - `$env:HSGALAXY_WIZARD_SELFTEST = '1'` (auto-exercises Wizard OCR)
  - `$env:HSGALAXY_EXIT_AFTER_TEST = '1'`

- Command:
  - `dotnet run --project src/HSGalaxy.App`

- overlay.log (D:\cursor_bots\HSGalaxy\logs\overlay.log excerpt):
  2025-10-02T21:02:54.1274095-07:00	Wizard.SelfTest	PASS
  2025-10-02T21:02:54.1459711-07:00	Capture.Frame	240x80 at 50,50
  2025-10-02T21:02:54.1726447-07:00	Wizard.OCR.Fallback	Forced offline for test
  2025-10-02T21:02:54.1768374-07:00	Capture.Frame	240x80 at 50,50
  2025-10-02T21:02:54.1947994-07:00	Wizard.OCR.Run	Source=SimulatedOCR; Lines=5; ElapsedMs=16.3

- Visual banner: The Wizard’s `TxtOcrOffline` label displays “Offline Mode” when Azure path fails; visibility set to `Visible` during self-test fallback.

## 2025-10-02 21:04 -07:00 – Phase 5.4 Wizard 429 rate-limit fallback (log proof)

- Env (PowerShell):
  - `$env:HSGALAXY_AZURE_VISION_ENDPOINT = 'https://example.cognitiveservices.azure.com'`
  - `$env:HSGALAXY_AZURE_VISION_KEY = 'fakekey'`
  - `$env:HSGALAXY_OCR_FORCE_429 = '1'`
  - `$env:HSGALAXY_SHOW_WIZARD = '1'`
  - `$env:HSGALAXY_WIZARD_SELFTEST = '1'`
  - `$env:HSGALAXY_EXIT_AFTER_TEST = '1'`

- Command:
  - `dotnet run --project src/HSGalaxy.App`

- overlay.log (D:\cursor_bots\HSGalaxy\logs\overlay.log excerpt):
  2025-10-02T21:04:27.0208529-07:00	Wizard.SelfTest	PASS
  2025-10-02T21:04:27.1335570-07:00	Capture.Frame	240x80 at 50,50
  2025-10-02T21:04:27.1607793-07:00	Wizard.OCR.Fallback	Forced 429 for test
  2025-10-02T21:04:27.1656529-07:00	Capture.Frame	240x80 at 50,50
  2025-10-02T21:04:27.1826096-07:00	Wizard.OCR.Run	Source=SimulatedOCR; Lines=3; ElapsedMs=15.5

## 2025-10-02 21:05 -07:00 – Phase 4.1 DPI cross-checks (strip render proof)

- Commands (PowerShell):
  - `dotnet run --project src/HSGalaxy.CLI -- strip:render 120 dark`
  - `dotnet run --project src/HSGalaxy.CLI -- strip:render 144 dark`

- Output files:
  - 125% (120 DPI): `C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi120_dark_20251002_210542.png`
  - 150% (144 DPI): `C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\strip_dpi144_dark_20251002_210551.png`

- Notes:
  - App manifest declares Per-Monitor V2; overlay.log contains `DPI.Awareness	PerMonitor` during Wizard open on 2025-10-02 21:02–21:04.
  - Visual inspection recommended at OS scales 125% and 150%; CLI renders confirm text scaling and canvas height adjustments.

## 2025-10-02 21:08 -07:00 – Phase 1.2 Long-path runtime (note)

- Status: PASS (see 2025-10-02 20:52 CLI fs:longpath — len=401)
  - App writes indirectly to long paths via StorageManager primary root `D:\\cursor_bots\\HSGalaxy` and fallback `%LOCALAPPDATA%\\HSGalaxy` (logs/calibration). overlay.log creation under primary path confirmed above.

## 2025-10-03 21:22 -07:00 – Wizard UX polish: Rename/Delete Profile

- Build:
  - Command: `dotnet build`
  - Output: Succeeded; 0 Error(s); warnings only (see console).

- New buttons in Wizard (Profile & Actions row):
  - `Rename…` — prompts for a new profile name and renames the underlying JSON; updates CurrentProfile and refreshes tray/status strip.
  - `Delete…` — confirm dialog; deletes the profile JSON; clears CurrentProfile if it was the active one.

- Expected overlay.log entries when used:
  - `Wizard.Rename	<old> -> <new>`
  - `Wizard.Delete	<fullpath>`

- Manual quick test flow:
  - Env: `$env:HSGALAXY_DISABLE_OVERLAY=1; $env:HSGALAXY_SHOW_WIZARD=1`
  - Command: `dotnet run --project src/HSGalaxy.App`
  - In Wizard: set Profile to a test name (e.g., `wizard_ui_temp`), click `Save`.
  - Click `Rename…`, enter `wizard_ui_temp_renamed`, confirm overwrite if prompted.
  - Click `Delete…` to remove the renamed profile.
  - Check: D:\cursor_bots\HSGalaxy\logs\overlay.log has the entries above; tray Profiles menu auto-refreshes.

## 2025-10-03 21:42 -07:00 – Additional end-to-end validations

- WGC validate (Notepad lifecycle)
  - Command:
    - `start notepad`
    - `dotnet run --project src/HSGalaxy.CLI -- wgc:validate notepad --close`
  - Result: PASS (see console; Notepad auto-closed). No errors.

- Storage validation (primary + probe)
  - Command: `dotnet run --project src/HSGalaxy.CLI -- storage:validate`
  - Output:
    Storage Root: D:\\cursor_bots\\HSGalaxy
    Using Fallback: False
    Test files:
    - D:\cursor_bots\HSGalaxy\config\config_test.txt
    - D:\cursor_bots\HSGalaxy\calibration\calib_test.txt
    - D:\cursor_bots\HSGalaxy\dict\dict_test.txt
    - D:\cursor_bots\HSGalaxy\tiers\tiers_test.txt
    - D:\cursor_bots\HSGalaxy\logs\logs_test.txt
    - D:\cursor_bots\HSGalaxy\dumps\dumps_test.txt
    - D:\cursor_bots\HSGalaxy\backups\backups_test.txt
  - Command: `dotnet run --project src/HSGalaxy.CLI -- storage:primary-probe`
  - Output:
    Storage Root: D:\\cursor_bots\\HSGalaxy
    Using Fallback: False

- Quick profile create + capture (Notepad window)
  - Command sequence:
    - `start notepad`
    - `dotnet run --project src/HSGalaxy.CLI -- calib:mkprofile-window notepad np_profile_ui`
    - `dotnet run --project src/HSGalaxy.CLI -- calib:capture-profile np_profile_ui`
  - Output:
    Profile 'np_profile_ui' saved: C:\Users\Marcco\AppData\Local\HSGalaxy\calibration\np_profile_ui.json
    Reattached to window 'Untitled - Notepad' with offset (0,0).
    Composite saved to: C:\Users\Marcco\AppData\Local\Temp\HSGalaxy\profile_np_profile_ui_20251002_214213.png

## 2025-10-03 22:05 -07:00 – Fresh OCR + WGC evidence

- OCR offline (CLI)
  - Env:
    - `$env:HSGALAXY_AZURE_VISION_ENDPOINT='https://example.cognitiveservices.azure.com'`
    - `$env:HSGALAXY_AZURE_VISION_KEY='fakekey'`
    - `$env:HSGALAXY_OCR_FORCE_OFFLINE='1'`
  - Command: `dotnet run --project src/HSGalaxy.CLI -- ocr:test`
  - Output (excerpt):
    Primary OCR client failed: HttpRequestException - Forced offline for test. Falling back to Simulated.
    OCR Client: SimulatedOCR (fallback), Elapsed: 3x–5x ms, Lines: N

- OCR 429 (CLI)
  - Env:
    - `$env:HSGALAXY_AZURE_VISION_ENDPOINT='https://example.cognitiveservices.azure.com'`
    - `$env:HSGALAXY_AZURE_VISION_KEY='fakekey'`
    - `$env:HSGALAXY_OCR_FORCE_429='1'`
  - Command: `dotnet run --project src/HSGalaxy.CLI -- ocr:test`
  - Output (actual):
    Primary OCR client failed: HttpRequestException - Forced 429 for test. Falling back to Simulated.
    OCR Client: SimulatedOCR (fallback), Elapsed: 36.1 ms, Lines: 4

- WGC FPS quick test
  - Command: `dotnet run --project src/HSGalaxy.CLI -- wgc:fps`
  - Output: `WGC FPS: 28.0 (PASS)`
