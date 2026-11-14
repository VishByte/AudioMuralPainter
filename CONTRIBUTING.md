# Working on AudioMuralPainter

The application targets the Windows 11 .NET Framework runtime and builds with the Windows C# compiler. It has no third-party runtime packages. Keep source compatible with that compiler; a newer SDK is not required to build or run AMP.

From a Git checkout:

```powershell
./Source/Build.ps1 -OutputDirectory ./work/build
./tools/Verify.ps1
```

`Verify.ps1` checks the checkout for sensitive-data patterns, builds with warnings as errors, runs the automated suite, and checks failing command-line paths. It writes only to its output directory. Self-tests use synthetic samples and do not start playback capture.

For formatting, use PowerShell 7 (which includes Roslyn):

```powershell
./tools/Format.ps1
./tools/Format.ps1 -Check
```

The formatter checks that C# token kinds and text remain unchanged. CI also checks formatting. Keep meaningful names and explanatory comments around native contracts; avoid compressed statements. Prefer targeted changes over broad architecture rewrites.

## Architecture and ownership

- `Loopback.cs`: WASAPI capture and bounded sample history. Capture runs on its own thread; snapshots synchronize access to the ring buffer. `PcmDecoder.cs`, `Spectrum.cs`, and `WindowsAudioInterop.cs` isolate sample decoding, FFT analysis, and native contracts.
- `Preferences.cs`: defaults, validation, copying, and resets. `PreferencesStore.cs` owns bounded XML parsing and atomic persistence; `Presets.cs` owns the named library. Mark local-only fields with `LocalPreference`: both appearance export and import preservation use that one definition. Validate imports before using values in controls, allocations, or native calls.
- `RenderEngine.cs` and `Renderer.cs`: drawing, coordinates, and meter math. Reuse resources in the frame loop and invalidate caches when their inputs change. Axis labels and the plotted data must use the same scale.
- `LayeredSurface.cs` and `NativeDrawing.cs`: Windows drawing boundaries. Validate native array counts, check window-state changes, retain managed owners for handles, and preserve the public drawing fallback.
- `Program.cs` dispatches startup and diagnostic commands. `Visualizer.cs` owns the window, frame scheduling, and shutdown; `DesktopIntegration.cs` owns tray and recovery. Click-through must never be enabled without a registered recovery shortcut. Recreated windows must re-establish registration and presentation state. A cancelled close must leave resources usable.
- `Settings.cs` owns settings navigation and lifecycle. Its partial-class companions `SettingsPages.cs` and `SettingsControls.cs` hold page definitions and reusable editors. `PreferenceBinding.cs` resolves typed field selectors outside the frame loop. Preview controls and the hotkey dialog have separate files.
- `UiResources.cs` owns each form's shared fonts. Dispose them after child controls. Keep audio, rendering, and persistence out of editor factories.
- `Tests.cs` and `SecurityTests.cs`: repeatable signal, rendering, persistence, interaction, and adversarial-input checks.

Editor selectors use actual model fields, so renames are compiler-checked. Regression checks also verify that each visual preference belongs to a valid, unique reset section. Hotkey field metadata and copy/serialization reflection are developer-defined; imported XML cannot supply arbitrary type or method names. The drawing fast path's reflection is optional and must retain a tested public API fallback.

When adding a preference, define its default and validation, decide whether it is local-only, wire its typed editor and reset section, and invalidate affected renderer caches. Keep schema changes backward compatible. Add a behavioral regression test when a change affects parsing, rendering, persistence, or interaction.

## Desktop and performance verification

```powershell
./tools/DesktopChecks.ps1
```

This optional test needs a normal Windows desktop. It opens temporary windows, reserves Ctrl+Alt+Shift+F9, checks real window flags and recovery, recreates the window handle, cancels a close, and repeatedly opens/closes settings. It writes preferences beside its isolated test executable. It exits with failure when the desktop or shortcut is unavailable rather than claiming a pass.

Use `AudioMuralPainter.exe --benchmark report.tsv settings.xml` for repeatable offscreen renderer comparisons. That benchmark excludes window presentation. Click-through performance needs a live or controlled-input desktop comparison. Record window size, DPI, audio input, appearance, frame rate, trial order, and whether timings include capture/presentation. A configured FPS limit is not a guarantee of achieved FPS; WinForms timer delivery depends on Windows scheduling.

## Packaging

```powershell
./tools/Package.ps1 -OutputDirectory ./work/release
```

Packaging builds and tests first, then copies named documents and Git-visible source files into a fresh staging folder. It never archives the whole working directory, excludes ignored scratch files, and refuses to overwrite an existing ZIP. The portable package includes source and its build script; development tools and CI live in the Git checkout. Do not include personal settings, presets, credentials, debug symbols, generated test fixtures, or `.git`. Review the final archive and publish a new version rather than replacing an existing release asset silently.

CI runs on Windows with a read-only repository token, no persisted checkout credentials, and an official checkout action pinned to an exact commit. Keep workflow dependencies pinned and review updates. Do not use production secrets for pull-request builds.
