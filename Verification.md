# AudioMuralPainter 2.2 verification — November 14, 2026

Final executable SHA-256: C5A08B5045C82287767343AFC0FC18CE825F2D43F4AFBE8B4534D3F931B4397C

- Build: existing Windows .NET Framework compiler, optimized, warnings treated as errors; successful.
- Automated checks: 101 passed on the final source/build. Full results are in Checks.txt.
- Normal desktop launch: successful. Windows reserved Ctrl+Alt+F1 and enabled click-through.
- Cross-process mouse input: enabled click-through, closed settings, and clicked an ordinary temporary click-counter window underneath AMP. Its count increased while the overlay remained visible. Repeated on the final build, from 3 to 4 clicks.
- Global recovery: with the temporary window focused, Ctrl+Alt+F1 opened AMP settings. The saved click-through choice remained checked while mouse interaction was paused for settings. Rechecked after the final executable was launched.
- Customization: the user entered Ctrl+Alt+F8 in the editor and applied it. The agent verified that the replacement opened settings from the separate focused window, then restored Ctrl+Alt+F1 with the per-row (default) link and Apply. Default recovery was verified again afterward.
- Modal focus: fixed recovery to raise the hotkey editor when it is already open. On the final build, recovery from the temporary window returned focus to the editor's global-shortcut field.
- Tray: the user tested Make AMP clickable and Open settings and reported that both worked on retry. Explorer automation was denied, so tray interaction was performed by the user, not automated.
- Reported exit: the user initially saw AMP disappear while testing the tray. The agent had closed AMP to replace its executable while that manual check was pending, so the disappearance may have been that planned exit. A crash could not be confirmed or excluded: Windows denied this session permission to read the Application event log. The user reported a successful retry.
- Failure handling: the earlier restricted test process received Access denied (Win32 5) from RegisterHotKey. AMP displayed the error and kept click-through disabled. The normal desktop launch succeeded without changing Windows security settings.
- Cleanup: the temporary click-counter window was closed. The final AMP build remains running. The recovery binding is back to Ctrl+Alt+F1; the current user-selected click-through setting is retained.

Audio.cs, Renderer.cs, RenderEngine.cs, NativeDrawing.cs, and Benchmark.cs remain identical to the optimized 2.1 source. Earlier renderer benchmarks are retained in Performance.md; they are not new measurements of 2.2. Opaque click-through uses the layered renderer and can have a different cost from ordinary opaque mode.

No software was installed, no PATH/startup/service entries were added, and no Windows security settings were modified. Appearance preferences were backed up before desktop testing. The distributable ZIP excludes personal settings and presets; a fresh extraction starts with defaults and click-through off.
