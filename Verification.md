# AudioMuralPainter 2.2.1 verification — 2026-11-14

The audit covered application code, Windows interop, settings and diagnostics, build/release tooling, documentation, Git history, and the existing public 2.2.0 ZIP. Findings and source references are recorded in [AUDIT.md](AUDIT.md).

## Repeatable checks

- Optimized build with the Windows .NET Framework compiler and warnings treated as errors.
- 182 automated assertions passed. The complete result is in Checks.txt. Coverage includes DSP, bounded and adversarial XML, local/private preset fields, atomic-save failures, native array limits, hotkeys, renderer caches, calibrated scope axes, reset definitions, editor bindings, and threshold capture loss.
- 128 seeded boundary configurations were rendered as part of those checks. This is bounded regression fuzzing, not exhaustive input exploration.
- All seven settings pages build their expected controls. The settings snapshot now renders actual controls; an empty-form image is a test failure.
- Unknown commands and diagnostic I/O failures exit with the expected nonzero status.
- Formatter idempotency, whitespace checks, and the checkout privacy-pattern guardrail passed.
- Four ordinary render snapshots were unchanged by the final structural cleanup. The scope-axis corrections intentionally change relevant grid/label positions.

Run tools/Verify.ps1 from a Git checkout. Self-tests use synthetic audio, fake hotkey registration, and invisible UI controls; they do not capture live playback.

## Windows desktop checks

The isolated tools/DesktopChecks.ps1 harness passed 11 checks on the normal desktop:

- Actual layered/transparent window flags for clear and opaque modes.
- Disabling click-through and restoring ordinary opaque interaction.
- Temporarily unlocking interaction for settings and restoring the saved choice on close.
- Recreating the native window handle while preserving recovery and click-through.
- Cancelling a close while retaining a usable window, settings, and recovery behavior.
- Repeatedly rebuilding every settings page and opening/closing settings.

After 12 warm-up settings cycles, another 12 cycles left GDI handles at 84 → 84 and USER handles at 30 → 30 after collection. Each cycle visited all seven pages. Tests used isolated preferences and a temporary Ctrl+Alt+Shift+F9 registration.

The earlier 2.2.0 manual checks verified real mouse clicks reaching a separate process, global recovery, custom hotkeys, modal focus, and tray actions. Those historical checks are not represented as new manual tests of 2.2.1. The current desktop harness checks registration, native state, and lifecycle.

## Performance and limits

Three alternating original/final synthetic renderer trials showed no material regression in this workload. See [Performance.md](Performance.md) for timings and configuration. Offscreen rendering excludes audio capture, desktop presentation, and GPU composition; it cannot establish live click-through CPU cost.

No exhaustive DPI/multi-monitor matrix, long-duration soak, driver/kernel attack, or independent penetration test was performed. The executable remains unsigned. WinForms timer delivery remains best effort; earlier desktop measurements at a 60 FPS setting reached roughly 31–32 FPS.

No software was installed, no PATH/startup/service entries were added, and no Windows security policies were changed. Personal settings remain separate from source and release packages.
