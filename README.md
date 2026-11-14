# AudioMuralPainter 2.2 (AMP)

Portable Windows 11 playback visualizer. Open **AudioMuralPainter.exe**. The main window shows only the visuals; press **F1** for settings. The full product name is AudioMuralPainter, with **AMP** used in the tray and compact headings.

No installer, driver, package download, PATH entry, service, startup task, account, or microphone is needed. The app uses the existing Windows .NET Framework and WASAPI playback loopback. It is unsigned and runs without administrator privileges.

## Building a source checkout

From the repository folder, run `./Source/Build.ps1` in PowerShell. It creates **AudioMuralPainter.exe** beside this README using the Windows .NET Framework C# compiler; no package restore is required. Run `./AudioMuralPainter.exe` to launch the app.

Personal **Amp.settings.xml** preferences, saved **Presets**, compiled executables, and ZIP archives are excluded from Git. A fresh source checkout uses the built-in defaults. To run the automated checks without opening the visualizer, use `./AudioMuralPainter.exe --self-test work/checks.txt`.

Version 2.2 adds optional mouse click-through, a global settings/recovery shortcut, a tray menu, and customizable keyboard shortcuts. It retains the optimized 2.1 audio/rendering pipeline. The earlier performance measurements are in **Performance.md**; those results are not new measurements of this release. An opaque background with click-through uses the layered renderer, so its cost can differ from the ordinary opaque mode.

## Controls and recovery

| Default shortcut or action | Result |
| --- | --- |
| **Ctrl+Alt+F1** (global) | Open settings and temporarily unlock mouse interaction, even while another app has focus |
| F1 (focused) | Show or hide settings |
| Esc | Close settings/dialogs, or leave fullscreen |
| F11 / Alt+Enter (focused) | Toggle fullscreen |
| Ctrl+Q (focused) | Quit AMP |
| Alt+F4 | Close the active window; quits when the visualizer is active |
| Drag inside the visualizer | Move the window, when clickable |
| Drag an edge or corner | Resize, when clickable |
| Double-click inside the visualizer | Toggle fullscreen, when clickable |
| Right-click the **AMP tray icon** | Open the tray menu |
| Double-click the **AMP tray icon** | Open settings and temporarily unlock interaction |

The tray menu includes **Open settings**, **Make AMP clickable**, a click-through toggle, **Customize hotkeys…**, and **Quit AMP**. Windows may place AMP inside the tray's hidden-icons area.

Enable **Display → Mouse click-through** to send mouse input to windows underneath AMP, including over the visible bars and waveform. Background opacity and click-through are independent. Enable **Always on top** if the visualizer should remain visible above the windows receiving those clicks.

**Ctrl+Alt+F1 is the default recovery shortcut.** Opening settings pauses click-through while those settings remain open. Closing settings restores the checkbox's saved choice. **Make AMP clickable** in the tray permanently turns the checkbox off until you enable it again.

AMP enables click-through only after Windows successfully reserves the global recovery shortcut. If another app owns the shortcut, or Windows denies registration, AMP stays clickable and explains the error in settings. Choose a different global shortcut with **Customize hotkeys…**. Changing bindings first reserves the replacement; if it fails, the old working shortcut remains active. Running a second AMP instance may cause a shortcut conflict.

## Customizing and resetting

Select **Display → Customize hotkeys…**, or use the tray menu. Click a shortcut field and press the desired keys, then select **Apply**. The global shortcut requires Ctrl or Alt. Other shortcuts apply only while AMP or its settings have focus. Backspace clears an optional shortcut; every row has a clickable **(default)** reset. Duplicate bindings and reserved navigation combinations are rejected. Esc, Tab, Alt+F4, and Windows security shortcuts retain their usual roles.

Global shortcut registration exists only while AMP is running. AMP uses the native Windows [RegisterHotKey API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey); it does not install a keyboard hook or record keystrokes. Shortcuts and overlays may be unavailable on secure desktops or in exclusive fullscreen applications. The tray remains an alternative recovery route on the normal desktop.

Visual parameters also have clickable **(default)** resets, section resets, and **Reset visual settings**. Appearance presets and Reset visual settings preserve window geometry, hotkeys, and the click-through choice. Resetting the Display section turns click-through off. Settings include a live example preview.

## Display

Choose the oscilloscope, frequency bars, or both. Adjust background opacity from clear to black while the visuals retain their brightness. When click-through is off, the clear setting retains an almost invisible input surface (alpha 1/255), so you can focus or drag the window anywhere.

Outer padding and the gap between views use fixed logical pixels rather than percentages of the window size. The default margin is 8 px. Windows display scaling is respected. You can also set the scope/bars height split, window size, maximum refresh rate, and always on top. The app starts windowed.

Transparency and click-through use native per-pixel [layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features). If Windows cannot present the layered surface, AMP returns to a clickable solid background and displays the error in settings.

## Bars

Set the minimum and maximum frequencies, logarithmic or linear spacing, number of bars, and FFT resolution. The effective maximum is limited to 49% of the playback sample rate; displayed labels always reflect the effective range. Large FFT sizes improve low-frequency detail at the cost of a longer analysis window and more CPU use.

The meter floor controls which level maps to the bottom of the display. The optional minimum response cutoff uses the incoming per-band level **before sensitivity gain**. A soft cutoff width of 0 dB gives a hard cutoff; larger values fade responses near the cutoff. Release smoothing still governs the decay of already-visible bars. Disable dim baseline segments for completely empty space at silence.

Customize bar spacing, solid or segmented bars, segment height and gap, baseline visibility and opacity, and peak markers.

## Oscilloscope and response

Adjust the displayed time span, maximum waveform points, line thickness, and glow. Sensitivities can be linked or set independently for the scope and bars. Bar attack and release are separate, with peak hold and peak fall timing also adjustable. Time-based movement stays consistent across refresh rates.

## Colors and gradients

Set the low, middle, and peak bar colors using a color picker or a six-digit hex value. Color transition points can be entered in **percent** or **dBFS**, or dragged in the threshold preview. Both units edit the same stored positions. Threshold dBFS represents the displayed level **after gain**, using the current meter floor; this differs from the incoming-level cutoff.

Choose level colors or a smooth gradient for bars, and solid color or a gradient for the scope. Gradients run vertically bottom-to-top or horizontally left-to-right; the reverse toggle flips either direction.

Gradient coverage:

- **Each bar:** repeats the gradient within each bar; the scope uses its own plot and palette.
- **Bars view:** spreads the bar palette over the entire bars plot; the scope still uses its own plot and palette.
- **Both views:** shares the bar palette continuously across the scope, gap, and bars. Select Gradient for both color styles to see a continuous gradient across both views.

The scope has its own start, middle, and end colors for local gradients. Its solid color is also its local gradient start. Vertical gradients stay anchored to full plot height as levels rise and fall. Bar gradient stops follow the configurable color transition points. Scope-local gradients use evenly spaced stops.

## Labels and guides

Separate toggles control bar frequency labels, scope time labels, scope amplitude scale, and each view's grid. Customize text/grid color, label size, label opacity, maximum horizontal label count, and grid opacity. Label counts adapt to the available width.

Scope time labels run from the negative displayed duration to 0 ms. The amplitude scale uses normalized sample amplitude from −1 to +1 after scope sensitivity gain. Digital dBFS values are not physical sound-pressure measurements.

## Presets and persistence

Built-in presets: Default, Minimal scope, Classic meters, Continuous gradient, and Glass. Save your own named setups beside the app in **Presets**, or import/export a preset XML file. Applying a preset preserves your window size, position, hotkeys, and click-through choice.

Preferences are saved beside AudioMuralPainter.exe as **Amp.settings.xml** when settings close or AMP exits; tray interaction and hotkey changes also save immediately. The legacy filename is retained for compatibility. Keep AMP in a writable folder. To migrate an older installation, copy its Amp.settings.xml and Presets folder beside the new executable. Older preferences remain readable and new controls receive safe defaults. Presets/settings never save audio.

## Audio and privacy

AMP follows the default Windows playback device, combines channels into a mono analysis signal, and reconnects when that device changes or becomes unavailable. Audio is processed in memory and is not saved or transmitted. Protected or exclusive-mode audio may not be available to [WASAPI loopback](https://learn.microsoft.com/en-us/windows/win32/coreaudio/loopback-recording).

## Source and checks

All source is in **Source**. **Source\Build.ps1** builds **AudioMuralPainter.exe** with the existing Windows .NET Framework C# compiler and warnings treated as errors. There are no third-party packages.

Desktop integration, hotkey validation/registration, the hotkey editor, and product metadata are separate modules. Audio and rendering retain their existing responsibilities. The internal Amp namespace, icon asset filenames, and settings filename remain short for compatibility.

`AudioMuralPainter.exe --self-test <report-path>` runs the automated checks and saves diagnostic PNGs beside the report. `--probe-audio <report-path>` checks playback capture without opening a window. `--benchmark <report-path> <settings-path>` runs the synthetic rendering benchmark. `--input-test-target` opens a temporary click-counter window for manual overlay testing; it does not start audio capture or register shortcuts.

Verification: **101 automated checks passed**, covering signal processing, settings migration, presets, rendering, transparency, gradients, thresholds, labels, optimized buffers/caches, shortcut parsing and validation, registration replacement/rollback, interaction policy, window-style flags, and resource cleanup. Fake registrations isolate the automated tests from your real global shortcuts; those tests do not establish that a particular shortcut is available on your PC. See **Checks.txt** and **Verification.md** for the desktop test status.

The fast array drawing path uses the same Windows GDI+ operations with existing .NET Framework graphics handles, with the public drawing API as a fallback. No drivers, packages, services, or PATH entries are installed. Windows security settings were not changed.
